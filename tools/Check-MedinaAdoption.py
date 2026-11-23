"""Check the exact accepted Medina model, drives, lights and portrait."""

import gzip
import hashlib
import json
import struct
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CANONICAL = ROOT / "Assets/Models/Source/Medina-v4"
ACCEPTED_SHA = "770578ca43f2f8cf72690578b2d595dfb2fe510b505984ac5acfd64003587b6d"


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    accepted = read(CANONICAL / "owner-acceptance.json")
    assert accepted["accepted"] and accepted["glbSha256"] == ACCEPTED_SHA
    assert sha((CANONICAL / "Medina-v4.blend").read_bytes()) == accepted["editableSourceSha256"]
    raw = gzip.decompress((CANONICAL / "Medina-v4.glb.gz").read_bytes())
    assert sha(raw) == ACCEPTED_SHA
    size, kind = struct.unpack_from("<I4s", raw, 12)
    assert kind == b"JSON"
    gltf = json.loads(raw[20:20 + size])
    assert len(gltf["extensions"]["KHR_lights_punctual"]["lights"]) == 20
    assert sum("KHR_lights_punctual" in node.get("extensions", {}) for node in gltf["nodes"]) == 20
    drive = read(CANONICAL / "engines.json")
    assert drive["modelSha256"] == ACCEPTED_SHA
    assert len(drive["engines"]) == len({tuple(row["position"]) for row in drive["engines"]}) == 8
    for folder, effects_file in (("FleetStudio", "ship-effects.js"), ("Assets/FleetViewer", "rocinante-effects.js")):
        bundle = ROOT / folder
        entry = next(row for row in read(bundle / "catalog.json") if row["id"] == "Medina")
        assert entry["sha256"] == ACCEPTED_SHA and entry["triangles"] == 132587 and entry["lights"] == 20
        assert gzip.decompress((bundle / entry["model"]).read_bytes()) == raw
        assert read(bundle / "engines.json")["Medina"] == drive
        assert sha((bundle / effects_file).read_bytes()) == accepted["reviewEffectsSha256"]
    portraits = ROOT / "Assets/FleetViewer/portraits"
    portrait = next(row for row in read(portraits / "index.json")["portraits"] if row["id"] == "Medina")
    assert portrait["sourceSha256"] == ACCEPTED_SHA and portrait["width"] >= 1024 and portrait["height"] >= 2048
    assert sha((portraits / "Medina.png").read_bytes()) == portrait["sha256"]
    print("PASS: Medina V4 exact GLB, 132,587 triangles, 20 source lights, eight registered plumes and matching portrait.")


if __name__ == "__main__":
    main()
