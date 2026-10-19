"""Package the fleet as editable GLBs and offline Razorback-style viewers."""
from pathlib import Path
import argparse, base64, hashlib, json, re, zipfile

ROOT=Path(__file__).resolve().parents[1]


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--razorback-template-zip',type=Path)
    parser.add_argument('--output',type=Path,default=ROOT/'artifacts/Expanse_Fleet_Models_v1')
    args=parser.parse_args()
    template_path=ROOT/'tools/FleetViewer.template.html'
    if not template_path.exists():
        if not args.razorback_template_zip:raise ValueError('The initial template import requires the owner Razorback v2 archive.')
        with zipfile.ZipFile(args.razorback_template_zip) as z:
            template=z.read('Razorback_Digital_Model_v2/source/viewer_template.html').decode('utf-8')
        template=template.replace('Razorback v2 — blueprint shape correction','__MODEL_ID__ / original fleet model')
        template=template.replace('the Razorback spacecraft','the selected spacecraft')
        template=template.replace('Blueprint-corrected digital asset / 02','Original fan models / fleet 01')
        template=template.replace('<h1>RAZORBACK</h1>','<h1>__MODEL_ID__</h1>')
        template=template.replace('Reshaped from your model and the existing orthographic blueprint.','Reference-guided geometry and original paint. Drag to inspect every side.')
        template=template.replace("a.download='Razorback_Blueprint_v2.glb'","a.download='__MODEL_ID__.glb'")
        template=re.sub(r'const presets=\{.*?\};', 'const presets=__PRESETS__;',template,count=1)
        template=template.replace('hh=10.5*','hh=__HALF_SPAN__*')
        template=template.replace('Math.max(4,Math.min(65,','Math.max(.22,Math.min(65,')
        template=template.replace('/^(Hull|Nose|Forward bay)/.test(p.name)','/^(paint|white|accent)$/.test(p.name)')
        template=template.replace("p.name.startsWith('FX')","p.name==='blue'")
        template=template.replace('Blueprint profile','Orthographic profile').replace('Orthographic blueprint profile','Orthographic profile')
        template=template.replace('Razorback','Fleet model')
        # Remove Razorback-specific construction notes, retaining the working
        # camera, GLB loader, PBR shader and interactive controls unchanged.
        template=re.sub(r'<div class="micro">.*?</div>','<div class="micro">Independent fan approximation; unseen details interpreted. Model units are not certified physical dimensions.</div>',template,flags=re.S)
        template_path.write_text(template,encoding='utf-8')
    template=template_path.read_text(encoding='utf-8')
    src=ROOT/'Assets/Models/Source/Fleet'
    report=json.loads((src/'build-report.json').read_text())
    out=args.output; out.mkdir(parents=True,exist_ok=True)
    records=report['models']
    for item in records:
        name=item['id'];lo,hi=item['bounds'];center=[(a+b)/2 for a,b in zip(lo,hi)]
        span=max(b-a for a,b in zip(lo,hi))
        def preset(yaw,pitch,radius,target=None):
            return dict(yaw=yaw,pitch=pitch,roll=0,radius=span*radius,target=center if target is None else target)
        tail=[center[0],lo[1]+span*.12,center[2]]
        nose=[center[0],hi[1]-span*.12,center[2]]
        presets=dict(hero=preset(.58,.24,1.95),face=preset(0,0,1.8),blueprint=preset(0,0,1.8),
                     rear=preset(.35,-1.28,1.4,tail),engine=preset(.30,-.45,.82,tail),nose=preset(.28,1.20,1.05,nose))
        html=template.replace('__MODEL_ID__',name).replace('__TRIANGLES__',f"{item['triangles']:,}")
        html=html.replace('__OBJECTS__',str(item['materials'])).replace('__PRESETS__',json.dumps(presets))
        html=html.replace('__HALF_SPAN__',str(span*.56))
        html=html.replace('__MODEL_BASE64__',base64.b64encode((src/(name+'.glb')).read_bytes()).decode('ascii'))
        if name=='Pella':
            html=html.replace('/^(paint|white|accent)$/.test(p.name)','/^(paint|armor|black)$/.test(p.name)')
            html=html.replace('Original fan models / fleet 01','Adapted fan mesh / CC BY 4.0')
            html=html.replace('Reference-guided geometry and original paint. Drag to inspect every side.','Geometry by Martin Taichl. Reduced and repainted for this panel.')
            html=html.replace('Independent fan approximation; unseen details interpreted. Model units are not certified physical dimensions.','Martin Taichl / Printables model 468982 / CC BY 4.0. See MODEL-CREDITS.txt for source, licence and adaptation details.')
        (out/(name+'.html')).write_text(html,encoding='utf-8')
    buttons=''.join(f'<button data-model="{r["id"]}" aria-pressed="false">{r["id"]}</button>' for r in records)
    index='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Expanse fleet / original 3D models</title>
<style>*{box-sizing:border-box}body{margin:0;background:#080e15;color:#e4edf3;font:14px system-ui;display:grid;grid-template-columns:200px 1fr;height:100vh}aside{padding:24px 18px;border-right:1px solid #24313d;overflow:auto}h1{font-size:16px;font-weight:600;margin:0 0 6px}p{font-size:11px;color:#91a2ae;line-height:1.6;margin:0 0 20px}button{display:block;width:100%;text-align:left;background:#101c27;border:1px solid #263b49;border-radius:4px;color:#a9bdcb;padding:10px 12px;margin-bottom:6px;cursor:pointer}button[aria-pressed=true]{border-color:#7dd9e9;background:#163340;color:#e0faff}button:focus-visible{outline:2px solid #b5f1ff;outline-offset:2px}iframe{width:100%;height:100%;border:0}@media(max-width:640px){body{grid-template-columns:135px 1fr}aside{padding:15px 10px}button{padding:9px 7px}}</style>
<aside><h1>THE EXPANSE</h1><p>14 original fan models and configurations.<br>Orbit, inspect and download each editable GLB.</p>__BUTTONS__<p>Reference-guided approximations. No production meshes or image pixels imported.</p></aside><iframe id="viewer" title="Interactive 3D spacecraft" src="Tynan.html"></iframe>
<script>const buttons=[...document.querySelectorAll('[data-model]')];function select(id){document.querySelector('#viewer').src=id+'.html';buttons.forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.model===id)));}buttons.forEach(b=>b.onclick=()=>select(b.dataset.model));buttons[0].setAttribute('aria-pressed','true');</script></html>'''.replace('__BUTTONS__',buttons)
    index=index.replace('14 original fan models and configurations.','14 fan models and configurations. Pella adapts Martin Taichl’s CC BY 4.0 mesh.')
    (out/'Fleet_Interactive_Preview.html').write_text(index,encoding='utf-8')
    package_files={}
    for path in [*src.iterdir(),ROOT/'tools/Build-ExpanseFleet.py',ROOT/'tools/PellaModel.py',ROOT/'tools/PellaMesh.py',ROOT/'tools/Convert-HoloScene.py',ROOT/'tools/Build-FleetReview.py',ROOT/'tools/Update-FleetProvenance.py',ROOT/'Assets/Models/sources.json',template_path]:
        if path.is_file():package_files[str(path.relative_to(ROOT)).replace('\\','/')]=path
    for item in records:
        path=ROOT/'Assets/Models'/(item['id']+'.ship');package_files[str(path.relative_to(ROOT)).replace('\\','/')]=path
    for path in out.glob('*.html'):package_files[path.name]=path
    package_files['MODEL-CREDITS.txt']=ROOT/'Assets/Models/ATTRIBUTION.txt'
    archive=out.with_suffix('.zip')
    with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for name,path in sorted(package_files.items()):z.write(path,'Expanse_Fleet_Models_v1/'+name)
        z.writestr('Expanse_Fleet_Models_v1/requirements-build.txt','numpy\nPillow\ntrimesh==5.1.1\npymeshlab==2025.7.post1\n')
        checksums='\n'.join(hashlib.sha256(path.read_bytes()).hexdigest()+'  '+name for name,path in sorted(package_files.items()))+'\n'
        z.writestr('Expanse_Fleet_Models_v1/SHA256SUMS.txt',checksums)
    result=dict(archive=str(archive),bytes=archive.stat().st_size,sha256=hashlib.sha256(archive.read_bytes()).hexdigest(),
                models=len(records),viewer=str(out/'Fleet_Interactive_Preview.html'),files=len(package_files)+2)
    (out/'package-report.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))


if __name__=='__main__':main()
