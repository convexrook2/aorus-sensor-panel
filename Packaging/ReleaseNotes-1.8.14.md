# v1.8.14 — Pella, Scirocco and AORUS memory correction

Pella now retains the downloaded hull relief, Free Navy paint, 34 work lights
and four fitted drive plumes. Scirocco now uses the owner-selected, painted
STL model with physical hull relief, corrected marking placement, 14 work
lights and four registered drives. Both loading portraits match the full
models used by the rotating showcase and interactive holotable. The accepted
geometry is unchanged, and other accepted ships are preserved.

AORUS Lite releases the optional ship and solar graphics when they are not
needed. Animation decoding, game discovery, settings allocation and runtime
memory use are reduced without reducing the accepted artwork or ship quality.
FPS capture starts when a supported game is running and stops when it exits.
Sensor collection continues in the tray while hidden artwork stops.

The pre-release six-minute LIVE test with Le Mans Ultimate recorded 11,745
AORUS samples below 100 MB of private resident RAM across the app and its
helpers. The maximum was 91.94 MB, including return from Expanse, and 88.55 MB
in the subsequent tray phase. These measurements describe that test and
hardware, not a guarantee for every machine or every instant. Shared and
committed memory are different measurements. Expanse has a separate graphics
budget, and its shutdown interval is reported separately.

The familiar monitoring layout, saved preferences and full ship shading are
preserved. Fleet Studio remains a separate application; this installer updates
SensorPanel only.
