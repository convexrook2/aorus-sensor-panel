Fixes display switching and automatic recovery when the sensor monitor's HDMI cable is disconnected and reconnected.

- **Move to display** now lists connected monitors as soon as the context menu opens. Selecting a monitor moves and shows the panel there, retaining its current fit/window mode. The same empty-submenu fix restores the **FPS game** choices.
- Disconnecting the selected secondary monitor hides the panel while retaining its monitor identity and fullscreen setting. Reconnecting restores a previously visible panel at the monitor's current bounds without activating it or switching to a fallback screen.
- Choosing **Hide to tray** cancels automatic restoration. Recovery waits if the selected secondary monitor becomes the primary workspace. Delayed monitor enumeration is retried while the selected monitor is absent.
- Installer updates explicitly request administrator access and report a failure's exit code. Published packages now include the optional NVIDIA FrameView bridge and its notices; the build checks the published collector as well as the panel. Normal automatic game FPS continues to use PresentMon.

Validation covered source builds, collector/panel checks, native display switching, fit bounds and installed fullscreen operation. The retained HDMI recovery implementation had an earlier physical disconnect/reconnect check. This scope does not establish behavior on every physical display, DPI setting or reboot/sign-in configuration.

Requires **Microsoft .NET 10 Desktop Runtime, Windows x64, stable 10.0.x**. Setup detects it and offers the [Microsoft download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) when missing. CPU/motherboard readings require administrator access and the existing [PawnIO 2.2+ driver](https://pawnio.eu); NVIDIA GPU readings require the installed NVIDIA driver. Rainmeter and HWiNFO are not required. USB screens must be exposed as Windows monitors to use display switching.

Download **AORUS-Sensor-Panel-FAN-MADE-Setup.exe**. **README.txt** provides installation and control instructions; **SHA256SUMS.txt** verifies the installer and README. Independent FAN MADE project; AORUS artwork and trademarks belong to their owners.
