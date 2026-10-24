"""Read-only identity/content checks for the owner-admitted Arboghast pass08."""
from pathlib import Path
import gzip,hashlib,json,math,struct,subprocess
ROOT=Path(__file__).resolve().parents[1];FLEET=ROOT/'Assets/FleetViewer'
EXPECTED='10744079e7ee154daac7345bac020ca6f0bf6c256032c9636244baa461333778'
BLEND='05327285e4f3b0a09ddae357efbd7b6f730e946c79b35279a891fc9ae1e3074c'
def sha(data):return hashlib.sha256(data).hexdigest()
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
entry=next(x for x in read(FLEET/'catalog.json') if x['id']=='Arboghast')
raw=gzip.decompress((FLEET/entry['model']).read_bytes());assert sha(raw)==EXPECTED==entry['sha256']
source=ROOT/'Assets/Models/Source/Arboghast-pass08'
assert sha((source/'Arboghast-011-pass08.glb').read_bytes())==EXPECTED
assert sha((source/'Arboghast-011-pass08.blend').read_bytes())==BLEND
assert entry['orientation']=='survey-disc' and entry['localLightShadows'] and math.isclose(entry['lightUnitScale'],1/683)
assert struct.unpack_from('<III',raw)==(0x46546c67,2,len(raw))
n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);nodes=doc['nodes']
lights=doc['extensions']['KHR_lights_punctual']['lights']
assert len(lights)==entry['sourceLights']==8 and all(x['type']=='spot' and x['intensity']>0 for x in lights)
assert sum('partial inner enclosure' in x.get('name','') or 'EMITTER INTERPRETATION' in x.get('name','') for x in nodes)==0
assert sum('low curved inner cowl below lip' in x.get('name','') for x in nodes)==1
assert sum('physical track' in x.get('name','') for x in nodes)==24
assert all(sum(p in x.get('name','') for x in nodes)==4 for p in ['separate concave reflector shell','left T deep changing section','separate curved bell and inner throat'])
row=next(x for x in read(FLEET/'portraits/index.json')['portraits'] if x['id']=='Arboghast')
assert row['sourceSha256']==EXPECTED and sha((FLEET/'portraits/Arboghast.png').read_bytes())==row['sha256']
assert row['width']>=1024 and row['height']>=2048 and entry['triangles']==1165180
subprocess.run(['node',str(ROOT/'tools/Check-ArboghastPlumes.mjs')],cwd=ROOT,check=True)
print('PASS: Arboghast pass08 exact Blender/GLB/portrait identity; upright disc,24 tracks,four dishes/four drives,eight actual spots. Source check; not LIVE/GPU/install proof.')
