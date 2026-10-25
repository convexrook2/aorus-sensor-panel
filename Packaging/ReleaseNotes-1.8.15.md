# v1.8.15 — Update while SensorPanel is running

Setup now closes the installed SensorPanel before updating it, including when
the panel is hidden in the tray. Windows Restart Manager identifies applications
using the installation's files and forces closure when necessary. The previous
startup block requiring a manual tray exit has been removed for upgrades.

Leave "Automatically close the applications" selected if Setup displays that
page. Saved preferences are retained. Use the final Launch option or the app
shortcut to start the updated panel. Uninstalling still requires a tray exit.

The accepted Pella, Scirocco and other ships, their lights and plumes, and the
1.8.14 AORUS memory corrections are unchanged. This installer updates SensorPanel;
Fleet Studio remains separate.
