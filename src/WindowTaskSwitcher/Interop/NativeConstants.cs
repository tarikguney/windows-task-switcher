namespace WindowTaskSwitcher.Interop;

internal static class NativeConstants
{
    // Window Styles Extended
    public const int GWL_EXSTYLE = -20;
    public const int GWL_STYLE = -16;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_APPWINDOW = 0x00040000;
    public const uint WS_EX_NOACTIVATE = 0x08000000;

    // DWM Window Attributes
    public const int DWMWA_CLOAKED = 14;

    // Window Messages
    public const uint WM_GETICON = 0x007F;
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_HOTKEY = 0x0312;

    // Icon sizes
    public const nint ICON_SMALL = 0;
    public const nint ICON_BIG = 1;
    public const nint ICON_SMALL2 = 2;

    // GetClassLong
    public const int GCL_HICON = -14;
    public const int GCL_HICONSM = -34;
    public const int GCLP_HICON = -14;
    public const int GCLP_HICONSM = -34;

    // ShowWindow commands
    public const int SW_RESTORE = 9;
    public const int SW_SHOW = 5;
    public const int SW_MINIMIZE = 6;

    // keybd_event
    public const byte VK_MENU = 0x12;
    public const byte VK_TAB = 0x09;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    // Low-level keyboard hook
    public const int WH_KEYBOARD_LL = 13;
    public const uint LLKHF_ALTDOWN = 0x20;

    // SHGetFileInfo flags
    public const uint SHGFI_ICON = 0x000000100;
    public const uint SHGFI_SMALLICON = 0x000000001;
    public const uint SHGFI_LARGEICON = 0x000000000;

    // RegisterHotKey modifiers
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_NOREPEAT = 0x4000;
}
