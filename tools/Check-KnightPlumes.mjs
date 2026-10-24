// Actual isolated Knight effect and fitted outlet data; no WebGL/native claim.
import fs from 'node:fs';
import assert from 'node:assert/strict';
import * as THREE from '../FleetStudio/vendor/three/build/three.module.js';
const base=new URL('../',import.meta.url),read=p=>fs.readFileSync(new URL(p,base));
const threeURL=new URL('FleetStudio/vendor/three/build/three.module.js',base).href;
const effectsText=read('FleetStudio/ship-effects.js').toString();
const {makeShipEffects}=await import('data:text/javascript;base64,'+Buffer.from(effectsText.replace("from 'three'",'from '+JSON.stringify(threeURL))).toString('base64'));
const anchors=JSON.parse(read('Assets/Models/Source/Knight-review04/engines.json')).engines;
const data=read('Assets/Models/Source/Knight-review04/Knight.glb'),size=data.readUInt32LE(12),doc=JSON.parse(data.subarray(20,20+size));
const bounds=new THREE.Box3();
function walk(index,parent){
 const node=doc.nodes[index],local=node.matrix?new THREE.Matrix4().fromArray(node.matrix):new THREE.Matrix4().compose(new THREE.Vector3(...(node.translation||[0,0,0])),new THREE.Quaternion(...(node.rotation||[0,0,0,1])),new THREE.Vector3(...(node.scale||[1,1,1])));
 const world=new THREE.Matrix4().multiplyMatrices(parent,local);
 if(node.mesh!==undefined)for(const p of doc.meshes[node.mesh].primitives){const a=doc.accessors[p.attributes.POSITION];bounds.union(new THREE.Box3(new THREE.Vector3(...a.min),new THREE.Vector3(...a.max)).applyMatrix4(world));}
 for(const child of node.children||[])walk(child,world);
}
for(const index of doc.scenes[doc.scene||0].nodes)walk(index,new THREE.Matrix4());
const dimensions=bounds.getSize(new THREE.Vector3()),center=bounds.getCenter(new THREE.Vector3()),scale=3.2/Math.max(...dimensions.toArray());
const root=new THREE.Group(),model=new THREE.Group(),material=new THREE.MeshStandardMaterial({emissive:0x111111,emissiveIntensity:2});
const shell=new THREE.Mesh(new THREE.BoxGeometry(...dimensions.toArray()),material);shell.position.copy(center);model.add(shell,new THREE.PointLight(0xffffff,4));model.position.sub(center);root.add(model);root.scale.setScalar(scale);
const effect=makeShipEffects(root,model,anchors,center,scale);effect.setPlumes(true);
const volumes=[],lamps=[];
root.traverse(o=>{if(o.material?.userData.fleetPlumeEmission)volumes.push(o);if(o.userData.plumeLight)lamps.push(o);});
assert.equal(volumes.length,3);assert.equal(lamps.length,3);
const field=volumes[0].material.uniforms.gas.value;
assert.equal(field.isData3DTexture,true);assert.ok(field.image.depth>1);
for(let i=0;i<3;i++){
 const v=volumes[i],a=anchors[i],u=v.material.uniforms;
 assert.equal(u.gas.value,field,'Each drive must share the one spatial density field');
 assert.equal(u.time,volumes[0].material.uniforms.time,'Each drive must share the one animation clock');
 assert.equal(v.geometry,volumes[0].geometry);
 assert.ok(new THREE.Vector3(0,1,0).applyQuaternion(v.parent.quaternion).dot(new THREE.Vector3(...a.direction))>.999999);
 assert.ok(v.scale.y/v.userData.hullLength<=.26+1e-8,'Plume exceeds the actual fitted hull limit');
 assert.equal(v.material.depthWrite,false);assert.equal(v.material.uniforms.clipHull.value,false);
 assert.ok(u.viewToPlume&&u.hullDepth,'Actual hull-depth clipping is missing');
 assert.ok(u.coreNear.value.r>.95&&u.coreNear.value.g>.9&&u.coreNear.value.b>.95,'Hot core must remain white');
 assert.ok(u.coreFar.value.b>u.coreFar.value.r&&u.coreFar.value.b>u.coreFar.value.g,'Tail lost its violet colour');
 assert.equal(lamps[i].decay,2);assert.equal(lamps[i].castShadow,true);assert.ok(lamps[i].intensity>0&&lamps[i].distance>0);
}
root.updateMatrixWorld(true);effect.tick(.75);assert.equal(volumes[2].material.uniforms.time.value,.75);
const projected=lamps.map(l=>l.parent.localToWorld(new THREE.Vector3(0,-1,0)).applyMatrix4(l.shadow.matrix));
root.rotation.set(.4,.7,-.2);root.updateMatrixWorld(true);effect.tick(1.25);
for(let i=0;i<lamps.length;i++)assert.ok(lamps[i].parent.localToWorld(new THREE.Vector3(0,-1,0)).applyMatrix4(lamps[i].shadow.matrix).distanceTo(projected[i])<1e-5,'Nozzle spill drifts during ship rotation');
effect.setLights(false);assert.equal(effect.stats().activePlumeLights,3);assert.equal(material.emissiveIntensity,0);
effect.setPlumes(false);assert.equal(effect.stats().activePlumeLights,0);assert.equal(effect.tick(2),false);
effect.setLights(true);assert.equal(material.emissiveIntensity,2);
let disposed=false;field.addEventListener('dispose',()=>disposed=true);effect.dispose();assert.equal(disposed,true);
shell.geometry.dispose();material.dispose();
console.log('PASS: Knight has three fitted volumetric plumes, one shared 3D field/geometry/clock, white/violet emission, hull-relative length, rigid shadowed light spill, independent controls and disposal. Source/runtime evidence only.');
