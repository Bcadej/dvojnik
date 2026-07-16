using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Resources;

namespace FileExplorerClone;

/// <summary>
/// Access point for the embedded string tables. The neutral table is Slovenian
/// (Resources/Strings.resx); English lives in the en-GB satellite assembly.
/// </summary>
internal static class Strings
{
    public static readonly ResourceManager ResourceManager =
        new("FileExplorerClone.Resources.Strings", typeof(Strings).Assembly);

    public static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}

/// <summary>
/// Bindable façade over <see cref="Strings"/>. XAML binds through the indexer
/// (<c>{Binding [Menu_Help], Source={x:Static local:Loc.Instance}}</c>); raising
/// PropertyChanged for "Item[]" re-evaluates every such binding, which is what
/// lets the UI switch language without a restart.
/// </summary>
public class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private Loc() { }

    public string this[string key] => Strings.Get(key);

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void RaiseAllChanged() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
}

/// <summary>
/// Owns the current UI language and persists the user's choice between sessions.
/// </summary>
public static class LanguageManager
{
    public const string Slovenian = "sl-SI";
    public const string English = "en-GB";

    /// <summary>Raised after the language changes so views can refresh non-bound text.</summary>
    public static event Action? LanguageChanged;

    public static string CurrentLanguage { get; private set; } = Slovenian;

    /// <summary>
    /// Where the language choice is stored. Shown (and openable) in Help &gt; About so the
    /// setting can be inspected or edited by hand.
    /// </summary>
    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Dvojnik", "language.txt");

    /// <summary>Applies the saved language, falling back to Slovenian as the app default.</summary>
    public static void Initialise()
    {
        var saved = TryReadSaved();
        var language = saved == English ? English : Slovenian;

        // Write the file out on first run so the About link always has something to open.
        Apply(language, persist: true);
    }

    public static void Apply(string language, bool persist = true)
    {
        CurrentLanguage = language;

        var culture = new CultureInfo(language);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;

        if (persist) TrySave(language);

        Loc.Instance.RaiseAllChanged();
        LanguageChanged?.Invoke();
    }

    private static string? TryReadSaved()
    {
        try
        {
            return File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static void TrySave(string language)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, language);
        }
        catch
        {
            // A read-only profile shouldn't stop the app from switching language.
        }
    }
}
