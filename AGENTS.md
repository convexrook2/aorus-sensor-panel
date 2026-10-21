# X890 Sensor agent rules

This repository belongs to Lennon. X890 Sensor is primarily a monitoring app; the rotating fleet showcase and Fleet Studio are optional visual features. This file is the current project working agreement. Read it with the root `README.md` before non-trivial work, then read only the relevant subsystem documentation. Historical chat results, rendered screenshots, and a catalogue label do not override current source or owner corrections.

## Scope and ownership

- Keep test installers and checksums in `artifacts/installer`. Do not use the
  owner's Desktop as a staging area. Build another installer only for the next
  release or an explicit owner request; 1.8.7's installed image quality is accepted.

- Work in the existing `main` checkout. Lennon has explicitly asked for one branch and no new worktrees. Check `git status --short --branch` and the relevant diff before editing; the current checkout contains unrelated dirty work. Preserve it. Do not stash, reset, clean, restore, blanket-stage, or overwrite another slice.
- Assign one writer per file or behavior. Read-only helpers may audit separate questions; reconcile their evidence before editing. Do not let a helper silently take native input, install software, publish, or alter another ship.
- Keep the monitoring product, native showcase, standalone Fleet Studio, source assets, local preview, built package, installed app, and public site distinct. A Fleet Studio result is not evidence that the installed sensor app changed. Fleet Studio does not own FPS collection or PresentMon.
- Do not add ship controls to the already crowded monitoring settings merely to support a visual study. Keep showcase controls local to the showcase or Fleet Studio unless Lennon directs otherwise.
- Ship themes share the established monitoring layout within each display mode.
  The left stays the ship showcase; CPU, GPU, solar navigation, memory, network,
  and board/cooling readings retain their familiar positions across ships.
  Change ship-specific graphics, palette and framing without making the owner
  search for monitored values. A hull-scan reference is not permission to move
  the monitoring sections.
- The Expanse is the automatic cycle through the existing ship themes, not
  merely a model slideshow with one fixed palette. Explicit selection resumes
  cycling after a manual hold. Keep its cycle toggle and interval directly below
  the theme selector, as well as in Ship options; preserve the owner's interval.

## Fleet Studio local service

- Fleet Studio is for development and reference review. Sensor-panel releases
  add the accepted ships to the sensor panel; they do not authorize launching,
  installing or deploying Fleet Studio.

- Normal desktop use is `FleetStudio.Desktop` (WPF/WebView2 local content;
  virtual-host mapping in development, protected archive responses in publish),
  opened explicitly from its Windows Desktop or Start-menu shortcut.
  `FleetStudio/Install-Desktop.ps1` prepares or refreshes that per-user built copy;
  `X890.code-workspace` and `Start-Desktop.cmd` remain development launch paths.
  It has no SensorCollector reference and no Python/HTTP server. Close exits;
  minimize disposes the browser/model and restore reloads. Preserve the current
  monitor design, latest model bundle and full shading. Do not substitute older
  still artwork, replace the solar scene or remove controls for efficiency.
  Optimize lifecycle, duplicate work and inactive rendering instead. Run the LIVE
  profile for owner acceptance; an empty saved-data preview is not live proof.
  The following loopback rules apply to the optional legacy browser preview.

- Reserve `127.0.0.1:8770` for the standalone, read-only Fleet Studio server. The reusable URL is `http://127.0.0.1:8770/`; `/fleet-preview` redirects there. Port `8771` may be used for a short, separately stopped validation server. Verify `/health` before assuming that a listener on `8770` is Fleet Studio.
- Default to **on-demand** operation: no Fleet Studio sign-in task or persistent server while unused. `FleetStudio/Start-Viewer.cmd` opens the default browser and a visible server console when the named task is absent. Close both the viewer tab and console when finished; closing only the tab leaves the server running. `FleetStudio/Install-LocalServer.ps1 -Remove` removes a matching, stopped sign-in task. Installing the optional **Fleet Studio Local Viewer** task requires a fresh explicit owner request for always-available background operation. The server must bind to loopback and remain separate from monitoring. Its log is `%LOCALAPPDATA%\FleetStudio\server.log`.
- The server serves files and does not create a WebGL context or use the GPU. A browser tab displaying a 3D model does. Keep only the needed viewer tabs open; hidden tabs pause rendering but can retain GPU memory. Do not add always-running renders, automatic browser launches, or a GPU-heavy helper for the server.
- Do not identify or stop a process by port alone. Check the exact PID, executable path, command line, and ownership first; stop only the verified Fleet Studio process when replacing its listener.

## Shared ship-rendering performance

- Present and future native ships must use the shared `FleetViewport` renderer
  with one reused model viewport and preserve the accepted smooth rotation.
  Keep the existing animation-frame timing and normal WebView capture cadence;
  the owner rejected capture division and timer-based pacing as visibly choppy.
  Preserve the exact full model, maps, shading,
  resolution, shadows, AO, rotation speed and existing backgrounds.
- Stop covered/inactive visual work as well as minimized work. In particular,
  restoring the holotable must not restart the covered solar map, crew/backdrop
  animation or instrument animation underneath it. Restore those normally on
  return to monitoring; live collection and bindings continue independently.
- WebView capture/copy cadence is separate from Three.js drawing. Do not stack
  capture throttling on the renderer's frame limit. JavaScript frame counts
  and lower utilization do not prove smooth native presentation. Optimize
  covered/inactive work first; visible motion must remain acceptable to Lennon.
- Match the native WebView capture dimensions to ancestor display scaling.
  Device pixel ratio alone does not account for a surrounding WPF Viewbox.
  Verify both canvas/capture pixels and physical on-screen dimensions before
  calling an enlarged native view full resolution. Preserve the shared layout.
- Compare rotating and minimized resource use with the entire monitor process
  tree, including WebView2 and FPS helpers, in the visible VS Code terminal.
  Record hardware, window size, actual elapsed time and model hashes. Distinguish
  private resident RAM, shared working-set sums, committed memory, GPU engine
  activity and unverified VRAM. Do not infer another ship's measured budget
  from Pella's result; heavier ships still require their own representative run.

## Model and reference fidelity

- Use the named artist's original images, production views, and rotatable source models as primary visual evidence. Owner markup is a geometry correction or annotation, not automatically a literal ship part. Keep distinct surfaces geometrically distinct; do not paint a line on one flat face to simulate depth.
- Check bow, drive end, sides, above, below, and three-quarter views after each structural pass. Show the corresponding source view beside a proposed change. Preserve the accepted Guanshiyin v11 and existing finished ship geometry unless a specific ship and revision are reopened.
- Labels such as `earlier fan study` and `reference pass` state review maturity; they do not prove acceptance. Keep rejected models and source references separate from production assets. Record provenance and exact model hashes when assets move between Fleet Studio and native showcase.
- For Sol Ring, a Fleet Studio v3 model and the solar-map gate marker are separate from the installed app's rotating showcase. Do not call the gate installed until its own native routing, orientation, aperture behavior, packaged assets, and rendered validation are actually complete.

## Evidence and validation

- Preserve the owner's window layout exactly. Do not move, resize, maximize,
  restore, minimize, snap or transfer VS Code windows between monitors. Use the
  existing visible window where Lennon placed it. Do not move other windows
  to rearrange his workspace for validation. This applies to every chat and
  delegated helper. A request to continue, test or launch is not permission to
  change window placement. If an automation tool changes placement as a side
  effect of focusing or sending input, stop using that input route; do not
  repeat it or try to reposition the window back yourself. Use a route known
  to preserve placement, or give Lennon the exact visible Task/profile to run.

- Published creative assets use the shared authenticated RLAR v2 `.rl` standard
  in `docs/RL-ASSET-STANDARD.md`. Keep source assets editable, keys outside Git,
  notices readable and normal models/quality unchanged. Do not extract a loose
  installed asset tree or claim client-side encryption makes theft impossible.
  Rookline v1 and adoption by other projects require their own migration scope.

- Distinguish source or syntax checks, local browser rendering, native UI, installed package, live monitoring data, CI, and release evidence. State the exact command or UI flow and scope before validation. If Lennon is working in VS Code, use its visible validation surface for builds, tests, launches, logs, and failures as directed by his global AGENTS instructions; do not silently substitute a hidden run.
- Give a direct, verified local URL when asking Lennon to review a preview. Verify that the page loads and its actual controls work. A process or open port alone is not proof of a rendered model.
- Diagnose from current evidence, change the smallest coherent surface, and preserve source files outside scope. Do not treat tests, hashes, or attractive renders as proof of spatial accuracy or native installation.
- A commit, push, CI dispatch, installer run, public deployment, deletion, hardware action, or external message needs its applicable user authorization. Routine edits and read-only checks within an authorized task should proceed without extra approval loops.

## Succession and handoff

- Use one active handoff at a time. An outgoing chat owns its scope until it states what it released and a named successor actually reads back and accepts that scope. Creating a chat, assigning a title, or sending a prompt is not acceptance.
- Every handoff must name repository, branch and HEAD, dirty/index state, current writer and owned paths, protected/unrelated changes, active holds, actual validation by evidence class, gaps, exact return point, and the next safe action. Say explicitly when the successor has **no custody** yet.
- Carry owner corrections and current documentation into the handoff. Do not infer permission to build, install, publish, use native input, or edit another ship from a prior chat's plan. Update the affected README or authority note when a bounded change alters how the project is run.
