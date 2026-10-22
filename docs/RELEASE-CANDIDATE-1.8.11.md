# Sensor-panel 1.8.11 — local candidate preparation

Prepared October 21, 2026 by the current X890 continuity chat
`01a160a3-a390-7c02-99ce-4234fd53712d` at Lennon's request. The owner subsequently
authorized the bounded exclusion patch, version bump, release-task correction
and checks in Codex. The subsequent blocker-resolution pass updated Vite to
8.3.5 and preserved exact private research originals before making source paths
portable. **All five local check groups pass, including npm audit with zero
vulnerabilities; not committed,
published, installed or approved for release.** Current results are recorded in
[the check report](RELEASE-1.8.11-CHECKS.md).

Baseline: public v1.8.10, tag/HEAD
`d7724d70bf94dbd6bbedc1ecc296557e8d8c5abb`. Authenticated GitHub release listing
and remote tags were checked during preparation: 1.8.10 is the latest listed
release and no v1.8.11 tag was present. Current app/installer/readme versions are
now 1.8.11. The shared CI contract also requires the Fleet Studio source version
to match; that metadata was synchronized without installing/deploying Studio.

Existing `main` checkout only. Initial preparation state: index clean, 91 status
entries including extensive concurrent work. No branch/worktree or blanket
staging. The current dirty checkout is **not yet an eligible package source**.

## Proposed contents

Recommend one sensor-panel installer containing both runtime experiences.
Separate content-free Lite / Expanse installers are deferred.

| Included change | User-visible result | Current evidence and remaining limit |
| --- | --- | --- |
| AORUS Lite / The Expanse Experience selector | Lite releases active ship graphics and stops Expanse visuals; each experience remembers its theme | Prior owner-confirmed controls; saved-data native migration/settings/lifecycle receipt passed. Final candidate upgrade remains untested. |
| Full Fidelity / Efficient selection | Full Fidelity is the default, with no artificial ship/solar cap. Efficient uses 30 fps ship drawing and 10 Hz idle solar with the same model/shading quality | Prior saved-data receipt verifies mode selection and counters. It does not measure physical FPS or GPU use of the final Full Fidelity profile. |
| Rotating showcase with local PAUSE / RESUME | Saved rotation preference and held angle, independent of map/background motion | Prior scoped trial accepted. One full-quality viewport is reused between showcase and interactive holotable; leaving ship themes releases it. Final combined candidate still needs validation. |
| Covered/minimized and solar rendering efficiency | Covered map/crew/background activity stays stopped; static decorative layers are cached; off-screen solar meshes detach | Current source and prior bounded lifecycle/trial receipts. Sensor collection continues independently. No new hardware compatibility claim. |
| Sol Ring in monitoring | Gate theme, Expanse cycle, SolGate navigation association, upright full model and separate animated aperture | Prior source/protected saved-data and LIVE receipts; owner-reported visual pass is scoped to that development build. Exact candidate CI, package, installed acceptance and a representative gate resource run remain pending. |
| Ship selection ordering and prepared loading fallbacks | Ship-name ordering preserves canonical IDs and cycling order; model-bound portraits support loading | Current source; 22 portraits match model identities and image hashes. Portraits are fallbacks; normal showcase rotation stays restored. |

Supporting source includes preference migration, native checks, full-quality
viewport lifecycle, portrait export/validation, rendering/resource diagnostics,
gate import support and protected asset manifest updates. They grant no launch,
hardware or publication authority on their own.

An incidental `App.cs` change treats elevation cancellation (Win32 1223) as
ordinary cancellation. It shares the file with required portrait export routing;
retain it only after the candidate source review. It is not a newly validated
collector/FPS fix. No SensorCollector or FPS implementation change is proposed.

## Explicit exclusions and preserved baselines

- **Arboghast rejected study stays STOPPED and NOT ADOPTED.** Exclude its
  candidate, source/build scripts, isolated review content, renders/references
  and proposed palette/HUD/capture changes. No builder rerun or speculative fix.
- Keep the already released Arboghast model/theme baseline. Its decoded hash
  remains `d537f34c26e9e82c3f8f85e678b04d06ae867d04ea95437e1f7625ab71f4dd84`.
  This is exclusion of rejected new work, not removal of an existing ship.
- Knight stays parked “good for now”; no Knight model or research promotion.
  Canterbury, Charon and rejected Roci studies are outside this release.
- Preserve finished Rocinante, Anubis, Pella and Guanshiyin v11. All 21 released
  catalogue model identities remain unchanged; Sol Ring is the only new row.
- No Fleet Studio launch/install/deployment or legacy server/startup change.
  The dirty Studio manifest and two additional historical Anubis files are
  source-only unrelated work and are excluded from the proposed sensor slice.
- No separate Lite installer, model simplification, telemetry/FPS change,
  native window rearrangement or new performance guarantee.

## Completed source changes and remaining release gates

1. **Held source deltas excluded.** The rejected Arboghast palette/ConsoleStyle,
   frames, metric-colour change and explicit-capture 180 ms wait/render yield
   were removed from `PanelTheme.cs`, `ShipPanel.cs` and `PanelWindow.cs`.
   The reviewable removal patch is
   [exclude-held-arboghast.patch](release-preparation-1.8.11/exclude-held-arboghast.patch).
   It was generated against the saved pre-slice copies and is now **applied**
   under Lennon's explicit direction. All three files match their protected
   pre-Arboghast text; earlier unrelated changes remain intact. The rejected
   model/source/review files remain preserved and stopped.
2. **Agree and freeze the source slice.** Review
   [scope.json](release-preparation-1.8.11/scope.json), especially mixed files,
   incidental cancellation handling and dev-only diagnostics. Keep rejected
   source/research outside the candidate without deleting it. No stash/reset/
   clean/blanket-stage. Exact release commit and source cleanliness unresolved.
3. **Version synchronization complete.** `SensorPanel.csproj`,
   `FleetStudio.Desktop.csproj`, `Packaging/SensorPanel.iss` and
   `Packaging/README.txt` agree on 1.8.11. Draft
   [release notes](../Packaging/ReleaseNotes-1.8.11.md) remain blocked from
   publication until their claims and release approval are final.
4. **Release task corrected.** The default task checks locally and cannot
   publish/install. A separate CI-candidate review task asks for an exact pushed
   SHA and fresh CI build number, with no fixed build #9. The completion script
   defaults to `Review`; `Publish` returns without installing, and `Install`
   verifies an existing public release without creating one. Version comes
   from the requested commit, titles are generic and draft notes block Publish.
   Six isolated safety cases passed with mocked external/native actions.
5. **Validate the final bytes.** Earlier receipts are not proof of the new
   combined candidate after exclusion/version edits. Run source/build/contracts,
   model/portrait and RLAR checks, saved-data experience/showcase/holotable/gate
   checks, and the agreed native/LIVE acceptance. Preserve normal capture cadence
   and full model quality. Local Release build, assets, managed/mapping and
   archive checks and all five full-wrapper groups passed; final native/LIVE limits remain in the
   check report. No live monitor or review window was launched.
6. **Exact pushed-SHA CI and package.** After applicable scoped Git authority,
   use `X890-CI` with the full pushed `EXPECTED_SHA` and explicitly requested
   installer build. Require matching requested/checkout/workspace SHA, SUCCESS,
   correct version, package manifest and checksums. Local checks now freeze
   asset paths, sizes and hashes before/after, preserving all 783 inputs.
   Jenkins requires clean tracked and untracked source at entry and exit.
   The passing dirty working-tree run is distinct from exact-commit Jenkins.
7. **Review package and upgrade separately.** Keep artifacts under
   `artifacts/installer`; no Desktop staging. Check protected assets, preserved
   model identities and absent rejected content. Public tag/upload/download
   parity, in-place upgrade/startup/preferences and installed native/LIVE checks
   are later gates, each with applicable owner authority.

## Performance decision

The older Efficient trial measured Pella 4.058% / Rocinante 3.550% average
busiest GPU engine across the whole app tree with ship and solar moving.
The <=2% target was not met. New Full Fidelity profile GPU is unmeasured.
No <=2%, physical-FPS, universal smoothness or VRAM guarantee belongs in notes.

Recommend releasing the selectable behavior with these limits disclosed after
final candidate acceptance, rather than silently making <=2% an achieved claim.
If <=2% is required for this release, that becomes a separate performance
blocker requiring an accepted bounded optimization and representative new runs.
Sol Ring and heavier ships do not inherit Pella's measured budget.

## Preparation evidence

Read-only audit and exact hashes:
[source-audit.json](release-preparation-1.8.11/source-audit.json). Source/catalogue
inspection found 21 unchanged released model identities, one added Sol Ring,
and 22 model-bound fallback portrait hashes with no mismatch. Sol Ring compressed
bytes and portal shader match their existing Studio sources. These are asset
integrity results, not native acceptance or geometry-fidelity proof.

Prior product evidence remains in `docs/PERFORMANCE-2026-10-21.md`,
`docs/SOL-RING.md`, `artifacts/experience-check`, and
`artifacts/sol-ring-review-20261020`. Tracker's supplied Sol Ring owner pass is
recorded in its `data/x890.json`; it does not accept a new installed package.
Baseline release/install receipt is `artifacts/release-1.8.10/result.json`.

Follow [the release flow](RELEASE-FLOW.md) after this source/candidate decision.
No release commit, CI dispatch, installer, public publication or installation
exists for 1.8.11 yet. Local encrypted publish outputs are validation artifacts,
not public releases. Arboghast remains stopped throughout.
