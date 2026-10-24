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
Sun-centred lighting and atmosphere rims retain the lighting detail. The Sun
uses a warm-white exposure adjustment of its original textured emissive surface,
stronger native point lighting and three soft white-gold corona layers. The
small star marker retains a visible bloom even when the true solar disk is
sub-pixel; the physical radius and orbit scale stay unchanged. These are display
exposure choices, not a physical irradiance simulation. The web renderer is not loaded.
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
Focus frames the selected planet and nearby moons without letting a distant
satellite reduce the planet to a dot. The wheel zooms out to the wider moon
system or in to its surface. This changes the camera, not physical dimensions.
The AU/km bar measures horizontal projected distance.

At wide zoom, the already bundled `skybox_milky_way.jpg` panorama fades in behind
the orbits. It fades out by 2.4x zoom for close inspection. `SolarSky` decodes one
shared 2048-pixel-wide image and applies a bounded exposure curve once, leaving
the source file unchanged. Zoom changes only the retained image's opacity;
there are no sky particles, animated post-processing passes or additional timers.
This is background sky context, not galaxy-scale navigation or an astrometric
camera. Solar System Scope's CC BY 4.0 credit is retained with the assets.

`nasa-horizons.json.gz` contains geometric J2000 ecliptic/ICRF vectors from NASA/JPL
Horizons, 2026-10-01 through 2027-10-01 TDB. Planet/small-body samples are daily;
moon samples are hourly and relative to their own planet. Cubic Hermite
interpolation uses the supplied velocities. True-scale curves use the exact
same Hermite polynomials as body positions, split at the current simulation
epoch. Their projected cubic control hulls refine to at most 0.3 pixel flatness
before clipping. This replaces the coarse 160-point annual polyline, which
could miss a close-view planet by hundreds of pixels. Moon guide curves stay
relative to the current position of their parent; they are not a heliocentric
history trail.

`nasa-orbits.json.gz` adds 721 geometric position/velocity samples each for
Mars, Jupiter, Saturn, Uranus and Neptune over approximately one revolution.
These context guides use the same Sun-centred frame and units. The existing
one-year runtime samples replace guide samples within the runtime range, so
the displayed body and curve agree exactly there. Ends are not joined into
invented closed ellipses. `tools/Update-SolarOrbitGuides.py --receipt
artifacts/<new-receipt>` fetches these sequentially from Horizons, preserving
raw responses and full query URLs; it refuses to replace an existing guide.
The original runtime dataset and independent position checkpoints are unchanged.
Radii are in kilometres; one AU is 149597870.7 km.
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
inset and the dashed portal link when both endpoints are in a system view.
The inset stays within compact system views; the symbolic link is omitted in
physical close views. It is the Nauvoo/Behemoth hull, distinct
from the alien central Ring Station. An inset location is not a heliocentric
coordinate; the dashed link does not represent a physical distance.

Tycho and Medina reuse the original panel detail geometry from
`NavigationWeb/src/models.ts`; an offline exporter retains their faces and colours.
The earlier exported gate mesh remains preserved for reference. The active map now
loads the accepted Sol Ring v3 source from `FleetViewer/models/SolRing.glb.gz`
only when its projected radius reaches eight pixels. It retains all 282,272
triangles, with bounded native batches, original layered rim geometry and a
separate two-sided opaque aperture. WPF materials, two inspection lights and a
native texture approximation of `portal.js` are a distinct map adaptation of the
full showcase's PBR/GLSL rendering. The aperture uses the existing map clock,
updates at most ten times per second and stops with paused/hidden rendering;
there is no additional timer. Full Fidelity's scene cadence stays uncapped.

At system scale, the gate is a small navigation glyph and its detailed mesh
is detached. Focusing it reveals the ring; the plane stays upright with its
normal radial to the Sun and follows the map's actual yaw/tilt during inspection.
The illustrative 1,000 km diameter uses the uniform AU conversion in true scale.
Neither that fictional dimension nor the landmark's direction is NASA data.
No restricted Sketchfab model is bundled.
Razorback v2 remains in the ship strip and holotable, independently of navigation.

## Build and evidence

`SensorPanel.exe --preview tools/preview-empty.json --windowed --preview-theme
Dark --check-solar-radiance --leave-review-open` checks the rendered bright Sun,
Milky Way fade, actual native planet illumination with the solar lamp on/off,
and paused/hidden stopping. It saves native captures and a result receipt under
`artifacts/solar-radiance-20261023/native`. It does not read live hardware, install
an update or measure whole-process GPU usage. The native solar point light is
separate from its [emissive material](https://learn.microsoft.com/dotnet/desktop/wpf/graphics-multimedia/3-d-graphics-overview#applying-materials-to-the-model),
which makes the Sun itself appear bright.

`SensorPanel.exe --preview tools/preview-empty.json --windowed --preview-theme
Dark --check-true-scale` opens a visible saved-data native review and exits after
checking close-view path alignment, 48 body/epoch combinations, both ring faces,
compact map rendering and aperture animation/pause/hidden lifecycle. Add
`--leave-review-open` to keep the successful ring review open. Its results and
captures are in `artifacts/solar-true-scale-20261023/native`. This does not read
hardware, replace settings, install an app or measure whole-process resources.

NASA reference: [Eyes' orbit views and close inspection](https://www.nasa.gov/centers-and-facilities/jpl/explore-the-solar-system-with-nasas-new-and-improved-3d-eyes/)
and [JPL's solar-system scale explanation](https://www.jpl.nasa.gov/edu/resources/video/solar-system-size-and-distance/).
These inform the presentation approach; the WPF curve implementation is local,
not a claim to use NASA Eyes' renderer.

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
