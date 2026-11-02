# Donnager and Agatha King — 1.8.17 integration

On November 2, 2026 (America/Toronto), Lennon authorized completion of the
release after accepting Agatha King and queuing Donnager's Fleet Studio and
SensorPanel update. This releases the prior driving hold. The scope is the two
accepted models, matching portraits, source identities, checks and packaging.

| Model | Accepted decoded SHA-256 | Triangles | Source lights | Drives |
|---|---|---:|---:|---:|
| Donnager V6B | `51e1a585ef18ea407c7699a94b7d973043efbb94d16c1590bacc60140e8f36a9` | 1,848,436 | 64 | 4 |
| Agatha King V16 | `fedbfda735582b899231d79f23676550dbeee2d3652aa8b602e19623993880d8` | 1,250,901 | 54 | 6 |

Canonical packed Blender sources, compressed exports, acceptance and drive
records are in `Assets/Models/Source/Donnager-v6b` and `AgathaKing-v16d`.
Both consumers use those exact decoded exports. All other fleet catalogue
entries, model bytes and drive registrations are preserved. Previous Studio
models remain available for comparison. The monitoring layout and shared
rendering quality are unchanged.

Native 1024×2048 portraits were generated from the exact accepted models with
full shading, shadows and ambient occlusion. `Check-CapitalShipsAdoption.py`
pins source/model/portrait identities, source lights and drive registrations.
The protected native suite includes both new ships plus its prior references.
Source checks, rendered/native evidence, CI, publication and installation are
recorded separately under the release receipts; preparation is not release proof.

The full GitHub release flow uses a scoped alternate index on published main,
preserving the local study branch, real index and unrelated dirty work. It must
pass normal privacy, exact-source CI, authenticated package and native package
checks before publication. The accepted geometry and textures are not rebuilt.
Installation remains a separate phase. Battlefield 6 live confirmation remains
deferred, with no claim that its blank FPS value is fixed.

Published November 2 from `ede66e4d322bdedb7c16478096d0ae08f8c10a9b`
after Jenkins #35 and the exact CI payload's 12 native views passed. Public
installer, README and checksum downloads match the reviewed files. Neither
installed application was replaced. See [the release result](RELEASE-1.8.17-RESULT.md).
