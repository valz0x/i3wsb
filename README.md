# i3wsb — an i3-style workspace bar for Windows

[![build](https://github.com/torrex-v/i3wsb/actions/workflows/build.yml/badge.svg)](https://github.com/torrex-v/i3wsb/actions/workflows/build.yml)
[![latest release](https://img.shields.io/github/v/release/torrex-v/i3wsb?sort=semver)](https://github.com/torrex-v/i3wsb/releases/latest)
&nbsp;·&nbsp; Windows 11 &nbsp;·&nbsp; .NET 10 &nbsp;·&nbsp; MIT

A thin always-on-top bar that shows every Windows 11 **virtual desktop** and the
apps on it, i3-style, and switches between them with the keyboard.

![i3wsb](docs/i3wsb.png)

*`1 [firefox-code] | 2 [term] | 3 [slack] | 4 []` — the focused desktop is
highlighted, its apps listed by name; date + time on the right.*

- **`Win`+`1..4`** jumps to a desktop, **`Win`+`Shift`+`1..4`** throws the active
  window there. (Windows normally eats `Win`+number for the taskbar — see
  [How it works](#how-it-works).)
- Anchored to the top edge as a real **AppBar**, so maximized and snapped
  windows get pushed down instead of hiding under it — a proper i3 strut.
- Click a segment to switch. Hover for the full window titles.
- Right section shows the **date and time**, ticking every second.
- Auto-hides while a fullscreen app (a game, a video) is in front.

There's also an optional **icon-dock view** (a mac-style strip of the current
desktop's app icons) if you prefer that — toggle with `Ctrl`+`Alt`+`S`.

## Install

**Download** the latest `i3wsb-vX.Y.Z-win-x64.zip` from the
**[Releases](https://github.com/torrex-v/i3wsb/releases/latest)** page, unzip it
anywhere, and run `i3wsb.exe`. It's a self-contained build — no .NET install
needed. Keep `VirtualDesktopAccessor.dll` in the same folder.

Requires **Windows 11** (a recent build — the virtual-desktop COM API changes
between releases).

To start it with Windows, drop a shortcut to `i3wsb.exe` into `shell:startup`
(`Win`+`R` → type `shell:startup`).

### Build from source

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
git clone https://github.com/torrex-v/i3wsb.git
cd i3wsb
dotnet build -c Release
.\bin\Release\net10.0-windows\i3wsb.exe
```

The same self-contained zip the Releases page ships is produced by
`.github/workflows/release.yml` on every `v*` tag.

## Hotkeys

| Key                       | Action                                        |
|---------------------------|-----------------------------------------------|
| `Super`+`1..4`            | Switch to desktop 1–4 (created if missing)     |
| `Super`+`Shift`+`1..4`    | Move the active window to desktop 1–4          |
| `Ctrl`+`Alt`+`D`          | Show / hide the bar                            |
| `Ctrl`+`Alt`+`S`          | Toggle view: workspace bar ↔ icon dock         |
| `Ctrl`+`Alt`+`T`          | Toggle always-on-top                           |
| `Ctrl`+`Alt`+`F`          | Toggle auto-hide over fullscreen apps          |

`Super` is `Win` by default; switch it to `Alt` in the tray menu or config.
Top-row and numpad digits both work. Right-click the tray icon for every option.

## Settings

Persisted to `%LOCALAPPDATA%\i3wsb\config.ini`:

```ini
view=bar          ; or: icons
anchor=top        ; or: float  — float = a draggable content-width strip, no AppBar
super=win         ; or: alt
alwaysOnTop=true
autoHide=true
```

App names in bar view are the exe basename, lowercased and deduped
(`firefox.exe` → `firefox`); a small override map fixes cases where the exe
name isn't the brand (`msedge`→`edge`, `WindowsTerminal`→`term`, …). See
`AppNames.cs`.

## How it works

- **Virtual desktops** — all switching / querying / window-moving goes through
  [Ciantic's `VirtualDesktopAccessor.dll`](https://github.com/Ciantic/VirtualDesktopAccessor),
  the community shim over Microsoft's undocumented virtual-desktop COM API.
  Keep the DLL up to date with your Windows build.
- **Hotkeys** — a `WH_KEYBOARD_LL` hook, not `RegisterHotKey`. The shell reserves
  `Win`+`1..9` and `RegisterHotKey` silently fails for them, so the hook
  intercepts the keystroke, runs the action, and swallows it (with a `Ctrl` tap
  after `Win`+key so the Start menu doesn't open).
- **AppBar** — when anchored, the window registers via `SHAppBarMessage` so the
  shell reserves work area for it. This is released on exit; a *hard kill* can
  leave the strip reserved until Explorer restarts, same as any AppBar app.

## Caveats

- Windows 11 only; virtual-desktop behaviour depends on the OS and the bundled
  DLL, both of which Microsoft can break on a feature update.
- The global keyboard hook means `Win`+`1..4` (or `Alt`+`1..4`) no longer do
  their default thing while i3wsb runs.
- Multi-monitor: the AppBar targets the monitor the window is on; not heavily
  tested across mixed-DPI setups.

## License

[MIT](LICENSE). Bundled `VirtualDesktopAccessor.dll` is third-party MIT — see
[THIRD_PARTY.md](THIRD_PARTY.md).
