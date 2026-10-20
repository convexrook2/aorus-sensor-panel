# v1.8.8 — Theme cycling and protected assets

The Expanse theme cycles through the existing ship themes, including their
palettes. Its cycle toggle and timing controls are directly below the theme
selector. Explicit ship selection temporarily holds the selection before the
saved cycle resumes.

Published creative assets are bundled in an authenticated RLAR v2 `assets.rl`
archive. The app reads assets into memory as needed, without extracting a loose
asset directory. Editable source assets and readable third-party notices remain
available in their respective source/package locations. This deters casual
copying; it does not make client-side assets impossible to recover.

The accepted monitoring layout, latest models, full maps and shading, native
capture resolution and smooth rotation are unchanged. Covered and minimized
visual work pauses while sensor collection continues. The separately launched
Fleet Studio remains independent of the monitor and has no background server.

See `docs/PERFORMANCE-2026-10-19.md` for the measured Pella/Rocinante process-tree
results, including WebView2. Those results describe the tested published build;
they are separate from installation and hardware acceptance of this installer.

Jenkins source/build/asset checks are defined in `Jenkinsfile` and
`tools/Run-CI.ps1`. Installer compilation is an explicit release option. CI does
not install the app, launch live sensors, change startup tasks or publish releases.
