# v1.8.16 — Guanshiyin and game FPS selection

Adds the completed Guanshiyin to SensorPanel and improves game FPS selection.
Battlefield 6's blank FPS reading remains unresolved pending a live game check.

- Adds the fully accepted Guanshiyin103B to SensorPanel's rotating showcase and
  holotable, including its complete gill panels, detailed raised drive, broad
  gold mast wrap, dual illuminated guardrails, 31 rectangular source lights,
  fitted plume and matching full-resolution portrait. The exact accepted
  geometry and textures are unchanged; the other 21 ships remain intact.
- Detects verified games using visible owned windows and retains their verified
  process while loading recreates a window. Optional process-timing access
  failures no longer discard a verified game executable.
- Clears an expired game FPS pin, preserving any newer user selection, and
  discards the exited/reused PID's frame history. Desktop/app frames remain
  ineligible; no FPS value is estimated from display refresh or GPU load.

The live Battlefield 6 comparison identified the game but ETW trace startup was
access denied even from elevated collectors. Trace-start probes succeeded with
the game closed. The controlled pre-launch/live-game comparison remains
pending; these notes do not claim that the blank BF6 reading is resolved.

The existing monitor layout, preferences, full-fidelity/efficient rendering
choices, auto-rotation, sensor collection and accepted other ships are retained.
The sensor-panel installer does not install or deploy Fleet Studio. Full
Fidelity/Efficient rendering choices, rotation and monitoring layout remain
unchanged.
