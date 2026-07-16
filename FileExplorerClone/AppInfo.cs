using System.Globalization;
using System.IO;
using System.Reflection;

namespace FileExplorerClone;

/// <summary>
/// Version, build stamp and on-disk location of the running executable.
/// </summary>
public static class AppInfo
{
    /// <summary>Three-part version, e.g. "1.0.1". Bumped by publish.ps1 on every publish.</summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>Build timestamp, baked in at compile time by the csproj (UTC), shown local.</summary>
    public static DateTime BuildTime { get; } = ReadBuildTime();

    /// <summary>Full path of the running .exe. Works under single-file publish.</summary>
    public static string ExecutablePath { get; } = Environment.ProcessPath ?? "";

    public static string ExecutableFolder =>
        string.IsNullOrEmpty(ExecutablePath) ? "" : Path.GetDirectoryName(ExecutablePath) ?? "";

    private static string ReadVersion()
    {
        var info = typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // The SDK appends "+<commit sha>" to the informational version; trim it off.
        if (!string.IsNullOrEmpty(info))
        {
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }

        return typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static DateTime ReadBuildTime()
    {
        var stamp = typeof(AppInfo).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildTime")?.Value;

        if (DateTime.TryParse(stamp, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var utc))
            return utc.ToLocalTime();

        return DateTime.Now;
    }
}

/// <summary>
/// Culture-specific date rendering. Slovenian uses "dd. MM. yyyy" — dots followed by
/// spaces; British English uses "dd/MM/yyyy".
/// </summary>
public static class DateFormats
{
    public static string DatePattern =>
        LanguageManager.CurrentLanguage == LanguageManager.English ? "dd/MM/yyyy" : "dd. MM. yyyy";

    public static string DateTimePattern => DatePattern + " HH:mm";

    public static string FormatDate(DateTime value) =>
        value.ToString(DatePattern, CultureInfo.InvariantCulture);

    public static string FormatDateTime(DateTime value) =>
        value.ToString(DateTimePattern, CultureInfo.InvariantCulture);
}
