# Fleet Studio

Hosting target: **https://convexrook2.github.io/aorus-sensor-panel/**.
GitHub Pages was not enabled at the 2026-10-17 checkpoint; this address is not
yet a verified live viewer. Local preview instructions are below. Once hosting
is enabled, direct ship links can use `?ship=Pella` or `?ship=AgathaKing` and
the viewer will not require a local Python process. Development stays on `main`
in the original checkout.

The catalogue contains 23 ship/station/gate variants and their available
comparison revisions. Latest included revisions are Pella's thin layered armour
over its recessed flat backing, Agatha King's v4 fitted drive lighting,
Guanshiyin V05 v7, Morrigan / Scipio Africanus v4, Donnager's v5 fitted
lights and the separate Rocinante Season 6 v6 study. Earlier approximations
remain explicitly labelled. These are review assets, not final owner acceptance
or a native sensor-panel installation.

## Pella coating and bow correction

The Pella preview now uses a smooth dark coating across the forward hull and
four engine pods. The repeated brick colour, roughness and normal maps have
been replaced with restrained grain. Fine engraved print-mesh gutters have
been removed from the broad coating fields, and the complete source forward
hull replaces the earlier visibly rippled reduction. Duplicate forward patches
and the printable label strip have been retired; two existing Free Navy vector
markings sit on the supported forward facets. Large recesses, hatches, weapon
mounts, drive ribs and collars remain.

This is a **credited fan reconstruction and visual study**, not a released
production mesh or a claim of exact production fidelity. Production appearance
was checked across Kelvin Tam's exterior gallery and the first two minutes of
the owner-supplied Generation Films video. Interior shots and other ships in
the montage were excluded as exterior finish evidence.

The bow correction replaces five interpreted internal filler shells with
smaller recessed flat-faced backstops. It preserves the exterior armour and
details. The finishing pass adds fourteen thin, overlapping outer skins on the
four pods and two forward fields, with the existing emblems raised onto the
coating. The panel layout is a reference-guided interpretation; hidden production
construction is not verified. The smooth finish and recessed backing remain.

Agatha King's v4 pass fits six recessed luminous throats inside the existing
drive bells and local light spill at 48 existing emissive fittings. It retains
the complete v3 hull, paint maps and source material. Name and UN insignia
placement remain unverified, so this pass adds no new markings.

The repository package contains the catalogue, model assets, comparison
revisions, browser viewer and locally vendored Three.js. The monitoring
executable, game FPS acquisition, original SOL layout and installer are unchanged.

## Preview

Open **http://127.0.0.1:8770/** in any browser for the regular local viewer.
It is a static site served from this folder; it does not need a separate build
or the sensor-monitoring app. The local server must be running on this PC.

To keep the address available after each Windows sign-in, run
`Install-LocalServer.ps1` once. It registers the **Fleet Studio Local Viewer**
task for the current user, using `pythonw.exe` with no console window. The task
retries a failed server up to three times, one minute apart, and can run on
battery power. Its log is at
`%LOCALAPPDATA%\FleetStudio\server.log`. It does not open a browser at sign-in.
The server is available while this user is signed in and the PC is running.

Double-click **Start-Viewer.cmd** to open the site in the default browser. It
checks `/health`, starts the registered task if needed, and waits for readiness
before opening the browser. It starts a visible fallback server only when the
task is absent; that console must remain open. If the task fails or points to a
different checkout, the launcher reports the failure instead of starting a
competing server. The older
`/Fleet-Studio/Successor/`, `/Fleet-Studio/Review/`, and `/fleet-preview`
links redirect to the same viewer and retain the selected ship.

`Start-Viewer.cmd --check` runs the same readiness/task recovery check without
opening a browser or starting a fallback. Exit codes are 0 for ready, 2 for a
missing task, and 1 for a task/startup failure. Sign-in never opens a viewer tab.

For diagnosis, inspect `Get-ScheduledTask -TaskName 'Fleet Studio Local Viewer'`
and `Get-Content "$env:LOCALAPPDATA\FleetStudio\server.log" -Tail 20` in a visible
PowerShell terminal. New server starts and requests include timestamps and PIDs;
the visible fallback also prints logs to its console. A running Python process
keeps its loaded server code until deliberately restarted. Identify its exact
executable, command line and owner before stopping it. Sign-in persistence still
requires an actual sign-out/sign-in observation; task registration alone does
not prove it.

From the repository root, run in a visible terminal:

```powershell
python FleetStudio/serve.py --port 8771
```

This local QA address works only while that process is running. The HTTPS
hosting target above requires GitHub Pages setup before it can replace it.

Open `http://127.0.0.1:8771/?ship=Pella` in the browser. Drag to orbit, use the
wheel to zoom, or choose a camera view. Ship lights and animated engine plumes
are independent controls. Rotation can be paused while the exhaust animates.
Studio, cinematic and ship-only lighting are available, as are clay, normals
and wireframe inspection. Hidden tabs stop rendering.

The comparison button swaps to each ship's separately retained earlier model in memory.
It does not install, delete or overwrite either version. Reloading returns to
the corrected model. The installed panel is not modified by this viewer.

```powershell
python FleetStudio/tools/verify_assets.py
python FleetStudio/tools/unpack_models.py
```

The second command creates ordinary `.glb` copies in an ignored `unpacked/`
folder. These can be imported with Blender's **File > Import > glTF 2.0**.
Textures and source point lights are embedded. Blender's glTF light unit
conversion differs from this browser's calibrated studio rig; use an inspection
light rig when comparing exposure. The local authoring `.blend` files are kept
separately from this distribution.

## Asset integrity and evidence

- Current Pella model: 1,023,479 triangles, 21 material entries, 16 embedded images,
  four drives and 25 point lights. Agatha King: 297,861 triangles, three material
  entries, four embedded images, six drives and 54 point lights.
- Current finishing pass: six Blender inspection views per ship under a fixed
  rig, matched baseline views, exact preservation of all existing model/map bytes,
  and packaged asset checksum/catalogue verification. Browser validation of these
  two revisions is pending because the local browser tool rejected the preview
  URL. These renders do not establish native app or game performance.
- Gzip is lossless transport compression. Unreferenced intermediate buffers
  were removed; all referenced accessor and embedded image bytes were compared
  exactly before packaging. No additional LOD reduction or image resampling.
- `manifest.json` pins both compressed and decoded SHA-256 values. The browser
  verifies the decoded model before parsing it.
- Validation stays active under `python -O`; a mismatched asset is rejected and
  unpacking refuses to overwrite an edited copy. Rapid model-version switching
  keeps the requested revision/hash together and ignores superseded failures.
- Earlier Pella coating validation: twelve Blender views under one studio rig, both forward
  close-ups, both pod close-ups, and browser checks of camera views, rotation,
  lights, plumes, alternate lighting and previous-version restore.
- Earlier short-window check at 800×400: all seven camera buttons remain clear of the
  footer controls; normal browser dimensions are restored after inspection.
- Earlier coating preservation check: 991 existing source files, 142 installed
  files and 111 earlier preview files match their checkpoint. The original
  checkout's Git index and pre-existing binary diff remain unchanged.
- No native app build, install or live game FPS pass is claimed for this asset
  change. The existing installed rollback remains available locally.

## Attribution and references

Pella geometry is based on Martin Taichl / **@Martin**'s
[Raptor-class light cruiser](https://www.printables.com/model/468982-raptor-class-light-cruiser-from-the-expanse),
licensed [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Changes include
the described coating, mesh cleanup, restored source details, concealed hull
closures, vector markings and fitted work lamps. The previous coating also used
measured material response from Jakub.Vildomec's
[MCRN Tachi fan model](https://sketchfab.com/3d-models/mcrn-tachi-expanse-tv-show-76fc983ab08c449b9042491a00e621cf),
CC BY 4.0; the new maps are authored fine grain.

Visual references, linked for inspection only:

- [Kelvin Tam / Rocket Science production gallery](https://kelvintam.artstation.com/projects/v2358E)
- [Thirupathi Nomula production modelling reel](https://thirupathinomula.artstation.com/projects/y4QPKK)
- [Generation Films Pella breakdown](https://www.youtube.com/watch?v=eZg1ocpLXOE):
  0:54–1:04 exterior pass and 1:51–1:54 docking exterior; all frames before 2:00
  were reviewed locally.

Reference photographs, production renders and video are **not redistributed**
in this package. The Expanse, its designs and Free Navy insignia belong to their
respective rights holders. This is an unofficial fan project.

Three.js is MIT-licensed; its license is included at `vendor/three/LICENSE`.

Each catalogue entry retains its author/adaptation credit and artist/source
links. External CC BY assets retain their existing attribution. Authored fleet
studies identify their production or fan reference basis. The published package
contains no copied production photographs, reference renders or video.

## Updating the fixed URL

Review new assets locally, then import a bounded snapshot from the local fleet
review folder, using the newer Pella preview as its override:

```powershell
python FleetStudio/tools/import_previews.py --source artifacts/fleet-preview/Fleet-Studio/Successor --pella artifacts/fleet-preview/Delivery
python FleetStudio/tools/verify_assets.py
```

The importer preserves all binary geometry/image bytes, removes authoring
recipe metadata, retains credits and publishes content-addressed gzip assets.
It only reads the authoring folders. It does not replace their originals or
modify the sensor panel. Old model files remain available for rollback.

`.github/workflows/fleet-pages.yml` checks and deploys only `FleetStudio/` after
changes to that directory reach `main`; it can also be dispatched manually.
No extra branch or workspace is required. The repository root and unrelated
dirty work are excluded from the deployed artifact.
