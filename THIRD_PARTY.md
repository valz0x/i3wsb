# Third-party components

## VirtualDesktopAccessor.dll

`VirtualDesktopAccessor.dll` is committed in the repo root as a convenience so
the app runs without a separate download. It is a third-party binary.

| | |
|---|---|
| Project | [Ciantic / VirtualDesktopAccessor](https://github.com/Ciantic/VirtualDesktopAccessor) |
| Author | Jari Otto Oskari Pennanen |
| License | MIT — full text in [`third-party/VirtualDesktopAccessor/LICENSE.txt`](third-party/VirtualDesktopAccessor/LICENSE.txt) |

The DLL wraps **undocumented** Windows virtual-desktop COM interfaces, so a major
Windows update can require a newer build. Grab releases from the
[upstream releases page](https://github.com/Ciantic/VirtualDesktopAccessor/releases)
and drop the new `VirtualDesktopAccessor.dll` next to `i3wsb.exe`.
