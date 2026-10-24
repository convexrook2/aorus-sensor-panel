"""Verify accepted Canterbury source, packaged model, outlets and portrait identity."""
import gzip,hashlib,json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'Assets/Models/Source/Canterbury-finish14'
EXPECTED='290778c8f5e70e3076e994d8c58d54e375c8438ceacd57728ad4eca6ea9e0d35'
BLEND='7d7d42777e8c3b62d49b0eb74915cba1106918a177b1b74adb18b0cfff4a1a7a'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def require(ok,message):
    if not ok:raise ValueError(message)
require(sha(SOURCE/'Canterbury.glb')==EXPECTED,'Accepted GLB changed')
require(sha(SOURCE/'Canterbury.blend')==BLEND,'Accepted editable Blender changed')
receipt=read(SOURCE/'owner-acceptance.json')
require(receipt['modelSha256']==EXPECTED and receipt['blendSha256']==BLEND,'Acceptance identity changed')
raw=(SOURCE/'Canterbury.glb').read_bytes();n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n])
require(len(doc['meshes'])==212 and len(doc['images'])==3 and len(doc['materials'])==12,'Source content changed')
require(len(doc['extensions']['KHR_lights_punctual']['lights'])==6,'Expected six source lights')
names={n.get('name','') for n in doc['nodes']}
require(all(f'Aft pressure vessel {i:02}' in names for i in range(1,7)),'Six aft vessels missing')
anchors=read(SOURCE/'engines.json')
require(len(anchors)==9 and all(a['plumeProfile']=='canterbury' and a['addCore'] is False and a['direction']==[0,0,-1] for a in anchors),'Accepted outlet registration changed')
for folder in [ROOT/'Assets/FleetViewer',ROOT/'FleetStudio']:
    entry=next(x for x in read(folder/'catalog.json') if x['id']=='Canterbury')
    require(entry['sha256']==EXPECTED and gzip.decompress((folder/entry['model']).read_bytes())==raw,'Packaged source identity differs')
    require(entry['triangles']==112232 and entry['sourceLights']==6 and entry['lightUnitScale']==1,'Source scale/content metadata differs')
    require(read(folder/'engines.json')['Canterbury']==dict(modelSha256=EXPECTED,engines=anchors),'Packaged outlets differ')
fleet=ROOT/'Assets/FleetViewer';row=next(x for x in read(fleet/'portraits/index.json')['portraits'] if x['id']=='Canterbury')
require(row['sourceSha256']==EXPECTED and row['sha256']==sha(fleet/'portraits/Canterbury.png'),'Portrait identity differs')
require(row['width']>=1024 and row['height']>=2048,'Portrait resolution regressed')
print('PASS: accepted Canterbury Blender/GLB/portrait identities, 212 meshes, 112232 triangles, six aft vessels, six lights and nine registered outlets. Native acceptance and publication are separate.')
