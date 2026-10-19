# Desktop runtime receipt — 2026-10-19

Owner scope: make X890 runnable/viewable from VS Code with an efficient always-on
monitor and separate on-demand Fleet Studio. No ship/model work is authorized by
this change. Existing unrelated assets, model revisions/deletions and studies
remain outside the change.

## Implemented

- `X890.code-workspace` selects `X890.slnx` with exactly three real projects.
  Existing asset/visual validation tasks are preserved alongside build/run tasks.
- C#/.NET 10 WPF monitor and existing SensorCollector backend are retained.
  Default graphics are 2D: still artwork, bounded CPU/GPU histories, no embedded
  WebView2 or solar scene, no decoded animation sequence or automatic effects.
  Still-image generation does not retain mesh caches; only one still is cached.
  `--full-graphics` and existing explicit visual checks retain the original path.
- Independent WPF/WebView2 Fleet Studio loads bundled static files via a local
  virtual host. No SensorCollector dependency, Python process or HTTP listener.
  Rendering is event-driven, animation explicitly enabled and capped at 30 fps.
  Efficient quality avoids postprocessing and uses pixel ratio 1.
  Minimize disposes the browser/model; restore reloads ship/quality and resets
  camera/effects. Close exits the process. Nothing is installed at sign-in.
- Development builds make the optional native FrameView SDK bridge opt-in.
  Installer tooling explicitly keeps its existing bridge requirement. Managed
  collector checks explicitly report the skipped ABI; full checks still require it.

## Actual validation

All builds, checks and launches below ran visibly in VS Code.

- `dotnet build .\X890.slnx -c Debug`: all three projects passed.
- `SensorCollector.exe --self-test-managed`: passed sensor, voltage, DDR5 clock,
  automatic-game selection, PresentMon isolation/staleness and desktop QPC
  contracts. Optional FrameView native ABI explicitly skipped.
- `Get-Content .\FleetStudio\review.js -Raw | node --input-type=module --check`:
  passed (exit 0). Scoped staged Git whitespace validation accompanies the save step.
- F5 monitor profile: native saved-data window rendered, empty readings labelled
  as preview, still artwork and final 2D clock histories visible. This is not
  live sensor/hardware proof.
- F5 Fleet Studio: local Pella rendered; side-camera change, rotation toggle,
  full-shading selection and pause/idle checked. Rotation displayed about 27 fps.
  Minimize removed all six WebView2 children; restore reloaded the selected ship.
  Close exited normally. No geometry/acceptance claims follow from these checks.

Five-second Debug samples through `tools/Measure-DesktopResources.ps1`:

| Observed state | Processes | Machine CPU | Summed working set | Summed private |
| --- | ---: | ---: | ---: | ---: |
| Final 2D monitor, saved Rocinante snapshot | 1 | 0.01% | 220.0 MiB | 202.8 MiB |
| Fleet Studio, Pella loaded and idle | 7 | 0.00% rounded | 884.2 MiB | 851.8 MiB |
| Fleet Studio minimized | 1 | 0.00% rounded | 184.8 MiB | 151.0 MiB |

These are local interval measurements under a debugger, not upper bounds or a
claim of universally lowest resource use. Working-set sums include shared pages.
GPU/VRAM and sustained live polling were not measured. Large viewer assets still
cost memory while open; close Fleet Studio when finished.

## Remaining evidence

Live hardware launch requires the owner to accept the existing Windows elevation
prompt. Live readings, long-running collector cost, native FrameView ABI,
installer/installed upgrade, CI and release are not established by this receipt.
Ship development stays held until the owner accepts this runtime setup.
