# Changelog

All notable changes to Zwitcher are listed here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/).

## [Unreleased]

### Changed

- Logging uses Serilog. The log rolls over at 1 MB and keeps up to 5 files, and errors are
  logged with their full exception.

## [1.0.1] - 2026-11-08

### Changed

- Settings and About are now one window with a *General* and an *About* page, and the tray
  menu has a single *Settings* item. It opens as a flyout in the bottom-right corner, directly
  above the taskbar, and closes when you click elsewhere. Left-click the tray icon to open it.
- The settings window follows the Windows light or dark mode.

## [1.0.0] - 2026-11-08

First release.

### Added

- Alt+Tab and Alt+Shift+Tab open Zwitcher instead of the native switcher. Release Alt to
  switch, Esc to cancel, a quick tap switches to the previous window.
- Windows in most-recently-used order, filtered like the native switcher: no tool windows,
  hidden UWP frames or windows on other virtual desktops.
- The list updates while open when windows open, close or change their title.
- Arrow keys while Alt is held, and mouse clicks on an entry.
- App icons, including packaged (Store) apps, with a fallback for hung windows.
- List and grid layouts.
- Shows on the primary monitor, the monitor with the cursor or the monitor with the
  active window. Per-monitor DPI aware; long lists scroll.
- Tray icon with a settings window, an *Enabled* toggle that hands Alt+Tab back to Windows,
  and *About*.
- Start with Windows (current user).
- Settings in `%AppData%\Zwitcher\settings.json`; a damaged file falls back to defaults.
- Log file in `%LocalAppData%\Zwitcher\logs`, rolled over at 1 MB. Crashes are logged, and
  the keyboard hook is removed so that the native Alt+Tab returns.
- Installers (`Zwitcher-win-Setup.exe` per user and `Zwitcher-win.msi` per system) built with
  [Velopack](https://velopack.io).
- Update check on start. When a newer version exists, Zwitcher shows a tray notification and
  an *Update to x.y.z* item in the tray menu. Nothing is installed until you click it; the app
  then downloads the update and restarts.

### Known limitations

- The per-user installer (`Zwitcher-win-Setup.exe`) and the portable zip still can't handle
  Alt+Tab over elevated windows. Windows grants `uiAccess` only to apps under Program Files.
  Switch to the MSI to get it.

[1.0.1]: https://github.com/3LK3/zwitcher/releases/tag/v1.0.1
[1.0.0]: https://github.com/3LK3/zwitcher/releases/tag/v1.0.0
