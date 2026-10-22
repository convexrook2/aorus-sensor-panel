# Solar navigation

The original **SOL / NAVIGATION** diagram is the primary view again. Its native
WPF header, destination list, elliptical orbit layout, asteroid belt, pan/zoom,
selection markers, separate Medina inset and **RESET** control come from the
v1.7.1 implementation. There is no added SYSTEM / INNER / DETAIL toolbar.
Body names stay in the destination list instead of covering the map. Hovering
over a body reveals one contextual name; leaving it or dragging clears the name.
Click or tap a model, or use the destination list, to select it. Overlapping
models resolve to the visible front surface, preserving moon occultations.

Detailed assets come from the pinned Belter Charts source at
`ba86075477fc529294a5e51cb4e1b0d183ff893a`: real planet/Sun texture maps, Earth
clouds, the radial Saturn ring texture, Eros' 89,398-face shape and Phoebe's
packed mesh. `SolarBodyAssets.cs` loads them into cached, frozen native WPF
models. Eros geometry is retained without simplification; batches stay below
WPF's per-mesh limits. Vertex colours use face averages in a native palette.
The native day/night surface reproduces Belter's Earth material equation, using
its original city-light map, sun direction and smooth terminator. A small native
texture is updated at most twice per second, independently of sensor polling.
Sun-centred lighting, atmosphere rims and the two soft corona layers restore the
lighting detail. The web renderer is not loaded.
Selecting a body enlarges its model within the diagram so its detail is visible.
Both modes preserve orbital depth: near-side moons pass in front of their planet;
far-side moons are occluded. Schematic spacing expands the orbit's major axis
without pushing its entire projected ellipse outside the enlarged planet.

The panel does not load the web renderer or require WebView2 for navigation.
`NavigationWeb` and the previous adapter source are preserved for reference;
`BelterSystemView.cs` is excluded from compilation. The pre-restoration version
is also preserved in the timestamped `artifacts/native-solar-restore-safety-*`
backup. The scientific native 3D model is retained separately in source.

## Scale and landmarks

The default **SCHEMATIC / NOT TO SCALE** mode preserves the original layout,
enlarging bodies and compressing distances for readability. Planetary directions
and parent-relative moon motion follow the bundled NASA/JPL vectors. The scale
button switches to **TRUE SCALE**: the same diagram uses physical radii and one
uniform AU-to-screen conversion, with an orthographic tilted projection. Tiny
dots and selection rings are navigation markers, not enlarged physical bodies.
Focus frames a planet's moon system; the mouse wheel can zoom to its surface.
The AU/km bar measures horizontal projected distance.

`nasa-horizons.json.gz` contains geometric J2000 ecliptic/ICRF vectors from NASA/JPL
Horizons, 2026-10-01 through 2027-10-01 TDB. Planet/small-body samples are daily;
moon samples are hourly and relative to their own planet. Cubic Hermite
interpolation uses the supplied velocities. True-scale trails show sampled
trajectories over the covered year (outer planets show arcs, not fabricated
complete ellipses). Radii are in kilometres; one AU is 149597870.7 km.
`tools/Update-SolarEphemeris.ps1` reproducibly downloads the data with sequential
development-time requests; the panel itself makes no network requests.
https://ssd-api.jpl.nasa.gov/doc/horizons.html
https://ssd.jpl.nasa.gov/planets/phys_par.html
https://ssd.jpl.nasa.gov/sats/phys_par/

The footer displays the simulation UTC time. PAUSED, 1x, 6 H/S and 4 D/S control
the orbital clock; 6 H/S is the default. The clock loops within the bundled
range. This is a bounded simulation, not a live NASA feed. Body spin is
decorative. Disabling motion or hiding the view stops rendering. Missing or
invalid NASA data disables true scale and leaves the schematic available.

Tycho is shown as a mobile Belt station. The stationary Sol gate marker sits
outside Uranus and inside Neptune, corresponding to roughly 2 AU beyond Uranus;
its direction is illustrative. In true scale it is fixed at two AU beyond
Uranus' distance at the dataset's start epoch; it never follows the planet or
simulation clock. Medina retains the separate labelled Ring Space
inset and the original dashed portal link. It is the Nauvoo/Behemoth hull, distinct
from the alien central Ring Station. An inset location is not a heliocentric
coordinate; the dashed link does not represent a physical distance.

Tycho, Medina and gate meshes reuse the original detail geometry already authored
for this panel. Their source is `NavigationWeb/src/models.ts`; an offline exporter
retains their faces and colours. No restricted Sketchfab model is bundled.
Razorback v2 remains in the ship strip and holotable, independently of navigation.

## Build and evidence

Run `tools/Build-SolarAssets.ps1` to copy the original local texture/rock assets
and export the existing landmark models. `-Restore` restores the pinned offline
geometry tooling without package lifecycle scripts. Normal panel operation needs
no Node.js, local server or browser runtime.

Run `tools/Validate-SolarNavigation.ps1` for the build, existing collector/mapping
checks and visible native saved-data previews at 1920x480, 1280x720 and 480x800.
Checks cover all destination focus states, actual asset loading and render
budgets, labels, schematic/geographic boundaries, paused/hidden animation and
unchanged sensor readings/maxima. Screenshots and results are saved in
`artifacts/solar-navigation-validation`. These previews are separate from
installed-build or live-hardware proof. Efficient idle orbital animation requests one
update every 100 ms (10 per second); dispatcher load can reduce the achieved
update rate. Camera transitions use up to 60 updates per second and return to
the idle rate. Direct dragging refreshes on pointer movement, independently of
that idle timer. These scene update rates are not measurements of physically
presented display frames.
Full Fidelity follows native composition frames for both orbital animation and
camera motion, without the Efficient mode's application frame caps. Its callback
detaches when paused and settled, hidden, unloaded or disabled. Efficient attaches
that callback only during a camera transition and detaches when it settles.
True-scale zoom interpolates the scale ratio instead of taking huge linear steps.

PAUSED stops the settled scene's timer and refreshes. Focus transitions, dragging
and zooming remain available while paused. Stars and stable schematic planet
orbits use separate retained drawing layers; their bitmap caches account for
ancestor Viewbox scaling and display DPI. Camera, size, selection and scale-mode
changes invalidate the appropriate layer. Dynamic belts, moon orbits, models and
lighting retain their original rendering. Earth lighting skips identical sun
directions and settles to the final direction after a paused camera change.

The explicit `--check-solar-efficiency` windowed saved-data review records scene
update counters, checks paused/focus behaviour, and captures seven deterministic
map views under `artifacts/solar-efficiency/after`. The `--solar-baseline` option
labels a baseline receipt; it does not disable optimizations. This diagnostic
does not measure physical display FPS, live sensors or GPU utilization.

Models outside the projected view are detached from the 3D scene, retaining
their cached assets for re-entry. Sub-pixel true-scale bodies use the existing
selection dots instead of full meshes. A conservative bound includes Saturn's
rings and irregular station/rock geometry. Orbital segments and portal links
are clipped to the view before creating drawing geometry; corona/rim fills
also use bounded rectangles. Close views adjust camera depth to the visible
meshes. Earth lighting is updated only while its mesh is attached. The outer
map clips its children, avoiding a duplicate Viewport3D clip. Native checks
cover crossing/outside trails, scene membership, close-view depth and bounded
true-scale geometry as well as the original moon-depth and monitoring checks.

`tools/Restore-SolarCheckpoint.ps1 -Checkpoint <receipt> -Scope Verify` checks the
exact pre-change source, installed files and installer. `-Scope Installed`
restores only this update's recorded files and relaunches the existing startup
task; `-Scope Source` restores only this task's source changes. Later edits are
protected by before/after hashes. Media, preferences and the Git index are not
overwritten. Program Files restoration needs the Windows administrator prompt.

See `SolarNavigation/CREDITS.txt` for full sources and adaptation details.
The previous web build remains reproducible with `tools/Build-BelterNavigation.ps1`;
run `Build-SolarAssets.ps1` afterward to restore the active native asset exports.
