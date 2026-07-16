using System.ComponentModel;
using System.IO;

namespace FileExplorerClone;

public enum CompareState
{
    Normal,     // no comparison active, or identical on both sides
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

    public static FileSystemItem FromDirectory(DirectoryInfo dir) => new()
    {
        Name = dir.Name,
        FullPath = dir.FullName,
        IsDirectory = true,
        Modified = dir.LastWriteTime
    };

    public static FileSystemItem FromFile(FileInfo file) => new()
    {
        Name = file.Name,
        FullPath = file.FullName,
        IsDirectory = false,
        Size = file.Length,
        Modified = file.LastWriteTime
    };
}
