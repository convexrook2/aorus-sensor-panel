"""Sync selected reviewed models into the desktop bundle, or check it read-only."""
import argparse,copy,gzip,hashlib,json,shutil,struct
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
DEST=ROOT/'Assets/FleetViewer'
STUDIO=ROOT/'FleetStudio'

def require(condition,message):
    if not condition:raise ValueError(message)

def sha(data):return hashlib.sha256(data).hexdigest()
def read(path):return json.loads(path.read_text(encoding='utf-8'))
def write(path,data):
    temp=path.with_name(path.name+'.new')
    temp.write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8');temp.replace(path)

def local(folder,name):
    path=(folder/name).resolve()
    require(path.is_relative_to(folder.resolve()),'Path outside bundle: '+name)
    return path

def inspect(data):
    raw=gzip.decompress(data)
    require(struct.unpack_from('<III',raw)==(0x46546c67,2,len(raw)),'Invalid GLB header')
    size,kind=struct.unpack_from('<II',raw,12)
    require(kind==0x4e4f534a,'Missing GLB JSON')
    doc=json.loads(raw[20:20+size]);length,kind=struct.unpack_from('<II',raw,20+size)
    require(kind==0x004e4942 and 28+size+length==len(raw),'Invalid GLB binary chunk')
    require(all('uri' not in x for x in doc.get('images',[])+doc.get('buffers',[])),'External asset reference')
    used={p['material'] for m in doc['meshes'] for p in m['primitives'] if 'material' in p}
    materials=[doc['materials'][i] for i in used]
    maps={
        'colour':sum('baseColorTexture' in m.get('pbrMetallicRoughness',{}) for m in materials),
        'normal':sum('normalTexture' in m for m in materials),
        'occlusion':sum('occlusionTexture' in m for m in materials),
        'metalRough':sum('metallicRoughnessTexture' in m.get('pbrMetallicRoughness',{}) for m in materials),
        'emissive':sum('emissiveTexture' in m for m in materials)}
    # Match viewer.js: each mesh instance in the active glTF scene contributes
    # triangles. A repeated mesh shares source vertices but is drawn twice.
    mesh_triangles=[sum(doc['accessors'][p.get('indices',p['attributes']['POSITION'])]['count']//3 for p in m['primitives'])
        for m in doc['meshes']]
    nodes=doc['nodes']
    def visit(node_id,ancestors):
        require(node_id not in ancestors,'Cyclic glTF scene graph')
        node=nodes[node_id]
        return (mesh_triangles[node['mesh']] if 'mesh' in node else 0)+sum(
            visit(child,ancestors|{node_id}) for child in node.get('children',[]))
    scene=doc['scenes'][doc.get('scene',0)]
    triangles=sum(visit(node_id,set()) for node_id in scene['nodes'])
    return raw,doc,maps,triangles

def verify():
    manifest=read(DEST/'manifest.json');catalog=read(DEST/'catalog.json')
    require(manifest['ships']==len(catalog),'Manifest ship count')
    require(len({e['id'] for e in catalog})==len(catalog),'Duplicate ship ID')
    listed={x['path'] for x in manifest['files']}
    require(len(listed)==len(manifest['files']),'Duplicate manifest file')
    actual={p.relative_to(DEST).as_posix() for p in DEST.rglob('*') if p.is_file() and p.name!='manifest.json'}
    require(actual==listed,'Bundle file inventory differs from manifest')
    for row in manifest['files']:
        data=local(DEST,row['path']).read_bytes()
        require(len(data)==row['bytes'] and sha(data)==row['sha256'],'Manifest mismatch: '+row['path'])
    for row in catalog:
        data=local(DEST,row['model']).read_bytes();raw,doc,maps,triangles=inspect(data)
        require(sha(data)==row['compressedSha256'] and sha(raw)==row['sha256'],'Model checksum: '+row['id'])
        require(len(data)==row['compressedBytes'] and len(raw)==row['sourceBytes'],'Model size: '+row['id'])
        require(triangles==row['triangles'] and maps==row['renderMaps'],'Geometry/maps: '+row['id'])
        require(len(doc['materials'])==row['materials'] and len(doc.get('images',[]))==row['images'],'Material/images: '+row['id'])
        print('PASS:',row['id'],f'{triangles:,}','triangles, full maps and exact hashes')
    print('PASS:',len(catalog),'desktop ships,',len(listed),'bundle files; no native build or launch')

def sync(ships,rollback):
    # Explicit native-id=studio-id mapping keeps separate era assets intact.
    sources=dict(item.split('=',1) if '=' in item else (item,item) for item in ships)
    require(len(sources)==len(ships),'Duplicate requested native ship')
    ships=list(sources)
    require(len(set(ships))==len(ships),'Duplicate requested ship')
    rollback=rollback.resolve()
    require(rollback.is_relative_to((ROOT/'artifacts').resolve()) and not rollback.exists(),'Use a new rollback folder under artifacts')
    catalog=read(DEST/'catalog.json');before=copy.deepcopy(catalog)
    source={e['id']:e for e in read(STUDIO/'catalog.json')};planned=[]
    source_manifest={x['file']:x for x in read(STUDIO/'manifest.json')['assets']}
    for ship in ships:
        old=next((x for x in catalog if x['id']==ship),None)
        require(old is not None and sources[ship] in source,'Ship absent from an existing catalogue: '+ship)
        entry=source[sources[ship]];data=local(STUDIO,entry['model']).read_bytes()
        require(sha(data)==source_manifest[entry['model']]['gzip_sha256'],'Reviewed compressed source checksum')
        raw,doc,maps,triangles=inspect(data)
        require(sha(raw)==entry['sha256'] and triangles==entry['triangles'],'Reviewed source checksum/count')
        row={k:v for k,v in entry.items() if not k.startswith('before') and k!='references'}
        row.update(id=ship,sourceId=sources[ship],model=old['model'],sourceBytes=len(raw),compressedBytes=len(data),compression='gzip-lossless',
            compressedSha256=sha(data),renderMaps=maps,lightUnitScale=1/683)
        planned.append((ship,old,row,data))
    rollback.mkdir(parents=True)
    names=['catalog.json','manifest.json','ATTRIBUTION.txt']+[x[1]['model'] for x in planned]
    backups=[]
    for name in names:
        source_path=local(DEST,name);target=local(rollback,name);target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(source_path,target)
        require(sha(target.read_bytes())==sha(source_path.read_bytes()),'Rollback copy mismatch')
        backups.append({'path':name,'sha256':sha(target.read_bytes())})
    write(rollback/'receipt.json',{'files':backups,'ships':ships})
    for ship,old,row,data in planned:
        target=local(DEST,old['model']);temp=target.with_name(target.name+'.new');temp.write_bytes(data);temp.replace(target)
        catalog[catalog.index(old)]=row
    for old,new in zip(before,catalog):
        require(old['id'] in ships or old==new,'Unrequested catalogue change')
    write(DEST/'catalog.json',catalog)
    credits=['Full model geometry and PBR textures retained from the reviewed Fleet Studio.',
        'Gzip is lossless; source SHA-256 is checked again after decompression.','']
    credits.extend(e['id']+': '+e['credit']+'\n'+e.get('sourceUrl',e.get('referenceUrl','')) for e in catalog)
    credits.extend(['','Three.js: MIT licence. See vendor/three/LICENSE.'])
    (DEST/'ATTRIBUTION.txt').write_text('\n\n'.join(credits)+'\n',encoding='utf-8')
    files=[{'path':p.relative_to(DEST).as_posix(),'bytes':p.stat().st_size,'sha256':sha(p.read_bytes())}
        for p in sorted(DEST.rglob('*')) if p.is_file() and p.name!='manifest.json']
    write(DEST/'manifest.json',{'lossless':True,'ships':len(catalog),'source':'Reviewed Fleet Studio catalogue; scoped model updates','files':files})
    verify()

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check',action='store_true');parser.add_argument('--ships',nargs='+')
    parser.add_argument('--rollback',type=Path)
    args=parser.parse_args()
    if args.check:
        require(not args.ships and not args.rollback,'Check mode is read-only');verify()
    else:
        require(args.ships and args.rollback,'Sync requires --ships and --rollback');sync(args.ships,args.rollback)

if __name__=='__main__':main()
