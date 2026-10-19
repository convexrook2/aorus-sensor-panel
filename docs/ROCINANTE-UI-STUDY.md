# Rocinante interface and Season 6 colour study

Owner scope, 2026-10-19: revisit Rocinante after acceptance of Pella, use the
saved cockpit hull-scan reference and Chrome bookmarks, keep latest geometry
and accepted smooth rotation, show a study before app changes. Owner explicitly
corrected “blueprint” to “hull scan” and rejected the current colours as a basis
for Season 6. No runtime implementation or model replacement is approved here.

## Research actually used

The local Chrome **THE EXPANSE SENSOR** bookmark folder was read without mutation
because Chrome was absent from the connected-browser inventory. Its selected
links include Harrison Vincent, Rhys Yorke, Lee Fitzgerald, Karl Crosby,
Sébastien Garnier, Charles Q. Li, Kelvin Tam and Belter Charts. Non-Rocinante
ships are context for research breadth, not evidence for Rocinante's graphics.

- [Harrison Vincent — The Expanse](https://www.harrisonvincent.com/works/the-expanse):
  creator confirms UI/animation for the 2019 Prime Video SDCC immersive set.
  Saved original `docs/references/Rocinante/rocinante-sdcc-hull-scan.png` was
  inspected. Dominant horizontal hull, cyan ruled lines, small annotations and
  round diagnostic views; event artwork, not an identified Season 6 episode.
- [Rhys Yorke — Rocinante UI](https://rhys.artstation.com/projects/q9Am1L):
  actual bookmarked creator page opened and visually inspected in the Codex
  browser, including several set views and the flat navigation screen. Burgundy
  header bars, cyan/cream traces, narrow side instruments and small dials.
  This does not establish that every portfolio image is from Season 6.
- [Rhys Yorke interview](https://www.pushing-pixels.org/2021/09/04/the-art-and-craft-of-screen-graphics-interview-with-rhys-yorke.html):
  firsthand discussion of faction identity and practical actor interaction;
  informs readability and restrained hierarchy rather than decorative clutter.
- [Ryan Dening — Roci Design](https://deningart.artstation.com/projects/04DvnY):
  production concept artist documents later-season paint and upgrades. Page
  text was reviewed; it identifies the updated paint pass as Season 4, so it
  is not relabelled as a final Season 6 CSL material specification.
- [Rocket Science VFX retrospective](https://www.rsvfx.com/rsvfx-delivers-over-2000-cg-heavy-vfx-shots-over-epic-six-season-run-on-the-expanse-a-retrospective/):
  production studio context, not a calibrated paint guide.
- Saved `RocinanteS6-TV-S06E06-CSL.jpg` inspected as the existing final-episode
  close-up reference. Fresh playback was not performed. Its attribution remains
  the existing archive's; dark hull, pale blue-grey reflective forward plating
  and worn muted markings guide the material proposal. Scene illumination and
  grading prevent claiming exact production RGB values.
- [Master Replicas XL gallery](https://www.masterreplicas.co.uk/products/the-expanse-the-rocinante-xl-copy)
  and saved creator print photos are secondary paint/angle comparisons. The
  replica is not treated as definitive final-episode CSL finish evidence.

Direct ArtStation generic/API requests returned 403/sign-in shells; the public
Rhys portfolio rendered successfully in the browser. No sign-in, permission
change or access-barrier bypass was performed. That temporary research tab was
closed after inspection. No Prime Video account or playback was used.

## Study boundary and choices

**Hull diagnostics** uses a wide left hull view, temperature dials and primary
CPU/GPU readings beneath it, and solar navigation plus memory/network readings
on the right. **Flight console** keeps a tall left showcase with navigation
above the instrument bank and the production-reference burgundy headers. Both
retain the existing background as a quiet layer and make monitoring readable.
All readings are explicitly labelled sample data. The solar scene is depicted
schematically for layout; no replacement solar asset is proposed or installed.

The model illustration uses current `FleetStudio/catalog.json` entry
`RocinanteS6`, 687,992 triangles, decoded SHA-256
`cc3018a5ba94efdbaa17a1fd548c533e5bc4d51d5cb1300bec1f414e224840f9`.
The monitor's separate `Rocinante`/Tachi asset remains 140,863 triangles; it is
not silently relabelled or used as the Season 6 geometry. Material colours in
the illustration are a proposal, not accepted production paint. Blender changes
materials only in memory for a still render, exports no model, saves no blend,
and checks the original compressed asset remains byte-identical. Geometry,
catalogues, app sources, native renderer timing and running live Pella are
unchanged. Study stills do not demonstrate native animation or performance.

Study sources, render receipt and local preview scripts are under
`output/rocinante-ui-study-20261019/`. The inline study lives in the thread's
visualization folder as `rocinante-hull-scan-study.html`. Owner review remains
pending. Do not implement either UI or paint proposal until the owner directs it.

## Validation and cleanup

Visible VS Code runs completed `render-study.py`, `Prepare-Study.ps1` and
`Review-Study.ps1`. Node syntax and the inline wrapper check passed. Blender
verified full source triangles and hash and exited. The final hull image was
reframed to include the whole ship; camera framing is study-only. The final
inline fragment is 678,232 bytes with embedded study renders and the existing
background; no network/model loading or continuous renderer is used by it.

Both final variants were visually inspected in VS Code's integrated browser.
CPU/GPU selection changed its selected state and footer label; the variant
carousel switched successfully to the distinct Flight console layout. This
is local study/browser evidence, not live sensor data, native implementation,
ship-material acceptance or performance proof. No app build was run.

The temporary VS Code preview tab and research tab were closed; port 8782's
listener was stopped and absence verified. No Blender process remained. Hashes
of the accepted Pella exe, DLL, viewer and model still match the accepted
resource-test receipt. No unrelated app was closed or changed. Source model
and app runtime remain untouched; owner study/colour review is pending.

## Owner correction: fixed monitoring positions

Lennon rejected the alternative arrangements: the basic monitoring layout must
stay consistent so users do not scan around for values; the left is always the
ship showcase. This supersedes the Hull diagnostics and Flight console layout
choices above. They remain historical studies, not implementation candidates.

The revised `rocinante-fixed-layout-study.html` keeps the established landscape
hierarchy from ShipPanel.Calculate/PellaPlan: left showcase; CPU and GPU at top;
solar navigation below them; memory and network below navigation; board/cooling
at the bottom. No per-ship placement variation is proposed. Only visual styling
and the existing proposed S6 material illustration differ. Future ship themes
must preserve positions within the selected display mode; AGENTS.md records it.

`Review-FixedStudy.ps1` passed Node syntax and preview-wrapper generation in the
visible VS Code terminal. A final VS Code rendered check was interrupted when
focus changed during native input; no substitute hidden validation was used.
The corrected inline study is ready for owner review; final VS Code visual
validation remains pending. Its temporary server PID 58076 was verified by
exact command, executable, parent and known PID, then stopped for cleanup.
No product source, model, catalogue or rotation change was made in this pass.

## Owner rejection: Season 6 finish, 2026-10-19

The owner rejected the current Season 6 ship styling after seeing the fixed-layout
study. Layout preservation is still required; it does not constitute acceptance
of the ship materials or screen treatment. Do not carry the current colour study
forward as an accepted Season 6 baseline.

The saved finale close-up shows an uneven, reflective blue-silver/violet surface
on specific armour panels, dark surrounding hull, visible seams and worn markings.
This is a visual observation under the shot's lighting, not a calibrated paint
specification. The study's broad smooth coloured panels fail to convey that finish.
Preserve the latest geometry and smooth native rotation while proposing any
subsequent material correction separately.

Research was refreshed without opening app/browser windows or changing models:

- The existing CSL close-up remains the strongest saved late-Season-6 hull
  comparison. Its source is the archived wiki image, not fresh Prime playback.
- [Naren Shankar's Season 6 opening-episodes interview](https://www.slashfilm.com/712225/the-expanse-showrunner-talks-first-two-episodes-including-the-azure-dragon-and-a-certain-sex-scene/)
  includes a Prime Video-credited Rocinante bridge still. It establishes the
  Season 6 set context, but its wide holographic view is not a legible close-up
  specification of all screen components.
- [Frank Yang's explicitly Season 6 production concept portfolio](https://frankyang.artstation.com/projects/Lea21A)
  identifies Rocinante reactor-room views, courtesy of Alcon. These are interior
  concept references, not a bridge-UI or final hull-paint guide.
- Rhys Yorke's Rocinante gallery does not identify its images as Season 6;
  the interview discusses work through Season 6, but that does not date each
  pictured graphic. Keep these references labelled general Rocinante UI.
- Ryan Dening explicitly identifies the updated paint pass on his Roci page
  as Season 4. Replica/printed-model photos remain secondary comparisons.

Current app, model bundle, renderer cadence and fixed monitoring positions remain
unchanged. A confirmed Season 6 screen close-up remains a research gap.

## Bookmark gallery sweep, 2026-10-19

Read the current Chrome `THE EXPANSE SENSOR` bookmark folder without modifying
it. Inspected the following in the Codex browser, scrolling the image stacks
through their lower images and project descriptions rather than treating the
first image as the whole reference. ArtStation's side arrows in these views
switch projects, not additional hidden views of the same image.

| Bookmark / collection | Observed result |
| --- | --- |
| [Rhys Yorke, Rocinante UI](https://rhys.artstation.com/projects/q9Am1L) | Ten linked images including the art-book spread. Actual Roci set screens, system-control close-up, overhead table and flat navigation artwork; season not individually identified. All image-stack sections inspected. |
| [Kelvin Tam, Tycho Season 5](https://kelvintam.artstation.com/projects/n0E9B4) | Nine station/docking views inspected to the footer. No clearly identified Roci close-up found; not a Roci material guide. |
| [Karl Crosby, Season 2](https://www.artstation.com/artwork/1ndxoZ) | Full stack inspected: Arboghast, farm barge, Guy Molinari flight deck and breaching pod. No Roci-specific image identified. |
| [Lee Fitzgerald, Expanse album](https://www.artstation.com/leefitzgerald/albums/2514082) | Seventeen project entries. Followed the mixed S4, S5 and S2-3 collections below, inspecting their image stacks through the end. Other explicitly named vehicle/prop projects were not exhaustively opened. |
| [Lee, S4 Various](https://www.artstation.com/artwork/OmQNVe) | Buried Roci landing-leg sheet, reactor concept, Barbapiccola rescue composition sheet and view from Roci. Explicitly S4; not S6 paint evidence. Two embedded clips showed station views; no full video-playback audit claimed. |
| [Lee, S5 Various](https://www.artstation.com/artwork/Qn9vVd) | Full image stack inspected. Tycho drones/pods, props, satellites, two useful Pella interior concepts and Drummer fleet; no Roci-specific finish sheet identified. |
| [Lee, S2-3](https://www.artstation.com/artwork/X1rzxL) | Full image stack inspected. Roci medlab and Prospero launch-area view from Roci; numerous other ships/sets. Earlier-season context only. |
| [Sebastien Garnier, Charon](https://www.artstation.com/artwork/lD9L4V) | Ten linked images inspected. Artist explicitly identifies it as non-canon commissioned tabletop fan art; no Roci image identified. |
| [Charles Q. Li, Chetzemoka](https://www.artstation.com/artwork/NyWLlg) | One still and three short embedded clips visible through project description; Chetzemoka asset/station shots, no Roci-specific shot identified. Not a full frame-by-frame video audit. |
| [Wiki bookmark](https://expanse.fandom.com/wiki/Category:The_Expanse) -> [Rocinante TV media](https://expanse.fandom.com/wiki/Rocinante_(TV)#Media) | Inspected gallery rows and opened the final recon-drone-control image in its lightbox. Thin warm red/amber rules, off-white text, plotted traces and model inset on black. The image URL has archive timestamp `20201011003712`: this is not confirmed S6 evidence. |
| Harrison Vincent | Exact bookmarked page blocked by saved browser permission. No alternate-browser/tool workaround attempted. Previously saved hull-scan image remains available; full current gallery not inspected. |

Gradio, Tripo lattice-probe model and Belter Charts are tool/map bookmarks, not
Rocinante production photo galleries, and were not opened for this sweep.

Useful original image links (no copies of remote art downloaded):

- [Rhys, system-control close-up](https://cdna.artstation.com/p/assets/images/images/027/972/238/large/rhys-yorke-ex-rocinante-05.jpg?1593107198)
- [Rhys, overhead display](https://cdna.artstation.com/p/assets/images/images/027/972/622/large/rhys-yorke-ex-rocinante-08.jpg?1593107800)
- [Rhys, flat navigation graphic](https://cdnb.artstation.com/p/assets/images/images/027/972/645/large/rhys-yorke-ex-rocinante-09.jpg?1593107855)
- [Lee, Roci landing legs](https://cdna.artstation.com/p/assets/images/images/056/026/318/large/lee-fitzgerald-nf-rocimods-gear-ext-02.jpg?1668286225)
- [Lee, Roci reactor](https://cdnb.artstation.com/p/assets/images/images/056/026/341/large/lee-fitzgerald-nf-rocireactor-int-02a.jpg?1668286280)
- [Lee, rescue composition sheet](https://cdnb.artstation.com/p/assets/images/images/056/026/349/large/lee-fitzgerald-nf-rocisavingbarb-wideview-01.jpg?1668286289)
- [Lee, Roci medlab](https://cdna.artstation.com/p/assets/images/images/055/956/104/large/lee-fitzgerald-nf-roci-medlab-01.jpg?1668110209)
- [Wiki, recon-drone-control image](https://static.wikia.nocookie.net/expanse/images/8/8b/Rocinante_Recon_Drone.jpg/revision/latest/scale-to-width-down/1000?cb=20201011003712)

The temporary research tab and an advertising popup were closed; in-app browser
tab inventory was empty afterward. Existing user Chrome tabs were untouched.
No app, renderer, model or layout edits were made.

## Reference-screen revision with owner emblem

The owner requested a new study using the recovered Rocinante screen references
and supplied the favourite crew emblem. `rocinante-reference-console-study.html`
in the thread visualization folder is the current UI review candidate. It keeps
the fixed monitoring hierarchy and left showcase, using finer separators,
compact labels, subdued red boundaries, amber dial marks, a dark gridded field
and the unchanged owner-supplied full-colour emblem beneath the ship. Large
burgundy header fills were removed. The source background remains embedded.

The study reuses the full latest S6 geometry illustration; it does not resolve
the rejected hull-material proposal. The visible label says the CSL finish is
under review. No geometry export, model paint change or app implementation is
included. Readings are sample data; the solar diagram remains a study schematic.

`Prepare-ReferenceStudy.ps1` ran visibly in X890 VS Code: script syntax, embedded
assets and protected source/model hashes passed. The preview loaded in VS Code's
integrated browser; the full composition, logo and dials rendered. Selecting GPU
updated its highlight and footer without moving values. The preview tab was
closed, its terminal server interrupted and port 8782 confirmed stopped.
`output/rocinante-ui-study-20261019/reference-pass-validation.json` records the
checks. This was study-only evidence at that point.

## Native console implementation, 2026-10-19

The owner's follow-up to proceed reopened the native Rocinante console. Its
palette now uses warm off-white readings, amber gauge ticks, muted labels and
thin red instrument boundaries over the existing backdrop and static grid.
The original supplied crew emblem is embedded unchanged beneath the left ship
viewport. CPU/GPU, navigation, memory/network and board/cooling retain the
accepted Pella console positions for each display mode. Collector bindings,
solar navigation and the renderer's smooth rotation cadence are unchanged.
Other ship themes retain their styling.

The native Rocinante catalogue previously pointed to the original Tachi asset.
It now resolves to the existing separate S6 study: 687,992 triangles, decoded
SHA-256 `cc3018a5ba94efdbaa17a1fd548c533e5bc4d51d5cb1300bec1f414e224840f9`.
This is a lossless copy, not a geometry or material edit. The old native model
and catalogue are recoverable in `artifacts/roci-native-before-20261019`;
Fleet Studio's source and separate original Tachi entry remain intact.
Hull-finish acceptance remains separate from this console implementation.

Run the local review candidate from X890 VS Code's integrated terminal:

```powershell
& .\artifacts\roci-console-review\SensorPanel.exe --windowed --theme Rocinante --runtime-check
```

`Validate-RociConsole.ps1` ran visibly there: scoped model sync and exact bundle
inventory/hash/map checks passed for 21 ships, Release build succeeded, and
`--check-mapping` exited successfully. The renderer, Pella bundle and Fleet
Studio S6 source retained their pre-check hashes; the emblem matches the supplied
image. These are build/source checks. The LIVE launch was issued from the same
terminal. After the owner requested a repeat Windows prompt, elevation succeeded
and the actual LIVE candidate opened (PID 72436). Its fresh runtime receipt
passed at 21:03:07 UTC with 18 collection cycles and 144 available readings.
The WebView2 renderer reported Rocinante, 687,992 triangles, the expected S6 hash,
full maps, shadows/AO, motion active and no geometry/texture reduction or frame
limit. Native window inspection confirmed the left showcase, original logo,
dials, solar navigation and live values. This confirms runtime/rendering, not
owner acceptance of hull paint or a new full-process resource benchmark.
Receipts are preserved in `artifacts/roci-console-live-receipt`.
The requested live app is left open for review. No temporary browser/server was
opened for this pass. No installed package or startup configuration was changed.

## Local installer 1.8.6

At the owner's request, `tools/Build-Installer.ps1` completed visibly in X890
VS Code. Package build, collector/native ABI checks, published mapping/resources,
44 exact fleet file hashes and Inno Setup compilation passed. The installer and
published monitor report version 1.8.6. `Finish-Installer186.ps1` verified the
installer checksum and a Desktop copy named `X890 Sensor 1.8.6 Setup.exe`.
The installer is 255,739,386 bytes; SHA-256:
`7433743D90E42DC863F87E9C39DD450B966079FC5A16951ECCAA356B18841C8C`.
The package manifest and build transcript are under `artifacts/installer` and
`artifacts/installer-build.txt`. Installation and installed visual review remain
with the owner. No renderer/model quality edits were made for packaging.
