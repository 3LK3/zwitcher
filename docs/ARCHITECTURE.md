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
    Activate,   // Alt+Tab pressed while idle
    Next,       // Tab while active
    Prev,       // Shift+Tab while active
    Confirm,    // Alt released while active
    Cancel,     // Esc while active
}
```

`WM_SWITCHER_COMMAND = WM_APP + 1`.

## Hook-side state

The hook has to decide **synchronously** whether to swallow a key. It can't wait
for the UI thread. So it keeps a minimal state of its own:

- `altDown`: tracked from `VK_LMENU`/`VK_RMENU` down/up events (hook thread only).
- `switcherActive`: a `volatile bool`. The hook sets it to `true` when it posts
  `Activate`, and the controller sets it to `false` when it returns to Idle.

Decision table inside the callback:

| Key event | Condition | Action |
|---|---|---|
| Tab down | `altDown && !switcherActive` | post `Activate` (or `Prev` if Shift held), swallow |
| Tab down | `switcherActive` | post `Next` / `Prev`, swallow |
| Esc down | `switcherActive` | post `Cancel`, swallow |
| Alt up | `switcherActive` | post `Confirm`, **pass through** |
| anything else | — | pass through |

Alt key-up is always passed through so the system never sees a "stuck" Alt.

The hook callback does nothing beyond this table. No logging, no allocation, no
LINQ, no locks. The delegate is stored in a field so the GC can't collect it.

## Components

### `KeyboardHook` (Core/Input)
- Starts a dedicated thread, installs `WH_KEYBOARD_LL`, runs `GetMessage` loop.
- Translates keys to `SwitcherCommand` via the table above and posts them.
- `Dispose` posts `WM_QUIT` to its thread and unhooks.

### `WindowTracker` (Core/Windows)
- On startup: `EnumWindows` → filter → initial list (z-order as initial MRU guess).
- Subscribes (on the UI thread, `WINEVENT_OUTOFCONTEXT`) to:
  - `EVENT_SYSTEM_FOREGROUND`: move window to the front of the MRU list
  - `EVENT_OBJECT_DESTROY` (filtered to `OBJID_WINDOW`): remove window
  - `EVENT_OBJECT_NAMECHANGE`: update title
  - `EVENT_OBJECT_SHOW` / `EVENT_OBJECT_CLOAKED` / `UNCLOAKED`: re-evaluate eligibility
- Exposes `IReadOnlyList<WindowInfo> Snapshot()` and an event `WindowsChanged`.
- Eligibility filtering lives in a single `WindowFilter` class that reads window
  properties through `IWindowQuery`, so tests can use fake data.

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

### `IWindowActivator` (Core/Activation)
Wraps bringing a window to the foreground: restore if minimized
(`ShowWindow(SW_RESTORE)`), then `SetForegroundWindow`. While the overlay is
the foreground window, Windows permits the call.

## State machine

```
            Activate
   ┌──────┐ ───────────────► ┌────────┐
   │ Idle │                  │ Active │ ◄── Next / Prev / WindowsChanged / ItemHover
   └──────┘ ◄─────────────── └────────┘
        Confirm / Cancel / ItemClicked
```

| State | Event | Action | Next |
|---|---|---|---|
| Idle | `Activate` | snapshot windows; select index 1 (or 0 if only one); `view.Show` | Active |
| Idle | other | ignore | Idle |
| Active | `Next` / `Prev` | move selection with wrap-around; `view.UpdateSelection` | Active |
| Active | `WindowsChanged` | re-snapshot; keep the same window selected if it still exists; `view.UpdateWindows` | Active |
| Active | `Confirm` / `ItemClicked(i)` | `view.Hide`; activate selected; clear `switcherActive` | Idle |
| Active | `Cancel` | `view.Hide`; clear `switcherActive` | Idle |

Edge cases:
- Empty window list on `Activate`: don't show anything. Clear the flag and return to Idle.
- Selected window closes while active: select the nearest remaining index.
- Activation fails (window gone): ignore and stay Idle.

## Typical flow

```
User            Hook thread            UI thread
 │ Alt down  ──► altDown = true
 │ Tab down  ──► post Activate ───────► snapshot, Show, select [1]
 │ Tab down  ──► post Next ───────────► select [2]
 │ Alt up    ──► post Confirm ────────► Hide, SetForegroundWindow([2])
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
2. Create the message-only window and the `SwitcherController` on the UI thread.
3. Create the `WindowTracker` and do the initial enumeration.
4. Pre-create the overlay window hidden (create the HWND, so first show is instant).
5. Start the `KeyboardHook` thread last.

Shutdown is the reverse: hook first (so no new commands arrive), then WinEvent
hooks, then windows.

## Project mapping

| Component | Project / folder |
|---|---|
| `KeyboardHook`, `SwitcherCommand` | `Zwitcher.Core/Input` |
| `WindowTracker`, `WindowFilter`, `IWindowQuery`, `WindowInfo` | `Zwitcher.Core/Windows` |
| `WindowActivator` | `Zwitcher.Core/Activation` |
| `SwitcherController`, `IOverlayView`, `ISwitcherState`, `SingleInstanceGuard` | `Zwitcher.Core` |
| CsWin32 wrappers, `NativeMethods.txt` | `Zwitcher.Core/Interop` |
| `OverlayWindow` (implements `IOverlayView`), message window, tray | `Zwitcher.App` |

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

- While an elevated window has focus, the hook doesn't receive input. Fixes are to
  run elevated, or to use `uiAccess=true` (which requires a signed exe installed
  under Program Files). Out of scope for now.
- Win+Tab and Task View are untouched.
