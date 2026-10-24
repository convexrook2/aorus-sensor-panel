"""Freeze and adopt the exact accepted Knight review 04; never rebuild its model."""
from pathlib import Path
import argparse, gzip, hashlib, importlib.util, json, shutil, subprocess

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'Sources/Knight'; REVIEW=SOURCE/'DesktopReview/FleetStudio'
CANON=ROOT/'Assets/Models/Source/Knight-review04'
OUT=ROOT/'artifacts/knight-adoption-20261023'
NATIVE=ROOT/'Assets/FleetViewer'; STUDIO=ROOT/'FleetStudio'
EXPECTED='d6b519e0767e770dc896e8c139892ed2ef5a52e4e0a16bb1e2b9a4bab5cd8e74'
BLEND='de753d8528a51d547564cb67d8f5f3c30c7fff3fc8b968c303007756965504f3'

def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def read(p): return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def write(p,x): Path(p).write_text(json.dumps(x,indent=2)+'\n',encoding='utf-8')
def accepted():
    assert sha(SOURCE/'candidate/Knight-production-reference.glb')==EXPECTED
    assert sha(SOURCE/'candidate/Knight-production-reference.blend')==BLEND
    r=read(SOURCE/'acceptance.json')
    assert r['modelSha256']==EXPECTED and r['editableBlendSha256']==BLEND

def freeze():
    accepted(); assert not OUT.exists(),'Reuse the existing frozen baseline'
    OUT.mkdir(parents=True)
    files={p.relative_to(ROOT).as_posix():sha(p) for folder in [NATIVE,STUDIO,SOURCE] for p in folder.rglob('*') if p.is_file()}
    backups=['catalog.json','manifest.json','engines.json','ATTRIBUTION.txt','viewer.js','rocinante-effects.js','models/Knight.glb.gz','portraits/Knight.png','portraits/index.json']
    for rel in ['Assets/FleetViewer/'+n for n in backups]+['FleetStudio/'+n for n in ['catalog.json','manifest.json','engines.json','ATTRIBUTION.txt','review.js','ship-effects.js']]:
        dst=OUT/'before'/rel; dst.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(ROOT/rel,dst)
    write(OUT/'before.json',dict(head=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),files=files,
        nativeCatalog=read(NATIVE/'catalog.json'),studioCatalog=read(STUDIO/'catalog.json'),
        nativeEngines=read(NATIVE/'engines.json'),studioEngines=read(STUDIO/'engines.json'),portraits=read(NATIVE/'portraits/index.json')))
    print('Frozen',len(files),'files; accepted Knight and earlier fleet retained')

def manifests():
    m=read(NATIVE/'manifest.json'); m['files']=[dict(path=p.relative_to(NATIVE).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(NATIVE.rglob('*')) if p.is_file() and p.name!='manifest.json'];write(NATIVE/'manifest.json',m)
    entry=next(x for x in read(STUDIO/'catalog.json') if x['id']=='Knight')
    m=read(STUDIO/'manifest.json')
    row=dict(file=entry['model'],sha256=EXPECTED,source_sha256=EXPECTED,gzip_sha256=entry['compressedSha256'],compressed_bytes=entry['compressedBytes'],uncompressed_bytes=entry['sourceBytes'],triangles=entry['triangles'],materials=9,images=2,lights=17,geometry_and_image_bytes_exact=True)
    old=next((x for x in m['assets'] if x['file']==row['file']),None)
    if old: old.update(row)
    else: m['assets'].append(row)
    write(STUDIO/'manifest.json',m)

def model():
    accepted(); assert (OUT/'before.json').exists(); CANON.mkdir(parents=True,exist_ok=True)
    for src,name in [('candidate/Knight-production-reference.glb','Knight.glb'),('candidate/Knight-production-reference.blend','Knight.blend'),('acceptance.json','owner-acceptance.json'),('candidate/model-receipt.json','model-receipt.json'),('source-acquisition.json','source-acquisition.json')]:
        dest=CANON/name
        if dest.exists(): assert sha(dest)==sha(SOURCE/src)
        else: shutil.copy2(SOURCE/src,dest)
    drive=read(REVIEW/'engines.json')['Knight']; assert drive['modelSha256']==EXPECTED and len(drive['engines'])==3
    write(CANON/'engines.json',drive)
    spec=importlib.util.spec_from_file_location('sync',ROOT/'tools/Sync-FleetViewer.py'); sync=importlib.util.module_from_spec(spec);spec.loader.exec_module(sync)
    raw=(CANON/'Knight.glb').read_bytes(); payload=gzip.compress(raw,compresslevel=9,mtime=0)
    decoded,doc,maps,triangles=sync.inspect(payload);assert decoded==raw and triangles==968540
    review=next(x for x in read(REVIEW/'catalog.json') if x['id']=='Knight')
    common=dict(title='Knight / accepted silver hull',credit=review['credit'],referenceUrl=review['referenceUrl'],
        note='Owner-accepted review 04: silver aluminium, service wear, physical hull relief and reconstructed markings. Exact accepted geometry and maps, seventeen work lights and three fitted canted drives. Original font, fine print and hidden details remain approximate.',
        triangles=triangles,materials=9,images=2,maps=maps,renderMaps=maps,sha256=EXPECTED,
        sourceBytes=len(raw),compressedBytes=len(payload),compressedSha256=hashlib.sha256(payload).hexdigest(),compression='gzip-lossless',
        lightUnitScale=1/683,localLightShadows=True,sourceLights=17,
        editableSource='Assets/Models/Source/Knight-review04/Knight.blend',adoption='Owner accepted review 04 and requested app inclusion and corrective release on 2026-10-23')
    for folder in [NATIVE,STUDIO]:
        catalog=read(folder/'catalog.json'); old=next(x for x in catalog if x['id']=='Knight'); entry={**old,**common}
        entry['model']='models/Knight.glb.gz' if folder==NATIVE else 'models/Knight-'+EXPECTED[:12]+'.glb.gz'
        catalog[catalog.index(old)]=entry; (folder/entry['model']).write_bytes(payload);write(folder/'catalog.json',catalog)
        engines=read(folder/'engines.json');engines['Knight']=drive;write(folder/'engines.json',engines)
        credits=(folder/'ATTRIBUTION.txt').read_text(encoding='utf-8')
        block='Knight: '+old['credit']+'\n'+old.get('sourceUrl',old.get('referenceUrl',''))
        new='Knight: '+entry['credit']+'\n'+entry['referenceUrl']+'\nOriginal licensed sources: https://www.thingiverse.com/thing:2060044 and https://www.thingiverse.com/thing:1235700\nCC BY 4.0: https://creativecommons.org/licenses/by/4.0/'
        baseline=(OUT/'before'/folder.relative_to(ROOT)/'ATTRIBUTION.txt').read_text(encoding='utf-8')
        prior=next(x for x in read(OUT/'before'/folder.relative_to(ROOT)/'catalog.json') if x['id']=='Knight')
        block='Knight: '+prior['credit']+'\n'+prior.get('sourceUrl',prior.get('referenceUrl',''))
        credits=baseline.replace(block,new) if block in baseline else baseline+'\n'+new+'\n'
        (folder/'ATTRIBUTION.txt').write_text(credits,encoding='utf-8')
    manifests(); print('Adopted unchanged Knight',EXPECTED,'968540 triangles, 17 lights, 3 outlets')

def portrait():
    export=OUT/'portrait-export'; rows=read(export/'index.json')['portraits'];assert len(rows)==1
    row=rows[0];assert row['id']=='Knight' and row['sourceSha256']==EXPECTED and row['width']>=1024 and row['height']>=2048
    assert sha(export/'Knight.png')==row['sha256']
    index=read(NATIVE/'portraits/index.json'); old=next(x for x in index['portraits'] if x['id']=='Knight')
    index['portraits'][index['portraits'].index(old)]={k:row[k] for k in ['id','sourceSha256','sha256','width','height']}
    shutil.copy2(export/'Knight.png',NATIVE/'portraits/Knight.png');write(NATIVE/'portraits/index.json',index);manifests()

def check():
    accepted(); b=read(OUT/'before.json')
    for folder,key in [(NATIVE,'native'),(STUDIO,'studio')]:
        c=read(folder/'catalog.json');assert len(c)==len(b[key+'Catalog'])
        assert all(x==next(v for v in c if v['id']==x['id']) for x in b[key+'Catalog'] if x['id']!='Knight')
        e=read(folder/'engines.json');assert all(e[k]==v for k,v in b[key+'Engines'].items() if k!='Knight')
    portraits=read(NATIVE/'portraits/index.json')['portraits']
    assert all(x==next(v for v in portraits if v['id']==x['id']) for x in b['portraits']['portraits'] if x['id']!='Knight')
    allowed={'Assets/FleetViewer/'+x for x in ['catalog.json','manifest.json','engines.json','ATTRIBUTION.txt','models/Knight.glb.gz','portraits/Knight.png','portraits/index.json','viewer.js','rocinante-effects.js']}
    allowed|={'FleetStudio/'+x for x in ['catalog.json','manifest.json','engines.json','ATTRIBUTION.txt','review.js','ship-effects.js']}
    changed=[p for p,h in b['files'].items() if p not in allowed and sha(ROOT/p)!=h];assert not changed,changed
    result=dict(result='PASS',acceptedModelUnchanged=True,acceptedBlendUnchanged=True,isolatedReviewUnchanged=True,otherShipCatalogsAndOutletsPreserved=True,other21PortraitsPreserved=True,protectedFiles=len(b['files'])-len(allowed))
    write(OUT/'preservation.json',result);print(json.dumps(result))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('phase',choices=['freeze','model','portrait','manifests','check']);globals()[p.parse_args().phase]()
