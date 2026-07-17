using System.IO;
using System.Text;

namespace FileExplorerClone;

/// <summary>
/// Dead-simple always-on file logger. Writes one line per event to
/// <c>%APPDATA%\Dvojnik\logs\dvojnik-yyyyMMdd.log</c>, keeping a week of files.
///
/// It never throws: logging is a diagnostic aid, so a failure to write must never take down
/// the operation being logged. All public methods swallow their own I/O errors.
/// </summary>
public static class Logger
{
    private static readonly object Gate = new();

    /// <summary>Folder the daily log files live in. Exposed so the About window can reveal it.</summary>
    public static string LogFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dvojnik", "logs");

    private const int RetentionDays = 7;
    private static bool _started;

    /// <summary>Log levels, coarsest first. Everything is written; the tag aids scanning/grepping.</summary>
    public enum Level { INFO, OP, NAV, WARN, ERROR, DEBUG }

    /// <summary>Writes a one-off startup banner and prunes old logs. Safe to call more than once.</summary>
    public static void Startup()
    {
        lock (Gate)
        {
            if (_started) return;
            _started = true;
        }

        PruneOldLogs();
        Write(Level.INFO, $"===== Dvojnik {AppInfo.Version} started (built {AppInfo.BuildTime:yyyy-MM-dd HH:mm}, " +
                          $"pid {Environment.ProcessId}, {Environment.OSVersion}) =====");
    }

    public static void Info(string message) => Write(Level.INFO, message);
    public static void Nav(string message) => Write(Level.NAV, message);
    public static void Debug(string message) => Write(Level.DEBUG, message);
    public static void Warn(string message) => Write(Level.WARN, message);

    /// <summary>Logs an exception with its full type, HRESULT-bearing text, and stack.</summary>
    public static void Error(string message, Exception ex)
        => Write(Level.ERROR, $"{message} -> {Describe(ex)}");

    public static void Error(string message) => Write(Level.ERROR, message);

    /// <summary>
    /// Logs a file operation as a structured, greppable line, e.g.
    /// <c>OP MOVE src="..." dst="..." recycle=False result=OK</c>. Pass the exception on failure.
    /// </summary>
    public static void Operation(string op, string result, Exception? ex = null, params (string Key, object? Value)[] fields)
    {
        var sb = new StringBuilder(op);
        foreach (var (key, value) in fields)
            sb.Append(' ').Append(key).Append("=\"").Append(value).Append('"');
        sb.Append(" result=").Append(result);
        if (ex != null) sb.Append(" -> ").Append(Describe(ex));
        Write(Level.OP, sb.ToString());
    }

    private static string Describe(Exception ex)
    {
        // HRESULT is the decisive clue for shell/COM failures and rarely shows up in Message.
        var hr = ex.HResult != 0 ? $" (0x{ex.HResult:X8})" : "";
        var text = $"{ex.GetType().Name}{hr}: {ex.Message}";
        if (ex.InnerException != null) text += $" | inner: {Describe(ex.InnerException)}";
        return text;
    }

    private static void Write(Level level, string message)
    {
        try
        {
            Directory.CreateDirectory(LogFolder);
            var path = Path.Combine(LogFolder, $"dvojnik-{DateTime.Now:yyyyMMdd}.log");
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level,-5} {message}{Environment.NewLine}";
            lock (Gate)
                File.AppendAllText(path, line, Encoding.UTF8);
        }
        catch
        {
            // Never let logging break the app.
        }
    }

    private static void PruneOldLogs()
    {
        try
        {
            if (!Directory.Exists(LogFolder)) return;
            var cutoff = DateTime.Now.AddDays(-RetentionDays);
            foreach (var file in Directory.EnumerateFiles(LogFolder, "dvojnik-*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch
        {
            // A stuck old file is not worth failing startup over.
        }
    }
}
