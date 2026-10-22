# Sensor panel 1.8.11 - native and LIVE validation

Validated 2026-10-21/22 from the exact Jenkins #11 package for source 74eec450472d07666ce2a91d0aa7cf72cb96da4f. Installer SHA-256: 3afd64f1705ffa66aff69eb230f1b125940860ca5a07a180d37fce335601d1aa. Tested SensorPanel.dll SHA-256: ebb3da9cfdbe393f32aed060297384cef135b6700b46a456970b1b8c0adb80eb.

The 54 payload files were staged under artifacts/release-1.8.11/native-candidate and hash-verified against the installer manifest. No installation or release publication was performed. Diagnostics ran visibly with Codex commands. The owner's VS Code and other window placements were not changed.

The subsequent release-note refresh is documentation only. Functional code, monitoring collection and all model assets stay frozen. A fresh exact-SHA CI installer is required to embed the finalized notes; native receipts above remain tied to their actual tested binary hashes, rather than being relabelled as a later build.

## Passed native checks

- Experience Settings: AORUS Lite renderer unload, remembered per-experience themes, Full Fidelity uncapped scheduling, Efficient 30 fps ship / 10 Hz idle-solar budgets, full model identity, covered-map stopping, preference migration and unchanged owner settings. Controls passed 1280x800 and 480x800 checks.
- Showcase rotation: Pella, Rocinante and Anubis; direct pause/resume controls, exact held angle, shared full-quality holotable viewport, portrait/wide/landscape layouts, inactive stopping and disposal when leaving ship themes.
- Protected native assets/pixels: Pella and Rocinante use their exact full models. At the 1920x1080 check, canvas/capture was 1881x706 and physical viewport 1880x706; no surrounding scaling enlargement. Default holotables stopped drawing while idle.
- Sol Ring: upright source model, separate two-sided animated aperture, source hash/count, navigation association, pause, minimize/restore and switching back through Pella/Rocinante. Protected assets were enabled. Native captures were inspected.
- Solar: settled/paused stopping, transition scheduling, deterministic scene images and an actual native map drag. The drag registered two pointer updates; this is interaction evidence, not physical display FPS.

## Passed LIVE checks

Elevated collection returned 143 available readings at the preserved 1000 ms polling interval. Pella and Rocinante completed showcase, idle/rotating holotable, minimize/restore and shared-renderer-return handshakes in both rendering modes. Sol Ring's separate LIVE run recorded 27 collection cycles and passed centring, programmatic orbit/zoom, optional rotation, reopen, landscape/portrait resize and minimized rendering stop/restore. These are staged native/LIVE results, not an installed-upgrade test or a universal hardware compatibility claim.

Owner settings retained SHA-256 ab943c38b6b382e611c5ba498b1d07895b19c9ae1ce22f3dccc37217c7b50506. Source/research/models for the rejected Arboghast study remain STOPPED and NOT ADOPTED. The released legacy Arboghast remains unchanged; no builder or study work was resumed.

## Whole-process resource measurements

Hardware: AMD Ryzen 9 9950X3D (32 logical processors), NVIDIA GeForce RTX 5090, driver 32.0.16.2101; integrated AMD graphics also present. Review window 1920x1080. Each row has 30 samples after eight seconds settling; actual elapsed time is listed. The stable nine-process tree includes WebView2, the FPS helper and its console host. GPU is the busiest hardware-engine utilization across that tree, not a sum of unrelated engines.

| Mode | Ship | Window state | Average GPU % | Private resident MiB | Actual seconds |
| --- | --- | --- | ---: | ---: | ---: |
| FullFidelity | Pella | Minimized | 0.000 | 527.3 | 48.728 |
| FullFidelity | Pella | Visible | 17.856 | 707.2 | 49.603 |
| FullFidelity | Rocinante | Minimized | 0.000 | 529.5 | 48.084 |
| FullFidelity | Rocinante | Visible | 17.509 | 883.6 | 49.296 |
| Efficient | Pella | Minimized | 0.000 | 512.5 | 48.472 |
| Efficient | Pella | Visible | 4.049 | 691.0 | 48.382 |
| Efficient | Rocinante | Minimized | 0.000 | 559.4 | 46.858 |
| Efficient | Rocinante | Visible | 3.720 | 855.9 | 48.064 |

The <=2% Efficient-mode target is not met. Full Fidelity costs substantially more in these runs. Working-set sums can double-count shared pages; private committed bytes are not resident RAM. Per-process graphics-memory sums were explicitly excluded as unvalidated VRAM. Unrelated/global performance-counter warnings are retained in raw receipts; invalid tracked-PID counters cause failure. No physical-FPS or universal smoothness guarantee is inferred.

Early strict coverage attempts stopped on Chrome overlap; incomplete samples are retained as history and excluded from the 30-sample summaries. The owner subsequently explicitly accepted the small overlap and directed continuing. No further acceptance gate is imposed solely for that overlap. No other window was moved to obtain results.

Sol Ring's sustained GPU budget remains unmeasured. Its independent LIVE rotation/aperture/lifecycle checks passed; Pella/Rocinante numbers are not used as a gate-budget claim. Elevated native input did not establish an extra manual rotation-control test; the recorded programmatic LIVE check did verify optional rotation. The owner closed that completed elevated review.

## Receipts and remaining acceptance

Local raw JSON, per-process/engine CSVs and native captures are retained under artifacts/release-1.8.11/native-live-validation. The hashed native-live-result.json indexes all ten passed native/LIVE receipts and eight completed resource cases. Prior receipts and failed/partial attempts are retained separately.

Finalize the notes without changing functional code/assets, rebuild the exact-SHA CI installer and verify the refreshed payload before delivery. Public publication, installation/startup/upgrade and installed native/LIVE acceptance remain separate actions. No release tag or upload, installation, Fleet Studio deployment, new game-FPS verification or hardware compatibility expansion is included here.
