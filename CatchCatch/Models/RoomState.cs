using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CatchCatch.Models;

public class BubbleMessage
{
    public string UserId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

public class RoomState : INotifyPropertyChanged
{
    private bool _isConnected;
    private string _roomCode = "";

    public bool IsConnected
    {
        get => _isConnected;
        set => SetField(ref _isConnected, value);
    }

    public string RoomCode
    {
        get => _roomCode;
        set => SetField(ref _roomCode, value);
    }

    public ObservableCollection<CatState> Peers { get; } = new();
    public ObservableCollection<BubbleMessage> ChatHistory { get; } = new();

    public void AddChatMessage(BubbleMessage msg)
    {
        ChatHistory.Add(msg);
        while (ChatHistory.Count > 100)
            ChatHistory.RemoveAt(0);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
