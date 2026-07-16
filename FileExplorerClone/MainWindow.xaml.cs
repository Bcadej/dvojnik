using System.IO;
using System.Windows;

namespace FileExplorerClone;

public partial class MainWindow : Window
{
    private bool _syncEnabled;
    private bool _isSyncNavigating; // guard against feedback loops
    private string? _leftSyncRoot;
    private string? _rightSyncRoot;

    // The status line is built in code, so remember how to rebuild it when the
    // language changes rather than leaving stale text on screen.
    private Func<string>? _statusBuilder;

    public MainWindow()
    {
        InitializeComponent();

        LeftPane.Navigated += OnPaneNavigated;
        RightPane.Navigated += OnPaneNavigated;

        LanguageManager.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => LanguageManager.LanguageChanged -= OnLanguageChanged;

        SyncLanguageMenuChecks();

        // Initial compare pass once both panes have loaded their startup folder.
        Loaded += (_, _) => CompareAndHighlight();
    }

    // ----- Language -----

    private void SlovenianMenuItem_Click(object sender, RoutedEventArgs e)
        => LanguageManager.Apply(LanguageManager.Slovenian);

    private void EnglishMenuItem_Click(object sender, RoutedEventArgs e)
        => LanguageManager.Apply(LanguageManager.English);

    private void OnLanguageChanged()
    {
        SyncLanguageMenuChecks();
        UpdateSyncToggleCaption();
        SetStatus(_statusBuilder);
    }

    private void SyncLanguageMenuChecks()
    {
        SlovenianMenuItem.IsChecked = LanguageManager.CurrentLanguage == LanguageManager.Slovenian;
        EnglishMenuItem.IsChecked = LanguageManager.CurrentLanguage == LanguageManager.English;
    }

    private void SetStatus(Func<string>? builder)
    {
        _statusBuilder = builder;
        StatusText.Text = builder?.Invoke() ?? "";
    }

    private void UpdateSyncToggleCaption()
        => SyncToggle.Content = Strings.Get(_syncEnabled ? "Toolbar_SyncOn" : "Toolbar_SyncOff");

    // ----- Help -----

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        => new AboutWindow { Owner = this }.ShowDialog();

    // ----- Sync / swap -----

    private void SyncToggle_Changed(object sender, RoutedEventArgs e)
    {
        _syncEnabled = SyncToggle.IsChecked == true;

        if (_syncEnabled)
        {
            // Anchor both panes' current locations as the "roots" for relative sync.
            _leftSyncRoot = LeftPane.CurrentPath;
            _rightSyncRoot = RightPane.CurrentPath;
            SetStatus(() => Strings.Get("Status_SyncOn"));
        }
        else
        {
            SetStatus(null);
        }

        UpdateSyncToggleCaption();

        // Pads the panes when switching on, and strips the padding when switching off.
        CompareAndHighlight();
    }

    private void SwapButton_Click(object sender, RoutedEventArgs e)
    {
        var leftPath = LeftPane.CurrentPath;
        var rightPath = RightPane.CurrentPath;

        _isSyncNavigating = true;
        LeftPane.NavigateTo(rightPath);
        RightPane.NavigateTo(leftPath);
        _isSyncNavigating = false;

        if (_syncEnabled)
        {
            (_leftSyncRoot, _rightSyncRoot) = (_rightSyncRoot, _leftSyncRoot);
        }

        CompareAndHighlight();
    }

    private void OnPaneNavigated(ExplorerPane source, string newPath)
    {
        if (_isSyncNavigating) return;

        if (_syncEnabled && _leftSyncRoot != null && _rightSyncRoot != null)
        {
            _isSyncNavigating = true;
            try
            {
                bool sourceIsLeft = ReferenceEquals(source, LeftPane);
                var sourceRoot = sourceIsLeft ? _leftSyncRoot : _rightSyncRoot;
                var targetRoot = sourceIsLeft ? _rightSyncRoot : _leftSyncRoot;
                var targetPane = sourceIsLeft ? RightPane : LeftPane;

                var relative = Path.GetRelativePath(sourceRoot, newPath);
                var targetPath = relative == "." ? targetRoot : Path.Combine(targetRoot, relative);

                if (Directory.Exists(targetPath))
                {
                    targetPane.NavigateTo(targetPath);
                    SetStatus(() => Strings.Get("Status_InSync"));
                }
                else
                {
                    SetStatus(() => Strings.Format("Status_NoMatchingFolder", relative));
                }
            }
            finally
            {
                _isSyncNavigating = false;
            }
        }

        CompareAndHighlight();
    }

    // Compares the two panes' current listings by name, highlights differences, and pads
    // each side with blank rows so matching entries sit level with each other.
    // Only runs while Sync View is on, since that's the "compare folders" mode.
    private void CompareAndHighlight()
    {
        if (!_syncEnabled)
        {
            ClearHighlights();
            LeftPane.ShowRealItems();
            RightPane.ShowRealItems();
            return;
        }

        // Always compare the real contents — never a previously padded listing.
        var left = LeftPane.RealItems;
        var right = RightPane.RealItems;

        var leftByName = BuildNameLookup(left);
        var rightByName = BuildNameLookup(right);

        foreach (var item in left) item.CompareState = ClassifyAgainst(item, rightByName);
        foreach (var item in right) item.CompareState = ClassifyAgainst(item, leftByName);

        AlignPanes(left, right);
    }

    // Folders on a case-sensitive-enabled volume can hold both "A.txt" and "a.txt", which
    // would make ToDictionary throw on the duplicate key. First one wins instead.
    private static Dictionary<string, FileSystemItem> BuildNameLookup(IReadOnlyList<FileSystemItem> items)
    {
        var map = new Dictionary<string, FileSystemItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items) map.TryAdd(item.Name, item);
        return map;
    }

    /// <summary>
    /// Builds one shared row order from both listings and hands each pane a padded copy:
    /// where a pane has no entry for a row, it gets a blank spacer instead. The result is
    /// that an item present on both sides appears at the same height in both panes.
    /// </summary>
    private void AlignPanes(IReadOnlyList<FileSystemItem> left, IReadOnlyList<FileSystemItem> right)
    {
        // Key on kind + name so a folder never lines up with a file of the same name.
        var leftLookup = BuildRowLookup(left);
        var rightLookup = BuildRowLookup(right);

        // Directories first, then files, each alphabetically — matching how a pane sorts
        // its own listing, so the order is unsurprising when Sync View goes on.
        var order = leftLookup.Keys
            .Union(rightLookup.Keys)
            .OrderByDescending(k => k.IsDirectory)
            .ThenBy(k => k.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var leftRows = new List<FileSystemItem>(order.Count);
        var rightRows = new List<FileSystemItem>(order.Count);

        foreach (var key in order)
        {
            leftRows.Add(leftLookup.TryGetValue(key, out var l) ? l : FileSystemItem.Placeholder());
            rightRows.Add(rightLookup.TryGetValue(key, out var r) ? r : FileSystemItem.Placeholder());
        }

        LeftPane.ShowAligned(leftRows);
        RightPane.ShowAligned(rightRows);
    }

    private static Dictionary<RowKey, FileSystemItem> BuildRowLookup(IReadOnlyList<FileSystemItem> items)
    {
        var map = new Dictionary<RowKey, FileSystemItem>();
        foreach (var item in items) map.TryAdd(RowKey.For(item), item);
        return map;
    }

    /// <summary>Identifies a row across the two panes: same kind and same name.</summary>
    private readonly record struct RowKey(bool IsDirectory, string Name)
    {
        public static RowKey For(FileSystemItem item) => new(item.IsDirectory, item.Name);

        public bool Equals(RowKey other) =>
            IsDirectory == other.IsDirectory &&
            string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() =>
            HashCode.Combine(IsDirectory, Name.ToUpperInvariant());
    }

    private static CompareState ClassifyAgainst(FileSystemItem item, Dictionary<string, FileSystemItem> other)
    {
        if (!other.TryGetValue(item.Name, out var match))
            return CompareState.OnlyHere;

        if (match.IsDirectory != item.IsDirectory)
            return CompareState.OnlyHere; // name collision but different kind — treat as mismatch

        if (!item.IsDirectory && (match.Size != item.Size || match.Modified != item.Modified))
            return CompareState.Differs;

        return CompareState.Normal;
    }

    private void ClearHighlights()
    {
        foreach (var item in LeftPane.RealItems) item.CompareState = CompareState.Normal;
        foreach (var item in RightPane.RealItems) item.CompareState = CompareState.Normal;
    }
}
