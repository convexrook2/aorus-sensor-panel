# Arboghast pass08 Sensor integration - October 22, 2026

October 23 update: the owner authorized release of the prepared 1.8.12 changes
plus accepted Canterbury. The current release checks and authority are recorded
in [the 1.8.12 release record](RELEASE-1.8.12-CANDIDATE.md). The dated evidence and
pending statements below retain the earlier integration checkpoint.

Lennon confirmed pass08 in Fleet Studio, then requested "include it now and
procceed". The 1.8.12 source candidate replaces the legacy Arboghast full model.
The rejected October 21 model and styling remain preserved and excluded.

The source model is an original X890 reconstruction after Chris Danelon's
production turntable. The exact admitted GLB SHA256 is
`10744079e7ee154daac7345bac020ca6f0bf6c256032c9636244baa461333778`;
the Blender SHA256 is
`05327285e4f3b0a09ddae357efbd7b6f730e946c79b35279a891fc9ae1e3074c`.
Editable canonical copies are in Assets/Models/Source/Arboghast-pass08.
The original study, all video-frame evidence and previous passes are retained.

The full model retains 1,165,180 triangles, 21 materials, the embedded roof finish
and eight actual warm spot lights. A survey-disc orientation preserves its
source Y-up pose. Light calibration and local spot shadows follow the reviewed
copy. No substitute source mesh or new navigation light is added. The existing
theme, monitoring layout, rotation controls, saved rendering
mode and shared showcase/holotable viewport remain in use.

The fallback/background portrait is generated in the actual Sensor native
renderer and bound to the same GLB hash. Arboghast's faded artwork uses that
portrait instead of rendering the retained legacy .ship geometry. A single-ship
export option avoids regenerating the other 21 portraits. Native diagnostics
record the actual source hash, triangles, lights, maps, quality and dimensions.

## Evidence and current boundary

- The local release build passed with zero warnings/errors.
- Native portrait export passed at 1024x2048, reporting the exact model hash,
  eight lights, survey-disc pose, shadows, ambient occlusion and full geometry.
- The 22-ship native bundle and all 22 portrait hash/resolution checks passed.
- The protected package's native saved-data showcase and holotable checks passed
  at 1024x576 and 1920x1080, with the exact GLB and eight source lights in all
  four views. Captures were visually inspected. Canvas/capture resolution,
  idle holotable rendering and return of the same shared viewport passed.
- The initial small showcase capture raced WPF composition and still showed
  loading. A 600 ms settling wait was added only to the diagnostic; the repeat
  captures show the model at both sizes. No production capture delay was added.
- All 507 encrypted assets match their source bytes. No loose protected roots,
  build keys, editable source models or debug files occur in the local package.
- Managed sensor contracts and native package resource mappings passed. The
  optional FrameView native ABI was not exercised by the managed-only check.
- Existing Anubis/Rocinante gates and all six isolated release-flow safety
  cases passed. These are local checks, separate from a real Jenkins run.
- Files outside the bounded integration changes and the other 21 ship/portrait
  entries remain exact; preservation counts and hashes are in the receipts.
- The installed 1.8.11 monitor is separate. Its automatic 15-second cycle changes
  ExpanseShip in settings.json. The exact prior settings hash was reconstructed
  by changing only that field; all other preferences, including Efficient,
  remain unchanged. This integration does not write the preference file.

## Owner-requested engine plumes

After the initial integration, Lennon requested engine plumes. Four compact
blue-white exhaust effects now start just beyond the four existing rolled
mouth lips and follow each bell's measured outward cant. Source centering and
scale are applied once. Colour, radius and length are display tuning, separate
from the production-reference geometry. The hollow throats are not capped.

The accepted GLB, editable Blender scene, portrait and eight warm source lights
remain unchanged. Plumes use the existing shared effect module and obey the
showcase's rotation/activity state; the paused holotable remains idle. Other
ships' exhaust anchors and the shared shaders remain unchanged. The native
diagnostic verifies four visible plumes and eight active source lights, with
an additional underside view that hides the display plinth only for that
diagnostic capture. Normal holotable presentation remains unchanged.

Latest plume receipts and captures are in artifacts/arboghast-plumes-20261022.
The earlier adoption package and captures are retained as historical evidence.
`node tools/Check-ArboghastPlumes.mjs` measures registration against the actual
source lip and neck geometry and checks effect on/off, source transforms and
disposal. It runs through the existing Arboghast CI gate. Native saved-data,
LIVE/resource, CI, publication and installation evidence remain separate.

## Separate Fleet Studio update

Lennon subsequently requested installation/update of Fleet Studio. Its normal
catalogue now contains the exact pass08 model, upright disc orientation, eight
work lights and the same four registered plume anchors as the Sensor candidate.
The per-user desktop app and Desktop/Start-menu shortcuts were refreshed to
1.8.12. The installed protected executable passed full-shading model/rotation
checks for Pella, Rocinante S6 and Arboghast, including Arboghast's plume control
and drive-end capture. All 63 protected assets match source; other 22 catalogue
entries, prior manifest assets and 3,300 protected files were preserved.

The previous installation is recoverable under
artifacts/fleet-arboghast-install-20261022/installed-before. Source, installation,
shortcut, archive and installed render receipts are in the same parent folder.
This updates Fleet Studio only; it does not publish or install the 1.8.12 Sensor
candidate. The installed monitoring app remains 1.8.11. Choose Arboghast in Fleet
Studio and enable Engine plumes to inspect the exhaust.

Lennon then requested brighter, light-emitting exhaust across all ships. The
shared plume renderer now integrates filled gas emission rather than drawing
a hollow tapered surface. Pure HDR plume emission participates in selective
glow; shadowed, world-scaled inverse-square spill illuminates the real nozzle
hardware. Source lamps and plume lighting remain independent. All source hulls,
materials, portraits and engine catalogues are frozen through this update.
Source contracts cover all 27 drive records / 86 registered outlets. The
protected native diagnostic separately checks HDR emission and actual hull
illumination for all 22 engine-equipped catalogue entries, with Sol Ring
remaining engine-free. Current receipts are in
artifacts/fleet-plume-radiance-20261022; later installed checks are recorded
there rather than inferred from the earlier installation receipt.

Lennon rejected that plume's appearance while orbiting. The replacement uses
one shared advected 3D density field, geometry and clock across each ship's
engines, with real hull-depth clipping. Its fresh orbit review also exposed
missing first-toggle shadow-map invalidation; model-owned shadow materials
remove stale texture references during ship changes. The rejected implementation
and its captures remain preserved. New source, motion, illumination and local
Fleet Studio update evidence is kept separately under
artifacts/fleet-plume-3d-flow-20261022. Motion clips and fixed-camera flow clips
supplement source/HDR checks; none constitutes owner visual acceptance or a
Sensor installation.

Local receipts and rollback copies are under
artifacts/arboghast-pass08-adoption-20261022. They distinguish source/asset
checks, native saved-data rendering, protected archive checks and later release
steps. Final local results are summarized in that folder's result.json, with
current native captures under native-quality/ and the initial capture evidence
retained under native-quality-before-capture-settle/.

Source checks use `python tools/Check-ArboghastAdoption.py`,
`python tools/Sync-FleetViewer.py --check` and
`python tools/Check-FleetPortraits.py`. The admitted model check is wired into
the normal CI gates. Existing accepted-ship checks remain in place.

No installation, Git commit/push, CI dispatch, installer build or external
publication has run for this candidate. The draft 1.8.12 notes and version
metadata do not imply a released package. Full LIVE/hardware and whole-app GPU
acceptance remain pending; no <=2% budget is asserted. The full CI pipeline has
not been run against this mixed research checkout. A release needs the agreed
source slice frozen to a commit, exact-commit CI, installer validation and the
remaining native/LIVE and resource acceptance. Exact camber, hidden fittings
and lamp photometry remain authored fits accepted for the current reconstruction.
