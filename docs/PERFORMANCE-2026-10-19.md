# Pella and Rocinante resource checks - 19 October 2026

LIVE monitoring in the visible X890 VS Code terminal. These are local Release
builds, not an installed-release or installer test. No design, model, texture,
shading, resolution or smooth-rotation timing was changed for measurement.

Hardware: AMD Ryzen 9 9950X3D (32 logical processors), NVIDIA RTX 5090, with AMD
integrated graphics also present. The renderer identified the RTX 5090. NVIDIA
Windows driver version: 32.0.16.1742; AMD: 32.0.21045.5002.

Windows review size: 1920 x 1080. Each run has 30 samples after 8 seconds settling;
CIM/counter work makes elapsed time longer than the requested sample count.
Minimized state is checked with HWND-to-PID verification and IsIconic, rather
than interpreting a cached app flag. The minimized 160 x 28 off-screen window
rectangle is Windows' minimized placeholder, not a lower-resolution ship render.

Scope: the exact monitor PID and stable descendants, including six WebView2
processes, PresentMon and its console helper. Shared kernel/compositor/driver
work outside the process tree is excluded. LMU, Rookline and SteamVR were also
open; this is not an isolated-machine or repeatability benchmark.

## Before protected packaging

Mean values. RAM below is the sum of private resident working sets, excluding
shared resident pages. Working-set sums and private committed bytes are recorded
separately in the raw receipts; neither is interchangeable with private RAM.
GPU is the busiest hardware engine across the app tree, not a sum of all engines.

| Ship | View/state | CPU, whole machine | Private RAM MiB | GPU engine | Elapsed s |
|---|---|---:|---:|---:|---:|
| Pella | Monitor / Visible | 4.74% | 627.3 | 10.70% | 53.00 |
| Pella | Holotable / Visible | 1.80% | 618.9 | 7.93% | 52.87 |
| Pella | Holotable / Minimized | 0.20% | 456.3 | 0.00% | 52.70 |
| Rocinante | Monitor / Visible | 2.56% | 658.7 | 9.52% | 52.82 |
| Rocinante | Holotable / Visible | 1.81% | 620.6 | 9.01% | 51.52 |
| Rocinante | Holotable / Minimized | 0.18% | 463.8 | 0.00% | 50.26 |

Both minimized samples recorded zero app-tree GPU activity. The app's explicit
lifecycle receipt also verifies rendering inactive while minimized and frames
advancing after restore, without changing the accepted animation timing.
Live collection continues while visual rendering is stopped.

The Rocinante minimized run reported invalid counters for unrelated PIDs 55768,
72392 and 78832 as other applications changed processes. None belongs to its
nine-process monitored tree. Those invalid rows were excluded; all tracked
identities remained stable. The sampler now fails if any tracked PID has an
invalid GPU counter. Raw warnings remain in the receipt.

Per-process GPU memory counter sums exceeded whole-adapter allocation in earlier
checks. They remain diagnostic-only: no exclusive-VRAM claim is made. Whole-board
memory includes unrelated applications. The zero GPU result is an engine activity
measurement, not proof the model's GPU memory has been completely released.

Pella's initial monitoring CPU mean was higher than Rocinante's. These short,
sequential measurements do not isolate a causal ship-to-ship cost difference.
The ordinary monitoring screen also animates instruments/solar content that the
holotable covers; a large single-ship view can cost less than the full monitor.

## Asset and lifecycle evidence

Pella: 1,023,503 triangles; compressed native model SHA-256
`9C7A4F0AA6CFDEB487CFD29F4000FCAEAD2C6C067FC7214FA6D2AE8437115D5F`.
Rocinante Season 6: 687,992 triangles; compressed SHA-256
`F7195068905AC8103100F0C9D9CEA29DE668DC9642823652F24AF7E26BC04092`.
Shared viewer JavaScript SHA-256
`9170CD5DB2A465A29EF570D6DA73585FFD0351B8E20024FA278D8B51096AB1A0`.

Raw baseline receipts and per-process/per-engine CSV companions:
- `artifacts/performance/pella-holotable-minimized-20261019-192242-289-52724.json`
- `artifacts/performance/pella-holotable-visible-20261019-192131-495-52724.json`
- `artifacts/performance/pella-monitor-visible-20261019-192020-517-52724.json`
- `artifacts/performance/rocinante-holotable-minimized-20261019-193341-098-52724.json`
- `artifacts/performance/rocinante-holotable-visible-20261019-193232-758-52724.json`
- `artifacts/performance/rocinante-monitor-visible-20261019-192354-259-52724.json`

The initial Rocinante holotable attempt was interrupted when the live monitor
closed; it is excluded. Its successful repeat and minimized run used the same
unchanged measurement binary. `artifacts/fleet-pair-lifecycle.json` records the
later explicit restore check; subsequent protected runs replace that current
receipt, so the completed baseline receipt is preserved separately below.

## Protected publish

`tools/Validate-ProtectedAssets.ps1` passed with build logs and exact encrypted
asset/source parity in `artifacts/rl-publish-20261019-194954-523`. It exercised
11 authenticated archive round-trip/rejection/allocation checks. The published monitor's
477 protected files and Fleet Studio's 57 protected files contain the unchanged
source bytes, with no loose protected asset folders or build key/source/PDB files.

Visible native saved-data checks passed for Pella and Rocinante in both monitor
and holotable views, including advancing rendering and full-size pixel checks.
Fleet Studio independently passed its full-geometry/material/rotation checks.
These native publish checks are not fresh sensor or installed-package acceptance.
All review windows closed themselves. No installer was built or installed.


The final source/packaging pass also passed `git diff --check`, PowerShell parsing
of the changed packaging/measurement scripts, and encrypted publication under
`Package / win-x64`: `artifacts/rl-package-check-20261019-195029-367/result.json`.
That is a package configuration check, not an installer build or upgrade test.

## Excluded additional protected run

Four preliminary protected receipts from PID 61744 (Pella's three states and
Rocinante monitoring, timestamps 19:42-19:46) incorrectly included RadeonSoftware
and cncmd. RadeonSoftware started at 12:29, before the current WebView process
which later reused its parent's PID. This was a parent-PID reuse error, not a
process launched by X890. These receipts remain preserved for diagnosis and are
excluded from X890 conclusions. The sampler now requires every child to have a
creation time at least as late as its observed parent. A subsequent holotable
attempt stopped when the holotable closed; its incomplete data is also excluded.

The loader now decrypts each large entry in place, avoiding a second model-sized
managed allocation. Exact binary/text round trips, wrong-key/product rejection,
index/payload tamper rejection and the allocation bound all pass. This changes
loading work only; no render-frame pacing, model data or presentation settings
were changed. The final protected resource pass below uses this version and
corrected process chronology. Its 15 samples per state are a short live check,
with an 8-second settle delay and the actual elapsed time retained in each receipt.


## Corrected protected build - completed LIVE check

| Ship | View/state | CPU mean / peak | Private RAM MiB mean / peak | GPU mean / peak | Elapsed s |
|---|---|---:|---:|---:|---:|
| Pella | Monitor / Visible | 1.05 / 1.36% | 697.16 / 702.99 | 11.06 / 11.29% | 24.88 |
| Pella | Holotable / Visible | 2.20 / 3.50% | 685.31 / 694.13 | 7.84 / 7.97% | 24.55 |
| Pella | Holotable / Minimized | 0.13 / 0.24% | 587.18 / 654.07 | 0.00 / 0.00% | 24.33 |
| Rocinante | Monitor / Visible | 2.34 / 6.32% | 738.01 / 740.82 | 9.38 / 9.62% | 24.52 |
| Rocinante | Holotable / Visible | 1.83 / 2.58% | 659.54 / 662.97 | 7.28 / 7.46% | 24.38 |
| Rocinante | Holotable / Minimized | 0.14 / 0.21% | 581.93 / 627.14 | 0.00 / 0.00% | 24.27 |

These six results include WebView2 and FPS helpers, with nine verified processes
and 15 samples in each state. CPU is whole-machine percentage; GPU is the busiest
engine. Private RAM remains several hundred MiB while minimized: rendering stops
but the monitor retains state for restoration and continues live collection.

Separate memory measures (means, MiB; working-set sums can count shared pages more than once):

| Ship | View/state | Working-set sum | Private commit |
|---|---|---:|---:|
| Pella | Monitor / Visible | 1243.4 | 1616.9 |
| Pella | Holotable / Visible | 1234.3 | 1927.6 |
| Pella | Holotable / Minimized | 1049.6 | 1871.3 |
| Rocinante | Monitor / Visible | 1151.9 | 1470.3 |
| Rocinante | Holotable / Visible | 1040.1 | 1831.3 |
| Rocinante | Holotable / Minimized | 931.4 | 1790.2 |

Private resident RAM was higher than the earlier unencrypted baseline. Total
working-set sums were similar or lower in several states. This short, sequential
check does not isolate physical-memory overhead, and no claim is made that
encryption is free or that all shared memory is uniquely attributable. The
allocation test proves removal of the duplicate model-sized decrypt buffer.
CPU variation between runs is not evidence that encryption improved CPU use.

- `artifacts/performance/pella-monitor-visible-20261019-195107-301-52724.json`
  Counter warnings affect only unrelated PIDs: [8524, 16896, 33716, 40624, 64692, 78792]; tracked intersection: [].
- `artifacts/performance/pella-holotable-visible-20261019-195149-153-52724.json`
- `artifacts/performance/pella-holotable-minimized-20261019-195229-526-52724.json`
  Counter warnings affect only unrelated PIDs: [76148]; tracked intersection: [].
- `artifacts/performance/rocinante-monitor-visible-20261019-195311-903-52724.json`
- `artifacts/performance/rocinante-holotable-visible-20261019-195352-338-52724.json`
- `artifacts/performance/rocinante-holotable-minimized-20261019-195432-683-52724.json`

Measured runtime DLL SHA-256: `37A67B0B92C2B5B6CF397643E90AA04ACBEC55FA8461C7CE98CEEBD0B3483A3F`.
Measured encrypted archive SHA-256: `21778769A27452887E4F4EE7915AEC958D39819EF6226D6F3CF63E635A07E4A0`.

`artifacts/performance/protected-pair-lifecycle-20261019.json` confirms both
renderers inactive on minimize and advancing after restore. Native pixel checks
also confirm full-size presentation; accepted JavaScript/model hashes are unchanged.
No long-duration leak, installed-release or exclusive-VRAM claim is made.


## Distribution audit and task boundary

`artifacts/rl-distribution-audit.json` passed. The distribution audit republished
both apps with `Private=false` on the build-tool reference, preventing the
asset packer's apphost/config files from being copied into the apps. No `RlPack*`
files remain in the verified publish folders. Their native rendering checks
passed again, and the monitor DLL SHA-256 is identical to the measured runtime.
The latest native publish proof is
`artifacts/rl-publish-20261019-195607-923/result.json`; the subsequent
`Package/win-x64` check also passed (see the newest `rl-package-check-*` receipt).
The salted archive hash changes on repack; every decrypted source hash was
verified again. No model/render JavaScript source bytes changed.

Both review apps exited; the visible VS Code validation output is retained.
The existing installed monitor and installed Fleet Studio copy were not changed.
No installer, installation, push or new commit occurred. The working changes
remain on main after checkpoint `2d2c7f2cbbc325b39509b6d6e15c180e310634e8`.
The next explicitly authorized release still needs an installer/upgrade round trip.
