# v1.7.0 — Sol navigation and ship associations

- Add an interactive Sol navigation display with planets, moons and stations
  alongside the real hardware readings. Drag to orbit, wheel to zoom, click a
  destination or use its list, and press Home or RESET for the whole system.
- Keep the map explicitly schematic. Positions, body sizes, distances and
  rotation are illustrative; they are not ephemerides or live ship tracking.
  Tycho is a mobile station. Medina is shown separately in Ring Space.
- Add a ship selector that changes theme and focuses a documented series
  association, with its relationship stated in the console caption:

| Ship | Focus | Relationship |
| --- | --- | --- |
| Rocinante/Tachi | Tycho Station | Refit / safe harbor |
| Donnager | Callisto shipyards | Build site / Jupiter fleet |
| Truman | Earth / United Nations | UNN affiliation |
| Tynan | Medina / Ring Space | Former operating base |
| Razorback | Earth / Proserpina Starworks | Builder headquarters |
| Morrigan | Mars / Mariner Valley | MCRN patrol class / story location |
| Dropship | Mars / MCRN | Marine transport affiliation |
| Epstein's Yacht | Mars / Solomon Epstein | Experimental test flight |

Mapping and source links are recorded in
[ExpanseNavigation.cs](../SensorPanel/ExpanseNavigation.cs). These associations
are not invented homeports or a claim about a ship's present position.

- Retain five original-colour 3D models in the left artwork strip. Correct
  Epstein's Yacht and Truman upright orientation. Razorback, Tynan and Morrigan
  retain their flat blueprint artwork. The old separate HOLOTABLE button viewer
  and Original colours / Hologram controls are no longer exposed.
- Keep automatic map/ship motion separate from sensor polling and readout
  pulses. Pulse changed numbers defaults off. Motion pauses when hidden or
  disabled and respects Windows animation preferences. Sensor polling remains
  500–5000 ms, default 1000 ms.
- Preserve display placement, fullscreen tray behavior, theme, startup and
  sensor freshness rules. Requirements remain .NET 10 Desktop Runtime x64,
  the existing PawnIO driver for CPU/board readings and the NVIDIA driver.

The intended Razorback is
[Sebastián Espinoza's red-and-white model](https://sketchfab.com/3d-models/razorback-the-expanse-fan-art-0eca0a298cba4396bceca35d85dc51e8).
The listing does not enable model download. It is a reference only; no model
is included. Razorback retains flat artwork until authorised model files and
redistribution terms are available.

Production interface inspiration:
[Rhys Yorke's Razorback navigation UI](https://rhys.artstation.com/projects/oAVPWm),
[Rocinante UI](https://rhys.artstation.com/projects/q9Am1L), and
[his screen-graphics interview](https://www.pushing-pixels.org/2021/09/04/the-art-and-craft-of-screen-graphics-interview-with-rhys-yorke.html).
This remains an independent fan adaptation; no official production interface
or asset endorsement is claimed.

Validation covered build/resource checks, ship selection and navigation focus,
preserved sensor values/maxima, saved-data layouts, packaged pointer controls,
installer-file digests and application-reported fullscreen/tray state. Touch,
reboot/sign-in and a new performance benchmark were outside this scope. These
checks do not establish compatibility with every hardware/display combination.
