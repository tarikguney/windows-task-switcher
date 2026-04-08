using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WindowTaskSwitcher.Interop;
using WindowTaskSwitcher.Models;

namespace WindowTaskSwitcher.Services;

public sealed class WindowEnumerationService
{
    private readonly Dictionary<uint, BitmapSource?> _iconCache = new();
    private IntPtr _ownHandle;

    public void SetOwnHandle(IntPtr handle)
    {
        _ownHandle = handle;
    }

    public List<WindowInfo> GetWindows()
    {
        var windows = new List<WindowInfo>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (ShouldIncludeWindow(hWnd))
            {
                var info = CreateWindowInfo(hWnd);
                if (info != null)
                    windows.Add(info);
            }
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private bool ShouldIncludeWindow(IntPtr hWnd)
    {
        // Skip our own window
        if (hWnd == _ownHandle)
            return false;

        // Must be visible
        if (!NativeMethods.IsWindowVisible(hWnd))
            return false;

        // Must have a title
        if (NativeMethods.GetWindowTextLength(hWnd) == 0)
            return false;

        // Check extended styles
        int exStyle = NativeMethods.GetWindowLong(hWnd, NativeConstants.GWL_EXSTYLE);
        bool isToolWindow = (exStyle & NativeConstants.WS_EX_TOOLWINDOW) != 0;
        bool isAppWindow = (exStyle & NativeConstants.WS_EX_APPWINDOW) != 0;

        // Check owner
        IntPtr owner = NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER);
        bool hasOwner = owner != IntPtr.Zero;

        // Taskbar heuristic: show if no owner (top-level) or has WS_EX_APPWINDOW
        // Hide tool windows unless they explicitly have WS_EX_APPWINDOW
        if (isToolWindow && !isAppWindow)
            return false;
        if (hasOwner && !isAppWindow)
            return false;

        // Filter cloaked (hidden UWP) windows
        if (IsCloaked(hWnd))
            return false;

        return true;
    }

    private static bool IsCloaked(IntPtr hWnd)
    {
        int cloaked = 0;
        int result = NativeMethods.DwmGetWindowAttribute(hWnd, NativeConstants.DWMWA_CLOAKED,
            out cloaked, sizeof(int));
        return result == 0 && cloaked != 0;
    }

    private WindowInfo? CreateWindowInfo(IntPtr hWnd)
    {
        // Get title
        int length = NativeMethods.GetWindowTextLength(hWnd);
        if (length == 0) return null;

        var sb = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
        string title = sb.ToString();

        // Get process info
        NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
        string processName = GetProcessName(processId);

        // Get icon
        BitmapSource? icon = GetWindowIcon(hWnd, processId);

        return new WindowInfo
        {
            Handle = hWnd,
            Title = title,
            ProcessName = processName,
            ProcessId = processId,
            Icon = icon
        };
    }

    private static string GetProcessName(uint processId)
    {
        try
        {
            var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private BitmapSource? GetWindowIcon(IntPtr hWnd, uint processId)
    {
        if (_iconCache.TryGetValue(processId, out var cached))
            return cached;

        BitmapSource? icon = null;

        try
        {
            // Try WM_GETICON (small)
            IntPtr hIcon = NativeMethods.SendMessage(hWnd, NativeConstants.WM_GETICON,
                NativeConstants.ICON_SMALL2, 0);

            if (hIcon == IntPtr.Zero)
                hIcon = NativeMethods.SendMessage(hWnd, NativeConstants.WM_GETICON,
                    NativeConstants.ICON_SMALL, 0);

            if (hIcon == IntPtr.Zero)
                hIcon = NativeMethods.SendMessage(hWnd, NativeConstants.WM_GETICON,
                    NativeConstants.ICON_BIG, 0);

            // Fallback: GetClassLongPtr
            if (hIcon == IntPtr.Zero)
                hIcon = NativeMethods.GetClassLongPtr(hWnd, NativeConstants.GCLP_HICONSM);

            if (hIcon == IntPtr.Zero)
                hIcon = NativeMethods.GetClassLongPtr(hWnd, NativeConstants.GCLP_HICON);

            // Fallback: SHGetFileInfo from executable path
            if (hIcon == IntPtr.Zero)
            {
                hIcon = GetIconFromProcess(processId);
            }

            if (hIcon != IntPtr.Zero)
            {
                icon = Imaging.CreateBitmapSourceFromHIcon(hIcon,
                    Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                icon.Freeze(); // Allow cross-thread access
            }
        }
        catch
        {
            // Silently fail — icon is optional
        }

        _iconCache[processId] = icon;
        return icon;
    }

    private static IntPtr GetIconFromProcess(uint processId)
    {
        try
        {
            var process = Process.GetProcessById((int)processId);
            string? path = process.MainModule?.FileName;
            if (string.IsNullOrEmpty(path)) return IntPtr.Zero;

            var shFileInfo = new NativeMethods.SHFILEINFO();
            NativeMethods.SHGetFileInfo(path, 0, ref shFileInfo,
                (uint)Marshal.SizeOf(shFileInfo),
                NativeConstants.SHGFI_ICON | NativeConstants.SHGFI_SMALLICON);

            return shFileInfo.hIcon;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    public void ClearIconCache()
    {
        _iconCache.Clear();
    }
}
