The original PresentMon 2.6.0 binary is retained unchanged. The panel prefers the
separately named X890 compatibility build, built from upstream revision
e13fce6acdb55a808fd8318175a56863e532d95f with x890-compat.patch.

Rebuild from a visible console:

    pwsh -File tools/Build-PresentMonCompat.ps1

Requires Git, a Windows SDK, and Visual Studio C++ build tools. The script creates
a new private artifacts directory, checks pinned source revisions, applies the
patch, and writes a build receipt. It never overwrites an existing source tree.

The patch accepts DXGI analytic Present events and the logging Present pair
(178/179, pThis/dwFlags/m_Ret) observed on this Windows installation. It selects
one event stream per process to avoid counting the same call twice. Process
termination clears this selection. Upstream runtime start/stop analysis still
rejects test presents, failed calls, and occluded results.

Process start/stop events advance before resolving each present's process and
swap chain. A reused PID cannot retain a terminated executable's name or frame
history, and its old query handle is released. The build runs regression cases
against the actual upstream output code, including Git followed by BF6 on the
same PID. To repeat those cases with an existing patched source directory:

    pwsh -File tools/Test-PresentMonProcessLifetimes.ps1 -SourceDirectory <source-directory>

The monitor uses --v1_metrics --no_track_display --no_track_gpu --no_track_input.
Its game FPS is successful application-present cadence, not confirmed displayed
FPS. The CSV's QPCTime is seconds even with --qpc_time_ms in v1 mode; the collector
normalizes it to milliseconds. Only recognized running games are eligible.
Desktop focus retains the running game, but ordinary applications and desktop
composition do not become game FPS. Static, minimized, or unsupported rendering
paths may have no measurable FPS.

Capture starts when the collector detects a recognized game and stops when the
last game exits. One session remains active across foreground/game selection
changes while any eligible game is running. The idle sensor monitor therefore
has no FPS helper or discarded desktop CSV stream. Session shutdown runs off
the sensor polling thread and unblocks a quiet redirected pipe. Capture restarts
when a later game is detected; the existing one-second event window must fill
before a fresh FPS value is available.

The compatibility binary also supports --desktop_frames, which subscribes to Microsoft-Windows-Dwm-Core CurrentFrameId
(event 451, version 0, FrameId payload). A bounded queue carries those observations
to the existing output thread, which writes separate #X890DesktopFrame records;
it does not manufacture application presents or interleave CSV writers.
This optional protocol measures received, unique composition-frame intervals.
The monitor's game-only capture does not enable it or use a desktop fallback.
The protocol is session-wide composition FPS, not per-app FPS, monitor Hz, or confirmed
scan-out. No frame IDs are filled in after gaps, and stale data becomes unavailable.
The real-time trace explicitly requests a one-second flush interval.

Capture remains external Windows ETW. No DLL injection, graphics API hook,
game-memory reader, extra service, or driver is introduced by this FPS repair.
This describes the implementation, not an anti-cheat vendor certification.

The original helper remains the displayed-frame fallback if the compatibility
helper is absent. Both binaries' results are identified accurately in diagnostics.
See LICENSE.txt and COMPAT-NOTICES.txt for upstream and dependency notices.
