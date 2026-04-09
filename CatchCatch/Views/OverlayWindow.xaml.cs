using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CatchCatch.Helpers;
using CatchCatch.Models;

namespace CatchCatch.Views;

public partial class OverlayWindow : Window
{
    private const double CatSize = 80;
    private const double BubbleMaxWidth = 200;
    private const double BubblePadding = 8;
    private const int MaxBubbles = 5;

    private readonly Dictionary<string, CatVisual> _catVisuals = new();
    private bool _clickThrough = true;
    private bool _isDragging;
    private Point _dragOffset;

    public Action<double, double>? OnCatDragged;
    public Action<double, double>? OnCatDragEnd;
    public Action<string>? OnCatClicked;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetClickThrough(true);
    }

    public void CoverScreen(System.Windows.Forms.Screen screen)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = screen.Bounds.Left / dpi.DpiScaleX;
        Top = screen.Bounds.Top / dpi.DpiScaleY;
        Width = screen.Bounds.Width / dpi.DpiScaleX;
        Height = screen.Bounds.Height / dpi.DpiScaleY;
    }

    public void SetClickThrough(bool transparent)
    {
        _clickThrough = transparent;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        var style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        if (transparent)
            style |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW;
        else
            style = (style & ~NativeMethods.WS_EX_TRANSPARENT) | NativeMethods.WS_EX_TOOLWINDOW;

        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, style);
    }

    public void UpdateCat(string userId, double x, double y, bool isActive, CatTheme theme,
        string name, bool isLocal, List<BubbleMessage>? bubbles = null, bool isChatOpen = false)
    {
        if (!_catVisuals.TryGetValue(userId, out var visual))
        {
            visual = new CatVisual();
            _catVisuals[userId] = visual;
            OverlayCanvas.Children.Add(visual.Container);
            OverlayCanvas.Children.Add(visual.NameBorder);
        }

        // Update image
        var imgPath = isActive ? theme.ActiveImage() : theme.IdleImage();
        if (visual.CurrentImagePath != imgPath)
        {
            visual.CatImage.Source = new BitmapImage(new Uri(imgPath));
            visual.CurrentImagePath = imgPath;
        }

        // Update name label
        visual.NameLabel.Text = name;
        var nameOffset = isChatOpen ? 42.0 : 0.0;

        // Position on canvas (x, y are absolute screen coords relative to this window)
        var left = x - Left;
        var top = y - Top;
        Canvas.SetLeft(visual.Container, left);
        Canvas.SetTop(visual.Container, top);

        // Position name below cat
        Canvas.SetLeft(visual.NameBorder, left + (CatSize - visual.NameBorder.ActualWidth) / 2);
        Canvas.SetTop(visual.NameBorder, top + CatSize + 2 + nameOffset);

        // Update bubbles
        UpdateBubbles(visual, left, top, bubbles);

        // Handle drag for local cat
        if (isLocal && !visual.DragSetup)
        {
            visual.CatImage.Cursor = Cursors.Hand;
            visual.DragSetup = true;
        }
    }

    private void UpdateBubbles(CatVisual visual, double catLeft, double catTop,
        List<BubbleMessage>? bubbles)
    {
        // Remove old bubbles
        foreach (var b in visual.BubbleElements)
            OverlayCanvas.Children.Remove(b);
        visual.BubbleElements.Clear();

        if (bubbles == null || bubbles.Count == 0) return;

        var recentBubbles = bubbles.TakeLast(MaxBubbles).ToList();
        var yOffset = 0.0;

        for (int i = recentBubbles.Count - 1; i >= 0; i--)
        {
            var bubble = CreateBubble(recentBubbles[i].Text);
            bubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var bh = bubble.DesiredSize.Height;

            yOffset += bh + 4;
            Canvas.SetLeft(bubble, catLeft + (CatSize - Math.Min(bubble.DesiredSize.Width, BubbleMaxWidth)) / 2);
            Canvas.SetTop(bubble, catTop - yOffset);

            OverlayCanvas.Children.Add(bubble);
            visual.BubbleElements.Add(bubble);
        }
    }

    private static Border CreateBubble(string text)
    {
        var tb = new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            FontSize = 12,
            MaxWidth = BubbleMaxWidth - BubblePadding * 2,
            TextWrapping = TextWrapping.Wrap,
        };

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(BubblePadding, 4, BubblePadding, 4),
            Child = tb,
            MaxWidth = BubbleMaxWidth,
        };
    }

    public void RemoveCat(string userId)
    {
        if (!_catVisuals.Remove(userId, out var visual)) return;
        OverlayCanvas.Children.Remove(visual.Container);
        OverlayCanvas.Children.Remove(visual.NameBorder);
        foreach (var b in visual.BubbleElements)
            OverlayCanvas.Children.Remove(b);
    }

    public void EnableDrag(bool enable)
    {
        SetClickThrough(!enable);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!_clickThrough)
        {
            // Check if clicking on a cat
            var pos = e.GetPosition(OverlayCanvas);
            foreach (var (userId, visual) in _catVisuals)
            {
                var catLeft = Canvas.GetLeft(visual.Container);
                var catTop = Canvas.GetTop(visual.Container);
                if (pos.X >= catLeft && pos.X <= catLeft + CatSize &&
                    pos.Y >= catTop && pos.Y <= catTop + CatSize)
                {
                    if (visual.DragSetup) // local cat
                    {
                        _isDragging = true;
                        _dragOffset = new Point(pos.X - catLeft, pos.Y - catTop);
                        CaptureMouse();
                    }
                    else
                    {
                        OnCatClicked?.Invoke(userId);
                    }
                    break;
                }
            }
        }
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_isDragging)
        {
            var pos = e.GetPosition(OverlayCanvas);
            var newX = pos.X - _dragOffset.X + Left;
            var newY = pos.Y - _dragOffset.Y + Top;
            OnCatDragged?.Invoke(newX, newY);
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            var pos = e.GetPosition(OverlayCanvas);
            var newX = pos.X - _dragOffset.X + Left;
            var newY = pos.Y - _dragOffset.Y + Top;
            OnCatDragEnd?.Invoke(newX, newY);
        }
        base.OnMouseLeftButtonUp(e);
    }

    private class CatVisual
    {
        public Canvas Container { get; } = new() { Width = CatSize, Height = CatSize };
        public Image CatImage { get; }
        public string? CurrentImagePath { get; set; }
        public TextBlock NameLabel { get; }
        public Border NameBorder { get; }
        public List<UIElement> BubbleElements { get; } = new();
        public bool DragSetup { get; set; }

        public CatVisual()
        {
            CatImage = new Image
            {
                Width = CatSize,
                Height = CatSize,
            };
            System.Windows.Media.RenderOptions.SetBitmapScalingMode(CatImage, BitmapScalingMode.NearestNeighbor);
            Container.Children.Add(CatImage);

            NameLabel = new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = Brushes.White,
            };

            NameBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 2, 6, 2),
                Child = NameLabel,
            };
        }
    }
}
