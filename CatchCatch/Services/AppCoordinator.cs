using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using CatchCatch.Models;
using CatchCatch.Views;
using Hardcodet.Wpf.TaskbarNotification;

namespace CatchCatch.Services;

public sealed class AppCoordinator : IDisposable
{
    private readonly CatState _localCat;
    private readonly RoomState _room = new();
    private readonly WebSocketClient _wsClient = new();
    private readonly GlobalInputMonitor _inputMonitor = new();
    private readonly UpdateChecker _updateChecker = new();

    private readonly List<OverlayWindow> _overlayWindows = new();
    private ChatWindow? _chatWindow;
    private TaskbarIcon? _trayIcon;

    private DispatcherTimer? _stateTimer;
    private DispatcherTimer? _activityTimer;
    private DateTime _lastActivity = DateTime.MinValue;
    private bool _moveMode;
    private bool _disposed;
    private System.Windows.Controls.MenuItem? _keystrokeMenuItem;

    // Bubble timers: userId -> list of (message, timer)
    private readonly Dictionary<string, List<(BubbleMessage Msg, DispatcherTimer Timer)>> _activeBubbles = new();

    public AppCoordinator()
    {
        var initX = Settings.Default.CatX > 0 ? Settings.Default.CatX : GetDefaultX();
        var initY = Settings.Default.CatY > 0 ? Settings.Default.CatY : GetDefaultY();
        (initX, initY) = ClampToScreen(initX, initY);

        _localCat = new CatState(Guid.NewGuid().ToString("N")[..8])
        {
            Name = Settings.Default.CatName.Length > 0 ? Settings.Default.CatName : "Anonymous",
            Theme = CatThemeExtensions.FromWireString(Settings.Default.CatTheme),
            AbsX = initX,
            AbsY = initY,
            ShowName = Settings.Default.ShowName,
            SyncPosition = Settings.Default.SyncPosition,
            KeystrokeCount = LoadKeystrokeCount(),
            PowerMode = Settings.Default.PowerMode,
        };
    }

    public void Start()
    {
        SetupOverlays();
        SetupTrayIcon();
        SetupInputMonitor();
        SetupTimers();
        SetupWebSocket();

        _ = _updateChecker.CheckAsync();
    }

    private void SetupOverlays()
    {
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var overlay = new OverlayWindow();
            overlay.Show();
            overlay.CoverScreen(screen);
            overlay.OnCatDragged = (x, y) =>
            {
                (x, y) = ClampToScreen(x, y);
                _localCat.AbsX = x;
                _localCat.AbsY = y;
                RefreshOverlays();
            };
            overlay.OnCatDragEnd = (x, y) =>
            {
                (x, y) = ClampToScreen(x, y);
                _localCat.AbsX = x;
                _localCat.AbsY = y;
                SavePosition();
                RefreshOverlays();
            };
            overlay.OnPeerDragged = (userId, x, y) =>
            {
                var peer = _room.Peers.FirstOrDefault(p => p.UserId == userId);
                if (peer == null) return;
                var screen2 = System.Windows.Forms.Screen.PrimaryScreen!;
                peer.AbsX = Math.Clamp(x, screen2.Bounds.Left + 40, screen2.Bounds.Right - 40);
                peer.AbsY = Math.Clamp(y, screen2.Bounds.Top + 40, screen2.Bounds.Bottom - 40);
            };
            overlay.OnPeerDragEnd = _ => { };
            _overlayWindows.Add(overlay);
        }

        RefreshOverlays();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "catch-catch",
            Icon = LoadAppIcon(),
            MenuActivation = Hardcodet.Wpf.TaskbarNotification.PopupActivationMode.RightClick,
        };

        // 좌클릭으로도 컨텍스트 메뉴 열기
        _trayIcon.TrayLeftMouseUp += (_, _) =>
        {
            if (_trayIcon.ContextMenu != null)
            {
                _trayIcon.ContextMenu.IsOpen = true;
            }
        };

        var menu = new System.Windows.Controls.ContextMenu();

        // Keystroke count
        _keystrokeMenuItem = new System.Windows.Controls.MenuItem
        {
            Header = $"Today: {_localCat.KeystrokeCount:N0} keystrokes",
            IsEnabled = false,
        };

        // Name
        var nameItem = new System.Windows.Controls.MenuItem { Header = $"Name: {_localCat.Name}", IsEnabled = false };

        // Rename
        var renameItem = new System.Windows.Controls.MenuItem { Header = "Rename..." };
        renameItem.Click += (_, _) => ShowRenameDialog();

        // Theme submenu
        var themeMenu = new System.Windows.Controls.MenuItem { Header = "Theme" };
        foreach (CatTheme t in Enum.GetValues<CatTheme>())
        {
            var theme = t;
            var item = new System.Windows.Controls.MenuItem
            {
                Header = theme.DisplayName(),
                IsCheckable = true,
                IsChecked = theme == _localCat.Theme,
            };
            item.Click += (_, _) => ChangeTheme(theme);
            themeMenu.Items.Add(item);
        }

        // Show name toggle
        var showNameItem = new System.Windows.Controls.MenuItem
        {
            Header = "Show Name", IsCheckable = true, IsChecked = _localCat.ShowName,
        };
        showNameItem.Click += (_, _) =>
        {
            _localCat.ShowName = showNameItem.IsChecked;
            Settings.Default.ShowName = showNameItem.IsChecked;
            Settings.Default.Save();
            RefreshOverlays();
        };

        // Sync position toggle
        var syncPosItem = new System.Windows.Controls.MenuItem
        {
            Header = "Sync Position", IsCheckable = true, IsChecked = _localCat.SyncPosition,
        };
        syncPosItem.Click += (_, _) =>
        {
            _localCat.SyncPosition = syncPosItem.IsChecked;
            Settings.Default.SyncPosition = syncPosItem.IsChecked;
            Settings.Default.Save();
        };

        // Power mode toggle
        var powerModeItem = new System.Windows.Controls.MenuItem
        {
            Header = "Power Mode", IsCheckable = true, IsChecked = _localCat.PowerMode,
        };
        powerModeItem.Click += (_, _) =>
        {
            _localCat.PowerMode = powerModeItem.IsChecked;
            Settings.Default.PowerMode = powerModeItem.IsChecked;
            Settings.Default.Save();
            if (!_localCat.PowerMode)
            {
                _localCat.ComboCount = 0;
                _localCat.Particles.Clear();
                RefreshOverlays();
            }
        };

        // Move toggle
        var moveItem = new System.Windows.Controls.MenuItem { Header = "Move Cat", IsCheckable = true };
        moveItem.Click += (_, _) => ToggleMoveMode(moveItem.IsChecked);

        // Room
        var roomHeader = new System.Windows.Controls.MenuItem { Header = "Room: Not connected", IsEnabled = false };
        var joinItem = new System.Windows.Controls.MenuItem { Header = "Join Room..." };
        joinItem.Click += (_, _) => ShowJoinDialog();
        var leaveItem = new System.Windows.Controls.MenuItem { Header = "Leave Room", IsEnabled = false };
        leaveItem.Click += async (_, _) => await LeaveRoom();

        // Chat
        var chatItem = new System.Windows.Controls.MenuItem { Header = "Open Chat" };
        chatItem.Click += (_, _) => ToggleChat();

        // Update
        var updateItem = new System.Windows.Controls.MenuItem { Header = "Check for Update" };
        updateItem.Click += async (_, _) =>
        {
            await _updateChecker.CheckAsync();
            if (_updateChecker.HasUpdate && _updateChecker.DownloadUrl != null)
            {
                var result = MessageBox.Show(
                    $"v{_updateChecker.LatestVersion} available. Download?",
                    "Update", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _updateChecker.DownloadUrl,
                        UseShellExecute = true,
                    });
            }
            else
            {
                MessageBox.Show("You're up to date!", "Update");
            }
        };

        // Quit
        var quitItem = new System.Windows.Controls.MenuItem { Header = "Quit" };
        quitItem.Click += (_, _) =>
        {
            Dispose();
            Application.Current.Shutdown();
        };

        menu.Items.Add(_keystrokeMenuItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(nameItem);
        menu.Items.Add(renameItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(themeMenu);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(showNameItem);
        menu.Items.Add(syncPosItem);
        menu.Items.Add(powerModeItem);
        menu.Items.Add(moveItem);
        menu.Items.Add(chatItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(roomHeader);
        menu.Items.Add(joinItem);
        menu.Items.Add(leaveItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(updateItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(quitItem);

        _trayIcon.ContextMenu = menu;

        // Store references for updating
        _trayIcon.Tag = new TrayMenuRefs(nameItem, roomHeader, joinItem, leaveItem, themeMenu);

        // Double-click opens chat
        _trayIcon.TrayMouseDoubleClick += (_, _) => ToggleChat();

        _room.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(RoomState.IsConnected))
                UpdateTrayRoomStatus();
        };
    }

    private void SetupInputMonitor()
    {
        _inputMonitor.OnActivity += () =>
        {
            _lastActivity = DateTime.Now;
            _localCat.IncrementKeystroke();
            _localCat.BumpCombo();
            if (!_localCat.IsActive)
            {
                _localCat.IsActive = true;
                Application.Current.Dispatcher.Invoke(RefreshOverlays);
            }
        };
        _inputMonitor.OnAllKeysReleased += () =>
        {
            if (_localCat.IsActive)
            {
                _localCat.IsActive = false;
                Application.Current.Dispatcher.Invoke(RefreshOverlays);
            }
        };
        _inputMonitor.Install();
    }

    private void SetupTimers()
    {
        // State sync timer (100ms = 10Hz)
        _stateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _stateTimer.Tick += async (_, _) => await SendStateUpdate();
        _stateTimer.Start();

        // Activity decay timer
        _activityTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _activityTimer.Tick += (_, _) =>
        {
            if (_localCat.IsActive && (DateTime.Now - _lastActivity).TotalMilliseconds > 300)
            {
                _localCat.IsActive = false;
                SaveKeystrokeCount();
                RefreshOverlays();
            }
        };
        _activityTimer.Start();
    }

    private void SetupWebSocket()
    {
        _wsClient.OnMessage += msg => Application.Current.Dispatcher.Invoke(() => HandleMessage(msg));
        _wsClient.OnDisconnected += () => Application.Current.Dispatcher.Invoke(() =>
        {
            _room.IsConnected = false;
            _room.Peers.Clear();
            RefreshOverlays();
        });
    }

    private void HandleMessage(WsMessage msg)
    {
        switch (msg.Type)
        {
            case "joined":
                _room.IsConnected = true;
                if (msg.Users != null)
                {
                    foreach (var u in msg.Users)
                    {
                        var peer = new CatState(u.UserId ?? "")
                        {
                            Name = u.Name ?? "Anonymous",
                            Theme = CatThemeExtensions.FromWireString(u.Theme),
                            AbsX = NormToAbsX(u.X ?? 0.85),
                            AbsY = NormToAbsY(u.Y ?? 0.85),
                            IsActive = u.Active ?? false,
                        };
                        _room.Peers.Add(peer);
                    }
                }
                RefreshOverlays();
                break;

            case "user_joined":
                var newPeer = new CatState(msg.UserId ?? "")
                {
                    Name = msg.Name ?? "Anonymous",
                    Theme = CatThemeExtensions.FromWireString(msg.Theme),
                };
                _room.Peers.Add(newPeer);
                RefreshOverlays();
                break;

            case "user_left":
                var leaving = _room.Peers.FirstOrDefault(p => p.UserId == msg.UserId);
                if (leaving != null)
                {
                    _room.Peers.Remove(leaving);
                    foreach (var overlay in _overlayWindows)
                        overlay.RemoveCat(leaving.UserId);
                    RemoveBubbles(leaving.UserId);
                }
                break;

            case "state":
                var peer2 = _room.Peers.FirstOrDefault(p => p.UserId == msg.UserId);
                if (peer2 != null)
                {
                    if (_localCat.SyncPosition)
                    {
                        if (msg.X.HasValue) peer2.AbsX = NormToAbsX(msg.X.Value);
                        if (msg.Y.HasValue) peer2.AbsY = NormToAbsY(msg.Y.Value);
                    }
                    // active 상태는 항상 반영
                    if (msg.Active.HasValue) peer2.IsActive = msg.Active.Value;
                    // combo
                    if (msg.Combo.HasValue)
                    {
                        var prevCombo = peer2.ComboCount;
                        peer2.ComboCount = msg.Combo.Value;
                        // Spawn peer particles when combo increases
                        if (msg.Combo.Value > prevCombo)
                            peer2.SpawnParticles();
                    }
                    RefreshOverlays();
                }
                break;

            case "theme":
                var peer3 = _room.Peers.FirstOrDefault(p => p.UserId == msg.UserId);
                if (peer3 != null)
                {
                    peer3.Theme = CatThemeExtensions.FromWireString(msg.Theme);
                    RefreshOverlays();
                }
                break;

            case "renamed":
                var peer4 = _room.Peers.FirstOrDefault(p => p.UserId == msg.UserId);
                if (peer4 != null)
                {
                    peer4.Name = msg.Name ?? "Anonymous";
                    RefreshOverlays();
                }
                break;

            case "chat":
                var bubble = new BubbleMessage
                {
                    UserId = msg.UserId ?? "",
                    Name = msg.Name ?? "",
                    Text = msg.Text ?? "",
                };
                _room.AddChatMessage(bubble);
                ShowBubble(bubble);
                break;
        }
    }

    private void ShowBubble(BubbleMessage msg)
    {
        if (!_activeBubbles.ContainsKey(msg.UserId))
            _activeBubbles[msg.UserId] = new();

        var list = _activeBubbles[msg.UserId];

        // Max 5 bubbles per user
        while (list.Count >= MaxBubbles)
        {
            list[0].Timer.Stop();
            list.RemoveAt(0);
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            list.RemoveAll(x => x.Msg == msg);
            RefreshOverlays();
        };
        timer.Start();

        list.Add((msg, timer));
        RefreshOverlays();
    }

    private void RemoveBubbles(string userId)
    {
        if (_activeBubbles.Remove(userId, out var list))
        {
            foreach (var (_, timer) in list)
                timer.Stop();
        }
    }

    private const int MaxBubbles = 5;

    private void RefreshOverlays()
    {
        // 로컬 고양이가 위치한 화면 찾기
        var catPoint = new System.Drawing.Point((int)_localCat.AbsX, (int)_localCat.AbsY);
        var catScreen = System.Windows.Forms.Screen.FromPoint(catPoint);

        foreach (var overlay in _overlayWindows)
        {
            var screen = GetOverlayScreen(overlay);
            if (screen?.DeviceName == catScreen.DeviceName)
            {
                var localBubbles = _activeBubbles.GetValueOrDefault(_localCat.UserId)?
                    .Select(x => x.Msg).ToList();

                overlay.UpdateCat(
                    _localCat.UserId, _localCat.AbsX, _localCat.AbsY,
                    _localCat.IsActive, _localCat.Theme,
                    _localCat.Name, isLocal: true, showName: _localCat.ShowName,
                    bubbles: localBubbles, isChatOpen: _localCat.IsChatOpen,
                    comboCount: _localCat.ComboCount, particles: _localCat.Particles);
            }
            else
            {
                // 다른 화면에서는 로컬 고양이 제거
                overlay.RemoveCat(_localCat.UserId);
            }

            // Peer cats on all screens
            foreach (var peer in _room.Peers)
            {
                var peerBubbles = _activeBubbles.GetValueOrDefault(peer.UserId)?
                    .Select(x => x.Msg).ToList();

                overlay.UpdateCat(
                    peer.UserId, peer.AbsX, peer.AbsY,
                    peer.IsActive, peer.Theme,
                    peer.Name, isLocal: false,
                    bubbles: peerBubbles,
                    comboCount: peer.ComboCount, particles: peer.Particles);
            }
        }
    }

    private async Task SendStateUpdate()
    {
        if (!_room.IsConnected || !_localCat.SyncPosition) return;

        var screen = System.Windows.Forms.Screen.PrimaryScreen!;
        var normX = (_localCat.AbsX - screen.Bounds.Left) / screen.Bounds.Width;
        // Y flip: macOS sends 1.0-y (Y-up system), Windows must match
        var normY = 1.0 - (_localCat.AbsY - screen.Bounds.Top) / screen.Bounds.Height;

        await _wsClient.SendAsync(new WsMessage
        {
            Type = "state",
            X = Math.Clamp(normX, 0, 1),
            Y = Math.Clamp(normY, 0, 1),
            Active = _localCat.IsActive,
            Combo = _localCat.PowerMode ? _localCat.ComboCount : null,
        });
    }

    private double NormToAbsX(double norm)
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen!;
        var x = screen.Bounds.Left + norm * screen.Bounds.Width;
        return Math.Clamp(x, screen.Bounds.Left + 40, screen.Bounds.Right - 40);
    }

    private double NormToAbsY(double norm)
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen!;
        // Y flip: server coordinates use macOS convention (1.0-y)
        var y = screen.Bounds.Top + (1.0 - norm) * screen.Bounds.Height;
        return Math.Clamp(y, screen.Bounds.Top + 40, screen.Bounds.Bottom - 40);
    }

    private void ToggleMoveMode(bool enable)
    {
        _moveMode = enable;
        foreach (var overlay in _overlayWindows)
            overlay.EnableDrag(enable);
    }

    private void ToggleChat()
    {
        if (_chatWindow?.IsVisible == true)
        {
            _chatWindow.HidePanel();
            return;
        }

        _chatWindow ??= CreateChatWindow();
        _chatWindow.ShowNear(_localCat.AbsX, _localCat.AbsY, 80);
    }

    private ChatWindow CreateChatWindow()
    {
        var cw = new ChatWindow();
        cw.OnSend += async text =>
        {
            if (_room.IsConnected)
            {
                await _wsClient.SendAsync(new WsMessage { Type = "chat", Text = text });
            }
        };
        cw.OnVisibilityChanged += visible =>
        {
            _localCat.IsChatOpen = visible;
            RefreshOverlays();
        };
        return cw;
    }

    private void ShowRenameDialog()
    {
        var input = InputDialog.Show("Enter your name:", "Rename", _localCat.Name);
        if (string.IsNullOrWhiteSpace(input)) return;

        var name = input.Trim()[..Math.Min(input.Trim().Length, 50)];
        _localCat.Name = name;
        Settings.Default.CatName = name;
        Settings.Default.Save();
        UpdateTrayName();

        if (_room.IsConnected)
        {
            _ = _wsClient.SendAsync(new WsMessage { Type = "rename", Name = name });
        }

        RefreshOverlays();
    }

    private async void ShowJoinDialog()
    {
        var code = InputDialog.Show("Enter room code:", "Join Room");
        if (string.IsNullOrWhiteSpace(code)) return;

        _room.RoomCode = code.Trim().ToUpper();
        await _wsClient.ConnectAsync(
            _room.RoomCode, _localCat.UserId,
            _localCat.Name, _localCat.Theme.ToWireString());
    }

    private async Task LeaveRoom()
    {
        await _wsClient.DisconnectAsync();
        _room.IsConnected = false;
        _room.Peers.Clear();
        _room.RoomCode = "";

        foreach (var overlay in _overlayWindows)
        {
            foreach (var peer in _room.Peers)
                overlay.RemoveCat(peer.UserId);
        }

        RefreshOverlays();
    }

    private void ChangeTheme(CatTheme theme)
    {
        _localCat.Theme = theme;
        Settings.Default.CatTheme = theme.ToWireString();
        Settings.Default.Save();

        // Update checkmarks
        if (_trayIcon?.Tag is TrayMenuRefs refs)
        {
            foreach (System.Windows.Controls.MenuItem item in refs.ThemeMenu.Items)
                item.IsChecked = item.Header.ToString() == theme.DisplayName();
        }

        if (_room.IsConnected)
        {
            _ = _wsClient.SendAsync(new WsMessage { Type = "theme", Theme = theme.ToWireString() });
        }

        RefreshOverlays();
    }

    private void UpdateTrayRoomStatus()
    {
        if (_trayIcon?.Tag is not TrayMenuRefs refs) return;

        refs.RoomHeader.Header = _room.IsConnected
            ? $"Room: {_room.RoomCode}"
            : "Room: Not connected";
        refs.JoinItem.IsEnabled = !_room.IsConnected;
        refs.LeaveItem.IsEnabled = _room.IsConnected;
    }

    private void UpdateTrayName()
    {
        if (_trayIcon?.Tag is TrayMenuRefs refs)
            refs.NameItem.Header = $"Name: {_localCat.Name}";
    }

    private void SavePosition()
    {
        Settings.Default.CatX = _localCat.AbsX;
        Settings.Default.CatY = _localCat.AbsY;
        Settings.Default.Save();
    }

    private static (double x, double y) ClampToScreen(double x, double y)
    {
        // 전체 가상 스크린 (모든 모니터 합친 영역)
        var vs = System.Windows.Forms.SystemInformation.VirtualScreen;
        const double margin = 40;
        var clampedX = Math.Clamp(x, vs.Left + margin, vs.Right - margin);
        var clampedY = Math.Clamp(y, vs.Top + margin, vs.Bottom - margin);
        return (clampedX, clampedY);
    }

    private static double GetDefaultX()
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen!;
        return screen.Bounds.Right - 120;
    }

    private static double GetDefaultY()
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen!;
        return screen.Bounds.Bottom - 120;
    }

    private static System.Windows.Forms.Screen? GetOverlayScreen(OverlayWindow overlay)
    {
        return System.Windows.Forms.Screen.FromPoint(
            new System.Drawing.Point((int)(overlay.Left + overlay.Width / 2),
                                     (int)(overlay.Top + overlay.Height / 2)));
    }

    private static int LoadKeystrokeCount()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        if (Settings.Default.KeystrokeDate != today)
        {
            Settings.Default.KeystrokeCount = 0;
            Settings.Default.KeystrokeDate = today;
            Settings.Default.Save();
            return 0;
        }
        return Settings.Default.KeystrokeCount;
    }

    private void SaveKeystrokeCount()
    {
        Settings.Default.KeystrokeCount = _localCat.KeystrokeCount;
        Settings.Default.KeystrokeDate = DateTime.Now.ToString("yyyy-MM-dd");
        Settings.Default.Save();
        if (_keystrokeMenuItem != null)
            _keystrokeMenuItem.Header = $"Today: {_localCat.KeystrokeCount:N0} keystrokes";
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        // Try to load from resources, fallback to default
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/app_icon.png");
            var stream = Application.GetResourceStream(uri)?.Stream;
            if (stream != null)
            {
                var bitmap = new System.Drawing.Bitmap(stream);
                return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
            }
        }
        catch { }

        return System.Drawing.SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _stateTimer?.Stop();
        _activityTimer?.Stop();
        _inputMonitor.Dispose();
        _wsClient.Dispose();
        _trayIcon?.Dispose();

        foreach (var overlay in _overlayWindows)
            overlay.Close();

        _chatWindow?.Close();
    }

    private record TrayMenuRefs(
        System.Windows.Controls.MenuItem NameItem,
        System.Windows.Controls.MenuItem RoomHeader,
        System.Windows.Controls.MenuItem JoinItem,
        System.Windows.Controls.MenuItem LeaveItem,
        System.Windows.Controls.MenuItem ThemeMenu);
}
