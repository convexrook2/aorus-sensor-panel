"""Adopt the owner-accepted Pella v27 and Scirocco paint02 without rebuilding them."""
from pathlib import Path
import argparse, copy, gzip, hashlib, importlib.util, json, shutil, subprocess

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / 'Assets/FleetViewer'
STUDIO = ROOT / 'FleetStudio'
OUT = ROOT / 'artifacts/pella-scirocco-adoption-20261024'
SHIPS = {
    'Pella': dict(base='Sources/Pella/candidate/Pella-v27-restored',
                  blend='Sources/Pella/candidate/Pella-v27-source-relief.blend', revision='v27',
                  sha='df080b0037ceb485ad4c8005e63c1a4b471171c60bf276ae3ea8ef365cce6d78',
                  triangles=1388395, lights=34),
    'Scirocco': dict(base='Sources/Scirocco/selected-paint02/Scirocco-selected-paint02',
                     blend='Sources/Scirocco/selected-paint02/Scirocco-selected-paint02.blend', revision='paint02',
                     sha='7cbe6ab1719ded37da2de370a51072ccc9c252b82f26097c7b8b145659563320',
                     triangles=460489, lights=14),
}

def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def read(p): return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def write(p, value):
    Path(p).parent.mkdir(parents=True, exist_ok=True)
    Path(p).write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')
def row(folder, ship): return next(e for e in read(folder/'catalog.json') if e['id'] == ship)
def canonical(ship): return ROOT/'Assets/Models/Source'/f'{ship}-{SHIPS[ship]["revision"]}'
def inspected(ship):
    s = SHIPS[ship]
    payload = (ROOT/(s['base']+'.glb.gz')).read_bytes()
    spec = importlib.util.spec_from_file_location('sync', ROOT/'tools/Sync-FleetViewer.py')
    sync = importlib.util.module_from_spec(spec); spec.loader.exec_module(sync)
    raw, doc, maps, triangles = sync.inspect(payload)
    assert hashlib.sha256(raw).hexdigest() == s['sha'] == sha(ROOT/(s['base']+'.glb'))
    assert raw == (ROOT/(s['base']+'.glb')).read_bytes() and triangles == s['triangles']
    assert len(doc.get('extensions', {}).get('KHR_lights_punctual', {}).get('lights', [])) == s['lights']
    return payload, raw, doc, maps, triangles

def freeze():
    assert not OUT.exists(), 'Existing frozen baseline must be retained'
    for ship in SHIPS: inspected(ship)
    OUT.mkdir(parents=True)
    files = {p.relative_to(ROOT).as_posix(): sha(p) for folder in [NATIVE, STUDIO] for p in folder.rglob('*') if p.is_file()}
    names = ['catalog.json', 'manifest.json', 'engines.json', 'ATTRIBUTION.txt']
    paths = [folder/name for folder in [NATIVE, STUDIO] for name in names]
    paths += [NATIVE/'viewer.js', STUDIO/'review.js', NATIVE/'portraits/index.json']
    paths += [NATIVE/f'{kind}/{ship}.{ext}' for ship in SHIPS for kind, ext in [('models', 'glb.gz'), ('portraits', 'png')]]
    for p in paths:
        dest = OUT/'before'/p.relative_to(ROOT); dest.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(p, dest)
    write(OUT/'before.json', dict(head=subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
        dirty=subprocess.check_output(['git', 'status', '--short'], text=True), files=files,
        nativeCatalog=read(NATIVE/'catalog.json'), studioCatalog=read(STUDIO/'catalog.json'),
        nativeEngines=read(NATIVE/'engines.json'), studioEngines=read(STUDIO/'engines.json'),
        portraits=read(NATIVE/'portraits/index.json')))
    print('Frozen existing fleet, portraits and outlet registrations:', len(files), 'files')

def model():
    baseline = read(OUT/'before.json')
    for ship, s in SHIPS.items():
        payload, raw, doc, maps, triangles = inspected(ship)
        canon = canonical(ship); canon.mkdir(parents=True, exist_ok=True)
        (canon/(ship+'.glb.gz')).write_bytes(payload)
        for source, name in [(ROOT/(s['base']+'.glb'), ship+'.glb'), (ROOT/s['blend'], ship+'.blend')]:
            dest = canon/name
            if dest.exists(): assert sha(dest) == sha(source)
            else: shutil.copy2(source, dest)
        write(canon/'owner-acceptance.json', dict(date='2026-10-24', ship=ship, revision=s['revision'],
            ownerInstruction='accepted updated fleet and sensor panel with the updated ships, both pella and scirocco',
            modelSha256=s['sha'], editableBlendSha256=sha(canon/(ship+'.blend')),
            scope='Accepted model, paint and lighting; integrate into Fleet Studio and SensorPanel. No geometry rebuild.'))
        entry = read(ROOT/'Sources/Scirocco/selected-paint02/catalog-entry.json') if ship == 'Scirocco' else row(STUDIO, ship)
        common = {k: v for k, v in entry.items() if not k.startswith('before') and k not in ('references', 'model')}
        common.update(title=f'{ship} / accepted {s["revision"]}', sha256=s['sha'], triangles=triangles,
            materials=len(doc['materials']), images=len(doc.get('images', [])), renderMaps=maps,
            sourceBytes=len(raw), compressedBytes=len(payload), compressedSha256=hashlib.sha256(payload).hexdigest(),
            compression='gzip-lossless', sourceLights=s['lights'], lightUnitScale=1/683, localLightShadows=True,
            editableSource=(canon/(ship+'.blend')).relative_to(ROOT).as_posix(),
            adoption='Owner accepted and requested Fleet Studio and SensorPanel integration on 2026-10-24')
        common['note'] = common['note'].replace(' Local paint review, not installed.', ' Owner-accepted paint02; original selected hull geometry retained.')
        drive = copy.deepcopy(baseline['studioEngines']['Pella']) if ship == 'Pella' else read(ROOT/'Sources/Scirocco/selected-paint02/engines.json')['Scirocco']
        drive.update(modelSha256=s['sha'], finish=f'Owner-accepted {s["revision"]}; four source-fitted drive outlets')
        assert len(drive['engines']) == 4
        write(canon/'engines.json', drive)
        for folder, key in [(NATIVE, 'native'), (STUDIO, 'studio')]:
            catalog = read(folder/'catalog.json'); old = next(e for e in catalog if e['id'] == ship)
            new = {**old, **common}; new.pop('references', None)
            new['model'] = f'models/{ship}.glb.gz' if folder == NATIVE else f'models/{ship}-{s["sha"][:12]}.glb.gz'
            if folder == STUDIO and ship == 'Pella':
                new.update(beforeTitle='Pella / previous v26', beforeNote='Previous Pella before the accepted source-relief restoration.', beforeEngineId='PellaBeforeV27')
            catalog[catalog.index(old)] = new
            (folder/new['model']).write_bytes(payload); write(folder/'catalog.json', catalog)
            engines = read(folder/'engines.json'); engines[ship] = drive
            if folder == STUDIO and ship == 'Pella':
                previous = copy.deepcopy(baseline['studioEngines']['Pella']); previous['modelSha256'] = new['beforeSha256']
                engines['PellaBeforeV27'] = previous
            write(folder/'engines.json', engines)
            credits = (folder/'ATTRIBUTION.txt').read_text(encoding='utf-8')
            if ship == 'Scirocco':
                prior = next(e for e in baseline[key+'Catalog'] if e['id'] == ship)
                old_block = ship+': '+prior['credit']+'\n'+prior.get('sourceUrl', prior.get('referenceUrl', ''))
                new_block = ship+': '+new['credit']+'\n'+new['sourceUrl']+'\nCC BY 4.0: https://creativecommons.org/licenses/by/4.0/'
                if old_block in credits: credits = credits.replace(old_block, new_block)
                elif new_block not in credits: credits += '\n'+new_block+'\n'
                (folder/'ATTRIBUTION.txt').write_text(credits, encoding='utf-8')
        print('Adopted exact', ship, s['sha'], triangles, 'triangles;', s['lights'], 'lights; four drives')
    p = NATIVE/'viewer.js'; text = p.read_text(encoding='utf-8')
    text = text.replace("['Rocinante','Anubis','Arboghast','Canterbury','Knight']", "['Rocinante','Anubis','Arboghast','Canterbury','Knight','Pella','Scirocco']")
    text = text.replace("['Arboghast','Canterbury','Knight']", "['Arboghast','Canterbury','Knight','Pella','Scirocco']")
    p.write_text(text, encoding='utf-8', newline='\n')
    p = STUDIO/'review.js'; text = p.read_text(encoding='utf-8')
    text = text.replace("['Arboghast','Canterbury','Knight']", "['Arboghast','Canterbury','Knight','Pella','Scirocco']")
    text = text.replace("['RocinanteS6','Anubis','Knight']", "['RocinanteS6','Anubis','Knight','Pella','Scirocco']")
    p.write_text(text, encoding='utf-8')
    manifests()

def manifests():
    m = read(NATIVE/'manifest.json')
    m['files'] = [dict(path=p.relative_to(NATIVE).as_posix(), bytes=p.stat().st_size, sha256=sha(p)) for p in sorted(NATIVE.rglob('*')) if p.is_file() and p.name != 'manifest.json']
    write(NATIVE/'manifest.json', m)
    m = read(STUDIO/'manifest.json')
    for ship, s in SHIPS.items():
        payload, raw, doc, maps, triangles = inspected(ship); entry = row(STUDIO, ship)
        unique = sum(doc['accessors'][p.get('indices', p['attributes']['POSITION'])]['count']//3 for mesh in doc['meshes'] for p in mesh['primitives'])
        item = dict(file=entry['model'], sha256=s['sha'], source_sha256=s['sha'], gzip_sha256=entry['compressedSha256'],
            compressed_bytes=len(payload), uncompressed_bytes=len(raw), triangles=unique, rendered_triangles=triangles,
            materials=entry['materials'], images=entry['images'], lights=s['lights'], accepted_export_bytes_exact=True)
        old = next((e for e in m['assets'] if e['file'] == item['file']), None)
        if old: old.update(item)
        else: m['assets'].append(item)
    write(STUDIO/'manifest.json', m)

def portraits():
    index = read(NATIVE/'portraits/index.json')
    for ship, s in SHIPS.items():
        folder = OUT/('portrait-'+ship); rows = read(folder/'index.json')['portraits']; assert len(rows) == 1
        entry = rows[0]; assert entry['id'] == ship and entry['sourceSha256'] == s['sha']
        assert entry['width'] >= 1024 and entry['height'] >= 2048 and sha(folder/(ship+'.png')) == entry['sha256']
        old = next(e for e in index['portraits'] if e['id'] == ship)
        index['portraits'][index['portraits'].index(old)] = {k: entry[k] for k in ['id', 'sourceSha256', 'sha256', 'width', 'height']}
        shutil.copy2(folder/(ship+'.png'), NATIVE/'portraits'/(ship+'.png'))
    write(NATIVE/'portraits/index.json', index); manifests()

def check():
    b = read(OUT/'before.json')
    for folder, key in [(NATIVE, 'native'), (STUDIO, 'studio')]:
        catalog = read(folder/'catalog.json'); engines = read(folder/'engines.json')
        assert len(catalog) == len(b[key+'Catalog'])
        assert all(e == next(v for v in catalog if v['id'] == e['id']) for e in b[key+'Catalog'] if e['id'] not in SHIPS)
        assert all(engines[k] == v for k, v in b[key+'Engines'].items() if k not in SHIPS)
        for ship, s in SHIPS.items():
            e = next(v for v in catalog if v['id'] == ship)
            assert hashlib.sha256(gzip.decompress((folder/e['model']).read_bytes())).hexdigest() == s['sha']
            assert engines[ship]['modelSha256'] == s['sha'] and len(engines[ship]['engines']) == 4
            assert sha(canonical(ship)/(ship+'.glb')) == s['sha']
            assert sha(canonical(ship)/(ship+'.blend')) == sha(ROOT/s['blend'])
    portraits = read(NATIVE/'portraits/index.json')['portraits']
    assert all(e == next(v for v in portraits if v['id'] == e['id']) for e in b['portraits']['portraits'] if e['id'] not in SHIPS)
    allowed = {'Assets/FleetViewer/'+n for n in ['catalog.json','manifest.json','engines.json','ATTRIBUTION.txt','viewer.js','portraits/index.json']}
    allowed |= {'Assets/FleetViewer/'+k+'/'+s+'.'+ext for s in SHIPS for k, ext in [('models','glb.gz'), ('portraits','png')]}
    allowed |= {'FleetStudio/'+n for n in ['catalog.json','manifest.json','engines.json','ATTRIBUTION.txt','review.js','README.md']}
    changed = [p for p, h in b['files'].items() if p not in allowed and sha(ROOT/p) != h]; assert not changed, changed
    result = dict(result='PASS', acceptedExportsAndBlendsUnchanged=True, otherShipModelsCatalogsEnginesAndPortraitsUnchanged=True,
        protectedFiles=len(b['files'])-len(allowed), sourceModels={s:v['sha'] for s,v in SHIPS.items()})
    write(OUT/'preservation.json', result); print(json.dumps(result))

if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('phase', choices=['freeze','model','manifests','portraits','check'])
    globals()[parser.parse_args().phase]()
