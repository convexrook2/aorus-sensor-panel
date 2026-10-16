AORUS SENSOR PANEL — FAN MADE
Version 1.8.4 | Windows x64

An independent sensor dashboard for dedicated Windows displays, with AORUS-
inspired themes, Expanse-inspired ship consoles and schematic Sol navigation.
Rainmeter and the HWiNFO application are not required.

REQUIREMENTS
- Microsoft .NET 10 Desktop Runtime, Windows x64, stable 10.0.x.
  Setup checks for it and offers the official download if missing. Install
  Desktop Runtime, then choose Check again in Setup. The SDK is not needed.
  Windows, .NET Framework, x86 runtimes and the core runtime alone do not
  guarantee this requirement is met.
  https://dotnet.microsoft.com/en-us/download/dotnet/10.0
- Administrator access and an existing PawnIO 2.2+ driver for supported CPU
  and motherboard readings. No driver is installed, changed or removed here.
  https://pawnio.eu
- An installed NVIDIA driver for NVIDIA GPU readings. Other GPU vendors
  are not currently supported by this collector.
- A display exposed as a Windows monitor. USB panels using a proprietary
  data protocol require a separate vendor integration.

Sensor support depends on the hardware library and explicit CPU/firmware
checks. Vcore and live RAM-clock decoding support a limited set of layouts.
This package does not promise support for every motherboard or processor.
Unsupported, failed or stale readings show an em dash rather than an estimate.

GETTING STARTED
1. Run AORUS-Sensor-Panel-FAN-MADE-Setup.exe.
2. Install the Desktop Runtime if requested, then choose Check again.
3. Open the panel and accept Windows' administrator prompt for sensor access.
4. From the FAN MADE tray icon, choose Open settings, then Move to display
   and Fit to display.
   This saves a dedicated display for later launches.

Automatic fullscreen avoids taking over the primary workspace. If no suitable
saved display is available, startup stays in the tray.
Fullscreen on a secondary display keeps the tray icon without a taskbar button.
Restoring a window or moving to the primary display restores that button.
Disconnecting the selected secondary display hides the panel and retains its
selection. Reconnecting restores a previously visible panel without taking
focus. Hide to tray cancels automatic restoration.

CONTROLS
Click the settings gear or right-click the panel. The compact menu includes
theme, display, layout, scaling, polling, game selection, README and tray
controls. Back returns within settings; Escape closes it.

Fit to display fills the selected monitor. F11 or double-click toggles fit and
restore. Resize a restored window with its corner grip; Escape leaves fit mode.
Layout offers automatic landscape, wide-strip and portrait arrangements, with
manual overrides. Fit scaling preserves proportions, Fill crops edges and
Stretch fills the area while changing proportions.

Sensor polling accepts 500-5000 ms, default 1000 ms. Faster polling increases
hardware access and may repeat values from slower sensors. Collection is serial
and never overlaps. Polling does not change ship/navigation animation rate.
Settings > Sensor readouts offers optional number pulses (OFF by default)
and independent smooth temperature rings. Settings > Holographic display
controls automatic map motion and ship rotation. Motion pauses when hidden
or disabled and respects Windows animation preferences.

THEMES AND SOL NAVIGATION
The collection includes Classic, Dark, ICE and additional colour palettes,
plus The Expanse main theme and eight individual ship themes. Theme changes retain readings,
maxima, display and polling settings. Session maxima reset on app restart.

THE EXPANSE MAIN THEME
Choose Settings > Theme > The Expanse for a space showcase cycling through
all eight existing ships. Settings > Expanse showcase controls Auto cycle
and the cycle interval: 45 seconds by default, with choices from 15-120 seconds.
Selecting a ship manually holds it until Auto cycle is enabled again.
These controls are separate from Holographic display (map motion/ship rotation).

Personnel cards identify verified ship relationships. Generic types without
identified crews show no portrait. Optional portraits and
scene backgrounds are loaded only from a local media pack. The app does not
download show images at runtime or include private media packs in its installer.
PNG/JPEG exports from ArtCraft can be used locally; ArtCraft is not required.
Media pack format and instructions:
https://github.com/convexrook2/aorus-sensor-panel/blob/main/Assets/Expanse/README.md

The ship selector changes the theme and focuses a documented series
association. The original SOL / NAVIGATION diagram is restored: drag to rotate,
wheel to zoom, choose a destination and press Home or RESET for the whole system.
Belter Charts supplies detailed Sun/planet textures, Earth clouds/night lights,
Saturn ring strip and irregular Eros/Phoebe meshes. Native WPF renders them.
SCHEMATIC retains enlarged bodies and readable spacing. TRUE SCALE uses physical
radii and NASA/JPL Horizons positions, with an AU/km bar and navigation markers
for tiny bodies. Moon motion follows hourly parent-relative NASA vectors;
planet samples are daily. The local data covers 2026-10-01 to 2027-10-01.
The footer shows simulated UTC and the clock speed (default: 6 hours/second).
It loops within that range; there is no live NASA connection. PAUSED stops the
orbital clock. Body spin is decorative. Earth day/night lighting, atmosphere
rims and a brighter two-layer Sun corona run locally, independently of sensors.
Tycho is a mobile Belt station. The fixed Sol gate is outside Uranus, about
2 AU beyond its orbit. Medina is in the separately labelled Ring Space inset,
distinct from the alien central station. Animation pauses when hidden or motion
is disabled. Body names appear only on hover; the destination list keeps the
selected name. True-scale zoom skips off-screen and sub-pixel meshes and clips
orbit trails before drawing. See SolarNavigation/CREDITS.txt.
Decorative animation is not sensor data.

Rocinante/Tachi, Donnager, Truman, Epstein's Yacht and the MCRN dropship have
bundled original-colour 3D models in the left artwork strip. Razorback now uses
the owner's blueprint-corrected v2 model in the rotating strip and holotable.
Tynan and Morrigan use flat artwork. The Sketchfab Razorback is a visual reference;
no mesh or texture was extracted from that page. The original v1 remains preserved.

Bundled meshes retain original colour textures and markings; native WPF
lighting approximates their source materials. Ship and idle map animation are
capped at 20 updates/second; camera movement uses up to 60 and detaches its
render callback afterward, separately from collection. Credits: Holotable-Model-Credits.txt and
Ship-Artwork-Sources.json.

START WITH WINDOWS
This option is initially OFF. Enable it in Setup or the panel menu to create
a current-user Task Scheduler logon task, with sensor-access permissions and
a short sign-in delay. No password is stored and no background service is
installed. It runs after sign-in. Disable it in the panel menu or rerun Setup
with the option unchecked.

READING SOURCES
Ship consoles use larger temperature/sensor readouts and a compact theme title.
CCD HOTSPOT replaces the voltage-source notice where supported. BOARD & COOLING
shows board temperatures, CPU FAN/CPU OPT/pump RPM and GPU fan duty where available.
The GPU fan percentage is not an RPM reading. Missing/stale values remain unavailable.
Hardware names: Windows SMBIOS/WMI and driver identity, not artwork labels.
CPU temperature/power/clocks and board temperatures/fans: embedded
LibreHardwareMonitor and existing PawnIO, subject to hardware/access checks.
CPU package temperature differs from a board temperature probe.
Vcore: measured AMD SVI3 rail on explicitly supported firmware layouts;
requested VID is not presented as measured Vcore.
RAM clock: live MCLK on supported CPU layouts. DDR transfer rate is twice MCLK.
SMBIOS configured speed is not substituted for a live reading.
NVIDIA GPU temperature/power/usage/clocks: NVML from the installed driver.
CPU usage, memory use, uptime and network traffic: Windows APIs/counters.
FPS: external Windows ETW/PresentMon monitors recognized running games.
The compatibility reader measures successful
application-present cadence, not confirmed displayed frames or monitor refresh
rate. Automatic follows the foreground recognized game; FPS source can pin
a game. Desktop composition and ordinary applications are excluded.
No injected hook or game-memory reader is used.

Automatic FPS selection uses the local Steam game catalog and retains the
running game while another application has focus. With no recognized game
running, FPS is unavailable and its source shows No game. Capture remains
external ETW, without game injection or an anti-cheat bypass. A pinned game
retains its own availability status and cannot fall back to desktop/app FPS.

TROUBLESHOOTING
Missing CPU/board readings: check administrator access and PawnIO. Hardware
or firmware layouts outside the supported set remain unavailable.
Missing NVIDIA fields: check the installed driver and device support.
Hidden panel: double-click the FAN MADE tray icon; Windows may put it under
the hidden-icons arrow. Use Exit to stop collection and the FPS helper.
Fan/header labels follow upstream mapping; consult the board manual.
Resource use depends on hardware, display size, theme and workload. There
is no universal resource-use or game-FPS guarantee.

UPDATE / UNINSTALL
Exit from the tray before updating or uninstalling. Remove the application
through Windows Settings > Apps. Its startup task and installed files are
removed; shared runtimes, drivers and user preferences are preserved.

CREDITS AND REFERENCES
This independent FAN MADE project is not affiliated with or endorsed by
GIGABYTE, AORUS, The Expanse or the referenced artists. Names, trademarks,
fonts, artwork and models remain their respective owners' property.
Preserve THIRD-PARTY-NOTICES.txt, Licenses and asset attributions.

Project and downloads: https://github.com/convexrook2/aorus-sensor-panel
LibreHardwareMonitor: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
PresentMon: https://github.com/GameTechDev/PresentMon
NVIDIA NVML: https://docs.nvidia.com/deploy/nvml-api/
CPU mapping reference: https://github.com/irusanov/ZenStates-Core
Razorback UI inspiration: https://rhys.artstation.com/projects/oAVPWm
Rocinante UI inspiration: https://rhys.artstation.com/projects/q9Am1L
Production screen-graphics interview:
https://www.pushing-pixels.org/2021/09/04/the-art-and-craft-of-screen-graphics-interview-with-rhys-yorke.html
Ship profile art: https://rjb-mallacore.artstation.com/projects/o2QJBq
Tynan concept art: https://frankyang.artstation.com/projects/3oAAeo
Palette inspiration: https://www.nordtheme.com/
https://draculatheme.com/contribute
https://catppuccin.com/palette/
https://github.com/robb0wen/synthwave-vscode
