using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WindowTaskSwitcher.Interop;

namespace WindowTaskSwitcher.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 9000;
    private HwndSource? _source;
    private IntPtr _windowHandle;
    private IntPtr _keyboardHookId;
    private NativeMethods.LowLevelKeyboardProc? _hookProc;
    private bool _overrideAltTab;

    public event Action? HotkeyPressed;
    public event Action? AltTabPressed;

    public bool RegisterHotkey(Window window, uint modifiers, uint key)
    {
        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.Handle;

        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(WndProc);

        return NativeMethods.RegisterHotKey(_windowHandle, HotkeyId, modifiers | NativeConstants.MOD_NOREPEAT, key);
    }

    public void SetAltTabOverride(bool enabled)
    {
        _overrideAltTab = enabled;

        if (enabled && _keyboardHookId == IntPtr.Zero)
        {
            InstallKeyboardHook();
        }
        else if (!enabled && _keyboardHookId != IntPtr.Zero)
        {
            UninstallKeyboardHook();
        }
    }

    private void InstallKeyboardHook()
    {
        _hookProc = LowLevelKeyboardCallback;
        _keyboardHookId = NativeMethods.SetWindowsHookEx(
            NativeConstants.WH_KEYBOARD_LL,
            _hookProc,
            NativeMethods.GetModuleHandle(null),
            0);
    }

    private void UninstallKeyboardHook()
    {
        if (_keyboardHookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHookId);
            _keyboardHookId = IntPtr.Zero;
        }
        _hookProc = null;
    }

    private IntPtr LowLevelKeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _overrideAltTab)
        {
            var kbd = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            bool altDown = (kbd.flags & NativeConstants.LLKHF_ALTDOWN) != 0;

            if (altDown && kbd.vkCode == NativeConstants.VK_TAB)
            {
                // Post to UI thread and return immediately (must be fast)
                Application.Current?.Dispatcher.BeginInvoke(() => AltTabPressed?.Invoke());
                return (IntPtr)1; // Suppress default Alt+Tab
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeConstants.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UninstallKeyboardHook();

        if (_windowHandle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, HotkeyId);
        }

        _source?.RemoveHook(WndProc);
        _source = null;
    }
}
