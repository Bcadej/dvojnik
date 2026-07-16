using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VbFileIO = Microsoft.VisualBasic.FileIO;

namespace FileExplorerClone;

public partial class ExplorerPane : UserControl
{
    /// <summary>What the list actually shows — real entries, plus blank spacers in Sync View.</summary>
    public ObservableCollection<FileSystemItem> Items { get; } = new();

    /// <summary>
    /// The folder's real contents, without any alignment spacers. Comparison and alignment
    /// always work from this, so padding never feeds back into the next comparison.
    /// </summary>
    public IReadOnlyList<FileSystemItem> RealItems => _realItems;
    private readonly List<FileSystemItem> _realItems = new();

    /// <summary>Distinguishes the two panes so each gets its own saved shortcut bar.</summary>
    public string PaneId { get; set; } = "Pane";

    /// <summary>Column this pane is sorted by, and in which direction.</summary>
    public FileSortField SortField { get; private set; } = FileSortField.Name;
    public bool SortAscending { get; private set; } = true;

    /// <summary>Raised when the user clicks a column header, so Sync View can match sides.</summary>
    public event Action<ExplorerPane>? SortChanged;

    public ObservableCollection<ShortcutItem> Shortcuts { get; } = new();
    private bool _shortcutsLoaded;

    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private bool _suppressHistoryPush;

    // Drag origin, used to tell a click apart from the start of a drag.
    private Point _dragStart;
    private bool _mayStartDrag;

    public string CurrentPath { get; private set; } = "";

    // Fired whenever this pane successfully navigates. Used by MainWindow for sync mode.
    public event Action<ExplorerPane, string>? Navigated;

    /// <summary>
    /// Raised after any pane changes the file system, so every open pane can refresh —
    /// a drop into one pane usually also changes what the other one should show.
    /// </summary>
    private static event Action? FileSystemChanged;

    // Simple shared clipboard for copy/cut across panes.
    private static readonly List<string> ClipboardPaths = new();
    private static bool _clipboardIsCut;

    public ExplorerPane()
    {
        InitializeComponent();
        FileListView.ItemsSource = Items;
        ShortcutList.ItemsSource = Shortcuts;
        Shortcuts.CollectionChanged += (_, _) => UpdateShortcutHint();

        FileSystemChanged += OnFileSystemChanged;
        LanguageManager.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) =>
        {
            FileSystemChanged -= OnFileSystemChanged;
            LanguageManager.LanguageChanged -= OnLanguageChanged;
        };

        // PaneId is assigned from XAML after the constructor runs, so the saved
        // shortcuts can only be loaded once the control is up.
        Loaded += (_, _) =>
        {
            if (_shortcutsLoaded) return;
            _shortcutsLoaded = true;
            foreach (var s in ShortcutStore.Load(PaneId)) Shortcuts.Add(s);
            UpdateShortcutHint();
            UpdateSortArrows();
        };

        NavigateTo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    private void OnFileSystemChanged() => Refresh();

    private void OnLanguageChanged()
    {
        foreach (var item in Items) item.RefreshLocalisedLabels();
    }

    public void NavigateTo(string path)
    {
        if (!Directory.Exists(path))
        {
            MessageBox.Show(Strings.Format("Msg_PathNotFound", path), Strings.Get("Msg_Navigate_Title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var dirInfo = new DirectoryInfo(path);
            var entries = new List<FileSystemItem>();

            foreach (var d in dirInfo.GetDirectories().OrderBy(d => d.Name))
            {
                try { entries.Add(FileSystemItem.FromDirectory(d)); } catch { /* skip inaccessible */ }
            }
            foreach (var f in dirInfo.GetFiles().OrderBy(f => f.Name))
            {
                try { entries.Add(FileSystemItem.FromFile(f)); } catch { /* skip inaccessible */ }
            }

            _realItems.Clear();
            _realItems.AddRange(FileSorter.Sort(entries, SortField, SortAscending));

            // Show the plain listing; MainWindow re-pads it afterwards if Sync View is on.
            ShowRealItems();

            CurrentPath = dirInfo.FullName;
            AddressBar.Text = CurrentPath;

            if (!_suppressHistoryPush)
            {
                // Trim forward history when navigating fresh
                if (_historyIndex < _history.Count - 1)
                    _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
                _history.Add(CurrentPath);
                _historyIndex = _history.Count - 1;
            }

            Navigated?.Invoke(this, CurrentPath);
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(Strings.Get("Msg_AccessDenied"), Strings.Get("Msg_Navigate_Title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Re-reads the current folder without touching the history stack.</summary>
    public void Refresh()
    {
        if (!string.IsNullOrEmpty(CurrentPath)) GoTo(CurrentPath, pushHistory: false);
    }

    /// <summary>Shows the folder's real contents, with no alignment spacers.</summary>
    public void ShowRealItems()
    {
        // Already showing exactly these rows — don't rebuild and throw away the selection.
        if (Items.SequenceEqual(_realItems)) return;

        Items.Clear();
        foreach (var item in _realItems) Items.Add(item);
    }

    /// <summary>Shows a padded listing supplied by MainWindow so both panes line up.</summary>
    public void ShowAligned(IEnumerable<FileSystemItem> aligned)
    {
        Items.Clear();
        foreach (var item in aligned) Items.Add(item);
    }

    // ----- Sorting -----

    private void ColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader { Tag: string tag }) return;
        if (!Enum.TryParse<FileSortField>(tag, out var field)) return;

        // Same column again reverses; a different column starts ascending.
        if (field == SortField) SortAscending = !SortAscending;
        else { SortField = field; SortAscending = true; }

        ApplySort();
        SortChanged?.Invoke(this);
    }

    /// <summary>Applies a sort chosen elsewhere (the other pane, while in Sync View).</summary>
    public void SetSort(FileSortField field, bool ascending)
    {
        if (SortField == field && SortAscending == ascending) return;

        SortField = field;
        SortAscending = ascending;
        ApplySort();
    }

    private void ApplySort()
    {
        var sorted = FileSorter.Sort(_realItems, SortField, SortAscending).ToList();
        _realItems.Clear();
        _realItems.AddRange(sorted);

        // In Sync View, MainWindow re-pads over the top of this straight afterwards.
        ShowRealItems();
        UpdateSortArrows();
    }

    private void UpdateSortArrows()
    {
        var arrow = SortAscending ? "▲" : "▼"; // ▲ / ▼

        NameSortArrow.Text = SortField == FileSortField.Name ? arrow : "";
        SizeSortArrow.Text = SortField == FileSortField.Size ? arrow : "";
        TypeSortArrow.Text = SortField == FileSortField.Type ? arrow : "";
        ModifiedSortArrow.Text = SortField == FileSortField.Modified ? arrow : "";
    }

    private void GoTo(string path, bool pushHistory)
    {
        _suppressHistoryPush = !pushHistory;
        NavigateTo(path);
        _suppressHistoryPush = false;
    }

    // ----- Toolbar handlers -----

    private void BackButton_Click(object sender, RoutedEventArgs e) => GoBack();

    private void ForwardButton_Click(object sender, RoutedEventArgs e) => GoForward();

    private void UpButton_Click(object sender, RoutedEventArgs e) => GoUp();

    /// <summary>
    /// Moves to the parent folder and selects the folder just left, so you can walk up and
    /// down a tree without losing your place.
    /// </summary>
    public void GoUp()
    {
        var parent = Directory.GetParent(CurrentPath);
        if (parent == null) return;

        var cameFrom = Path.GetFileName(Path.TrimEndingDirectorySeparator(CurrentPath));
        NavigateTo(parent.FullName);
        SelectByName(cameFrom);
    }

    private void GoBack()
    {
        if (_historyIndex <= 0) return;
        _historyIndex--;
        GoTo(_history[_historyIndex], pushHistory: false);
    }

    private void GoForward()
    {
        if (_historyIndex >= _history.Count - 1) return;
        _historyIndex++;
        GoTo(_history[_historyIndex], pushHistory: false);
    }

    /// <summary>Selects a row by name and scrolls it into view, if it is there.</summary>
    private void SelectByName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return;

        var match = Items.FirstOrDefault(i =>
            !i.IsPlaceholder && string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
        if (match == null) return;

        FileListView.SelectedItem = match;
        FileListView.ScrollIntoView(match);
        FocusSelectedRow();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => Refresh();

    private void GoButton_Click(object sender, RoutedEventArgs e) => NavigateTo(AddressBar.Text.Trim());

    private void AddressBar_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) NavigateTo(AddressBar.Text.Trim());
    }

    // ----- List handlers -----

    /// <summary>Selected rows, excluding the blank spacers that Sync View pads with.</summary>
    private List<FileSystemItem> SelectedRealItems() =>
        FileListView.SelectedItems.Cast<FileSystemItem>().Where(i => !i.IsPlaceholder).ToList();

    private void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileListView.SelectedItem is FileSystemItem { IsPlaceholder: false } item)
            OpenItem(item);
    }

    // ----- Keyboard -----

    /// <summary>
    /// Keyboard browsing. Up/Down/Home/End/type-ahead come free from the ListView; blank
    /// spacer rows are skipped because their containers are not focusable.
    ///
    /// Bound to the list rather than the window so that typing in the address bar keeps
    /// working normally (Backspace especially). Tunnelling (Preview) rather than bubbling,
    /// because the ListView's inner ScrollViewer swallows Left/Right to scroll sideways and
    /// would eat Alt+Left/Right before they ever reached us.
    /// </summary>
    private void FileListView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // With a modifier held, WPF reports Key.System and puts the real key in SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        switch (key)
        {
            // --- Browsing ---
            case Key.Enter:
                if (FileListView.SelectedItem is FileSystemItem { IsPlaceholder: false } item)
                    OpenItem(item);
                e.Handled = true;
                break;

            case Key.Back:                       // Backspace: up one level
                GoUp();
                e.Handled = true;
                break;

            case Key.Up when alt:                // Alt+Up: up one level (Explorer's binding)
                GoUp();
                e.Handled = true;
                break;

            case Key.Left when alt:              // Alt+Left / Alt+Right: history
                GoBack();
                e.Handled = true;
                break;

            case Key.Right when alt:
                GoForward();
                e.Handled = true;
                break;

            case Key.Tab:                        // Tab: jump to the other pane
                SwitchPaneRequested?.Invoke(this);
                e.Handled = true;
                break;

            // --- Actions ---
            case Key.F5:
                Refresh();
                e.Handled = true;
                break;

            case Key.F2:
                RenameSelected();
                e.Handled = true;
                break;

            case Key.Delete:
                DeleteSelected();
                e.Handled = true;
                break;

            case Key.F7:                         // F7 / Ctrl+Shift+N: new folder
                NewFolder();
                e.Handled = true;
                break;

            case Key.N when ctrl && shift:
                NewFolder();
                e.Handled = true;
                break;

            case Key.C when ctrl:
                CopySelection(cut: false);
                e.Handled = true;
                break;

            case Key.X when ctrl:
                CopySelection(cut: true);
                e.Handled = true;
                break;

            case Key.V when ctrl:
                PasteHere();
                e.Handled = true;
                break;
        }
    }

    /// <summary>Raised when the user asks to move to the other pane (Tab).</summary>
    public event Action<ExplorerPane>? SwitchPaneRequested;

    /// <summary>Puts keyboard focus on this pane's list, selecting a row if none is current.</summary>
    public void FocusList()
    {
        if (FileListView.SelectedItem is not FileSystemItem { IsPlaceholder: false })
        {
            var first = Items.FirstOrDefault(i => !i.IsPlaceholder);
            if (first != null) FileListView.SelectedItem = first;
        }

        FileListView.Focus();
        FocusSelectedRow();
    }

    /// <summary>Moves focus onto the selected row's container so the arrow keys act on it.</summary>
    private void FocusSelectedRow()
    {
        if (FileListView.SelectedItem is null) return;

        FileListView.ScrollIntoView(FileListView.SelectedItem);

        // The container may not exist yet if the list was just rebuilt.
        if (FileListView.ItemContainerGenerator.ContainerFromItem(FileListView.SelectedItem)
            is ListViewItem row)
        {
            row.Focus();
        }
        else
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (FileListView.SelectedItem is not null &&
                    FileListView.ItemContainerGenerator.ContainerFromItem(FileListView.SelectedItem)
                        is ListViewItem later)
                    later.Focus();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void OpenItem(FileSystemItem item)
    {
        if (item.IsPlaceholder) return;

        if (item.IsDirectory)
        {
            NavigateTo(item.FullPath);
        }
        else
        {
            try
            {
                Process.Start(new ProcessStartInfo(item.FullPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(Strings.Format("Msg_CouldNotOpen", ex.Message), Strings.Get("Msg_Open_Title"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ----- Drag and drop -----

    private void FileListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        // Only arm a drag when the press landed on a real row (never a blank spacer).
        _mayStartDrag = FindItemUnder(e.OriginalSource as DependencyObject) is { IsPlaceholder: false };
    }

    private void FileListView_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_mayStartDrag || e.LeftButton != MouseButtonState.Pressed) return;

        var delta = e.GetPosition(null) - _dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var paths = SelectedRealItems().Select(i => i.FullPath).ToArray();
        if (paths.Length == 0) return;

        _mayStartDrag = false;

        // FileDrop is the shell's own format, so this also drops onto Windows Explorer.
        // Link must be allowed too, otherwise a drop on our own shortcut bar (which answers
        // with Link) gets downgraded to None and silently refused.
        var data = new DataObject(DataFormats.FileDrop, paths);
        DragDrop.DoDragDrop(FileListView, data,
            DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);

        // The drop target may have moved files out from under us.
        FileSystemChanged?.Invoke();
    }

    private void FileListView_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? EffectForModifiers(e.KeyStates)
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>Plain drag copies; holding SHIFT moves.</summary>
    private static DragDropEffects EffectForModifiers(DragDropKeyStates keyStates)
        => keyStates.HasFlag(DragDropKeyStates.ShiftKey) ? DragDropEffects.Move : DragDropEffects.Copy;

    private void FileListView_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] sources || sources.Length == 0) return;

        e.Handled = true;

        bool isMove = EffectForModifiers(e.KeyStates) == DragDropEffects.Move;
        e.Effects = isMove ? DragDropEffects.Move : DragDropEffects.Copy;

        // Dropping onto a folder row targets that folder; anywhere else — including a blank
        // spacer row — targets this pane's current folder.
        var overItem = FindItemUnder(e.OriginalSource as DependencyObject);
        var targetDir = overItem is { IsDirectory: true, IsPlaceholder: false }
            ? overItem.FullPath
            : CurrentPath;

        if (string.IsNullOrEmpty(targetDir)) return;

        bool changed = false;

        foreach (var source in sources)
        {
            try
            {
                if (!TransferOne(source, targetDir, isMove)) continue;
                changed = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Strings.Format("Msg_TransferFailed", Path.GetFileName(source), ex.Message),
                    Strings.Get("Msg_Transfer_Title"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        if (changed) FileSystemChanged?.Invoke();
    }

    /// <summary>
    /// Copies or moves one path into <paramref name="targetDir"/>.
    /// Returns false when the transfer is a no-op that should be silently skipped.
    /// </summary>
    private bool TransferOne(string source, string targetDir, bool isMove)
    {
        bool isDir = Directory.Exists(source);
        if (!isDir && !File.Exists(source)) return false;

        var sourceParent = Path.GetDirectoryName(source);

        // Moving something into the folder it already lives in is a no-op.
        if (isMove && PathsEqual(sourceParent, targetDir)) return false;

        // Moving a folder into itself (or its own subtree) would destroy it.
        if (isDir && IsSameOrSubPath(targetDir, source))
        {
            MessageBox.Show(Strings.Get("Msg_CannotDropOntoItself"), Strings.Get("Msg_Transfer_Title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var dest = UniqueDestination(Path.Combine(targetDir, Path.GetFileName(source)));

        if (isMove)
        {
            if (isDir) Directory.Move(source, dest);
            else File.Move(source, dest);
        }
        else
        {
            if (isDir) CopyDirectoryRecursive(source, dest);
            else File.Copy(source, dest, overwrite: false);
        }

        return true;
    }

    /// <summary>Appends " (2)", " (3)" … when the name is already taken, as Explorer does.</summary>
    private static string UniqueDestination(string desired)
    {
        if (!File.Exists(desired) && !Directory.Exists(desired)) return desired;

        var dir = Path.GetDirectoryName(desired)!;
        var stem = Path.GetFileNameWithoutExtension(desired);
        var ext = Path.GetExtension(desired);

        for (int n = 2; ; n++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({n}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }

    private static bool PathsEqual(string? a, string? b)
    {
        if (a is null || b is null) return false;
        return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when <paramref name="candidate"/> is <paramref name="root"/> itself or sits inside it.</summary>
    private static bool IsSameOrSubPath(string candidate, string root)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        var rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        if (string.Equals(full, rootFull, StringComparison.OrdinalIgnoreCase)) return true;

        return full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Walks up the visual tree from a hit-test result to the row's data item.</summary>
    private static FileSystemItem? FindItemUnder(DependencyObject? source)
    {
        while (source != null && source is not ListViewItem)
        {
            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return (source as ListViewItem)?.DataContext as FileSystemItem;
    }

    // ----- Shortcut bar -----

    private void UpdateShortcutHint()
        => ShortcutHint.Visibility = Shortcuts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Dropping on the bar creates a shortcut — it never copies or moves anything.</summary>
    private void ShortcutBar_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = ShortcutDropEffect(e);
        e.Handled = true;
    }

    /// <summary>
    /// Link is the honest answer for "make a shortcut", but the drag source decides what is
    /// allowed. If it doesn't offer Link we report Copy — never Move, which would make the
    /// source (Explorer, say) delete the original after we merely noted its path.
    /// </summary>
    private static DragDropEffects ShortcutDropEffect(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return DragDropEffects.None;

        if (e.AllowedEffects.HasFlag(DragDropEffects.Link)) return DragDropEffects.Link;
        if (e.AllowedEffects.HasFlag(DragDropEffects.Copy)) return DragDropEffects.Copy;

        return DragDropEffects.None;
    }

    private void ShortcutBar_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;

        e.Handled = true;
        e.Effects = ShortcutDropEffect(e);

        AddShortcuts(paths);
    }

    /// <summary>Adds paths to this pane's bar, skipping duplicates and dead paths.</summary>
    private void AddShortcuts(IEnumerable<string> paths)
    {
        bool added = false;

        foreach (var path in paths)
        {
            // Ignore anything already on this bar.
            if (Shortcuts.Any(s => string.Equals(s.FullPath, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            var item = ShortcutItem.FromPath(path);
            if (item == null) continue;

            Shortcuts.Add(item);
            added = true;
        }

        if (added) ShortcutStore.Save(PaneId, Shortcuts);
    }

    private void Shortcut_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ShortcutItem shortcut) return;

        if (shortcut.IsDirectory && Directory.Exists(shortcut.FullPath))
        {
            NavigateTo(shortcut.FullPath);
            return;
        }

        if (!shortcut.IsDirectory && File.Exists(shortcut.FullPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo(shortcut.FullPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(Strings.Format("Msg_CouldNotOpen", ex.Message), Strings.Get("Msg_Open_Title"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return;
        }

        // Target is gone — say so and drop the dead shortcut.
        MessageBox.Show(Strings.Format("Shortcut_Missing", shortcut.FullPath), Strings.Get("Shortcut_Title"),
            MessageBoxButton.OK, MessageBoxImage.Warning);
        RemoveShortcut(shortcut);
    }

    /// <summary>Context-menu route onto the shortcut bar, as an alternative to dragging.</summary>
    private void AddToShortcutsMenuItem_Click(object sender, RoutedEventArgs e)
        => AddShortcuts(SelectedRealItems().Select(i => i.FullPath));

    /// <summary>Relabels a shortcut. The target is untouched — only the button's caption changes.</summary>
    private void RenameShortcut_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ShortcutItem shortcut) return;

        var newName = PromptDialog.Show(Strings.Get("Shortcut_Rename"),
            Strings.Get("Shortcut_RenameLabel"), shortcut.Name);

        // Cancelled — leave it alone. (An empty box means "go back to the default name".)
        if (newName is null) return;

        // Storing the default name as a custom one would only freeze it in place.
        shortcut.CustomName = newName.Trim() == shortcut.DefaultName ? null : newName;
        ShortcutStore.Save(PaneId, Shortcuts);
    }

    private void RemoveShortcut_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ShortcutItem shortcut)
            RemoveShortcut(shortcut);
    }

    private void RemoveAllShortcuts_Click(object sender, RoutedEventArgs e)
    {
        if (Shortcuts.Count == 0) return;
        Shortcuts.Clear();
        ShortcutStore.Save(PaneId, Shortcuts);
    }

    private void RemoveShortcut(ShortcutItem shortcut)
    {
        Shortcuts.Remove(shortcut);
        ShortcutStore.Save(PaneId, Shortcuts);
    }

    // ----- Context menu handlers -----

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (FileListView.SelectedItem is FileSystemItem { IsPlaceholder: false } item) OpenItem(item);
    }

    private void RenameMenuItem_Click(object sender, RoutedEventArgs e) => RenameSelected();

    private void RenameSelected()
    {
        if (FileListView.SelectedItem is not FileSystemItem { IsPlaceholder: false } item) return;

        var newName = PromptDialog.Show(Strings.Get("Rename_Title"), Strings.Get("Rename_Label"), item.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;

        try
        {
            var newPath = Path.Combine(Path.GetDirectoryName(item.FullPath)!, newName);
            if (item.IsDirectory) Directory.Move(item.FullPath, newPath);
            else File.Move(item.FullPath, newPath);
            FileSystemChanged?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show(Strings.Format("Msg_RenameFailed", ex.Message), Strings.Get("Rename_Title"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e) => DeleteSelected();

    private void DeleteSelected()
    {
        var selected = SelectedRealItems();
        if (selected.Count == 0) return;

        var result = MessageBox.Show(Strings.Format("Msg_Delete_Confirm", selected.Count),
            Strings.Get("Menu_Delete"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        foreach (var item in selected)
        {
            try
            {
                if (item.IsDirectory)
                    VbFileIO.FileSystem.DeleteDirectory(item.FullPath, VbFileIO.UIOption.OnlyErrorDialogs,
                        VbFileIO.RecycleOption.SendToRecycleBin);
                else
                    VbFileIO.FileSystem.DeleteFile(item.FullPath, VbFileIO.UIOption.OnlyErrorDialogs,
                        VbFileIO.RecycleOption.SendToRecycleBin);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Strings.Format("Msg_CouldNotDelete", item.Name, ex.Message),
                    Strings.Get("Menu_Delete"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        FileSystemChanged?.Invoke();
    }

    private void CopyMenuItem_Click(object sender, RoutedEventArgs e) => CopySelection(cut: false);

    private void CutMenuItem_Click(object sender, RoutedEventArgs e) => CopySelection(cut: true);

    private void CopySelection(bool cut)
    {
        var paths = SelectedRealItems().Select(i => i.FullPath).ToList();
        if (paths.Count == 0) return;

        ClipboardPaths.Clear();
        ClipboardPaths.AddRange(paths);
        _clipboardIsCut = cut;
    }

    private void PasteMenuItem_Click(object sender, RoutedEventArgs e) => PasteHere();

    private void PasteHere()
    {
        if (ClipboardPaths.Count == 0) return;

        foreach (var srcPath in ClipboardPaths)
        {
            try
            {
                var name = Path.GetFileName(srcPath);
                var destPath = Path.Combine(CurrentPath, name);
                bool isDir = Directory.Exists(srcPath);

                if (_clipboardIsCut)
                {
                    if (isDir) Directory.Move(srcPath, destPath);
                    else File.Move(srcPath, destPath);
                }
                else
                {
                    if (isDir) CopyDirectoryRecursive(srcPath, destPath);
                    else File.Copy(srcPath, destPath, overwrite: false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Strings.Format("Msg_PasteFailed", srcPath, ex.Message), Strings.Get("Menu_Paste"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        if (_clipboardIsCut) ClipboardPaths.Clear();
        FileSystemChanged?.Invoke();
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: false);
        foreach (var subDir in Directory.GetDirectories(sourceDir))
            CopyDirectoryRecursive(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
    }

    private void NewFolderMenuItem_Click(object sender, RoutedEventArgs e) => NewFolder();

    private void NewFolder()
    {
        var name = PromptDialog.Show(Strings.Get("NewFolder_Title"), Strings.Get("NewFolder_Label"),
            Strings.Get("NewFolder_Default"));
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            Directory.CreateDirectory(Path.Combine(CurrentPath, name));
            FileSystemChanged?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show(Strings.Format("Msg_CouldNotCreateFolder", ex.Message), Strings.Get("NewFolder_Title"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
