# Fleet rendering and GPU target

## Surfaced experience and rendering choices

After accepting the trial, the owner requested visible choices for AORUS Lite
and The Expanse Experience, with optional Efficient rendering and unrestricted
Full Fidelity. Settings now opens with Experience and, for Expanse, Rendering.
The same rendering choice is in Ship options. AORUS unloads the ship viewport
and stops solar/background activity; it does not remove installed files.
Separate asset-free Lite installer packaging remains release scope.

Full Fidelity is the default when no rendering preference is saved. It has
no artificial ship or solar frame cap: ship motion uses browser animation
frames and solar motion uses native composition frames. Efficient retains
the accepted 30 fps ship / 10 Hz idle solar policy, with identical model detail,
textures, lighting, shadows, AO, physical resolution and capture cadence.
Both preserve pause, covered/minimized stopping and live monitoring bindings.

The visible saved-data `--check-experience --leave-review-open` check passed
as process 69580, Release assembly
`E6907FDDFF8DF6B028C9C1B96D68F99D1B8368E5184910E16DFFF355A0882D23`.
It first reproduced the missing selector in the preceding build. The final
check selected the actual Settings controls, verified AORUS renderer release,
remembered themes, Full Fidelity's null frame limits for both showcase and
holotable, Efficient's 30/10 budgets with unchanged model identity, covered
solar stopping, unchanged readings/peaks/preferences, and reachable selectors
at 1280×800 and 480×800. The stale Ship options motion-command lookup was fixed.
Legacy preference migration and serialization retained unrelated polling,
theme and rotation choices. Preview selections did not write owner settings.

Native renderer counters observed 160.006 draws per animation second in Full
Fidelity and 30.002 in Efficient on this display. These are not physical FPS
or GPU measurements. Receipt and images: `artifacts/experience-check/result.json`,
`review.png`, `narrow.png`, `aorus-lite.png`, `expanse-full.png`, and
`ship-options.png`. Build, syntax and full source bundle validation passed.
No installation, CI, publication or release occurred.

The GPU numbers below belong to their pinned earlier trial assemblies, not
this newly selectable Full Fidelity profile. In particular, earlier uncapped
showcase measurements still used a timer-limited solar map and capped
holotable; they do not establish this profile's full app-tree GPU use.

## Approved 30 fps ship / 10 Hz solar trial

The owner approved a trial limiting actual ship motion drawing to 30 fps and
idle solar scene updates to 10 per second. Ship rotation still uses elapsed
requestAnimationFrame time, and native WebView capture cadence is unchanged.
Explicit ship interaction/resize invalidations draw on the next animation
frame. Solar pointer events remain immediate and focus transitions retain
their existing up-to-60-update path. Full models, maps, shadows, AO, lighting,
physical capture resolution, rotation speed and backgrounds remain unchanged.
The trial is implemented and the monitoring comparison is complete. Normal
automatic rotation still exceeds <=2%; visible smoothness remains for owner
review. Earlier results farther below are explicitly marked as historical.

### Trial LIVE monitoring results

| Showcase and solar state | Pella average GPU | Rocinante average GPU |
|---|---:|---:|
| Ship rotating, solar moving |4.058%|3.550%|
| Ship rotating, solar paused |3.617%|3.143%|
| Ship paused, solar moving |0.597%|0.580%|
| Ship paused, solar paused |0.215%|0.168%|

The normal view is 68.3% lower for Pella and 69.3% lower for Rocinante than
the earlier uncapped comparison (12.799% / 11.559%). These are short Windows
counter comparisons, not a guaranteed GPU budget on another machine. Solar
motion with the ship paused is below 2% for both; continuous full-quality ship
drawing remains the larger cost. Costs of independent GPU engines are not
additive, and changing clocks/background activity limit attribution.

All eight cases contain 15 samples over 23.82–24.31 actual seconds after
eight seconds settling. The same Ryzen 9 9950X3D / RTX 5090 host, 1920×1080
window, 3840×2160 primary display and 96 DPI were used. Metric: busiest hardware
GPU engine across the nine-process app tree (monitor, six WebView2 descendants,
PresentMon and its console). Desktop compositor/driver work outside that tree
is excluded. Private resident RAM, summed working sets and private committed
bytes remain separate in the receipts; GPU memory sums are not exclusive VRAM.

The exact Release assembly is
`2D05965F55B1D2223BA62DFA7F031D984F3C209F5F6B1C141E814321BD183F42`.
Native process 3720 retained the original full models and maps. Canvas,
capture and physical showcase dimensions matched: Pella 342×735, Rocinante
342×524. Source hashes and triangle counts are identical to the earlier
comparison. Long native renderer-counter intervals measured 29.994 and 30.002
draws per animation second respectively; these are not physical display FPS.

Every accepted sample passed the 45-point coverage guard. Two covered attempts
were discarded before the valid continuation of the same verified process and
session. The diagnostic later closed itself. Raw JSON/CSV sets end in `-48360`
under `artifacts/performance`; the hashed index and native/pixel evidence are
`cadence-trial-20261021-120854/visual-summary.json` and `visual-native.json`.
The continuation command is preserved there as `Resume-VisualComparison.ps1`.

### Trial native functional and solar checks

Release build and JavaScript syntax checks passed with zero compiler warnings
or errors. `Sync-FleetViewer.py --check` passed all 22 full models/maps/hashes
and the 74-file source bundle. The visible saved-data rotation check passed
Pella, Rocinante and Anubis at about 30 draws/animation-second, unchanged
30-second revolution speed, direct pause/resume, exact paused angle, shared
holotable ownership, three layouts, inactive stop and disposal on leaving ship
themes. An initial startup sample observed no active rotation and was not used
as cadence evidence; the complete repeat passed as native process 41100.

The 100 ms solar timer achieved 9.00 idle updates/second in its native check;
settled PAUSED produced zero timer/scene updates. Focus transitions continued
and settled, and an actual short pointer gesture refreshed directly (two
pointer events / two scene updates over 18.6 ms). This verifies the immediate
input path, not sustained pointer/display FPS. Seven deterministic solar
captures remained pixel-identical to the preceding 50 ms version, with zero
changed pixels in each 978×184 image. Durable receipts:
`cadence-trial-20261021-120854/rotation-check.json`, `solar-check/` and
`solar-image-comparison.json`. No installer, installed app, CI or release was
changed, and source model geometry was not edited.

### Trial LIVE holotable and minimize/restore results

| View | Pella average GPU | Rocinante average GPU |
|---|---:|---:|
| Holotable idle |0.125%|0.135%|
| Holotable rotating |6.617%|6.288%|
| Minimized |0.000%|0.000%|

These six cases used the same exact Release assembly, host, 1920×1080 window
and nine-process scope. Each has ten samples over 15.54–15.71 actual seconds
after eight seconds settling. The visible cases passed coverage checks on
every sample and had solar navigation stopped both before and after. Native
process 55100 confirmed renderer inactivity when minimized, resumed frames
after restore and identical full models. The short zero minimized counters
do not mean monitoring collection or other applications stopped.

Active holotable GPU was higher than the preceding run (5.392% / 5.570%),
so no holotable rotation efficiency gain is claimed. Its earlier interval
logic produced 24.288 / 26.138 draws per animation second over native counter
intervals; carrying the timing remainder now produced 30.020 / 30.031. That
increased drawing is consistent with the higher GPU cost, though the short
samples and clock/background differences do not isolate a sole cause.
These remain renderer counters, not physical presentation FPS. Idle redraw
on demand and covered/minimized stopping remain intact.

The first holotable attempt was interrupted when ChatGPT covered the review;
it produced no accepted case. After the owner made the desktop available,
the same verified session continued and completed all six cases, then closed
itself. Raw JSON/CSV sets end in `-12484`; hashed receipt index and native
evidence are `cadence-trial-20261021-120854/lifecycle-summary.json` and
`lifecycle-native.json`. `Resume-LifecycleComparison.ps1` preserves the exact
continuation command. All 14 monitoring/lifecycle cases are complete; visible
motion acceptance and the <=2% normal-monitor target remain separate.

Latest owner correction: restore automatic rotation in the left showcase with
a direct PAUSE ROTATION / RESUME ROTATION control beneath the ship. Preserve
the exact paused angle and save that choice separately from map/background
motion. The showcase and holotable share one full-quality viewport; leaving
ship themes releases it. The earlier static-portrait study below is historical
evidence, not a GPU result for this restored rotating mode. Normal-monitor
target: <=2% average GPU across the app tree. The restored rotating mode exceeds
that target in the controlled LIVE comparison below.

The restored showcase passed the visible saved-data check for Pella, Rocinante
and Anubis: default rotation, direct button pause/resume, exact paused angle,
one shared viewport through holotable open/close, reachable controls in three
layouts, inactive rotation stop, and disposal on leaving ship themes. Receipt:
`artifacts/showcase-rotation-check/result.json`. The prepared portraits remain
as loading fallbacks and offline provenance assets. This native check does not
establish LIVE sensors, installed behavior or the GPU target.

## Earlier uncapped LIVE comparison after restoring rotation

`tools/Measure-FleetPair.ps1 -Seconds 15 -IsolateMap -CompareRotation` completed
all eight visible cases on October 21. Release assembly SHA-256:
`7175842BCA4EF5C461C63139792B1BF38C846056C62E9F29886B5D21C240CEA1`.
The opt-in diagnostic temporarily toggles each animation independently without
writing the owner's saved settings. Live sensor collection continues throughout.

| Showcase and solar state | Pella average GPU | Rocinante average GPU |
|---|---:|---:|
| Ship rotating, solar moving |12.799%|11.559%|
| Ship rotating, solar paused |12.461%|11.119%|
| Ship paused, solar moving |3.450%|3.171%|
| Ship paused, solar paused |0.503%|0.477%|

Every case contains 15 counter samples over 23.17–23.86 actual seconds after an
eight-second settling period. Each used the same 1920×1080 native window at
96 DPI on the Ryzen 9 9950X3D / RTX 5090 host. The nine-process tree includes
the monitor, all six WebView2 descendants, PresentMon and its console helper.
The metric is the busiest hardware engine across that app tree. It excludes
unrelated apps and compositor/driver work outside the tree. It is neither
whole-machine GPU utilization nor an additive attribution of independent costs.
Changing GPU clocks and the short sample duration limit comparisons.

A read-only 45-point interior screen-coverage check ran on every sample;
all cases passed it and recorded zero covered points before and after.
The native renderer validated the original full models and maps without
geometry reduction or texture resampling: Pella 1,023,503 triangles, Rocinante
687,992. Actual browser/capture pixels matched physical showcase pixels:
342×735 for Pella and 342×524 for Rocinante. Model source SHA-256 values:

- Pella: `2a6126d5a1570607f8687fc671f85e8afdea04f624fd05ff737fc6021be53ff0`
- Rocinante: `d391bc43b0843a328e435f0eefe16a34a0c8724cdaf0fe275f449fcd330e3eb3`

Raw JSON/CSV files end in `-35016` under `artifacts/performance`. The durable
index, including each receipt's SHA-256, is
`artifacts/performance/visual-isolation-20261021-summary.json`; native scene and
pixel evidence is in `visual-isolation-20261021-47816.json` in that directory.
Private resident RAM, shared working-set sums and committed private bytes are
recorded separately. GPU memory-counter sums remain excluded from VRAM claims.

The <=2% target is met only with both animations paused in this run. Restoring
continuous full-model rotation makes it the larger visible cost. Pausing only
the ship still exceeds 2% with solar motion running. No model quality, rotation
speed, native capture cadence or installed files were changed for the comparison.

## Earlier LIVE holotable and minimize/restore comparison

The separate lifecycle pass completed six cases using Release assembly
`CF89A9B6DCA4FA57F93C44488975701105DA631426AB4C0693EF12B11B343EDB`.
Its source change makes the opt-in review activate its own window at phase
changes and stop if the holotable is closed. Model rendering and quality are
unchanged from the visual-isolation assembly.

| View | Pella average GPU | Rocinante average GPU |
|---|---:|---:|
| Holotable idle |0.147%|0.130%|
| Holotable rotating |5.392%|5.570%|
| Minimized |0.000%|0.000%|

Each case contains 15 samples over 23.59–23.68 actual seconds. All visible
samples passed the same screen-coverage guard and kept the covered solar map
inactive. Native receipts confirm full-model identity through both views,
renderer inactivity when minimized, and resumed rendering after restoring.
The review then closed itself. The zero minimized counters describe this short
app-tree sample; monitoring collection and unrelated apps continued.

Index: `artifacts/performance/lifecycle-20261021-summary.json`; native evidence:
`artifacts/performance/lifecycle-20261021-28448.json`. The six raw JSON/CSV sets
end in `-77312` in the same directory. The earlier covered lifecycle attempt
produced no accepted resource result.

The visual-isolation renderer counters also show roughly 160 rendered frames
per animation second in the small rotating showcase for both ships. The
holotable retains its existing 30-frame limit. This is a renderer-counter
observation, not physical display FPS. The solar map's separate 20-update target
does not limit the rotating ship. Further work toward <=2% while moving needs
another rendering strategy or an accepted cadence tradeoff; the current result
does not justify reducing source geometry, maps or native capture cadence.

## Earlier static-portrait study

## Implementation and source evidence

- All 22 current ship/gate portraits are 1024×2048 PNGs rendered using the native
  full GLB/PBR/shadow/AO/glow pipeline. The camera is elevated and angled around
  the ship following the owner's correction. The export bakes the glow against
  the dark display background to avoid transparent browser-capture artefacts.
- The portrait receipt checks image hashes/dimensions and source-model SHA-256.
  No master model, mesh, map or accepted material/light strength was changed.
- Monitoring creates no ship WebView. Holotable opens a fresh full renderer,
  supports orbit/zoom, starts with rotation off, and redraws on demand. Close
  disposes/discards it. Switching to AORUS also closes it.
- Debug and Release sensor builds passed without warnings/errors. The portrait
  validator's 12 malformed/valid fixture tests and actual 22-image bundle passed.
  Roci, Anubis and shared light-glow source contracts passed.

## Native saved-data review

`tools/Prepare-StaticFleet.ps1 -SkipExport -Review` passed. Receipt:
`artifacts/static-fleet-check/result.json`.

All 22 static portraits loaded without a ship renderer. Pella, Rocinante,
Anubis, Guanshiyin and Sol Ring additionally passed full-model source hash
parity, idle frame stability, programmatic orbit/zoom, close/disposal,
fresh reopening, and switching to AORUS/disposal. This is native saved-data
evidence, not installed-app, manual-input, live-sensor or GPU-budget proof.

## Initial LIVE measurement

Release development build, 1920×1080 window on a 3840×2160 primary display,
96 DPI, Ryzen 9 9950X3D/RTX 5090 host. Whole descendant tree includes the monitor,
WebView2 when present, PresentMon and console helpers. Metric is the busiest
GPU hardware engine across this tree, not whole-machine utilization or a sum
of independent engines. Desktop compositor/driver costs outside the tree are
excluded. GPU memory-counter sums remain unvalidated and are not VRAM claims.

| Pella view | Average GPU | Actual sample time | Processes |
|---|---:|---:|---:|
| Static monitoring, solar animation on |5.035%|23.173s|3, no WebView2 |
| Full-detail holotable, idle |0.737%|23.966s|9 |

Raw receipts under `artifacts/performance`:
`pella-monitor-visible-20261021-095125-346-66572.json` and
`pella-holotableidle-visible-20261021-095203-820-66572.json`.

These results do not meet the normal-monitor target. Solar animation remains
the leading source candidate (native 3D and both overlays request 20 Hz updates);
its individual contribution needs a controlled on/pause comparison.
The initial rotating sample failed when a helper's private working-set counter
disappeared. Subsequent ship selection changed during the interrupted session;
no rotating/minimized or Rocinante result is claimed from that run.
The separate map-only comparison did not start: Windows sensor-elevation approval
was canceled (Win32 error 1223). It was not retried, and supplies no map-specific
measurement. The <=2% target and remaining GPU attribution stay pending.

## Earlier solar scheduling and drawing changes at 20 Hz

This section records the preceding 50 ms configuration. Its former `after`
receipt and images are preserved in
`artifacts/performance/cadence-trial-20261021-120854/before-solar`; the current
`artifacts/solar-efficiency/after` folder contains the new 100 ms trial check.

The settled PAUSED map previously continued refreshing. It now stops the timer
and scene updates while preserving animated focus, direct dragging and zooming.
Stars and stable schematic orbits have retained drawing layers with bitmap
caches sized for the actual ancestor display scale. The Pella grid uses the same
physical-scale cache. Dynamic map layers, meshes, textures, lighting and the
configured 50 ms idle interval remain intact. Earth lighting skips identical
directions and settles to the final direction after a paused camera change.

Visible saved-data receipts in `artifacts/solar-efficiency/before` and `after`
show approximately 16 scene updates/second during unattended motion in both
versions. PAUSED changed from 15.94 updates/second to zero. The requested idle
rate is 20 updates/second; it is not a guaranteed physical presentation rate.
Direct pointer movement refreshes immediately and bypasses that idle timer.
The short recorded pointer gesture establishes that input path, not sustained
display FPS.

Seven deterministic before/after map captures (Sun/Earth/Saturn/Jupiter/Medina
schematic and Sun/Earth true scale) compared identically: zero changed pixels
in every 978×184 image. Receipt: `artifacts/solar-efficiency/image-comparison.json`.
The native check also passed pause and paused-camera settling. These are
saved-data image/scheduling checks, separate from the LIVE GPU comparison.

The first post-change LIVE comparison produced low GPU counters, but a native
observation found the review covered by another window. A foreground-guarded
repeat also stopped when another app took focus. These attempts do not establish
an unobstructed GPU result and are excluded from the target conclusion. The
map comparison now requires a read-only 45-point interior screen-coverage check
on every sample; focus alone is not sufficient to tell whether a window is
covered. The visible comparison remains pending an unobstructed run.

No installer, CI dispatch, commit, push or release was performed for this change.
