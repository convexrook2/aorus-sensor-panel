"""Check the exact distributed assets without a GPU or third-party Python deps."""
import gzip,hashlib,json,struct
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
def digest(data):return hashlib.sha256(data).hexdigest()
def require(condition,message):
    """Keep validation active even when Python is invoked with -O."""
    if not condition:raise ValueError(message)
manifest=json.loads((ROOT/'manifest.json').read_text())
catalog=json.loads((ROOT/'catalog.json').read_text())
require(len(catalog)==1 and catalog[0]['id']=='Pella','Expected one Pella catalogue entry')
for key in ('current','previous'):
    expected=manifest[key];path=ROOT/expected['file'];compressed=path.read_bytes()
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
    require(len(doc['materials'])==expected['materials'] and len(doc['images'])==expected['images'],f'{key}: material/image counts')
    require(all('bufferView'in im and 'uri'not in im for im in doc['images']),f'{key}: non-embedded image')
    require(len(doc['extensions']['KHR_lights_punctual']['lights'])==expected['lights'],f'{key}: point light count')
    field='model' if key=='current' else 'beforeModel';sha='sha256' if key=='current' else 'beforeSha256'
    require(catalog[0][field]==expected['file'] and catalog[0][sha]==expected['sha256'],f'{key}: catalogue/manifest mismatch')
    print(f'{key}: PASS, {triangles:,} triangles, {expected["lights"]} lights, embedded maps, exact SHA-256')
require(len(json.loads((ROOT/'engines.json').read_text())['Pella']['engines'])==4,'Expected four drive anchors')
print('PASS: four drives, catalogue/current/rollback assets agree; no runtime installation performed.')
