using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace CatchCatch.Models;

public class Particle
{
    public double StartX { get; set; }  // 시작 x 오프셋
    public double Dx { get; set; }      // 수평 드리프트
    public double Dy { get; set; }      // 수직 이동 (음수=위)
    public DateTime Created { get; set; } = DateTime.Now;
    public string Color { get; set; } = "Cyan";
}

public class CatState : INotifyPropertyChanged
{
    private static readonly Random Rng = new();

    private double _absX;
    private double _absY;
    private bool _isActive;
    private string _name = "Anonymous";
    private CatTheme _theme = CatTheme.Cat;
    private bool _isChatOpen;
    private bool _showName = true;
    private bool _syncPosition = true;
    private int _keystrokeCount;
    private int _comboCount;
    private bool _powerMode = true;
    private DispatcherTimer? _comboResetTimer;

    public string UserId { get; }

    public double AbsX
    {
        get => _absX;
        set => SetField(ref _absX, value);
    }

    public double AbsY
    {
        get => _absY;
        set => SetField(ref _absY, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public CatTheme Theme
    {
        get => _theme;
        set => SetField(ref _theme, value);
    }

    public bool IsChatOpen
    {
        get => _isChatOpen;
        set => SetField(ref _isChatOpen, value);
    }

    public bool ShowName
    {
        get => _showName;
        set => SetField(ref _showName, value);
    }

    public bool SyncPosition
    {
        get => _syncPosition;
        set => SetField(ref _syncPosition, value);
    }

    public int KeystrokeCount
    {
        get => _keystrokeCount;
        set => SetField(ref _keystrokeCount, value);
    }

    public int ComboCount
    {
        get => _comboCount;
        set => SetField(ref _comboCount, value);
    }

    public bool PowerMode
    {
        get => _powerMode;
        set => SetField(ref _powerMode, value);
    }

    public List<Particle> Particles { get; } = new();

    private static readonly string[][] ColorPools =
    {
        new[] { "Cyan", "Blue", "White" },       // 0-29
        new[] { "Green", "Cyan", "Yellow" },      // 30-59
        new[] { "Yellow", "Orange", "Pink" },     // 60-99
        new[] { "Orange", "Red", "Pink" },        // 100-149
        new[] { "Red", "Pink", "Orange" },        // 150+
    };

    public string ComboColor => ComboCount switch
    {
        >= 150 => "Pink",
        >= 100 => "Red",
        >= 60 => "Orange",
        >= 30 => "Green",
        _ => "Cyan",
    };

    private static string RandomColorForTier(int combo)
    {
        var pool = combo >= 150 ? ColorPools[4] : combo >= 100 ? ColorPools[3] : combo >= 60 ? ColorPools[2] : combo >= 30 ? ColorPools[1] : ColorPools[0];
        return pool[Rng.Next(pool.Length)];
    }

    public void IncrementKeystroke()
    {
        KeystrokeCount++;
    }

    public void BumpCombo()
    {
        if (!PowerMode) return;

        ComboCount++;

        if (_comboResetTimer == null)
        {
            _comboResetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _comboResetTimer.Tick += (_, _) =>
            {
                _comboResetTimer.Stop();
                ComboCount = 0;
                Particles.Clear();
            };
        }
        _comboResetTimer.Stop();
        _comboResetTimer.Start();

        SpawnParticles();
    }

    public void SpawnParticles()
    {
        var count = Math.Min(3 + ComboCount / 10, 8);
        for (var i = 0; i < count; i++)
        {
            Particles.Add(new Particle
            {
                StartX = Rng.NextDouble() * 60 - 30,
                Dx = Rng.NextDouble() * 30 - 15,
                Dy = Rng.NextDouble() * -35 - 15,
                Color = RandomColorForTier(ComboCount),
            });
        }
    }

    public CatState(string userId)
    {
        UserId = userId;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
