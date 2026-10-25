# SensorPanel 1.8.14 release result

Published October 24, 2026, after Jenkins X890-CI #18 succeeded for
`cf7c68af929ae98a9e7762eeeb57bc2dbb624ecb`. The public release is
[v1.8.14](https://github.com/convexrook2/aorus-sensor-panel/releases/tag/v1.8.14). Its downloaded installer, README and checksums
matched the reviewed CI artifacts.

- Installer SHA-256: `1ba65cedc4153350a52fd67941766360545d6ad83d9843f6bca3e06e9ac86c06`.
- Installer size: 440,396,640 bytes; 54 packaged files.
- The visible native check ran against those exact 54 files: accepted Pella
  v27, Scirocco paint02 and preserved Rocinante in showcase and holotable,
  including model hashes, full triangle counts, light/plume checks, portrait
  identity and native capture sizing. Saved data was used.
- Source/build, managed sensor/FPS contracts, native bridge checks, encrypted
  archive checks, 50 navigation tests and dependency security gates passed.
  Compatibility-range dependency migrations remain separately recorded.
- AORUS LIVE/LMU memory evidence remains the bounded pre-release measurement
  in [AORUS-MEMORY.md](AORUS-MEMORY.md); this release did not repeat hardware
  or GPU testing and does not establish an unsampled absolute memory ceiling.
- Accepted ship geometry and paint are unchanged. Other ship assets, unrelated
  dirty AGENTS/reference edits and local research were preserved.

The SensorPanel installer was not executed. Installed-app and new LIVE
acceptance remain pending. Fleet Studio had already been refreshed and tested
as a separate integration action; this release did not reinstall or deploy it.

Local evidence: `artifacts/release-1.8.14/publication.json`,
`candidate-review.json`, `package-parity.json`, `native-package-check/`,
`jenkins-18-success.png`, and Jenkins #18 archived source identity, tests and
package manifest. The release tag remains on the tested product commit;
this result is a later documentation-only closeout.
