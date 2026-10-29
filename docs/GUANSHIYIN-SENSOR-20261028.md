# Guanshiyin SensorPanel integration — October 28, 2026

Release closeout: [1.8.16 was published and its download verified](RELEASE-1.8.16-RESULT.md)
after exact-source Jenkins #26 and native review of the CI payload. The
preparation states below are historical; production installation and the BF6
live check remain pending.

The owner accepted the completed Guanshiyin and requested all recent updates
in SensorPanel. The bundle audit found only Guanshiyin had different geometry
or image bytes. Other Fleet Studio/native hash differences were existing
consumer-specific metadata and light calibration, so those entries were held.

## Exact accepted asset

- Canonical editable source: `Assets/Models/Source/Guanshiyin-gold103`.
- Blender SHA-256: `9a5fc03b7bed8562610b894773944f828da4a862eaca3b70ba19b0374c44a96c`.
- Uncompressed GLB SHA-256: `1a5ec3894f687c5effbb58808224639e8ce6539dc290625c79d67f681615a017`.
- 3,174,446 triangles, 286 meshes, 25 materials and four images.
- 31 rectangular source lights, one registered drive, fitted plume and a
  1024 × 2048 native portrait made from the exact accepted model.

The native bundle uses the accepted GLB without geometry or texture changes.
The rectangular-light helper retains the source dimensions/power under the
viewer's fit transform. Other ships, catalogue entries and drive records are
preserved. Fleet Studio's source and installed package are unchanged by this
SensorPanel integration. No Blender review or geometry work is reopened.

## Validation and delivery boundary

The owner approved bounded background source/build/diagnostic checks and
visible native saved-data checks because the VS Code input route is held.
Existing windows must stay untouched. That approval excludes installation,
publication, commit and push.

Source checks passed for collector selection/lifetime handling, fleet model
identity, portraits, exhaust registration, showcase motion and inspection.
The first protected native check exposed an early capture-size diagnostic;
the diagnostic now waits for physical dimensions before applying its existing
strict assertion. Production viewport sizing/cadence was not changed. The
next protected native check passed Guanshiyin, Pella, Scirocco and Rocinante
in showcase and holotable views, including Guanshiyin's 31 lights and drive.
The final local 1.8.16 package subsequently passed the same native suite,
the packaged collector self-tests including its native x64 bridge ABI, the
resource/mapping check and exact verification of all 515 encrypted assets.
All 208 previously recorded protected fleet/source files and every other
ship's catalogue and drive records remain unchanged. Evidence is recorded
under `artifacts/sensor-update-20261028`, including
`native-package-1.8.16/result.json` and `package-self-tests.log`. This is
saved-data native/package evidence; live hardware and BF6 acceptance remain
separate.

Initial local installer preparation failed the existing public-content gate because old
tracked research documents contain account-specific Windows profile paths.
Those held files were not edited or omitted to obtain a pass. An exploratory
local-manifest option also rejected those tracked inputs and was removed;
the production installer and privacy gate remain unchanged.

Lennon then requested the full GitHub release flow. The local study branch
contains research checkpoint commits absent from published main. The release
therefore uses an alternate index based on published main, adding only the
explicit product/source slice while leaving the real index, local branch and
all research intact. The release manifest retains prior production assets and
the accepted final Guanshiyin; five intermediate local study exports remain
outside the release. Normal privacy and CI/package gates apply to that clean
commit. At preparation time no 1.8.16 CI/publication/installation is claimed;
the installed version is 1.8.15. Subsequent release receipts must record the
exact commit, build and downloaded installer identities separately.

## FPS investigation

The collector now includes visible owned game windows and retains a verified
game PID while loading replaces its window. A denied optional start-time
query no longer discards a path-verified game. Completed/reused game PIDs lose
their frame queues and expired FPS pin; concurrent newer owner selections
are preserved. Managed regression checks cover those changes. These changes
do not establish the cause of Battlefield 6's reported dash.

Both installed and updated elevated collectors detected Battlefield 6, but
each produced zero valid samples and reported access denied starting ETW.
Direct ETW and both PresentMon startup probes succeeded after the game closed.
This does not identify an anti-cheat or Windows policy as the cause. A
controlled trace-before-launch comparison is still required. The owner
explicitly deferred that game check. The bounded pre-game trace was stopped;
no service, game setting or persistent privilege policy was changed.

FPS is measured from actual eligible game presents, not estimated from
display refresh or GPU load. Current compatibility mode measures application
present cadence, not displayed/scan-out cadence. No universal-game FPS
compatibility or successful Battlefield 6 fix is claimed.
