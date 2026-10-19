# Pella monitoring console

The owner selected the release monitoring layout with Pella styling and a grid
background for a live VS Code review. After the updated live window opened,
Lennon responded "NICE" on 2026-10-19, accepting this visual direction.

## Presentation

- Preserve the release CPU/GPU temperature dials, clock rates, power, voltage,
  maxima, memory, network, uptime and board/cooling instruments. They bind to
  the existing collector presentation; there is no simulated telemetry.
- Keep the current full Pella v26 model as the focus of the left showcase.
  Landscape layouts give that panel more width. The same full-quality viewport
  is reused by the holotable. Geometry, textures, shadows and AO are unchanged.
- Keep the existing interactive solar system visible in the centre, with its
  navigation controls. It is no longer hidden behind a Sol Map button.
- Place a static grid over the existing Expanse backdrop. The frozen grid
  drawing is rebuilt only on resize; it adds no timer, WebView or collector.
  Existing minimize/hidden rendering behaviour is retained.
- Preserve local background/media support. The current local media manifest
  contains no Pella-specific photo, so Pella uses the built-in stars/planet
  backdrop. An unrelated ship's photo is not substituted.
- Use the Pella palette and instrument typography, with pale readable values.
  The scope walls, reticle and oversized single-number presentation are removed.

The earlier numeric-only and scope-wall implementations were rejected. The
S6E3 Pella control frame supplied wall-angle evidence for those studies, not a
complete monitoring layout. Broader screen-reference research is still pending.

## Run and review

Use the visible VS Code integrated PowerShell terminal:

```powershell
dotnet build .\SensorPanel\SensorPanel.csproj -c Release --no-restore
& .\SensorPanel\bin\Release\net10.0-windows\SensorPanel.exe --check-mapping
& .\SensorPanel\bin\Release\net10.0-windows\SensorPanel.exe --windowed --theme Pella --runtime-check
```

`--theme` selects the initial theme for this run without changing the saved
theme. Windows elevation is required for normal hardware collection. The live
window and fresh `artifacts/live-runtime-check.json` establish runtime evidence;
saved-data studies and a build alone do not. Any recorded CPU/RAM sample in
that receipt covers the monitor process only, excluding WebView2/GPU resources.

The previous installed release is 1.8.5, source revision
`e74632859054500de3ead18d6ee6b1487d7d12c5`. This review runs a checkout build;
it does not update the installed package. Fleet Studio and model assets remain
separate. Installation and full resource checks remain open.

## Live review receipt (2026-10-19)

`artifacts/Validate-PellaConsole.ps1` ran from the visible VS Code terminal.
The verified earlier development instance (PID 29908) was closed after owner
approval of Windows elevation. Release build and mapping/layout checks passed.
The updated live Release window (PID 40428) opened with the dials, clock/power
instruments, visible solar system, grid/background and enlarged left model.

The fresh runtime receipt completed at 11:24:52 Toronto time: 143 available
readings, 1000 ms polling, Pella v26, 1,023,503 triangles, no geometry reduction
or texture resampling, with shadows and ambient occlusion enabled. Source SHA:
`2a6126d5a1570607f8687fc671f85e8afdea04f624fd05ff737fc6021be53ff0`.
This verifies the checkout's live rendering and collection; it does not update
the installed 1.8.5 package or establish total CPU/RAM/GPU resource usage.

## Passive holotable revision (2026-10-19)

The owner corrected the inspection direction: the holotable is a display
showcase, without orbit, zoom or reset controls. It reuses the full model,
centres its rotation pivot after source transforms, and gives it the full
central stage. A small dark plinth receives the ship's shadow; faint accent
rings stay on its surface. The existing stars/planet or local media background
remains behind it, with the Pella grid kept subtle. Six live readings sit below
the model. **BACK TO MONITOR** and **Escape** return to the normal console;
the normal dials and solar system remain there.

Automatic rotation uses the saved motion choice and Windows animation
preferences. Active holotable draws are capped at 30 fps, without reducing
geometry, maps, pixel ratio, shadows or AO. The same WebView is reused;
minimized rendering stops while sensor collection continues.

The visible VS Code command `& .\artifacts\Validate-PellaShowcase.ps1` built
`artifacts/pella-showcase-review/SensorPanel.exe`, passed mapping/layout checks
and verified all 21 native models / 44 bundle files. The elevated LIVE candidate
PID 23820 opened with `--windowed --theme Pella --runtime-check --check-holotable-live`.
It did not replace installed files or modify startup tasks.

Fresh native receipts:

- `artifacts/live-holotable-check.json`, completed 13:20:28 Toronto: passed
  first-open and reopen without reset, landscape/portrait resizing, restoration,
  automatic rotation (26.98 rendered fps over three seconds), and minimized
  inactivity (no frame increase during the 0.9-second observation).
- `artifacts/live-runtime-check.json`, completed 13:20:43: elevated real
  collection, 143 available readings, 1000 ms polling, Pella v26 / 1,023,503
  triangles, identical source SHA above, full maps/shadows/AO.
- `artifacts/live-holotable-layout.png` captures the actual WPF composition.
  The check opens and resizes programmatically in the visible native app;
  it is not evidence of manual touch input or owner visual acceptance.

The accompanying 15-second sample reports 1.327% CPU, 772.4 MiB working set and
690.2 MiB private memory for the monitor process only, following the resize
checks. WebView2 children and GPU/VRAM are excluded. These are development review
measurements, not a total resource budget or a release/installation result.
Fleet Studio remains separate and was not changed by this revision.

## Complete monitor process-tree sample (2026-10-19)

Owner-visible VS Code sampling covered the same live Release candidate PID
23820: the monitor, six WebView2 processes, PresentMon and its console host.
The Pella holotable was rotating at its existing 1024×576 window size; models,
quality, rendering configuration and sensor settings were not changed.

| Average | Rotating | Minimized |
| --- | ---: | ---: |
| CPU, percentage of the whole machine | 1.954% | 0.089% |
| Private resident RAM | 671.4 MiB | 446.9 MiB |
| Working-set sum, including shared pages | 1271.5 MiB | 821.0 MiB |
| Private committed memory, not physical RAM | 1635.6 MiB | 1611.9 MiB |
| GPU busiest engine, 3D | 10.150% | 0.000% |

Each state has 30 valid samples spanning about 46 seconds, plus eight seconds
of settling. Counter/CIM collection makes the actual spacing longer than one
second. Peak CPU was 2.348% rotating / 0.253% minimized; peak GPU was 10.806% /
0%. The nine process identities remained stable; no counter errors or warnings
were recorded in either completed sample. CPU fell about 95.4%; private
resident RAM fell about 33.4%. Minimized collection remained live: the persisted
sensor snapshot timestamp 13:39:48 falls within the native minimized interval
13:39:13–13:40:03. The showcase was restored afterward and its full model was
still visible; the compressed Pella SHA remained `9c7a4f0aa6cfdeb487cfd29f4000fcaead2c6c067fc7214fa6d2ae8437115d5f`.

Raw receipts and matching sample/process/engine CSVs:

- `artifacts/performance/pella-visible-20261019-133108-755-37208.json`
- `artifacts/performance/pella-minimized-20261019-133905-564-37208.json`

The app's saved display receipt does not update on minimize. The minimized
sampler instead reads Win32 `IsIconic` and `IsWindowVisible`, verifies window
ownership against the exact monitor PID, and rejects a state change on every
sample. It does not control the window. Private resident memory comes from
Windows' PID-filtered `WorkingSetPrivate` data; the initial PDH-name attempts
failed and did not create result receipts. The newer Process V2 provider also
returned an error on this host. No application rebuild was required.

The per-process dedicated-GPU-memory sums were impossible (about 57 GiB rotating
and 87 GiB minimized, versus about 3 GiB across the adapters for the entire
desktop). They remain in the raw files for diagnosis and are excluded from
conclusions. Exact app-exclusive VRAM is **unverified**. Microsoft documents
[incorrect process GPU-memory counters](https://learn.microsoft.com/troubleshoot/windows-client/performance/gpu-process-memory-counters-report-wrong-value);
that is a known limitation, not proof of the cause on this Windows build.
GPU utilization counters were valid. Desktop compositor, driver/kernel costs
outside the selected process tree, and unrelated applications are excluded.
Working-set sums can count shared pages repeatedly; private committed bytes
are allocations that need not remain resident. This is a short checkout-build
measurement, not an installed-release or long-duration soak test. Visible
rotation still has material graphics cost; minimizing stops measured GPU work
but retains memory. No visual, model or quality optimization was performed.

## Shared rendering review — cadence change rejected and reverted

The owner authorized shared render-work optimization after the complete
process-tree baseline above, with future ships following the same approach.
The v2 candidate passed syntax, Release build, mapping/layout, all 21-model /
44-file bundle checks, and its programmatic native lifecycle checks. However,
the owner rejected its visible rotation as much too choppy. Those checks did
not establish acceptable native presentation. Its resource sample was stopped
at 14/30 samples; no completed comparison or performance gain is claimed.

The capture-divider adapter and timer-based frame scheduler have been removed.
The previous animation-frame loop and normal WebView2 capture behavior are
restored, including the existing holotable drawing cap and 30-second revolution.
The exact latest model, textures, PBR pipeline, shadows, AO, native pixel ratio,
backgrounds, display stage and instruments are unchanged. A lower utilization
number cannot justify rejected motion.

The covered-work correction remains: the solar map, backdrop/crew and
instrument animations stay paused under an open holotable, including after
minimize/restore. Closing the holotable restores normal monitor animation.
Explicit diagnostic profiling and the opt-in resource handshake remain tools;
normal launches have no handshake polling or automatic window-state changes.

The rollback is validated through the visible VS Code terminal:

```powershell
& .\artifacts\Validate-PellaSmooth.ps1
```

This builds `artifacts/pella-smooth-review/SensorPanel.exe`, checks syntax,
mapping/layout and the bundle, then requests Windows hardware-access elevation.
The replacement helper verifies candidate v2 PID 61876 against its executable
path and creation time before closing it. The new live app performs brief
open/reopen/resize/minimize/restore checks and stays visible for motion review.
Native results and owner motion acceptance must be recorded separately.
No installation, startup task, Fleet Studio, commit, push or publication changes.

### Rollback validation receipt — 2026-10-19 14:11 Toronto

The visible VS Code run passed JS syntax, Release build, mapping/layout and
21-model / 44-file integrity checks. The elevated replacement verified and
closed rejected v2 PID 61876, then launched the corrected live build as PID
50572 from `artifacts/pella-smooth-review/SensorPanel.exe`. Both
`artifacts/live-holotable-check.json` and `artifacts/live-runtime-check.json`
passed for that PID. Native rotation diagnostics measured 26.67 rendered fps,
consistent with the earlier approximately 27 fps baseline; this remains a
renderer count rather than a measurement of presented-frame smoothness.
Pella remains 1,023,503 triangles with decoded SHA-256
`2a6126d5a1570607f8687fc671f85e8afdea04f624fd05ff737fc6021be53ff0`.
Full maps, shadows and AO passed. Only one SensorPanel process remained.
Fresh native captures showed the rotating stage, unchanged background/grid,
and changing live hardware readings. The live app is left visible for owner
motion review. No new resource comparison was run after rollback; no savings
are claimed from the rejected candidate or transferred from the baseline.

### Full-tree measurement of accepted smooth build — 2026-10-19 14:28 Toronto

Owner accepted the restored rotation and requested current full-process-tree
measurements without changing it. Ran `artifacts/Measure-PellaSmooth.ps1` in the
visible VS Code terminal. After the owner approved Windows elevation, the helper
verified and closed PID 50572, then relaunched the SAME build with only its
explicit diagnostic handshake enabled as PID 62280. No rebuild or app source,
render cadence, design, model, texture, shadow, AO or resolution change occurred.

Thirty samples per state took 47.11 seconds rotating and 47.17 seconds minimized,
after 8 seconds settling each. Windows window ownership and minimized state
were checked throughout. The same nine processes remained stable: monitor,
six WebView2 processes, PresentMon and conhost. Hardware: Ryzen 9 9950X3D (16
cores / 32 logical processors), RTX 5090 and integrated AMD adapter; native
window 1024x576 at 96 DPI. Full Pella renderer diagnostics passed at 26.66 fps.

| Full process tree | Rotating average (peak) | Minimized average (peak) |
| --- | ---: | ---: |
| CPU, percent of whole machine | 1.549% (2.352%) | 0.130% (0.276%) |
| Private resident RAM | 599.307 MiB (608.754) | 449.480 MiB (564.938) |
| Working-set sum, shared pages may repeat | 1242.618 MiB (1250.691) | 902.825 MiB (1144.387) |
| Private committed memory | 1573.268 MiB (1583.234) | 1543.957 MiB (1549.387) |
| Busiest GPU engine | 6.685% (6.994%) | 0% (0%) |

The six WebView2 processes alone averaged 0.463% CPU / 219.3 MiB private
resident while rotating, and 0.002% CPU / 77.7 MiB minimized. These are already
included in the totals. GPU engine activity reached zero minimized; allocated
memory is retained. Private resident memory continued falling during the
minimized sample, so its average is not a long-term idle-memory estimate.
Per-process GPU-memory counters remain inconsistent and are excluded; exact
app-exclusive VRAM is unverified. Desktop compositor and kernel/driver costs
outside the selected tree are excluded. This is a short checkout-build check,
not an installed-package or long-duration soak claim.

Receipts are in `artifacts/performance/pella-smooth-comparison-20261019.json`,
`pella-visible-20261019-142634-750-37208.json` and
`pella-minimized-20261019-142734-661-37208.json`, with raw sample/process/engine
CSVs. Counter warnings/errors were empty in both runs. The lifecycle receipt
passed: renderer inactive minimized and active with advancing frames restored.
The live holotable and runtime checks passed with fresh readings. Before/after
hashes matched for SensorPanel.exe, SensorPanel.dll, viewer.js and Pella.glb.gz;
latest Pella remains 1,023,503 triangles and the same decoded SHA recorded above.
Only PID 62280 remained; Pella was left visible and rotating. Fleet Studio and
installed files were untouched. Tracker receives reporting evidence only.

### Owner acceptance — 2026-10-19

Lennon explicitly accepted the restored smooth Pella build and the reported
rotating/minimized full-process-tree resource checks ("accepted"). This closes
owner review for this bounded checkout-build pass. Preserve its rotation timing,
design, latest model and full shading for subsequent work; the rejected capture
throttling remains removed. Future ships follow the shared lifecycle rules and
need their own representative measurements. This acceptance does not constitute
an installation, published release or long-duration soak result.
