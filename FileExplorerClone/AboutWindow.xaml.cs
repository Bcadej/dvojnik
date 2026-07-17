using System.Diagnostics;
using System.IO;
using System.Windows;

namespace FileExplorerClone;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        AppNameText.Text = Strings.Get("App_Title");
        VersionText.Text = AppInfo.Version;
        BuildTimeText.Text = DateFormats.FormatDateTime(AppInfo.BuildTime);
        LocationRun.Text = string.IsNullOrEmpty(AppInfo.ExecutablePath)
            ? "—"
            : AppInfo.ExecutablePath;
        LocationLink.IsEnabled = !string.IsNullOrEmpty(AppInfo.ExecutablePath);

        SettingsRun.Text = LanguageManager.SettingsPath;
        LogsRun.Text = Logger.LogFolder;
    }

    /// <summary>Opens the folder holding the language setting file, with the file selected.</summary>
    private void SettingsLink_Click(object sender, RoutedEventArgs e)
        => RevealInExplorer(LanguageManager.SettingsPath);

    /// <summary>Opens the folder holding the daily log files.</summary>
    private void LogsLink_Click(object sender, RoutedEventArgs e)
        => OpenFolder(Logger.LogFolder);

    /// <summary>
    /// Opens Explorer at a file's folder with the file highlighted, falling back to just
    /// opening the folder if the file is missing.
    /// </summary>
    private void RevealInExplorer(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
                {
                    UseShellExecute = true
                });
                return;
            }

            var folder = Path.GetDirectoryName(path);
            if (Directory.Exists(folder))
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Strings.Format("Msg_CouldNotOpen", ex.Message),
                Strings.Get("Msg_Open_Title"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Opens Explorer at the app's folder with the .exe already selected.</summary>
    private void LocationLink_Click(object sender, RoutedEventArgs e)
        => RevealInExplorer(AppInfo.ExecutablePath);

    /// <summary>Opens a folder directly, creating it first if it doesn't exist yet.</summary>
    private void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Strings.Format("Msg_CouldNotOpen", ex.Message),
                Strings.Get("Msg_Open_Title"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
