# Architecture

Zwitcher replaces the Windows Alt+Tab switcher. This document describes how the
pieces fit together, which thread owns what, and how events flow.

## Overview

```
 Hook thread                      UI thread (owns all state)
┌──────────────┐  PostMessage   ┌───────────────────────────┐
│ KeyboardHook │ ─────────────► │ SwitcherController        │
│ (WH_KEYBD_LL)│  (int codes)   │  state: Idle / Active     │
└──────────────┘                │  selection index          │
                                │                           │
 WinEvent hook ───────────────► │ WindowTracker (MRU list)  │
 (foreground, destroy,          │                           │
  name change)                  │ ──► IOverlayView (WPF)    │
                                │ ──► IWindowActivator      │
                                └───────────────────────────┘
```

The central point is **`SwitcherController`**, a state machine on the UI
thread. It is the only component that makes decisions. All other components
either produce events (the hooks and the tracker) or carry out commands (the view
and the activator).

## Threading model

There are exactly two threads.

| Thread | Owns | Must not |
|---|---|---|
| **Hook thread** | `WH_KEYBOARD_LL` hook and its message loop | touch UI, allocate in the callback, block, lock |
| **UI thread** | Everything else: controller, tracker, WinEvent hook, overlay, activation | do long work (it delays input handling) |

Rules:
- All mutable application state lives on the UI thread. **No locks.**
- The only data shared across threads is a couple of `volatile` flags (see
  [Hook-side state](#hook-side-state)) and posted messages.
- No `async`/`await` or thread-pool work in the core flow. If something slow is ever
  needed (e.g. loading UWP package icons), do it off-thread and post the result back
  to the UI thread.

## The message queue

We don't build our own queue. **The Win32 message queue of the UI thread is the
queue.**

- At startup, the App creates a hidden message-only window (`HwndSource` with
  `HWND_MESSAGE` parent) on the UI thread.
- The hook thread calls `PostMessage(hwnd, WM_SWITCHER_COMMAND, (WPARAM)command, 0)`.
- The `HwndSource` hook receives it and calls `controller.Handle(command)`.

Why `PostMessage` rather than `Dispatcher.BeginInvoke`: it allocates nothing, it
can't block, and it preserves order. All of this matters inside a low-level hook
callback.

### Commands

```csharp
enum SwitcherCommand
{
    Activate,       // Alt+Tab pressed while idle
    ActivatePrev,   // Alt+Shift+Tab pressed while idle
    Next,           // Tab while active
    Prev,           // Shift+Tab while active
    Confirm,        // Alt released while active
    Cancel,         // Esc while active
    WindowsChanged, // the list may have changed while active (not from the hook)
}
```

`WM_SWITCHER_COMMAND = WM_APP + 1`.

## Hook-side state

The hook has to decide **synchronously** whether to swallow a key. It can't wait
for the UI thread. It keeps no key state of its own, because tracked state can go stale:
we miss keys while an elevated window has focus, and a stale "Alt is down" would turn a
plain Tab into a switcher command.

- `altDown`: read from `LLKHF_ALTDOWN` on each event.
- `Shift held`: read with `GetAsyncKeyState`. `GetKeyState` wouldn't work, because the
  hook thread has no input queue of its own.
- `switcherActive`: a `volatile bool`. The hook sets it to `true` when it posts
  `Activate` or `ActivatePrev`, and the controller sets it to `false` when it returns
  to Idle.

Decision table inside the callback:

| Key event | Condition | Action |
|---|---|---|
| Tab down | `altDown && !switcherActive` | post `Activate` (or `ActivatePrev` if Shift held), swallow |
| Tab down | `switcherActive` | post `Next` / `Prev`, swallow |
| Esc down | `switcherActive` | post `Cancel`, swallow |
| Left / Up down | `switcherActive` | post `Prev`, swallow |
| Right / Down down | `switcherActive` | post `Next`, swallow |
| Alt up | `switcherActive` | post `Confirm`, **pass through** |
| anything else | — | pass through |

Alt key-up is always passed through so the system never sees a "stuck" Alt.

The hook callback does nothing beyond this table. No logging, no allocation, no
LINQ, no locks. The delegate is stored in a field so the GC can't collect it. The table
itself is the pure function `HookDecisions.Decide`, so it's unit tested.

## Components

### `KeyboardHook` (Core/Input)
- Starts a dedicated thread, installs `WH_KEYBOARD_LL`, runs `GetMessage` loop.
- Translates keys to `SwitcherCommand` via the table above and posts them.
- `Dispose` posts `WM_QUIT` to its thread and unhooks.
- `Enabled = false` passes every key through, so the native Alt+Tab works.

### `WindowTracker` (Core/Windows)
The tracker keeps the **order**, not the list. `Snapshot()` enumerates and filters on every
call (a few milliseconds, once per open), then sorts by `MruList`. So titles and eligibility
can't go stale, and a missed event costs order at worst, not a wrong list.

- On startup: `EnumWindows` → filter → seeds `MruList` (z-order as the initial guess).
- `WinEventSource` subscribes on the UI thread (`WINEVENT_OUTOFCONTEXT`, only `OBJID_WINDOW`
  / `CHILDID_SELF`). Our own process isn't skipped, because the settings window belongs in
  the MRU list. Showing the overlay does report a change, which the controller finds
  unchanged, at the cost of one snapshot per open.
  - `EVENT_SYSTEM_FOREGROUND`: move the window and its root owner to the front. A dialog
    isn't listed, so using it counts for its owner. Doesn't raise `WindowsChanged`, so rows
    don't reorder while tabbing.
  - `EVENT_SYSTEM_MINIMIZESTART`: move the window to the back.
  - `EVENT_OBJECT_DESTROY`: remove the window, so a reused handle can't inherit its rank.
    Raises `WindowsChanged` if it was known.
  - `EVENT_OBJECT_SHOW` / `HIDE` / `NAMECHANGE` / `CLOAKED` / `UNCLOAKED`: raise
    `WindowsChanged` for top-level windows.
- Exposes `Snapshot()`, `IsForeground(window)` and an event `WindowsChanged`.
- WinEvents can arrive during any message pumping, so `WindowsChanged` never calls the
  controller. The app posts `SwitcherCommand.WindowsChanged` instead, only while
  `switcherActive` is set. That keeps the message queue as the only queue.
- Eligibility filtering lives in a single `WindowFilter` class that reads window
  properties through `IWindowQuery`, so tests can use fake data.

`WindowFilter` follows [how Alt+Tab picks windows](https://devblogs.microsoft.com/oldnewthing/20071008-00/?p=24863).
Microsoft calls that an implementation detail, so native Alt+Tab is the reference. A
window is eligible if all of these hold:

1. It's visible and isn't a tool window. `WS_EX_TOOLWINDOW` counts as invisible.
2. It isn't cloaked (`DWMWA_CLOAKED`). This removes hidden UWP frames and windows on
   other virtual desktops, which `IsWindowVisible` still reports.
3. It isn't the shell's desktop window (`GetShellWindow`).
4. Its title isn't empty.
5. It represents its owner group. `WS_EX_APPWINDOW` windows count as having no owner.
   All others: start at the root owner and follow `GetLastActivePopup` to the first
   visible window, which must be this window.

For a hidden owner with a visible owned window, the article's text includes the owned
window but its pseudo-code doesn't. We follow the text.

```csharp
sealed record WindowInfo(nint Handle, string Title, int ProcessId, string? AppId);
```

### `SwitcherController` (Core)
State machine. See [State machine](#state-machine).
Depends only on interfaces: `IWindowTracker`, `IOverlayView`, `IWindowActivator`,
and the hook's `switcherActive` flag (via a small `ISwitcherState` interface).

### `IOverlayView` (Core interface, implemented in App)
```csharp
interface IOverlayView
{
    void Show(IReadOnlyList<WindowInfo> windows, int selectedIndex);
    void UpdateSelection(int selectedIndex);
    void UpdateWindows(IReadOnlyList<WindowInfo> windows, int selectedIndex);
    void Hide();
    event Action<int> ItemClicked;
}
```
The WPF implementation is a pre-created, hidden, borderless, topmost window. It
only renders what it is told. It also places DWM thumbnails (see [Thumbnails](#thumbnails)).

- **Alt+Tab:** the window sets `WS_EX_TOOLWINDOW`, which keeps it out of native Alt+Tab
  and out of `WindowFilter`.
- **Layouts:** a layout is a set of XAML resources, `{name}Width`, `{name}Panel` and
  `{name}Item`, picked by `OverlayLayout`. A new one needs those and an enum value, no
  controller change. `Next` and `Prev` stay linear in every layout.
- **Placement:** centered in the work area of the monitor from the *Show on* setting. Position
  and size are set in physical pixels with `SetWindowPos`, because WPF's `Left` and `Top`
  are unreliable across monitors with different DPI.
- **Icons:** `Show` only uses cached icons. Missing ones load afterwards, one
  `Dispatcher` item each at `Background` priority on the UI thread, so key presses run
  in between. `WM_GETICON` is a cross-process call, so it goes through
  `SendMessageTimeout` with `SMTO_ABORTIFHUNG`. The cache is per HWND and drops windows
  that are no longer listed. Lookup order: `WM_GETICON`, class icon, then a fallback icon,
  which a packaged app's icon replaces if there is one.
- **Packaged icons:** the frame belongs to ApplicationFrameHost and has no icon, so
  `PackagedIcons` goes through the AppUserModelID (`SHGetPropertyStoreForWindow`) and the
  shell (`IShellItemImageFactory`). Measured at up to 150 ms cold and 11 ms warm, so it runs
  on a pool thread and posts the result back. This is the one exception to the two threads.
  Nothing may escape from that thread, because an uncaught exception ends the process.
- **Long lists:** the height is capped at 80% of the work area. Rows scroll with a hidden
  scroll bar, and the selection is kept in view.
- **Warm-up:** the overlay is shown once at startup with `Opacity=0`, which takes the first
  real open from about 94 ms to 27 ms.

- **Mouse:** a click on a row raises `ItemClicked`. Hovering doesn't move the selection:
  the overlay opens under a resting cursor, so that could silently change the selection
  and a quick Alt+Tab would switch to the wrong window.

### `IWindowActivator` (Core/Activation)
Wraps bringing a window to the foreground: restore if minimized
(`ShowWindowAsync(SW_RESTORE)`, so a hung window can't block the UI thread), then
`SetForegroundWindow` on the last active popup if it's visible, else on the window.
That way focus lands in an open dialog, not in its disabled owner.

The overlay is never the foreground window (`ShowActivated="False"`), so Windows only
permits `SetForegroundWindow` if
[another condition](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setforegroundwindow)
applies. Measured on Windows 11 with injected keys, after ordinary input went to another
window:

| Trigger | Method | Result |
|---|---|---|
| `RegisterHotKey` | `SetForegroundWindow` | works |
| `WH_KEYBOARD_LL`, key swallowed or passed through | `SetForegroundWindow` | **fails** |
| `WH_KEYBOARD_LL` | with `AttachThreadInput` | fails |
| `WH_KEYBOARD_LL` | `SwitchToThisWindow` | fails |
| `WH_KEYBOARD_LL` | inject a key tap (Alt, F22 or `vkE8`) first | works |

The rule is per process, so it doesn't matter that the UI thread makes the call. A hook
only observes a key on its way to the foreground app, which is the process that received
the input. A process also keeps foreground rights from its launch until the next input, so
a test right after startup passes misleadingly.

**We inject a tap of the unassigned key `vkE8` (`MaskKey`), always from the UI thread and
never from the hook callback.** This relies on measured, undocumented behavior.

- `Prepare()`: the controller calls it first when opening, while Alt is still held. With
  Tab swallowed, the foreground app would see a lone Alt press and release, which opens
  its menu. The tap in between prevents that.
- `Activate()`: taps again right before `SetForegroundWindow`, because releasing Alt was
  input to another process, which ended the right from the first tap.

### Settings (Core, UI in App)
`SettingsService` holds the current `Settings` record on the UI thread. `Update` saves through
`ISettingsStore` and raises `Changed`, and one handler in the App applies the result to the
hook, the overlay and the tray. An update that changes nothing does nothing, so controls
showing the same setting can't echo each other.

`JsonSettingsStore` uses `%AppData%\Zwitcher\settings.json`. A missing or damaged file gives
the defaults, because a switcher must not refuse to start over a settings file. A failed save
is logged, and the setting still applies until the next start.

Start with Windows isn't in `Settings`. `StartupRegistration` reads and writes the HKCU `Run`
key directly, so the two can't disagree. At startup it follows the exe if it moved.

### Logging (Core/Diagnostics)
Code logs through `System.Diagnostics.Trace` (`TraceInformation`, `TraceWarning`,
`TraceError`), so Core needs no logger of its own and no static state. At startup the App adds
a `RollingFileTraceListener` writing to `%LocalAppData%\Zwitcher\logs\zwitcher.log`. It rolls
over at 1 MB, keeps three old files, and swallows its own write errors: logging must never
take the app down.

- **Never from the hook callback.** A file write there would delay every keystroke.
- Trace serializes listener calls (`Trace.UseGlobalLock`), so the icon pool thread may log
  too. This lock is inside the BCL and only on the logging path, not in our state.
- `Debug.WriteLine` lands in the same file in Debug builds and is compiled out of Release.
  Timing lines on the show path use it, so Release does no I/O there.

### Updates (App/Updates)
`UpdateService` wraps Velopack's `UpdateManager` with a `GithubSource`. At startup (Release
builds only) `App` runs one fire-and-forget check, so a slow network never delays the hook.
It does nothing when the app wasn't installed by Velopack. If an update exists, `TrayIcon`
shows a balloon and an *Update to x* menu item. Only a click downloads the update and calls
`ApplyUpdatesAndRestart`; nothing is installed unasked. Not part of the controller or the
hook.

### Launcher (Zwitcher.Launcher)
Signed builds of `Zwitcher.App.exe` have `uiAccess="true"` (see Known limitations). Windows
refuses to `CreateProcess` a uiAccess exe from a normal process (`ERROR_ELEVATION_REQUIRED`);
only `ShellExecute`, through the AppInfo service, works. Velopack's root stub, its shortcuts,
the restart after an update and its install/update hooks all use `CreateProcess` on the main
exe. So the main exe is `Zwitcher.exe`, a tiny launcher without uiAccess: it runs
`VelopackApp.Build().Run()` for the hooks, starts `Zwitcher.App.exe` next to it with
`UseShellExecute = true` (passing its arguments on), and exits. It has no UI and no other
logic. The Run key points straight at `Zwitcher.App.exe`, since Explorer starts it through the
shell anyway.

## State machine

```
            Activate
   ┌──────┐ ───────────────► ┌────────┐
   │ Idle │                  │ Active │ ◄── Next / Prev / WindowsChanged
   └──────┘ ◄─────────────── └────────┘
        Confirm / Cancel / ItemClicked
```

| State | Event | Action | Next |
|---|---|---|---|
| Idle | `Activate` | snapshot windows; select index 1, or 0 if there's only one or index 0 isn't in the foreground (desktop or taskbar has focus); `view.Show` | Active |
| Idle | `ActivatePrev` | snapshot windows; select the last index; `view.Show` | Active |
| Idle | other | ignore | Idle |
| Active | `Activate` / `ActivatePrev` | ignore (the hook never posts these while active) | Active |
| Active | `Next` / `Prev` | move selection with wrap-around; `view.UpdateSelection` | Active |
| Active | `WindowsChanged` | re-snapshot; if unchanged, nothing; else keep the same window selected if it still exists; `view.UpdateWindows` | Active |
| Active | `Confirm` / `ItemClicked(i)` | `view.Hide`; activate selected; clear `switcherActive` | Idle |
| Active | `Cancel` | `view.Hide`; clear `switcherActive` | Idle |

Edge cases:
- Empty window list on `Activate` / `ActivatePrev`: don't show anything. Clear the flag
  and return to Idle.
- Selected window closes while active: select the nearest remaining index.
- No windows left while active: hide without activating and return to Idle.
- Activation fails (window gone): ignore and stay Idle.

## Typical flow

```
User            Hook thread            UI thread
 │ Alt down  ──► (passes through)
 │ Tab down  ──► post Activate ───────► mask key, snapshot, Show, select [1]
 │ Tab down  ──► post Next ───────────► select [2]
 │ Alt up    ──► post Confirm ────────► Hide, mask key, SetForegroundWindow([2])
                                         switcherActive = false
                                         (later) EVENT_SYSTEM_FOREGROUND
                                         → tracker moves [2] to MRU front
```

## Thumbnails

- Thumbnails are drawn by DWM onto the overlay HWND via `DwmRegisterThumbnail`.
- The view reserves an empty placeholder element per item, converts its bounds
  to device pixels (per-monitor DPI aware), and calls
  `DwmUpdateThumbnailProperties`.
- WPF can't render on top of thumbnails (airspace). Selection highlights and labels
  go around the thumbnails, not over them.
- Register on `Show`, unregister all on `Hide`.
- Thumbnails belong purely to the view. The controller doesn't know about them.

## Startup and shutdown

Startup (App):
1. Single-instance check (named mutex).
2. Load the settings and repair the `Run` entry if the exe moved.
3. Create the `WinEventSource` and the `WindowTracker`, and do the initial enumeration.
4. Pre-create the overlay window hidden (create the HWND, so first show is instant).
5. Create the `SwitcherController` and the message-only window on the UI thread.
6. Start the `KeyboardHook` thread last.

Shutdown is the reverse: hook first (so no new commands arrive), then WinEvent
hooks, then windows.

The log starts right after step 1, so that a second instance can't write to the same file.

**Crashes:** `AppDomain.UnhandledException` covers every thread; WPF passes unhandled
UI-thread exceptions on to it. The handler logs the exception, disposes the hook, and removes
the tray icon if it runs on the UI thread. Then the process dies. Carrying on in an unknown
state is worse for a switcher than falling back to the native Alt+Tab. Windows would remove
the hook with the process anyway, but unhooking first means no keystroke waits on a dying
process. Unobserved task exceptions are only logged.

## Project mapping

| Component | Project / folder |
|---|---|
| `KeyboardHook`, `SwitcherCommand` | `Zwitcher.Core/Input` |
| `WindowTracker`, `WindowFilter`, `IWindowQuery`, `WindowInfo` | `Zwitcher.Core/Windows` |
| `WindowActivator` | `Zwitcher.Core/Activation` |
| `SwitcherController`, `IOverlayView`, `ISwitcherState`, `SingleInstanceGuard` | `Zwitcher.Core` |
| CsWin32 wrappers, `NativeMethods.txt` | `Zwitcher.Core/Interop` |
| `RollingFileTraceListener` | `Zwitcher.Core/Diagnostics` |
| `Settings`, `ISettingsStore`, `JsonSettingsStore`, `SettingsService`, `StartupRegistration` | `Zwitcher.Core` |
| `OverlayWindow` (implements `IOverlayView`), `SettingsWindow`, message window, tray | `Zwitcher.App` |
| Launcher (Velopack's main exe, starts the app via ShellExecute) | `Zwitcher.Launcher` |

## Testing

- **`WindowFilter`**: fake `IWindowQuery` with combinations of styles, cloaking
  and owners.
- **`SwitcherController`**: fake view, tracker and activator. Drive it with command
  sequences, e.g. `Activate, Next, Next, Confirm`, and assert the activated handle.
  Also cover wrap-around, cancel, and windows closing while active.
- **MRU logic**: feed foreground/destroy events to the tracker and assert the order.
- **The hook and real Win32 calls**: test manually. Keep that code thin so there's
  little to test.

## Known limitations

- While an elevated window has focus, the hook doesn't receive input (UIPI). Signed
  release builds work around this with `uiAccess="true"` (`app.uiaccess.manifest`):
  Windows honours it only for an Authenticode-signed exe installed in a secure location,
  which is why the MSI installs per machine under Program Files. Unsigned and Debug builds
  use `app.manifest` (`uiAccess="false"`) and keep the limitation, because Windows refuses
  to start an unsigned uiAccess exe. There is no workaround in code.
- Win+Tab and Task View are untouched.

## Release

`build/publish.ps1` publishes a self-contained, single-file `win-x64` `Zwitcher.App.exe`
(WPF's native DLLs included through `IncludeNativeLibrariesForSelfExtract`, no trimming,
which WPF doesn't support) and the trimmed, single-file launcher `Zwitcher.exe` into the same
folder, and packs both with `vpk pack --mainExe Zwitcher.exe` into `artifacts/release` (full and delta `.nupkg`,
`releases.win.json`, `RELEASES`, `assets.win.json`, `Setup.exe`, a per-machine `.msi`, `Portable.zip`). The version lives only in
`Directory.Build.props`. When `SIGNING_PFX_PATH` and `SIGNING_PFX_PASSWORD` are set,
publish.ps1 builds with `-p:UiAccess=true` and `vpk` signs the binaries; otherwise the build is
unsigned and without uiAccess (CI, local). On a `v*` tag, the release workflow decodes the
`SIGNING_PFX_BASE64` secret to a temp file and runs `build/release.ps1`, which fails unless the
tag is `v<Version>` and signing is configured, downloads the previous release for delta packages, and
creates the GitHub release with all those files and that version's CHANGELOG section. The
in-app update check reads these files through `GithubSource`.
