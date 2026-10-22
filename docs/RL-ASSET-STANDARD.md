# .rl installed asset standard (RLAR v2)

Owner intent: keep editable project sources, ship protected assets with the app,
and use `.rl` consistently for future projects. This does not authorize changing
other repositories. Rookline's existing RLAR v1 reader/writer is unchanged.

## Protection boundary

Every X890 monitor or Fleet Studio `dotnet publish` packs `assets.rl`, verifies
every decrypted file against its source bytes, then removes the corresponding
loose copies from that publish folder. The next installer ships that archive;
the installed app does not extract a loose asset tree. Installing does not
perform a lossy conversion or reduce model geometry, textures or render pixels.

Monitor roots: `FleetViewer`, `SolarNavigation`, `Models`, `Ships`, and the used
`AORUS` images/animation frames. Fleet Studio root: `FleetStudio`. Source-only
Blender work, reference downloads and build utilities are not shipped. Readable
third-party credits/licenses remain outside the archive. Compiled UI code,
XAML, font resources, the Windows application icon, executables and runtime
dependencies remain normal Windows/.NET files; this is asset protection, not
executable encryption or code obfuscation.

The client must contain the ability to decrypt its assets. A determined person
can reverse engineer that ability or capture an asset in memory. This deters
casual extraction and detects tampering; it cannot guarantee theft prevention
or turn third-party/fan assets into exclusive property. Disabling WebView
DevTools in protected mode is convenience friction, not a security boundary.

## Reusable format

`Shared/RlArchive.cs` and `tools/RlPack` provide the shared container/packer;
`Shared/AppAssets.cs` supplies the WPF/WebView2 adapter. Each product uses its own
identity and random 256-bit build key. No extra cryptography package is needed.

* Magic: eight bytes `52 4c 41 52 02 00 00 00`.
* Header: 16-byte random salt, 12-byte index nonce, 16-byte authentication tag,
  signed 32-bit little-endian encrypted-index length, encrypted UTF-8 JSON index.
* Index: product and ordered path/offset/stored/original/compression entries.
  Offsets are relative to the data section and must be contiguous.
* Data: per-entry 12-byte random nonce, 16-byte tag, ciphertext. GZip is used only
  when smaller; already compressed model/image bytes are retained exactly.
* AES-256-GCM with a 128-bit tag. HKDF-SHA256 derives separate index/data keys
  using the archive salt and `product/index` or `product/data` context. Data
  nonces are unique within an archive; each archive gets a fresh salt.
* Index AAD: magic, salt and little-endian index length. Entry AAD: product,
  path, offset, original/stored lengths and compression flag, separated by LF;
  invariant decimal numbers. The authenticated index hides filenames too.

The reader authenticates before using metadata/content, rejects malformed paths,
duplicate entries, unsupported versions, gaps, trailing bytes and oversized
decompression. Wrong keys/products and altered archives fail closed. It opens
and decrypts requested assets individually; it does not retain a decrypted fleet
dictionary. Large entries decrypt in place, avoiding a second model-sized managed
buffer; the archive checks enforce that allocation bound. WebView2 receives
in-memory responses; WPF decodes from streams.
Normal development builds retain loose files for editing; a present but invalid
archive never falls back to loose content.

## Build-key handling

Default key files live outside the repository and published payload:
`%LOCALAPPDATA%\LennonBuildKeys\X890.Sensor-v2.key` and
`%LOCALAPPDATA%\LennonBuildKeys\X890.FleetStudio-v2.key`. They are random raw
32-byte build secrets under the Windows user profile, not DPAPI-encrypted files.
Do not commit, log or distribute them. Back them up privately for reproducible
key identity. A new key requires rebuilding the app and its archive together.

MSBuild generates the runtime key class under ignored `obj`, never in source.
`RlKeyFile` can select a separately managed CI/build-machine key. Keep generated
sources, debug/source bundles and raw key files out of distribution. The compiled
runtime key is inherently recoverable; it is not a remote licensing service.

## Validation and release

Run `& .\tools\Validate-ProtectedAssets.ps1` in the visible X890 VS Code terminal.
It builds, exercises authenticated round trips/rejections, publishes to fresh
ignored `artifacts` folders, verifies hashes and opens visible native checks.
It builds no installer, installs nothing, changes no startup task and pushes
nothing. Native checks use saved data; they do not prove live sensor operation.

The next explicitly authorized release uses the existing installer workflow.
Upgrade cleanup lists exact former loose paths; unrelated files are preserved.
Fleet Studio's explicit setup stages/validates an archive before touching its
per-user copy and refuses unknown/changed old loose assets. An installer or
upgrade round trip remains a separate release validation step.

For a published build's LIVE process-tree resource check, run
`& .\tools\Measure-FleetPair.ps1 -ExecutablePath '<exact SensorPanel.exe path>'`
in VS Code after closing other monitor instances. This explicit diagnostic opens
one monitor, requests normal Windows sensor elevation, measures both ships in
rotating showcase/idle holotable/optional holotable rotation/minimized states, restores rendering, then exits. It neither
rebuilds nor installs anything. Raw receipts identify the binary and archive
hash, the whole descendant tree, hardware and actual elapsed time. Do not
attribute shared GPU memory counters or unrelated applications to this app.
`-IsolateMap` instead compares the rotating showcase with solar animation running
and paused, retaining the visible map and live readings. It does not save the
temporary motion change. Prepared ship portraits live under
`FleetViewer/portraits`, inside the existing monitor archive root.
Adding `-CompareRotation` runs all four combinations of ship rotation and solar
motion for each ship. Each visible sample requires the review to pass a 45-point
interior screen-coverage check. The diagnostic activates only its own review
window at phase changes and stops if the owner closes its holotable or selects
another ship. No VS Code input, installation or saved animation change is needed.

Reference API contracts: [AES-GCM encryption](https://learn.microsoft.com/dotnet/api/system.security.cryptography.aesgcm.encrypt?view=net-10.0)
and [WebView2 local content responses](https://learn.microsoft.com/microsoft-edge/webview2/concepts/working-with-local-content#loading-local-content-by-handling-the-%60webresourcerequested%60-event).
Decryption is off the UI thread for WebView requests and occurs at asset load,
not on every rendered frame. The protected host uses `no-store` responses and
does not create an HTTP service or extract a plaintext asset cache.

Other projects should reuse the v2 format and their own product key, adapter
and payload checks. Migrate Rookline v1 separately with compatibility tests;
renaming an arbitrary file to `.rl` provides no protection.
