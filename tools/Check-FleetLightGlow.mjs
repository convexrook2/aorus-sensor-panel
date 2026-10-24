// Renderer-control/source regression. Does not create a GPU context or prove a
// native image. The actual pass and pinned Three modules run with a fake renderer.
import fs from 'node:fs';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import * as THREE from '../FleetStudio/vendor/three/build/three.module.js';
const root=new URL('../',import.meta.url),threeURL=new URL('FleetStudio/vendor/three/build/three.module.js',root).href;
function moduleURL(path,replacements={}){
 let source=fs.readFileSync(new URL(path,root),'utf8');
 for(const [from,to] of Object.entries({'three':threeURL,...replacements})){
  source=source.split("from '"+from+"'").join('from '+JSON.stringify(to));
 }
 return 'data:text/javascript;base64,'+Buffer.from(source).toString('base64');
}
const passURL=moduleURL('FleetStudio/vendor/three/examples/jsm/postprocessing/Pass.js');
const copyURL=moduleURL('FleetStudio/vendor/three/examples/jsm/shaders/CopyShader.js');
const highURL=moduleURL('FleetStudio/vendor/three/examples/jsm/shaders/LuminosityHighPassShader.js');
const bloomURL=moduleURL('FleetStudio/vendor/three/examples/jsm/postprocessing/UnrealBloomPass.js',{
 './Pass.js':passURL,'../shaders/CopyShader.js':copyURL,'../shaders/LuminosityHighPassShader.js':highURL});
const {EmissiveGlowPass}=await import(moduleURL('FleetStudio/light-glow.js',{
 './vendor/three/examples/jsm/postprocessing/Pass.js':passURL,
 './vendor/three/examples/jsm/postprocessing/UnrealBloomPass.js':bloomURL}));
const readFile=path=>fs.readFileSync(new URL(path,root));
for(const path of ['light-glow.js','vendor/three/examples/jsm/postprocessing/UnrealBloomPass.js','vendor/three/examples/jsm/shaders/LuminosityHighPassShader.js']){
 assert.ok(readFile('FleetStudio/'+path).equals(readFile('Assets/FleetViewer/'+path)),'Studio/native glow dependency differs: '+path);
}
assert.ok(readFile('FleetStudio/ship-effects.js').equals(readFile('Assets/FleetViewer/rocinante-effects.js')),'Shared light controls differ');
for(const path of ['FleetStudio/review.js','Assets/FleetViewer/viewer.js']){
 const viewer=readFile(path).toString('utf8');
 assert.ok(viewer.includes("import {EmissiveGlowPass} from './light-glow.js'"),'Shared glow import missing: '+path);
 const ao=viewer.indexOf('composer.addPass(ao)'),glow=viewer.indexOf('composer.addPass(lightGlow)'),output=viewer.indexOf('composer.addPass(new OutputPass())');
 assert.ok(ao>=0&&glow>ao&&output>glow,'Glow must follow ordinary AO and precede colour output: '+path);
 assert.ok(viewer.includes('lightGlow.reset()')&&viewer.includes('lightGlow.dispose()'),'Model switch and unload must release glow resources: '+path);
 assert.ok(!viewer.includes('new UnrealBloomPass('),'Ordinary painted/specular surfaces must not feed a second broad bloom: '+path);
}
const nativeManifest=JSON.parse(readFile('Assets/FleetViewer/manifest.json'));
for(const path of ['light-glow.js','viewer.js','rocinante-effects.js','vendor/three/examples/jsm/postprocessing/UnrealBloomPass.js','vendor/three/examples/jsm/shaders/LuminosityHighPassShader.js']){
 const entries=nativeManifest.files.filter(row=>row.path===path),bytes=readFile('Assets/FleetViewer/'+path);
 assert.equal(entries.length,1,'Native manifest must list the current glow dependency once: '+path);
 assert.equal(entries[0].bytes,bytes.length,'Native manifest size stale: '+path);
 assert.equal(entries[0].sha256,crypto.createHash('sha256').update(bytes).digest('hex'),'Native manifest hash stale: '+path);
}

class FakeRenderer{
 constructor(scene){this.scene=scene;this.target={name:'previous target'};this.color=new THREE.Color(.2,.3,.4);this.alpha=.35;
  this.autoClear=true;this.shadowMap={enabled:true};this.xr={enabled:true};this.calls=[];this.fail=null;
  this.state={buffers:{stencil:{setTest:value=>this.stencil=value}}};}
 getRenderTarget(){return this.target;}getActiveCubeFace(){return 2;}getActiveMipmapLevel(){return 1;}
 getClearColor(out){return out.copy(this.color);}getClearAlpha(){return this.alpha;}
 setClearColor(color,alpha){this.color.set(color);if(alpha!==undefined)this.alpha=alpha;}
 setRenderTarget(target,face,mip){this.target=target;this.face=face;this.mip=mip;}
 clear(){this.calls.push({clear:this.target});}
 render(object){
  const call={object,target:this.target};
  if(object===this.scene){call.materials=[];object.traverseVisible(o=>{if(o.isMesh)call.materials.push({object:o,material:o.material});});
   call.background=object.background;call.environment=object.environment;call.fog=object.fog;call.override=object.overrideMaterial;
   call.shadow=this.shadowMap.enabled;call.xr=this.xr.enabled;}
  this.calls.push(call);if(this.fail?.(object))throw Error('Injected renderer failure');
 }
}
const scene=new THREE.Scene(),camera=new THREE.OrthographicCamera(),model=new THREE.Group();scene.add(model);
scene.background=new THREE.Color(0xffffff);scene.environment=new THREE.Texture();scene.fog=new THREE.Fog(0xabcdef,1,10);
scene.overrideMaterial=new THREE.MeshNormalMaterial();
const geometry=new THREE.BoxGeometry(),map=new THREE.Texture(),emissiveMap=new THREE.Texture(),alphaMap=new THREE.Texture();
const weak=new THREE.MeshStandardMaterial({color:0xffffff,emissive:0xff2020,emissiveIntensity:.3,map,emissiveMap,alphaMap,alphaTest:.4});
const bright=new THREE.MeshPhysicalMaterial({emissive:0x88ccff,emissiveIntensity:10});
const strong=new THREE.MeshStandardMaterial({emissive:0x88ccff,emissiveIntensity:5});
const white=new THREE.MeshStandardMaterial({color:0xffffff,metalness:1,roughness:0,map,alphaMap,alphaTest:.4});
const unknown=new THREE.ShaderMaterial({uniforms:{white:{value:new THREE.Color(0xffffff)}}});
const plumeMaterial=new THREE.ShaderMaterial({fragmentShader:'void main(){gl_FragColor=vec4(18.,14.,10.,.4);}',transparent:true,depthWrite:false,toneMapped:false});
plumeMaterial.userData.fleetPlumeEmission=true;const plumeMesh=new THREE.Mesh(geometry,plumeMaterial);model.add(plumeMesh);
let forwarded=0;const hook=function(shader){assert.equal(this,weak);forwarded++;shader.uniforms.sourceMask={value:7};
 shader.fragmentShader=shader.fragmentShader.replace('#include <emissivemap_fragment>','#include <emissivemap_fragment>\ntotalEmissiveRadiance*=0.25;');};
weak.onBeforeCompile=hook;weak.customProgramCacheKey=()=> 'source-position-mask';
const mixed=new THREE.Mesh(geometry,[white,weak]);model.add(mixed,new THREE.Mesh(geometry,bright),new THREE.Mesh(geometry,strong),new THREE.Mesh(geometry,unknown));
const sprite=new THREE.Sprite();model.add(sprite);
const originals=[weak,bright,strong,white,unknown,plumeMaterial],saved=originals.map(m=>({color:m.color?.clone(),emissive:m.emissive?.clone(),strength:m.emissiveIntensity,hook:m.onBeforeCompile}));
let sourceDisposals=0;for(const item of [...originals,map,emissiveMap,alphaMap])item.addEventListener('dispose',()=>sourceDisposals++);
const pass=new EmissiveGlowPass(scene,camera),renderer=new FakeRenderer(scene),read=new THREE.WebGLRenderTarget(16,16),write=read.clone();
const old={target:renderer.target,color:renderer.color.clone(),alpha:renderer.alpha,background:scene.background,environment:scene.environment,fog:scene.fog,override:scene.overrideMaterial};
function assertRestored(){
 assert.equal(mixed.material[0],white);assert.equal(mixed.material[1],weak);assert.equal(sprite.visible,true);
 assert.equal(scene.background,old.background);assert.equal(scene.environment,old.environment);assert.equal(scene.fog,old.fog);assert.equal(scene.overrideMaterial,old.override);
 assert.equal(renderer.target,old.target);assert.deepEqual(renderer.color,old.color);assert.equal(renderer.alpha,old.alpha);
 assert.equal(renderer.autoClear,true);assert.equal(renderer.shadowMap.enabled,true);assert.equal(renderer.xr.enabled,true);assert.equal(renderer.face,2);assert.equal(renderer.mip,1);
 for(const [i,m] of originals.entries()){if(m.color)assert.deepEqual(m.color,saved[i].color);if(m.emissive)assert.deepEqual(m.emissive,saved[i].emissive);
  assert.equal(m.emissiveIntensity,saved[i].strength);assert.equal(m.onBeforeCompile,saved[i].hook);}
}
pass.setSize(640,360);assert.equal(pass._maskTarget.width,640);assert.equal(pass._maskTarget.height,360);
assert.equal(pass._maskTarget.texture.type,THREE.HalfFloatType);assert.equal(pass._maskTarget.depthBuffer,true);
assert.equal(pass._bloom.strength,.18);assert.equal(pass._bloom.radius,.35);assert.equal(pass._bloom.threshold,.02);
assert.equal(pass._compositeMaterial.blending,THREE.NoBlending);assert.equal(pass._compositeMaterial.depthTest,false);
assert.ok(pass._compositeMaterial.fragmentShader.includes('max(original.a,haloAlpha)'),'Transparent outer glow needs nonzero light-derived coverage');
pass.render(renderer,write,read,.016,false);assertRestored();
const maskCall=renderer.calls.find(c=>c.object===scene);assert.equal(maskCall.background,null);assert.equal(maskCall.environment,null);assert.equal(maskCall.fog,null);assert.equal(maskCall.override,null);
assert.equal(maskCall.shadow,false);assert.equal(maskCall.xr,false);
const mixedMasks=maskCall.materials.find(row=>row.object===mixed).material;
assert.ok(Array.isArray(mixedMasks));assert.equal(mixedMasks.length,2);
const weakMask=mixedMasks[1];assert.notEqual(weakMask,weak);assert.equal(weakMask.emissiveMap,emissiveMap);assert.equal(weakMask.map,map);assert.equal(weakMask.alphaMap,alphaMap);assert.equal(weakMask.alphaTest,.4);assert.equal(weakMask.emissiveIntensity,.3);
const shader={uniforms:{},vertexShader:'#include <begin_vertex>',fragmentShader:'#include <emissivemap_fragment>\n#include <opaque_fragment>\n#include <tonemapping_fragment>'};
weakMask.onBeforeCompile(shader,renderer);assert.equal(forwarded,1);assert.equal(shader.uniforms.sourceMask.value,7);
assert.ok(shader.fragmentShader.includes('totalEmissiveRadiance*=0.25;'),'Original spatial emission mask must survive');
assert.ok(shader.fragmentShader.includes('gl_FragColor.rgb=totalEmissiveRadiance;'),'Reflected/specular light must not seed glow');
assert.ok(weakMask.customProgramCacheKey().includes('source-position-mask'));
const hullMask=mixedMasks[0];assert.equal(hullMask.emissiveIntensity,white.emissiveIntensity);assert.equal(hullMask.emissive.getHex(),0);assert.equal(hullMask.depthWrite,true);assert.equal(hullMask.alphaMap,alphaMap);
const unknownMask=maskCall.materials.find(row=>row.object.material===unknown).material;
assert.ok(unknownMask.isMeshBasicMaterial);assert.equal(unknownMask.color.getHex(),0,'Unknown white shader is not an emitter');
assert.equal(maskCall.materials.find(row=>row.object===plumeMesh).material,plumeMaterial,'Pure emission gas must seed HDR glow with its original animated uniforms');
assert.equal(pass._compositeMaterial.uniforms.beauty.value,read.texture);
assert.equal(pass._compositeMaterial.uniforms.glow.value,pass._bloom.renderTargetsHorizontal[0].texture);
assert.notEqual(pass._compositeMaterial.uniforms.glow.value,pass._maskTarget.texture,'Unblurred emission must not be added twice');
weak.emissiveIntensity=0;saved[0].strength=0;renderer.calls=[];pass.render(renderer,write,read,.016,false);assertRestored();
assert.equal(renderer.calls.find(c=>c.object===scene).materials.find(row=>row.object===mixed).material[1].emissiveIntensity,0,'Lights-off must update cached extraction');
weak.emissiveIntensity=.3;saved[0].strength=.3;
for(const stage of ['mask','bloom','composite']){
 renderer.fail=object=>stage==='mask'?object===scene:stage==='bloom'?object.material===pass._bloom.materialHighPassFilter:object.material===pass._compositeMaterial;
 assert.throws(()=>pass.render(renderer,write,read,.016,false),/Injected renderer failure/);assertRestored();
}
renderer.fail=null;
renderer.fail=object=>object===scene;assert.throws(()=>pass.render(renderer,write,read,.016,true),/Injected renderer failure/);
assert.equal(renderer.stencil,true,'An active composer stencil mask must restore even on failure');assertRestored();renderer.fail=null;
let oldMaskDisposed=false;weakMask.addEventListener('dispose',()=>oldMaskDisposed=true);
scene.remove(model);pass.render(renderer,write,read,.016,false);assert.equal(oldMaskDisposed,true,'Removed model clones must not remain cached');
assert.equal(pass._materials.size,0);scene.add(model);pass.render(renderer,write,read,.016,false);
let resetDisposals=0;for(const {material} of pass._materials.values())material.addEventListener('dispose',()=>resetDisposals++);
const cached=pass._materials.size;pass.reset();assert.equal(resetDisposals,cached);assert.equal(pass._materials.size,0);assert.equal(pass._compositeMaterial.uniforms.beauty.value,null);
pass.dispose();assert.equal(sourceDisposals,0,'Source materials and shared maps must never be disposed by glow');
read.dispose();write.dispose();geometry.dispose();for(const m of originals)m.dispose();map.dispose();emissiveMap.dispose();alphaMap.dispose();sprite.material.dispose();scene.environment.dispose();scene.overrideMaterial.dispose();
console.log('PASS: shared selective HDR emission glow; physical colour/map/intensity and source shader masks retained; opaque/cutout occlusion, transparent coverage, exception restoration and old-model cleanup. No GPU/native image tested.');
