"""Check the accepted ships in both consumers; no native launch or installation."""
from pathlib import Path
import gzip, hashlib, json

ROOT = Path(__file__).resolve().parents[1]
SHIPS = {
    'Pella': ('v27', 'df080b0037ceb485ad4c8005e63c1a4b471171c60bf276ae3ea8ef365cce6d78', 1388395, 34),
    'Scirocco': ('paint02', '7cbe6ab1719ded37da2de370a51072ccc9c252b82f26097c7b8b145659563320', 460489, 14),
}
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(data): return hashlib.sha256(data).hexdigest()

for ship, (revision, expected, triangles, lights) in SHIPS.items():
    canon = ROOT/'Assets/Models/Source'/f'{ship}-{revision}'
    raw = gzip.decompress((canon/(ship+'.glb.gz')).read_bytes())
    if (canon/(ship+'.glb')).exists():
        assert (canon/(ship+'.glb')).read_bytes() == raw, ship+' local editable GLB differs'
    accepted = read(canon/'owner-acceptance.json')
    assert sha(raw) == expected == accepted['modelSha256'], ship+' accepted model changed'
    assert sha((canon/(ship+'.blend')).read_bytes()) == accepted['editableBlendSha256'], ship+' accepted Blender changed'
    drive = read(canon/'engines.json')
    assert drive['modelSha256'] == expected and len(drive['engines']) == 4
    for folder in [ROOT/'FleetStudio', ROOT/'Assets/FleetViewer']:
        entry = next(e for e in read(folder/'catalog.json') if e['id'] == ship)
        payload = (folder/entry['model']).read_bytes()
        assert gzip.decompress(payload) == raw and entry['sha256'] == expected, ship+' consumer model differs'
        assert sha(payload) == entry['compressedSha256'] and entry['triangles'] == triangles
        assert entry['sourceLights'] == lights and entry['lightUnitScale'] == 1/683
        assert read(folder/'engines.json')[ship] == drive, ship+' drive registration differs'
    native = ROOT/'Assets/FleetViewer'
    portrait = next(e for e in read(native/'portraits/index.json')['portraits'] if e['id'] == ship)
    assert portrait['sourceSha256'] == expected and sha((native/'portraits'/(ship+'.png')).read_bytes()) == portrait['sha256']
    assert portrait['width'] >= 1024 and portrait['height'] >= 2048
    print(f'PASS: {ship} {revision}, exact accepted geometry/paint, {lights} lights, four registered drives and matching portrait in both consumers.')
print('Source identity checks only; native rendering, installed state and release are separate evidence.')
