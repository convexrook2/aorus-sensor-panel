EXPANSE FLEET / ORIGINAL FAN MODELS

HISTORICAL PARAMETRIC SOURCES
These retained editable sources predate later accepted full-detail revisions.
They are not the current renderer bundle and must not overwrite it on rebuild.
Current native models/hashes: Assets/FleetViewer/catalog.json and manifest.json.
Current standalone viewer models/hashes: FleetStudio/catalog.json and manifest.json.
The counts and limits below describe this historical generator, not today's
full-detail native showcase or accepted Pella and Rocinante models.

Thirteen active editable GLB models and configurations, authored for this panel using
the same reference-guided Python modelling and native conversion as Razorback v2.
The existing Rocinante, Donnager, Truman, Dropship, Epstein and Razorback meshes
are preserved. Agatha King shares the existing credited Truman mesh.

Included: Tynan, Morrigan, Canterbury, Knight, Anubis, Pella, Nauvoo, Behemoth,
Medina, Scopuli, Scirocco, Hammurabi and Arboghast.

Geometry, UV maps, panel paint and small stencils are authored in
tools/Build-ExpanseFleet.py. No production meshes, purchased assets, Sketchfab
geometry or reference-image pixels are included in these GLBs. All ship designs
remain the property of their respective fictional-design rightsholders. These
are independent fan approximations, not official models. Unseen mechanisms,
minor fittings and markings are interpreted. Modelling units describe relative
proportions, not certified physical measurements. The panel normalises each
hull to fit its viewer; these models do not change the navigation scale modes.

Guanshiyin is retained separately as the owner-selected Fleet Studio v11 model;
the earlier parametric Guanshiyin generator has been retired.
Its old source and generator files remain on disk pending cleanup.

Nauvoo, Behemoth and Medina share a generation-ship base with different fittings.
Hammurabi shares the Scirocco-class base. The wider Arboghast is tilted for a
readable portrait without stretching its proportions.

REBUILD (build dependencies only; none are required by the installed panel)
Python 3.11+, NumPy, Pillow, trimesh 5.1.1.
Run from the repository root:
  python tools/Build-ExpanseFleet.py
To rebuild one hull:
  python tools/Build-ExpanseFleet.py --ships Tynan
The script exports these GLBs and Assets/Models/<Ship>.ship through the unchanged
tools/Convert-HoloScene.py converter. Every native model retains its authored
colours, texture coordinates and emissive fittings. WPF approximates GLB PBR.

The generator enforces the existing 150,000-triangle model limit. The native
converter splits material batches below 20,000 vertices / 60,000 indices.
build-report.json records model counts, source hashes and native asset hashes.
references.json records the visual-reference provenance and interpretation limits.

The standalone Fleet_Interactive_Preview.html reuses the original Razorback
WebGL reviewer's GLB loader, lighting, orbit controls and PBR shader. It works
offline without a CDN or external runtime. It is a model-review artifact,
not part of the running sensor panel. Open it from the extracted ZIP folder.
