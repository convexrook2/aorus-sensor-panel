# Updating a running SensorPanel

The 1.8.15 installer uses `CloseApplications=force`. Windows Restart Manager
registers destination executable/library paths from the installer's file list,
then closes applications using those files before cleanup and replacement.
This covers a panel hidden in the tray and an application that refuses a normal
close. It does not search for and kill every process named SensorPanel.

The interactive wizard defaults to automatically closing applications when its
files-in-use page appears. Silent installs close them automatically. Setup does
not restart the panel automatically; the final Launch checkbox and shortcut remain
available. The existing mutex guard still applies to uninstalling, so tray Exit
is required before uninstall. Prerequisite failures stop before shutdown/writes.
No preference file, accepted ship, light, plume or monitoring code is changed.

The release wrapper delegates closure to the installer instead of rejecting an
authorized upgrade because SensorPanel or SensorCollector is running. Its public
download, startup selection, installation path and file-hash guards remain.

## Reproducible validation

Run visibly from the approved terminal:

```powershell
pwsh -NoProfile -File tools/Check-InstallerUpgrade.ps1
pwsh -NoProfile -File tools/Check-SensorReleaseFlow.ps1
```

The upgrade check compiles the actual installer script with isolated identity,
no uninstall registry/shortcuts/startup actions, and inert versioned WinForms
executables. It runs the installer with visible progress and its default shutdown
policy. It verifies visible, tray-only and refusal-to-close upgrades, exact new
executable hashes, preference retention, and survival of a same-named executable
outside the target directory. A missing-runtime case verifies that shutdown and
replacement never begin. Only fixture process handles are cleaned up afterwards.
This is installer integration evidence, not a production installation or hardware
test. Artifacts stay under `artifacts/installer/upgrade-check-*`.

October 25 results: the committed 1.8.14 policy reproduced the running-app block
(`upgrade-check-20261025-003045`). The new policy passed visible, tray and stubborn
cases (`upgrade-check-20261025-003112`). The missing-runtime case passed separately
(`upgrade-check-20261025-003227`) after fixing argument-array construction in the
test harness. All successful upgrade cases preserved preferences and the unrelated
process. The production installation was not modified by these tests.

To reproduce the original block, use `-BaselineRevision
25d085bdf4709be0b7163ad32fee17969eafb248` on the upgrade-check command.

Official behavior: [Inno Setup CloseApplications](https://jrsoftware.org/ishelp/topic_setup_closeapplications.htm),
[AppMutex](https://jrsoftware.org/ishelp/topic_setup_appmutex.htm),
and [Windows Restart Manager](https://learn.microsoft.com/windows/win32/rstmgr/using-restart-manager-with-a-primary-installer).
