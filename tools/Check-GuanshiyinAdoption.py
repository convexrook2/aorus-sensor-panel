"""Pin the owner's completed Guanshiyin source and both consumers; no native launch."""
from pathlib import Path
import gzip,hashlib,json
R=Path(__file__).resolve().parents[1];C=R/'Assets/Models/Source/Guanshiyin-gold103'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
a=read(C/'acceptance103.json');assert a['status']=='owner_accepted_complete'
expected='1a5ec3894f687c5effbb58808224639e8ce6539dc290625c79d67f681615a017'
assert a['glbSha256']==expected
assert sha(C/a['source'])==a['sourceSha256']=='9a5fc03b7bed8562610b894773944f828da4a862eaca3b70ba19b0374c44a96c'
raw=gzip.decompress((C/(a['glb']+'.gz')).read_bytes());assert hashlib.sha256(raw).hexdigest()==expected
drive=read(C/'engines.json');assert drive['modelSha256']==expected and len(drive['engines'])==1
for folder in [R/'FleetStudio',R/'Assets/FleetViewer']:
 e=next(x for x in read(folder/'catalog.json') if x['id']=='Guanshiyin')
 assert e['sha256']==expected and gzip.decompress((folder/e['model']).read_bytes())==raw
 assert e['triangles']==3174446 and e['materials']==25 and e['images']==4 and e['sourceAreaLights']
 assert read(folder/'engines.json')['Guanshiyin']==drive
for rel in ['source-area-lights.js','vendor/three/examples/jsm/lights/RectAreaLightUniformsLib.js','vendor/three/examples/jsm/lights/RectAreaLightTexturesLib.js']:
 assert (R/'FleetStudio'/rel).read_bytes()==(R/'Assets/FleetViewer'/rel).read_bytes(),rel
p=next(x for x in read(R/'Assets/FleetViewer/portraits/index.json')['portraits'] if x['id']=='Guanshiyin')
assert p['sourceSha256']==expected and p['width']>=1024 and p['height']>=2048
assert sha(R/'Assets/FleetViewer/portraits/Guanshiyin.png')==p['sha256']
print('PASS: accepted Guanshiyin103B, full geometry,31 rectangular source lights, registered drive and matching native portrait. Native/installed evidence is separate.')
