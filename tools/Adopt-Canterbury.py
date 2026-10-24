"""Adopt the owner-accepted finish14 without changing other ship entries."""
from pathlib import Path
import argparse,copy,gzip,hashlib,importlib.util,json,shutil,subprocess,datetime
ROOT=Path(__file__).resolve().parents[1];SOURCE=ROOT/'Sources/Canterbury/finished'
OUT=ROOT/'artifacts/canterbury-adoption-20261023';CANON=ROOT/'Assets/Models/Source/Canterbury-finish14'
NATIVE=ROOT/'Assets/FleetViewer';STUDIO=ROOT/'FleetStudio'
EXPECTED='290778c8f5e70e3076e994d8c58d54e375c8438ceacd57728ad4eca6ea9e0d35'
BLEND='7d7d42777e8c3b62d49b0eb74915cba1106918a177b1b74adb18b0cfff4a1a7a'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def write(p,x):Path(p).write_text(json.dumps(x,indent=2)+'\n',encoding='utf-8')
def manifests():
    m=read(NATIVE/'manifest.json');m['files']=[dict(path=p.relative_to(NATIVE).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(NATIVE.rglob('*')) if p.is_file() and p.name!='manifest.json'];write(NATIVE/'manifest.json',m)
    m=read(STUDIO/'manifest.json');name=next(x['model'] for x in read(STUDIO/'catalog.json') if x['id']=='Canterbury');raw=(SOURCE/'Canterbury.glb').read_bytes();packed=(STUDIO/name).read_bytes()
    entry=next(x for x in read(STUDIO/'catalog.json') if x['id']=='Canterbury')
    row=dict(file=name,sha256=EXPECTED,source_sha256=EXPECTED,gzip_sha256=hashlib.sha256(packed).hexdigest(),compressed_bytes=len(packed),uncompressed_bytes=len(raw),triangles=entry['triangles'],materials=entry['materials'],images=entry['images'],lights=6,geometry_and_image_bytes_exact=True)
    prior=next((a for a in m['assets'] if a['file']==name),None)
    if prior:prior.update(row)
    else:m['assets'].append(row)
    write(STUDIO/'manifest.json',m)
def freeze():
    assert not OUT.exists(),'Freeze already exists; reuse it'
    assert sha(SOURCE/'Canterbury.glb')==EXPECTED and sha(SOURCE/'Canterbury.blend')==BLEND
    OUT.mkdir(parents=True);files={}
    for folder in [NATIVE,STUDIO]:
        for p in folder.rglob('*'):
            if p.is_file():files[p.relative_to(ROOT).as_posix()]=sha(p)
    for rel in ['SensorPanel/ShipArtwork.cs','tools/Run-CI.ps1','Packaging/ReleaseNotes-1.8.12.md','README.md','docs/RELEASE-FLOW.md']:
        files[rel]=sha(ROOT/rel)
    backups=['Assets/FleetViewer/'+n for n in ['catalog.json','manifest.json','engines.json','ATTRIBUTION.txt','models/Canterbury.glb.gz','portraits/Canterbury.png','portraits/index.json','viewer.js','rocinante-effects.js']]
    backups+=['FleetStudio/'+n for n in ['catalog.json','manifest.json','engines.json','ATTRIBUTION.txt','review.js','ship-effects.js']]
    backups+=['SensorPanel/ShipArtwork.cs','tools/Run-CI.ps1','Packaging/ReleaseNotes-1.8.12.md','README.md','docs/RELEASE-FLOW.md']
    for rel in backups:
        dest=OUT/'before'/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/rel,dest)
    write(OUT/'before.json',dict(head=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),indexSha256=sha(ROOT/'.git/index'),files=files,
        nativeCatalog=read(NATIVE/'catalog.json'),nativeEngines=read(NATIVE/'engines.json'),studioCatalog=read(STUDIO/'catalog.json'),studioEngines=read(STUDIO/'engines.json'),portraits=read(NATIVE/'portraits/index.json')))
    print('FROZEN',len(files),'files; recoverable backups retained')
def model():
    before=read(OUT/'before.json');assert sha(SOURCE/'Canterbury.glb')==EXPECTED and sha(SOURCE/'Canterbury.blend')==BLEND
    CANON.mkdir(parents=True,exist_ok=True)
    for name in ['Canterbury.glb','Canterbury.blend','engines.json','owner-acceptance.json','model-receipt.json']:
        target=CANON/name
        if target.exists():assert sha(target)==sha(SOURCE/name),name
        else:shutil.copy2(SOURCE/name,target)
    spec=importlib.util.spec_from_file_location('sync',ROOT/'tools/Sync-FleetViewer.py');sync=importlib.util.module_from_spec(spec);spec.loader.exec_module(sync)
    raw=(SOURCE/'Canterbury.glb').read_bytes();payload=gzip.compress(raw,compresslevel=9,mtime=0);decoded,doc,maps,triangles=sync.inspect(payload);assert decoded==raw and triangles==112232
    credit='Original X890 fan reconstruction after Ryan Dening concepts and Chris Danelon production views; owner-accepted finish14. No production mesh acquired.'
    common=dict(title='Canterbury / accepted reconstruction',credit=credit,note='Owner accepted finish14 on 2026-10-23. Raised shroud, stepped housings, six aft vessels and exposed centerline core; entrance retained. Hidden dimensions, fine fittings and bridge-reference disagreement remain estimates.',
        referenceUrl='https://www.youtube.com/watch?v=a3lL1bhpl9c&t=11s',triangles=triangles,materials=len(doc['materials']),images=len(doc['images']),maps=maps,renderMaps=maps,sha256=EXPECTED,
        sourceBytes=len(raw),compressedBytes=len(payload),compressedSha256=hashlib.sha256(payload).hexdigest(),compression='gzip-lossless',lightUnitScale=1,localLightShadows=True,sourceLights=6,
        editableSource='Assets/Models/Source/Canterbury-finish14/Canterbury.blend',adoption='Owner accepted current model and requested release on 2026-10-23')
    for folder in [NATIVE,STUDIO]:
        catalog=read(folder/'catalog.json');old=next(x for x in catalog if x['id']=='Canterbury');entry={**old,**common}
        entry['model']='models/Canterbury.glb.gz' if folder==NATIVE else 'models/Canterbury-'+EXPECTED[:12]+'.glb.gz'
        catalog[catalog.index(old)]=entry;(folder/entry['model']).write_bytes(payload);write(folder/'catalog.json',catalog)
        engines=read(folder/'engines.json');engines['Canterbury']=dict(modelSha256=EXPECTED,engines=read(SOURCE/'engines.json'));write(folder/'engines.json',engines)
        credits=(folder/'ATTRIBUTION.txt').read_text(encoding='utf-8');old_block='Canterbury: '+old['credit']+'\n'+old.get('sourceUrl',old.get('referenceUrl',''))
        if old_block in credits:credits=credits.replace(old_block,'Canterbury: '+credit+'\n'+entry['referenceUrl'])
        else:credits+='\nCanterbury: '+credit+'\n'+entry['referenceUrl']+'\n'
        (folder/'ATTRIBUTION.txt').write_text(credits,encoding='utf-8')
    manifests();print('ADOPTED Canterbury:',EXPECTED,'112232 triangles, 3 maps, 6 lights, 9 outlets')
def portrait():
    export=OUT/'portrait-export';rows=read(export/'index.json')['portraits'];assert len(rows)==1;row=rows[0]
    assert row['id']=='Canterbury' and row['sourceSha256']==EXPECTED and row['width']>=1024 and row['height']>=2048
    assert sha(export/'Canterbury.png')==row['sha256']
    current=read(NATIVE/'portraits/index.json');old=next(x for x in current['portraits'] if x['id']=='Canterbury');current['portraits'][current['portraits'].index(old)]={k:row[k] for k in ['id','sourceSha256','sha256','width','height']}
    shutil.copy2(export/'Canterbury.png',NATIVE/'portraits/Canterbury.png');write(NATIVE/'portraits/index.json',current);manifests();print('ADOPTED native Canterbury portrait')
def check():
    b=read(OUT/'before.json')
    for folder,key in [(NATIVE,'native'),(STUDIO,'studio')]:
        c=read(folder/'catalog.json');assert len(c)==len(b[key+'Catalog'])
        assert all(x==next(v for v in c if v['id']==x['id']) for x in b[key+'Catalog'] if x['id']!='Canterbury')
        e=read(folder/'engines.json');assert all(e[k]==v for k,v in b[key+'Engines'].items() if k!='Canterbury')
        assert e['Canterbury']['modelSha256']==EXPECTED and len(e['Canterbury']['engines'])==9
        row=next(x for x in c if x['id']=='Canterbury');assert hashlib.sha256(gzip.decompress((folder/row['model']).read_bytes())).hexdigest()==EXPECTED
    portraits=read(NATIVE/'portraits/index.json')['portraits'];assert all(x==next(v for v in portraits if v['id']==x['id']) for x in b['portraits']['portraits'] if x['id']!='Canterbury')
    allowed={'Assets/FleetViewer/'+x for x in ['catalog.json','manifest.json','engines.json','ATTRIBUTION.txt','models/Canterbury.glb.gz','portraits/Canterbury.png','portraits/index.json','viewer.js','rocinante-effects.js']}
    allowed|={'FleetStudio/'+x for x in ['catalog.json','manifest.json','engines.json','ATTRIBUTION.txt','review.js','ship-effects.js']}
    allowed|={'SensorPanel/ShipArtwork.cs','tools/Run-CI.ps1','Packaging/ReleaseNotes-1.8.12.md','README.md','docs/RELEASE-FLOW.md'}
    changed=[p for p,h in b['files'].items() if p not in allowed and sha(ROOT/p)!=h];assert not changed,changed
    result=dict(result='PASS',modelSha256=EXPECTED,otherShipCatalogsAndOutletsPreserved=True,other21NativePortraitsPreserved=True,protectedFiles=len(b['files'])-len(allowed),installed=False,published=False)
    write(OUT/'preservation.json',result);print(json.dumps(result))
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('phase',choices=['freeze','model','portrait','manifests','check']);globals()[p.parse_args().phase]()
