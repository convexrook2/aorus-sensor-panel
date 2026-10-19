# X890 Sensor agent rules

This repository belongs to Lennon. X890 Sensor is primarily a monitoring app; the rotating fleet showcase and Fleet Studio are optional visual features. This file is the current project working agreement. Read it with the root `README.md` before non-trivial work, then read only the relevant subsystem documentation. Historical chat results, rendered screenshots, and a catalogue label do not override current source or owner corrections.

## Scope and ownership

- Work in the existing `main` checkout. Lennon has explicitly asked for one branch and no new worktrees. Check `git status --short --branch` and the relevant diff before editing; the current checkout contains unrelated dirty work. Preserve it. Do not stash, reset, clean, restore, blanket-stage, or overwrite another slice.
- Assign one writer per file or behavior. Read-only helpers may audit separate questions; reconcile their evidence before editing. Do not let a helper silently take native input, install software, publish, or alter another ship.
- Keep the monitoring product, native showcase, standalone Fleet Studio, source assets, local preview, built package, installed app, and public site distinct. A Fleet Studio result is not evidence that the installed sensor app changed. Fleet Studio does not own FPS collection or PresentMon.
- Do not add ship controls to the already crowded monitoring settings merely to support a visual study. Keep showcase controls local to the showcase or Fleet Studio unless Lennon directs otherwise.

## Fleet Studio local service

- Reserve `127.0.0.1:8770` for the standalone, read-only Fleet Studio server. The reusable URL is `http://127.0.0.1:8770/`; `/fleet-preview` redirects there. Port `8771` may be used for a short, separately stopped validation server. Verify `/health` before assuming that a listener on `8770` is Fleet Studio.
- Default to **on-demand** operation: no Fleet Studio sign-in task or persistent server while unused. `FleetStudio/Start-Viewer.cmd` opens the default browser and a visible server console when the named task is absent. Close both the viewer tab and console when finished; closing only the tab leaves the server running. `FleetStudio/Install-LocalServer.ps1 -Remove` removes a matching, stopped sign-in task. Installing the optional **Fleet Studio Local Viewer** task requires a fresh explicit owner request for always-available background operation. The server must bind to loopback and remain separate from monitoring. Its log is `%LOCALAPPDATA%\FleetStudio\server.log`.
- The server serves files and does not create a WebGL context or use the GPU. A browser tab displaying a 3D model does. Keep only the needed viewer tabs open; hidden tabs pause rendering but can retain GPU memory. Do not add always-running renders, automatic browser launches, or a GPU-heavy helper for the server.
- Do not identify or stop a process by port alone. Check the exact PID, executable path, command line, and ownership first; stop only the verified Fleet Studio process when replacing its listener.

## Model and reference fidelity

- Use the named artist's original images, production views, and rotatable source models as primary visual evidence. Owner markup is a geometry correction or annotation, not automatically a literal ship part. Keep distinct surfaces geometrically distinct; do not paint a line on one flat face to simulate depth.
- Check bow, drive end, sides, above, below, and three-quarter views after each structural pass. Show the corresponding source view beside a proposed change. Preserve the accepted Guanshiyin v11 and existing finished ship geometry unless a specific ship and revision are reopened.
- Labels such as `earlier fan study` and `reference pass` state review maturity; they do not prove acceptance. Keep rejected models and source references separate from production assets. Record provenance and exact model hashes when assets move between Fleet Studio and native showcase.
- For Sol Ring, a Fleet Studio v3 model and the solar-map gate marker are separate from the installed app's rotating showcase. Do not call the gate installed until its own native routing, orientation, aperture behavior, packaged assets, and rendered validation are actually complete.

## Evidence and validation

- Distinguish source or syntax checks, local browser rendering, native UI, installed package, live monitoring data, CI, and release evidence. State the exact command or UI flow and scope before validation. If Lennon is working in VS Code, use its visible validation surface for builds, tests, launches, logs, and failures as directed by his global AGENTS instructions; do not silently substitute a hidden run.
- Give a direct, verified local URL when asking Lennon to review a preview. Verify that the page loads and its actual controls work. A process or open port alone is not proof of a rendered model.
- Diagnose from current evidence, change the smallest coherent surface, and preserve source files outside scope. Do not treat tests, hashes, or attractive renders as proof of spatial accuracy or native installation.
- A commit, push, CI dispatch, installer run, public deployment, deletion, hardware action, or external message needs its applicable user authorization. Routine edits and read-only checks within an authorized task should proceed without extra approval loops.

## Succession and handoff

- Use one active handoff at a time. An outgoing chat owns its scope until it states what it released and a named successor actually reads back and accepts that scope. Creating a chat, assigning a title, or sending a prompt is not acceptance.
- Every handoff must name repository, branch and HEAD, dirty/index state, current writer and owned paths, protected/unrelated changes, active holds, actual validation by evidence class, gaps, exact return point, and the next safe action. Say explicitly when the successor has **no custody** yet.
- Carry owner corrections and current documentation into the handoff. Do not infer permission to build, install, publish, use native input, or edit another ship from a prior chat's plan. Update the affected README or authority note when a bounded change alters how the project is run.
