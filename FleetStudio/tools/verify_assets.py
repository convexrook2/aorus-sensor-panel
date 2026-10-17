"""Check the exact distributed assets without a GPU or third-party Python deps."""
import gzip,hashlib,json,struct
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
def digest(data):return hashlib.sha256(data).hexdigest()
manifest=json.loads((ROOT/'manifest.json').read_text())
catalog=json.loads((ROOT/'catalog.json').read_text())
assert len(catalog)==1 and catalog[0]['id']=='Pella'
for key in ('current','previous'):
    expected=manifest[key];path=ROOT/expected['file'];compressed=path.read_bytes()
    assert digest(compressed)==expected['gzip_sha256'],f'{key}: compressed checksum'
    raw=gzip.decompress(compressed);assert digest(raw)==expected['sha256'],f'{key}: decoded checksum'
    magic,version,total=struct.unpack_from('<III',raw);assert (magic,version,total)==(0x46546c67,2,len(raw))
    jl,jt=struct.unpack_from('<II',raw,12);assert jt==0x4e4f534a
    doc=json.loads(raw[20:20+jl]);bl,bt=struct.unpack_from('<II',raw,20+jl);assert bt==0x004e4942 and 28+jl+bl==len(raw)
    for view in doc['bufferViews']:
        assert view.get('buffer',0)==0 and view.get('byteOffset',0)+view['byteLength']<=bl
    triangles=0
    for mesh in doc['meshes']:
        assert mesh['primitives']
        for p in mesh['primitives']:
            assert p.get('mode',4)==4
            index=doc['accessors'][p['indices']];assert index['count']%3==0;triangles+=index['count']//3
            positions=doc['accessors'][p['attributes']['POSITION']]
            for a in p['attributes'].values():assert doc['accessors'][a]['count']==positions['count']
    assert triangles==expected['triangles']
    assert len(doc['materials'])==expected['materials'] and len(doc['images'])==expected['images']
    assert all('bufferView'in im and 'uri'not in im for im in doc['images'])
    assert len(doc['extensions']['KHR_lights_punctual']['lights'])==expected['lights']
    field='model' if key=='current' else 'beforeModel';sha='sha256' if key=='current' else 'beforeSha256'
    assert catalog[0][field]==expected['file'] and catalog[0][sha]==expected['sha256']
    print(f'{key}: PASS, {triangles:,} triangles, {expected["lights"]} lights, embedded maps, exact SHA-256')
assert len(json.loads((ROOT/'engines.json').read_text())['Pella']['engines'])==4
print('PASS: four drives, catalogue/current/rollback assets agree; no runtime installation performed.')
