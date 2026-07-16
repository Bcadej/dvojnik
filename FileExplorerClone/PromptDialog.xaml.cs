using System.Windows;
using System.Windows.Input;

namespace FileExplorerClone;

public partial class PromptDialog : Window
{
    public string ResultText { get; private set; } = "";

    public PromptDialog(string title, string label, string defaultValue)
    {
        InitializeComponent();
        Title = title;
        PromptLabel.Text = label;
        InputBox.Text = defaultValue;
        InputBox.SelectAll();
        Loaded += (_, _) => InputBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        ResultText = InputBox.Text;
        DialogResult = true;
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ResultText = InputBox.Text;
            DialogResult = true;
        }
    }

    public static string? Show(string title, string label, string defaultValue)
    {
        var dlg = new PromptDialog(title, label, defaultValue) { Owner = Application.Current.MainWindow };
        return dlg.ShowDialog() == true ? dlg.ResultText : null;
    }
}
