using System.IO;

namespace FileExplorerClone;

/// <summary>
/// One entry on a pane's shortcut bar: a remembered folder or file the user dropped there.
/// </summary>
public class ShortcutItem
{
    public string FullPath { get; init; } = "";
    public bool IsDirectory { get; init; }

    /// <summary>Folder or file name; falls back to the whole path for drive roots like "D:\".</summary>
    public string Name
    {
        get
        {
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(FullPath));
            return string.IsNullOrEmpty(name) ? FullPath : name;
        }
    }

    public string Icon => IsDirectory ? "\U0001F4C1" : "\U0001F4C4";

    public static ShortcutItem? FromPath(string path)
    {
        if (Directory.Exists(path)) return new ShortcutItem { FullPath = path, IsDirectory = true };
        if (File.Exists(path)) return new ShortcutItem { FullPath = path, IsDirectory = false };
        return null;
    }
}

/// <summary>
/// Loads and saves a pane's shortcut bar as a plain list of paths, one per line, under
/// %AppData%\Dvojnik. Each pane gets its own file so the two bars stay independent.
/// </summary>
public static class ShortcutStore
{
    public static string FolderPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dvojnik");

    public static string FileFor(string paneId) => Path.Combine(FolderPath, $"shortcuts-{paneId}.txt");

    public static List<ShortcutItem> Load(string paneId)
    {
        var result = new List<ShortcutItem>();

        try
        {
            var file = FileFor(paneId);
            if (!File.Exists(file)) return result;

            foreach (var line in File.ReadAllLines(file))
            {
                var path = line.Trim();
                if (path.Length == 0) continue;

                // Silently drop shortcuts whose target has since been deleted or unmounted.
                var item = ShortcutItem.FromPath(path);
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
            File.WriteAllLines(FileFor(paneId), items.Select(i => i.FullPath));
        }
        catch
        {
            // A read-only profile shouldn't break the UI.
        }
    }
}
