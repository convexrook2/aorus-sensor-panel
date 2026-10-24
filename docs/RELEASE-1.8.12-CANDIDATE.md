# Sensor 1.8.12 release record

On October 23 Lennon accepted Canterbury finish14 as sufficient and requested
release. He explicitly chose the prepared 1.8.12 changes plus Canterbury.
The release covers the Sensor application, with the matching shared Fleet Studio
source retained for reproducibility. It does not install or deploy Fleet Studio.

Included: exact accepted Canterbury finish14 and Arboghast pass08, their full
model portraits, shared 3D exhaust, shadow and selective-glow handling, showcase
tilt, holographic platform and ship inspection, plus the prepared Sol paths,
Sun/sky rendering and separate native-map Sol Ring adaptation.

Canonical editable models and acceptance/provenance records are under
`Assets/Models/Source/Canterbury-finish14` and
`Assets/Models/Source/Arboghast-pass08`. The Anubis GLB, material recipe and
original owner receipt remain unchanged. Its separate
`Sources/Anubis/runtime-adoption-1.8.12.json` pins the later authorized shared
renderer changes without rewriting the historical acceptance.

Local Canterbury native saved-data checks passed at 1024 and 1920 widths in
showcase and holotable views. The captured renderer reports the accepted SHA,
112,232 source triangles, six source lights and nine registered plumes.
The loading/background portrait is 1024 by 2048 and uses the same model.
The adoption guard confirms other ship catalogue entries, outlet records and
the other 21 native portraits were unchanged by Canterbury integration.

The full local release suite passed: 50 navigation tests, dependency audit and
freshness, managed/resource contracts, accepted-asset guards, Release builds,
511 protected Sensor assets and 64 separate Fleet Studio assets. The required
maintenance updates are WebView2 SDK 1.0.4314.35 in both apps and Zustand 5.0.16
within the existing navigation dependency range; accepted browser assets were
not rebuilt or replaced by the engineering build.

The final protected-build native pass also passed after the SDK update:
Canterbury in both sizes, Pella/Arboghast holotable interaction and inactive
stopping, Sol body/path alignment, animated/paused/hidden aperture behavior,
Sun lighting and zoom-dependent sky fading. These are rendered saved-data
checks, not an installed or LIVE monitoring result.

Local evidence is retained under `artifacts/canterbury-adoption-20261023` and
release receipts under `artifacts/release-1.8.12`. The complete local and Jenkins
gates must pass before publication; the Jenkins build must use the exact pushed
commit and build the installer from clean source. Public downloads must match
that installer's checksum. Installation remains a separate action.

Private research folders (`Sources/Arboghast`, `Sources/Canterbury`,
`Sources/Knight`, `output`) are excluded only from the local untracked-file
publication scan using a temporary Git excludes file. They are preserved,
unstaged and absent from the release commit. The normal Jenkins checks scan
the complete clean checkout without these local exclusions.

No new LIVE/hardware, game FPS or whole-process GPU measurement is claimed.
The Efficient <=2% GPU target remains unproved. Hidden model dimensions and
fine fittings remain authored approximations. Rejected studies, further Knight
work and ice cargo are outside this release.
