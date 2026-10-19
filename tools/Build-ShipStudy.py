"""Create one offline ship study without overwriting the accepted fleet previews."""
from pathlib import Path
import argparse
import base64
import hashlib
import html
import json
import math
import random
import re
import shutil

ROOT = Path(__file__).resolve().parents[1]


def space_background():
    rng = random.Random(82017)
    stars = ''.join(f'<circle cx="{rng.randrange(1920)}" cy="{rng.randrange(1080)}" r="{rng.choice([.35,.45,.6,.8,1.0])}" fill="#a5b9ca" opacity="{rng.uniform(.08,.45):.2f}"/>' for _ in range(280))
    svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="1920" height="1080"><defs>
    <radialGradient id="a" cx="64%" cy="48%" r="65%"><stop stop-color="#112432"/><stop offset=".6" stop-color="#070e17"/><stop offset="1" stop-color="#03070b"/></radialGradient>
    <radialGradient id="b" cx="88%" cy="23%" r="65%"><stop stop-color="#512e1c" stop-opacity=".24"/><stop offset="1" stop-color="#050a10" stop-opacity="0"/></radialGradient></defs>
    <path fill="url(#a)" d="M0 0H1920V1080H0Z"/><path fill="url(#b)" d="M0 0H1920V1080H0Z"/>{stars}</svg>'''
    return 'data:image/svg+xml;base64,' + base64.b64encode(svg.encode()).decode()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--ship', required=True)
    parser.add_argument('--title')
    parser.add_argument('--study-dir', required=True, type=Path)
    parser.add_argument('--preview-root', type=Path,
                        help='Optional dedicated public preview root; writes only this ship subfolder.')
    parser.add_argument('--reference-url', default='https://www.artstation.com/artwork/xYGE4O')
    parser.add_argument('--credit', default='Original fan reconstruction from Lee Fitzgerald / North Front Studio production drawings.')
    args = parser.parse_args()
    if not re.fullmatch(r'[A-Za-z][A-Za-z0-9]*', args.ship):
        parser.error('Use the exact alphanumeric ship identity.')
    stage = args.study_dir.resolve()
    source = stage / 'source'
    output = args.preview_root.resolve() / args.ship if args.preview_root else stage / 'preview'
    output.mkdir(parents=True, exist_ok=True)
    report = json.loads((source / 'build-report.json').read_text())
    model = next(m for m in report['models'] if m['id'] == args.ship)
    lo, hi = model['bounds']
    center = [(a+b)/2 for a,b in zip(lo,hi)]
    span = max(b-a for a,b in zip(lo,hi))
    aspect = max(hi[0]-lo[0], hi[2]-lo[2]) / (hi[1]-lo[1])
    def preset(yaw, pitch, radius, target=None):
        return dict(yaw=yaw,pitch=pitch,radius=span*radius,roll=0,target=target or center)
    tail = [center[0], lo[1]+span*.17, center[2]]
    top = [center[0], hi[1]-span*.18, center[2]]
    presets = dict(hero=preset(.62,.14,1.63),face=preset(0,0,1.58),side=preset(math.pi/2,0,1.58),
        rear=preset(-.5,-1.2,1.35,tail),engine=preset(.45,-.7,.64,tail),
        nose=preset(.4,.4,.8,top),blueprint=preset(0,0,1.6),top=preset(.1,1.5,1.1))
    if args.ship == 'Guanshiyin':
        # Leave room for the long bow antennae and aft lips above the controls.
        presets.update(hero=preset(.62,.14,1.72),face=preset(math.pi,0,1.76),
            side=preset(math.pi/2,0,1.76),blueprint=preset(math.pi,0,1.76))
    title = args.title or args.ship
    raw = (source/(args.ship+'.glb')).read_bytes()
    viewer = (ROOT/'tools/FleetViewer.template.html').read_text(encoding='utf-8')
    replacements = {'__MODEL_ID__':html.escape(title),'__TRIANGLES__':f"{model['triangles']:,}",
        '__OBJECTS__':str(model['materials']),'__MODEL_BASE64__':base64.b64encode(raw).decode(),
        '__PRESETS__':json.dumps(presets),'__HALF_SPAN__':str(span*.57)}
    for key,value in replacements.items(): viewer=viewer.replace(key,value)
    # glTF defaults to repeating UVs; the native ImageBrush also tiles. Clamping
    # here flattened the upper hull's wear into a single texture-edge colour.
    viewer=viewer.replace('(doc.images||[]).map(async im=>', '(doc.images||[]).map(async (im,imageIndex)=>')
    viewer=viewer.replace('const tex=gl.createTexture();',
        'const textureDef=(doc.textures||[]).find(t=>t.source===imageIndex);const sampler=doc.samplers?.[textureDef?.sampler]||{};const tex=gl.createTexture();')
    viewer=viewer.replace('gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE', 'gl.TEXTURE_WRAP_S,sampler.wrapS??gl.REPEAT')
    viewer=viewer.replace('gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE', 'gl.TEXTURE_WRAP_T,sampler.wrapT??gl.REPEAT')
    viewer=viewer.replace('Original fan models / fleet 01','PRODUCTION REFERENCE STUDY')
    viewer=viewer.replace('Reference-guided geometry and original paint. Drag to inspect every side.', 'Reference proportions · Authored materials · Drag to inspect')
    viewer=viewer.replace('Independent fan approximation; unseen details interpreted. Model units are not certified physical dimensions.', html.escape(args.credit)+' Unseen details are interpreted.')
    viewer=viewer.replace('named objects','material groups')
    viewer=viewer.replace("spin:false,hull:true,glow:false", "spin:true,hull:true,glow:true,studio:true")
    viewer=viewer.replace('id="spin" aria-pressed="false">Auto-rotate','id="spin" class="active" aria-pressed="true">Pause rotation')
    viewer=viewer.replace('id="glow" aria-pressed="false">Drive off','id="glow" class="active" aria-pressed="true">Drive on')
    viewer=viewer.replace('if(k===\'hull\')', "if(k==='spin')$('#spin').textContent=state.spin?'Pause rotation':'Auto-rotate';if(k==='hull')")
    viewer=viewer.replace("Math.max(1,1.05/(w/h))",f"Math.max(1,{aspect*1.08:.5f}/(w/h))")
    viewer=viewer.replace("Math.max(1,1/aspect)",f"Math.max(1,{aspect*1.08:.5f}/aspect)")
    viewer=viewer.replace("ortho=name==='blueprint';", "ortho=['blueprint','side','top'].includes(name);state.spin=false;$('#spin').textContent='Auto-rotate';$('#spin').setAttribute('aria-pressed','false');$('#spin').classList.remove('active');")
    viewer=viewer.replace("blueprint:'Orthographic profile'", "blueprint:'Front orthographic',side:'Side orthographic',top:'Top orthographic'")
    viewer=viewer.replace('Orthographic profile</button>', 'Front orthographic</button>\n<button data-view="side"><small>07</small> Side orthographic</button>\n<button data-view="top"><small>08</small> Top orthographic</button>')
    viewer=viewer.replace("$('#reset').onclick=()=>setView('hero');", "$('#reset').onclick=()=>{setView('hero');};")
    viewer=viewer.replace("if(e.key==='Escape')state.spin=false", "if(e.key==='Escape'){state.spin=false;$('#spin').textContent='Auto-rotate';$('#spin').setAttribute('aria-pressed','false')}")
    viewer=viewer.replace("uniform sampler2D colorMap;", "uniform float studio;uniform float driveOn;uniform vec3 drivePosition;uniform sampler2D colorMap;")
    viewer=viewer.replace('vec3(3.2,3.05,2.8),N,V', 'mix(vec3(3.2,3.05,2.8),vec3(3.1),studio),N,V')
    viewer=viewer.replace('vec3(1.1,1.4,1.9),N,V', 'mix(vec3(1.1,1.4,1.9),vec3(1.65),studio),N,V')
    viewer=viewer.replace('vec3(1.7,2.05,2.7),N,V', 'mix(vec3(1.7,2.05,2.7),vec3(1.8),studio),N,V')
    viewer=viewer.replace("'useTexture','wireOnly'", "'useTexture','wireOnly','studio','driveOn','drivePosition'")
    viewer=viewer.replace('state.glow,state.wire,ortho', 'state.glow,state.wire,state.studio,ortho')
    viewer=viewer.replace('gl.uniform1i(U.wireOnly,state.wire);', 'gl.uniform1i(U.wireOnly,state.wire);gl.uniform1f(U.studio,state.studio?1:0);gl.uniform1f(U.driveOn,state.glow?1:0);gl.uniform3fv(U.drivePosition,'+json.dumps([center[0],lo[1]-.28,center[2]])+');')
    practical_lights = ''
    if args.ship == 'Tynan':
        # Local service lamps illuminate nearby metal in the cinematic study.
        # Neutral studio inspection stays independent of this presentation light.
        practical_lights = '''
    vec3 toService=vec3(-.18,1.10,.20)-vPosition;
    c+=light(normalize(toService),vec3(5.2,1.9,.30)/(1.+dot(toService,toService)*48.),N,V,albedo,F0,r)*(1.-studio);
    vec3 toAirlock=vec3(.10,-.44,.89)-vPosition;
    c+=light(normalize(toAirlock),vec3(3.5,1.45,.24)/(1.+dot(toAirlock,toAirlock)*60.),N,V,albedo,F0,r)*(1.-studio);
        '''
    viewer=viewer.replace('c+=emissive*1.45;', '''
    c*=mix(.62,1.,studio);
    vec3 toDrive=drivePosition-vPosition;
    c+=light(normalize(toDrive),vec3(.12,11.,26.)/(1.+dot(toDrive,toDrive)*2.),N,V,albedo,F0,r)*(1.-studio)*driveOn;
    ''' + practical_lights + '''
    c+=emissive*1.45;
    ''')
    viewer=viewer.replace('/^(paint|white|accent)$/.test(p.name)', '/^(paint|hull|armor|white|accent|stripe|ceramic)/i.test(p.name)')
    sky=space_background()
    css=f'''#canvas{{background:#050a10 url("{sky}") center/cover no-repeat}}
    body.studio #canvas{{background:radial-gradient(ellipse at 50% 40%,#20252a,#080c10 75%)}}
    header{{top:24px;left:26px}}h1{{font-size:26px}}#badge{{display:none}}#views{{left:26px;top:135px;gap:5px}}#views button{{padding:7px 9px;min-width:125px}}
    #tools{{left:26px;bottom:24px;max-width:80vw;gap:5px}}#tools button{{padding:8px 10px}}#details{{right:22px;bottom:25px;max-width:245px}}#details .micro{{font-size:9px}}#help{{left:26px;bottom:78px}}
    #backgroundPreview{{display:none;position:absolute;inset:0;background:#03080d;z-index:8}}#backgroundPreview img{{width:100%;height:100%;object-fit:contain}}
    #backgroundPreview nav{{position:absolute;left:24px;top:24px;display:flex;gap:10px}}
    @media(max-width:900px){{#details,#help,#viewLabel,.sub{{display:none}}header{{top:18px;left:16px}}h1{{font-size:20px}}#views{{left:16px;right:16px;top:auto;bottom:112px;flex-direction:row;flex-wrap:wrap;gap:4px}}#views button{{font-size:9px;padding:5px 7px;min-width:0}}#views button small{{display:none}}#tools{{left:16px;right:16px;bottom:18px;max-width:none}}#tools button{{font-size:10px;padding:7px 8px}}}}
    '''
    viewer=viewer.replace('</style>',css+'</style>')
    viewer=viewer.replace('<body>','<body class="studio">')
    viewer=viewer.replace('<button id="reset">','<button id="lighting" aria-pressed="true">Studio lighting</button>\n<button id="background">Background preview</button>\n<button id="reset">')
    viewer=viewer.replace('<script id="modelData"', '<section id="backgroundPreview"><img id="backgroundImage" alt="Original Bashi Bazouk model rendered as a matching sensor-panel background"><nav><button id="backgroundClose">Back to model</button><button id="backgroundSave">Save background PNG</button></nav></section>\n<script id="modelData"')
    viewer=viewer.replace('Original Bashi Bazouk model rendered', 'Original '+html.escape(title)+' model rendered')
    controls=r'''
    $('#lighting').onclick=()=>{state.studio=!state.studio;document.body.classList.toggle('studio',state.studio);$('#lighting').textContent=state.studio?'Studio lighting':'Cinematic lighting';$('#lighting').setAttribute('aria-pressed',String(state.studio));};
    let backgroundData='';
    const backgroundSky=new Image();backgroundSky.src=__SKY__;
    $('#background').onclick=async()=>{
      await backgroundSky.decode();
      const saved={...state,target:[...state.target]},oldStyle=canvas.style.cssText,oldOrtho=ortho;
      Object.assign(state,{...presets.hero,target:[...presets.hero.target],radius:presets.hero.radius*.87,yaw:.72,pitch:.18,roll:-.32,studio:false,spin:false,hull:true,wire:false,glow:true});ortho=false;
      canvas.style.width='1920px';canvas.style.height='1080px';renderedKey='';draw(performance.now());
      const output=document.createElement('canvas');output.width=1920;output.height=1080;const ctx=output.getContext('2d');
      ctx.drawImage(backgroundSky,0,0,1920,1080);ctx.globalAlpha=.78;ctx.drawImage(canvas,220,-20,1920,1080);ctx.globalAlpha=1;
      const shade=ctx.createLinearGradient(0,0,1800,0);shade.addColorStop(0,'rgba(2,7,12,.96)');shade.addColorStop(.35,'rgba(2,7,12,.76)');shade.addColorStop(.68,'rgba(2,7,12,.18)');shade.addColorStop(1,'rgba(2,7,12,.20)');ctx.fillStyle=shade;ctx.fillRect(0,0,1920,1080);
      backgroundData=output.toDataURL('image/png');$('#backgroundImage').src=backgroundData;$('#backgroundPreview').style.display='block';
      canvas.style.cssText=oldStyle;Object.assign(state,saved);state.spin=false;ortho=oldOrtho;renderedKey='';draw(performance.now());
      $('#spin').textContent='Auto-rotate';$('#spin').setAttribute('aria-pressed','false');$('#spin').classList.remove('active');
    };
    $('#backgroundClose').onclick=()=>{$('#backgroundPreview').style.display='none';};
    $('#backgroundSave').onclick=()=>{if(!backgroundData)return;const a=document.createElement('a');a.href=backgroundData;a.download=__BACKGROUND_FILENAME__;a.click();};
    '''.replace('__SKY__',json.dumps(sky)).replace('__BACKGROUND_FILENAME__',json.dumps(args.ship+'-background.png'))
    viewer=viewer.replace("$('#loading').style.display='none';",controls+"\n$('#loading').style.display='none';")
    viewer=viewer.replace('requestAnimationFrame(draw)}','if(!window.__manualStudyFrame)requestAnimationFrame(draw)}')
    # A one-off background export must not start a second animation chain.
    viewer=viewer.replace("renderedKey='';draw(performance.now());", "renderedKey='';window.__manualStudyFrame=true;draw(performance.now());window.__manualStudyFrame=false;")
    viewer=viewer.replace("a.download='"+title+".glb'", "a.download='"+args.ship+".glb'")
    (output/'model.html').write_text(viewer,encoding='utf-8')
    refs=sorted((stage/'references').glob('*.jpg'))
    (output/'references').mkdir(exist_ok=True)
    for reference in refs:
        shutil.copy2(reference, output/'references'/reference.name)
    options=''.join(f'<option value="references/{html.escape(p.name)}">{html.escape(p.stem.replace("-"," ").title())}</option>' for p in refs)
    initial='references/'+refs[0].name if refs else ''
    index='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>__TITLE__ / model study</title>
    <style>*{box-sizing:border-box}html,body{margin:0;height:100%;background:#050a10;color:#c9d3dc;font:13px system-ui}body{display:grid;grid-template-rows:52px 1fr}header{display:flex;align-items:center;justify-content:space-between;padding:0 18px;border-bottom:1px solid #23313d;gap:12px}header b{font-weight:550;letter-spacing:.1em}header small{color:#738896;margin-left:12px}button,select,a{border:1px solid #354651;background:#111e28;color:#c1d6df;border-radius:4px;padding:7px 11px;font:12px system-ui;text-decoration:none;cursor:pointer}main{display:grid;grid-template-columns:1fr;min-height:0}.compare main{grid-template-columns:1fr 1fr}iframe{border:0;width:100%;height:100%}aside{display:none;position:relative;min-height:0;border-left:1px solid #253543}.compare aside{display:block}aside img{width:100%;height:100%;object-fit:contain;padding:55px 10px 60px}aside select{position:absolute;top:15px;left:16px}.credit{position:absolute;bottom:14px;left:16px;right:16px;font-size:10px;line-height:1.6;color:#8298a6}.credit a{border:0;background:none;padding:0;font-size:10px;text-decoration:underline}@media(max-width:850px){header small{display:none}header b{font-size:11px}.compare main{grid-template-columns:1fr;grid-template-rows:58% 42%}aside img{padding:38px 5px 32px}.credit{font-size:9px;bottom:6px}aside select{top:6px;padding:3px 6px}}</style>
    <header><div><b>__TITLE__</b><small>FINAL DRAWING STUDY · ORIGINAL COLOURS</small></div><button id="compare" aria-pressed="false">Compare drawings</button></header>
    <main><iframe src="model.html" title="Rotating __TITLE__ model"></iframe><aside><select aria-label="Reference drawing">__OPTIONS__</select><img src="__INITIAL__" alt="Production drawing for local model comparison"><div class="credit">Reference art © its respective owners. Local comparison only. <a href="__REFERENCE_URL__" target="_blank" rel="noopener">Artist source</a><br>Original model; unseen features interpreted. Model scale is relative.</div></aside></main>
    <script>document.querySelector('#compare').onclick=()=>{const on=document.body.classList.toggle('compare');document.querySelector('#compare').textContent=on?'Model only':'Compare drawings';document.querySelector('#compare').setAttribute('aria-pressed',String(on));};document.querySelector('select').onchange=e=>document.querySelector('aside img').src=e.target.value;</script></html>'''
    index=index.replace('iframe{border:0;', 'iframe{display:block;border:0;').replace('html,body{margin:0;', 'html,body{overflow:hidden;margin:0;')
    index=index.replace('src="model.html"', 'src="model.html?v='+hashlib.sha256(viewer.encode()).hexdigest()[:16]+'"')
    index=index.replace('FINAL DRAWING STUDY · ORIGINAL COLOURS', 'PRODUCTION REFERENCE STUDY')
    for key,value in {'__TITLE__':html.escape(title),'__OPTIONS__':options,'__INITIAL__':html.escape(initial),'__REFERENCE_URL__':html.escape(args.reference_url)}.items():index=index.replace(key,value)
    (output/'index.html').write_text(index,encoding='utf-8')
    receipt={'ship':args.ship,'title':title,'preview':str(output/'index.html'),'model':model,'glbSha256':hashlib.sha256(raw).hexdigest(),'background':'Original model render, generated by Background preview; no production pixels in exported background.','referenceUrl':args.reference_url,'credit':args.credit,'scope':'Staged study only; no active assets or FPS changes.'}
    (output/'study.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(receipt,indent=2))


if __name__=='__main__':
    main()
