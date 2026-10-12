Fixes startup and Fit to display when a settings file contains only the polling interval, or when a saved monitor has disconnected. Display selection now falls back correctly and placement is reapplied after initial WPF layout and tray restoration. Fit to display always fits; F11/double-click toggle fit and restore.

Automatic layout now reflows sensor sections for landscape, wide-strip and portrait screens. Manual Classic 16:9, Wide and Portrait layouts are available. Scale artwork supports Fit (preserve proportions), Fill (crop edges) and Stretch (no borders). Restored windows can be resized using the corner grip. Layout/scaling preferences persist, per-monitor DPI awareness is enabled, and monitor resolution changes refresh placement.

Select a secondary monitor with Move to display, then Fit to display to save it for automatic startup. Automatic fullscreen never takes over the primary workspace.

Requires the shared Microsoft .NET 10 Desktop Runtime (Windows x64, stable 10.0.x). Setup offers the [Microsoft download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) if missing. CPU/motherboard collection uses the existing PawnIO 2.2+ driver; NVIDIA collection uses the installed NVIDIA driver. Independent FAN MADE project; AORUS trademarks/artwork belong to their owners.

Validation covered empty/disconnected display selections, negative monitor coordinates, layout geometry, saved-data previews in landscape/wide/portrait arrangements, fit/restore controls, resizing and isolated shared-runtime install/uninstall. These checks do not establish support for every physical monitor or DPI configuration. USB panels that are not exposed as Windows monitors require a separate vendor interface.

Download the setup EXE, README and SHA256SUMS. Version 1.0.2 introduced shared-runtime packaging at approximately 8.7 MiB download / 29 MiB installed.
