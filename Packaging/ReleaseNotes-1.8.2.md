# v1.8.2 — Detailed Sol navigation, Razorback v2 and FPS repairs

Restores the original **SOL / NAVIGATION** diagram with detailed Belter Charts
Sun and planet textures, Earth clouds, Saturn rings, and irregular Eros/Phoebe
meshes. The destination list, orbit layout and Reset control retain the original
presentation. The diagram is explicitly schematic and not to scale. Tycho is a
mobile Belt station; the stationary Sol gate is outside Uranus; Medina remains
in a separate labelled Ring Space inset. Assets and credits are bundled locally;
the native view does not require WebView2 or a local web server.

Razorback uses the owner's blueprint-corrected v2 model in the rotating ship
strip and holotable. Source geometry and attribution are retained. The earlier
web navigation experiment remains in source, excluded from the native build.

FPS capture includes the external Windows ETW compatibility repair and clears
process identity/history when a PID is reused, so the source names the actual
presenting application. Windows desktop composition uses observed DWM frame
events when no application stream is available, and is labelled separately.
Missing or stale measurements remain unavailable. No game injection, graphics
hook, game-memory reader or additional driver is introduced. This is not a
certification by an anti-cheat vendor.

The Expanse main theme cycles through eight ship scenes with corrected personnel
associations and optional local media. Personal portrait/background packs stay
local and are not included in the repository or installer. Settings and the
existing startup choice are retained during upgrade.

Requires Windows x64 and Microsoft .NET 10 Desktop Runtime (stable 10.0.x).
Supported CPU/motherboard readings require administrator access and an existing
PawnIO driver; NVIDIA readings require the NVIDIA driver. Setup installs no
driver and preserves user preferences and local media.

Validation covers collector and resource checks, native saved-data previews at
1920x480, 1280x720 and 480x800, ship navigation/holotable checks, and the installed
package's version and file hashes. Saved-data previews do not establish universal
hardware, display or game compatibility.
