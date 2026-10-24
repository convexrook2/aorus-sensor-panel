"""Bounded pass08 Sensor adoption: freeze, model, portrait, check. No install."""
import argparse,gzip,hashlib,importlib.util,json,os,re,shutil,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/arboghast-pass08-adoption-20261022'
FLEET=ROOT/'Assets/FleetViewer'
STUDY=ROOT/'output/arboghast-surface-light-08-20261022'
GLB=ROOT/'Sources/Arboghast/surface-light-08-20261022/model/Arboghast-011-surface-light-08.glb'
EXPECTED='10744079e7ee154daac7345bac020ca6f0bf6c256032c9636244baa461333778'
CANONICAL=ROOT/'Assets/Models/Source/Arboghast-pass08'
ALLOWED={
 'Assets/FleetViewer/catalog.json','Assets/FleetViewer/manifest.json','Assets/FleetViewer/ATTRIBUTION.txt',
 'Assets/FleetViewer/models/Arboghast.glb.gz','Assets/FleetViewer/portraits/Arboghast.png','Assets/FleetViewer/portraits/index.json',
 'Assets/FleetViewer/viewer.js','Assets/FleetViewer/engines.json','SensorPanel/App.cs','SensorPanel/FleetPortraitExport.cs',
 'SensorPanel/ShipArtwork.cs','SensorPanel/ShipPoster.cs','SensorPanel/Holotable.cs','README.md','docs/RELEASE-FLOW.md',
 'SensorPanel/SensorPanel.csproj','FleetStudio.Desktop/FleetStudio.Desktop.csproj','Packaging/README.txt','Packaging/SensorPanel.iss',
 'SensorPanel/NativeQualityCheck.cs','SensorPanel/FleetViewport.cs','tools/Run-CI.ps1'}
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def write(p,x):Path(p).write_text(json.dumps(x,indent=2)+'\n',encoding='utf-8')
def manifest():
    prior=read(FLEET/'manifest.json');prior['files']=[{'path':p.relative_to(FLEET).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(FLEET.rglob('*')) if p.is_file() and p.name!='manifest.json']
    write(FLEET/'manifest.json',prior)
def freeze():
    assert not OUT.exists(),'Use the existing freeze receipt; do not overwrite it'
    assert subprocess.check_output(['git','branch','--show-current'],cwd=ROOT,text=True).strip()=='main'
    assert sha(GLB)==EXPECTED
    OUT.mkdir(parents=True);backups=[]
    for rel in sorted(ALLOWED):
        src=ROOT/rel;dest=OUT/'before'/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,dest)
        assert sha(src)==sha(dest);backups.append({'path':rel,'sha256':sha(src)})
    protected={}
    folders=[ROOT/'Sources/Arboghast',ROOT/'FleetStudio',FLEET]+list((ROOT/'output').glob('arboghast*'))
    for folder in folders:
        for p in folder.rglob('*'):
            if p.is_file() and p.relative_to(ROOT).as_posix() not in ALLOWED:
                rel=p.relative_to(ROOT).as_posix();protected[rel]={'path':rel,'bytes':p.stat().st_size,'sha256':sha(p)}
    settings=Path(os.environ['LOCALAPPDATA'])/'X890Sensor/settings.json'
    receipt={'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
      'authority':'Owner: include it now and procceed; source integration/checks, before publication or install',
      'candidateSha256':EXPECTED,'backups':backups,'protected':list(protected.values()),
      'nativeCatalogBefore':read(FLEET/'catalog.json'),'portraitIndexBefore':read(FLEET/'portraits/index.json'),
      'settings':{'path':str(settings),'sha256':sha(settings) if settings.is_file() else None}}
    write(OUT/'before.json',receipt);print('ARBO_ADOPTION_FROZEN',len(protected),'protected files;',len(backups),'recoverable product paths',flush=True)
def model():
    before=read(OUT/'before.json');assert sha(GLB)==EXPECTED==read(STUDY/'owner-confirmation.json')['glbSha256']
    spec=importlib.util.spec_from_file_location('fleet_sync',ROOT/'tools/Sync-FleetViewer.py');sync=importlib.util.module_from_spec(spec);spec.loader.exec_module(sync)
    raw=GLB.read_bytes();payload=gzip.compress(raw,compresslevel=9,mtime=0);decoded,doc,maps,triangles=sync.inspect(payload)
    assert decoded==raw and triangles==1165180
    CANONICAL.mkdir(exist_ok=True)
    for source,name in [(GLB,'Arboghast-011-pass08.glb'),(GLB.with_suffix('.blend'),'Arboghast-011-pass08.blend')]:
        target=CANONICAL/name
        if target.exists():assert sha(target)==sha(source),'Canonical source differs: '+name
        else:shutil.copyfile(source,target)
    write(CANONICAL/'provenance.json',{'generation':'surface-light-08','modelSha256':EXPECTED,
      'blendSha256':sha(GLB.with_suffix('.blend')),'author':'Original X890 reconstruction',
      'reference':'Chris Danelon production turntable; owner-supplied OBS recording',
      'referenceUrl':'https://www.youtube.com/watch?v=vefkFrWLuNI&t=131s',
      'ownerAuthority':'Owner confirmed pass08 and requested Sensor inclusion on2026-10-22',
      'limits':'Dimensions, hidden fittings and photometry are authored fits; eight repeated lamps is an inferred count',
      'originalStudyPreserved':True})
    catalog=read(FLEET/'catalog.json');old=next(x for x in catalog if x['id']=='Arboghast')
    credit='Original X890 fan reconstruction after Chris Danelon production turntable references; owner-confirmed pass08. No production mesh acquired.'
    entry={**old,'title':'UNS Arboghast /011 reconstruction pass08','credit':credit,
      'note':'Owner-confirmed pass08: bowed roof, recessed instrument well with low return, separate equipment and eight warm work lights. Exact dimensions, hidden fittings and photometry remain authored fits.',
      'referenceUrl':'https://www.youtube.com/watch?v=vefkFrWLuNI&t=131s','orientation':'survey-disc',
      'triangles':triangles,'materials':len(doc['materials']),'images':len(doc.get('images',[])),
      'maps':maps,'renderMaps':maps,'sha256':EXPECTED,'sourceBytes':len(raw),'compressedBytes':len(payload),
      'compressedSha256':hashlib.sha256(payload).hexdigest(),'compression':'gzip-lossless',
      'lightUnitScale':1/683,'localLightShadows':True,'sourceLights':8,
      'editableSource':(CANONICAL/'Arboghast-011-pass08.blend').relative_to(ROOT).as_posix(),'adoption':'Owner requested inclusion 2026-10-22; source integration; installed/release checks separate'}
    catalog[catalog.index(old)]=entry
    assert all(x['id']=='Arboghast' or x==next(y for y in before['nativeCatalogBefore'] if y['id']==x['id']) for x in catalog)
    (FLEET/entry['model']).write_bytes(payload);write(FLEET/'catalog.json',catalog)
    credits=(FLEET/'ATTRIBUTION.txt').read_text(encoding='utf-8')
    prior_block='Arboghast: '+old['credit']+'\n'+old.get('sourceUrl',old.get('referenceUrl',''))
    assert credits.count(prior_block)==1
    credits=credits.replace(prior_block,'Arboghast: '+credit+'\n'+entry['referenceUrl'])
    (FLEET/'ATTRIBUTION.txt').write_text(credits,encoding='utf-8');manifest()
    previous=next(x for x in before['nativeCatalogBefore'] if x['id']=='Arboghast')
    write(OUT/'model-adoption.json',{'sourceSha256':EXPECTED,'previousSha256':previous['sha256'],'entry':entry,'lossless':True,'installed':False})
    print('ARBO_MODEL_ADOPTED',triangles,'triangles;8 actual lights;',EXPECTED,flush=True)
def portrait():
    export=OUT/'portrait-export';index=read(export/'index.json');assert len(index['portraits'])==1
    row=index['portraits'][0];assert row['id']=='Arboghast' and row['sourceSha256']==EXPECTED and row['width']>=1024 and row['height']>=2048
    assert sha(export/'Arboghast.png')==row['sha256']
    current=read(FLEET/'portraits/index.json');old=next(r for r in current['portraits'] if r['id']=='Arboghast')
    current['portraits'][current['portraits'].index(old)]={k:row[k] for k in ['id','sourceSha256','sha256','width','height']}
    shutil.copyfile(export/'Arboghast.png',FLEET/'portraits/Arboghast.png');write(FLEET/'portraits/index.json',current);manifest()
    print('ARBO_NATIVE_PORTRAIT_ADOPTED',row['width'],row['height'],flush=True)
def check():
    before=read(OUT/'before.json');changed=[]
    for x in read(OUT/'version-before/receipt.json'):
        original=(OUT/'version-before'/x['path']).read_text(encoding='utf-8-sig')
        assert (ROOT/x['path']).read_text(encoding='utf-8-sig')==original.replace('1.8.11','1.8.12'),'Unexpected version-file edit: '+x['path']
    protected={x['path']:x for x in before['protected'] if x['path'] not in ALLOWED}
    for x in read(STUDY/'protected-before.json')['files']:
        rel=Path(x['path']).as_posix()
        if rel not in ALLOWED and rel not in protected:protected[rel]={**x,'path':rel}
    for x in protected.values():
        p=ROOT/x['path']
        if not p.is_file() or p.stat().st_size!=x['bytes'] or sha(p)!=x['sha256']:changed.append(x['path'])
    assert not changed,changed[:10]
    catalog=read(FLEET/'catalog.json');assert len(catalog)==len(before['nativeCatalogBefore'])
    old_engines=json.loads(subprocess.check_output(['git','show',before['head']+':Assets/FleetViewer/engines.json'],cwd=ROOT,text=True))
    engines=read(FLEET/'engines.json')
    assert set(engines)==set(old_engines)|{'Arboghast'} and all(engines[k]==v for k,v in old_engines.items())
    assert engines['Arboghast']['modelSha256']==EXPECTED and len(engines['Arboghast']['engines'])==4
    assert all(x['id']=='Arboghast' or x==next(y for y in before['nativeCatalogBefore'] if y['id']==x['id']) for x in catalog)
    portraits=read(FLEET/'portraits/index.json')['portraits'];assert len(portraits)==len(before['portraitIndexBefore']['portraits'])
    assert all(x['id']=='Arboghast' or x==next(y for y in before['portraitIndexBefore']['portraits'] if y['id']==x['id']) for x in portraits)
    row=next(x for x in catalog if x['id']=='Arboghast');assert hashlib.sha256(gzip.decompress((FLEET/row['model']).read_bytes())).hexdigest()==EXPECTED
    assert row['orientation']=='survey-disc' and row['sourceLights']==8 and row['lightUnitScale']==1/683 and row['localLightShadows']
    assert sha(GLB)==EXPECTED and subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()==before['head']
    settings=before['settings'];settings_path=Path(settings['path']);settings_bytes=settings_path.read_bytes() if settings_path.is_file() else None
    settings_now=hashlib.sha256(settings_bytes).hexdigest() if settings_bytes is not None else None
    cycle_only=False;prior_ship=None
    if settings_bytes is not None:
        ship_field=re.search(rb'("ExpanseShip"\s*:\s*")([^"\\]*)(")',settings_bytes)
        if ship_field:
            for ship in before['nativeCatalogBefore']:
                candidate=settings_bytes[:ship_field.start(2)]+ship['id'].encode('ascii')+settings_bytes[ship_field.end(2):]
                if hashlib.sha256(candidate).hexdigest()==settings['sha256']:
                    cycle_only=True;prior_ship=ship['id'];break
    result={'passed':True,'sourceSha256':EXPECTED,'protectedFiles':len(protected),'other21ShipsUnchanged':True,
      'other21PortraitsUnchanged':True,'settingsUnchanged':settings_now==settings['sha256'],
      'preferencesExceptCyclingShipUnchanged':cycle_only,'priorCyclingShip':prior_ship,
      'settingsEvidence':'Exact prior settings hash reconstructed by changing only ExpanseShip' if cycle_only else 'Byte hash comparison only; changes not attributed',
      'settingsSha256':settings_now,'installed':False,'published':False}
    write(OUT/'preservation-result.json',result);print('ARBO_ADOPTION_PRESERVED',json.dumps(result),flush=True)
if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('phase',choices=['freeze','model','portrait','check']);args=parser.parse_args()
    globals()[args.phase]()
