# Native AORUS memory review

Build the monitor and this console probe in Release. The probe launches a
visible development monitor, exercises its lifecycle, samples its private
resident RAM and includes descendant processes. Its own allocations stay
outside the measured app tree. It does not trim working sets or force diagnostic
collections. The monitor's normal production cleanup remains active.

From the repository root, a saved-data review is:

```powershell
dotnet build SensorPanel/SensorPanel.csproj -c Release
dotnet build tools/AorusMemoryProbe/AorusMemoryProbe.csproj -c Release
& ./tools/AorusMemoryProbe/bin/Release/net10.0-windows/AorusMemoryProbe.exe --exe ./SensorPanel/bin/Release/net10.0-windows/SensorPanel.exe --preview ./tools/preview-empty.json --memory-aorus-only --memory-all-themes --memory-phase-seconds 30 --memory-theme-seconds 5 --output ./artifacts/aorus-memory-preview
```

For actual hardware, exit the installed tray instance and run the same probe
from a visible administrator terminal, replacing `--preview ...` with `--live`.
The probe rejects an unelevated LIVE run to prevent measuring only an elevation
launcher. It does not install software or change startup/preferences.

Omit `--memory-aorus-only` to also exercise Expanse and return to AORUS. The
retirement phase still belongs to Expanse and is reported separately; the
AORUS gate resumes when the requested mode has actually been activated.
No accepted model is edited by a review.

`memory.json` reports peaks, actual average sampling interval, helpers and a
strict **100,000,000-byte** private-resident gate. Total resident memory includes
shared pages and is reported separately, as is private committed memory.
`lifecycle.json` records live sensor cycles, actual window size, artwork ticks,
heap figures and production cleanup counts/timing. `samples.json` preserves
individual measurements. Sampling is a bounded observation, not proof that an
unsampled spike or future runtime can never exceed the limit.

`--dump-tool <local dotnet-dump.exe>` captures a heap after settings close.
Those runs are diagnostic and cannot pass as performance acceptance. Dumps
can contain application data and keys; keep them local in ignored artifacts.
