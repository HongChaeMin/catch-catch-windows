using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CatchCatch.Models;

public class CatState : INotifyPropertyChanged
{
    private double _absX;
    private double _absY;
    private bool _isActive;
    private string _name = "Anonymous";
    private CatTheme _theme = CatTheme.Cat;
    private bool _isChatOpen;
    private bool _showName = true;
    private bool _syncPosition = true;
    private int _keystrokeCount;

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

    public void IncrementKeystroke()
    {
        KeystrokeCount++;
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
