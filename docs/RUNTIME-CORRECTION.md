# Current-design live runtime correction — 2026-10-19

This supersedes the rejected still-art/2D-graph mode in `7ea9edc`. The monitor
again uses its current WebView2 ship viewport, full model geometry and PBR maps,
native pixel ratio, shadows/AO, solar navigation, holotable and saved appearance
controls. Fleet Studio stays a separate on-demand desktop app with full shading
by default. No ship geometry or source model bytes were changed by this fix.

Efficiency changes preserve the visuals: coalesce repeated model requests and
activity/resize messages, reuse shadows when the hull/light is unchanged, pause
visual work while hidden/minimized, request lower WebView memory while inactive,
and decode/release the AORUS animation sequence only when its theme needs it.
Sensor collection remains serial at the saved interval. Continuous visible
animation still requires CPU/GPU resources; this does not promise zero cost.

The first VS Code launch profile is now **X890 · LIVE monitor**. It runs the
checkout's apphost, never an empty snapshot. Elevation explicitly relaunches the
apphost even when a debugger initially hosts the DLL in dotnet.exe. The window
title distinguishes LIVE monitoring, saved preview and development builds.

## Actual evidence

- Visible VS Code Debug solution build and managed collector contracts passed.
- Both JavaScript entrypoints passed syntax checks. The native bundle checker
  verified all 21 models, 44 files, exact hashes, full triangle counts and maps.
- The actual LIVE profile opened
  `SensorPanel/bin/Debug/net10.0-windows/SensorPanel.exe`, PID 31104, with
  `preview=false` and elevation confirmed by its runtime receipt.
- A native window observation showed live Ryzen 9 9950X3D, RTX 5090, motherboard,
  fan, RAM-clock and network readings alongside the restored ship/solar design.
- `artifacts/live-runtime-check.json` passed: 18 collection cycles, 143 available
  readings, 1000 ms polling and fresh timestamps. The displayed Anubis renderer
  checked its exact catalogue hash, 2,754 triangles, all expected maps, shadows
  and AO; no geometry reduction or texture resampling. Anubis remains explicitly
  labelled an earlier fan study in the current catalogue.
- Its 15-second monitor-process sample was 1.148% machine CPU, 570.1 MiB working
  set and 653.6 MiB private memory. That excludes WebView2 children and GPU/VRAM;
  it is not the rejected empty-preview measurement or a sustained upper bound.
- A subsequent visible VS Code process-tree sample included nine processes,
  including WebView2 and the FPS helper. Over 15.08 seconds it measured 2.364%
  machine CPU, 1000.6 MiB summed working set and 961.7 MiB private memory.
  Working sets can double-count shared pages. GPU/VRAM were not measured.
  There were zero separate Fleet Studio/server processes. This is an active
  animated-scene sample, not minimized performance or a promise of low cost.

After the settings rebuild, a new actual LIVE launch from VS Code opened PID
40824. Its native window now visibly shows Pella beside fresh hardware readings.
Its runtime receipt passed with 143 available readings and confirmed Pella v26
(1,023,503 triangles, source SHA-256
`2a6126d5a1570607f8687fc671f85e8afdea04f624fd05ff737fc6021be53ff0`)
with all expected PBR maps, shadows and AO, no geometry reduction or texture
resampling. The prior Anubis/Pella observation mismatch is resolved by this new
capture and renderer receipt. The current native catalogue also contains
Guanshiyin v11 (98,140 triangles, source SHA-256
`6c01a52e3cc53412eb3d7f4344b6cf0a4029609b2e2e6f18a061fe4548f5ace4`);
both match their Fleet Studio catalogue source hashes. Windows elevation still
prevents the current UI connector from operating live controls.
Guanshiyin live interactions, owner acceptance, installed-package changes and release remain
separate until actually observed. The latest asset/model work predating this fix
remains protected in the working checkout.

## Simpler settings

The owner rejected the long scrolling settings menu. Its main page now has six
entries: Display, Theme, Sensors, Advanced, Hide to tray and Close panel.
Display contains placement/fit/layout/scaling; Sensors contains FPS/polling.
Advanced holds occasional controls. Ship rotation and showcase controls moved
to the local ellipsis button beside the ship selector. All existing commands
remain available; no ship assets, collector logic or saved preferences changed.
The panel uses a solid themed background instead of a live blurred backdrop.
The focused Debug build passed visibly in VS Code and the actual live app
reopened. The owner opened its gear menu; a native capture showed all six entries
inside a compact panel without a scrollbar, beside Pella and current live readings.
Individual submenu actions were not exercised in that capture.

## Windowed Debug performance — 2026-10-19

The owner corrected an initial wrong-ship run; those earlier values are excluded.
The completed owner repeat at 08:51:56–08:52:28 America/Toronto used live Pella
in PID 40824. The native window subsequently showed Pella with current readings.
The sample includes all nine processes: monitor/collector, WebView2 children,
PresentMon and its console host. Each state has 30 one-second counter samples
(about 33 seconds elapsed because of collection overhead).

| State | Mean machine CPU | Summed working set | Busiest GPU engine |
| --- | ---: | ---: | ---: |
| Pella visible, windowed | 0.908% | 1,265.790 MiB average | 11.570% average |
| Owner-minimized Pella | 0.121% | 655.673 MiB average; 605.655 MiB in last ten samples | 0% in all 30 samples |

Minimizing reduced measured CPU by 86.7%; graphics activity stopped within this
process tree. Physical residency trimmed during the minimized interval, ending
at 616.375 MiB. Private committed bytes remained about 1,519.807 MiB on average;
that is not physical resident RAM. The collector continued publishing fresh
143-reading snapshots while the app remained minimized. Fullscreen and windowed
costs are distinct; a visible fullscreen panel remains active even without a
taskbar button. Minimizing VS Code or a launcher does not minimize that panel.

RAM working sets can double-count shared pages. GPU usage is the busiest
hardware engine after summing this tree's contributions to each engine, following
[Microsoft's GPU accounting explanation](https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/).
Per-process dedicated-memory sums reported over 30 GiB while NVIDIA's whole-board
query reported 3,166 MiB used. These unvalidated GPU-memory sums are excluded from
VRAM conclusions; shared allocations and Windows counter limitations apply.
Desktop compositor/kernel/driver work outside the process tree is excluded.

Raw owner-repeat evidence is under `artifacts/performance/pella-visible-20261019-085156-owner.*`;
the minimized sample is `pella-minimized-20261019-085417-701-5728.*`.
`tools/Measure-MonitorResources.ps1` is a read-only sampler, with stable process
identity checks, Pella receipt checks and unique output names. It changes no
models or app settings. A second agent-run sample stopped when its status receipt
could not confirm Pella; it is not counted in the table.

A Release build from the same checkout passed with zero warnings/errors. Its
fullscreen measurement remains separate until the current instance is replaced
and the actual sensor-display case is measured. No installer was run.

## Native ship bitmap sizing, 2026-10-19

The owner reported both native ship views looked less clean than the browser
comparison. A native saved-data diagnostic reproduced an actual raster-size
mismatch under the monitoring Viewbox. At a 1920x1080 review window, the Pella
portrait canvas was 236x544 while its displayed rectangle was about 342x787;
the holotable canvas was 1264x522 for about 1828x756 displayed pixels. Existing
MSAA and unmodified source meshes do not prevent enlargement of that bitmap.
This establishes a sizing defect; it does not prove the lighting changes caused
the owner's reported difference from the previous installed version.

`FleetViewport` now gives the WebView a raster surface sized for the ancestor
scale, applying the inverse transform within the same logical layout rectangle.
A Canvas prevents its larger desired size from changing the monitoring layout.
Window size/DPI and existing reparenting events refresh sizing; no continuous
poll or second viewport was added. Renderer JS, latest models, textures,
lighting, shadow/AO settings, 4x MSAA and accepted rotation/capture timing retain
their exact pre-change hashes/settings. Fleet Studio is unchanged.

`artifacts/Check-NativePixels.ps1` ran visibly in X890 VS Code before and after
the fix. Both portrait and holotable were checked at 1024x576 and 1920x1080.
The larger corrected canvases were 342x787 and 1828x757, matching the physical
rectangles and captured WPF image sizes. Native captures show the scene; the
explicit diagnostic closes itself. Before evidence is in
`artifacts/native-pixel-before`, after evidence in `artifacts/native-pixel-check`.
These are native saved-data sizing checks, not a fresh LIVE hardware or full
process-tree performance benchmark. In particular, larger pixel counts need
their own performance evidence; earlier window-size results are not promoted.

The 1.8.7 installer built visibly in the same terminal. Collector/native ABI,
published resources/mappings and all 44 fleet file hashes passed. Setup and
published executable versions and the Desktop copy checksum were verified.
SHA-256: `F545BF3262962FC2814B7B1A2EC3AA1221944252BA0E05E1457E90B932F50B5F`.
The owner will install `X890 Sensor 1.8.7 Setup.exe` for visual review.
No installation, public release or startup change was performed by this pass.

Owner follow-up, 2026-10-19: Lennon confirmed the installed 1.8.7 image-quality
fix. This is owner-reported installed visual acceptance. A new full process-tree
performance benchmark remains separate. The owner deleted the Desktop installer
copies. Future test installers/checksums stay in `artifacts/installer`; no rebuild
is requested before the next release.

## Expanse theme cycling, 2026-10-19

The owner reported that The Expanse no longer cycled the ship themes and that
its timing controls were missing. Current preferences had `ExpanseCycle=false`
and a 30-second interval. Explicitly choosing The Expanse retained that old hold;
the controls existed only behind the small Ship options button. The old scene
switch updated the model/media but retained one fixed Expanse console palette.

Explicit selection now resumes cycling without changing the saved interval.
Cycle ship themes and Change ship every appear immediately below the main theme
selector while The Expanse is selected, and remain available in Ship options.
Each scene now uses that ship's existing full console styling, including the
accepted Pella and Rocinante styles. Readings, histories, maxima, the solar view
and shared model viewport remain shared. Manual selection still pauses; the
visible toggle resumes. Existing minimized/settings/holotable guards remain.

`& .\artifacts\Check-ExpanseCycle.ps1` ran visibly in X890 VS Code. The first
run reproduced the missing main timing control; its failure is preserved in
`artifacts/expanse-cycle-before.json`. The corrected Release development build
and native saved-data diagnostic passed: 21 matching ship/theme routes, timing
selection, manual hold/resume, minimize/restore and a real 15-second automatic
advance. Sensor values/maxima and binding checks passed. Screenshots and receipt
are in `artifacts/expanse-cycle-check`. The review window closed itself.
All 23 protected model/renderer hashes in `artifacts/expanse-cycle-protected.json`
remain unchanged, including FleetViewport and viewer.js. No installer, installed
app replacement, LIVE hardware validation or new performance measurement ran.

Owner follow-up, 2026-10-19: Lennon reported "confirmed cycling now".
Record cycling as owner-confirmed working. This confirmation does not identify
the executable/build or separately confirm the revised timing controls; it does
not establish installation of the development correction. No additional build
or installer is requested. Preserve the accepted designs, models and rotation.
