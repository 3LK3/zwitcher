# Changelog

All notable changes to Zwitcher are listed here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-11-07

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
- Self-contained single-file `win-x64` build; no .NET install needed.

[1.0.0]: https://github.com/3LK3/zwitcher/releases/tag/v1.0.0
