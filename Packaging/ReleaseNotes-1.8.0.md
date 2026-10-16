# v1.8.0 — The Expanse showcase

Adds **The Expanse**, a main theme that cycles the existing eight ships with
cast cards and full-window space backgrounds. Original-colour 3D ships rotate
where models are bundled; the remaining ships retain their flat artwork.

Scene changes preserve the existing sensor controls, readings, maxima and
histories. Only decorative imagery and ship identity transition. The cycle
uses a 45-second default, with 15–120-second choices; manual ship selection
holds that scene until auto-cycle is enabled again. It pauses while hidden,
respects Windows animation settings, and waits during settings/navigation use.
Showcase controls remain separate from sensor polling and holographic motion.

Optional PNG/JPEG backgrounds and portraits load from a local scene manifest.
The app makes no network requests for this media. User media is excluded from
the installer and repository; a built-in space view remains available without
a pack. The included media guide also covers imports from ArtCraft or another
image editor. ArtCraft is not required by the panel.

Validation covers a Release build, mapping/resource checks and visible
synthetic-data previews at landscape, ultrawide and portrait sizes. These
checks are separate from an installed live-hardware or performance test.
