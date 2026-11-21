# i3wsb — an i3-style workspace bar for Windows

Windows 11 &nbsp;·&nbsp; .NET 10 &nbsp;·&nbsp; MIT

A lightweight always-on-top bar for Windows 11 that shows your virtual desktops, open windows, active window title, and system status widgets (CPU, RAM, volume, battery, clock).

![i3wsb](docs/i3wsb.png)

- **`Win`+`1..9`** switches between desktops 1 to 9.
- **`Win`+`Shift`+`1..9`** moves the active window to the target desktop.
- **Mouse scroll**: scroll the mouse wheel anywhere over the bar to cycle through desktops.
- **AppBar strut**: anchored to the top or bottom screen edge so maximized and snapped windows resize cleanly around it.
- **Widgets**: lightweight status modules on the right:
  - CPU and RAM monitors (using native Win32 system counters).
  - Master volume indicator: click to toggle mute, scroll wheel to adjust volume.
  - Battery percentage and charging indicator (automatically hidden on desktop PCs).
  - Date and time.
- **Active window**: shows the title of the focused window with click-to-focus.
- **Themes**: built-in dark palettes (Modern Dark, Catppuccin Mocha, Nord, Tokyo Night).
- **Auto-hide**: hides itself when a fullscreen app or game is running in front.
- **View modes**: switch between full workspace bar mode and icon dock mode (`Ctrl`+`Alt`+`S`). In both modes, workspace numbers and system widgets remain visible and active.

## Recommended setup and workflow

For a fast, keyboard-driven i3-like experience on Windows 11:

1. **Auto-hide the Windows taskbar**:
   Go to `Settings` -> `Personalization` -> `Taskbar` -> `Taskbar behaviors` and check `Automatically hide the taskbar`. Let i3wsb serve as your primary top or bottom statusbar.
2. **Dynamic desktop management**:
   Keep `autoPrune=true` enabled. Pressing `Win`+`2`, `Win`+`3`, or clicking `+` creates a new virtual desktop when you need one. Once you close all windows on that desktop and switch away, i3wsb automatically removes the empty desktop.
3. **Disable taskbar button flashing**:
   Windows normally flashes taskbar buttons when background apps want attention, which can be noisy across multiple virtual desktops. Check `Disable taskbar flashing` in the right-click menu (or set `disableTaskbarFlashing=true` in `config.ini`) to suppress flashing.

## Install

Download the latest `i3wsb-vX.Y.Z-win-x64.zip` from the [Releases](https://github.com/torrex-v/i3wsb/releases/latest) page, extract it, and run `i3wsb.exe`.

Keep `VirtualDesktopAccessor.dll` in the same directory as the executable.

To run it on startup, place a shortcut to `i3wsb.exe` in `shell:startup` (`Win`+`R` -> `shell:startup`).

### Build from source

Requires .NET 10 SDK.

```powershell
git clone https://github.com/torrex-v/i3wsb.git
cd i3wsb
dotnet build -c Release
.\bin\Release\net10.0-windows\i3wsb.exe
```

## Hotkeys

| Key | Action |
|---|---|
| `Super`+`1..9` | Switch to desktop 1–9 (created if missing) |
| `Super`+`Shift`+`1..9` | Move the active window to desktop 1–9 |
| `Ctrl`+`Alt`+`D` | Show or hide the bar |
| `Ctrl`+`Alt`+`S` | Toggle view: workspace bar / icon dock |
| `Ctrl`+`Alt`+`T` | Toggle always-on-top |
| `Ctrl`+`Alt`+`F` | Toggle auto-hide over fullscreen apps |

`Super` is `Win` by default; you can change it to `Alt` via the right-click tray menu or config file. Both top-row numbers and numpad digits are supported.

Right-click anywhere on the bar or on the tray icon to access settings (themes, widgets, position).

## Settings

Saved to `%LOCALAPPDATA%\i3wsb\config.ini`:

```ini
view=bar          ; bar or icons
anchor=top        ; top, bottom, or float
edge=top          ; top or bottom
super=win         ; win or alt
theme=dark        ; dark, catppuccin, nord, tokyonight
alwaysOnTop=true
autoHide=true
autoPrune=true    ; automatically removes empty virtual desktops
disableTaskbarFlashing=false ; stops background apps from flashing taskbar buttons
showCpu=true
showRam=true
showVolume=true
showBattery=true
showOpenApps=true ; show open apps for the current desktop in the center
showClock=true
```

## How it works

- **Virtual desktops**: all switching and window movement goes through `VirtualDesktopAccessor.dll`.
- **Hotkeys**: registered through a low-level keyboard hook (`WH_KEYBOARD_LL`) so Windows does not intercept `Win`+numbers for taskbar pinning.
- **AppBar docking**: uses `SHAppBarMessage` (`ABM_NEW` / `ABM_SETPOS`) to reserve work area at the top or bottom of the screen.

## License

MIT. Bundled `VirtualDesktopAccessor.dll` is third-party MIT (see THIRD_PARTY.md).
