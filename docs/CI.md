# X890 Jenkins

For the complete source-to-installed sequence and current holds, see
[release flow](RELEASE-FLOW.md). A green Jenkins run is one gate in that flow.

`X890-CI` uses the existing local Jenkins controller and its Windows agent in a
dedicated workspace. It never builds in the owner's interactive checkout. The
pipeline definition is `Jenkinsfile` from this repository's `main` branch.
Like Rookline, a daily Jenkins schedule validates the integration branch even
when it has not changed, so dependency/feed drift is detected. Runs serialize
and time out after 45 minutes. Keep 20
build records and the last five sets of artifacts.

## Gates

`tools/Run-JenkinsChecks.ps1` is the PowerShell 7 entry point. It records the
Windows toolchain versions and runs the existing `tools/Run-CI.ps1` core, which
checks PowerShell/JavaScript syntax, release version agreement,
public-content policy, accepted fleet manifests/hashes, dependency restore/audit,
the Release solution build, managed sensor contracts, panel mapping/resources,
authenticated archive round trips and rejection tests. It publishes both apps
and verifies decrypted asset bytes against source, with no loose protected asset
roots or build keys/source/debug files in either publish output.

The outer runner additionally restores the retained navigation engineering
project with `npm ci --ignore-scripts --no-audit`, then separately requires the
high/critical npm advisory check, runs all discovered
Vitest cases and builds TypeScript/Vite into `artifacts/ci/navigation-build`.
It never uses that project's default build output, which would overwrite
accepted runtime assets. NuGet freshness and npm updates within the declared
compatibility ranges are required gates; CI fails on unapplied compatible
updates rather than silently upgrading packages. New npm majors/out-of-range
versions are explicitly retained in `dependency-migrations.json` for separate
review. This X890 policy preserves the admitted Three.js/compiler compatibility
ranges; it differs from Rookline's strict latest-version policy. High/critical
security advisories still fail independently of those ranges. Dependency
updates remain reviewed source changes.

Local checks freeze SHA-256, size and path-set snapshots of `Assets` and
`FleetStudio/models` before running, then reject changed, added or removed
asset inputs. This permits validation of an explicitly prepared dirty local
candidate while proving that the checks did not alter its assets. Jenkins
additionally requires clean tracked and untracked source at both entry and
exit; local success is not exact-commit Jenkins evidence. Eight isolated cases
cover asset mutation and clean-source enforcement. Asset preservation is also
accounted for if an earlier gate fails.

The explicit npm audit has bounded network retries/timeouts and remains
mandatory. Disabling npm ci's duplicate automatic audit does not bypass the
separate gate. Network failure fails the run. Vite is now locked to 8.3.5
within its existing declared compatibility range; other range migrations
remain separate scope.

Jenkins publishes actual navigation test cases plus clearly named aggregate
contract-gate results. Console self-tests are not presented as invented
individual assertion counts. Full command output, failures, JSON receipts,
source SHA and JUnit XML are retained even when a later gate fails.

The build service generates its own per-product keys in its account's
`LOCALAPPDATA/LennonBuildKeys`, outside Git and archived artifacts. No owner key
needs to be copied into Jenkins. Executable and archive must come from the same
build. Legal notices remain readable. Source checkouts include editable assets.

Every run separately records the Jenkins checkout SHA, actual workspace HEAD
and requested pushed SHA, following Rookline's source-identity gate.
Set `EXPECTED_SHA` for a release or
other exact-revision check; it fails if the actual checkout differs. A passing
run covers that SHA only. It does not establish native visual quality, hardware
compatibility, resource use, an installed upgrade or public release acceptance.

`BUILD_INSTALLER` defaults to false. Select it only for an explicitly requested
release candidate. It compiles the installer and archives its checksum/manifest;
there is no installation, live-monitor launch, driver/startup modification,
release upload or public deployment step. The separate Fleet Studio Pages
workflow remains manual.

## Agent prerequisites

Windows x64, PowerShell 7+, Git, .NET 10 SDK/Desktop Runtime, Python 3, Node.js/npm and the existing
Jenkins Pipeline/Git plugins. Release packaging additionally requires Inno Setup
6, Visual Studio x64 C++ tools and the FrameView SDK with its notices. Managed
CI does not need administrator access, PawnIO or a game/GPU capture session.

If the Jenkins service account lacks Python on PATH, the X890 runner reads
`$JENKINS_HOME/X890-CI-toolchain.json`, with a `PythonExecutable` property pointing
to an existing executable on that agent. This per-job file stays outside Git
and the disposable workspace. It does not install software or change the
controller/system PATH or another project's environment. Normal local runs use
their existing Python command. Keep this path current when moving the agent or
updating a versioned interpreter installation.

## Visible local check

From X890's VS Code terminal on the owner's selected project monitor:

```powershell
& .\tools\Run-JenkinsChecks.ps1
# Toolchain check only:
& .\tools\Run-JenkinsChecks.ps1 -PreflightOnly
# Only for an explicitly requested installer:
& .\tools\Build-Installer.ps1
```

The installer and checksum stay in `artifacts/installer`. Do not stage them on
the Desktop. The Jenkins job cannot validate unpushed changes in an interactive
checkout; commit/push and first successful execution are distinct activation
steps, requiring their applicable owner authorization.

Initial setup checkpoint on 2026-10-19, before first push: the job was created
with this public repository and `*/main`, using no checkout credentials. It was
disabled while the pipeline/source changes awaited an authorized push. At that
checkpoint no Jenkins build had run; configuration alone was not a passing CI
result. Consult the job's current build and exact source SHA for later results.

The earlier visible VS Code local run on 2026-10-19 passed the core CI script, including all
11 archive rejection/round-trip checks and exact source parity for 477 monitor
assets and 57 separate Fleet Studio assets. Its receipts are in `artifacts/ci`.
This is local validation of the working tree, not a Jenkins result for a pushed
commit. Installer compilation also preserves the existing branding files;
generation is only needed when those files are absent.

Pipeline options follow the [Jenkins Declarative Pipeline reference](https://www.jenkins.io/doc/book/pipeline/syntax/).
