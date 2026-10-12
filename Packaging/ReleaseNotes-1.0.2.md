The installer now uses the shared Microsoft .NET 10 Desktop Runtime (x64) instead of bundling a private copy. The setup download is approximately 8.7 MiB and the installed app, including its uninstaller, is approximately 29.1 MiB (previously about 200 MiB installed).

Setup detects the required stable 10.0.x Desktop Runtime. If it is missing, setup explains the requirement, offers Microsoft's download page and lets you check again after installing it. Windows 11 alone does not guarantee that this runtime is installed. Download the **Windows x64 Desktop Runtime** from [Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).

Upgrades remove recorded old bundled runtime files while preserving files still needed by the new app. The shared system runtime and user settings are preserved.

This independent FAN MADE project uses AORUS artwork and branding supplied with the original skin; those assets and trademarks belong to their owners. CPU/motherboard readings require administrator access and the existing [PawnIO 2.2+ driver](https://pawnio.eu). GPU readings require the installed NVIDIA driver. Automatic game selection currently recognizes Steam games. Rainmeter and HWiNFO are not required.

Validation covered builds, collector and artwork/mapping checks, prerequisite detection, and isolated install/uninstall with package-file digest verification. Missing, wrong-major, preview-only and core-only runtimes were blocked before installation writes. These checks do not establish compatibility with every hardware/display configuration or startup after sign-in.

Download `AORUS-Sensor-Panel-FAN-MADE-Setup.exe`. `README.txt` contains setup instructions, controls and supported sensor sources. `SHA256SUMS.txt` verifies the attached installer and README.
