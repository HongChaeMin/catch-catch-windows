using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using CatchCatch.Helpers;
using CatchCatch.Models;

namespace CatchCatch.Views;

public partial class OverlayWindow : Window
{
    private const double CatSize = 80;
    private const double BubbleMaxWidth = 200;
    private const double BubblePadding = 8;
    private const int MaxBubbles = 5;
    private const double ParticleLifetime = 0.8; // seconds
    private const double ParticleSize = 7;

    private readonly Dictionary<string, CatVisual> _catVisuals = new();
    private readonly DispatcherTimer _particleTimer;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        _particleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60fps
        _particleTimer.Tick += OnParticleTick;
        _particleTimer.Start();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetClickThrough(true);
    }

    public double DpiScaleX { get; private set; } = 1.0;
    public double DpiScaleY { get; private set; } = 1.0;

    public void CoverScreen(System.Windows.Forms.Screen screen)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        DpiScaleX = dpi.DpiScaleX;
        DpiScaleY = dpi.DpiScaleY;
        Left = screen.Bounds.Left / DpiScaleX;
        Top = screen.Bounds.Top / DpiScaleY;
        Width = screen.Bounds.Width / DpiScaleX;
        Height = screen.Bounds.Height / DpiScaleY;
    }

    public void SetClickThrough(bool transparent)
    {
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
        string name, bool isLocal, bool showName = true,
        List<BubbleMessage>? bubbles = null, bool isChatOpen = false,
        int comboCount = 0, List<Particle>? particles = null,
        bool isSleeping = false)
    {
        if (!_catVisuals.TryGetValue(userId, out var visual))
        {
            visual = new CatVisual();
            _catVisuals[userId] = visual;
            OverlayCanvas.Children.Add(visual.Container);
            OverlayCanvas.Children.Add(visual.NameBorder);
            OverlayCanvas.Children.Add(visual.ComboLabel);
            OverlayCanvas.Children.Add(visual.SleepLabel);
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
        visual.NameBorder.Visibility = showName ? Visibility.Visible : Visibility.Collapsed;
        var nameOffset = isChatOpen ? 42.0 : 0.0;

        // Convert physical pixel coords to DIPs for canvas positioning
        var left = x / DpiScaleX - Left;
        var top = y / DpiScaleY - Top;
        Canvas.SetLeft(visual.Container, left);
        Canvas.SetTop(visual.Container, top);

        // Position name below cat
        Canvas.SetLeft(visual.NameBorder, left + (CatSize - visual.NameBorder.ActualWidth) / 2);
        Canvas.SetTop(visual.NameBorder, top + CatSize + 2 + nameOffset);

        // Update combo label
        if (comboCount > 0)
        {
            visual.ComboLabel.Text = $"x{comboCount}";
            visual.ComboLabel.Foreground = new SolidColorBrush(GetComboColor(comboCount));
            visual.ComboLabel.Visibility = Visibility.Visible;
            Canvas.SetLeft(visual.ComboLabel, left + CatSize - 4);
            Canvas.SetTop(visual.ComboLabel, top - 16);
        }
        else
        {
            visual.ComboLabel.Visibility = Visibility.Collapsed;
        }

        // Update sleep indicator
        if (isSleeping)
        {
            visual.SleepLabel.Visibility = Visibility.Visible;
            Canvas.SetLeft(visual.SleepLabel, left + CatSize / 2);
            Canvas.SetTop(visual.SleepLabel, top - 22);
        }
        else
        {
            visual.SleepLabel.Visibility = Visibility.Collapsed;
        }

        // Store particles reference and position for animation
        visual.Particles = particles;
        visual.CatLeft = left;
        visual.CatTop = top;

        // Update bubbles
        UpdateBubbles(visual, left, top, bubbles);

        // Set hand cursor for local cat
        if (isLocal && !visual.CursorSet)
        {
            visual.CatImage.Cursor = Cursors.Hand;
            visual.CursorSet = true;
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
        OverlayCanvas.Children.Remove(visual.ComboLabel);
        OverlayCanvas.Children.Remove(visual.SleepLabel);
        foreach (var b in visual.BubbleElements)
            OverlayCanvas.Children.Remove(b);
        foreach (var e in visual.ParticleElements)
            OverlayCanvas.Children.Remove(e);
    }

    private void OnParticleTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;

        // Sleep label pulse: 0.4 ~ 1.0 over 1.5s cycle
        var sleepPhase = now.TimeOfDay.TotalSeconds % 1.5 / 1.5; // 0..1
        var sleepOpacity = 0.4 + 0.6 * (0.5 + 0.5 * Math.Sin(sleepPhase * 2 * Math.PI));

        foreach (var (_, visual) in _catVisuals)
        {
            if (visual.SleepLabel.Visibility == Visibility.Visible)
                visual.SleepLabel.Opacity = sleepOpacity;
            // Remove old particle UI elements
            foreach (var el in visual.ParticleElements)
                OverlayCanvas.Children.Remove(el);
            visual.ParticleElements.Clear();

            if (visual.Particles == null || visual.Particles.Count == 0) continue;

            // Remove expired particles from the source list
            visual.Particles.RemoveAll(p => (now - p.Created).TotalSeconds > ParticleLifetime);

            var centerX = visual.CatLeft + CatSize / 2;
            var bottomY = visual.CatTop + CatSize * 0.75;  // 고양이 하단 근처

            foreach (var p in visual.Particles)
            {
                var age = (now - p.Created).TotalSeconds;
                var progress = age / ParticleLifetime;
                var opacity = 1.0 - progress * progress;  // 부드러운 페이드
                var size = ParticleSize * (1.0 - progress * 0.4);

                var px = centerX + p.StartX + p.Dx * progress;
                var py = bottomY + p.Dy * progress;
                var color = GetParticleColor(p.Color);

                // 글로우 (큰 원)
                var glow = new Ellipse
                {
                    Width = size * 2.5, Height = size * 2.5,
                    Fill = new SolidColorBrush(color) { Opacity = opacity * 0.35 },
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(glow, px - glow.Width / 2);
                Canvas.SetTop(glow, py - glow.Height / 2);
                OverlayCanvas.Children.Add(glow);
                visual.ParticleElements.Add(glow);

                // 코어 (작은 원)
                var core = new Ellipse
                {
                    Width = size, Height = size,
                    Fill = new SolidColorBrush(color) { Opacity = opacity },
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(core, px - core.Width / 2);
                Canvas.SetTop(core, py - core.Height / 2);
                OverlayCanvas.Children.Add(core);
                visual.ParticleElements.Add(core);
            }
        }
    }

    private static Color GetComboColor(int combo) => combo switch
    {
        >= 150 => Color.FromRgb(255, 77, 179),   // pink
        >= 100 => Colors.Red,
        >= 60 => Colors.Orange,
        >= 30 => Color.FromRgb(77, 255, 128),    // green
        _ => Color.FromRgb(77, 230, 255),         // cyan
    };

    private static Color GetParticleColor(string color) => color switch
    {
        "Red" => Color.FromRgb(255, 50, 50),
        "Orange" => Color.FromRgb(255, 153, 50),
        "Yellow" => Color.FromRgb(255, 242, 77),
        "Green" => Color.FromRgb(77, 255, 128),
        "Cyan" => Color.FromRgb(77, 230, 255),
        "Blue" => Color.FromRgb(102, 153, 255),
        "Pink" => Color.FromRgb(255, 77, 179),
        _ => Colors.White,
    };


    private class CatVisual
    {
        public Canvas Container { get; } = new() { Width = CatSize, Height = CatSize };
        public Image CatImage { get; }
        public string? CurrentImagePath { get; set; }
        public TextBlock NameLabel { get; }
        public Border NameBorder { get; }
        public TextBlock ComboLabel { get; }
        public TextBlock SleepLabel { get; }
        public List<UIElement> BubbleElements { get; } = new();
        public List<UIElement> ParticleElements { get; } = new();
        public List<Particle>? Particles { get; set; }
        public double CatLeft { get; set; }
        public double CatTop { get; set; }
        public bool CursorSet { get; set; }

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

            ComboLabel = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };

            SleepLabel = new TextBlock
            {
                Text = "\U0001F4A4",
                FontSize = 18,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };
        }
    }
}
