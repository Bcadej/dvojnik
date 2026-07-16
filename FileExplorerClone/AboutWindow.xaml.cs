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
    }

    /// <summary>Opens the language setting file in whatever handles .txt.</summary>
    private void SettingsLink_Click(object sender, RoutedEventArgs e)
    {
        var settings = LanguageManager.SettingsPath;

        try
        {
            if (File.Exists(settings))
            {
                Process.Start(new ProcessStartInfo(settings) { UseShellExecute = true });
            }
            else
            {
                // Shouldn't happen — it's written on startup — but reveal the folder rather than fail.
                var folder = Path.GetDirectoryName(settings);
                if (Directory.Exists(folder))
                    Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Strings.Format("Msg_CouldNotOpen", ex.Message),
                Strings.Get("Msg_Open_Title"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Opens Explorer at the app's folder with the .exe already selected.</summary>
    private void LocationLink_Click(object sender, RoutedEventArgs e)
    {
        var exe = AppInfo.ExecutablePath;

        try
        {
            if (File.Exists(exe))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{exe}\"")
                {
                    UseShellExecute = true
                });
            }
            else if (Directory.Exists(AppInfo.ExecutableFolder))
            {
                Process.Start(new ProcessStartInfo(AppInfo.ExecutableFolder) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Strings.Format("Msg_CouldNotOpen", ex.Message),
                Strings.Get("Msg_Open_Title"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
