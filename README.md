# Zwitcher

[![CI](https://github.com/3LK3/zwitcher/actions/workflows/ci.yml/badge.svg)](https://github.com/3LK3/zwitcher/actions/workflows/ci.yml)

A fast, compact replacement for the Windows Alt+Tab switcher.

Zwitcher intercepts Alt+Tab with a low-level keyboard hook and shows a small overlay
listing your open windows in most-recently-used order.

> **Platform:** Windows 10 22H2 and Windows 11 only.

## Features

- **Alt+Tab / Alt+Shift+Tab** to cycle forward and backward, release Alt to switch.
- **MRU order** driven by foreground-change events, like the native switcher.
- **Native-like window list**: the same eligibility rules as Windows (no tool windows,
  cloaked UWP frames or windows on other virtual desktops).
- **List and grid layouts**.
- **Multi-monitor and per-monitor DPI aware**: show the overlay on the primary monitor,
  the monitor under the cursor, or the monitor of the foreground window.
- **Arrow keys, Esc and mouse clicks** work while the overlay is open.
- **Instant overlay**: it is created hidden at startup and only shown and hidden after that.
- **Tray app** with a settings window, a toggle to hand Alt+Tab back to Windows, and
  optional start with Windows.

## Install

1. Download `Zwitcher-<version>-win-x64.zip` from the
   [latest release](https://github.com/3LK3/zwitcher/releases/latest).
2. Unzip it to a folder that stays put, e.g. `%LocalAppData%\Programs\Zwitcher`. The zip
   holds a single `Zwitcher.exe`, and nothing else needs installing: .NET is built in.
3. Run `Zwitcher.exe`. The exe isn't signed, so Windows SmartScreen may warn about it:
   choose **More info → Run anyway**.
4. Zwitcher sits in the tray. Press **Alt+Tab**. To start it with Windows, open
   **Settings** from the tray menu and tick **Start with Windows**.

If you move the exe later, start it once from the new place; it updates its start-up entry
itself.

**Files:** settings in `%AppData%\Zwitcher\settings.json`, logs in
`%LocalAppData%\Zwitcher\logs` (shown in *About*). If something goes wrong, the log is the
first place to look.

**Uninstall:** untick *Start with Windows*, choose *Exit* in the tray menu, then delete the
exe and the two folders above.

## Building

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

Release build, the same as CI makes it (self-contained single file, zipped into
`artifacts/`):

```powershell
./build/publish.ps1
```

CI runs on GitHub Actions: formatting check, build with warnings as errors, tests and a
publish. Pushing a tag like `v1.0.0` runs the release workflow, which builds the zip and
creates a GitHub release with the notes from [CHANGELOG.md](CHANGELOG.md). The tag must
match `<Version>` in `Directory.Build.props`, the only place the version is set.

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
- Win+Tab and Task View are untouched.
- The exe is unsigned, so SmartScreen may warn on first start.
- Releases are built for x64 only.
- The exe is large (about 165 MB, 68 MB zipped) because it carries the .NET runtime, WPF
  and WinForms.
