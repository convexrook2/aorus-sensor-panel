AORUS SENSOR PANEL — FAN MADE
Version 1.7.1 | Windows x64

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
plus eight Expanse-inspired ship themes. Theme changes retain readings,
maxima, display and polling settings. Session maxima reset on app restart.

The ship selector changes the theme and focuses a documented series
association. Drag the Sol view to orbit, wheel to zoom, choose a destination
by clicking or using its list, and press Home or RESET for the whole system.
Positions, distances, body sizes and motion are schematic, not ephemerides
or live spacecraft tracking. Medina is separate in Ring Space; Tycho is a
mobile station. Decorative animation is not sensor data.

Rocinante/Tachi, Donnager, Truman, Epstein's Yacht and the MCRN dropship have
bundled original-colour 3D models in the left artwork strip. Razorback, Tynan
and Morrigan use flat artwork. Razorback currently has NO bundled 3D model.
The requested red-and-white model by Sebastian Espinoza is a visual reference:
https://sketchfab.com/3d-models/razorback-the-expanse-fan-art-0eca0a298cba4396bceca35d85dc51e8
Its listing does not enable download. Including it requires authorised model
files and redistribution terms. No gold or low-poly substitute is included.

Bundled meshes retain original colour textures and markings; native WPF
lighting approximates their source materials. Ship/map motion is capped at
20 fps separately from collection. Credits: Holotable-Model-Credits.txt and
Ship-Artwork-Sources.json.

START WITH WINDOWS
This option is initially OFF. Enable it in Setup or the panel menu to create
a current-user Task Scheduler logon task, with sensor-access permissions and
a short sign-in delay. No password is stored and no background service is
installed. It runs after sign-in. Disable it in the panel menu or rerun Setup
with the option unchecked.

READING SOURCES
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
Display FPS: portable PresentMon using Windows ETW for a selected game's
displayed-frame intervals. This is not monitor refresh rate and may differ
from an in-game counter.

Automatic FPS selection currently catalogues installed Steam games/demos and
excludes ordinary desktop apps and launchers. Other launchers are not yet
automatically catalogued. If anti-cheat blocks a new trace, close the game
and panel, start the panel first, then start the game.

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
