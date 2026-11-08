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

Each [release](https://github.com/3LK3/zwitcher/releases/latest) has two installers. .NET is
built into both.

| | `Zwitcher-win.msi` (recommended) | `Zwitcher-win-Setup.exe` |
|---|---|---|
| Installs to | `C:\Program Files\Zwitcher` (all users) | `%LocalAppData%\Zwitcher` (current user) |
| Admin rights | Needed to install and to update | Not needed |
| Alt+Tab over admin windows | Yes, see below | **No**, see [Known limitations](#known-limitations) |

1. Download one of them and run it. Windows SmartScreen may warn about a new installer:
   choose **More info → Run anyway**.
2. Zwitcher sits in the tray. Press **Alt+Tab**. To start it with Windows, open
   **Settings** from the tray menu and tick **Start with Windows**.

Alt+Tab while an **elevated (administrator) window** has focus works only with the MSI
install. Windows lets Zwitcher see keyboard input over elevated windows (`uiAccess`) only when
its exe is signed with a certificate the PC trusts **and** sits in a folder only
administrators can write to, such as Program Files. A per-user install in `%LocalAppData%`
can never meet the second condition.

**Updates:** Zwitcher checks for a new version when it starts. If there is one, it shows a
tray notification and an *Update to x.y.z* item in the tray menu; click it to download and
restart. An MSI install asks for admin rights to update. `Zwitcher-win-Portable.zip` is also
available, but it does not update itself and has the same limitation as the per-user install.

**Switching from the per-user install to the MSI:** exit Zwitcher, remove the per-user install
in **Settings → Apps**, then run the MSI.

**Files:** settings in `%AppData%\Zwitcher\settings.json`, logs in
`%LocalAppData%\Zwitcher\logs` (shown in *About*). If something goes wrong, the log is the
first place to look.

**Uninstall:** untick *Start with Windows*, choose *Exit* in the tray menu, then remove
Zwitcher in Windows **Settings → Apps**. Delete the two folders above to remove settings and
logs.

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

Release build, the same as CI makes it (self-contained single files, packed with Velopack
into `artifacts/release`):

```powershell
./build/publish.ps1
```

Without `SIGNING_PFX_PATH` and `SIGNING_PFX_PASSWORD` set, this builds an unsigned package
without `uiAccess`, so it keeps the elevated-window limitation. With them, it signs every exe,
the Setup.exe and the MSI, and the app gets `uiAccess`. The release workflow reads the
certificate from the `SIGNING_PFX_BASE64` and `SIGNING_PFX_PASSWORD` repository secrets.

`Zwitcher.exe` in an install is a small launcher. It starts `Zwitcher.App.exe` through the
shell, because Windows won't let other programs (like Velopack's shortcuts and updater) start
a `uiAccess` exe directly. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

CI runs on GitHub Actions: formatting check, build with warnings as errors, tests and a
publish. Pushing a tag like `v1.0.0` runs the release workflow, which builds the Velopack
files and creates a GitHub release with the notes from [CHANGELOG.md](CHANGELOG.md). The tag must
match `<Version>` in `Directory.Build.props`, the only place the version is set.

### Project layout

```
src/Zwitcher.App/          WPF app: overlay, tray icon, settings, message window
src/Zwitcher.Core/         Logic and Win32 interop (no UI): controller, hook, window tracking
src/Zwitcher.Launcher/     Zwitcher.exe: starts the app through the shell (see above)
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
  input to the hook, so Alt+Tab falls back to the native switcher there. Only the MSI
  install under Program Files avoids this (see [Install](#install)).
- **Per-user installs** (`Zwitcher-win-Setup.exe`, in `%LocalAppData%`) and the portable zip
  always have the limitation above. Windows grants `uiAccess` only to exes in folders that
  only administrators can write to, and a user's own folders never qualify. Zwitcher still
  works everywhere else.
- Unsigned and self-built copies also have the limitation, wherever they are installed.
- Win+Tab and Task View are untouched.
- SmartScreen may warn about the installer until the signing certificate has built up
  reputation.
- Releases are built for x64 only.
- The app exe is large (about 165 MB) because it carries the .NET runtime, WPF
  and WinForms.
