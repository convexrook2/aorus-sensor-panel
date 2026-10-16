# v1.8.4 — Detailed Sol navigation and smoother true-scale zoom

The original SOL / NAVIGATION diagram keeps its familiar controls and gains
Belter Charts' detailed surfaces, Earth night lights and clouds, atmosphere
rims and a brighter two-layer Sun corona. Names appear only when hovering over
a body; the destination list retains the selected name. Ship-location context
is available from the small navigation heading instead of covering the map.

SCHEMATIC mode retains enlarged bodies and readable spacing. TRUE SCALE uses
physical radii and bundled NASA/JPL Horizons vectors with a uniform distance
scale, an AU/km bar and navigation markers for tiny bodies. Moon positions are
relative to their planets and preserve front transits and rear occultations.
The offline data covers 2026-10-01 through 2027-10-01; a simulated UTC clock and
speed selector make the bounded, looping simulation explicit.

True-scale zoom detaches off-screen and sub-pixel meshes, clips orbit segments
and portal links before drawing, and bounds atmosphere/corona fills to the view.
Visible assets retain their detail. Camera depth adapts to close views, and
camera movement uses a temporary render callback with a 60-update/second limit.
Idle orbital animation remains capped at 20 and stops when hidden or disabled.

Tycho remains an illustrative mobile Belt station. The fictional Sol gate is
fixed outside Uranus, approximately two AU beyond its distance at the data's
start epoch. Medina stays in a separate Ring Space inset. Razorback v2, the
monitoring layout, user preferences and optional private media are retained.

Games-only FPS capture is unchanged: the existing external Windows ETW /
PresentMon path is retained without injecting into or hooking games.

Validation includes collector and resource checks, final native saved-data
previews at 1920x1080 and 480x800, NASA units/interpolation and moon-depth checks,
hover/selection checks, hidden/paused rendering checks and a visible zoom
comparison. In the controlled close-Earth sequence, median UI delay fell from
968 ms to 31 ms. That is a UI responsiveness measurement, not game FPS.
The installed build was also checked with live Le Mans Ultimate FPS, unchanged
FPS runtime files, preserved preferences/media and verified rollback backups.

Requires Windows x64 and Microsoft .NET 10 Desktop Runtime (stable 10.0.x).
Supported CPU/motherboard readings require administrator access and an existing
PawnIO driver; NVIDIA readings require the NVIDIA driver.
