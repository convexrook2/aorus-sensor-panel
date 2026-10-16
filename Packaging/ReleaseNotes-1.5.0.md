# v1.5.0 — Holographic ship consoles

- Add coloured sensor readouts, a brief pulse when values change, and smooth
  temperature-ring transitions across all eight Expanse ship themes.
- Add an interactive holotable with pointer/touch orbit, wheel/pinch zoom,
  keyboard controls, reset and Escape-to-close inside the existing panel window.
- Use real Tachi/Rocinante, Donnager, Truman, Epstein's Yacht and MCRN dropship geometry downloaded through
  Sketchfab's enabled download controls. Retain model and artist credits.
- Preserve original painted textures and materials, with a colour-mode button
  to switch to the glowing hologram view. WPF lighting approximates source PBR.
- Add Epstein's Yacht as its own blue-and-white console with a faded model-rendered backdrop.
- Keep Razorback, Tynan and Morrigan as flat blueprint backgrounds;
  show the HOLOTABLE button only on ships with real 3D models.
- Limit ship choices to individual TV-series vessels/classes, preserving downloaded
  original paint. Exclude fleet charts, custom designs and the gold Razorback variant.
- Add a saved Holographic motion option, honour Windows animation preferences,
  and stop motion when hidden or closed. Continuous table-ring motion is capped
  at 20 fps; sensor polling and numeric readings keep their existing behaviour.
- Use compact embedded meshes and 1k original colour textures. Native WPF
  rendering needs no embedded browser, Python or additional installed runtime.

The installer continues to require .NET 10 Desktop Runtime x64. Display choice,
HDMI reconnect recovery, fullscreen tray behaviour and startup remain unchanged.
