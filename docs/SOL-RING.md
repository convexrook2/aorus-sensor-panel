# Sol Ring native showcase

The monitoring selector, theme and Expanse cycle include `SolRing`. It focuses
the `SolGate` navigation landmark without replacing the solar scene.
Its monitor uses the accepted Pella/Rocinante section positions and left
showcase, with the existing grid/space backdrop and instruments. The passive
holotable reuses the same `FleetViewport`, normal capture cadence and rotation
timing. Existing ship geometry and renderer settings are retained.

The full v3 fan reconstruction comes from
`FleetStudio/models/SolRing-463844ce94fe.glb.gz`, not the schematic solar marker.
It has 282,272 triangles, seven materials, 16 source lights and no image maps.
Decoded SHA-256:
`c80193dd8327d6bda2a55664f34617b2910130f6e961ef772dc23293599283d1`.
Compressed SHA-256:
`7152881b089c5ceecede9d6adf5d1605d9048378f73b1a4f6d08d997c94db505`.
The copy is lossless; no mesh, source materials or original Studio files change.
Credits and the existing reference link accompany the native catalogue.

The gate preserves its source XY orientation rather than aligning its widest
axis like a ship. Only the named `Active aperture` mesh receives the same
`portal.js` display shader used in Fleet Studio; the source asset is unchanged.
It is two-sided, casts no solid shadow, advances on the shared animation clock,
and stops with disabled motion or minimized/inactive rendering. Switching to a
ship disposes it with the previous model. No new settings or background process
is added. Full shading, MSAA, AO and the shared pixel sizing remain enabled.

`--check-sol-ring` is an explicit saved-data native diagnostic used with
`--preview tools/preview-empty.json --windowed --preview-theme SolRing`. It
checks monitor/holotable rendering, source hash/count, upright orientation,
two-sided aperture, animation, motion-off, minimize/restore and switching back
through Pella/Rocinante. It writes `artifacts/sol-ring-native-check` and closes.
Saved-data, protected publish, live hardware, installed copy and release evidence
remain separate. No installer is requested for this integration.

## October 23 map candidate

The native solar map now has its own adaptation of the same hash-pinned v3
geometry. A distant gate uses one small glyph; focus loads all 282,272 original
triangles and reveals the layered rim and two-sided animated aperture. The map
uses native WPF materials/lighting and a CPU texture approximation of `portal.js`,
separate from the showcase's full PBR shader and sixteen source spill lights.
Its upright plane follows map yaw/tilt instead of decorative station wobble.
The illustrative 1,000 km diameter uses the map's uniform true-scale conversion.
The original GLB, full showcase and earlier exported marker mesh are unchanged.

The explicit `--check-true-scale` saved-data review records both gate faces,
compact views, source identity, animation and stopped paused/hidden states.
Candidate evidence is under `artifacts/solar-true-scale-20261023`; it does not
update the installed 1.8.11 Sensor or establish LIVE/resource acceptance.

## Validation on 2026-10-20

The scoped import ran visibly in X890 VS Code and retained all 21 previous
catalogue rows and exact model hashes. Both source and encrypted-publish native
checks passed, with captures in `artifacts/sol-ring-review-20261020/source` and
`protected`. The gate rendered in monitor and holotable views; its shader time
advanced with motion, stopped while disabled/minimized, and restored normally.
Switches through Pella, Rocinante and back retained their model/hash/map checks
and did not retain a gate aperture on either ship. Captures were inspected.

Release build, managed collector contracts, native mapping/resources, 11 RLAR
archive contracts, all 50 navigation cases, npm audit and dependency freshness
passed locally. Protected monitor publication verified 479 exact source assets;
separate Studio remained at 57. The final Jenkins-wrapper unchanged-assets gate
failed because this working tree intentionally contains the new gate assets and
renderer changes. That guard was retained. This is not an exact-commit Jenkins
pass; Jenkins build #3 still covers the preceding committed source only.

No installer, installation, public deployment or new commit is part of these
checks. Live sensor and owner visual acceptance are separate from saved-data
rendering and protected-publication evidence.

The subsequent elevated LIVE development build passed its runtime check with
Sol Ring, 25 collection cycles and 143 available readings. Its separate native
holotable diagnostic passed initial centring, rotation, reopen, landscape and
portrait resize, minimize/restore and stopped minimized rendering. Receipts and
captures are in `artifacts/sol-ring-review-20261020/live`. The exact existing
installed monitor path was verified by the elevated launch helper before that
running instance was stopped; its installation was not replaced. The development
review app is left open. This is live runtime evidence, not owner visual
acceptance, a new whole-process resource benchmark or installed-release proof.
