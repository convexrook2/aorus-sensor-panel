# v1.6.0 — Projected sensor dashboard

- Put the sensor numbers on the main holographic display, with translucent
  projection planes, rising light arcs, peripheral beacons and a projection base.
  No gridlines or simulated tactical telemetry.
- Display the original-colour ship rotating in the left artwork strip, in the
  position previously occupied by the AORUS animation. Real 3D models rotate;
  Razorback, Tynan and Morrigan retain a single flat blueprint there.
- Separate Settings > Sensor readouts from Settings > Holographic display.
  Readout pulses default off. Smooth temperature rings remain independent.
  Holographic motion controls the projections and ship rotation only.
- Pause projection/ship motion when hidden, unloaded or disabled and respect
  Windows animation preferences. Rotation/sweep are capped at 20 fps and run
  independently of the unchanged sensor polling interval.
- Preserve original model materials, live sensor bindings, display choice,
  tray-only fullscreen operation and Windows startup preferences.

The application uses the existing .NET 10 Desktop Runtime x64 requirement.
