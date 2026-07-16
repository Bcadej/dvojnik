namespace FileExplorerClone;

/// <summary>Which column a pane is sorted by.</summary>
public enum FileSortField
{
    Name,
    Size,
    Type,
    Modified
}

/// <summary>
/// Orders a listing the way Explorer does: folders always first, then the chosen column.
/// Used both for a pane's own list and for the shared row order in Sync View.
/// </summary>
public static class FileSorter
{
    public static IEnumerable<FileSystemItem> Sort(
        IEnumerable<FileSystemItem> items, FileSortField field, bool ascending)
    {
        // Folders stay above files regardless of direction — flipping that around is
        // disorienting, and it is what Explorer does too.
        var ordered = items.OrderByDescending(i => i.IsDirectory);

        return field switch
        {
            FileSortField.Name => ascending
                ? ordered.ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
                : ordered.ThenByDescending(i => i.Name, StringComparer.CurrentCultureIgnoreCase),

            FileSortField.Size => ascending
                ? ordered.ThenBy(i => i.Size).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
                : ordered.ThenByDescending(i => i.Size).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),

            FileSortField.Type => ascending
                ? ordered.ThenBy(i => i.TypeLabel, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
                : ordered.ThenByDescending(i => i.TypeLabel, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),

            FileSortField.Modified => ascending
                ? ordered.ThenBy(i => i.Modified).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
                : ordered.ThenByDescending(i => i.Modified).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),

            _ => ordered.ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
        };
    }
}
