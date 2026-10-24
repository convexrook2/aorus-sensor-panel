// Measured outlet registration and display-effect lifecycle; no native/GPU proof.
import fs from 'node:fs';
import assert from 'node:assert/strict';
import * as THREE from '../FleetStudio/vendor/three/build/three.module.js';
const rootURL=new URL('../',import.meta.url),read=p=>fs.readFileSync(new URL(p,rootURL));
const engines=JSON.parse(read('Assets/FleetViewer/engines.json'));
assert.equal(engines.Arboghast?.engines.length,4,'Arboghast needs four registered drive plumes');
const studio=JSON.parse(read('FleetStudio/engines.json'));
assert.deepEqual(engines.Arboghast,studio.Arboghast,'Arboghast native/Studio exhaust registration must agree');
for(const id of ['RocinanteS6','Anubis'])assert.deepEqual(engines[id],studio[id],'Existing exhaust changed: '+id);
const raw=read('Assets/Models/Source/Arboghast-pass08/Arboghast-011-pass08.glb');
const jsonLength=raw.readUInt32LE(12),doc=JSON.parse(raw.subarray(20,20+jsonLength)),blob=raw.subarray(28+jsonLength);
const worlds=new Map(),bounds=new THREE.Box3();
function visit(i,parent){const n=doc.nodes[i],m=n.matrix?new THREE.Matrix4().fromArray(n.matrix):new THREE.Matrix4().compose(new THREE.Vector3(...(n.translation||[0,0,0])),new THREE.Quaternion(...(n.rotation||[0,0,0,1])),new THREE.Vector3(...(n.scale||[1,1,1])));const world=parent.clone().multiply(m);worlds.set(i,world);for(const c of n.children||[])visit(c,world);}
for(const i of doc.scenes[doc.scene||0].nodes)visit(i,new THREE.Matrix4());
for(const [i,world] of worlds){const n=doc.nodes[i];if(n.mesh==null)continue;for(const p of doc.meshes[n.mesh].primitives){const a=doc.accessors[p.attributes.POSITION];bounds.union(new THREE.Box3(new THREE.Vector3(...a.min),new THREE.Vector3(...a.max)).applyMatrix4(world));}}
function points(name){const i=doc.nodes.findIndex(n=>n.name===name);assert.ok(i>=0,'Missing physical fitting: '+name);const points=[];for(const p of doc.meshes[doc.nodes[i].mesh].primitives){const a=doc.accessors[p.attributes.POSITION],v=doc.bufferViews[a.bufferView];assert.equal(a.componentType,5126);for(let j=0;j<a.count;j++){const k=(v.byteOffset||0)+(a.byteOffset||0)+j*(v.byteStride||12);points.push(new THREE.Vector3(blob.readFloatLE(k),blob.readFloatLE(k+4),blob.readFloatLE(k+8)).applyMatrix4(worlds.get(i)));}}return points;}
const mean=ps=>ps.reduce((v,p)=>v.add(p),new THREE.Vector3()).divideScalar(ps.length);
for(let i=0;i<4;i++){
 const lip=points('Drive'+i+' segmented rolled mouth lip'),neck=points('Drive'+i+' neck gimbal mounting ring');
 const centre=mean(lip),axis=centre.clone().sub(mean(neck)).normalize(),a=engines.Arboghast.engines[i],position=new THREE.Vector3(...a.position),direction=new THREE.Vector3(...a.direction);
 assert.ok(direction.dot(axis)>.999999,'Exhaust does not follow the measured cant: '+i);
 const delta=position.clone().sub(centre),axial=delta.dot(axis),sideways=delta.clone().addScaledVector(axis,-axial).length();
 const outerPlane=Math.max(...lip.map(p=>p.clone().sub(centre).dot(axis)));
 assert.ok(sideways<.0001&&axial>outerPlane&&axial<outerPlane+.15,'Exhaust must start just beyond its own outlet: '+i);
 const radius=Math.min(...lip.map(p=>p.clone().sub(centre).projectOnPlane(axis).length()));
 assert.ok(a.radius<radius&&a.radius>radius*.8,'Exhaust must fit inside the existing mouth: '+i);
 assert.equal(a.addCore,false,'Do not cap the hollow source throat');assert.ok(a.length>0&&direction.y<-.95);
}
const moduleSource=read('Assets/FleetViewer/rocinante-effects.js').toString();
assert.ok(read('Assets/FleetViewer/rocinante-effects.js').equals(read('FleetStudio/ship-effects.js')));
const threeURL=new URL('FleetStudio/vendor/three/build/three.module.js',rootURL).href;
const {makeShipEffects}=await import('data:text/javascript;base64,'+Buffer.from(moduleSource.replace("from 'three'",'from '+JSON.stringify(threeURL))).toString('base64'));
const centre=bounds.getCenter(new THREE.Vector3()),scale=3.2/Math.max(...bounds.getSize(new THREE.Vector3()).toArray());
const root=new THREE.Group(),model=new THREE.Group();root.scale.setScalar(scale);model.position.sub(centre);root.add(model);
for(let i=0;i<8;i++)model.add(new THREE.SpotLight(0xffddaa,1));
const effects=makeShipEffects(root,model,engines.Arboghast.engines,centre,scale);
effects.setLights(true);effects.setPlumes(true);root.updateMatrixWorld(true);
assert.equal(effects.stats().visiblePlumes,4);assert.equal(effects.stats().activeSourceLights,8);
const group=root.getObjectByName('Browser engine effects');
for(let i=0;i<4;i++)assert.ok(group.children[i].getWorldPosition(new THREE.Vector3()).distanceTo(new THREE.Vector3(...engines.Arboghast.engines[i].position).sub(centre).multiplyScalar(scale))<1e-7,'Source centering/scale applied incorrectly');
effects.setPlumes(false);assert.equal(effects.stats().visiblePlumes,0);assert.equal(effects.tick(1),false);
effects.setPlumes(true);assert.equal(effects.tick(2),true);effects.dispose();assert.equal(root.getObjectByName('Browser engine effects'),undefined);
console.log('PASS: four measured canted outlets, clear mouth fit, one source transform, eight retained lights, exhaust on/off and disposal; other registered ships unchanged. No GPU/native proof.');
