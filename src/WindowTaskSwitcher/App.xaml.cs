using System.Threading;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using WindowTaskSwitcher.Interop;
using WindowTaskSwitcher.Models;
using WindowTaskSwitcher.Services;
using WindowTaskSwitcher.ViewModels;
using WindowTaskSwitcher.Views;

namespace WindowTaskSwitcher;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private TaskbarIcon? _trayIcon;
    private HotkeyService? _hotkeyService;
    private ThemeService? _themeService;
    private SwitcherWindow? _switcherWindow;
    private UserPreferences? _preferences;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single-instance guard
        _singleInstanceMutex = new Mutex(true, "WindowTaskSwitcher_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Window Task Switcher is already running.", "Window Task Switcher",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _preferences = UserPreferences.Load();

        // Apply system theme (dark/light) before creating any UI
        _themeService = new ThemeService();
        _themeService.ApplyTheme();

        // Create services
        var enumerationService = new WindowEnumerationService();
        var searchService = new FuzzySearchService();
        var learningService = new SearchLearningService();
        var switchService = new WindowSwitchService();

        // Create ViewModel
        var viewModel = new SwitcherViewModel(enumerationService, searchService, learningService, switchService);

        // Create the switcher window (hidden by default)
        _switcherWindow = new SwitcherWindow { DataContext = viewModel };
        _switcherWindow.Hide();

        // Set own handle for filtering
        var handle = new System.Windows.Interop.WindowInteropHelper(_switcherWindow).EnsureHandle();
        enumerationService.SetOwnHandle(handle);

        // Register hotkey
        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _hotkeyService.AltTabPressed += OnHotkeyPressed;

        // Default: Ctrl+Space
        bool registered = _hotkeyService.RegisterHotkey(_switcherWindow,
            NativeConstants.MOD_CONTROL, 0x20 /* VK_SPACE */);

        if (!registered)
        {
            MessageBox.Show(
                "Failed to register hotkey Ctrl+Space. Another application may be using it.\n\n" +
                "The app will still run in the system tray.",
                "Window Task Switcher",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Alt+Tab override (opt-in)
        if (_preferences.OverrideAltTab)
        {
            _hotkeyService.SetAltTabOverride(true);
        }

        // Setup tray icon
        SetupTrayIcon();
    }

    private void OnHotkeyPressed()
    {
        if (_switcherWindow == null) return;

        if (_switcherWindow.IsVisible)
            _switcherWindow.HideSwitcher();
        else
            _switcherWindow.ShowSwitcher();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Window Task Switcher (Ctrl+Space)",
            MenuActivation = PopupActivationMode.RightClick
        };

        var contextMenu = new System.Windows.Controls.ContextMenu();

        var altTabItem = new System.Windows.Controls.MenuItem
        {
            Header = "Override Alt+Tab",
            IsCheckable = true,
            IsChecked = _preferences?.OverrideAltTab ?? false
        };
        altTabItem.Click += (s, e) =>
        {
            bool enabled = altTabItem.IsChecked;
            _hotkeyService?.SetAltTabOverride(enabled);
            if (_preferences != null)
            {
                _preferences.OverrideAltTab = enabled;
                _preferences.Save();
            }
        };
        contextMenu.Items.Add(altTabItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (s, e) =>
        {
            _trayIcon?.Dispose();
            _hotkeyService?.Dispose();
            Shutdown();
        };
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextMenu = contextMenu;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _hotkeyService?.Dispose();
        _themeService?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
