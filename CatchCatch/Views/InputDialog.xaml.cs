using System.Windows;
using System.Windows.Input;

namespace CatchCatch.Views;

public partial class InputDialog : Window
{
    public string Result { get; private set; } = "";

    public InputDialog(string prompt, string title, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = defaultValue;
        Loaded += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
    }

    public static string? Show(string prompt, string title, string defaultValue = "")
    {
        var dialog = new InputDialog(prompt, title, defaultValue);
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        Result = InputBox.Text;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Result = InputBox.Text;
            DialogResult = true;
        }
    }
}
