# Windows Task Switcher — Implementation Plan

## Context

We're building a Windows-native equivalent of [Contexts.co](https://contexts.co/), a macOS app that replaces the default Cmd+Tab switcher with a fast, searchable, keyboard-driven window switcher. The goal is a lightweight productivity tool that lets you find and switch to any open window in 1-2 keystrokes via fuzzy search — something Windows lacks natively. The repo (`C:\Users\abguney\tools\windows-task-switcher`) is currently empty.

---

## Two Principal Engineers Discuss: Why This Matters

**Alex (PE, Developer Tools):**
> I have 30+ windows open at any time — VS Code, 8 Chrome tabs, Terminal, Teams, Outlook, Slack, OneNote. Alt+Tab is a slideshow I have to squint at. I end up clicking the taskbar like it's 2005. What I want is: press a key, type "sl gen" and instantly land on Slack #general. That's what Contexts does on Mac, and there's nothing like it on Windows.

**Sam (PE, Windows Platform):**
> The real productivity killer is the *context switch tax*. Every time you Alt+Tab through 15 windows, you lose 2-3 seconds and your train of thought. Multiply that by 200 switches/day and you're burning 10 minutes just *finding windows*. A fuzzy search switcher cuts that to under a second per switch. That's 8+ minutes back, but more importantly, you stay in flow.
>
> For the tech stack — this has to be **C# + WPF on .NET 8**. Here's why:
> - We need deep Win32 interop: `EnumWindows`, `SetForegroundWindow`, global hotkeys, DWM thumbnails. C# P/Invoke is first-class for all of these.
> - WPF gives us borderless transparent windows, hardware-accelerated rendering, and rich data templating — exactly what we need for the overlay.
> - WinUI 3 would force MSIX packaging and doesn't support transparent overlays well.
> - .NET 8 is LTS, cold-starts in ~300ms, and publishes as a single-file exe.
> - We don't need cross-platform. This is Windows-only, so lean into the platform.

**Alex:**
> Agreed on WPF. The search algorithm matters too. It needs to match non-consecutive characters — "chdev" should match "Chrome - DevTools". And it needs to learn: if I always pick Slack when I type "sl", that should float to the top over time. I don't want thumbnails or previews cluttering the UI — just a clean, fast, searchable list. Maybe a toggle for previews if someone wants them, but off by default.

**Sam:**
> Right. And can we actually *replace* Alt+Tab? That's the dream — muscle memory is already there.

**Alex:**
> Yes, but not with `RegisterHotKey` — Alt+Tab is a reserved system hotkey. We need a low-level keyboard hook (`WH_KEYBOARD_LL`). The hook intercepts `VK_TAB` with Alt held, suppresses the default switcher, and shows ours instead. The critical constraint is the hook callback must return within 1000ms or Windows silently kills it — so the callback just posts a message and returns immediately, all heavy work happens on the UI thread.

**Sam:**
> That's the right approach. We should make Alt+Tab override opt-in though — some users might want to keep the native switcher and just use Ctrl+Space for search. Two modes: search-first (Ctrl+Space) for the power users, and optionally replace Alt+Tab for the fully committed.
>
> The other hard engineering problem is `SetForegroundWindow`. Windows has a foreground lock that prevents apps from stealing focus. We'll need the classic `AttachThreadInput` workaround plus a simulated Alt keypress. If window switching doesn't work 100% of the time, the tool is useless. That's the make-or-break feature.

---

## Technology Stack (Verified)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Runtime | .NET 8.0 (LTS) | Stable, long-term support through Nov 2026 |
| UI Framework | WPF | Borderless transparent windows, hardware-accelerated, rich templating |
| Win32 Interop | P/Invoke (`[DllImport]`) | First-class in C#, all needed APIs are callable |
| MVVM Toolkit | CommunityToolkit.Mvvm 8.x | Source-generated ObservableObject/RelayCommand, eliminates boilerplate |
| Tray Icon | Hardcodet.NotifyIcon.Wpf | Better WPF integration than raw WinForms NotifyIcon |
| Serialization | System.Text.Json (built-in) | Settings and search history persistence |
| Packaging | Single-file publish (`dotnet publish -r win-x64 --self-contained`) | No installer needed for v1 |

**Rejected alternatives:**
- WinUI 3 — MSIX packaging complexity, no transparent overlay support
- C++ — 3-5x slower development for a UI-heavy app, no benefit over P/Invoke
- Rust + egui — poor native text rendering, verbose Win32 FFI
- Python + Qt — ~2s startup, 100MB+ package size

---

## Architecture

### Project Structure

```
windows-task-switcher/
├── WindowTaskSwitcher.sln
├── src/WindowTaskSwitcher/
│   ├── WindowTaskSwitcher.csproj
│   ├── App.xaml / App.xaml.cs              # Entry point, single-instance mutex, tray icon setup
│   ├── Views/
│   │   ├── SwitcherWindow.xaml/.cs         # Borderless overlay with search + results
│   │   └── SettingsWindow.xaml/.cs         # Hotkey config, startup toggle, theme
│   ├── ViewModels/
│   │   ├── SwitcherViewModel.cs            # Search state, filtered list, selection index
│   │   └── SettingsViewModel.cs
│   ├── Models/
│   │   ├── WindowInfo.cs                   # hwnd, title, processName, icon, desktopId
│   │   └── UserPreferences.cs              # Serializable settings model
│   ├── Services/
│   │   ├── WindowEnumerationService.cs     # EnumWindows + filtering + icon extraction
│   │   ├── WindowSwitchService.cs          # SetForegroundWindow with focus-steal workaround
│   │   ├── HotkeyService.cs               # RegisterHotKey + WM_HOTKEY via HwndSource
│   │   ├── FuzzySearchService.cs           # Scoring engine with acronym/position bonuses
│   │   ├── SearchLearningService.cs        # Frequency-based ranking boost, persisted
│   │   ├── VirtualDesktopService.cs        # IVirtualDesktopManager COM interop
│   │   ├── TrayIconService.cs              # System tray icon + context menu
│   │   └── StartupService.cs              # HKCU\...\Run registry key
│   ├── Interop/
│   │   ├── NativeMethods.cs               # All P/Invoke declarations
│   │   ├── NativeConstants.cs             # Win32 constants (WS_EX_*, WM_*, etc.)
│   │   └── VirtualDesktopInterop.cs       # COM interface definitions
│   ├── Converters/
│   │   └── BitmapToImageSourceConverter.cs
│   └── Resources/
│       ├── Styles.xaml                    # Dark theme resource dictionary
│       └── app.ico
├── tests/WindowTaskSwitcher.Tests/
│   ├── WindowTaskSwitcher.Tests.csproj
│   └── FuzzySearchTests.cs
└── .gitignore
```

### Data Flow

```
HotkeyService (Ctrl+Space) → SwitcherViewModel.Show()
  → WindowEnumerationService.GetWindows() [background thread]
  → SwitcherWindow becomes visible

User types → SwitcherViewModel.SearchText (PropertyChanged)
  → FuzzySearchService.Match(query, allWindows) → scored + sorted results
  → SearchLearningService.ApplyBoost(results) → re-ranked
  → SwitcherViewModel.FilteredWindows updates → UI re-renders

User presses Enter → WindowSwitchService.SwitchTo(hwnd)
  → SearchLearningService.RecordSelection(query, processName)
  → SwitcherWindow hides
```

---

## Core Implementation Details

### 1. Window Enumeration & Filtering (`WindowEnumerationService`)

Uses `EnumWindows` to iterate all top-level windows. A window appears in the switcher if:
1. `IsWindowVisible(hwnd) == true`
2. `GetWindowLong(GWL_EXSTYLE)` does NOT have `WS_EX_TOOLWINDOW`
3. `GetWindowText` length > 0
4. `DwmGetWindowAttribute(DWMWA_CLOAKED) == 0` (filters hidden UWP windows)
5. Not the switcher's own window
6. Owner is null OR has `WS_EX_APPWINDOW`

Icons extracted via: `SendMessage(WM_GETICON)` → `GetClassLong(GCL_HICON)` → `SHGetFileInfo` (fallback chain). Icons cached by `(processId, hwnd)`, extracted lazily on background thread.

### 2. Fuzzy Search Algorithm (`FuzzySearchService`)

Greedy character-by-character match with position-weighted scoring:

| Bonus | Points | Condition |
|-------|--------|-----------|
| Base match | 1 | Each matched character |
| Consecutive | 5 | Previous character also matched adjacently |
| Word start | 10 | Matched char follows space/punctuation or is at position 0 |
| Camel case | 8 | Matched char is uppercase, previous was lowercase |
| Prefix | 15 | Match starts at position 0 of candidate |
| Gap penalty | -1 | Each unmatched char between matches |

Returns both score AND matched character indices (for highlighted rendering).

**Learning:** `SearchLearningService` maintains `{(queryPrefix, processName) → count}` in `%APPDATA%\WindowTaskSwitcher\search_history.json`. Boost: `finalScore = fuzzyScore * (1 + 0.2 * min(count, 10))`. Counts decay monthly (halved).

### 3. Window Switching — The Hard Part (`WindowSwitchService`)

`SetForegroundWindow` alone fails due to Windows foreground lock. The workaround sequence:

```csharp
// 1. If minimized, restore first
if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);

// 2. Simulate Alt keypress to satisfy foreground lock
keybd_event(VK_MENU, 0, KEYEVENTF_EXTENDEDKEY, 0);
keybd_event(VK_MENU, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);

// 3. Attach to foreground thread
var foreThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
var curThread = GetCurrentThreadId();
AttachThreadInput(foreThread, curThread, true);

// 4. Now SetForegroundWindow will succeed
SetForegroundWindow(hwnd);
BringWindowToTop(hwnd);

// 5. Detach
AttachThreadInput(foreThread, curThread, false);
```

### 4. Global Hotkey & Alt+Tab Override (`HotkeyService`)

**Two hotkey mechanisms, both needed:**

**A) `RegisterHotKey` for Ctrl+Space (search mode):**
- Simple, reliable for the dedicated search hotkey
- Receives `WM_HOTKEY` via `HwndSource.AddHook`
- Default: `Ctrl+Space`. Configurable in settings.

**B) `WH_KEYBOARD_LL` low-level hook for Alt+Tab override:**
- `RegisterHotKey` **cannot** register Alt+Tab — it's a reserved system hotkey
- A low-level keyboard hook (`SetWindowsHookEx(WH_KEYBOARD_LL, ...)`) **can** intercept Alt+Tab
- The hook sees `WM_SYSKEYDOWN` for `VK_TAB` when Alt is held. Returning non-zero (not calling `CallNextHookEx`) suppresses the default Windows Alt+Tab switcher
- **Critical timeout constraint:** The hook callback must return within `LowLevelHooksTimeout` (max 1000ms on Windows 10 1709+). If it takes too long, Windows **silently removes the hook**. Solution: the callback only sets a flag/posts a message and returns immediately; all UI work happens on the WPF dispatcher thread
- The hook runs on the thread that installed it — WPF's dispatcher message loop satisfies this requirement
- **Cannot intercept:** Ctrl+Alt+Del (kernel-level SAS), Win+L on modern Windows
- Alt+Tab override is a **user-toggleable setting** (off by default) — some users may want to keep the native switcher

**Implementation in `HotkeyService`:**
```csharp
// Hook callback — must be FAST
private IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam) {
    if (nCode >= 0 && _overrideAltTab) {
        var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        bool altDown = (kbd.flags & LLKHF_ALTDOWN) != 0;
        if (altDown && kbd.vkCode == VK_TAB) {
            // Post message to UI thread, return immediately
            Dispatcher.BeginInvoke(() => ShowSwitcher());
            return (IntPtr)1; // Suppress default Alt+Tab
        }
    }
    return CallNextHookEx(_hookId, nCode, wParam, lParam);
}
```

### 5. Overlay UI (`SwitcherWindow.xaml`)

**Design principle: Clean, fast list. No visual noise.**

- `WindowStyle="None"`, `AllowsTransparency="True"`, `Topmost="True"`, `ShowInTaskbar="False"`
- Background: `#1E1E1E`, rounded corners (`CornerRadius="8"`), drop shadow
- Search box: `#2D2D2D`, 16px Segoe UI, auto-focused on show
- Results: 24x24 icon + window title, 14px Segoe UI, `#E0E0E0` text — **compact single-line rows**
- Selected item: `#264F78` background (VS Code blue)
- Matched characters: bold + `#4FC1FF` accent color
- Keyboard: Up/Down (move selection), Enter (switch), Escape (dismiss), Ctrl+W (close window)
- Centered on active monitor, 680px wide, height adapts to result count (max 500px)
- Dismisses on focus loss (`Deactivated` event)
- **Window thumbnails/previews: OFF by default.** Optional toggle in settings. When enabled, shows a DWM live thumbnail for the selected (highlighted) window in a side panel. When disabled (default), it's just the clean searchable list — no previews, no clutter.

### 6. System Tray + Settings

- `Hardcodet.NotifyIcon.Wpf` for tray icon with context menu: Settings, About, Quit
- Settings stored in `%APPDATA%\WindowTaskSwitcher\settings.json`
- Auto-start via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

### 7. Distribution & Installation

**No wizard installer.** Three distribution methods, simplest first:

**A) GitHub Releases (primary for v1):**
- `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`
- Produces a single `WindowTaskSwitcher.exe` (~60-80MB self-contained)
- User downloads and runs. No installation step. Store wherever they want.
- First run: prompt to add to startup (writes `HKCU\...\Run` registry key)

**B) Winget (future):**
- Winget supports standalone `.exe` installers with `InstallerType: exe`
- We'd create a minimal installer (Inno Setup or just the raw exe) that copies to `%LOCALAPPDATA%\WindowTaskSwitcher\`
- Submit a YAML manifest to `microsoft/winget-pkgs` repo — enables `winget install WindowTaskSwitcher`
- Manifest needs: `PackageIdentifier`, `PackageVersion`, `InstallerUrl` (GitHub release URL), `InstallerSha256`, `Architecture: x64`
- Silent install switch: `/VERYSILENT` (if using Inno) or just `/S` for the exe

**C) Scoop (alternative):**
- Even simpler than winget — just a JSON manifest pointing to the exe
- `scoop install window-task-switcher`

**For v1: Just publish to GitHub Releases as a single exe.** Winget/Scoop can come later when the tool is stable.

---

## Implementation Phases

### Phase 0: Repo Setup
1. Save this plan as `PLAN.md` in the repo root
2. `git init` the repo
3. Create the repo on GitHub under `tarikguney/windows-task-switcher` (public)
4. Add `.gitignore` for .NET
5. Commit plan + gitignore, push to GitHub

### Phase 1: Skeleton + Window Enumeration
1. `dotnet new wpf`, create solution structure
2. `NativeMethods.cs` with all P/Invoke declarations
3. `WindowEnumerationService` — enumerate, filter, extract titles and icons
4. `WindowInfo` model
5. Console test: print all visible window titles

### Phase 2: Search UI + Fuzzy Matching
1. `SwitcherWindow.xaml` — borderless overlay with TextBox + ItemsControl
2. `SwitcherViewModel` with `SearchText` binding and `FilteredWindows`
3. `FuzzySearchService` with scoring and match index tracking
4. Wire up: typing filters the list in real-time
5. Unit tests for fuzzy search edge cases

### Phase 3: Hotkey + Window Switching
1. `HotkeyService` using `RegisterHotKey`
2. `WindowSwitchService` with focus-steal workaround
3. Full cycle: Ctrl+Space → type → Enter → window switches → overlay hides

### Phase 4: Polish
1. `TrayIconService` — system tray with context menu
2. `StartupService` — registry auto-start toggle
3. `SearchLearningService` — persist selection frequency
4. `SettingsWindow` — hotkey config, startup toggle
5. Single-instance guard (named mutex)
6. Dark theme `Styles.xaml`

### Phase 5: Nice-to-Have
1. `VirtualDesktopService` — filter/group by virtual desktop
2. DWM live thumbnails on hover
3. Multi-monitor awareness
4. Highlighted matched characters in search results
5. Window close action (Ctrl+W)
6. ReadyToRun/single-file publish

---

## Verified Assumptions

All key assumptions have been verified as of 2026-04-08:

| Assumption | Status | Evidence |
|-----------|--------|----------|
| .NET 8 SDK available | **Verified** | `dotnet --list-sdks` shows 8.0.413 and 8.0.419 installed |
| .NET 10 SDK also available | **Verified** | 10.0.104 installed, but we use .NET 8 (LTS) for stability |
| WPF template available | **Verified** | `dotnet new wpf --list` shows template present |
| Hardcodet.NotifyIcon.Wpf supports .NET 8 | **Verified** | v2.0.1 targets `net8.0-windows7.0` |
| CommunityToolkit.Mvvm supports .NET 8 | **Verified** | v8.4.2 targets `net8.0` |
| `IVirtualDesktopManager` is a public API | **Verified** | Documented at learn.microsoft.com, CLSID `VirtualDesktopManager`, available Windows 10+. Exposes `IsWindowOnCurrentVirtualDesktop`, `GetWindowDesktopId`, `MoveWindowToDesktop` |
| `RegisterHotKey` works for Ctrl+Space | **Verified** | Documented Win32 API, receives `WM_HOTKEY` messages, supports `MOD_ALT`, `MOD_CONTROL`, `MOD_SHIFT`, `MOD_NOREPEAT` |
| `RegisterHotKey` CANNOT register Alt+Tab | **Verified** | Alt+Tab is a reserved system hotkey. `RegisterHotKey` will fail for it. |
| `WH_KEYBOARD_LL` CAN intercept Alt+Tab | **Verified** | Low-level keyboard hook sees `WM_SYSKEYDOWN` for `VK_TAB` with `LLKHF_ALTDOWN`. Returning non-zero suppresses default Alt+Tab. Callback must return within 1000ms (Win10 1709+) or hook is silently removed. Cannot intercept Ctrl+Alt+Del (kernel SAS). |
| **Alt+Space is NOT safe as default hotkey** | **CORRECTED** | Alt+Space opens the Windows system menu. **Default search hotkey: Ctrl+Space.** Alt+Tab override is a separate opt-in setting using `WH_KEYBOARD_LL`. |
| Win+key combos are reserved | **Verified** | MSDN: "Keyboard shortcuts that involve the WINDOWS key are reserved for use by the operating system." We will not use Win+key as default. |
| Winget supports standalone exe | **Verified** | `InstallerType: exe` is a supported type. Manifest requires `PackageIdentifier`, `PackageVersion`, `InstallerUrl`, `InstallerSha256`, `Architecture`. Can use `/S` or `/VERYSILENT` silent switches. |
| Single-file publish works | **Verified** | `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true` produces a single exe. .NET 8.0.419 SDK is installed. |

## Key Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| `SetForegroundWindow` fails | App is useless | `AttachThreadInput` + simulated Alt keypress workaround |
| UWP apps show as `ApplicationFrameHost` | Wrong icons/process names | Check `DWMWA_CLOAKED`, use AppUserModel.ID for UWP |
| Elevated (admin) windows can't be switched | Partial functionality | Document limitation; v1 skips this (most windows aren't elevated) |
| Virtual Desktop COM is undocumented (internal) | Limited desktop features | Use only documented `IVirtualDesktopManager` interface (3 methods, all public) |
| Icon extraction is slow for many windows | Laggy UI | Cache icons by process, extract lazily on background thread |
| Hotkey conflict with other apps | Hotkey registration fails | Detect failure from `RegisterHotKey` return value, prompt user to pick alternate |

---

## Verification Plan

1. **Build:** `dotnet build` succeeds with zero warnings
2. **Run:** Launch app, verify tray icon appears
3. **Hotkey:** Press Ctrl+Space, verify overlay appears centered on active monitor
4. **Enumeration:** Verify all visible windows appear (Chrome, VS Code, Terminal, etc.)
5. **Search:** Type partial names, verify fuzzy matching and ranking
6. **Switch:** Press Enter, verify target window comes to foreground
7. **Dismiss:** Press Escape, verify overlay hides
8. **Learning:** Repeat same search+selection 3 times, verify it ranks higher
9. **Close window:** Select a window, press Ctrl+W, verify it closes
10. **Unit tests:** `dotnet test` — fuzzy search scoring tests pass
