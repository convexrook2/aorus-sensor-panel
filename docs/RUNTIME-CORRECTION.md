# Current-design live runtime correction — 2026-10-19

This supersedes the rejected still-art/2D-graph mode in `7ea9edc`. The monitor
again uses its current WebView2 ship viewport, full model geometry and PBR maps,
native pixel ratio, shadows/AO, solar navigation, holotable and saved appearance
controls. Fleet Studio stays a separate on-demand desktop app with full shading
by default. No ship geometry or source model bytes were changed by this fix.

Efficiency changes preserve the visuals: coalesce repeated model requests and
activity/resize messages, reuse shadows when the hull/light is unchanged, pause
visual work while hidden/minimized, request lower WebView memory while inactive,
and decode/release the AORUS animation sequence only when its theme needs it.
Sensor collection remains serial at the saved interval. Continuous visible
animation still requires CPU/GPU resources; this does not promise zero cost.

The first VS Code launch profile is now **X890 · LIVE monitor**. It runs the
checkout's apphost, never an empty snapshot. Elevation explicitly relaunches the
apphost even when a debugger initially hosts the DLL in dotnet.exe. The window
title distinguishes LIVE monitoring, saved preview and development builds.

## Actual evidence

- Visible VS Code Debug solution build and managed collector contracts passed.
- Both JavaScript entrypoints passed syntax checks. The native bundle checker
  verified all 21 models, 44 files, exact hashes, full triangle counts and maps.
- The actual LIVE profile opened
  `SensorPanel/bin/Debug/net10.0-windows/SensorPanel.exe`, PID 31104, with
  `preview=false` and elevation confirmed by its runtime receipt.
- A native window observation showed live Ryzen 9 9950X3D, RTX 5090, motherboard,
  fan, RAM-clock and network readings alongside the restored ship/solar design.
- `artifacts/live-runtime-check.json` passed: 18 collection cycles, 143 available
  readings, 1000 ms polling and fresh timestamps. The displayed Anubis renderer
  checked its exact catalogue hash, 2,754 triangles, all expected maps, shadows
  and AO; no geometry reduction or texture resampling. Anubis remains explicitly
  labelled an earlier fan study in the current catalogue.
- Its 15-second monitor-process sample was 1.148% machine CPU, 570.1 MiB working
  set and 653.6 MiB private memory. That excludes WebView2 children and GPU/VRAM;
  it is not the rejected empty-preview measurement or a sustained upper bound.
- A subsequent visible VS Code process-tree sample included nine processes,
  including WebView2 and the FPS helper. Over 15.08 seconds it measured 2.364%
  machine CPU, 1000.6 MiB summed working set and 961.7 MiB private memory.
  Working sets can double-count shared pages. GPU/VRAM were not measured.
  There were zero separate Fleet Studio/server processes. This is an active
  animated-scene sample, not minimized performance or a promise of low cost.

After the settings rebuild, a new actual LIVE launch from VS Code opened PID
40824. Its native window now visibly shows Pella beside fresh hardware readings.
Its runtime receipt passed with 143 available readings and confirmed Pella v26
(1,023,503 triangles, source SHA-256
`2a6126d5a1570607f8687fc671f85e8afdea04f624fd05ff737fc6021be53ff0`)
with all expected PBR maps, shadows and AO, no geometry reduction or texture
resampling. The prior Anubis/Pella observation mismatch is resolved by this new
capture and renderer receipt. The current native catalogue also contains
Guanshiyin v11 (98,140 triangles, source SHA-256
`6c01a52e3cc53412eb3d7f4344b6cf0a4029609b2e2e6f18a061fe4548f5ace4`);
both match their Fleet Studio catalogue source hashes. Windows elevation still
prevents the current UI connector from operating live controls.
Guanshiyin live interactions, minimize/restore resource
measurement, owner acceptance, installed-package changes and release remain
separate until actually observed. The latest asset/model work predating this fix
remains protected in the working checkout.

## Simpler settings

The owner rejected the long scrolling settings menu. Its main page now has six
entries: Display, Theme, Sensors, Advanced, Hide to tray and Close panel.
Display contains placement/fit/layout/scaling; Sensors contains FPS/polling.
Advanced holds occasional controls. Ship rotation and showcase controls moved
to the local ellipsis button beside the ship selector. All existing commands
remain available; no ship assets, collector logic or saved preferences changed.
The panel uses a solid themed background instead of a live blurred backdrop.
The focused Debug build passed visibly in VS Code and the actual live app
reopened. The owner opened its gear menu; a native capture showed all six entries
inside a compact panel without a scrollbar, beside Pella and current live readings.
Individual submenu actions were not exercised in that capture.
