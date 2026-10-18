# v1.8.5 — Full ship viewport, Agatha King and Pella

All 21 desktop ships now have local 3D portraits and holotables sharing one
GPU viewport. Open a holotable by clicking the portrait or pressing H, then
drag to orbit, scroll or pinch to zoom, reset the view, or close it to return
to the portrait. Complete model geometry and embedded PBR maps are retained;
each model is checked against its catalogue hash.

Agatha King gains six recessed illuminated drive throats and local fitting
lights. Pella gains thin overlapping armour layers while retaining its smooth
dark coating and recessed flat backing. These are credited fan studies, not
official production meshes; earlier approximations remain labelled.

Holotables now resize after the shared viewport moves between hosts. Dragging
inside a holotable rotates the ship without moving the panel window. Animation
stops when hidden or disabled. Sensor bindings and SOL navigation are retained.

Validation includes a zero-warning Release build, exact desktop asset hashes,
native saved-data navigation/fleet checks for seven ships at 1280x720, and
manual Agatha King/Pella portrait, orbit, zoom, reset and close checks. This
does not establish a new live-game FPS or hardware-performance result.

Packaging checks cover the published collector/resources, all 44 fleet file
hashes and missing-runtime installer gates. A fresh install/uninstall round
trip is not claimed for this release.

Requires Windows x64, Microsoft .NET 10 Desktop Runtime (stable 10.0.x) and
Microsoft Edge WebView2 Evergreen Runtime. Setup checks both shared runtimes
and links to Microsoft's downloads when missing. Supported hardware readings
retain the existing administrator, PawnIO and NVIDIA-driver requirements.
Private reference images/video and media packs are excluded from the release.
