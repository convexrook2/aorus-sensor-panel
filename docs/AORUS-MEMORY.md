# AORUS memory correction

Owner scope, October 24, 2026: diagnose the installed panel's approximately
700 MB footprint, keep AORUS under 100 MB including in the tray, and preserve
every accepted ship. This source change is not yet an installed correction.
No installer, publication or installed-file replacement follows from validation.

## Completed LIVE check

The October 24, 2026 13:23 Toronto run **passed** with Le Mans Ultimate running.
All **11,745 AORUS samples** were below the strict **100,000,000-byte** limit,
including the FPS helper and other descendants. This measures private resident
RAM; shared resident pages and private committed memory are reported separately.

| Phase | Peak private resident bytes, whole app tree |
| --- | ---: |
| Startup, visible, restored, all eleven palettes/settings | 73,326,592 |
| Initial tray | 66,707,456 |
| Return from Expanse | 91,934,720 |
| Tray after Expanse | 88,551,424 |

The run lasted 371.078 seconds with a 30.957 ms average sampling interval and
366 live sensor collection cycles. The window remained 1024 by 576 on the
Ryzen 9 9950X3D / X870 AORUS ELITE WIFI7 ICE / RTX 5090 workstation with
66,155,397,120 bytes of usable physical RAM. Sixty-eight fresh FPS observations
reported 72.94–75.11 successful application presents per second from LMU
PID 32844, including every palette and both tray phases. These are application
present events, not confirmed display/scan-out FPS.

The optional solar scene was no longer reachable after returning to AORUS,
the ship renderer was released, and artwork ticks stopped in the tray while
sensor collection continued. Maximum production collection time was 7.730 ms.
All 174 accepted asset hashes remained unchanged after the run.

Exact tested package: `artifacts/aorus-memory-20261023/single-code-final`.
Hashes and the consolidated result are in `final-validation.json` beside it.
Raw `memory.json`, `samples.json`, `lifecycle.json`, `fps-observations.json` and
the final live sensor snapshot are in
`artifacts/aorus-memory-20261023/single-code-live-20261024-132351`.
This proves the bounded run, not an unsampled/future absolute memory ceiling.
The installed application has not been replaced.

## Findings

The installed heap retained the complete optional solar scene through the tray
icon, window and ShipPanel, including detailed geometry and decoded textures.
Stopping its animation and disposing only the ship WebView did not release that
scene. AORUS also retained every decoded animation frame. Repeated full-system
process/thread snapshots, desktop PresentMon CSV parsing and full Steam-cache
copies created additional temporary allocation. The hardware library kept
unused sensor histories even though the panel owns its own bounded graphs.

A later diagnostic heap contained 56 MB of managed objects but only about
6 MB remained reachable. On this 64 GB workstation, waiting for the default
collection budget allowed substantial dead allocation to remain resident.
The discarded 24 MiB hard-heap-limit experiment caused an allocation failure;
no hard heap limit remains in the source.

## Source behavior

- Build ShipPanel only for Expanse. Leaving it disposes its solar view,
  unregisters static/composition callbacks, clears image/model caches and
  collects retired resources after WPF has processed the transition.
- AORUS uses software WPF presentation, selected before window creation and
  also applied to additional WPF surfaces. Expanse restores normal hardware
  presentation. Its model, maps, shading and animation preferences are intact.
- Publish ReadyToRun code and disable tiered recompilation. This avoids keeping
  additional generated code/profiling allocations after optional graphics are
  released. It does not change rendering quality or animation limits. Identical
  binary/asset saved-data comparisons measured 69,410,816 bytes after Expanse
  with tiered compilation and 51,359,744 bytes without it. LIVE validation is
  still required; these comparisons had no sensor collection or FPS helper.
- Keep three full-resolution animation surfaces and one reusable compressed
  input buffer. Read lossless PNGs on demand through the same authenticated
  archive format. Decode the original RGBA8 rows directly into the existing
  pixel buffer, with two bounded scanlines on the stack; no per-frame GDI+
  bitmap or decoder cache remains. All 354 source frames and 104 unique pixel images are
  unchanged. The sequence index is generated and checked against their hashes.
- Reuse settings templates and discard closed settings controls. In AORUS,
  schedule a collection at dispatcher idle after 2 MB of allocation growth.
  Use generation 1 for a small heap; compact/decommit a heap above 12 MB and
  release freed native-heap caches. Also collect after transient settings/theme work. This has
  no heap cap and does not evict still-live pages from the working set. Record
  collection count and duration in native reviews. Expanse skips this policy.
- Enumerate qualifying game windows instead of taking a process/thread snapshot
  every sensor poll. Reject desktop FPS records before parsing their CSV fields.
  Start the external FPS capture only when a recognized game is running, and
  stop its ETW session when the last game exits. Desktop focus keeps capture
  running for an existing game. Cancellation interrupts even a quiet pipe by
  shutting down the owned helper off the sensor polling thread.
  Read only required Steam metadata, without copying the cache or building
  unused application configuration trees. Classify the four needed Steam key
  names into bytes rather than allocating its complete key-string table, and
  prepare installed path prefixes once per catalogue refresh. A read-only local
  comparison reduced cold discovery allocation from 2,375,488 to 449,928 bytes,
  and ten subsequent polls from 353,576 to 144,616 bytes. Disable unused Libre sensor history;
  current readings, panel peaks and its 60-sample graphs continue normally.

## Validation checkpoint

The full local source/build gates passed again on the dirty main checkout
after the PNG and catalogue changes, including sensor/game/FPS contracts,
50 navigation tests, exact animation pixels, archive authentication/rejection
and 512 protected monitor assets. Local protected checkpoint output:
`artifacts/ci-publish-SensorPanel-20261024-124459-375` includes the conditional
FPS helper and its passing lifecycle tests. The subsequent runtime-setting
change was built/protected successfully into the latest native-review candidate,
`artifacts/aorus-memory-20261023/single-code-final`.

The protected saved-data native review passed startup, visible, tray, restore
and every AORUS palette with settings and its popup open: **54,829,056 bytes
peak private resident RAM** in the final palette review. This is saved-data
evidence, not LIVE hardware acceptance.
The separate native experience review passed full model identity, uncapped
Full Fidelity, Efficient scheduling, covered-map stopping, theme persistence
and unchanged readings/peaks/preferences.

The return review waits for pending WebView2 creation before closing its
controller, then waits for the runtime processes reported by that controller's
environment to exit. This fixes a creation/disposal race found in repeated
native switching checks. Two consecutive protected native experience reviews
passed after the fix. WPF/heap retirement finishes before activating AORUS.
The sampler refreshes known children each sample so exited renderers are not
kept in a stale discovery snapshot. The retirement phase still belongs to the
old Expanse experience and remains separately reported above 100 MB.

The first six-minute final LIVE trial completed 365 hardware collection cycles.
All eleven AORUS palettes, settings popups and initial tray/restore phases
stayed below **92,602,368 bytes**, but the return from Expanse reached
**108,990,464 bytes** and failed the strict 100,000,000-byte whole-app-tree gate.
That failure remains in `artifacts/aorus-memory-20261023/final-live-20261024-111243`.
The next LIVE trial also failed the return gate at 110,592,000 bytes, despite
successful browser retirement. A saved-data post-return dump contained only
5,410,542 bytes of reachable managed objects; native/library and temporary
allocation still mattered. The subsequent 1 MB full-collection experiment
produced larger transient peaks and was discarded. The current 2 MB policy,
bounded PNG decoder and smaller discovery allocations proceeded to the later
LIVE measurements below. The protected native experience review passes, and all 104
unique source images match WPF Pbgra32 pixels exactly, covering all five PNG
filters and up to fourteen IDAT chunks per source image.
The October 24 12:31 LIVE run completed 363 sensor cycles in 367.996 seconds,
sampling every 31.105 ms on average at 1024 by 576. Ordinary AORUS phases peaked
at 91,402,240 bytes, but the return from Expanse reached 106,860,544 bytes and
failed; tray after return peaked at 92,545,024 bytes. The solar scene was no
longer reachable and the runtime had exited. Maximum production collection
time was 6.339 ms. Evidence remains in
`artifacts/aorus-memory-20261023/png-catalog-live-20261024-123106`.
The collector reported no recognized game, yet its always-running FPS helper
used about 7 MB and still supplied discarded desktop event lines. The next
candidate makes that helper conditional on game eligibility. Its lifecycle
contract tests cover initial idle, game arrival, retained capture, game exit,
rapid restart and cancellation during both active capture and idle waiting.
The October 24 13:01 LIVE run verified automatic capture on Le Mans Ultimate's
arrival and fresh application-present FPS through settings, Expanse and tray.
It completed 366 sensor cycles in 369.898 seconds. Normal AORUS peaked at
93,470,720 bytes, but return/tray after Expanse reached 111,185,920/104,980,480
bytes, so that candidate also failed. Its evidence is
`artifacts/aorus-memory-20261023/demand-live-20261024-130110`, including separate
FPS observations. The subsequent runtime comparison isolates the tiered-code
overhead; no earlier failed run is promoted to acceptance.
The candidate with tiered recompilation disabled passed the native experience
review (PID 69320), including unchanged full-model identity, Full Fidelity without
an artificial cap, Efficient cadence, holotable/covered-map lifecycle and owner
preference preservation. The October 24 13:14 elevation request did not start
the probe. The owner approved the 13:23 request, which completed the passing
LIVE run recorded at the top of this document.
Earlier LIVE trials and the discarded heap cap also remain in ignored artifacts.
No claim that AORUS stays below 100 MB in the installed app is made here.

See [the external probe](../tools/AorusMemoryProbe/README.md) for exact commands,
whole-process-tree scope, private/shared/committed distinctions and sampling
limits. A return-from-Expanse run must report the retirement phase separately.
Accepted assets are checked against the pre-edit 174-file hash receipt, in
addition to normal full fleet provenance checks. Do not publish heap dumps.
