# Accepted Pella and Scirocco integration — October 24, 2026

Lennon accepted the painted Scirocco and restored Pella and requested both in
Fleet Studio and SensorPanel. Integration copies the accepted exports exactly;
it does not rebuild geometry or repaint them. The selected Scirocco STL remains
the geometry baseline. Dropships and the other accepted ships are unchanged.

| Ship | Accepted revision | GLB SHA-256 | Rendered triangles | Source lights | Drives |
|---|---|---|---:|---:|---:|
| Pella | v27 restored relief | `df080b0037ceb485ad4c8005e63c1a4b471171c60bf276ae3ea8ef365cce6d78` | 1,388,395 | 34 | 4 |
| Scirocco | selected STL paint02 | `7cbe6ab1719ded37da2de370a51072ccc9c252b82f26097c7b8b145659563320` | 460,489 | 14 | 4 |

Editable Blender files, losslessly compressed exact GLBs, owner-acceptance records and drive records
are retained in `Assets/Models/Source/Pella-v27` and
`Assets/Models/Source/Scirocco-paint02`. The original research exports remain
unchanged. Pella geometry is by Martin Taichl, Scirocco geometry by cg; both are
CC BY 4.0 with attribution in each bundle and the native model credit.

Both catalogues now use the accepted exports. SensorPanel enables their source
lights and registered exhaust, with a model-hash guard on the outlets. Fresh
1024×2048 portraits were captured through its native full-quality renderer and
bound to the exact model hashes. Pella's previous Fleet Studio comparison is
retained with its own model-bound outlet record. Scirocco uses its accepted
blue-white exhaust profile; the shared shader is unchanged.

Evidence is under `artifacts/pella-scirocco-adoption-20261024`:

- `before.json` and `before/` preserve the pre-adoption catalogues, affected
  assets, portraits, outlet registrations, checkout identity and dirty state.
- `preservation.json` verifies 141 protected pre-existing fleet files and all
  other ships' catalogue, portrait and outlet records remain unchanged.
- `portrait-Pella/` and `portrait-Scirocco/` contain actual native captures and
  renderer diagnostics: exact hashes, full triangles, source lights and four
  active plumes, with no model reduction or texture resampling.
- `studio-installed/` contains the installed Fleet Studio encrypted-package
  check: accepted Pella and Scirocco hashes, lights, four outlets, plume controls
  and advancing rotation. Rocinante S6 and Arboghast were also checked.
- `native-protected-final/` contains the SensorPanel local encrypted-package check
  at 1920×1080: both accepted ships in the showcase and holotable, portrait
  identity, light/plume counts, correct capture sizing and shared viewport
  return. Rocinante was also checked. These are saved-data checks, not live
  hardware or GPU/memory measurements.

`tools/Check-PellaSciroccoAdoption.py` pins the accepted source and consumer
identities, lights, outlets and matching portraits and runs in the source CI
flow. Fleet asset verification, portrait checks, shared plume/glow contracts,
Knight/Canterbury preservation checks and Release builds passed locally.

The per-user Fleet Studio desktop copy was refreshed by
`FleetStudio/Install-Desktop.ps1`; its existing Desktop/Start-menu shortcuts
remain the normal launch path. No background service or startup task was added.
SensorPanel was built and published to a local validation folder only. The
installed monitor remains unchanged. No new installer, commit/push, CI run or
public release was performed. The existing AORUS memory work remains intact;
this ship integration makes no new resource-budget claim.
