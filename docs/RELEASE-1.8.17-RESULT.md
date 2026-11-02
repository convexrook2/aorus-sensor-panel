# SensorPanel 1.8.17 release result

Published November 2, 2026 (America/Toronto), from
`ede66e4d322bdedb7c16478096d0ae08f8c10a9b` after Jenkins X890-CI #35 succeeded.
The public [v1.8.17 release](https://github.com/convexrook2/aorus-sensor-panel/releases/tag/v1.8.17)
contains the installer, README and SHA-256 checksums. The release tag matches
the tested commit; all three downloaded files match the reviewed artifacts.

- Installer SHA-256: `75c66b19cca6a4048746eaee12be35035956318bb21d6647877bd0a38fb406c6`.
- Installer size: 477,566,071 bytes; 54 payload files independently verified.
- Requested, Jenkins checkout and actual workspace SHA all match. The package
  manifest records clean source at that exact commit.
- All CI groups passed: privacy, full solution build, model/source/portrait
  identities, sensor/resource contracts, authenticated archives, dependency
  audit/freshness and 50 navigation tests. Packaging passed the native x64
  FrameView bridge ABI check. All 836 accepted asset inputs remained unchanged.
- The protected SensorPanel archive preserves 515 exact source assets. Fleet
  Studio's separate protected build preserves 73. No loose protected asset
  folders, build keys, source or debug files entered either publish output.

The exact CI installer payload passed 12 saved-data native views: showcase and
holotable for Donnager, Agatha King, Guanshiyin, Pella, Scirocco and Rocinante.
The new ships' native captures were also visually inspected. Full model and
portrait identities, canvas resolution, shared viewport return, independent
hologram motion, source lighting and registered drive effects passed.

| Accepted model | Native triangles | Active source lights | Drive plumes |
|---|---:|---:|---:|
| Donnager V6B | 1,848,436 | 64 | 4 |
| Agatha King V16 | 1,250,901 | 54 | 6 |

Both consumers retain the exact accepted geometry and textures. Canonical packed
Blender sources and acceptance receipts remain editable. The native renderer now
enables the new ships' registered effects and rejects stale drive/model pairs;
the regression exercises that actual setup branch. Other accepted ships and the
monitoring layout are preserved.

No production installer was executed. Fleet Studio's source bundle was updated;
its installed desktop copy was not refreshed or launched for this release. No
LIVE monitoring, hardware, rotating GPU-budget or universal game-FPS acceptance
is claimed. Battlefield 6's blank FPS reading remains unresolved and its live
check remains deferred.

The scoped release commits preserve the local study branch, real Git index and
unrelated dirty work. This documentation closeout follows the release tag and
does not replace the tested product commit.

Local evidence: `artifacts/release-1.8.17/publication.json`,
`candidate-review.json` and `public-download`; plus
`artifacts/capital-release-20261102/ci35-package-review.json`,
`ci35-native-final`, `release-source.json` and the scoped follow-up receipts.
Jenkins #35 retains source identity, logs, test reports, installer checksums and
the package manifest.
