using System.ComponentModel;
using System.IO;

namespace FileExplorerClone;

public enum CompareState
{
    Normal,     // no comparison running at all — Sync View is off
    Identical,  // present on both sides, matching size and modified date
    OnlyHere,   // exists in this pane but not the other
    Differs     // same name exists on both sides but size/date differ
}

public class FileSystemItem : INotifyPropertyChanged
{
    public string Name { get; init; } = "";
    public string FullPath { get; init; } = "";
    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public DateTime Modified { get; init; }

    /// <summary>
    /// A blank spacer row. In Sync View each pane is padded with these wherever the other
    /// pane has an entry it lacks, so equivalent rows sit at the same height on both sides.
    /// Placeholders are not real files and must never be opened, dragged or deleted.
    /// </summary>
    public bool IsPlaceholder { get; init; }

    public string TypeLabel => IsPlaceholder
        ? ""
        : IsDirectory
            ? Strings.Get("Item_FileFolder")
            : (Path.GetExtension(Name) is { Length: > 1 } ext
                ? Strings.Format("Item_FileSuffix", ext.TrimStart('.').ToUpperInvariant())
                : Strings.Get("Item_File"));

    public string SizeLabel => IsPlaceholder || IsDirectory ? "" : FormatSize(Size);

    /// <summary>Date shown in the list, formatted for the current language.</summary>
    public string ModifiedLabel => IsPlaceholder ? "" : DateFormats.FormatDateTime(Modified);

    public string Icon => IsPlaceholder ? "" : IsDirectory ? "\U0001F4C1" : "\U0001F4C4"; // folder / file glyph

    /// <summary>
    /// True when the name ends with a space or a dot. Windows tolerates such names on some
    /// file systems and WebDAV shares create them, but its path normalisation silently trims a
    /// trailing space/dot off the last path segment — so an ordinary delete or rename ends up
    /// targeting the wrong name and fails. Flagged so the user can rename the file to fix it.
    /// </summary>
    public bool HasProblematicName =>
        !IsPlaceholder && Name.Length > 0 && (Name[^1] == ' ' || Name[^1] == '.');

    /// <summary>Explains the <see cref="HasProblematicName"/> flag; null (no tooltip) when the name is fine.</summary>
    public string? NameWarningTooltip =>
        HasProblematicName ? Strings.Get("Warn_TrailingSpaceDot") : null;

    private CompareState _compareState = CompareState.Normal;
    public CompareState CompareState
    {
        get => _compareState;
        set
        {
            if (_compareState != value)
            {
                _compareState = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CompareState)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Re-reads the labels that depend on the UI language.</summary>
    public void RefreshLocalisedLabels()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TypeLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModifiedLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameWarningTooltip)));
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.#} {units[unit]}";
    }

    /// <summary>
    /// WPF uses this for a row's UI Automation name, so screen readers announce the file
    /// name rather than the type name. Spacer rows deliberately announce nothing.
    /// </summary>
    public override string ToString() => IsPlaceholder ? "" : Name;

    /// <summary>An empty row used to align the two panes in Sync View.</summary>
    public static FileSystemItem Placeholder() => new() { IsPlaceholder = true };

    /// <summary>
    /// Removes trailing NUL characters from a name or path. The Windows WebDAV (DavWWWRoot)
    /// redirector returns every enumerated entry with a stray trailing U+0000 that is not part
    /// of the real name. Left in, it makes every path contain a null character, so every file
    /// API — delete, rename, copy — fails with "Null character in path". The real file has no
    /// null, so stripping it here gives a name that all operations can actually use.
    /// </summary>
    private static string StripNul(string value) => value.TrimEnd('\0');

    public static FileSystemItem FromDirectory(DirectoryInfo dir) => new()
    {
        Name = StripNul(dir.Name),
        FullPath = StripNul(dir.FullName),
        IsDirectory = true,
        Modified = dir.LastWriteTime
    };

    public static FileSystemItem FromFile(FileInfo file) => new()
    {
        Name = StripNul(file.Name),
        FullPath = StripNul(file.FullName),
        IsDirectory = false,
        Size = file.Length,
        Modified = file.LastWriteTime
    };
}
