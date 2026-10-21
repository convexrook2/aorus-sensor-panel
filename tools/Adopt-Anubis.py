"""Adopt only the owner-accepted Anubis, retaining source and asset rollback.

Run explicitly in the visible X890 VS Code task. No install, Git, CI or publish.
"""
import argparse
import collections
import datetime
import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import struct

ROOT = Path(__file__).resolve().parents[1]
STUDIO = ROOT / 'FleetStudio'
NATIVE = ROOT / 'Assets/FleetViewer'
SOURCE = ROOT / 'Sources/Anubis'
BASE_HASH = '070a255c154c3d62aa2fdbfeb0028e701bfaa06d3db546d717c669537e753be4'
RED_ONLY_HASH = '41fedbe295b89b482b527fb9d6b7e8d851faa729a6da415c6d32fb3154968d5d'
REJECTED_BOW_HASH = 'a633ff3e4743580fdf241dba251146b0be44d8dc387209242839ec3e23a0b128'


def require(value, message):
    if not value:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + '.new')
    temporary.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')
    temporary.replace(path)


def retain(path, data):
    """Never overwrite a different retained source or content-addressed asset."""
    if path.exists():
        require(path.read_bytes() == data, 'Existing retained file differs: ' + str(path))
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def accepted_review(raw):
    """Refuse to replace the fleet with anything except the owner's exact review."""
    receipt = read(SOURCE / 'owner-acceptance.json')
    require(receipt['accepted'] is True, 'Owner acceptance is absent')
    require(sha(raw) == receipt['candidate_sha256'], 'Candidate changed after owner acceptance')
    for item in receipt['accepted_source_files']:
        path = (ROOT / item['path']).resolve()
        require(path.is_relative_to(ROOT.resolve()), 'Accepted source path is outside the checkout')
        require(sha(path.read_bytes()) == item['sha256'], 'Accepted lighting/source changed: ' + item['path'])
    return receipt


def parse(raw):
    require(struct.unpack_from('<III', raw) == (0x46546c67, 2, len(raw)), 'Invalid GLB')
    length, kind = struct.unpack_from('<II', raw, 12)
    require(kind == 0x4e4f534a, 'Missing GLB JSON')
    size, kind = struct.unpack_from('<II', raw, 20 + length)
    require(kind == 0x004e4942 and 28 + length + size == len(raw), 'Invalid BIN')
    return json.loads(raw[20:20 + length]), raw[28 + length:]


def triangle_fingerprints(doc, blob, remap_red=False, remap_bow_only=False):
    """Exact triangle/normal/UV bytes, including original material routing."""
    result = {}
    for mesh in doc['meshes']:
        triangles = collections.Counter()
        for primitive in mesh['primitives']:
            require('indices' not in primitive and primitive.get('mode', 4) == 4, 'Unexpected source encoding')
            fields = []
            for name, width in [('POSITION', 12), ('NORMAL', 12), ('TEXCOORD_0', 8)]:
                accessor = doc['accessors'][primitive['attributes'][name]]
                view = doc['bufferViews'][accessor['bufferView']]
                require(accessor['componentType'] == 5126 and view.get('byteStride', width) == width, 'Unexpected attribute format')
                offset = view.get('byteOffset', 0) + accessor.get('byteOffset', 0)
                fields.append((offset, width, accessor['count']))
            require(len({f[2] for f in fields}) == 1 and fields[0][2] % 3 == 0, 'Attribute/triangle count')
            material = primitive['material']
            if (remap_red and material in (5, 6)) or (remap_bow_only and material == 6):
                require(mesh['name'] in ({5:{'amun7'},6:{'amun9','amun10'}}[material]), 'Emission outside its registered source fixture')
                material = 0
            for i in range(0, fields[0][2], 3):
                triangles[(material, b''.join(blob[o + i*w:o + (i+3)*w] for o,w,_ in fields))] += 1
        result[mesh['name']] = triangles
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--rollback', type=Path)
    parser.add_argument('--finish-bow', action='store_true', help='Apply the owner-requested bow light to the exact red-only adoption')
    parser.add_argument('--correct-bow', action='store_true', help='Replace the owner-rejected exterior strip with the reviewed interior finish')
    parser.add_argument('--review-root', type=Path, help='Explicit prepared candidate directory; defaults to the retained accepted source')
    args = parser.parse_args()
    review = args.review_root.resolve() if args.review_root else SOURCE
    require(not(args.finish_bow and args.correct_bow), 'Choose one bounded update')
    updating = args.finish_bow or args.correct_bow
    rollback = args.rollback or ROOT / 'artifacts' / ('anubis-main-fleet-' + datetime.datetime.now().strftime('%Y%m%d-%H%M%S'))
    rollback = rollback.resolve()
    require(rollback.is_relative_to((ROOT / 'artifacts').resolve()) and not rollback.exists(), 'Use a fresh rollback folder under artifacts')
    raw = (review / ('Anubis-source-assembly.glb' if args.review_root else 'Anubis.glb')).read_bytes()
    acceptance = accepted_review(raw) if args.correct_bow else None
    base = (SOURCE / 'Anubis-accepted-before-red.glb').read_bytes()
    require(sha(base) == BASE_HASH, 'Accepted base changed')
    doc, blob = parse(raw)
    before_doc, before_blob = parse(base)
    require(doc['nodes'] == before_doc['nodes'] and doc['scenes'] == before_doc['scenes'], 'Accepted fitting transforms changed')
    require(len(doc['materials']) == 7 and doc['materials'][:5] == before_doc['materials'], 'Accepted finishes changed')
    def image_bytes(document, binary):
        return [binary[v.get('byteOffset', 0):v.get('byteOffset', 0) + v['byteLength']]
                for image in document['images'] for v in [document['bufferViews'][image['bufferView']]]]
    require(image_bytes(doc, blob) == image_bytes(before_doc, before_blob), 'Accepted surface texture changed')
    require(triangle_fingerprints(doc, blob, True) == triangle_fingerprints(before_doc, before_blob), 'Geometry, normals, UVs or unrelated material routing changed')
    red_faces = sum(doc['accessors'][p['attributes']['POSITION']]['count'] // 3 for m in doc['meshes'] for p in m['primitives'] if p['material'] == 5)
    require(red_faces == 516, 'Expected six existing cap surfaces only')
    bow_faces = sum(doc['accessors'][p['attributes']['POSITION']]['count'] // 3 for m in doc['meshes'] for p in m['primitives'] if p['material'] == 6)
    require(bow_faces == 80 and all(m['name']=='amun10' for m in doc['meshes'] if any(p['material']==6 for p in m['primitives'])), 'Expected the complete existing inward bow lining: upper/lower, sides and step returns')
    report = read(review / 'assembly-review.json')
    require(report['sha256'] == sha(raw) and report['triangles'] == 198192 and report['sourceGeometryModified'] is False, 'Review identity mismatch')
    compressed = gzip.compress(raw, mtime=0)
    filename = 'models/Anubis-' + sha(raw)[:12] + '.glb.gz'
    catalog = read(STUDIO / 'catalog.json')
    old_catalog = json.loads(json.dumps(catalog))
    old = next(e for e in catalog if e['id'] == 'Anubis')
    if old['sha256'] == sha(raw) and args.correct_bow:
        current_native = next(e for e in read(NATIVE/'catalog.json') if e['id']=='Anubis')
        require(current_native['sha256'] == sha(raw), 'Existing native Anubis differs from the accepted fleet')
        require((SOURCE/'Anubis.glb').read_bytes() == raw, 'Existing authoring source differs from the accepted fleet')
        require(gzip.decompress((STUDIO/old['model']).read_bytes()) == raw, 'Existing Studio accepted asset differs')
        require(gzip.decompress((NATIVE/current_native['model']).read_bytes()) == raw, 'Existing native accepted asset differs')
        print('Accepted Anubis is already in both source fleets; no assets changed. Continue with main-fleet checks.', flush=True)
        return
    require(old['sha256'] != sha(raw), 'This exact Anubis is already adopted; run the check task instead')
    candidates = [e for e in read(review/'DesktopReview/FleetStudio/catalog.json') if e['id']=='AnubisSourceReview']
    require(len(candidates)==1 and candidates[0]['sha256']==sha(raw), 'Prepared candidate catalogue identity differs')
    candidate_entry = candidates[0]
    if updating:
        required_hash = REJECTED_BOW_HASH if args.correct_bow else RED_ONLY_HASH
        require(old['sha256'] == required_hash, 'Bow update requires its exact previous adoption')
        require(sha((SOURCE/'Anubis.glb').read_bytes()) == required_hash, 'Retained previous source changed')
        red_doc, red_blob = parse((SOURCE/'Anubis.glb').read_bytes())
        require(doc['materials'][:6] == red_doc['materials'][:6], 'The bow completion altered another finish')
        require(triangle_fingerprints(doc, blob, remap_bow_only=True) == triangle_fingerprints(red_doc, red_blob, remap_bow_only=True), 'The bow completion changed existing geometry or another material selection')
    else:
        require(old['sha256'] not in (RED_ONLY_HASH,REJECTED_BOW_HASH), 'Use the exact scoped bow-update flag')
    engines = read(STUDIO / 'engines.json')
    old_engines = json.loads(json.dumps(engines))
    native_engines = read(NATIVE / 'engines.json')
    old_native_engines = json.loads(json.dumps(native_engines))
    manifest = read(STUDIO / 'manifest.json')
    protected = {str(p.relative_to(ROOT)): sha(p.read_bytes()) for folder in (STUDIO/'models', NATIVE/'models')
                 for p in folder.glob('*') if p.is_file() and p != NATIVE/'models/Anubis.glb.gz'}
    rollback.mkdir(parents=True)
    mutable_sources = ['Anubis.glb','review-finish.py','prepare-review.py','assembly-review.json','identity.json']
    for name in mutable_sources:
        if (SOURCE/name).exists():
            require(updating, 'Existing authoring source requires explicit bow-completion scope')
            target = rollback/'Sources/Anubis'/name
            target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copy2(SOURCE/name,target)
    def source_file(path, data):
        if updating and path.name in mutable_sources:
            path.write_bytes(data)
        else:
            retain(path,data)
    for folder, names in [(STUDIO, ['catalog.json', 'manifest.json', 'engines.json', 'ATTRIBUTION.txt']),
                          (NATIVE, ['engines.json'])]:
        for name in names:
            target = rollback / folder.relative_to(ROOT) / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(folder / name, target)
    source_receipt = read(SOURCE / 'source-acquisition.json')
    for item in source_receipt['files']:
        source_path = SOURCE / Path(item['file'].replace('\\', '/'))
        data = source_path.read_bytes()
        require(sha(data) == item['sha256'], 'Original source changed: ' + source_path.name)
        retain(SOURCE / 'source-printables-123750' / source_path.name, data)
    source_file(SOURCE / 'assembly-review.json', (review / 'assembly-review.json').read_bytes())
    source_file(SOURCE / 'Anubis.glb', raw)
    retain(STUDIO / filename, compressed)
    spec = importlib.util.spec_from_file_location('fleet_sync', ROOT / 'tools/Sync-FleetViewer.py')
    sync = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(sync)
    _, _, maps, rendered = sync.inspect(compressed)
    require(rendered == 198192, 'Rendered triangle count changed')
    unique = sum(doc['accessors'][p['attributes']['POSITION']]['count']//3 for m in doc['meshes'] for p in m['primitives'])
    entry = {k:v for k,v in candidate_entry.items() if not k.startswith('before')}
    entry.update(id='Anubis', title='Anubis / production-reference finish', model=filename, sha256=sha(raw),
                 triangles=rendered, maps=maps, references=[],
                 credit='Geometry: Martin Taichl / Martin / taichl, CC BY 4.0. Source geometry retained; X890 registered fittings, procedural charcoal finish, existing emissive cap materials and separate drive effect. Unofficial fan recreation.',
                 sourceUrl=source_receipt['printables'], licenseUrl=source_receipt['license'],
                 note='Owner-accepted hull, fittings and lighting. Localized red collar emission follows existing cheek caps beneath the raised spine; individual production lenses merge in the glow, so source cap geometry is retained. Blue-cyan glow follows the complete existing inner bow lining, with uneven bright patches, perimeter spill and concealed fills illuminating the real background and port surround. Shared emission-only glow preserves source colours and maps. The owner noted that the result is a bit bright and accepted it as shown; brightness is preserved. Cyan segmented drive and compact blue-white exhaust preserved. The source creator describes this fan kit as bulkier than the production ship; no hull redesign, mirroring or smoothing was applied.',
                 beforeModel=old['model'], beforeSha256=old['sha256'], beforeTitle=old['title'],
                 beforeFlipLongitudinal=old.get('flipLongitudinal', False), beforeEngineId='AnubisOriginal',
                 beforeLabel='Compare earlier Anubis', beforeCredit=old['credit'], beforeNote=old['note'],
                 beforeInfo={'triangles':old['triangles'], 'materials':old['materials']})
    if updating:
        entry.update({k:v for k,v in old.items() if k.startswith('before')})
    catalog[catalog.index(old)] = entry
    require(all(a == b for a,b in zip(old_catalog, catalog) if a['id'] != 'Anubis'), 'Unrequested Studio entry changed')
    if not updating:
        engines['AnubisOriginal'] = old_engines['Anubis']
    engines['Anubis'] = {'engines':[report['engine']], 'finish':'Source-unit outlet and reviewed blue-white exhaust; existing cap emission only'}
    native_engines['Anubis'] = engines['Anubis']
    require(all(native_engines[k] == v for k,v in old_native_engines.items() if k != 'Anubis'), 'Unrequested native drive changed')
    manifest['assets'].append({'file':filename, 'sha256':sha(raw), 'gzip_sha256':sha(compressed),
        'source_sha256':sha(raw), 'binary_sha256':sha(blob), 'triangles':unique, 'rendered_triangles':rendered,
        'materials':len(doc['materials']), 'images':len(doc['images']), 'lights':0,
        'compressed_bytes':len(compressed), 'uncompressed_bytes':len(raw), 'geometry_and_image_bytes_exact':True})
    manifest['revision'] = 'Fleet Studio / accepted Anubis source assembly and production-reference finish / 2026-10-20'
    write(STUDIO/'catalog.json', catalog)
    write(STUDIO/'engines.json', engines)
    write(STUDIO/'manifest.json', manifest)
    write(NATIVE/'engines.json', native_engines)
    attribution = ('\nAnubis / production-reference finish\nMartin Taichl / Martin / taichl; CC BY 4.0.\n'
        + source_receipt['printables'] + '\n' + source_receipt['license']
        + '\nX890 changes: source-part assembly, registered fittings, procedural PBR finish, existing cap emission and separate drive effect. Source hull geometry retained. Unofficial fan model, not official production geometry. Production and paid-model references were inspected only; their images and assets are not redistributed.\n')
    existing_attribution=(STUDIO/'ATTRIBUTION.txt').read_text(encoding='utf-8')
    if attribution.strip() not in existing_attribution:
        with (STUDIO/'ATTRIBUTION.txt').open('a', encoding='utf-8') as out:
            out.write(attribution)
    sync.sync(['Anubis'], rollback/'native')
    require(all(sha((ROOT/p).read_bytes()) == value for p,value in protected.items()), 'Another model asset changed')
    identity = {'glb_sha256':sha(raw), 'accepted_base_sha256':BASE_HASH, 'triangles':rendered,
        'unique_triangles':unique, 'materials':len(doc['materials']), 'images':len(doc['images']),
        'red_cap_faces':red_faces, 'blue_bow_faces':bow_faces, 'license':'CC BY 4.0', 'author':source_receipt['author'],
        'source_url':source_receipt['printables'], 'source_geometry_modified':False,
        'hull_fittings_finish_owner_accepted':'2026-10-20',
        'red_finish_native_validation':'Red-only Fleet Studio review completed earlier; exact 516-face selection preserved',
        'bow_finish_owner_status':'Accepted in Fleet Studio; owner noted a bit bright and accepted as shown' if acceptance else 'Owner review pending',
        'lighting_owner_acceptance': 'Sources/Anubis/owner-acceptance.json' if acceptance else None}
    write(SOURCE/'identity.json', identity)
    write(rollback/'adoption-receipt.json', {**identity, 'rollback':str(rollback),
        'studio_model':filename, 'native_model':'Assets/FleetViewer/models/Anubis.glb.gz',
        'other_model_hashes_preserved':len(protected),
        'owner_visual_acceptance':'Accepted Fleet Studio source review' if acceptance else 'pending',
        'native_rendering':'Main-fleet rendered parity pending; installed sensor app unverified',
        'installed':False, 'ci_dispatched':False, 'published':False})
    print('PASS: only Anubis updated, with exact source geometry/normal/UV preservation, 516 red cap faces and 80 complete inner bow-lining faces; exterior strip restored', flush=True)
    print('Model SHA256: ' + sha(raw), flush=True)
    print('Rollback: ' + str(rollback), flush=True)
    print('Native review, installation and release are separate; none is implied by this source adoption.', flush=True)


if __name__ == '__main__':
    main()
