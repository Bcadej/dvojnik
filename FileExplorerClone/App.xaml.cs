using System.Windows;

namespace FileExplorerClone;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Slovenian is the default; a previously saved choice wins.
        LanguageManager.Initialise();
        base.OnStartup(e);
    }
}
