"""Read-only source/asset checks. Does not prove native rendering or install."""
import ast
import argparse
import collections
import gzip
import importlib.util
import json
from pathlib import Path
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[1]
sys.dont_write_bytecode=True
spec=importlib.util.spec_from_file_location('anubis_adoption',ROOT/'tools/Adopt-Anubis.py')
adopt=importlib.util.module_from_spec(spec)
spec.loader.exec_module(adopt)
read=lambda path:json.loads((ROOT/path).read_text(encoding='utf-8'))
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--candidate',action='store_true',help='Check the isolated source candidate before main-fleet adoption')
parser.add_argument('--review-root',type=Path,help='Prepared candidate directory; use only with --candidate (default: Sources/Anubis)')
args=parser.parse_args()
candidate=args.candidate
adopt.require(candidate or args.review_root is None,'--review-root requires --candidate')
review=(args.review_root or ROOT/'Sources/Anubis').resolve()
identity=read(review/'assembly-review.json' if candidate else 'Sources/Anubis/identity.json')
studio=next(e for e in read('FleetStudio/catalog.json') if e['id']=='Anubis')
native=next(e for e in read('Assets/FleetViewer/catalog.json') if e['id']=='Anubis')
source_path=review/'Anubis-source-assembly.glb' if candidate else ROOT/'Sources/Anubis/Anubis.glb'
source=source_path.read_bytes()
base=(ROOT/'Sources/Anubis/Anubis-accepted-before-red.glb').read_bytes()
adopt.require(adopt.sha(base)==adopt.BASE_HASH,'Accepted base identity')
adopt.require(adopt.sha(source)==identity['sha256' if candidate else 'glb_sha256'],'Anubis source identity disagrees')
if not candidate:
    adopt.accepted_review(source, ROOT/'Sources/Anubis/runtime-adoption-1.8.13.json')
    adopt.require(adopt.sha(source)==studio['sha256']==native['sha256'],'Anubis fleet identities disagree')
    adopt.require(gzip.decompress((ROOT/'FleetStudio'/studio['model']).read_bytes())==source,'Studio source parity')
    adopt.require(gzip.decompress((ROOT/'Assets/FleetViewer'/native['model']).read_bytes())==source,'Native source parity')
doc,blob=adopt.parse(source)
old,old_blob=adopt.parse(base)
adopt.require(doc['nodes']==old['nodes'] and doc['scenes']==old['scenes'],'Accepted poses changed')
adopt.require(adopt.triangle_fingerprints(doc,blob,True)==adopt.triangle_fingerprints(old,old_blob),'Accepted geometry/normal/UV/material routing changed')
red=sum(doc['accessors'][p['attributes']['POSITION']]['count']//3 for m in doc['meshes'] for p in m['primitives'] if p['material']==5)
bow=sum(doc['accessors'][p['attributes']['POSITION']]['count']//3 for m in doc['meshes'] for p in m['primitives'] if p['material']==6)
adopt.require(red==516 and bow==80 and len(doc['materials'])==7,'Red/bow fixture count or material count')
adopt.require(all(m['name']=='amun10' for m in doc['meshes'] if any(p['material']==6 for p in m['primitives'])),'Blue emission outside inward bow assembly')
red_source=gzip.decompress((ROOT/'Sources/Anubis/Anubis-red-only.glb.gz').read_bytes())
adopt.require(adopt.sha(red_source)==adopt.RED_ONLY_HASH,'Frozen red-only model changed')
red_doc,red_blob=adopt.parse(red_source)
adopt.require(doc['materials'][:6]==red_doc['materials'],'An accepted material changed')
adopt.require(adopt.triangle_fingerprints(doc,blob,remap_bow_only=True)==adopt.triangle_fingerprints(red_doc,red_blob),'Exact red-face membership or another accepted source surface changed')
bow_ids={209,210,211,212,213,214,393,394,395,396,399,487,488,490,491,493,644,645,397,398,574,576,577,578,581,637,634,636,638,640,648,649,571,572,
    100,101,102,103,104,107,406,409,410,411,414,471,562,563,564,565,567,624,477,481,482,485,642,643,619,623,625,629,646,647,403,404,566,568,
    400,401,569,570,391,392,579,580,412,413,560,561}
bow_stl=(ROOT/'Sources/Anubis/source-printables-123750/xxx_-_amun10.stl').read_bytes()
expected_blue=collections.Counter(bow_stl[84+i*50+12:84+i*50+48] for i in bow_ids)
actual_blue=collections.Counter()
for mesh in doc['meshes']:
    for primitive in mesh['primitives']:
        if primitive['material']!=6: continue
        accessor=doc['accessors'][primitive['attributes']['POSITION']]
        view=doc['bufferViews'][accessor['bufferView']]
        offset=view.get('byteOffset',0)+accessor.get('byteOffset',0)
        actual_blue.update(blob[offset+i*12:offset+(i+3)*12] for i in range(0,accessor['count'],3))
adopt.require(actual_blue==expected_blue,'Blue light selected a different source face')
adopt.require(studio['triangles']==native['triangles']==198192,'Rendered triangle count')
engines=read('FleetStudio/engines.json')['Anubis']
adopt.require(engines==read('Assets/FleetViewer/engines.json')['Anubis'],'Drive parity')
anchor=engines['engines'][0]
adopt.require(anchor['position']==[173.6,-30,0] and anchor['direction']==[1,0,0] and anchor['style']=='anubis-production' and anchor['addCore'] is False,'Drive anchor/style')
adopt.require(studio['flipLongitudinal'] and native['flipLongitudinal'] and studio['beforeFlipLongitudinal'] is False,'Source/retained orientation')
acquisition=read('Sources/Anubis/source-acquisition.json')
for item in acquisition['files']:
    path=ROOT/'Sources/Anubis'/item['file'].replace('\\','/')
    adopt.require(adopt.sha(path.read_bytes())==item['sha256'],'Original source changed: '+path.name)
for path in ['tools/Adopt-Anubis.py','tools/Check-AnubisAdoption.py','Sources/Anubis/prepare-review.py','Sources/Anubis/review-finish.py']:
    ast.parse((ROOT/path).read_text(encoding='utf-8'),filename=path)
print('PASS: Anubis '+('isolated candidate' if candidate else 'source/native/Studio parity')+'; 198,192 rendered triangles; exact 516 red cap faces and 80 full-perimeter inward bow-lining faces; all 26 licensed originals preserved.',flush=True)
commands=[[sys.executable,'FleetStudio/tools/verify_assets.py'],[sys.executable,'tools/Sync-FleetViewer.py','--check'],['node','tools/Check-RocinanteRelease.mjs'],['node','tools/Check-AnubisBowEffects.mjs','--model',str(source_path)],['node','tools/Check-FleetLightGlow.mjs']]
commands.extend(['node','--check',path] for path in ['FleetStudio/review.js','FleetStudio/ship-effects.js','FleetStudio/light-glow.js','Assets/FleetViewer/viewer.js','Assets/FleetViewer/rocinante-effects.js','Assets/FleetViewer/light-glow.js'])
for command in commands:
    print('CHECK: '+' '.join(command),flush=True)
    subprocess.run(command,cwd=ROOT,check=True)
print('PASS: Anubis source checks. Native UI, live sensor data, installation, CI and release remain separate evidence.',flush=True)
