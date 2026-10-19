"""Stage a local PBR fleet review without changing installed or accepted assets.

Uses the already installed MIT-licensed Three.js package. Every model is copied
verbatim; this tool never rewrites source GLBs or runs model builders.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re
import shutil
import struct

ROOT=Path(__file__).resolve().parents[1]

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def copy_verified(source,target):
    target.parent.mkdir(parents=True,exist_ok=True)
    digest=sha(source)
    if target.exists() and sha(target)!=digest:
        raise ValueError('Preserving different existing file: '+str(target))
    if not target.exists():shutil.copy2(source,target)
    if sha(target)!=digest:raise ValueError('Copy hash mismatch: '+str(target))
    return digest

def glb_info(path):
    with path.open('rb') as f:
        magic,version,total=struct.unpack('<III',f.read(12))
        if magic!=0x46546c67 or version!=2:raise ValueError('Not a glTF 2 binary: '+str(path))
        size,kind=struct.unpack('<II',f.read(8))
        if kind!=0x4e4f534a:raise ValueError('Missing GLB JSON')
        doc=json.loads(f.read(size))
    triangles=sum(doc['accessors'][p['indices']]['count']//3 for m in doc.get('meshes',[]) for p in m['primitives'] if p.get('mode',4)==4 and 'indices' in p)
    mats=doc.get('materials',[])
    return dict(triangles=triangles,materials=len(mats),images=len(doc.get('images',[])),
        maps={name:sum(bool(m.get(field) if field not in ('baseColorTexture','metallicRoughnessTexture') else m.get('pbrMetallicRoughness',{}).get(field)) for m in mats)
              for name,field in [('colour','baseColorTexture'),('normal','normalTexture'),('occlusion','occlusionTexture'),('metalRough','metallicRoughnessTexture'),('emissive','emissiveTexture')]})

def copy_three(out):
    source=ROOT/'NavigationWeb/node_modules/three'
    metadata=json.loads((source/'package.json').read_text())
    pending=['build/three.module.js','examples/jsm/loaders/GLTFLoader.js',
        'examples/jsm/controls/OrbitControls.js','examples/jsm/environments/RoomEnvironment.js',
        'examples/jsm/postprocessing/EffectComposer.js','examples/jsm/postprocessing/RenderPass.js',
        'examples/jsm/postprocessing/SSAOPass.js','examples/jsm/postprocessing/OutputPass.js']
    copied={}
    while pending:
        relative=pending.pop()
        if relative in copied:continue
        file=(source/relative).resolve(strict=True)
        file.relative_to(source.resolve())
        copied[relative]=copy_verified(file,out/'vendor/three'/relative)
        for dependency in re.findall(r'(?:from\s*|import\s*)[\'\"](\.[^\'\"]+)[\'\"]',file.read_text(encoding='utf-8')):
            pending.append((file.parent/dependency).resolve().relative_to(source.resolve()).as_posix())
    copy_verified(source/'LICENSE',out/'vendor/three/LICENSE')
    return dict(version=metadata['version'],license='MIT',files=copied)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    out=args.output.resolve()
    if (out/'catalog.json').exists():raise ValueError('Use a fresh versioned output directory')
    entries=json.loads(args.manifest.read_text(encoding='utf-8'))
    out.mkdir(parents=True,exist_ok=True)
    vendor=copy_three(out)
    catalog=[]
    for entry in entries:
        if not re.fullmatch(r'[A-Za-z][A-Za-z0-9]*',entry['id']):raise ValueError('Invalid ship identity')
        src=Path(entry['source']).resolve(strict=True)
        info=glb_info(src)
        target=out/'models'/(entry['id']+'.glb')
        data={k:v for k,v in entry.items() if k not in ('source','beforeSource')}
        data.update(info,model='models/'+target.name,sha256=copy_verified(src,target))
        if entry.get('beforeSource'):
            before=Path(entry['beforeSource']).resolve(strict=True)
            dest=out/'models'/(entry['id']+'-before.glb')
            data.update(beforeModel='models/'+dest.name,beforeSha256=copy_verified(before,dest),beforeInfo=glb_info(before))
        catalog.append(data)
    copy_verified(ROOT/'tools/FidelityViewer.template.html',out/'index.html')
    (out/'catalog.json').write_text(json.dumps(catalog,indent=2),encoding='utf-8')
    (out/'build-receipt.json').write_text(json.dumps(dict(vendor=vendor,models=catalog,
        htmlSha256=sha(out/'index.html'),catalogSha256=sha(out/'catalog.json'),
        manifestSha256=sha(args.manifest),builderSha256=sha(Path(__file__)),
        scope='Local PBR review only; original inputs, native panel, FPS and installation unchanged.'),indent=2),encoding='utf-8')
    print(json.dumps(dict(output=str(out),ships=len(catalog),three=vendor['version'],copiedBytes=sum(p.stat().st_size for p in (out/'models').glob('*.glb'))),indent=2))

if __name__=='__main__':main()
