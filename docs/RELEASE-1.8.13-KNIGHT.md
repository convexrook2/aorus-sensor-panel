# Sensor 1.8.13 - Knight adoption correction

Lennon requested that the accepted Knight be included with its portrait, lights
and plumes, and that the correction be released with the accepted model unchanged.
This authorizes the scoped integration, validation, commit/push, exact-source
Jenkins installer build and publication. Installation remains separate.

Version 1.8.12 still used the earlier 5,420-triangle Knight. Review 04 was accepted
but remained in the isolated source review. The canonical copy is now retained
in `Assets/Models/Source/Knight-review04`, with the original acceptance receipt,
source attribution and model/drive records. The original research folder remains
unchanged. The model has 968,540 rendered triangles, nine materials, two embedded
maps, seventeen work lights and three registered drives. Both source hashes are
hard-pinned by `tools/Check-KnightAdoption.py`.

The native and shared Studio catalogues use the same lossless GLB and outlet
registration. The native renderer enables Knight's accepted compact white/violet
profile and calibrated lights. Its full-resolution portrait is generated from
that renderer. Other ship catalogue entries, portraits and drive records are
checked against the pre-adoption snapshot. Anubis's original source acceptance
and 1.8.12 runtime receipt remain unchanged; the separate 1.8.13 runtime receipt
records only the shared files affected by Knight routing/profile additions.

Validation receipts are saved under `artifacts/knight-adoption-20261023` and
`artifacts/release-1.8.13`. Source checks, rendered saved-data checks, exact-source
CI, public download verification and installed/LIVE acceptance are separate.
No geometry refinement, other ship reconstruction, live hardware or GPU-budget
measurement is included. The user already installed 1.8.12; its 54 payload files
matched the published manifest before this correction began.

Local source/build/protected-package checks passed, including 50 navigation
tests and the Knight identity/effects checks. The final protected saved-data
native pass also passed at 1024 and 1920 widths: the showcase and holotable
reported the accepted model hash, seventeen active source lights and three
active plumes. Its screenshots and result are in
`artifacts/knight-adoption-20261023/native-protected-final`.

The owner's separate AORUS Lite memory report is confirmed in the installed
1.8.12 process. This correction does not claim to fix memory retention or meet
the requested 100 MB ceiling. No installed-process restart or memory-related
source change is included.
