using System.ComponentModel;
using System.IO;

namespace FileExplorerClone;

/// <summary>
/// One entry on a pane's shortcut bar: a remembered folder or file the user dropped there,
/// optionally under a name of their own choosing.
/// </summary>
public class ShortcutItem : INotifyPropertyChanged
{
    public string FullPath { get; init; } = "";
    public bool IsDirectory { get; init; }

    private string? _customName;

    /// <summary>User-chosen label. Null means "use the file or folder name".</summary>
    public string? CustomName
    {
        get => _customName;
        set
        {
            var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (_customName == trimmed) return;

            _customName = trimmed;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CustomName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    /// <summary>What the button shows: the custom name, else the folder or file name.</summary>
    public string Name => CustomName ?? DefaultName;

    /// <summary>Name from the path; falls back to the whole path for drive roots like "D:\".</summary>
    public string DefaultName
    {
        get
        {
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(FullPath));
            return string.IsNullOrEmpty(name) ? FullPath : name;
        }
    }

    public string Icon => IsDirectory ? "\U0001F4C1" : "\U0001F4C4";

    public event PropertyChangedEventHandler? PropertyChanged;

    public static ShortcutItem? FromPath(string path, string? customName = null)
    {
        if (Directory.Exists(path)) return new ShortcutItem { FullPath = path, IsDirectory = true, CustomName = customName };
        if (File.Exists(path)) return new ShortcutItem { FullPath = path, IsDirectory = false, CustomName = customName };
        return null;
    }
}

/// <summary>
/// Loads and saves a pane's shortcut bar under %AppData%\Dvojnik, one entry per line as
/// <c>path</c> or <c>path|custom name</c>. ("|" is illegal in Windows paths, so it is a safe
/// separator.) Each pane has its own file so the two bars stay independent.
/// </summary>
public static class ShortcutStore
{
    public static string FolderPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dvojnik");

    /// <summary>Slovenian file names, matching the app: bližnjice-levo / bližnjice-desno.</summary>
    public static string FileFor(string paneId) => Path.Combine(FolderPath, paneId switch
    {
        "Left" => "bližnjice-levo.txt",
        "Right" => "bližnjice-desno.txt",
        _ => $"bližnjice-{paneId}.txt"
    });

    /// <summary>Pre-1.0.6 file name, migrated on first load.</summary>
    private static string LegacyFileFor(string paneId) => Path.Combine(FolderPath, $"shortcuts-{paneId}.txt");

    public static List<ShortcutItem> Load(string paneId)
    {
        var result = new List<ShortcutItem>();

        try
        {
            MigrateLegacyFile(paneId);

            var file = FileFor(paneId);
            if (!File.Exists(file)) return result;

            foreach (var line in File.ReadAllLines(file))
            {
                var text = line.Trim();
                if (text.Length == 0) continue;

                // "path" or "path|custom name" — split on the first separator only.
                var sep = text.IndexOf('|');
                var path = sep >= 0 ? text[..sep] : text;
                var custom = sep >= 0 ? text[(sep + 1)..] : null;

                // Silently drop shortcuts whose target has since been deleted or unmounted.
                var item = ShortcutItem.FromPath(path.Trim(), custom);
                if (item != null) result.Add(item);
            }
        }
        catch
        {
            // A corrupt or unreadable list shouldn't stop the pane from opening.
        }

        return result;
    }

    public static void Save(string paneId, IEnumerable<ShortcutItem> items)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllLines(FileFor(paneId), items.Select(i =>
                i.CustomName is null ? i.FullPath : $"{i.FullPath}|{i.CustomName}"));
        }
        catch
        {
            // A read-only profile shouldn't break the UI.
        }
    }

    private static void MigrateLegacyFile(string paneId)
    {
        try
        {
            var legacy = LegacyFileFor(paneId);
            if (File.Exists(legacy) && !File.Exists(FileFor(paneId)))
                File.Move(legacy, FileFor(paneId));
        }
        catch
        {
            // Losing an old shortcut list is not worth an error dialog.
        }
    }
}
