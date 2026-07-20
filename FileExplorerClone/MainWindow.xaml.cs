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

        LeftPane.SortChanged += OnPaneSortChanged;
        RightPane.SortChanged += OnPaneSortChanged;

        LeftPane.SwitchPaneRequested += OnSwitchPaneRequested;
        RightPane.SwitchPaneRequested += OnSwitchPaneRequested;

        LanguageManager.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => LanguageManager.LanguageChanged -= OnLanguageChanged;

        SyncLanguageMenuChecks();

        // Initial compare pass once both panes have loaded their startup folder.
        Loaded += (_, _) =>
        {
            CompareAndHighlight();

            // Start with the keyboard already in the left pane's list.
            LeftPane.FocusList();
        };
    }

    /// <summary>Tab in one pane's list moves the keyboard to the other pane.</summary>
    private void OnSwitchPaneRequested(ExplorerPane source)
        => (ReferenceEquals(source, LeftPane) ? RightPane : LeftPane).FocusList();

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

    // ----- Sorting -----

    /// <summary>
    /// Aligned panes must share one row order, so in Sync View a sort on either side is
    /// mirrored to the other. Unsynced, each pane sorts independently.
    /// </summary>
    private void OnPaneSortChanged(ExplorerPane source)
    {
        if (_syncEnabled)
        {
            var other = ReferenceEquals(source, LeftPane) ? RightPane : LeftPane;
            other.SetSort(source.SortField, source.SortAscending);
        }

        CompareAndHighlight();
    }

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

            // Aligned rows only make sense if both sides share a sort order.
            RightPane.SetSort(LeftPane.SortField, LeftPane.SortAscending);

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

        SyncPeerPaths();
        CompareAndHighlight();
    }

    /// <summary>
    /// Tells each pane where the other one is, so their breadcrumb bars can dim the part of the
    /// path they share and highlight the step where they diverge.
    /// </summary>
    private void SyncPeerPaths()
    {
        LeftPane.SetPeerPath(RightPane.CurrentPath);
        RightPane.SetPeerPath(LeftPane.CurrentPath);
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

        // One representative item per row — whichever side has it — so the shared order can
        // be produced by the same sorter a pane uses on its own listing.
        var representatives = leftLookup.Keys
            .Union(rightLookup.Keys)
            .Select(k => leftLookup.TryGetValue(k, out var l) ? l : rightLookup[k]);

        // Both panes are kept on the same sort while synced, so either one's is fine.
        var order = FileSorter.Sort(representatives, LeftPane.SortField, LeftPane.SortAscending).ToList();

        var leftRows = new List<FileSystemItem>(order.Count);
        var rightRows = new List<FileSystemItem>(order.Count);

        foreach (var rep in order)
        {
            var key = RowKey.For(rep);
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

        return CompareState.Identical;
    }

    private void ClearHighlights()
    {
        foreach (var item in LeftPane.RealItems) item.CompareState = CompareState.Normal;
        foreach (var item in RightPane.RealItems) item.CompareState = CompareState.Normal;
    }
}
