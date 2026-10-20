# Finished Rocinante Season 6

The owner closed this Fleet Studio revision on 20 October 2026 after reviewing
the model, bluish-grey optical finish, light-pass video and corrected exhaust.
Version 1.8.9 adopts the same model losslessly in Fleet Studio and the native
monitor. Promotion changes packaging and runtime effect routing, not geometry.

Model SHA-256:
`d391bc43b0843a328e435f0eefe16a34a0c8724cdaf0fe275f449fcd330e3eb3`.
The full model has 687,992 triangles, 93 materials and ten embedded images.
Paired housing markings use the existing relief and physical edges; existing
lettering is red. The outlined grey tiles, bevels and lips share one finish.
Armour is bluish grey with a lighting-dependent thin-film response. Guns and
forward antennae retain their source asymmetry.

The modeled light inventory is six warm service lamps, two red bow navigation
fittings and one blue drive throat. The separate display effect adds a
blue-white core, blue body, soft halo and one spill light at the measured bell
outlet. Its source-world anchor is (.0007659383,-3.0020000447,.0108564086),
pointing along negative Y; the bell outlet is .002 units inward. Normal viewer
fitting applies once. Other ships keep their existing exhaust style.

Preserve the accepted model and finish unless the owner explicitly reopens
them. The original Tachi remains a separate comparison. The inventory does not
claim an exhaustive match to every light in every TV frame.

Editable packed source is [Rocinante-S6.blend](../Sources/Rocinante-S6/Rocinante-S6.blend).
It stays outside runtime asset roots. Geometry is by Martin Taichl / Martin,
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/), from the
[original model](https://www.printables.com/model/136343-rocinante-from-the-expanse).
X890 modifications cover PBR finishes, markings, emissive fixtures and separate
drive effects. This is credited fan artwork, not official production geometry.
TV/owner reference images are not shipped as textures.

`tools/Check-RocinanteRelease.mjs` checks the accepted identity, editable source,
fixture inventory, measured outlet, and native/Studio model and effect parity.
Existing fleet verifiers check complete package inventories. CI and package
checks do not establish an installed upgrade or live hardware acceptance.
