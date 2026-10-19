"""Package the reviewed Fleet Studio models losslessly for the desktop viewport."""
from pathlib import Path
import gzip
import hashlib
import json
import shutil
import struct

ROOT = Path(__file__).resolve().parents[1]
STUDIO = ROOT / 'artifacts/fleet-preview/Fleet-Studio'
DEST = ROOT / 'Assets/FleetViewer'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    catalog = json.loads((STUDIO / 'catalog.json').read_text(encoding='utf-8-sig'))
    rows = []
    for item in catalog:
        if item['id'] == 'Truman':
            continue  # The named Agatha King replaces its duplicate class entry.
        raw = (STUDIO / item['model']).read_bytes()
        assert sha(raw) == item['sha256'], 'Studio source changed: ' + item['id']
        magic, version, length = struct.unpack_from('<III', raw)
        assert magic == 0x46546C67 and version == 2 and length == len(raw)
        json_length = struct.unpack_from('<I', raw, 12)[0]
        document = json.loads(raw[20:20 + json_length])
        assert all('uri' not in b for b in document.get('buffers', []))
        assert all('uri' not in i for i in document.get('images', []))
        relative = 'models/' + item['id'] + '.glb.gz'
        target = DEST / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        compressed = gzip.compress(raw, compresslevel=6, mtime=0)
        assert gzip.decompress(compressed) == raw
        target.write_bytes(compressed)
        row = {k: v for k, v in item.items() if not k.startswith('before')}
        row.update(model=relative, sourceBytes=len(raw), compressedBytes=len(compressed),
                   compression='gzip-lossless', compressedSha256=sha(compressed))
        used = {p['material'] for mesh in document.get('meshes', [])
                for p in mesh['primitives'] if 'material' in p}
        materials = [document['materials'][i] for i in used]
        row['renderMaps'] = {
            'colour': sum('baseColorTexture' in m.get('pbrMetallicRoughness', {}) for m in materials),
            'normal': sum('normalTexture' in m for m in materials),
            'occlusion': sum('occlusionTexture' in m for m in materials),
            'metalRough': sum('metallicRoughnessTexture' in m.get('pbrMetallicRoughness', {}) for m in materials),
            'emissive': sum('emissiveTexture' in m for m in materials),
        }
        rows.append(row)
        print(f"EXACT: {item['id']}: {item['triangles']:,} triangles; "
              f"{len(raw):,} -> {len(compressed):,} bytes (lossless)", flush=True)
    assert len(rows) == 21 and len({r['id'] for r in rows}) == len(rows)
    (DEST / 'catalog.json').write_text(json.dumps(rows, indent=2) + '\n', encoding='utf-8')
    shutil.copytree(STUDIO / 'vendor', DEST / 'vendor', dirs_exist_ok=True)
    # Studio's old placeholder must not substitute for the upstream MIT notice.
    shutil.copy2(ROOT / 'NavigationWeb/node_modules/three/LICENSE', DEST / 'vendor/three/LICENSE')
    credits = ['Full model geometry and PBR textures retained from the reviewed Fleet Studio.',
               'Gzip is lossless; source SHA-256 is checked again after decompression.', '']
    credits.extend(r['id'] + ': ' + r['credit'] + '\n' + r.get('sourceUrl', r.get('referenceUrl', ''))
                   for r in rows)
    credits.extend(['', 'Three.js: MIT licence. See vendor/three/LICENSE.'])
    (DEST / 'ATTRIBUTION.txt').write_text('\n\n'.join(credits) + '\n', encoding='utf-8')
    files = [{'path': p.relative_to(DEST).as_posix(), 'bytes': p.stat().st_size,
              'sha256': sha(p.read_bytes())} for p in sorted(DEST.rglob('*'))
             if p.is_file() and p.name != 'manifest.json']
    (DEST / 'manifest.json').write_text(json.dumps({'lossless': True, 'ships': len(rows),
        'source': 'Reviewed Fleet Studio catalogue', 'files': files}, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
