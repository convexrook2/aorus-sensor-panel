"""Index duplicate animation PNGs without changing any source pixels."""
import argparse, hashlib, json, struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'Assets/AORUS/@Resources/GIFs'
unique, sequences, hashes = [], {}, {}
for folder, prefix, count in [('L', 'Comp ', 175), ('R', 'Comp ', 175), ('GIF1', 'X', 4)]:
    sequence = []
    for i in range(count):
        name = f'{folder}/{prefix}{i}.png'
        png = (FOLDER / name).read_bytes()
        digest = hashlib.sha256(png).hexdigest()
        if digest not in hashes:
            hashes[digest] = len(unique)
            width, height = struct.unpack('>II', png[16:24])
            unique.append(dict(file=name, sha256=digest, width=width, height=height, bytes=len(png)))
        sequence.append(hashes[digest])
    sequences[folder] = sequence
content = json.dumps(dict(schema=1, frames=unique, sequences=sequences), indent=2) + '\n'
path = FOLDER / 'frames.json'
parser = argparse.ArgumentParser()
parser.add_argument('--check', action='store_true')
args = parser.parse_args()
if args.check:
    assert path.read_text(encoding='utf-8') == content, 'Animation index is stale'
else:
    path.write_text(content, encoding='utf-8')
print(f'PASS: {sum(map(len, sequences.values()))} original animation frames / {len(unique)} unique PNGs; exact hashes.')
