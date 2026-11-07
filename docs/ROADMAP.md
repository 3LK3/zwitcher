# Roadmap

From an empty tray app to **v1.0**: a daily-driver Alt+Tab replacement with a
clean base for future features. Each milestone is small enough for one or two
agent sessions. It ends in a working, committed state and follows
`ARCHITECTURE.md` and `AGENTS.md`.

The order is deliberate. The overlay and the controller are built and tested
**before** the keyboard hook takes over Alt+Tab. That way you can test everything
with a harmless debug hotkey, and a bug never leaves you without a working
switcher.

Legend: **Goal** – what the milestone achieves · **Scope** – what to build ·
**Done when** – acceptance criteria.

---

## M0 – Skeleton: tray app

✓ DONE

**Goal:** A background app that starts, sits in the tray and exits cleanly.

**Scope**
- `Zwitcher.App` starts without a main window (`ShutdownMode=OnExplicitShutdown`).
- Tray icon with a context menu: *About* and *Exit*. Decision: use the WinForms
  `NotifyIcon` (`<UseWindowsForms>true</UseWindowsForms>`, no extra NuGet).
  Isolate it in a `TrayIcon` class so it can be swapped for raw
  `Shell_NotifyIcon` later.
- Single-instance guard (named mutex). A second launch exits immediately.
- `app.manifest` with PerMonitorV2 DPI awareness.
- `Directory.Build.props`, CSharpier tool manifest, empty `NativeMethods.txt`.
- CI (GitLab CI, hosted runner `saas-windows-medium-amd64`, SDK from `global.json`):
  restore, `csharpier check`, build, test.

**Done when**
- The app starts to the tray. *Exit* closes it and the icon disappears.
- *About* shows the version.
- A second instance doesn't start.
- Task Manager's DPI awareness column shows Per-Monitor (v2).
- The pipeline passes.

---

## M1 – Message window and controller skeleton

✓ DONE

**Goal:** The central event path exists and is tested, before any real input.

**Scope**
- Hidden message-only window (`HwndSource`, `HWND_MESSAGE` parent) on the UI
  thread. It receives `WM_SWITCHER_COMMAND` (`WM_APP + 1`).
- `SwitcherCommand` enum, `ISwitcherState` (the `switcherActive` flag).
- `SwitcherController` state machine (Idle/Active), per ARCHITECTURE.md, against
  interfaces `IWindowTracker`, `IOverlayView` and `IWindowActivator`.
- Unit tests with fakes: `Activate, Next, Next, Confirm`, wrap-around, `Prev`,
  `Cancel`, empty list, and an ignored command in Idle.
- A debug tray item, *Post test commands*, that `PostMessage`s a command sequence
  to the message window, with a temporary logging view implementation.

**Done when**
- All controller tests pass.
- The debug item drives the controller through the real message queue. The log
  shows the expected state transitions.

---

## M2 – Window enumeration and filtering

✓ DONE

**Goal:** A correct list of the windows the native Alt+Tab would show.

**Scope**
- `IWindowQuery` (style, ex-style, cloaked, owner, title, visibility, process id)
  with a CsWin32-backed implementation.
- `WindowFilter`: visible, not cloaked, tool-window/app-window rules, root
  owner/last active popup, non-empty title.
- `WindowInfo` record. A first `WindowTracker` that only enumerates (z-order
  order, no events yet).
- Tests for `WindowFilter` with fake data covering each rule.
- A debug tray item, *Dump windows*, that writes the list to the debug output.

**Done when**
- The dump matches native Alt+Tab on a normal desktop: Explorer, browser, a UWP
  app (e.g. Settings), and a minimized window are included; tool windows, hidden
  UWP frames and windows on other virtual desktops are excluded.

---

## M3 – Overlay view (debug hotkey)

✓ DONE

**Goal:** The real overlay, shown without touching Alt+Tab.

**Scope**
- `OverlayWindow` implementing `IOverlayView`: borderless, topmost, no taskbar
  entry, absent from Alt+Tab (set `WS_EX_TOOLWINDOW`; `ShowInTaskbar=False` may not be enough), pre-created hidden at startup.
- Compact list layout: app icon and title, with a clear selection highlight.
- Icon loading (`WM_GETICON` / class icon fallback), cached per HWND/process,
  never loaded on the show path if a cached icon exists.
- Shown on the monitor of the current foreground window, centered.
- Temporary **debug hotkey** via `RegisterHotKey` (e.g. Ctrl+Alt+Space), cycling
  with repeated presses. This is wired to the controller as `Activate`/`Next`.
  The hotkey is removed in M5.

**Done when**
- The hotkey shows the overlay with the M2 list. Pressing it again moves the
  selection.
- The overlay appears without visible delay (< 100 ms; measure once with a
  `Stopwatch` in debug builds).

---

## M4 – Activation

**Goal:** Selecting an entry switches to that window.

**Scope**
- `WindowActivator`: restore if minimized, `SetForegroundWindow`, tolerate a
  window that has already closed.
- Mouse: click an item to activate it (`ItemClicked`). Hover may move the
  selection.
- Debug hotkey flow: Esc cancels, and Enter confirms (stand-in for Alt release).

**Done when**
- Activating normal, minimized and UWP windows works reliably. Focus lands in
  the target window, not just in the taskbar.
- Closing the target window right before confirming doesn't crash.
- Activation works with `ShowActivated="False"` on the overlay. ARCHITECTURE.md
  assumes the overlay is the foreground window, so verify on a real machine that
  `SetForegroundWindow` is still permitted.

---

## M5 – Keyboard hook: take over Alt+Tab

**Goal:** Alt+Tab opens Zwitcher instead of the native switcher. Dogfooding starts.

**Scope**
- `KeyboardHook` on a dedicated thread with its own message loop, implementing
  exactly the decision table in ARCHITECTURE.md (Activate/Next/Prev/Confirm/Cancel,
  never swallowing Alt key-up).
- The hook sets `switcherActive = true` when posting `Activate`. The controller
  clears it.
- Remove the debug hotkey.
- Tray toggle *Enabled*. When disabled, the hook passes everything through, so the
  native Alt+Tab works.
- Safety: if the app crashes, the hook dies with it and the native Alt+Tab
  returns. Verify this.

**Done when**
- Alt+Tab, Alt+Tab+Tab, Alt+Shift+Tab, Esc and a quick Alt+Tab tap all behave like
  the native switcher.
- No typing lag in normal use. Alt never gets "stuck".
- Killing the process from Task Manager restores the native Alt+Tab immediately.

---

## M6 – MRU tracking and live updates

**Goal:** Correct recency order, and a list that stays correct while open.

**Scope**
- `WindowTracker` subscribes to WinEvents (`WINEVENT_OUTOFCONTEXT`, UI thread):
  foreground, destroy, name change, show/hide, cloak/uncloak.
- MRU list: the foreground window moves to the front. Initial order comes from
  z-order at startup.
- `WindowsChanged` while Active: the controller re-snapshots and keeps the same
  window selected, or the nearest index if it closed.
- Tests: MRU ordering from event sequences, and controller behavior on changes
  while Active.

**Done when**
- A quick Alt+Tab tap always switches to the previously used window.
- Opening or closing windows while the overlay is open updates it without
  flicker or a wrong selection.

---

## M7 – Daily-driver polish

**Goal:** No reason to switch back to the native Alt+Tab.

**Scope**
- Multi-monitor: correct placement, DPI changes, a monitor plugged in or removed
  while running.
- Long lists: a sensible maximum height, scrolling, and keeping the selection
  visible.
- UWP/packaged app icons (via the AppUserModelID / package), with a fallback icon.
- Edge cases: hung windows (`WM_GETICON` already uses
  `SendMessageTimeout`), windows without a title, fullscreen games and
  exclusive-fullscreen apps.
- Keyboard extras: arrow keys while Alt is held.

**Done when**
- One week of daily use without needing the native switcher, and no open bugs
  marked *blocker*.

---

## M8 – Settings and startup

**Goal:** Configurable behavior, and the foundation for future features.

**Scope**
- `Settings` record and `ISettingsStore` (JSON in `%AppData%\Zwitcher`), loaded at
  startup. Changes are applied on the UI thread.
- Initial settings: enabled, start with Windows (HKCU `Run` key), show on
  (monitor with the cursor vs. the foreground window), and the layout variant.
- Simple settings window opened from the tray.
- Layout made pluggable: `OverlayWindow` picks an item template / layout strategy
  from settings, so new layouts (grid, grouped) need no controller changes.

**Done when**
- Settings persist across restarts. Start with Windows works.
- Switching the layout setting changes the overlay without a restart.

---

## M9 – v1.0 release

**Goal:** Installable, diagnosable, documented.

**Scope**
- Logging to a rolling file, **never from the hook callback**. Unhandled
  exceptions are logged, and the hook is uninstalled cleanly.
- Publish: self-contained, single-file `win-x64` build (plus `win-arm64` if
  wanted), zipped. GitLab CI release on tag.
- Versioning (`<Version>` in `Directory.Build.props`, SemVer). The About dialog
  shows the version.
- README: features, install, known limitations (elevated windows), build
  instructions. CHANGELOG.

**Done when**
- A fresh machine can download the zip, run it, enable start with Windows, and
  use Alt+Tab with no other setup.
- `v1.0.0` is tagged, and the release is built by CI.

---

## After 1.0 (ready for more)

The architecture already leaves room for these. Most are view or tracker changes;
the controller and the hook stay untouched.

- **Live thumbnails** (DWM). View-only, behind a setting (see ARCHITECTURE.md).
- **Grouping by app** and an app-level switch (Alt+`).
- **Type to filter / search** while the switcher is open (new hook-side rule:
  swallow letters while active, new `Filter(char)` command).
- **Filters:** current virtual desktop only, current monitor only.
- **Actions on the selected window:** close (e.g. `Q`), minimize, move to monitor.
- **Theming:** light/dark following the system, accent color, Mica-like backdrop.
- **Elevated window support** via `uiAccess=true` (signed build, installed under
  Program Files).
- **Installer** (MSIX or winget manifest).