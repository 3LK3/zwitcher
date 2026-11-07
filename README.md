# Zwitcher

[![CI](https://github.com/3LK3/zwitcher/actions/workflows/ci.yml/badge.svg)](https://github.com/3LK3/zwitcher/actions/workflows/ci.yml)

A fast, compact replacement for the Windows Alt+Tab switcher.

Zwitcher intercepts Alt+Tab with a low-level keyboard hook and shows a small overlay
listing your open windows in most-recently-used order, with live window previews.

> **Platform:** Windows 10 22H2 and Windows 11 only.

## Features

- **Alt+Tab / Alt+Shift+Tab** to cycle forward and backward, release Alt to switch.
- **MRU order** driven by foreground-change events, like the native switcher.
- **Native-like window list**: the same eligibility rules as Windows (no tool windows,
  cloaked UWP frames or windows on other virtual desktops).
- **Live DWM thumbnails** next to the app icons.
- **List and grid layouts**.
- **Multi-monitor and per-monitor DPI aware**: show the overlay on the primary monitor,
  the monitor under the cursor, or the monitor of the foreground window.
- **Arrow keys, Esc and mouse clicks** work while the overlay is open.
- **Instant overlay**: it is created hidden at startup and only shown and hidden after that.
- **Tray app** with a settings window, a toggle to hand Alt+Tab back to Windows, and
  optional start with Windows.

## Getting started

Requirements: Windows and the .NET SDK pinned in [`global.json`](global.json).

```powershell
dotnet tool restore
dotnet build
dotnet run --project src/Zwitcher.App
```

Zwitcher starts in the system tray. Press **Alt+Tab** to try it. Use the tray menu to open
the settings or exit.

## Development

```powershell
dotnet test                  # unit tests
dotnet csharpier format .    # format
dotnet csharpier check .     # verify formatting (CI)
```

CI runs on GitHub Actions: formatting check, build with warnings as errors, then tests.

### Project layout

```
src/Zwitcher.App/          WPF app: overlay, tray icon, settings, message window
src/Zwitcher.Core/         Logic and Win32 interop (no UI): controller, hook, window tracking
tests/Zwitcher.Core.Tests/ Unit tests for Core
docs/                      Architecture and roadmap
```

Built with .NET 10, WPF and [CsWin32](https://github.com/microsoft/CsWin32). The design
(two threads, `SwitcherController` state machine, message flow) is described in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md); planned work is in
[docs/ROADMAP.md](docs/ROADMAP.md).

## Known limitations

- Windows only.
- While an **elevated (administrator) window** has focus, Windows does not deliver keyboard
  input to the hook, so Alt+Tab falls back to the native switcher there.
