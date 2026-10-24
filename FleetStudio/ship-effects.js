import * as THREE from 'three';

// Profiles share one gas animation. Size is bounded by the actual hull; stealth
// exhaust has a short, subdued signature, separate from accepted source lights.
const plumePalettes={
 knight:{near:[1,.97,1],far:[.46,.37,1],spill:[.70,.65,1],lengthRatio:.26,radiance:.65,density:.80,illumination:.65,reach:5.5},
 standard:{near:[1,.97,.91],far:[.48,.78,1],spill:[.78,.9,1],lengthRatio:.6,radiance:1,density:1,illumination:1,reach:7},
 stealth:{near:[.72,1,.8],far:[.22,.72,.4],spill:[.65,.9,.72],lengthRatio:.18,radiance:.12,density:.4,illumination:.035,reach:2},
 canterbury:{near:[1,.98,1],far:[.25,.5,1],spill:[.58,.75,1],lengthRatio:.23,radiance:.72,density:.80,illumination:.65,reach:5.5}
};

// Give each source material its own shadow uniforms. The renderer's shared depth
// material can otherwise retain a texture from a disposed previous ship.
export function ownShipShadows(model){
 const depth=new Map(),distance=new Map(),owned=[];
 model.traverse(o=>{
  if(!o.isMesh||Array.isArray(o.material))return;
  for(const [property,cache,Type] of [['customDepthMaterial',depth,THREE.MeshDepthMaterial],['customDistanceMaterial',distance,THREE.MeshDistanceMaterial]]){
   if(o[property])continue;let material=cache.get(o.material);if(!material){material=new Type();cache.set(o.material,material);}
   o[property]=material;owned.push({object:o,property,material});
  }
 });
 return {
  invalidate(){for(const material of [...depth.values(),...distance.values()])material.needsUpdate=true;},
  dispose(){for(const {object,property,material} of owned)if(object[property]===material)delete object[property];for(const material of [...depth.values(),...distance.values()])material.dispose();}
 };
}

// One local-space gas field, geometry and clock for every outlet on a ship.
// The texture has depth; it is never a sprite, a camera-facing plane or a shell.
function plumeAnimation(clock){
 const size=64,data=new Uint8Array(size**3);
 const hash=(x,y,z,n)=>{let h=Math.imul(x%n,374761393)^Math.imul(y%n,668265263)^Math.imul(z%n,2147483647);h=Math.imul(h^(h>>>13),1274126177);return ((h^(h>>>16))>>>0)/4294967295;};
 function noise(x,y,z,n){
  const ix=Math.floor(x),iy=Math.floor(y),iz=Math.floor(z);let u=x-ix,v=y-iy,w=z-iz;u=u*u*(3-2*u);v=v*v*(3-2*v);w=w*w*(3-2*w);
  const mix=(a,b,t)=>a+(b-a)*t;
  return mix(mix(mix(hash(ix,iy,iz,n),hash(ix+1,iy,iz,n),u),mix(hash(ix,iy+1,iz,n),hash(ix+1,iy+1,iz,n),u),v),mix(mix(hash(ix,iy,iz+1,n),hash(ix+1,iy,iz+1,n),u),mix(hash(ix,iy+1,iz+1,n),hash(ix+1,iy+1,iz+1,n),u),v),w);
 }
 for(let z=0,i=0;z<size;z++)for(let y=0;y<size;y++)for(let x=0;x<size;x++,i++){
  let value=0;for(const [cells,weight] of [[8,.62],[16,.27],[32,.11]])value+=weight*noise(x*cells/size,y*cells/size,z*cells/size,cells);
  data[i]=Math.round(value*255);
 }
 const field=new THREE.Data3DTexture(data,size,size,size);field.name='Shared advected 3D exhaust density';field.format=THREE.RedFormat;
 field.minFilter=field.magFilter=THREE.LinearFilter;field.wrapS=field.wrapT=field.wrapR=THREE.RepeatWrapping;field.unpackAlignment=1;field.needsUpdate=true;
 const geometry=new THREE.BoxGeometry(3.2,1,3.2);geometry.translate(0,.5,0);
 const vertex=`out vec3 vLocal;void main(){vLocal=position;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}`;
 const fragment=`precision highp sampler3D;
 uniform float time;uniform vec3 tint;uniform vec3 coreNear;uniform vec3 coreFar;uniform sampler3D gas;uniform float aspect;uniform float radiance;uniform float densityScale;
 uniform vec3 eye;uniform vec3 direction;uniform bool orthographic;
 uniform sampler2D hullDepth;uniform bool clipHull;uniform vec2 depthSize;uniform mat4 projectionInverse;uniform mat4 viewToPlume;
 in vec3 vLocal;out vec4 outColor;
 void main(){
  vec3 ray=orthographic?direction:normalize(vLocal-eye),origin=orthographic?vLocal-ray*8.:eye;
  vec3 inverseRay=1./(mix(vec3(1.),sign(ray),step(vec3(.000001),abs(ray)))*max(abs(ray),vec3(.000001)));
  vec3 a=(vec3(-1.6,0.,-1.6)-origin)*inverseRay,b=(vec3(1.6,1.,1.6)-origin)*inverseRay;
  vec3 nearPlane=min(a,b),farPlane=max(a,b);
  float enter=max(0.,max(nearPlane.x,max(nearPlane.y,nearPlane.z))),leave=min(farPlane.x,min(farPlane.y,farPlane.z));
  // Clip the integrated gas, rather than testing the depth of its proxy's exit face.
  if(clipHull){vec2 uv=gl_FragCoord.xy/depthSize;float depth=texture(hullDepth,uv).x;
   vec4 view=projectionInverse*vec4(uv*2.-1.,depth*2.-1.,1.);view/=view.w;
   vec3 stop=(viewToPlume*view).xyz;leave=min(leave,dot(stop-origin,ray));}
  if(leave<=enter)discard;
  float ds=(leave-enter)/72.,physicalStep=length(ray*vec3(1.,aspect,1.))*ds,transmission=1.;vec3 emission=vec3(0.);
  for(int i=0;i<72;i++){
   vec3 p=origin+ray*(enter+(float(i)+.5)*ds);float t=clamp(p.y,0.,1.),axial=t*aspect;
   // Time translates the density outward along the nozzle axis in 3D space.
   vec3 flow=vec3(p.x,axial-time*6.,p.z);
   float coarse=texture(gas,flow*.17).r,fine=texture(gas,flow*.39+vec3(.23,.41,.13)).r;
   float turbulence=coarse*.72+fine*.28;
   vec2 bend=vec2(texture(gas,flow*.11+vec3(.31,.17,.53)).r-.5,texture(gas,flow*.11+vec3(.71,.37,.19)).r-.5)*t*.48;
   float radius=length(p.xz-bend);
   float width=(.66+.28*(1.-exp(-axial*.9)))*(1.-.58*pow(t,1.35))*(.78+.45*turbulence);
   float edge=1.-smoothstep(width*.5,width,radius);
   float tail=exp(-t*1.7)*(1.-smoothstep(.70,1.,t));
   float body=edge*tail*smoothstep(.22,.69,turbulence);
   float coreWidth=.34*(1.-.78*t);
   float core=exp(-radius*radius/(coreWidth*coreWidth))*exp(-t*3.6)*(.80+.35*turbulence);
   float density=(body*.52+core*.75)*densityScale,attenuation=exp(-density*physicalStep*.32);
   vec3 hot=mix(coreNear,coreFar,smoothstep(.02,.45,t));
   emission+=transmission*(hot*core*10.+tint*body*2.8)*physicalStep;
   transmission*=attenuation;
  }
  outColor=vec4(emission*radiance,1.-transmission);
 }`;
 return {geometry,field,material(color,length,radius,palette){
  const m=new THREE.ShaderMaterial({name:'Shared flowing 3D engine gas',glslVersion:THREE.GLSL3,
   uniforms:{time:clock,tint:{value:color},coreNear:{value:new THREE.Color(...palette.near)},coreFar:{value:new THREE.Color(...palette.far)},radiance:{value:palette.radiance},densityScale:{value:palette.density},gas:{value:field},aspect:{value:length/radius},eye:{value:new THREE.Vector3()},direction:{value:new THREE.Vector3()},orthographic:{value:false},hullDepth:{value:null},clipHull:{value:false},depthSize:{value:new THREE.Vector2(1,1)},projectionInverse:{value:new THREE.Matrix4()},viewToPlume:{value:new THREE.Matrix4()}},
   vertexShader:vertex,fragmentShader:fragment,transparent:true,depthTest:false,depthWrite:false,side:THREE.BackSide,blending:THREE.AdditiveBlending,premultipliedAlpha:true,toneMapped:false});
  m.userData.fleetPlumeEmission=true;return m;
 }};
}

// This module adds inspection effects to a display copy. Source models and
// their materials stay separately recoverable; no monitoring data is touched.
export function makeShipEffects(root,model,anchors,center,scale){
 const group=new THREE.Group();group.name='Browser engine effects';root.add(group);
 const emitters=new Map(),lights=[],bowSpill=[],cores=[],plumeLights=[],plumes=[],geometries=[],materials=[];
 model.traverse(o=>{
  if(o.isLight)lights.push({object:o,intensity:o.intensity});
  if(o.isMesh)for(const m of [].concat(o.material))if(m.emissive&&(m.emissiveMap||m.emissive.r+m.emissive.g+m.emissive.b>0))emitters.set(m,m.emissiveIntensity??1);
 });
 // Anubis only: the existing upper/lower lining, sidewalls and step returns
 // emit around the mouth. Bright patches and cavity spill retain its depth.
 const bowMaterial=[...emitters.keys()].find(m=>m.userData.anubisInteriorGlow===true);
 if(bowMaterial){
  bowMaterial.onBeforeCompile=shader=>{
   shader.vertexShader='varying vec3 vBowSource;\n'+shader.vertexShader;
   shader.vertexShader=shader.vertexShader.replace('#include <begin_vertex>','#include <begin_vertex>\nvBowSource=position;');
   shader.fragmentShader='varying vec3 vBowSource;\n'+shader.fragmentShader;
   shader.fragmentShader=shader.fragmentShader.replace('#include <emissivemap_fragment>',`#include <emissivemap_fragment>
    float perimeterAngle=atan(vBowSource.z/16.0,(vBowSource.y+7.5)/6.5);
    float mouthDepth=(vBowSource.x+66.5+1.2*sin(perimeterAngle*3.0))/5.5;
    float depthGlow=exp(-mouthDepth*mouthDepth);
    float patchWave=.5+.5*sin(perimeterAngle*5.0+.75);
    float patches=.42+.58*patchWave*patchWave;
    totalEmissiveRadiance*=depthGlow*patches;`);
  };
  bowMaterial.customProgramCacheKey=()=> 'anubis-full-inner-bow-v2';
  bowMaterial.needsUpdate=true;
  const bowSources=[[-65.5,-5.4,-10.5],[-65.5,-4.3,0],[-65.5,-5.4,10.5],[-65.5,-8,14.5],
   [-65.5,-10.6,10.5],[-65.5,-11.6,0],[-65.5,-10.6,-10.5],[-65.5,-8,-14.5]];
  for(const source of bowSources){
   const spill=new THREE.PointLight(0x168dff,24*scale*scale,6.5*scale,2);
   spill.name='Confined inner bow-perimeter spill';spill.position.set(...source).sub(center);
   spill.userData.bowRegion='mouth';
   spill.castShadow=false;group.add(spill);bowSpill.push(spill);
  }
  // The rear wall is 41.6 source units behind the mouth, beyond those small
  // front lights. Concealed fills illuminate the actual background and port
  // surround from free space; their reach stops well short of the mouth.
  for(const z of [-8,8]){
   const fill=new THREE.PointLight(0x168dff,480*scale*scale,16*scale,2);
   fill.name='Inner bow background fill';fill.position.set(-32,-8,z).sub(center);
   fill.userData.bowRegion='background';fill.castShadow=false;group.add(fill);bowSpill.push(fill);
  }
 }
 const clock={value:0},shadowRotation=new THREE.Quaternion(),animation=anchors.length?plumeAnimation(clock):null;
 if(animation)geometries.push(animation.geometry);
 // Measure in the same source frame as the anchors. Parent fit, rotation and
 // placement must not change the exhaust-to-hull proportion.
 model.updateWorldMatrix(true,true);
 const toRoot=new THREE.Matrix4().copy(root.matrixWorld).invert(),matrix=new THREE.Matrix4(),hullBounds=new THREE.Box3();
 model.traverse(o=>{if(!o.isMesh)return;if(!o.geometry.boundingBox)o.geometry.computeBoundingBox();hullBounds.union(o.geometry.boundingBox.clone().applyMatrix4(matrix.multiplyMatrices(toRoot,o.matrixWorld)));});
 const hullSize=hullBounds.isEmpty()?null:hullBounds.getSize(new THREE.Vector3());
 for(const a of anchors){
  const p=new THREE.Vector3(...a.position).sub(center),d=new THREE.Vector3(...a.direction).normalize();
  const node=new THREE.Group();node.position.copy(p);node.quaternion.setFromUnitVectors(new THREE.Vector3(0,1,0),d);group.add(node);
  const color=new THREE.Color(...(a.color||[.09,.42,1])),palette=plumePalettes[a.plumeProfile]||plumePalettes.standard;
  const hullLength=hullSize?Math.abs(d.x)*hullSize.x+Math.abs(d.y)*hullSize.y+Math.abs(d.z)*hullSize.z:0;
  const length=Math.min(Math.max(a.length,.00001),hullLength>0?hullLength*palette.lengthRatio:Infinity),m=animation.material(color,length,a.radius,palette);materials.push(m);
  const plume=new THREE.Mesh(animation.geometry,m);plume.scale.set(a.radius,length,a.radius);plume.name='Flowing 3D engine plume';plume.layers.set(30);plume.castShadow=false;plume.receiveShadow=false;plume.renderOrder=10;
  plume.userData.hullLength=hullLength;
  const inverse=new THREE.Matrix4();plume.onBeforeRender=(_renderer,_scene,camera)=>{
   inverse.copy(plume.matrixWorld).invert();camera.getWorldPosition(m.uniforms.eye.value).applyMatrix4(inverse);camera.getWorldDirection(m.uniforms.direction.value).transformDirection(inverse);
   const depth=camera.userData.fleetPlumeDepth;m.uniforms.clipHull.value=Boolean(depth?.valid);m.uniforms.hullDepth.value=depth?.texture??null;if(depth)m.uniforms.depthSize.value.copy(depth.size);
   m.uniforms.viewToPlume.value.multiplyMatrices(inverse,camera.matrixWorld);m.uniforms.projectionInverse.value.copy(camera.projectionMatrixInverse);
   m.uniforms.orthographic.value=Boolean(camera.isOrthographicCamera);m.uniformsNeedUpdate=true;
  };
  node.add(plume);plumes.push(plume);
  // Source positions inherit the root transform. Photometry/range use world units
  // once, so differently authored model units do not change the visible light.
  const worldRadius=Math.max(a.radius*scale,.00001);
  const spillColor=new THREE.Color(...palette.spill).lerp(color,.18);
  const reach=worldRadius*palette.reach;
  const spill=new THREE.SpotLight(spillColor,65*worldRadius*worldRadius*palette.illumination,reach,1.35,.75,2);
  spill.name='Exhaust radiance on drive hardware';spill.userData.plumeLight=true;
  spill.position.set(0,a.radius*.65,0);spill.target.position.set(0,-a.radius,0);
  spill.castShadow=true;spill.shadow.mapSize.set(256,256);spill.shadow.camera.near=Math.max(.00001,worldRadius*.035);spill.shadow.camera.far=reach;
  spill.shadow.autoUpdate=false;spill.shadow.needsUpdate=true;
  spill.shadow.bias=-.00002;spill.shadow.normalBias=worldRadius*.025;
  node.add(spill,spill.target);plumeLights.push(spill);
  if(a.addCore){
   const g=new THREE.CircleGeometry(a.radius*.79,64);geometries.push(g);
   const mat=new THREE.MeshStandardMaterial({color:0x03070a,emissive:color,emissiveIntensity:5,roughness:.7,metalness:0,side:THREE.DoubleSide});materials.push(mat);
   const core=new THREE.Mesh(g,mat);core.rotation.x=-Math.PI/2;core.position.y=-a.radius*.14;core.name='Recessed inspection drive core';node.add(core);cores.push({object:core,material:mat});
  }
 }
 let lit=true,burning=false,plumeLighting=true;
 function apply(){
  for(const [m,intensity] of emitters)m.emissiveIntensity=lit?intensity:0;
  for(const a of lights)a.object.intensity=lit?a.intensity:0;
  for(const light of bowSpill)light.visible=lit;
  for(const p of plumes)p.visible=burning;
  for(const l of plumeLights)l.visible=burning&&plumeLighting;
  for(const c of cores)c.material.emissiveIntensity=lit||burning?5:0;
 }
 return {
  engineCount:anchors.length,emitterMaterials:emitters.size,sourceLightCount:lights.length,
  setLights(value){lit=Boolean(value);apply();},
  setPlumes(value){burning=Boolean(value);apply();},
  setPlumeLighting(value){plumeLighting=Boolean(value);apply();},
  invalidateShadows(){for(const light of plumeLights)light.shadow.needsUpdate=true;},
  tick(t){
   clock.value=t;
   // The emitter and its occluders form one rigid ship. Keep the projection in
   // the ship's frame while reusing its depth map through orbit/rotation.
   for(const light of plumeLights){light.updateWorldMatrix(true,false);light.target.updateWorldMatrix(true,false);light.parent.getWorldQuaternion(shadowRotation);light.shadow.camera.up.set(0,0,1).applyQuaternion(shadowRotation);light.shadow.updateMatrices(light);}
   return burning&&plumes.length>0;
  },
  stats(){return {plumeModel:'shared-3d-flow-v2',maxPlumeToHullRatio:Math.max(0,...plumes.map(p=>p.userData.hullLength>0?p.scale.y/p.userData.hullLength:0)),activePlumeLights:plumeLights.filter(l=>l.visible).length,lit,burning,engineCount:anchors.length,activeEmitters:lit?emitters.size:0,activeSourceLights:lit?lights.length:0,activeBowSpillLights:lit?bowSpill.length:0,visiblePlumes:burning?plumes.length:0};},
  dispose(){root.remove(group);for(const light of plumeLights)light.shadow?.dispose();for(const g of geometries)g.dispose();for(const m of materials)m.dispose();animation?.field.dispose();}
 };
}

// Gas emits light but is not an opaque occluder in the ambient-occlusion pass.
export function configurePlumeRendering(camera,ao){
 camera.layers.enable(30);
 if(ao.fleetPlumesConfigured)return;
 const render=ao.render;
 ao.render=function(...args){const mask=camera.layers.mask;camera.layers.disable(30);try{return render.apply(this,args);}finally{camera.layers.mask=mask;}};
 ao.fleetPlumesConfigured=true;
 // The nearest solid hull clips every ray segment, including gas in front of
 // the hull. Using the proxy's back-face depth makes that gas disappear in orbit.
 const depth=new THREE.DepthTexture(1,1),target=new THREE.WebGLRenderTarget(1,1,{depthTexture:depth});
 const material=new THREE.MeshDepthMaterial(),size=new THREE.Vector2(1,1);
 const record={texture:depth,size,valid:false};camera.userData.fleetPlumeDepth=record;
 return {
  enabled:true,needsSwap:false,renderToScreen:false,
  setSize(w,h){target.setSize(w,h);size.set(w,h);},
  render(renderer,_write,read){
   record.valid=false;let active=false;ao.scene.traverseVisible(o=>{if(o.isMesh&&[].concat(o.material).some(m=>m.userData.fleetPlumeEmission))active=true;});if(!active)return;
   if(read){if(size.x!==read.width||size.y!==read.height)this.setSize(read.width,read.height);}else{const current=renderer.getRenderTarget();if(current)this.setSize(current.width,current.height);else{renderer.getDrawingBufferSize(size);target.setSize(size.x,size.y);}}
   const saved={target:renderer.getRenderTarget(),mask:camera.layers.mask,override:ao.scene.overrideMaterial,background:ao.scene.background,shadow:renderer.shadowMap.enabled,autoClear:renderer.autoClear};
   try{camera.layers.disable(30);ao.scene.overrideMaterial=material;ao.scene.background=null;renderer.shadowMap.enabled=false;renderer.autoClear=true;renderer.setRenderTarget(target);renderer.clear();renderer.render(ao.scene,camera);record.valid=true;}
   finally{camera.layers.mask=saved.mask;ao.scene.overrideMaterial=saved.override;ao.scene.background=saved.background;renderer.shadowMap.enabled=saved.shadow;renderer.autoClear=saved.autoClear;renderer.setRenderTarget(saved.target);}
  },
  dispose(){target.dispose();material.dispose();if(camera.userData.fleetPlumeDepth===record)delete camera.userData.fleetPlumeDepth;}
 };
}
