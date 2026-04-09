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

    public bool IsAnyKeyPressed => _pressedKeys.Count > 0;

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

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kb = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();

            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
            {
                if (_pressedKeys.Add(kb.vkCode))
                    OnActivity?.Invoke();
            }
            else if (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP)
            {
                _pressedKeys.Remove(kb.vkCode);
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
