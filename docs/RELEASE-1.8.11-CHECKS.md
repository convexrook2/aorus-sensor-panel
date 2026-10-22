# 1.8.11 source changes and local check results

October 21, 2026. Owner authorized the prepared Arboghast exclusion patch,
1.8.11 metadata, release-task correction and checks in Codex, with results
required before public publication or installation.

**Requested source blockers resolved. The complete local check wrapper passed,
including npm audit with zero vulnerabilities. This is a dirty working-tree
validation, not exact-commit Jenkins or installed/native acceptance. Nothing
was uploaded, tagged or installed.**

## Changes made

- Applied only `exclude-held-arboghast.patch`. `PanelTheme.cs`, `ShipPanel.cs`
  and `PanelWindow.cs` match their protected pre-Arboghast text; earlier dirty
  work remains. The rejected model and study assets are untouched. The old
  released Arboghast remains; its model identity has not changed. No modeling,
  speculative fix, builder rerun or adoption occurred.
- Set `SensorPanel.csproj`, `FleetStudio.Desktop.csproj`,
  `Packaging/SensorPanel.iss` and `Packaging/README.txt` to 1.8.11. The Studio
  source-version synchronization satisfies the existing solution CI contract;
  it does not authorize Studio deployment or installation.
- Replaced the hard-coded 1.8.10/build-9 combined release task with a local
  check task. A separate CI-candidate Review task requests a full pushed SHA
  and actual successful installer build number.
- `Complete-SensorRelease.ps1` now defaults to Review. Publish and Install are
  explicit, separate phases. Version comes from the requested commit; release
  title no longer says Anubis/fleet lighting. Review never contacts GitHub or
  touches installation; Publish returns before installation; Install verifies
  an existing public release without creating one. Draft notes block Publish.
- Added six isolated release-flow cases and wired them into the core checks
  through a child PowerShell process, keeping test doubles out of real checks.
  The draft notes remain draft and publication remains gated.
- Updated only Vite in the lockfile, 8.3.4 to 8.3.5. Declared compatibility
  ranges and other locked package versions remain unchanged.
- Preserved exact originals of all 15 account-path-bearing research/continuity
  records privately under the ignored artifacts directory. Portable source
  copies change path prefixes only; original bytes and before/after hashes
  remain in the [preservation record](release-preparation-1.8.11/research-path-preservation.json).
  Research/model/reference content and Arboghast's stopped status are retained.
  The public-content guard itself was not changed or bypassed.
- Replaced the local asset check's comparison against HEAD with a frozen
  before/after hash, size and path-set comparison. Eight regressions verify
  mutation/addition/removal rejection and Jenkins cleanliness. Jenkins requires
  clean tracked and untracked source at both ends. No cleanliness requirement
  was removed from exact-commit CI.

## Actual results

| Check | Result | Evidence boundary |
| --- | --- | --- |
| Release solution build | PASS, 0 warnings / 0 errors | `dotnet build X890.slnx -c Release --tl:off`; no UI or installation acceptance |
| NuGet all-dependency audit restore | PASS | Explicit `NuGetAudit=true`, `NuGetAuditMode=all`; no outdated top-level NuGet package reported |
| Version agreement, task JSON, changed PowerShell parsing and `git diff --check` | PASS | Source checks only |
| Release-flow isolation | 6/6 PASS | Mock Git/GitHub/native routes in isolated artifact fixtures; no real release or install |
| Asset-immutability / clean-Jenkins-source regressions | 8/8 PASS | Same-size mutation, addition, removal, Studio-model mutation and dirty tracked/untracked CI inputs rejected |
| Native fleet integrity | PASS: 22 entries / 74 bundle files | Exact model/map hashes, including old Arboghast and added Sol Ring |
| Portrait validation | 12 regressions PASS; 22 portrait hashes/dimensions PASS | Loading-fallback/source evidence, not native rendered acceptance |
| Finished Roci / Anubis / Studio assets | PASS | Frozen identities, parity and existing-surface contracts; not new fidelity approval |
| Managed collector contracts | PASS | Optional FrameView native ABI explicitly skipped; no live sensor run |
| Panel mapping/resources | PASS | Explicit `--check-mapping` only; no live monitor/review window |
| RLAR round-trip/rejection contracts | 11 PASS | Archive contracts, not installed-product proof |
| Renderer JavaScript syntax | 8 files PASS | Includes native gate portal; no browser/native rendering claim |
| Local encrypted sensor publish output | PASS: 507 exact source assets | Local validation folder only; no loose protected roots, keys/source/debug files or pack tooling |
| Local encrypted Studio publish output | PASS: 62 exact source assets | Current dirty Studio source integrity only; not deployed or proposed as a Studio release |
| Navigation cases | 50/50 PASS, 6 files | JUnit under `artifacts/release-1.8.11-checks/navigation-tests.xml` |
| Navigation TypeScript/Vite build | PASS | Output explicitly isolated under artifacts; accepted runtime assets not overwritten |
| Public-content policy | PASS | All 15 records have portable source copies with exact private originals; guard unchanged |
| npm vulnerability audit | PASS: 0 vulnerabilities | Final full-wrapper retry succeeded; earlier endpoint timeouts retained as history |
| Compatible npm dependency freshness | PASS | Vite 8.3.5; no remaining in-range updates. Major migrations remain held |
| Full `Run-JenkinsChecks.ps1` | PASS: all 5 aggregate groups | 783 protected asset inputs unchanged; `ExactCommitCI=false`, no install/live-sensor action |
| Exact-commit Jenkins / installer / native LIVE / installed upgrade | NOT RUN | Requires agreed source slice and applicable next action authority |

The navigation test run emitted a Node localStorage experimental warning and
the isolated Vite build emitted its >500 kB chunk warning. Neither failed the
tests/build. Neither warning was suppressed or treated as a product defect fix.

## Earlier failures and resolution

`Run-JenkinsChecks.ps1` passed toolchain preflight, then its core stopped at
`Check-PublicContent.ps1` before the build. The new scope manifest's repository
entry was corrected to be relative. At that checkpoint, fifteen preserved
research, handoff and output files contained account-specific paths. They were
left untouched until the subsequent authorization below.
The owner subsequently authorized resolving these blockers while preserving
research. Exact originals were copied and hash-verified before any edit, and
only local path prefixes were made portable in the source copies. JSON shapes,
non-path values, original filenames and research assets are retained. The
historical helper script derives paths from its existing script location and
user-profile environment; it was parsed, not executed. All 15 private-original
and portable-copy hashes were verified. The guard now passes unchanged.

The old local unchanged-assets gate compared the starting candidate with HEAD,
so intentional pre-existing edits failed it without proving a mutation during
checks. The replacement compares frozen asset input hashes and path sets
before/after. Clean tracked/untracked Jenkins source is enforced at entry and
exit. The final full run passed this gate for 783 asset inputs. A reviewed,
committed release slice is still required for exact-commit CI.

After the wrapper stopped, the independent commands in the table ran directly
in Codex. This establishes their separate results without bypassing or changing
the wrapper's verdict. Earlier `artifacts/ci` evidence was copied to a dated
`artifacts/release-1.8.11-checks/prior-ci-*` directory before the failed run.

`npm ci --ignore-scripts --no-fund` stalled without output. Only that owned
check was interrupted. The retry with `--no-audit` installed the same 60 locked
packages in four seconds; it did not update the lockfile or suppress the
required separate audit. The separate audit used bounded network retries and
reported a timeout at the npm bulk advisory endpoint. Navigation tests/build
then passed independently. A later explicit retry and minimal official-endpoint
probe also timed out; those results are retained. After the scoped Vite update,
the final full wrapper's mandatory audit succeeded with **zero vulnerabilities**.
The final wrapper reran all 50 tests and the isolated Vite 8.3.5 build; compatible
dependency freshness passed. New Three.js/types, TypeScript and
Vitest versions outside current ranges remain separate migration reviews.

## Still required before publication or installation

1. Review/freeze the remaining mixed source and supporting tooling. Obtain the
   applicable scoped commit/push authority, then exact pushed-SHA Jenkins
   SUCCESS with the requested installer and matching source/package identities.
2. Perform agreed final native/saved-data/LIVE and representative resource
   acceptance. Full Fidelity GPU is still unmeasured; the earlier rotating
   Efficient trial was above the <=2% target. These local results do not accept
   the final visible motion or guarantee a GPU budget.
3. Review the actual installer/hashes and final notes, then separately authorize
   public publication and installation. No installer was compiled in this turn.

Machine-readable latest result:
`docs/release-preparation-1.8.11/blocker-resolution-result.json`. The earlier
`execution-result.json` remains a dated pre-resolution receipt. Local validation outputs
stay under `artifacts/release-1.8.11-checks`. HEAD and index remain unchanged;
all edits are local/unstaged. Arboghast stays STOPPED and NOT ADOPTED.
