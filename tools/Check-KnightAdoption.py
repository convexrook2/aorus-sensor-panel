"""Fail closed if the accepted Knight is absent, altered or disconnected."""
import gzip,hashlib,importlib.util,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]; SOURCE=ROOT/'Assets/Models/Source/Knight-review04'
EXPECTED='d6b519e0767e770dc896e8c139892ed2ef5a52e4e0a16bb1e2b9a4bab5cd8e74'
BLEND='de753d8528a51d547564cb67d8f5f3c30c7fff3fc8b968c303007756965504f3'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def require(ok,message):
    if not ok:raise ValueError(message)
require(sha(SOURCE/'Knight.glb')==EXPECTED,'Accepted Knight GLB changed')
require(sha(SOURCE/'Knight.blend')==BLEND,'Accepted Knight Blender changed')
receipt=read(SOURCE/'owner-acceptance.json')
require(receipt['modelSha256']==EXPECTED and receipt['editableBlendSha256']==BLEND,'Acceptance identity differs')
spec=importlib.util.spec_from_file_location('sync',ROOT/'tools/Sync-FleetViewer.py');sync=importlib.util.module_from_spec(spec);spec.loader.exec_module(sync)
raw=(SOURCE/'Knight.glb').read_bytes();_,doc,maps,triangles=sync.inspect(gzip.compress(raw))
require(triangles==968540 and len(doc['materials'])==9 and len(doc['images'])==2,'Accepted geometry/maps differ')
require(len(doc['extensions']['KHR_lights_punctual']['lights'])==17,'Seventeen work lights required')
drive=read(SOURCE/'engines.json')
require(drive['modelSha256']==EXPECTED and len(drive['engines'])==3,'Three registered drives required')
require(all(a['plumeProfile']=='knight' and a['addCore'] is False for a in drive['engines']),'Knight exhaust routing changed')
for folder in [ROOT/'Assets/FleetViewer',ROOT/'FleetStudio']:
    entry=next(x for x in read(folder/'catalog.json') if x['id']=='Knight')
    require(entry['sha256']==EXPECTED and gzip.decompress((folder/entry['model']).read_bytes())==raw,'Packaged Knight is not the accepted model')
    require(entry['triangles']==triangles and entry['renderMaps']==maps and entry['sourceLights']==17 and entry['lightUnitScale']==1/683,'Knight source metadata/light calibration differs')
    require(read(folder/'engines.json')['Knight']==drive,'Packaged Knight outlets differ')
fleet=ROOT/'Assets/FleetViewer';portrait=next(x for x in read(fleet/'portraits/index.json')['portraits'] if x['id']=='Knight')
require(portrait['sourceSha256']==EXPECTED and portrait['sha256']==sha(fleet/'portraits/Knight.png'),'Knight portrait is stale or altered')
require(portrait['width']>=1024 and portrait['height']>=2048,'Knight portrait resolution regressed')
print('PASS: exact accepted Knight Blender/GLB, 968540 triangles, 17 source lights, three registered plumes and full-resolution matching portrait. Source evidence only.')
