using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CatchCatch.Models;

namespace CatchCatch.Services;

public sealed class WebSocketClient : IDisposable
{
    private const string ServerUrl = "wss://catch.hannah-log.site";
    private const int MaxRetries = 3;

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public event Action<WsMessage>? OnMessage;
    public event Action? OnConnected;
    public event Action? OnDisconnected;

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public async Task ConnectAsync(string roomCode, string userId, string name, string theme)
    {
        _cts = new CancellationTokenSource();
        var retryCount = 0;

        while (retryCount <= MaxRetries && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                _ws?.Dispose();
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(new Uri(ServerUrl), _cts.Token);
                OnConnected?.Invoke();

                // Send join
                var joinMsg = new WsMessage
                {
                    Type = "join",
                    RoomCode = roomCode,
                    UserId = userId,
                    Name = name,
                    Theme = theme,
                };
                await SendAsync(joinMsg);

                // Start receiving
                await ReceiveLoopAsync(_cts.Token);
                break;
            }
            catch (Exception) when (!_cts.Token.IsCancellationRequested)
            {
                retryCount++;
                if (retryCount > MaxRetries) break;
                var delay = (int)Math.Pow(2, retryCount) * 1000;
                await Task.Delay(delay, _cts.Token).ConfigureAwait(false);
            }
        }

        OnDisconnected?.Invoke();
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];

        while (_ws?.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var sb = new StringBuilder();
            WebSocketReceiveResult result;

            do
            {
                result = await _ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    return;
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            } while (!result.EndOfMessage);

            try
            {
                var msg = JsonSerializer.Deserialize<WsMessage>(sb.ToString());
                if (msg != null)
                    OnMessage?.Invoke(msg);
            }
            catch (JsonException)
            {
                // ignore malformed messages
            }
        }
    }

    public async Task SendAsync(WsMessage message)
    {
        if (_ws?.State != WebSocketState.Open) return;

        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, _cts?.Token ?? default);
    }

    public async Task DisconnectAsync()
    {
        if (_ws?.State == WebSocketState.Open)
        {
            try
            {
                await SendAsync(new WsMessage { Type = "leave" });
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
            catch
            {
                // best effort
            }
        }

        _cts?.Cancel();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _ws?.Dispose();
    }
}
