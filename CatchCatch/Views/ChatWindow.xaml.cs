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
        // catX/catY are physical pixels; WPF Left/Top are DIPs
        var source = PresentationSource.FromVisual(this)
                     ?? PresentationSource.FromVisual(Application.Current.MainWindow!);
        var dpiX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        var dpiY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        var dipCatX = catX * dpiX;
        var dipCatY = catY * dpiY;
        var dipCatSize = catSize * dpiX;

        // Position below cat + name label (small gap)
        Left = dipCatX + (dipCatSize - Width) / 2;
        Top = dipCatY + dipCatSize + 4;

        // Clamp to screen bounds (convert to DIPs)
        var screen = System.Windows.Forms.Screen.FromPoint(
            new System.Drawing.Point((int)catX, (int)catY));
        var bounds = screen.WorkingArea;
        var bLeft = bounds.Left * dpiX;
        var bRight = bounds.Right * dpiX;
        var bBottom = bounds.Bottom * dpiY;

        if (Left + Width > bRight)
            Left = bRight - Width;
        if (Left < bLeft)
            Left = bLeft;
        if (Top + Height > bBottom)
            Top = dipCatY - Height - 4; // above cat if no room below

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
