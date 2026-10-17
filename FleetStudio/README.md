# Fleet Studio: Pella coating correction

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

The repository package contains only this reviewed ship, its previous finish,
the browser viewer and locally vendored Three.js. The monitoring executable,
game FPS acquisition, original SOL layout and installer are unchanged. Other
unfinished fleet work is deliberately outside this commit.

## Preview

From the repository root, run in a visible terminal:

```powershell
python FleetStudio/serve.py --port 8771
```

Open `http://127.0.0.1:8771/?ship=Pella` in the browser. Drag to orbit, use the
wheel to zoom, or choose a camera view. Ship lights and animated engine plumes
are independent controls. Rotation can be paused while the exhaust animates.
Studio, cinematic and ship-only lighting are available, as are clay, normals
and wireframe inspection. Hidden tabs stop rendering.

**Previous finish** swaps to the separately retained earlier model in memory.
It does not install, delete or overwrite either version. Reloading returns to
the corrected model. The installed panel is not modified by this viewer.

```powershell
python FleetStudio/tools/verify_assets.py
python FleetStudio/tools/unpack_models.py
```

The second command creates ordinary `.glb` copies in an ignored `unpacked/`
folder. These can be imported with Blender's **File > Import > glTF 2.0**.
Textures and 25 source point lights are embedded. Blender's glTF light unit
conversion differs from this browser's calibrated studio rig; use an inspection
light rig when comparing exposure. The local authoring `.blend` files are kept
separately from this distribution.

## Asset integrity and evidence

- Current model: 1,022,795 triangles, 21 material entries, 16 embedded images,
  four drives and 25 point lights.
- Gzip is lossless transport compression. Unreferenced intermediate buffers
  were removed; all referenced accessor and embedded image bytes were compared
  exactly before packaging. No additional LOD reduction or image resampling.
- `manifest.json` pins both compressed and decoded SHA-256 values. The browser
  verifies the decoded model before parsing it.
- Local validation: twelve Blender views under one studio rig, both forward
  close-ups, both pod close-ups, and browser checks of camera views, rotation,
  lights, plumes, alternate lighting and previous-version restore.
- Preservation check before delivery: 991 existing source files, 142 installed
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

Next fleet work: finish Guanshiyin against Lee Fitzgerald / North Front's
production drawings, then rebuild Morrigan from the supported references.
