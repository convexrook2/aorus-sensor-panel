# v1.7.1 — Universal setup and documentation

- Replace machine-specific notes and sample hardware names with general setup,
  support and usage instructions in the repository and installer README.
- Start new installations in the tray until a display is selected. Saved
  displays and fullscreen preferences continue to work with any monitor model.
- Replace default hardware labels and presentation test names with generic
  placeholders; live hardware names still come from the local system.
- Remove a session-specific cleanup script and discover the installed C++
  toolchain rather than assuming a particular Visual Studio edition.
- Clearly label Razorback's flat artwork: its requested 3D model is not included.
  No alternative model is silently substituted.

Hardware compatibility remains subject to the existing explicit sensor and
firmware checks. This release does not broaden low-level hardware access.
Requirements remain Windows x64, .NET 10 Desktop Runtime x64, and supported
drivers. No personal diagnostics, readings or settings are distributed.
