using System.Windows;
using System.Windows.Threading;

namespace FileExplorerClone;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Logger.Startup();

        // Last-resort nets so a crash leaves a stack trace behind instead of just vanishing.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Slovenian is the default; a previously saved choice wins.
        LanguageManager.Initialise();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info($"Dvojnik exiting (code {e.ApplicationExitCode})");
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        => Logger.Error("Unhandled UI-thread exception", e.Exception);

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Logger.Error($"Unhandled exception (terminating={e.IsTerminating})", ex);
        else
            Logger.Error($"Unhandled non-exception error (terminating={e.IsTerminating}): {e.ExceptionObject}");
    }

    private void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error("Unobserved task exception", e.Exception);
        e.SetObserved();
    }
}
