# X890 sensor-panel release flow

October 24 continuation: Lennon accepted the updated Pella and Scirocco in
both consumers and asked for the next release step. The 1.8.14 candidate
includes those unchanged accepted ships and the prepared AORUS memory/FPS
correction. Continue scoped source delivery, exact-source CI and package review;
see [the candidate boundary](RELEASE-1.8.14.md). Record publication and any
separate installation against the verified package. Do not install Fleet
Studio again as a SensorPanel release side effect.

October 23 corrective release authority: Lennon requested the accepted Knight
with its portrait, lights and plumes in the app, with the accepted model unchanged,
and requested release. This authorizes the scoped 1.8.13 commit/push, exact-source
CI installer build and publication after validation. See
[the Knight correction](RELEASE-1.8.13-KNIGHT.md). Installation is separate.
The earlier 1.8.12 release is published and the owner installed it.

October 23 release authority: Lennon accepted Canterbury finish14 as sufficient
and requested release. He explicitly selected the prepared 1.8.12 changes plus
Canterbury: accepted Arboghast pass08, shared exhaust, holotable, showcase motion
and Sol navigation. This authorizes the scoped commit/push, exact-source CI,
installer build and publication after validation. Installation is separate.
Canterbury's immutable Blender/export identities and owner receipt are in
`Assets/Models/Source/Canterbury-finish14`. No further geometry changes are part
of this release. Research and rejected studies stay outside the release slice.

October 22 update: 1.8.11 was released with the legacy Arboghast retained. The
owner subsequently confirmed the separate 011 pass08 reconstruction and
requested Sensor inclusion. The current **1.8.12 source candidate** adopts
pass08; see [its integration record](ARBOGHAST-PASS08-SENSOR-20261022.md).
The older rejected study and styling remain excluded. The October 21 readiness
states below are historical; publication/install authority for 1.8.11 does not
automatically apply to this candidate. Codex is the owner-authorized validation
terminal for this continuation, preserving the existing window layout.

Prepared October 21, 2026. Current continuity owner:
[🚀 X890 — Successor — 2026-10-21](codex://threads/01a160a3-a390-7c02-99ce-4234fd53712d).
This is a release procedure and readiness record. No release execution follows
from preparing it.

## Historical October 21 state and scope

The saved `artifacts/release-1.8.10/result.json` records Jenkins #9 SUCCESS for
`d7724d70bf94dbd6bbedc1ecc296557e8d8c5abb`, public download hash verification,
installer exit 0, 54 installed monitor files verified and preserved startup
choices. It explicitly records no fresh native-render or live-hardware retest,
and no Fleet Studio installation/deployment. These are saved results, not a new
public-endpoint or installed-state audit.

The existing `main` checkout has newer uncommitted experience, rendering and
fleet changes. The AORUS Lite / Expanse controls have scoped owner acceptance;
the saved GPU trial belongs to its recorded assemblies. The <=2% whole-app-tree
GPU target remains unmet by the rotating trial and unproved for the newer
Full Fidelity profile. Selecting Lite releases graphics but the current
installer still carries both experiences' assets. Separate Lite/Expanse
packages require their own accepted packaging design and validation.

Arboghast is REJECTED / STOPPED / NOT ADOPTED. Its candidate and review bundle
must not enter a release. The rejected `PanelTheme.cs` and `ShipPanel.cs`
styling, plus the unaccepted `PanelWindow.cs` explicit-capture delay, were
subsequently excluded under Lennon's explicit bounded authorization. The
three-delta patch is applied and the pre-slice text restored while preserving
earlier concurrent changes. Rejected model/research files remain preserved,
stopped and outside the candidate. See [1.8.11 preparation](RELEASE-CANDIDATE-1.8.11.md)
and [current checks](RELEASE-1.8.11-CHECKS.md).
Knight remains parked “good for now”. Preserve accepted Rocinante, Anubis,
Pella and Guanshiyin v11 and all unrelated work.

## Release gates

1. **Agree the candidate.** Select the sensor-panel changes and next version;
   keep source models, native showcase, standalone Fleet Studio and installed
   product identities separate. Record included paths/model hashes, exclusions,
   owner acceptance, held changes and known limits. A published version must
   not be reused for a changed package. Reconcile version fields, installer
   metadata and `Packaging/ReleaseNotes-<version>.md`. No new branch/worktree.
2. **Resolve the source boundary.** Review each dirty path before staging only
   the approved release slice. A candidate needs a reproducible committed file
   set with no rejected Arboghast delta. No stash/reset/clean/blanket-stage or
   overwriting concurrent work. If a clean, reproducible source/package cannot
   be obtained safely in this checkout, stop and agree the boundary with Lennon.
3. **Validate visibly.** Announce each command and scope, then use the existing
   VS Code terminal/window without changing its placement. Run
   `& .\tools\Run-JenkinsChecks.ps1` for source, build, dependency, contract and
   protected-asset gates. Use the existing `X890 · UI preview (no sensors)`
   profile for saved-data UI checks and `X890 · LIVE monitor` for separately
   authorized live acceptance. Preserve monitoring layout, full shading/model
   identity, rotation, capture resolution/cadence and covered/minimized stopping.
   Check both experiences, remembered themes, Full Fidelity/Efficient and
   showcase/holotable transitions. Record actual evidence; saved data is not
   hardware proof. Measure representative ships and the entire process tree
   when a performance claim is included. A source pass does not prove native
   smoothness or the <=2% target.
4. **Deliver the exact source.** With applicable commit/push authority, commit
   only the agreed slice and push normally. Record the full 40-character SHA.
   Dispatch `X890-CI` with `EXPECTED_SHA=<that pushed SHA>` and
   `BUILD_INSTALLER=true` only for the authorized release candidate. Require
   SUCCESS and equality of requested SHA, Jenkins checkout SHA and workspace
   HEAD. Retain logs, JUnit results, installer, checksum and package manifest.
5. **Review the package.** Keep the candidate under `artifacts/installer`.
   Require the same version/source SHA, clean recorded package source state,
   expected model/asset hashes, no rejected study, protected archive checks,
   readable notices and no keys/source/debug/loose protected assets in the
   distributable. Executable and encrypted archive must come from the same
   build. Package compilation alone is not installation or acceptance.
6. **Publish after approval.** Present the exact candidate, SHA, version,
   artifact hashes, validation and known gaps. With applicable publication
   authority, tag that SHA and upload that candidate with version-specific
   release notes/checksums. Verify the remote tag target and downloaded bytes.
   Do not rebuild or silently replace bytes after approval.
7. **Upgrade after approval.** Installation is a separate action. Preserve the
   current install, startup/shortcut choices and preferences. Identify any
   running process by executable path and ownership before stopping it; prefer
   the owner's tray Exit. Perform the authorized in-place upgrade, verify exit
   code/version/file hashes and run the agreed installed native/LIVE acceptance
   visibly. Do not deploy or install Fleet Studio as a side effect.
8. **Close out and report.** Save the source/CI/package/public-download/install
   identities and separate native, hardware, GPU and owner acceptance outcomes.
   Update the Progress Tracker from those receipts. Report pending work and
   failures honestly; Git backup/publication of Tracker is separate from product
   delivery. Record the previous known-good version and recovery plan before an
   upgrade; any recovery installation needs its applicable authority.

## Existing tooling and limits

`tools/Run-JenkinsChecks.ps1` and `Jenkinsfile` supply the exact-source gates;
`tools/Build-Installer.ps1` prepares a package and does not install it.

`tools/Complete-SensorRelease.ps1` now defaults to `Review`, with mandatory full
`ExpectedSha` and CI `BuildNumber` inputs. Version is read from that commit.
Review checks/stages CI artifacts without GitHub or installed-product actions.
Explicit `Publish` verifies the public release/download and returns without
installing; draft notes are rejected. Explicit `Install` verifies that existing
release and then performs the authorized upgrade without publishing. Each phase
needs its applicable owner authority; neither later phase runs automatically.
The normal VS Code task is checks only, with a separate CI-candidate review task.

`tools/Validate-Installer.ps1` is an isolated install/uninstall smoke path with
guards against an existing production installation/startup/shortcut. It is not
the default in-place upgrade flow. Do not remove those guards to force it onto
the owner's installed product.

## Next safe action

Complete the authorized 1.8.13 Knight-only adoption gates, commit the reviewed
slice, push and run exact-SHA Jenkins with installer compilation. Review and
publish those verified bytes. Preserve the accepted model and unrelated work.
Installation and new LIVE/hardware validation remain separate.
