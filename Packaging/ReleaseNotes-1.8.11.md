# v1.8.11 — Experiences, rotating showcase and Sol Ring

**DRAFT: proposed contents only. Not a published or validated release candidate.**

Choose AORUS Lite for the original monitoring themes or The Expanse Experience
for ship consoles. Each experience remembers its theme. Expanse defaults to
Full Fidelity; optional Efficient rendering limits automatic ship drawing to
30 fps and idle solar updates to 10 Hz while preserving the same full models,
maps, lights, shadows, AO and native resolution.

The left showcase rotates automatically. PAUSE ROTATION / RESUME ROTATION sits
directly beneath the ship, preserves its held angle and saves the choice
independently of map/background motion. The showcase and interactive holotable
reuse one full-quality viewport. Covered/minimized visuals stop, and choosing
Lite releases active ship graphics. Live collection remains independent.

Sol Ring joins the monitoring theme selector and Expanse cycle, with its full
upright rim model, separate animated aperture and existing SolGate navigation
association. The established monitoring layout and previously released models
are retained. Prepared, model-bound portraits serve as loading fallbacks.

This proposed release excludes the rejected Arboghast study and its related
styling/capture changes. Knight and other new ship studies are not included.
The previously released Arboghast remains unchanged.

Both experiences remain in one sensor-panel installer. Separate Lite packaging
is deferred. Fleet Studio remains a development tool and is not installed or
deployed by this release.

Efficient mode does not guarantee <=2% GPU use: the earlier rotating trial was
above that target. The final candidate's Full Fidelity GPU use is not yet
measured. Final source/CI/package, native/LIVE and installed acceptance must be
recorded before these draft notes are promoted for publication.
