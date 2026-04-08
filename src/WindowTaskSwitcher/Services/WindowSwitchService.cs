using WindowTaskSwitcher.Interop;

namespace WindowTaskSwitcher.Services;

public sealed class WindowSwitchService
{
    public bool SwitchTo(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;

        try
        {
            // 1. If minimized, restore first
            if (NativeMethods.IsIconic(hWnd))
                NativeMethods.ShowWindow(hWnd, NativeConstants.SW_RESTORE);

            // 2. Simulate Alt keypress to satisfy foreground lock
            NativeMethods.keybd_event(NativeConstants.VK_MENU, 0, NativeConstants.KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            NativeMethods.keybd_event(NativeConstants.VK_MENU, 0,
                NativeConstants.KEYEVENTF_EXTENDEDKEY | NativeConstants.KEYEVENTF_KEYUP, UIntPtr.Zero);

            // 3. Attach to foreground thread
            IntPtr foregroundWindow = NativeMethods.GetForegroundWindow();
            uint foreThread = NativeMethods.GetWindowThreadProcessId(foregroundWindow, out _);
            uint curThread = NativeMethods.GetCurrentThreadId();

            bool attached = false;
            if (foreThread != curThread)
            {
                attached = NativeMethods.AttachThreadInput(foreThread, curThread, true);
            }

            // 4. Switch
            NativeMethods.SetForegroundWindow(hWnd);
            NativeMethods.BringWindowToTop(hWnd);

            // 5. Detach
            if (attached)
            {
                NativeMethods.AttachThreadInput(foreThread, curThread, false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool CloseWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;

        try
        {
            NativeMethods.SendMessage(hWnd, NativeConstants.WM_CLOSE, 0, 0);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
