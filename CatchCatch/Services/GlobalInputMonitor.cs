using System.Diagnostics;
using System.Runtime.InteropServices;
using CatchCatch.Helpers;

namespace CatchCatch.Services;

public sealed class GlobalInputMonitor : IDisposable
{
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private readonly NativeMethods.LowLevelProc _keyboardProc;
    private readonly NativeMethods.LowLevelProc _mouseProc;
    private readonly HashSet<uint> _pressedKeys = new();
    private bool _disposed;

    public event Action? OnActivity;
    public event Action? OnAllKeysReleased;
    public event Action<int, int>? OnLeftDown;
    public event Action<int, int>? OnLeftClick;
    public event Action<int, int>? OnMouseMove;

    public GlobalInputMonitor()
    {
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;
    }

    public void Install()
    {
        var moduleHandle = NativeMethods.GetModuleHandle(
            Process.GetCurrentProcess().MainModule?.ModuleName);

        _keyboardHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);

        _mouseHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
    }

    // IME/한영키 등 입력 언어 전환 키는 무시
    private static bool IsImeKey(uint vkCode) => vkCode is
        0x15 or   // VK_HANGUL (한영키)
        0x19 or   // VK_HANJA (한자키)
        0xE5 or   // VK_PROCESSKEY (IME 처리 중)
        0x1F or   // VK_IME_ON
        0x1A;     // VK_IME_OFF

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kb = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

            if (!IsImeKey(kb.vkCode))
            {
                int msg = wParam.ToInt32();
                if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
                {
                    _pressedKeys.Add(kb.vkCode);
                    OnActivity?.Invoke();
                }
                else if (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP)
                {
                    _pressedKeys.Remove(kb.vkCode);
                    if (_pressedKeys.Count == 0)
                        OnAllKeysReleased?.Invoke();
                }
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_RBUTTONDOWN)
            {
                OnActivity?.Invoke();
            }

            // Unmarshal once for all mouse position events
            if (msg is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_LBUTTONUP
                or NativeMethods.WM_MOUSEMOVE)
            {
                var ms = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                var (x, y) = (ms.pt.x, ms.pt.y);

                if (msg is NativeMethods.WM_LBUTTONDOWN)
                    OnLeftDown?.Invoke(x, y);
                else if (msg is NativeMethods.WM_MOUSEMOVE)
                    OnMouseMove?.Invoke(x, y);
                else
                    OnLeftClick?.Invoke(x, y);
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_keyboardHook != IntPtr.Zero)
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
        if (_mouseHook != IntPtr.Zero)
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
    }
}
