"""Check full-model static portraits; this does not render or launch an app.

python tools/Check-FleetPortraits.py --self-test
python tools/Check-FleetPortraits.py
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import sys
import tempfile
import unittest
import zlib


ROOT = Path(__file__).resolve().parents[1]
SIGNATURE = b"\x89PNG\r\n\x1a\n"
MIN_WIDTH, MIN_HEIGHT = 1024, 2048


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def valid_sha(value) -> bool:
    return isinstance(value, str) and re.fullmatch(r"[0-9a-f]{64}", value) is not None


def png_dimensions(data: bytes, identity: str) -> tuple[int, int]:
    """Check container integrity without trusting the descriptor's dimensions."""
    require(data.startswith(SIGNATURE), "Invalid PNG signature: " + identity)
    offset = len(SIGNATURE)
    width = height = None
    image_data = False
    ended = False
    chunk_index = 0
    while offset < len(data):
        require(len(data) - offset >= 12, "Truncated PNG chunk: " + identity)
        length = struct.unpack_from(">I", data, offset)[0]
        require(length <= len(data) - offset - 12, "Truncated PNG payload: " + identity)
        kind = data[offset + 4:offset + 8]
        payload = data[offset + 8:offset + 8 + length]
        crc = struct.unpack_from(">I", data, offset + 8 + length)[0]
        require(zlib.crc32(kind + payload) & 0xffffffff == crc,
                "PNG checksum mismatch: " + identity)
        require(chunk_index != 0 or kind == b"IHDR", "PNG IHDR must be first: " + identity)
        if kind == b"IHDR":
            require(chunk_index == 0 and length == 13, "Invalid PNG IHDR: " + identity)
            width, height, depth, colour, compression, filtering, interlace = struct.unpack(">IIBBBBB", payload)
            allowed_depths = {0: (1, 2, 4, 8, 16), 2: (8, 16), 3: (1, 2, 4, 8), 4: (8, 16), 6: (8, 16)}
            require(width > 0 and height > 0 and depth in allowed_depths.get(colour, ())
                    and compression == 0 and filtering == 0 and interlace in (0, 1),
                    "Unsupported PNG IHDR: " + identity)
        elif kind == b"IDAT":
            require(length > 0, "Empty PNG image data: " + identity)
            image_data = True
        elif kind == b"IEND":
            require(length == 0 and image_data, "Invalid PNG end: " + identity)
            ended = True
        offset += 12 + length
        chunk_index += 1
        if ended:
            require(offset == len(data), "PNG trailing data: " + identity)
            break
    require(ended and width is not None and height is not None,
            "Incomplete PNG: " + identity)
    return width, height


def validate(bundle: Path) -> int:
    catalog = read_json(bundle / "catalog.json")
    require(isinstance(catalog, list) and len(catalog) > 0, "Empty fleet catalogue")
    entries = {}
    for item in catalog:
        require(isinstance(item, dict), "Invalid fleet catalogue row")
        identity = item.get("id")
        require(isinstance(identity, str) and re.fullmatch(r"[A-Za-z0-9_-]+", identity) is not None,
                "Unsafe fleet identity")
        require(identity not in entries, "Duplicate fleet identity: " + identity)
        require(valid_sha(item.get("sha256")), "Invalid model source hash: " + identity)
        entries[identity] = item

    directory = bundle / "portraits"
    descriptor = read_json(directory / "index.json")
    require(isinstance(descriptor, dict) and type(descriptor.get("schemaVersion")) is int
            and descriptor["schemaVersion"] == 1,
            "Unknown portrait schema")
    require(descriptor.get("view") == "three-quarter", "Portrait view must be three-quarter")
    rows = descriptor.get("portraits")
    require(isinstance(rows, list), "Missing portrait inventory")
    seen = set()
    for row in rows:
        require(isinstance(row, dict), "Invalid portrait row")
        identity = row.get("id")
        require(isinstance(identity, str) and identity in entries,
                "Unknown or unsafe portrait identity: " + str(identity))
        require(identity not in seen, "Duplicate portrait: " + identity)
        seen.add(identity)
        require(row.get("sourceSha256") == entries[identity]["sha256"],
                "Portrait model source is stale: " + identity)
        require(valid_sha(row.get("sha256")), "Invalid portrait hash: " + identity)
        data = (directory / (identity + ".png")).read_bytes()
        require(sha256(data) == row["sha256"], "Portrait checksum mismatch: " + identity)
        width, height = png_dimensions(data, identity)
        require(type(row.get("width")) is int and type(row.get("height")) is int
                and (row["width"], row["height"]) == (width, height),
                "Portrait dimensions differ from PNG: " + identity)
        require(width >= MIN_WIDTH and height >= MIN_HEIGHT,
                "Portrait resolution below 1024x2048: " + identity)
    require(seen == entries.keys(), "Missing portraits: " + ", ".join(sorted(entries.keys() - seen)))
    png_files = {path.name for path in directory.glob("*.png")}
    require(png_files == {identity + ".png" for identity in seen}, "Unexpected portrait PNG files")
    return len(seen)


def fixture_png(width: int = MIN_WIDTH, height: int = MIN_HEIGHT) -> bytes:
    def chunk(kind: bytes, payload: bytes) -> bytes:
        return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload) & 0xffffffff)
    scanlines = (b"\0" + b"\0" * (width * 4)) * height
    return SIGNATURE + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)) \
        + chunk(b"IDAT", zlib.compress(scanlines)) + chunk(b"IEND", b"")


class PortraitChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="x890-portrait-check-")
        self.addCleanup(self.temp.cleanup)
        self.bundle = Path(self.temp.name)
        (self.bundle / "portraits").mkdir()
        source = "1" * 64
        self.catalog = [{"id": "Pella", "sha256": source}, {"id": "SolRing", "sha256": "2" * 64}]
        data = fixture_png()
        self.index = {"schemaVersion": 1, "view": "three-quarter", "portraits": [
            {"id": entry["id"], "sourceSha256": entry["sha256"], "sha256": sha256(data),
             "width": MIN_WIDTH, "height": MIN_HEIGHT} for entry in self.catalog]}
        for entry in self.catalog:
            (self.bundle / "portraits" / (entry["id"] + ".png")).write_bytes(data)
        self.save()

    def save(self):
        (self.bundle / "catalog.json").write_text(json.dumps(self.catalog), encoding="utf-8")
        (self.bundle / "portraits/index.json").write_text(json.dumps(self.index), encoding="utf-8")

    def replace_image(self, data: bytes):
        (self.bundle / "portraits/Pella.png").write_bytes(data)
        self.index["portraits"][0]["sha256"] = sha256(data)
        self.save()

    def test_complete_catalogue_including_gate(self):
        self.assertEqual(validate(self.bundle), 2)

    def test_missing_ship(self):
        self.index["portraits"].pop()
        self.save()
        with self.assertRaisesRegex(ValueError, "Missing portraits: SolRing"):
            validate(self.bundle)

    def test_duplicate_ship(self):
        self.index["portraits"].append(self.index["portraits"][0])
        self.save()
        with self.assertRaisesRegex(ValueError, "Duplicate portrait"):
            validate(self.bundle)

    def test_stale_model(self):
        self.index["portraits"][0]["sourceSha256"] = "3" * 64
        self.save()
        with self.assertRaisesRegex(ValueError, "model source is stale"):
            validate(self.bundle)

    def test_tampered_image(self):
        (self.bundle / "portraits/Pella.png").write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "Portrait checksum mismatch"):
            validate(self.bundle)

    def test_forged_dimensions(self):
        self.index["portraits"][0]["width"] += 1
        self.save()
        with self.assertRaisesRegex(ValueError, "dimensions differ"):
            validate(self.bundle)

    def test_small_image(self):
        self.index["portraits"][0].update(width=512, height=1024)
        self.replace_image(fixture_png(512, 1024))
        with self.assertRaisesRegex(ValueError, "resolution below"):
            validate(self.bundle)

    def test_invalid_signature(self):
        self.replace_image(b"not a PNG")
        with self.assertRaisesRegex(ValueError, "Invalid PNG signature"):
            validate(self.bundle)

    def test_truncated_png(self):
        self.replace_image(fixture_png()[:-1])
        with self.assertRaisesRegex(ValueError, "Truncated PNG"):
            validate(self.bundle)

    def test_bad_chunk_checksum(self):
        data = bytearray(fixture_png())
        data[32] ^= 1
        self.replace_image(bytes(data))
        with self.assertRaisesRegex(ValueError, "PNG checksum mismatch"):
            validate(self.bundle)

    def test_unknown_or_unsafe_identity(self):
        for identity in ["Unknown", "../Pella"]:
            with self.subTest(identity=identity):
                self.index["portraits"][0]["id"] = identity
                self.save()
                with self.assertRaisesRegex(ValueError, "Unknown or unsafe"):
                    validate(self.bundle)

    def test_unlisted_image(self):
        (self.bundle / "portraits/Obsolete.png").write_bytes(fixture_png())
        with self.assertRaisesRegex(ValueError, "Unexpected portrait PNG"):
            validate(self.bundle)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--self-test", action="store_true", help="Use temporary malformed/valid fixtures only")
    args = parser.parse_args()
    if args.self_test:
        suite = unittest.defaultTestLoader.loadTestsFromTestCase(PortraitChecks)
        return 0 if unittest.TextTestRunner(verbosity=2).run(suite).wasSuccessful() else 1
    try:
        count = validate(ROOT / "Assets/FleetViewer")
    except (ValueError, OSError, KeyError, TypeError) as error:
        print("FAIL:", error, file=sys.stderr)
        return 1
    print(f"PASS: {count} full-model three-quarter portraits; exact model/image hashes and PNG dimensions.")
    print("Source asset check only; no app, GPU measurement, installer or native acceptance.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
