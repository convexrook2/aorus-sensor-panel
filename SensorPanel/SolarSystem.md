# Solar navigation

The original **SOL / NAVIGATION** diagram is the primary view again. Its native
WPF header, destination list, elliptical orbit layout, asteroid belt, pan/zoom,
selection markers, separate Medina inset and **RESET** control come from the
v1.7.1 implementation. There is no added SYSTEM / INNER / DETAIL toolbar.

Detailed assets come from the pinned Belter Charts source at
`ba86075477fc529294a5e51cb4e1b0d183ff893a`: real planet/Sun texture maps, Earth
clouds, the radial Saturn ring texture, Eros' 89,398-face shape and Phoebe's
packed mesh. `SolarBodyAssets.cs` loads them into cached, frozen native WPF
models. Eros geometry is retained without simplification; batches stay below
WPF's per-mesh limits. Vertex colours use face averages in a native palette.
Native lighting approximates the source materials; the web shaders are not used.
Selecting a body enlarges its model within the diagram so its detail is visible.

The panel does not load the web renderer or require WebView2 for navigation.
`NavigationWeb` and the previous adapter source are preserved for reference;
`BelterSystemView.cs` is excluded from compilation. The pre-restoration version
is also preserved in the timestamped `artifacts/native-solar-restore-safety-*`
backup. The scientific native 3D model is retained separately in source.

## Scale and landmarks

The owner's chosen original diagram is explicitly **SCHEMATIC / NOT TO SCALE**.
Models retain their shapes; display radii, orbital spacing and phases are
illustrative. It does not claim physical AU geometry, an ephemeris or live ship
tracking. Sun and body rotation are decorative and stop with motion disabled.

Tycho is shown as a mobile Belt station. The stationary Sol gate marker sits
outside Uranus and inside Neptune, corresponding to roughly 2 AU beyond Uranus;
its direction is illustrative. Medina retains the separate labelled Ring Space
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
installed-build or live-hardware proof. Animation is capped at 20 fps.

See `SolarNavigation/CREDITS.txt` for full sources and adaptation details.
The previous web build remains reproducible with `tools/Build-BelterNavigation.ps1`;
run `Build-SolarAssets.ps1` afterward to restore the active native asset exports.
