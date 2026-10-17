"""Copy a reviewed local fleet catalogue into the static site, without reducing assets."""
import argparse,copy,gzip,hashlib,json,struct
from pathlib import Path
from urllib.parse import urlparse

ROOT=Path(__file__).resolve().parents[1]
def digest(data):return hashlib.sha256(data).hexdigest()
def require(condition,message):
    if not condition:raise ValueError(message)
def read_json(path):return json.loads(path.read_text(encoding='utf-8'))
def write_json(path,data):
    temp=path.with_name(path.name+'.new');temp.write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8');temp.replace(path)
def public_url(value):return isinstance(value,str) and urlparse(value).scheme in ('https','http') and bool(urlparse(value).netloc)
def clean_metadata(value):
    if isinstance(value,dict):return {k:clean_metadata(v) for k,v in value.items() if k!='extras'}
    if isinstance(value,list):return [clean_metadata(v) for v in value]
    return value

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source',type=Path,required=True,help='Local reviewed catalogue directory')
    parser.add_argument('--pella',type=Path,help='Optional newer Pella-only catalogue directory')
    args=parser.parse_args();source=args.source.resolve()
    catalog=read_json(source/'catalog.json');engines=read_json(source/'engines.json')
    assets={};sources={};rows=[]
    (ROOT/'models').mkdir(exist_ok=True)
    def pack(path,ship,credit):
        path=path.resolve();require(path.is_file(),'Missing source '+str(path))
        compressed=path.suffix=='.gz';raw=gzip.decompress(path.read_bytes()) if compressed else path.read_bytes()
        source_sha=digest(raw)
        if source_sha in sources:return sources[source_sha]
        magic,version,total=struct.unpack_from('<III',raw)
        require((magic,version,total)==(0x46546c67,2,len(raw)),'Invalid GLB header '+ship)
        size=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+size]);length=struct.unpack_from('<I',raw,20+size)[0]
        blob=raw[28+size:28+size+length]
        require(all('bufferView'in im and 'uri'not in im for im in doc.get('images',[])),'Non-embedded map '+ship)
        require(len(doc.get('buffers',[]))==1 and 'uri'not in doc['buffers'][0],'External buffer '+ship)
        # Keep every existing geometry/image byte, and omit local authoring recipes.
        doc=clean_metadata(doc);doc['extras']={'credit':credit,'adaptation':'Unofficial fan study; see catalogue credits and linked sources.'}
        encoded=json.dumps(doc,separators=(',',':')).encode();encoded+=b' '*(-len(encoded)%4)
        public=struct.pack('<III',0x46546c67,2,28+len(encoded)+len(blob))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(blob),0x004e4942)+blob
        require(public[-len(blob):]==blob,'Binary payload changed '+ship)
        filename='models/'+ship+'-'+source_sha[:12]+'.glb.gz';target=ROOT/filename
        data=gzip.compress(public,compresslevel=9,mtime=0)
        require(len(data)<100*1024*1024,'Asset exceeds GitHub file limit '+ship)
        if target.exists():require(target.read_bytes()==data,'Preserving an existing different asset '+filename)
        else:target.write_bytes(data)
        triangles=sum(doc['accessors'][p['indices']]['count']//3 for m in doc['meshes'] for p in m['primitives'])
        record={'file':filename,'sha256':digest(public),'gzip_sha256':digest(data),'source_sha256':source_sha,
            'binary_sha256':digest(blob),'triangles':triangles,'materials':len(doc.get('materials',[])),
            'images':len(doc.get('images',[])),'lights':len(doc.get('extensions',{}).get('KHR_lights_punctual',{}).get('lights',[])),
            'compressed_bytes':len(data),'uncompressed_bytes':len(public),'geometry_and_image_bytes_exact':True}
        assets[filename]=record;sources[source_sha]=record
        print(ship+': '+str(triangles)+' triangles, '+str(round(len(data)/1e6,2))+' MB',flush=True)
        return record
    for original in catalog:
        e=copy.deepcopy(original);base=source
        if e['id']=='Pella' and args.pella:
            base=args.pella.resolve();new=read_json(base/'catalog.json')
            require(len(new)==1 and new[0]['id']=='Pella','Not a Pella-only override');e=copy.deepcopy(new[0])
            engines['Pella']=read_json(base/'engines.json')['Pella']
        links=list(e.get('relatedSources',[]))
        for ref in e.pop('references',[]):
            url=ref.get('source')
            if public_url(url) and not any(x.get('url')==url for x in links):links.append({'label':'Artist / exterior reference','url':url})
        e['relatedSources']=[x for x in links if public_url(x.get('url'))]
        e['references']=[]
        current=pack(base/e['model'],e['id'],e.get('credit',''))
        e.update(model=current['file'],sha256=current['sha256'],triangles=current['triangles'],materials=current['materials'],images=current['images'])
        if e.get('beforeModel'):
            previous=pack(base/e['beforeModel'],e['id']+'-before',e.get('beforeCredit',e.get('credit','')))
            e.update(beforeModel=previous['file'],beforeSha256=previous['sha256'],beforeInfo={'triangles':previous['triangles'],'materials':previous['materials']})
        rows.append(e)
    require(len({e['id'] for e in rows})==len(rows),'Duplicate ship IDs')
    for e in rows:
        require(e['id'] in engines,'Missing drive anchors '+e['id'])
        if e.get('beforeEngineId'):require(e['beforeEngineId'] in engines,'Missing previous drive anchors')
    pella=next(e for e in rows if e['id']=='Pella')
    manifest={'revision':'Fleet Studio permanent viewer / 2026-10-17','catalogue_entries':len(rows),'assets':list(assets.values()),
        'current':assets[pella['model']],'previous':assets[pella['beforeModel']],
        'scope':'Browser previews only. Native app, installer, monitoring, FPS and SOL unchanged.',
        'reference_images_or_video_bundled':False,'rollback':'Comparison assets and earlier authoring revisions retained.',
        'permanent_url':'https://convexrook2.github.io/aorus-sensor-panel/'}
    write_json(ROOT/'catalog.json',rows);write_json(ROOT/'engines.json',engines);write_json(ROOT/'manifest.json',manifest)
    print('READY:',len(rows),'entries,',len(assets),'unique assets')

if __name__=='__main__':main()
