# AORUS Sensor Panel — FAN MADE

A Windows sensor dashboard for a dedicated display, with AORUS-inspired themes,
Expanse-inspired ship consoles and an interactive Sol navigation view. It runs
without Rainmeter or the HWiNFO application.

[Download the installer](https://github.com/convexrook2/aorus-sensor-panel/releases/latest)
from GitHub Releases. Releases include setup instructions and SHA-256 checksums.
Hardware support depends on the available sensor interfaces and drivers.

## Open Fleet Studio

Use **Fleet Studio** on the Windows Desktop or in the Windows Start menu.
It opens the separate ship viewer directly, without an editor or command.
Close the window to exit; it has no background server or Windows startup entry.
The latest local model bundle is included. To refresh this desktop copy after
source changes, run `& .\FleetStudio\Install-Desktop.ps1` once from this checkout.
See [Fleet Studio setup](FleetStudio/README.md) for development alternatives.

## Run in VS Code

Open **X890.code-workspace**. The solution contains the monitor, its C# collector,
and a separate Fleet Studio desktop app. Install the .NET 10 SDK and the Microsoft
C# extension for development; the shipped monitor only needs the Desktop Runtime.

- **Run and Debug → X890 · LIVE monitor → F5** builds and launches the actual
  monitor from this checkout. Windows may ask you to approve hardware access;
  the elevated app continues independently of the initial debugger process.
  Its title identifies LIVE monitoring and a development build. A local runtime
  receipt records fresh samples and the actual full-model renderer.
- **X890 · UI preview (no sensors)** is an optional saved-data layout tool. It
  builds and opens a normal
  desktop window with a labelled empty snapshot. Dashes are expected; this does
  not read hardware or require administrator access.
- **Terminal → Run Task → Run X890 monitor (live)** builds and opens the live monitor.
  Accept Windows' administrator prompt yourself for hardware access. Its title
  says Live monitor. Exit the existing tray instance first if one is already running.
- **Run and Debug → Fleet Studio · desktop → F5** builds and opens the separate
  ship viewer. Close its window to stop it. Minimizing releases its WebView2 and
  loaded model; restoring reloads the selected ship and quality, with camera and
  effect controls reset. No Python process, HTTP server or sign-in task is needed.

For double-click launch from the checkout, use **Start-Monitor.cmd** or
**FleetStudio/Start-Desktop.cmd**. Their consoles show build failures. These use
the SDK and do not install or register anything.

The normal monitor uses WPF and the existing SensorCollector backend
(LibreHardwareMonitor/PawnIO, NVIDIA NVML and optional game FPS via PresentMon).
It collects serially at the saved polling interval, default 1000 ms. The current
ship viewport, solar navigation, holotable, full geometry/PBR maps, native pixel
ratio, shadows and ambient occlusion remain the normal design. Saved motion,
showcase and readout choices remain available; no still-art or graph substitute
is used to save resources. The old `--full-graphics` argument is unnecessary.

Efficiency comes from avoiding duplicate model requests, redrawing only when a
scene changes, reusing shadow maps when the hull/light is unchanged, and pausing
visual activity when minimized or hidden. Inactive WebViews request lower memory
usage without changing their geometry or shading. AORUS animation frames are
decoded only when that theme is visible and animated; ship themes release them.
Live collection continues independently of visual activity.

Fleet Studio uses JavaScript/Three.js inside a WPF/WebView2 window and reads its
bundled files through a local virtual host. Full shading is the default, with
an optional Efficient quality setting chosen inside the viewer. Rotation/effects
start paused, and the scene redraws when something changes. Deliberate animation
is capped at 30 fps. An open 3D viewer still uses RAM/GPU resources;
close it when finished. Its model geometry is unchanged. Hardware acceleration is
retained rather than moving rendering onto the CPU.

The legacy `http://127.0.0.1:8770/` browser preview remains optional and works only
while its separately launched server is running. Pinning a chat or URL cannot start
that server. The Desktop/Start-menu icons open Fleet Studio directly; the VS Code
profiles are development alternatives.

## Requirements

- Windows x64 and a display exposed as a Windows monitor. USB panels using a
  proprietary data protocol require a separate vendor integration.
- [Microsoft .NET 10 Desktop Runtime, Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0),
  stable 10.0.x. Setup checks for it and offers the official download if missing.
  Windows does not guarantee this runtime is installed. The SDK is not required;
  .NET Framework, an x86 runtime or the core runtime alone do not satisfy it.
- Administrator access and an existing [PawnIO 2.2+ driver](https://pawnio.eu)
  for supported CPU and motherboard readings. Setup does not install a driver.
- [Microsoft Edge WebView2 Evergreen Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section)
  for the full ship viewport. Setup checks for it and offers the official download.
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
4. From the FAN MADE tray icon, choose **Open settings**, select the **Screen**, then click **Fullscreen**
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

Pella retains the release monitoring instruments: CPU/GPU temperature dials,
clock rates, power, peaks, memory, network and cooling. The full model fills the
left showcase, and the existing solar system remains visible in the centre.
A cached static grid overlays the existing space/media background. The Pella
palette and typography apply to the instruments; scope walls and targeting
artwork are removed. See [Pella console](docs/PELLA-CONSOLE.md).

The [ship reference library](docs/references/README.md) keeps credits and source
links by ship, including the owner-selected Rocinante diagnostic blueprint for
a future look-and-feel revisit. Reference artwork is separate from runtime assets.

Click the settings gear or right-click the panel to open one settings form.
The current screen, layout, theme, update interval and FPS source are visible
without opening category pages. Choose values directly; **Fullscreen**,
**Windowed**, artwork sizing and the checkboxes each take one action after
opening settings. Startup, image capture, help and tray/exit controls are on the
same form. Changes apply immediately; **Done**, **×** or **Escape** closes it.
The form uses two columns where space allows and a single column on narrow
screens. Ship presentation stays in the separate **⋯ Ship options** form beside
the ship selector; cycling, timing and motion controls are direct there too.
The tray menu remains available while the panel is hidden.

- **Layout:** automatic landscape, wide-strip and portrait arrangements, with
  manual overrides.
- **Scale artwork:** Fit preserves proportions, Fill crops edges, and Stretch
  fills the area while changing proportions.
- **Window:** resize with the corner grip. F11 or double-click toggles fit and
  restore; Escape leaves fit mode.
- **Update every:** 500–5000 ms, default 1000 ms. Faster polling increases
  hardware access and may repeat values from slower sensors. Collection is
  serial and never overlaps.
- **Sensor readouts:** optional number pulses, off by default, and independent
  smooth temperature rings.
- **Ship options → Animate map and ship:** automatic map motion and ship rotation, separate
  from sensor polling. Motion pauses when hidden or disabled and respects
  Windows animation preferences.

The collection includes Classic, Dark, ICE and additional colour palettes,
plus **The Expanse** main theme and 21 individual ship themes. Theme changes retain readings,
maxima, display selection and polling settings. Session maxima reset when
the app restarts.

### The Expanse main theme

Ship consoles emphasize larger sensor readouts and a smaller theme heading.
The CPU card shows measured CCD hotspot temperature where supported. The
**BOARD & COOLING** strip separates board temperatures, CPU FAN/CPU OPT/pump
RPM and NVIDIA fan duty. Fan duty is a percentage, not RPM. Actor captions,
unidentified-crew placeholders and the inactive "ORIGINAL COLOURS" label are removed.

Choose **Settings → Theme → The Expanse** to cycle through all 21 ship themes,
including each ship's console colours and graphics. Selecting The Expanse
explicitly resumes cycling after an earlier manual hold. **Cycle ship themes**
and **Change ship every** appear directly below the theme selector, and also in
**⋯ Ship options**. The default interval is 45 seconds, with choices from 15–120
seconds; changing themes preserves the selected interval. Selecting a ship
manually pauses cycling until **Cycle ship themes** is enabled again. Readings,
session maxima, histories and the shared solar view are retained. These controls
are separate from **Animate map and ship**, which controls map motion and ship
rotation. Cycling pauses while minimized, in settings or in the holotable.

Personnel cards identify a verified character and their relationship to the
selected ship. Generic types without identified crews show no portrait. Optional local
portraits and scene backgrounds can replace the built-in space view. See the
[local media pack guide](Assets/Expanse/README.md). The app does not download
show images at runtime, and private media packs are excluded from the repository
and installer. PNG/JPEG exports from ArtCraft can be used as local media;
ArtCraft is not an application dependency.

## Ships and Sol navigation

The ship selector changes the theme and focuses a documented series
association. The original **SOL / NAVIGATION** diagram is restored: drag to
rotate, wheel to zoom, choose a destination by clicking or using its list,
and press **Home** or **RESET** for the whole system. Belter Charts supplies
the detailed Sun/planet texture maps, Earth clouds/night lights, Saturn ring strip,
and irregular Eros/Phoebe meshes. Native WPF renders these assets locally.
**SCHEMATIC** retains enlarged bodies and readable orbit spacing. **TRUE SCALE**
uses physical radii and NASA/JPL Horizons vectors, an AU/km bar and small
selection markers. Parent-relative moon trajectories are sampled hourly;
planet/small-body trajectories are sampled daily. Data is bundled for
2026-10-01 to 2027-10-01, with a visible simulated UTC clock and selectable
speed (default: six hours per second). The clock loops within that range;
the panel makes no NASA network requests. Earth night lights, atmosphere rims
and two soft Sun corona layers restore Belter's lighting detail locally.
Body names appear only on hover; the destination list keeps the selected name.
True-scale zoom skips off-screen and sub-pixel meshes and clips orbit trails
before drawing. Camera movement follows rendering at up to 60 updates/second;
idle orbital animation remains capped at 20 and pauses when hidden or disabled.
Tycho's mobile Belt position is approximate; the fixed Sol gate is shown
outside Uranus. Medina retains its separate Ring Space inset, distinct from
the alien central station. See [navigation scope and credits](SensorPanel/SolarSystem.md).

All 21 ships have local 3D portraits and holotables using one reusable WebView2
GPU viewport. Its captured image follows the native display scale, so enlarging
the panel renders more pixels instead of stretching a small ship bitmap. Resizing
does not change model detail, shading or rotation timing.
Click the portrait or press **H** to open the holotable: a centred,
passive rotating showcase above a small display plinth, with the existing space
or local media backdrop. Rotation follows **Animate map and ship** and Windows
animation preferences, drawing at most 30 fps and stopping when minimized.
There are no model navigation controls. **BACK TO MONITOR** or **Escape** returns
to monitoring. Both views retain the identical full
model and embedded PBR maps, checked against its SHA-256 value. No additional
mesh reduction or texture resampling occurs in the full viewport.

Agatha King has six illuminated drive throats. Pella has thin layered armour
over its smooth dark finish and recessed backing. These remain credited fan
studies; earlier approximations are labelled in the catalogue. Source and
asset credits accompany `Assets/FleetViewer/`. Artwork loads locally and uses
no runtime reference-image downloads. Ship and navigation motion stop when
hidden or disabled and run independently of collection. Decorative animation
does not represent sensor measurements or fictional tactical data.

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
| FPS | External Windows ETW/PresentMon for recognized running games. The FPS source menu lists games only. No desktop fallback, injected hooks or game-memory access. |

Automatic FPS selection uses the local Steam game catalog, follows the foreground
recognized game, and retains that running game while another application has
focus. Tools, ordinary applications and desktop composition are excluded. With
no recognized game running, FPS is unavailable and the source shows No game.
The compatibility reader measures successful game-present cadence, not confirmed
displayed frames or monitor refresh rate; it may differ from an in-game counter.
Capture uses external Windows ETW; no game injection or anti-cheat bypass is used.

Failed, missing or stale measurements remain unavailable. Fan/header labels
follow upstream mapping and may need verification against a board manual.
Resource use varies with hardware, display size, theme and workload; there
is no universal performance or game-FPS guarantee.

## Build and validation

Clone the repository and install the .NET 10 SDK. Restore/build the actual
application projects from the repository root (archived builds are not projects
in this solution):

```powershell
dotnet build .\X890.slnx -c Release
& .\SensorCollector\bin\Release\net10.0-windows\SensorCollector.exe --self-test-managed
```

Use `tools/Validate-Holotable.ps1` for build/resource checks and saved-data
presentation previews. `tools/Build-SolarAssets.ps1 -Restore` prepares the
local Belter texture/mesh assets with the offline Node.js tooling;
`tools/Validate-SolarNavigation.ps1` checks the restored original diagram
in visible native previews at three panel sizes.
Use `tools/Build-Installer.ps1` to build and package the
shared-runtime installer into `artifacts/installer`. Keep testing installers and
checksums in that project folder; do not copy them to the Desktop. Packaging
also requires Python 3 for asset checks
and Inno Setup 6. The FrameView bridge remains explicitly included by the installer
build, which still requires the FrameView SDK and C++ tools. For development,
NVIDIA FrameView is optional. Build its native bridge explicitly with
`-p:BuildFrameViewBridge=true` if needed; that additionally requires Visual Studio
x64 C++ build tools and the NVIDIA FrameView SDK. Toolchain discovery uses
`vswhere`; nonstandard installations can set `VCVARS64_PATH` and
`FRAMEVIEW_SDK_PATH`. Normal automatic FPS uses PresentMon and does not require
compiling that bridge.
`--self-test-managed` covers managed contracts and explicitly skips the native ABI;
the existing `--self-test` still requires and verifies the built bridge.

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
