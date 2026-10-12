# AORUS Sensor Panel — FAN MADE

A Windows sensor dashboard for a dedicated display, with AORUS-inspired themes,
Expanse-inspired ship consoles and an interactive Sol navigation view. It runs
without Rainmeter or the HWiNFO application.

[Download the installer](https://github.com/convexrook2/aorus-sensor-panel/releases/latest)
from GitHub Releases. Releases include setup instructions and SHA-256 checksums.
Hardware support depends on the available sensor interfaces and drivers.

## Requirements

- Windows x64 and a display exposed as a Windows monitor. USB panels using a
  proprietary data protocol require a separate vendor integration.
- [Microsoft .NET 10 Desktop Runtime, Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0),
  stable 10.0.x. Setup checks for it and offers the official download if missing.
  Windows does not guarantee this runtime is installed. The SDK is not required;
  .NET Framework, an x86 runtime or the core runtime alone do not satisfy it.
- Administrator access and an existing [PawnIO 2.2+ driver](https://pawnio.eu)
  for supported CPU and motherboard readings. Setup does not install a driver.
- An installed NVIDIA driver for NVIDIA GPU readings. Other GPU vendors are
  not currently supported by this collector.

CPU and motherboard compatibility is limited by the embedded hardware library
and additional CPU-specific checks. Vcore and live RAM-clock decoding support
only explicitly admitted processor/firmware layouts. This is not a promise of
support for every motherboard or processor. Unsupported or stale readings show
an em dash rather than an estimated value.

## Install and choose a display

1. Run `AORUS-Sensor-Panel-FAN-MADE-Setup.exe` from a release.
2. Install the required Desktop Runtime if setup requests it, then choose
   **Check again**.
3. Launch the panel and accept the administrator prompt for hardware access.
4. From the FAN MADE tray icon, choose **Open settings**, then **Move to display** and **Fit to display**
   to save a dedicated display.

Automatic fullscreen avoids taking over the primary workspace. With no suitable
saved display, the app starts in the tray. Fullscreen
on a secondary display keeps the tray icon without a taskbar button. Restoring
a window or moving to the primary display restores the taskbar button.

Disconnecting the selected secondary display hides the panel and retains its
selection. Reconnecting restores a previously visible panel without taking
focus. Choosing **Hide to tray** cancels that automatic restoration.

**Start with Windows** is optional and initially off. It creates a current-user
Task Scheduler logon task with the permissions needed for sensor access and a
short sign-in delay. It stores no password and installs no background service.
Use **Exit** in the tray before updating or uninstalling. Uninstall removes the
application and its startup task; shared runtimes, drivers and user preferences
are preserved.

## Controls and appearance

Click the settings gear or right-click the panel. The compact themed menu
includes display placement, layout, scaling, polling, theme, FPS selection,
README and tray controls. **Back** returns within settings; **Escape** closes
the menu. The tray menu remains available while the panel is hidden.

- **Layout:** automatic landscape, wide-strip and portrait arrangements, with
  manual overrides.
- **Scale artwork:** Fit preserves proportions, Fill crops edges, and Stretch
  fills the area while changing proportions.
- **Window:** resize with the corner grip. F11 or double-click toggles fit and
  restore; Escape leaves fit mode.
- **Sensor polling:** 500–5000 ms, default 1000 ms. Faster polling increases
  hardware access and may repeat values from slower sensors. Collection is
  serial and never overlaps.
- **Sensor readouts:** optional number pulses, off by default, and independent
  smooth temperature rings.
- **Holographic display:** automatic map motion and ship rotation, separate
  from sensor polling. Motion pauses when hidden or disabled and respects
  Windows animation preferences.

The collection includes Classic, Dark, ICE and additional colour palettes,
plus eight Expanse-inspired ship themes. Theme changes retain readings,
maxima, display selection and polling settings. Session maxima reset when
the app restarts.

## Ships and Sol navigation

The ship selector changes the theme and focuses a documented series
association. Drag the Sol view to orbit, use the wheel to zoom, select a
destination by clicking or using its list, and press **Home** or **RESET** for
the whole system. The map is explicitly schematic: distances, body sizes,
positions and motion are illustrative, not astronomical ephemerides or live
ship tracking. Medina is separate in Ring Space; Tycho is a mobile station.

Rocinante/Tachi, Donnager, Truman, Epstein's Yacht and the MCRN dropship have
bundled original-colour 3D models in the left artwork strip. Razorback, Tynan
and Morrigan use flat artwork. **Razorback does not currently include a 3D
model.** The requested
[red-and-white Razorback by Sebastián Espinoza](https://sketchfab.com/3d-models/razorback-the-expanse-fan-art-0eca0a298cba4396bceca35d85dc51e8)
is a visual reference; its listing does not enable model download. Inclusion
requires authorised model files and redistribution terms.

Bundled meshes retain original base-colour/emissive textures, UVs, normals and
artist credits. Native WPF lighting approximates their source materials.
Ship and map motion is capped at 20 fps independently of collection. Decorative
animation does not represent sensor measurements or fictional tactical data.

See [model credits](Assets/Models/ATTRIBUTION.txt),
[artwork sources](Assets/Ships/sources.json) and
[navigation references](SensorPanel/ExpanseNavigation.cs). Production interface
inspiration includes [Rhys Yorke's Razorback UI](https://rhys.artstation.com/projects/oAVPWm)
and [Rocinante UI](https://rhys.artstation.com/projects/q9Am1L).

## Reading sources and limitations

| Reading | Source and limits |
| --- | --- |
| Hardware names | Windows SMBIOS/WMI and driver identity; names are not embedded in artwork. |
| CPU temperature, power and clocks; board temperatures and fans | Embedded LibreHardwareMonitor with existing PawnIO, subject to hardware and access checks. Board probes and CPU package temperature are distinct readings. |
| Vcore | Measured AMD SVI3 rail on explicitly supported firmware tables. Requested VID is not presented as measured Vcore. |
| RAM clock | Live MCLK on supported CPU layouts; DDR transfer rate is twice MCLK. SMBIOS configured speed is not substituted for a live reading. |
| NVIDIA GPU temperature, power, usage and clocks | NVML from the installed NVIDIA driver, subject to each device's supported fields. |
| CPU usage, memory use, uptime and network traffic | Windows APIs/counters. |
| Display FPS | Portable PresentMon using Windows ETW for a selected game's displayed-frame intervals. |

Automatic FPS selection currently catalogues installed Steam games and demos,
excludes ordinary desktop apps and launchers, and follows a focused recognized
game. Other launchers are not automatically catalogued. FPS is not monitor
refresh rate and may differ from an in-game counter. If anti-cheat prevents a
new trace, close the game and panel, launch the panel first, then the game.

Failed, missing or stale measurements remain unavailable. Fan/header labels
follow upstream mapping and may need verification against a board manual.
Resource use varies with hardware, display size, theme and workload; there
is no universal performance or game-FPS guarantee.

## Build and validation

Clone the repository and install the .NET 10 SDK, Visual Studio x64 C++ build
tools, and the NVIDIA FrameView SDK. The current build compiles the native
FrameView bridge even though automatic FPS uses PresentMon. Toolchain discovery
uses `vswhere`; nonstandard installations can set `VCVARS64_PATH` and
`FRAMEVIEW_SDK_PATH`. Then restore/build from the repository root:

```powershell
dotnet build .\SensorPanel\SensorPanel.csproj -c Release
& .\SensorCollector\bin\Release\net10.0-windows\SensorCollector.exe --self-test
```

Use `tools/Validate-Holotable.ps1` for build/resource checks and saved-data
presentation previews. Use `tools/Build-Installer.ps1` to build and package the
shared-runtime installer; packaging also requires Inno Setup 6. The optional
NVIDIA FrameView runtime source is optional; its native bridge build currently
requires the SDK and C++ tools listed above. Normal automatic FPS uses PresentMon.

Keep diagnostic exports, screenshots, logs and machine-specific settings out
of commits and release uploads. Saved-data layout checks do not establish
compatibility with every physical display, hardware configuration or DPI
setting. Review release notes for the scope of each change.

## Credits

This independent FAN MADE project is not affiliated with or endorsed by
GIGABYTE, AORUS, The Expanse or the referenced artists. Names, trademarks,
fonts, artwork and models remain their respective owners' property. Preserve
the included third-party notices and asset attributions when redistributing.

Dependencies and references:
[LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor),
[PawnIO](https://pawnio.eu),
[NVIDIA NVML](https://docs.nvidia.com/deploy/nvml-api/),
[PresentMon](https://github.com/GameTechDev/PresentMon) and
[ZenStates-Core](https://github.com/irusanov/ZenStates-Core).

Palette inspiration: [Nord](https://www.nordtheme.com/),
[Dracula](https://draculatheme.com/contribute),
[Catppuccin](https://catppuccin.com/palette/) and
[SynthWave '84](https://github.com/robb0wen/synthwave-vscode).
