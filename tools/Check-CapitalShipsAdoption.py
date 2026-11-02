"""Pin accepted Donnager V6B and Agatha V16 source, runtime, lights and portraits."""
from pathlib import Path
import gzip,hashlib,json,struct
R=Path(__file__).resolve().parents[1]
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(b):return hashlib.sha256(b).hexdigest()
for ident,rev,digest,triangles,lamps,drives in [('Donnager','v6b','51e1a585ef18ea407c7699a94b7d973043efbb94d16c1590bacc60140e8f36a9',1848436,64,4),('AgathaKing','v16d','fedbfda735582b899231d79f23676550dbeee2d3652aa8b602e19623993880d8',1250901,54,6)]:
 c=R/'Assets/Models/Source'/f'{ident}-{rev}';a=read(c/'owner-acceptance.json');assert a['status']=='owner_accepted' and a['glbSha256']==digest
 assert sha((c/a['editableSource']).read_bytes())==a['editableSha256']
 raw=gzip.decompress((c/f'{ident}-{rev}.glb.gz').read_bytes());assert sha(raw)==digest
 n=struct.unpack_from('<I',raw,12)[0];d=json.loads(raw[20:20+n]);assert len(d['extensions']['KHR_lights_punctual']['lights'])==lamps
 engine=read(c/'engines.json');assert engine['modelSha256']==digest and len(engine['engines'])==drives
 for folder in [R/'FleetStudio',R/'Assets/FleetViewer']:
  e=next(x for x in read(folder/'catalog.json') if x['id']==ident)
  assert e['sha256']==digest and gzip.decompress((folder/e['model']).read_bytes())==raw
  assert e['triangles']==triangles and e['lights']==lamps
  assert read(folder/'engines.json')[ident]==engine
 p=next(x for x in read(R/'Assets/FleetViewer/portraits/index.json')['portraits'] if x['id']==ident)
 assert p['sourceSha256']==digest and p['width']>=1024 and p['height']>=2048
 assert sha((R/f'Assets/FleetViewer/portraits/{ident}.png').read_bytes())==p['sha256']
 print(f'PASS: accepted {ident} {rev}, {triangles:,} triangles, {lamps} source lights, {drives} drives and exact native portrait.')
