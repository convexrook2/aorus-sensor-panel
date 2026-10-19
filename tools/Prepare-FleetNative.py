"""Stage native ship packages from the reviewed Fleet Studio catalogue.

Does not change active Assets. Full study GLBs stay intact. Native packages use
the existing 150k triangle budget and 1024-pixel textures supported by WPF.
"""
from pathlib import Path
import argparse,contextlib,hashlib,importlib.util,io,json,struct,sys,zipfile
import numpy as np
from PIL import Image
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('converter',ROOT/'tools/Convert-HoloScene.py')
converter=importlib.util.module_from_spec(spec);spec.loader.exec_module(converter)
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def unpack(raw):
 assert struct.unpack_from('<4sII',raw)==(b'glTF',2,len(raw))
 n=struct.unpack_from('<I',raw,12)[0];return json.loads(raw[20:20+n]),raw[28+n:]
def encode(d,b):
 text=json.dumps(d,separators=(',',':'),ensure_ascii=False).encode();text+=b' '*(-len(text)%4);b+=b'\0'*(-len(b)%4)
 return struct.pack('<4sII',b'glTF',2,28+len(text)+len(b))+struct.pack('<I4s',len(text),b'JSON')+text+struct.pack('<I4s',len(b),b'BIN\0')+b
def main():
 ap=argparse.ArgumentParser();ap.add_argument('--catalog',type=Path,required=True);ap.add_argument('--output',type=Path,required=True);ap.add_argument('--ships',nargs='+',required=True);ap.add_argument('--resume',action='store_true');a=ap.parse_args()
 out=a.output.resolve();assert out.is_relative_to(ROOT) and (not out.exists() or a.resume);out.mkdir(parents=True,exist_ok=a.resume)
 cat=json.loads(a.catalog.read_text(encoding='utf-8'));rows=[]
 for ident in a.ships:
  entry=next(r for r in cat if r['id']==ident);src=(a.catalog.parent/entry['model']).resolve();source_hash=sha(src);assert source_hash==entry['sha256']
  d,b=unpack(src.read_bytes());prefix=b;changes=[]
  used=set()
  for m in d.get('materials',[]):
   for v in (m.get('pbrMetallicRoughness',{}).get('baseColorTexture'),m.get('emissiveTexture')):
    if v:used.add(d['textures'][v['index']]['source'])
  for i in sorted(used):
   image=d['images'][i];view=d['bufferViews'][image['bufferView']];start=view.get('byteOffset',0)
   raw=b[start:start+view['byteLength']];im=Image.open(io.BytesIO(raw)).convert('RGBA');before=im.size;im.thumbnail((1024,1024),Image.Resampling.LANCZOS)
   stream=io.BytesIO();im.save(stream,format='PNG',compress_level=6);data=stream.getvalue();assert len(data)<8_000_000
   b+=b'\0'*(-len(b)%4);d['bufferViews'].append(dict(buffer=0,byteOffset=len(b),byteLength=len(data)));b+=data
   d['images'][i]=dict(bufferView=len(d['bufferViews'])-1,mimeType='image/png')
   changes.append(dict(image=i,sourceSize=before,nativeSize=im.size))
  for material in d.get('materials',[]):
   pbr=material.get('pbrMetallicRoughness',{});factor=pbr.get('baseColorFactor',[1,1,1,1]);tex=pbr.get('baseColorTexture')
   if tex and factor!=[1,1,1,1]:
    oldtex=d['textures'][tex['index']];image=d['images'][oldtex['source']];view=d['bufferViews'][image['bufferView']];start=view.get('byteOffset',0)
    pixels=np.asarray(Image.open(io.BytesIO(b[start:start+view['byteLength']])).convert('RGBA'),dtype=np.float32)/255
    rgb=pixels[:,:,:3];linear=np.where(rgb<=.04045,rgb/12.92,((rgb+.055)/1.055)**2.4)*np.asarray(factor[:3])
    pixels[:,:,:3]=np.where(linear<=.0031308,linear*12.92,1.055*linear**(1/2.4)-.055);pixels[:,:,3]*=factor[3]
    stream=io.BytesIO();Image.fromarray(np.uint8(np.clip(pixels,0,1)*255)).save(stream,format='PNG');data=stream.getvalue()
    b+=b'\0'*(-len(b)%4);d['bufferViews'].append(dict(buffer=0,byteOffset=len(b),byteLength=len(data)));b+=data
    d['images'].append(dict(bufferView=len(d['bufferViews'])-1,mimeType='image/png'));d['textures'].append({**oldtex,'source':len(d['images'])-1})
    pbr['baseColorTexture']={**tex,'index':len(d['textures'])-1};pbr['baseColorFactor']=[1,1,1,1]
    changes.append(dict(material=material.get('name'),bakedLinearColourFactor=factor))
  d['buffers'][0]['byteLength']=len(b);prepared=out/(ident+'-native-input.glb');prepared.write_bytes(encode(d,b))
  assert unpack(prepared.read_bytes())[1][:len(prefix)]==prefix
  target=out/(ident+'.ship')
  if a.resume and target.exists():
   with zipfile.ZipFile(target) as z:assert json.loads(z.read('scene.json'))['sourceSha256']==sha(prepared)
  else:
   with contextlib.redirect_stdout(io.StringIO()):converter.convert(prepared,target,'150000')
  with zipfile.ZipFile(target) as z:
   assert z.testzip() is None;scene=json.loads(z.read('scene.json'))
   assert 0<scene['triangles']<=150000 and scene['parts']<=200
   assert all(x.file_size<8_000_000 for x in z.infolist() if x.filename.startswith('texture-'))
  assert sha(src)==source_hash
  rows.append(dict(ship=ident,studyTitle=entry['title'],studyModel=str(src.relative_to(ROOT)),studySha256=source_hash,
   preparedSha256=sha(prepared),native=str(target.relative_to(ROOT)),nativeSha256=sha(target),sourceTriangles=scene['sourceTriangles'],
   nativeTriangles=scene['triangles'],parts=scene['parts'],materials=len(scene['materials']),bytes=target.stat().st_size,textures=changes,
   credit=entry['credit'],referenceUrl=entry.get('referenceUrl'),sourceUrl=entry.get('sourceUrl'),
   limits='Native WPF keeps base colour, UVs, normals, material groups and emissive sources; PBR normal/ORM maps remain in the full study.'))
  print(f"{ident}: {scene['triangles']:,} native triangles / {scene['parts']} parts / {target.stat().st_size:,} bytes",flush=True)
 (out/'preparation-receipt.json').write_text(json.dumps(dict(result='PASS',models=rows),indent=2),encoding='utf-8')
if __name__=='__main__':main()
