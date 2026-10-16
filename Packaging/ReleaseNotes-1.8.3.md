# v1.8.3 — Games-only FPS and clearer monitoring

Automatic FPS now selects recognized running games from the local Steam game
catalog. It follows the foreground game, retains it while another application
has focus, and shows **No game** when no recognized game is running. The FPS
source menu lists games only; pinned games cannot fall back to desktop or app
FPS. Source labels use the game name, including names containing dots.

The repaired portable PresentMon helper and frame-interval calculation are
retained. Capture remains external Windows ETW, without game injection or a
graphics hook. Desktop composition capture and automatic app fallback are disabled.

Ship consoles give more space to temperatures and sensor values, reduce the
theme heading, and remove actor captions, unidentified-crew placeholders and
the inactive ORIGINAL COLOURS label. Measured CCD hotspot temperature replaces
the CPU voltage-source notice where supported. The BOARD & COOLING strip shows
board temperatures, CPU FAN/CPU OPT/pump RPM and NVIDIA fan duty where available.
Fan duty is a percentage, not RPM; unavailable or stale readings stay unavailable.

The original SOL / NAVIGATION diagram, detailed local assets and Razorback v2
are retained. User preferences and optional local media remain separate from
the installer.

Validation includes collector regression checks, sensor/resource checks and
native saved-data previews at landscape, wide and portrait sizes. The games-only
capture path was also checked live in Le Mans Ultimate before packaging, including
non-game rejection and owned helper/session cleanup.

Requires Windows x64 and Microsoft .NET 10 Desktop Runtime (stable 10.0.x).
Supported CPU/motherboard readings require administrator access and an existing
PawnIO driver; NVIDIA readings require the NVIDIA driver.
