// Frozen model identity and the original hidden-plume regression. No GPU/UI.
import fs from 'node:fs';
import crypto from 'node:crypto';
import zlib from 'node:zlib';
import assert from 'node:assert/strict';
import * as THREE from '../FleetStudio/vendor/three/build/three.module.js';
const root=new URL('../',import.meta.url),read=p=>fs.readFileSync(new URL(p,root));
const json=p=>JSON.parse(read(p)),sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const expected='d391bc43b0843a328e435f0eefe16a34a0c8724cdaf0fe275f449fcd330e3eb3';
const studio=json('FleetStudio/catalog.json').find(e=>e.id==='RocinanteS6');
const native=json('Assets/FleetViewer/catalog.json').find(e=>e.id==='Rocinante');
const bytes=read('FleetStudio/'+studio.model),raw=zlib.gunzipSync(bytes);
assert.equal(sha(raw),expected);assert.equal(studio.sha256,expected);assert.equal(native.sha256,expected);
assert.ok(read('Assets/FleetViewer/'+native.model).equals(bytes),'Native model must be the same lossless file');
assert.ok(read('Assets/FleetViewer/rocinante-effects.js').equals(read('FleetStudio/ship-effects.js')),'Shared effect copies must agree');
const source=json('Sources/Rocinante-S6/identity.json');
assert.equal(sha(read('Sources/Rocinante-S6/Rocinante-S6.blend')),source.blend_sha256);assert.equal(source.glb_sha256,expected);
const jsonLength=raw.readUInt32LE(12),doc=JSON.parse(raw.subarray(20,20+jsonLength)),blob=raw.subarray(28+jsonLength);
assert.equal(doc.materials.length,93);assert.equal(doc.images.length,10);
assert.equal(doc.meshes.reduce((n,m)=>n+m.primitives.reduce((v,p)=>v+doc.accessors[p.indices].count/3,0),0),687992);
const worlds=new Map();
function visit(i,parent){const n=doc.nodes[i],m=n.matrix?new THREE.Matrix4().fromArray(n.matrix):new THREE.Matrix4().compose(new THREE.Vector3(...(n.translation||[0,0,0])),new THREE.Quaternion(...(n.rotation||[0,0,0,1])),new THREE.Vector3(...(n.scale||[1,1,1])));const world=parent.clone().multiply(m);worlds.set(i,world);for(const c of n.children||[])visit(c,world);}
for(const i of doc.scenes[doc.scene||0].nodes)visit(i,new THREE.Matrix4());
const bell=[],overall=new THREE.Box3(),fixtures={service:0,navigation:0,drive:0};
for(const [i,world] of worlds){const node=doc.nodes[i];if(node.mesh==null)continue;const mesh=doc.meshes[node.mesh];
 const glow=new Set(mesh.primitives.map(p=>doc.materials[p.material]).filter(m=>m.emissiveTexture||m.emissiveFactor?.some(n=>n>0)));
 for(const m of glow){if(m.name==='Roci / warm white service fittings')fixtures.service++;else if(m.name==='Roci / red navigation fittings')fixtures.navigation++;else if(m.name==='Roci / blue Epstein drive core')fixtures.drive++;}
 for(const p of mesh.primitives){const a=doc.accessors[p.attributes.POSITION],v=doc.bufferViews[a.bufferView];assert.equal(a.componentType,5126);
  const offset=(v.byteOffset||0)+(a.byteOffset||0),stride=v.byteStride||12;
  overall.union(new THREE.Box3(new THREE.Vector3(...a.min),new THREE.Vector3(...a.max)).applyMatrix4(world));
  if(node.name==='Main drive bell')for(let j=0;j<a.count;j++){const k=offset+j*stride;bell.push(new THREE.Vector3(blob.readFloatLE(k),blob.readFloatLE(k+4),blob.readFloatLE(k+8)).applyMatrix4(world));}
 }
}
assert.deepEqual(fixtures,{service:6,navigation:2,drive:1});assert.ok(bell.length);
const box=new THREE.Box3().setFromPoints(bell),center=box.getCenter(new THREE.Vector3()),outlet=box.min.y;
const radius=Math.min(...bell.filter(p=>Math.abs(p.y-outlet)<1e-5).map(p=>Math.hypot(p.x-center.x,p.z-center.z)));
const engines=json('FleetStudio/engines.json'),anchors=engines.RocinanteS6.engines;
assert.equal(anchors.length,1);assert.deepEqual(json('Assets/FleetViewer/engines.json').RocinanteS6,engines.RocinanteS6);
const anchor=anchors[0];assert.equal(anchor.style,'rocinante-s6');assert.deepEqual(anchor.direction,[0,-1,0]);
assert.ok(Math.abs(anchor.position[0]-center.x)<1e-6&&Math.abs(anchor.position[2]-center.z)<1e-6);
assert.ok(Math.abs(anchor.position[1]-(outlet-.002))<1e-6,'Plume must start beyond the physical outlet, in source-world coordinates');
assert.ok(anchor.radius<radius&&anchor.radius>radius*.95);assert.equal(anchor.addCore,false);
for(const [id,e] of Object.entries(engines))if(id!=='RocinanteS6')assert.ok(e.engines.every(a=>a.style!=='rocinante-s6'),'Roci style leaked to '+id);
assert.equal(studio.beforeEngineId,'Rocinante');
console.log('PASS: frozen Roci source/export, native/Studio parity, nine fixture objects, measured outlet and Roci-only exhaust. No native rendering or installation tested.');
