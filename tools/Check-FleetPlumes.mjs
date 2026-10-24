// Actual shared effect with every registered drive. No WebGL/native proof.
import fs from 'node:fs';
import assert from 'node:assert/strict';
import * as THREE from '../FleetStudio/vendor/three/build/three.module.js';
const base=new URL('../',import.meta.url),read=p=>fs.readFileSync(new URL(p,base));
const threeURL=new URL('FleetStudio/vendor/three/build/three.module.js',base).href;
const source=read('FleetStudio/ship-effects.js');
assert.ok(source.equals(read('Assets/FleetViewer/rocinante-effects.js')),'Shared effects must agree');
const {makeShipEffects,configurePlumeRendering,ownShipShadows}=await import('data:text/javascript;base64,'+Buffer.from(source.toString().replace("from 'three'",'from '+JSON.stringify(threeURL))).toString('base64'));
const engines=JSON.parse(read('FleetStudio/engines.json'));let drives=0;
for(const [id,record] of Object.entries(engines)){
 const root=new THREE.Group(),model=new THREE.Group(),hull=new THREE.MeshStandardMaterial({emissive:0x221100,emissiveIntensity:3});
 model.add(new THREE.Mesh(new THREE.BoxGeometry(.1,.1,.1),hull),new THREE.SpotLight(0xffcc99,7));root.add(model);root.scale.setScalar(.017);
 const effects=makeShipEffects(root,model,record.engines,new THREE.Vector3(2,3,4),.017);effects.setPlumes(true);
 assert.equal(effects.stats().plumeModel,'shared-3d-flow-v2',id+': old surface plume');
 assert.equal(effects.stats().activePlumeLights,record.engines.length,id+': missing actual plume illumination');
 const volumes=[],lights=[];root.traverse(o=>{if(o.isMesh&&o.material.userData.fleetPlumeEmission)volumes.push(o);if(o.isLight&&o.userData.plumeLight)lights.push(o);});
 assert.equal(volumes.length,record.engines.length);assert.equal(lights.length,record.engines.length);
 for(let i=0;i<volumes.length;i++){
  const volume=volumes[i],anchor=record.engines[i],uniforms=volume.material.uniforms,stealth=anchor.plumeProfile==='stealth';
  if(id==='Anubis'||id==='AnubisOriginal')assert.equal(stealth,true,id+': stealth exhaust palette is missing');
  else assert.equal(stealth,false,id+': ordinary ship acquired a stealth palette');
  const canterbury=id==='Canterbury',knight=id==='Knight';
  assert.equal(anchor.plumeProfile==='knight',knight,id+': Knight profile routing');
  assert.equal(anchor.plumeProfile==='canterbury',canterbury,id+': Canterbury profile routing');
  const expectedNear=stealth?[.72,1,.8]:canterbury?[1,.98,1]:knight?[1,.97,1]:[1,.97,.91],expectedFar=stealth?[.22,.72,.4]:canterbury?[.25,.5,1]:knight?[.46,.37,1]:[.48,.78,1];
  assert.deepEqual(uniforms.coreNear.value.toArray(),expectedNear,id+': hot core colour');
  assert.deepEqual(uniforms.coreFar.value.toArray(),expectedFar,id+': flowing core colour');
  assert.deepEqual(uniforms.tint.value.toArray(),anchor.color||[.09,.42,1],id+': gas colour');
  assert.deepEqual(lights[i].color.toArray(),new THREE.Color(...(stealth?[.65,.9,.72]:canterbury?[.58,.75,1]:knight?[.70,.65,1]:[.78,.9,1])).lerp(uniforms.tint.value,.18).toArray(),id+': reflected exhaust light palette');
  if(stealth)for(const color of [uniforms.tint.value,uniforms.coreNear.value,uniforms.coreFar.value,lights[i].color])assert.ok(color.g>color.r&&color.g>color.b,id+': green tinge lost');
  assert.ok(volume.scale.y<=anchor.length+1e-8,id+': nozzle-radius minimum overrode the registered length');
  assert.ok(volume.scale.y/volume.userData.hullLength<=(stealth?.18:canterbury?.23:knight?.26:.6)+1e-8,id+': plume is out of proportion to the hull');
  assert.equal(volume.material.toneMapped,false);assert.equal(volume.material.depthWrite,false);assert.equal(volume.castShadow,false);
  assert.equal(volume.material.depthTest,false,'Proxy exit depth must not discard gas in front of a hull');
  assert.ok(volume.material.uniforms.gas.value.isData3DTexture&&volume.material.uniforms.viewToPlume,'Exhaust must sample spatial density and clip integrated rays against real hull depth');
  assert.equal(volume.geometry,volumes[0].geometry,id+': duplicated plume geometry');
  assert.equal(volume.material.uniforms.gas.value,volumes[0].material.uniforms.gas.value,id+': per-engine animation instead of one shared field');
  assert.equal(volume.material.uniforms.time,volumes[0].material.uniforms.time,id+': independent plume clocks');
 }
 effects.tick(.125);for(const volume of volumes)assert.equal(volume.material.uniforms.time.value,.125);
 for(const light of lights){assert.equal(light.decay,2);assert.equal(light.castShadow,true);assert.ok(light.distance>0&&light.intensity>0);assert.equal(light.isSpotLight,true);assert.ok(light.shadow.camera.near<light.distance);assert.equal(light.shadow.autoUpdate,false);}
 root.updateMatrixWorld(true);effects.tick(0);
 const projected=lights.map(light=>light.parent.localToWorld(new THREE.Vector3(0,-1,0)).applyMatrix4(light.shadow.matrix));
 root.rotation.y=.7;root.updateMatrixWorld(true);effects.tick(1);
 for(let i=0;i<lights.length;i++)assert.ok(lights[i].parent.localToWorld(new THREE.Vector3(0,-1,0)).applyMatrix4(lights[i].shadow.matrix).distanceTo(projected[i])<1e-5,'Cached engine shadow projection drifted during rigid rotation: '+id);
 effects.setLights(false);assert.equal(effects.stats().activePlumeLights,record.engines.length,'Hull lights toggle extinguished exhaust');
 effects.setPlumeLighting(false);assert.equal(effects.stats().activePlumeLights,0);assert.equal(effects.stats().visiblePlumes,record.engines.length);
 effects.setPlumeLighting(true);effects.setPlumes(false);assert.equal(effects.stats().activePlumeLights,0);assert.equal(effects.tick(2),false);
 effects.setLights(true);assert.equal(hull.emissiveIntensity,3);effects.dispose();assert.equal(root.getObjectByName('Browser engine effects'),undefined);
 model.children[0].geometry.dispose();hull.dispose();drives+=record.engines.length;
}
function illumination(units){const a={position:[0,0,0],direction:[0,-1,0],radius:units,length:units*6},root=new THREE.Group(),model=new THREE.Group();root.add(model);root.scale.setScalar(.1/units);const effect=makeShipEffects(root,model,[a],new THREE.Vector3(),.1/units);let light;root.traverse(o=>{if(o.userData.plumeLight)light=o;});const r={intensity:light.intensity,distance:light.distance};effect.dispose();return r;}
assert.deepEqual(illumination(1),illumination(1000),'Source-unit scale changed light strength/range');
// A large nozzle cannot inflate a plume beyond its hull. Parent placement and
// authoring units cannot change that proportion or the stealth signature.
function signature(stealth,units=1){
 const root=new THREE.Group(),model=new THREE.Group(),hull=new THREE.Mesh(new THREE.BoxGeometry(2*units,10*units,2*units),new THREE.MeshStandardMaterial());
 root.add(model);model.add(hull);root.scale.setScalar(.1/units);root.rotation.set(.3,.6,.2);root.position.set(7,-3,2);
 const anchor={position:[0,-5*units,0],direction:[0,-1,0],radius:.5*units,length:8*units,plumeProfile:stealth?'stealth':'standard'};
 const effect=makeShipEffects(root,model,[anchor],new THREE.Vector3(),.1/units);let plume,light;
 root.traverse(o=>{if(o.material?.userData.fleetPlumeEmission)plume=o;if(o.userData.plumeLight)light=o;});
 const result={ratio:effect.stats().maxPlumeToHullRatio,radiance:plume.material.uniforms.radiance.value,density:plume.material.uniforms.densityScale.value,light:light.intensity,reach:light.distance};
 effect.dispose();hull.geometry.dispose();hull.material.dispose();return result;
}
const ordinary=signature(false),stealth=signature(true),largeUnits=signature(true,1000);
assert.ok(ordinary.ratio<=.600001&&stealth.ratio<=.180001&&stealth.ratio<ordinary.ratio/2,'Stealth exhaust must stay compact relative to the same hull');
assert.ok(stealth.radiance<ordinary.radiance*.2&&stealth.density<ordinary.density*.5&&stealth.light<ordinary.light*.1&&stealth.reach<ordinary.reach*.5,'Stealth requires a lower signature, not only green paint');
for(const key of Object.keys(stealth))assert.ok(Math.abs(stealth[key]-largeUnits[key])<1e-8,'Parent fit/source units changed stealth '+key);
// Density must vary along all three spatial axes, not only along a plane/axis.
{
 const root=new THREE.Group(),model=new THREE.Group();root.add(model);
 const effect=makeShipEffects(root,model,[{position:[0,0,0],direction:[0,-1,0],radius:1,length:6}],new THREE.Vector3(),1);
 let plume;root.traverse(o=>{if(o.material?.userData.fleetPlumeEmission)plume=o;});
 const texture=plume.material.uniforms.gas.value,{data,width,height,depth}=texture.image;
 assert.ok(width>1&&height>1&&depth>1&&data.length===width*height*depth);
 for(const stride of [1,width,width*height]){
  let difference=0;for(let i=0;i<data.length-stride;i+=137)difference+=Math.abs(data[i]-data[i+stride]);assert.ok(difference>500,'Density collapsed in a spatial dimension');
 }
 let fieldDisposed=false;texture.addEventListener('dispose',()=>fieldDisposed=true);effect.dispose();assert.equal(fieldDisposed,true);
}
const camera=new THREE.PerspectiveCamera(),originalMask=camera.layers.mask;let aoMask;
const ao={scene:new THREE.Scene(),render(){aoMask=camera.layers.mask;throw Error('AO failure');}};const depthPass=configurePlumeRendering(camera,ao);
const beautyMask=camera.layers.mask;assert.notEqual(beautyMask,originalMask);
assert.throws(()=>ao.render(),/AO failure/);assert.equal(aoMask,originalMask);assert.equal(camera.layers.mask,beautyMask,'AO layer state leaked');
// A failed depth draw must restore the surrounding renderer and scene state.
const proxy=new THREE.Mesh(new THREE.BoxGeometry(),new THREE.MeshBasicMaterial());proxy.material.userData.fleetPlumeEmission=true;ao.scene.add(proxy);
const oldTarget=new THREE.WebGLRenderTarget(8,8),oldOverride=new THREE.MeshNormalMaterial(),background=new THREE.Color(0x112233);
ao.scene.overrideMaterial=oldOverride;ao.scene.background=background;
const fakeRenderer={target:oldTarget,autoClear:false,shadowMap:{enabled:true},getRenderTarget(){return this.target;},setRenderTarget(value){this.target=value;},getDrawingBufferSize(value){return value.set(16,16);},clear(){},render(){assert.equal(camera.layers.mask,originalMask);throw Error('Depth failure');}};
assert.throws(()=>depthPass.render(fakeRenderer),/Depth failure/);assert.equal(camera.layers.mask,beautyMask);assert.equal(fakeRenderer.target,oldTarget);assert.equal(fakeRenderer.autoClear,false);assert.equal(fakeRenderer.shadowMap.enabled,true);assert.equal(ao.scene.overrideMaterial,oldOverride);assert.equal(ao.scene.background,background);assert.equal(camera.userData.fleetPlumeDepth.valid,false);
depthPass.dispose();assert.equal(camera.userData.fleetPlumeDepth,undefined);oldTarget.dispose();oldOverride.dispose();proxy.geometry.dispose();proxy.material.dispose();
// Shadow uniforms cannot retain disposed textures from a previous ship. Honour
// an artist-supplied custom depth shader and release only the ones we own.
{
 const a=new THREE.MeshStandardMaterial(),b=new THREE.MeshStandardMaterial(),model=new THREE.Group();
 const meshes=[a,a,b].map(material=>new THREE.Mesh(new THREE.BoxGeometry(),material));model.add(...meshes);
 const sourceCustom=new THREE.MeshDepthMaterial();meshes[2].customDepthMaterial=sourceCustom;
 const shadows=ownShipShadows(model);
 assert.equal(meshes[0].customDepthMaterial,meshes[1].customDepthMaterial);assert.equal(meshes[2].customDepthMaterial,sourceCustom);
 const previousVersion=meshes[0].customDepthMaterial.version;shadows.invalidate();assert.ok(meshes[0].customDepthMaterial.version>previousVersion);
 let disposed=false;meshes[0].customDepthMaterial.addEventListener('dispose',()=>disposed=true);shadows.dispose();
 assert.equal(disposed,true);assert.equal(meshes[0].customDepthMaterial,undefined);assert.equal(meshes[2].customDepthMaterial,sourceCustom);
 for(const mesh of meshes)mesh.geometry.dispose();a.dispose();b.dispose();sourceCustom.dispose();
}
console.log(`PASS: ${Object.keys(engines).length} drive records / ${drives} outlets: shared 3D density/geometry/clock, hull-relative length, subdued stealth signature, shadowed inverse-square spill, source-unit invariance, independent controls, AO isolation and disposal. No native/GPU proof.`);
