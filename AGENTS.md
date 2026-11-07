# AGENTS.md

Guidance for AI coding agents working in this repository.

## Project

Switcher is a Windows replacement for the built-in Alt+Tab switcher. It intercepts
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
src/Switcher.App/          WPF exe: overlay window, tray icon, startup, settings UI
src/Switcher.Core/         No UI. All Win32 logic.
  Interop/                 NativeMethods.txt, thin safe wrappers around CsWin32 output
  Input/                   Low-level keyboard hook on a dedicated thread
  Windows/                 Window enumeration, filtering, MRU tracking
  Activation/              Bringing the chosen window to the foreground
tests/Switcher.Core.Tests/ Unit tests for Core
```

Rules:
- `Switcher.Core` must never reference WPF or any UI assembly.
- `Switcher.App` talks to Core through interfaces/services, not raw Win32 calls.
- Keep Win32 types (HWND, etc.) inside `Core`. Expose small records
  (e.g. `WindowInfo`) to the App.

## Commands

```
dotnet build
dotnet test
dotnet run --project src/Switcher.App
```

Build and run all tests before you call a task done. Fix warnings you introduce.

## Critical constraints

### Keyboard hook (most important)
The `WH_KEYBOARD_LL` callback runs on every keystroke system-wide. If it is slow,
typing lags for the whole OS, and Windows silently removes the hook after a timeout.
- The hook runs on its own dedicated thread with its own message loop, never the
  UI thread.
- The callback must do almost nothing: inspect the key, possibly swallow it, post a
  message or signal, and return. No allocation, no logging, no locks, no awaits,
  no LINQ.
- Keep the delegate rooted (stored in a field) so the GC does not collect it.
- Alt+Tab cannot be registered with `RegisterHotKey`; the hook is the only way.
- Known limitation: the hook does not receive input while an elevated window has
  focus, unless the app runs elevated or with `uiAccess=true`. Don't try to work
  around this silently. Document it.

### Window enumeration and filtering
"Real" alt-tab windows are identified by: visible, not cloaked
(`DWMWA_CLOAKED`), not `WS_EX_TOOLWINDOW` (unless `WS_EX_APPWINDOW`),
root owner / last active popup rules, and non-empty title. Keep this logic in one
place and cover it with tests using fake window data (abstract the Win32 queries
behind an interface so tests don't need real windows).

### MRU order
Track foreground changes with `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`. Don't rely
on z-order alone.

### Overlay UI
- Pre-create the overlay window at startup and keep it hidden; show/hide only.
  It must appear in well under 100 ms.
- Borderless, topmost, `ShowInTaskbar=False`, must not steal focus in ways that
  break activation of the target window.
- Live thumbnails use `DwmRegisterThumbnail` onto the overlay HWND. WPF cannot draw
  on top of thumbnails (airspace), so the layout reserves placeholder rectangles.
  Unregister thumbnails when the overlay hides.
- Must be correct with per-monitor DPI (PerMonitorV2) and multiple monitors.

### Performance
- Avoid allocations on hot paths (hook, show overlay).
- No background polling loops. Use events/hooks.

## Code style

- File-scoped namespaces, `sealed` by default, records for data.
- Prefer small, focused classes. No static mutable state except where Win32
  requires it (and then document why).
- Wrap native handles in `SafeHandle`, or make sure they are released deterministically.
- Comments explain *why*, especially for Win32 quirks. Link the relevant docs page.

## Working style for agents

- Make small, reviewable changes. One concern per change.
- If a Win32 behavior is uncertain, say so and propose how to verify it on a real
  machine. Don't guess silently.
- Don't add NuGet packages beyond CsWin32 and test libs without asking.
- Don't change this file unless asked.