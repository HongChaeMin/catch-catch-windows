using System.Windows;
using System.Windows.Input;

namespace CatchCatch.Views;

public partial class ChatWindow : Window
{
    public event Action<string>? OnSend;
    public event Action<bool>? OnVisibilityChanged;

    public ChatWindow()
    {
        InitializeComponent();
    }

    public void ShowNear(double catX, double catY, double catSize)
    {
        // Position below cat + name label
        Left = catX + (catSize - Width) / 2;
        Top = catY + catSize + 24;

        // Clamp to screen bounds
        var screen = System.Windows.Forms.Screen.FromPoint(
            new System.Drawing.Point((int)catX, (int)catY));
        var bounds = screen.WorkingArea;

        if (Left + Width > bounds.Right)
            Left = bounds.Right - Width;
        if (Left < bounds.Left)
            Left = bounds.Left;
        if (Top + Height > bounds.Bottom)
            Top = catY - Height - 4; // above cat if no room below

        Show();
        ChatInput.Focus();
        OnVisibilityChanged?.Invoke(true);
    }

    public void HidePanel()
    {
        Hide();
        OnVisibilityChanged?.Invoke(false);
    }

    private void Send()
    {
        var text = ChatInput.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        OnSend?.Invoke(text);
        ChatInput.Clear();
    }

    private void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Send();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            HidePanel();
            e.Handled = true;
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        Send();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        HidePanel();
    }
}
