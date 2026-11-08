# AGENTS.md

Guidance for AI coding agents working in this repository.

**Read `docs/ARCHITECTURE.md` before changing structure, threading or event flow.**
It is the source of truth for components, the state machine and the message flow.
If a change requires deviating from it, propose the change first and update
ARCHITECTURE.md in the same change.

## Project

Zwitcher is a Windows replacement for the built-in Alt+Tab switcher. It intercepts
Alt+Tab with a low-level keyboard hook and shows a custom, compact overlay listing
open windows in most-recently-used order, with optional live DWM thumbnails.

Windows only. Target: Windows 10 22H2+ and Windows 11.

## Stack

- .NET 10 (`net10.0-windows`), C# latest, nullable enabled
- WPF for the overlay UI
- Win32 interop via **CsWin32** (`Microsoft.Windows.CsWin32`). Do not hand-write
  `[DllImport]` signatures. Add the API name to `NativeMethods.txt` instead.
- xUnit for tests

## Layout

```
src/Zwitcher.App/          WPF exe. App.xaml.cs is the composition root.
  Overlay/                 OverlayWindow (IOverlayView), OverlayRow, WindowIconCache
  SettingsUi/              SettingsWindow, GeneralPage, AboutPage, SettingsFlyoutOpener
  Tray/                    TrayIcon
  Updates/                 AppUpdater, UpdateFlow
  Debugging/               DebugCommands (Debug builds only)
src/Zwitcher.Core/         No UI. All logic and Win32 interop. Folders are features.
  Switching/               SwitcherController (central state machine), SwitcherCommand,
                           IOverlayView, IWindowActivator / Win32WindowActivator
  Input/                   KeyboardHook (dedicated thread), HookDecisionTable, CommandPoster
  WindowList/              WindowTracker, MruList, WindowFilter, IWindowQuery, WindowInfo
  Icons/                   WindowIconReader, PackagedAppIconReader
  Screen/                  Monitors, MonitorInfo, Win32Window, FlyoutPlacement
  Configuration/           Settings, SettingsStore, JsonSettingsFile, StartupRegistration
  Interop/                 NativeMethods.txt only
  SingleInstanceGuard.cs   Named mutex that detects a second launch
  ZwitcherLog.cs           Log file location (the Serilog logger is composed in App)
tests/Zwitcher.Core.Tests/ Unit tests for Core
```

Win32 code lives in the feature folder it serves. A Win32 implementation of a Core
interface is named `Win32<Name>` (`IWindowQuery` → `Win32WindowQuery`), its test fake
`Fake<Name>`. No `Service`/`Manager`/`Helper` suffixes; see Naming in ARCHITECTURE.md.

Dependency rules:
- `Zwitcher.Core` must never reference WPF or any UI assembly.
- `Zwitcher.App` depends on Core. Core talks to the UI only through `IOverlayView`.
- Win32 types (HWND, structs) stay inside `Core`. Expose small records like
  `WindowInfo` (handles as `nint`) to everything else.

## Commands

```
dotnet tool restore                 # once, restores CSharpier (local tool)
dotnet build
dotnet test
dotnet run --project src/Zwitcher.App
dotnet csharpier format .           # format all files
dotnet csharpier check .            # verify formatting (CI)
```

Before you call a task done: run `dotnet csharpier format .`, build, and run all
tests. Fix warnings you introduce.

## Architecture in one paragraph

Two threads. The **hook thread** runs only the `WH_KEYBOARD_LL` hook. It translates
keys into `SwitcherCommand` values and `PostMessage`s them to a hidden
message-only window on the **UI thread**. The UI thread owns everything else:
`SwitcherController` (the only component that makes decisions), `WindowTracker`
(MRU list fed by WinEvent hooks), the overlay view and activation. The Win32 message
queue *is* the queue. Don't introduce channels, the thread pool, or `async` into
the core flow.

## Hard rules

### Threading
- All mutable state lives on the UI thread. No locks.
- The only cross-thread data: posted messages and the `volatile bool switcherActive`
  flag (set by the hook on `Activate`, cleared by the controller on return to Idle).
- Use `PostMessage`, never `SendMessage` or `Dispatcher.Invoke`, from the hook thread.

### Keyboard hook (most important)
The callback runs on every keystroke system-wide. If it's slow, typing lags for the
whole OS, and Windows silently removes the hook after a timeout.
- The callback implements only the decision table in ARCHITECTURE.md. Nothing else.
- No allocation, logging, locks, awaits, LINQ, string formatting or exceptions in
  the callback.
- Keep the hook delegate in a field so the GC can't collect it.
- Never swallow Alt key-up. Always pass it through.
- Alt+Tab can't be registered with `RegisterHotKey`. The hook is the only way.
- Elevated-window limitation: the hook gets no input while an elevated window has
  focus. Don't try to work around this silently. It's documented as out of scope.

### Window tracking
- Eligibility filtering lives only in `WindowFilter` and reads through
  `IWindowQuery`, so it's testable with fake data.
- MRU order comes from `EVENT_SYSTEM_FOREGROUND`, not from z-order (z-order is
  only the initial guess at startup).
- No polling loops. Everything is event-driven.

### Overlay UI
- The overlay window is created hidden at startup and only shown and hidden after
  that. It must appear in well under 100 ms.
- Borderless, topmost, `ShowInTaskbar=False`, PerMonitorV2 DPI aware, multi-monitor
  correct.
- The view is dumb. It renders what the controller passes and reports clicks. No
  decisions in the view.
- DWM thumbnails: register on Show, unregister on Hide. WPF can't draw over them
  (airspace), so place highlights and labels around them. Thumbnails are view-only;
  the controller doesn't know about them.

### Performance
- No allocations on hot paths (hook callback; Next/Prev handling).
- Show-overlay path: no I/O, no reflection, no expensive icon loading. Cache icons.

## Code style

- Formatting is owned by **CSharpier**. Don't hand-format, don't argue with its
  output, and don't add formatting rules to `.editorconfig` that conflict with it.
  `.editorconfig` is for analyzer and naming rules only.
- Never reformat files you didn't otherwise change in a task (keeps diffs reviewable).
  Running `dotnet csharpier format .` is fine as long as the repo was already
  formatted.
- File-scoped namespaces, `sealed` by default, records for data.
- Small, focused classes. Constructor injection of interfaces in Core.
- No static mutable state, except where Win32 requires it. Document why when you
  use it.
- Native handles: wrap them in `SafeHandle` or release them deterministically in
  `Dispose`.
- Comments explain *why*, especially Win32 quirks. Link the relevant docs page.

## Testing

- `WindowFilter`: fake `IWindowQuery` covering styles, cloaking and owners.
- `SwitcherController`: fake view, tracker and activator. Drive it with command
  sequences (e.g. `Activate, Next, Next, Confirm`) and assert the activated handle.
  Cover wrap-around, cancel, an empty list, and windows closing while active.
- `WindowTracker` MRU logic: feed it events and assert the order.
- The hook and raw Win32 calls are tested manually. Keep that code thin.

## Working style for agents

- Make small, reviewable changes. One concern per change.
- For non-trivial tasks, give a short plan first (files, APIs to add to
  `NativeMethods.txt`) and wait for approval.
- If a Win32 behavior is uncertain, say so and propose how to verify it on a real
  machine. Don't guess silently.
- Don't add NuGet packages beyond CsWin32 and test libraries without asking.
- Don't change this file unless asked. Keep ARCHITECTURE.md in sync with
  structural changes.
- Don't commit any changes made