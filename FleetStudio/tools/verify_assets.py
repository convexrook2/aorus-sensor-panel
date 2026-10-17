"""Check the hosted fleet assets without a GPU or third-party Python deps."""
import gzip,hashlib,json,math,struct
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
def digest(data):return hashlib.sha256(data).hexdigest()
def require(condition,message):
    """Keep validation active even when Python is invoked with -O."""
    if not condition:raise ValueError(message)
manifest=json.loads((ROOT/'manifest.json').read_text())
catalog=json.loads((ROOT/'catalog.json').read_text())
require(bool(catalog) and len({e['id'] for e in catalog})==len(catalog),'Empty or duplicate catalogue')
assets=manifest.get('assets',[manifest['current'],manifest['previous']])
by_file={e['file']:e for e in assets}
require(len(by_file)==len(assets),'Duplicate manifest files')
for expected in assets:
    key=expected['file'];path=(ROOT/key).resolve()
    require(path.is_relative_to(ROOT.resolve()),f'{key}: file outside package')
    compressed=path.read_bytes()
    require(digest(compressed)==expected['gzip_sha256'],f'{key}: compressed checksum')
    raw=gzip.decompress(compressed);require(digest(raw)==expected['sha256'],f'{key}: decoded checksum')
    magic,version,total=struct.unpack_from('<III',raw);require((magic,version,total)==(0x46546c67,2,len(raw)),f'{key}: GLB header')
    jl,jt=struct.unpack_from('<II',raw,12);require(jt==0x4e4f534a,f'{key}: JSON chunk')
    doc=json.loads(raw[20:20+jl]);bl,bt=struct.unpack_from('<II',raw,20+jl);require(bt==0x004e4942 and 28+jl+bl==len(raw),f'{key}: binary chunk')
    for view in doc['bufferViews']:
        require(view.get('buffer',0)==0 and view.get('byteOffset',0)+view['byteLength']<=bl,f'{key}: buffer view bounds')
    triangles=0
    for mesh in doc['meshes']:
        require(bool(mesh['primitives']),f'{key}: empty mesh')
        for p in mesh['primitives']:
            require(p.get('mode',4)==4,f'{key}: non-triangle primitive')
            index=doc['accessors'][p['indices']];require(index['count']%3==0,f'{key}: incomplete triangle');triangles+=index['count']//3
            positions=doc['accessors'][p['attributes']['POSITION']]
            for a in p['attributes'].values():require(doc['accessors'][a]['count']==positions['count'],f'{key}: attribute count')
    require(triangles==expected['triangles'],f'{key}: triangle count')
    require(len(doc.get('materials',[]))==expected['materials'] and len(doc.get('images',[]))==expected['images'],f'{key}: material/image counts')
    require(all('bufferView'in im and 'uri'not in im for im in doc.get('images',[])),f'{key}: non-embedded image')
    require(len(doc.get('extensions',{}).get('KHR_lights_punctual',{}).get('lights',[]))==expected['lights'],f'{key}: point light count')
    if 'binary_sha256'in expected:require(digest(raw[28+jl:28+jl+bl])==expected['binary_sha256'],f'{key}: source geometry/image bytes')
    print(f'{key}: PASS, {triangles:,} triangles, {expected["lights"]} lights, embedded maps, exact SHA-256')
engines=json.loads((ROOT/'engines.json').read_text())
for entry in catalog:
    for field,sha in [('model','sha256'),('beforeModel','beforeSha256')]:
        if not entry.get(field):continue
        require(entry[field] in by_file,f'{entry["id"]}: asset absent from manifest')
        require(entry[sha]==by_file[entry[field]]['sha256'],f'{entry["id"]}: catalogue/manifest mismatch')
    require(not entry.get('references'),f'{entry["id"]}: local reference image in hosted catalogue')
    require(entry['id'] in engines,f'{entry["id"]}: missing engine record')
    anchors=engines[entry['id']]['engines']
    require(bool(anchors) or entry['id']=='SolRing',f'{entry["id"]}: missing drive anchors')
    for a in anchors:
        require(len(a['position'])==len(a['direction'])==3 and all(math.isfinite(x) for x in a['position']+a['direction']),f'{entry["id"]}: invalid anchor')
        require(a['radius']>0 and a['length']>0,f'{entry["id"]}: invalid plume dimensions')
    if entry.get('beforeEngineId'):require(entry['beforeEngineId'] in engines,f'{entry["id"]}: previous drive anchors missing')
require(len(engines['Pella']['engines'])==4,'Expected four Pella drive anchors')
require(sum(p.stat().st_size for p in ROOT.rglob('*') if p.is_file())<900*1024*1024,'Static package exceeds hosting allowance')
print(f'PASS: {len(catalog)} catalogue entries, {len(assets)} exact assets, independent drive anchors; no runtime installation performed.')
