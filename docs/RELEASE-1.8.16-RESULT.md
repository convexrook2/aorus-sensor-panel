# SensorPanel 1.8.16 release result

Published October 28, 2026 (America/Toronto), from
`58327cc00a88ad80325156b8bb59924f9adf96d5` after Jenkins X890-CI #26 succeeded.
The public [v1.8.16 release](https://github.com/convexrook2/aorus-sensor-panel/releases/tag/v1.8.16)
contains the reviewed installer, README and checksums. The remote tag matches
the tested commit, and all three downloaded files match the staged artifacts.

- Installer SHA-256: `ea1d3a72ea345ad2cf7ce87f3935dba0ad76c5c3a84ee47c4255cd750b157f3f`.
- Installer size: 474,939,264 bytes; 54 payload files.
- Requested, Jenkins checkout and actual workspace SHA all match. The package
  manifest records clean source at that exact commit.
- All CI groups passed, including privacy, accepted model/source parity,
  authenticated archive checks, managed collector contracts, dependency
  audit/freshness and 50 navigation tests. Packaging also passed the native
  x64 FrameView bridge ABI check.
- Every retained payload file matches the installer manifest. That exact
  CI-built payload passed eight saved-data native views: showcase and
  holotable for Guanshiyin, Pella, Scirocco and Rocinante. Guanshiyin retained
  its full 3,174,446 triangles, 31 source area lights and registered drive.
  The 515 encrypted sensor assets match their source bytes; no loose
  protected asset tree or build key/source/debug files enter the package.

The accepted final Guanshiyin geometry and textures are unchanged. The new
game-discovery/lifetime handling and expired FPS pin correction passed their
source tests. **Battlefield 6's blank FPS reading remains unresolved.** Both
collectors detected it but trace startup failed while it was running. The
owner deferred the controlled live comparison; this release does not claim
universal game compatibility or a confirmed BF6 fix.

Windows reports 1.8.15 installed at this checkpoint. No production installer
was executed, and no installed-app/LIVE/hardware or GPU-budget acceptance is
claimed. Fleet Studio was not installed or deployed. Prior release bytes
were not replaced.

The release uses a scoped alternate index on published main. The local
Guanshiyin study branch, real index and research files remain intact. Five
intermediate local ship exports are excluded; the final editable source and
accepted model are retained canonically. The unchanged privacy gate passed
on the clean release source. CI #24 exposed a stale shared-renderer receipt,
and #25 required a newer compatible Node development-types patch; both were
corrected before successful #26 and publication. Historical Anubis acceptance
and its geometry, lighting and shared effect shaders remain intact.

Local evidence: `artifacts/release-1.8.16/publication.json`,
`candidate-review.json` and `public-download`; plus
`artifacts/sensor-update-20261028/ci26-package-review.json`,
`ci26-native-final`, `release-tree.json` and the scoped commit receipts.
Jenkins #26 retains source identity, logs, test reports, installer checksums
and package manifest. This documentation closeout follows the release tag
and does not replace the tested product commit.
