# AORUS Sensor Panel — FAN MADE

A Windows sensor dashboard for a dedicated display, with AORUS-inspired themes,
Expanse-inspired ship consoles and an interactive Sol navigation view. It runs
without Rainmeter or the HWiNFO application.

[Download the installer](https://github.com/convexrook2/aorus-sensor-panel/releases/latest)
from GitHub Releases. Releases include setup instructions and SHA-256 checksums.
Hardware support depends on the available sensor interfaces and drivers.

The **1.8.13 correction** adds the accepted Knight review 04 that was omitted
from 1.8.12: the exact silver-hull model, matching portrait, seventeen work
lights and three fitted white/violet plumes. See the
[Knight release record](docs/RELEASE-1.8.13-KNIGHT.md). Its accepted Blender/GLB
files are preserved unchanged in `Assets/Models/Source/Knight-review04`.

The released 1.8.12 Canterbury finish14, Arboghast pass08, exhaust, holotable
and Sol-navigation changes remain intact. Further geometry work on these
accepted ships is stopped and ice is deferred.

## Open Fleet Studio

Fleet Studio is a development and reference-review tool. Sensor-panel releases
update the ships inside the sensor panel; they do not launch, install or deploy
Fleet Studio.

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
sensor panel automatically rotates the full ship in its left showcase. Use
**PAUSE ROTATION** directly beneath the ship to hold its current angle, and
**RESUME ROTATION** to restart; this preference is saved separately from map
and background motion. During each 30-second turn, the showcase briefly tilts
through a lower viewing angle and smoothly returns to its normal view. Pausing
holds both the turn and tilt. Click the ship or press **H** for the full-detail holotable: drag
to turn the ship, wheel to zoom the ship, **RESET VIEW** to reset, and **Esc** to return. The
holographic platform retains its position, size and viewing angle during inspection. Rotation
in the holotable starts off and can be enabled with **AUTO ROTATE**. The
holotable has a projected 3D light platform with floating rings, rotating arcs
and a rising scan. It animates independently of the ship; **HOLOGRAM MOTION**
holds or resumes the light pattern. Hidden or inactive views stop animating. The same
viewport moves between the showcase and holotable; closing returns it to the
showcase and restores its pause preference. Leaving ship themes releases the
browser/model. Full geometry/PBR maps, native
pixel ratio, shadows, ambient occlusion and accepted lights remain intact in 3D.
Solar navigation, theme cycling and readout choices retain their existing layout.
The old `--full-graphics` argument is unnecessary.

Efficiency comes from avoiding duplicate model requests, redrawing only when a
scene changes, reusing one ship viewport, and pausing visual activity when
minimized or hidden. The visible holographic platform uses the selected
Full Fidelity or Efficient cadence. Pausing **HOLOGRAM MOTION** with ship
rotation off restores drawing only on interaction or a scene change. AORUS animation frames are
decoded only when that theme is visible and animated; ship themes release them.
Live collection continues independently of visual activity.

Fleet Studio uses JavaScript/Three.js inside a WPF/WebView2 window and reads its
bundled files through a local virtual host in development or in-memory encrypted
archive responses after publishing. Full shading is the default, with
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

Published builds protect creative assets in `assets.rl`; editable source files
remain in this checkout. See the [.rl asset standard](docs/RL-ASSET-STANDARD.md)
for coverage, build keys and the limits of client-side copying protection.

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

Settings opens with an **Experience** selector:

- **AORUS · Lite Mode** uses the original monitoring panel and colour themes.
  Expanse ship graphics are released and solar/background animation is stopped.
- **The Expanse Experience** exposes ship themes and **Rendering** directly in
  the same card. **Full Fidelity** is the default for a new rendering preference:
  ship drawing follows browser animation frames and solar animation follows
  native composition frames without artificial application frame caps.
  **Efficient** keeps the same models, textures, lights, shadows, AO and native
  capture resolution, limiting automatic ship drawing to 30 fps and idle solar
  updates to 10 per second. Direct interaction remains responsive.

The rendering choice is also in **⋯ Ship options**. Switching applies immediately
without reloading or reducing the current model, and is saved independently of
pause/motion choices. Each experience remembers its theme; sensor values,
peaks, polling interval and display selection are retained. Hidden, minimized
and covered visual work still stops in either mode. The current installer
still contains both experiences' assets; choosing Lite is a runtime selection,
not a separate content-free installation. Separate installers are release work.

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
- **Ship options → Map and background motion:** automatic solar-map motion and scene fades, separate
  from sensor polling. Motion pauses when hidden or disabled and respects
  Windows animation preferences.

The collection includes Classic, Dark, ICE and additional colour palettes,
plus **The Expanse** main theme and 22 individual ship/gate themes. Theme changes retain readings,
maxima, display selection and polling settings. Session maxima reset when
the app restarts.

### The Expanse main theme

Ship consoles emphasize larger sensor readouts and a smaller theme heading.
The CPU card shows measured CCD hotspot temperature where supported. The
**BOARD & COOLING** strip separates board temperatures, CPU FAN/CPU OPT/pump
RPM and NVIDIA fan duty. Fan duty is a percentage, not RPM. Actor captions,
unidentified-crew placeholders and the inactive "ORIGINAL COLOURS" label are removed.

Choose **Settings → Theme → The Expanse** to cycle through all 22 ship/gate themes,
including each ship's console colours and graphics. Selecting The Expanse
explicitly resumes cycling after an earlier manual hold. **Cycle ship themes**
and **Change ship every** appear directly below the theme selector, and also in
**⋯ Ship options**. The default interval is 45 seconds, with choices from 15–120
seconds; changing themes preserves the selected interval. Selecting a ship
manually pauses cycling until **Cycle ship themes** is enabled again. Readings,
session maxima, histories and the shared solar view are retained. These controls
are separate from map motion and the holotable's **AUTO ROTATE** control.
Cycling pauses while minimized, in settings or in the holotable.

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
selection markers. Body positions and orbit curves share the same interpolation;
curves refine with zoom so the selected body stays on its path. Focus keeps a
selected planet readable; wheel out to inspect its wider moon system.
Parent-relative moon trajectories are sampled hourly;
planet/small-body trajectories are sampled daily. Data is bundled for
2026-10-01 to 2027-10-01, with a visible simulated UTC clock and selectable
speed (default: six hours per second). The clock loops within that range;
the panel makes no NASA network requests. Earth night lights and atmosphere rims
retain Belter's lighting detail. The Sun has a brighter white-gold emitting
surface, stronger point lighting and a soft three-layer corona. A credited
Milky Way panorama fades into the background as you zoom out, then fades away
for close inspection. It is a cached sky image with no extra animation loop.
Body names appear only on hover; the destination list keeps the selected name.
True-scale zoom skips off-screen and sub-pixel meshes and clips orbit trails
before drawing. Efficient camera movement follows rendering at up to 60 updates/second;
Efficient idle orbital animation requests 10 updates/second; Full Fidelity follows
native composition frames. Both pause when hidden or disabled.
Tycho's mobile Belt position is approximate; the fixed Sol gate is shown
outside Uranus. Medina retains its separate Ring Space inset, distinct from
the alien central station. See [navigation scope and credits](SensorPanel/SolarSystem.md).

All 22 ship/gate themes use one full-quality shared ship viewport. Automatic
rotation retains elapsed-time speed and normal native capture cadence, with a
30 fps drawing limit only in Efficient mode. **PAUSE ROTATION / RESUME ROTATION** sits
directly beneath the ship and preserves its exact angle and saved preference.
Prepared 1024×2048 three-quarter portraits remain loading fallbacks; their
receipts bind each PNG to the full model's SHA-256 value.
Click the ship or press **H** for an interactive holotable above its display
plinth, with the existing space or local media backdrop. Drag to orbit, wheel
to zoom, or use **RESET VIEW**. **AUTO ROTATE** is optional, starts off, and
respects Windows animation preferences. Idle views redraw on demand; deliberate
rotation shares the selected rendering mode. **BACK TO MONITOR** or **Escape**
returns the same viewport to the showcase with its rotation preference retained.
Leaving ship themes releases the browser/model. No additional mesh reduction
or texture resampling occurs. Efficient retains the accepted trial policy;
its measurements do not establish the <=2% target or a GPU budget for Full Fidelity.

Developer task **Prepare ship portraits and review rotation controls** rebuilds, visibly
exports the images through the full renderer, checks all image/model hashes,
refreshes the bundle manifest and runs the saved-data native lifecycle review.
Use `pwsh -File tools/Prepare-StaticFleet.ps1 -Review`; `-SkipExport` reuses
already prepared images. It neither installs nor opens Fleet Studio. Portraits
remain inside `FleetViewer` and are protected by the existing RLAR archive in
published builds. [Current performance evidence](docs/PERFORMANCE-2026-10-21.md)
keeps the <=2% normal-monitor target separate from measured results.

**Sol Ring** is also available in the same selector and Expanse cycle. Its
existing Fleet Studio v3 rim model is copied losslessly into the monitor's own
bundle, with its upright gate orientation and the same separate animated
aperture shader. The left showcase, instruments, solar navigation and backgrounds
retain their established positions; its map association is the existing Sol gate
landmark. The map uses a small marker at system scale and the accepted v3 rim
geometry with native lighting and an animated aperture when focused. Its
1,000 km diameter and fixed direction are illustrative. Motion-off and minimize
stop both rotation and the aperture. The full
model and shader are included in the protected published asset archive. This
source addition does not update an installed copy; see [Sol Ring integration](docs/SOL-RING.md).

The owner-finished **Rocinante Season 6** includes smooth armour, paired relief
markings, red lettering, a bluish-grey optical finish and the corrected blue-white
drive exhaust. Its exact full model and editable source are preserved; see
[finished Roci baseline](docs/ROCINANTE-S6-FINISHED.md).

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

Use the [release flow](docs/RELEASE-FLOW.md) to scope the next sensor-panel
release, reconcile held changes, validate an exact revision and separately
authorize publication and installation. Arboghast remains rejected/stopped;
the newer local experience changes are not covered by the saved 1.8.10 release.
The [proposed 1.8.11 candidate](docs/RELEASE-CANDIDATE-1.8.11.md) lists included
changes, explicit exclusions and unresolved release gates. It is preparation,
not an approved or built package.

[Jenkins CI](docs/CI.md) defines repeatable source/build/asset checks for the
Windows agent. Run `& .\tools\Run-JenkinsChecks.ps1` visibly in VS Code for the same local
gates. Release installer compilation is a separate explicit option; successful
CI does not replace installed, live hardware or native visual acceptance.

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
