# SensorPanel 1.8.15 release result

Published October 25, 2026, from
`fdee377d9a691c2c0a2d8e8bb1472782eecb3a67` after Jenkins X890-CI #19 succeeded.
The public [v1.8.15 release](https://github.com/convexrook2/aorus-sensor-panel/releases/tag/v1.8.15)
contains the reviewed installer, README and checksums. All three downloaded
files matched their staged CI release artifacts; the tag matches the tested commit.

- Installer SHA-256: `c0c4271d92d151965dd3756d9eb0b2eaac31212c988d0b5b002b2c6daf813507`.
- Installer size: 440,427,104 bytes; 54 payload files.
- Isolated installer tests passed for visible, tray-only and refusal-to-close
  applications. Each verified replacement bytes, unchanged preferences and
  survival of an unrelated same-named executable. The missing-runtime gate
  preserved the running process and old files. See [the test scope](INSTALLER-UPGRADE.md).
- Local release checks and exact-source Jenkins checks passed, including
  protected asset parity, release-flow guards and 50 navigation tests. The
  native FrameView bridge ABI check passed during installer packaging.
- All accepted ships, materials, lights, plumes and monitoring behavior are
  unchanged. Fleet Studio was not installed or deployed.

Windows' uninstall registration was read on October 25 and reported 1.8.14
installed. This task did not execute the 1.8.15 production installer or perform
new installed-app/native/LIVE/hardware acceptance. The prior 1.8.14 release
bytes were not replaced.

Local evidence is under `artifacts/release-1.8.15`: `publication.json`,
`candidate-review.json`, `local-checks.json`, `jenkins-19-success.png` and the
public-download directory. Installer integration logs are under
`artifacts/installer/upgrade-check-*`. Jenkins #19 retains the exact source
identity, build logs, test reports and package manifest. This documentation
closeout follows the release tag; it does not replace the tested product commit.
