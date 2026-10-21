import * as THREE from 'three';
import {Pass,FullScreenQuad} from './vendor/three/examples/jsm/postprocessing/Pass.js';
import {UnrealBloomPass} from './vendor/three/examples/jsm/postprocessing/UnrealBloomPass.js';

// Blur only the model's existing physical-material emission. Ordinary white
// paint, reflections, light objects and unknown effect shaders cannot seed it.
// This pass precedes OutputPass, so both extraction and composition stay HDR.
export class EmissiveGlowPass extends Pass{
 constructor(scene,camera){
  super();this.scene=scene;this.camera=camera;this._materials=new Map();
  this._maskTarget=new THREE.WebGLRenderTarget(1,1,{type:THREE.HalfFloatType,depthBuffer:true,stencilBuffer:false});
  this._maskTarget.texture.name='Fleet existing-emission mask';
  this._maskTarget.texture.colorSpace=THREE.LinearSRGBColorSpace;
  this._maskTarget.texture.generateMipmaps=false;
  this._bloom=new UnrealBloomPass(new THREE.Vector2(1,1),.18,.35,.02);
  this._compositeMaterial=new THREE.ShaderMaterial({
   name:'Fleet existing-emission glow composite',
   uniforms:{beauty:{value:null},glow:{value:null}},
   vertexShader:'varying vec2 vUv;void main(){vUv=uv;gl_Position=vec4(position.xy,0.0,1.0);}',
   fragmentShader:`uniform sampler2D beauty;uniform sampler2D glow;varying vec2 vUv;
    void main(){
     vec4 original=texture2D(beauty,vUv);vec3 halo=texture2D(glow,vUv).rgb;
     // Bloom's internal alpha is always one. Derive coverage from its light,
     // preserving transparent native backgrounds while retaining outer glow.
     float haloAlpha=clamp(max(halo.r,max(halo.g,halo.b)),0.0,1.0);
     gl_FragColor=vec4(original.rgb+halo,max(original.a,haloAlpha));
    }`,
   depthTest:false,depthWrite:false,blending:THREE.NoBlending,toneMapped:false
  });
  this._quad=new FullScreenQuad(this._compositeMaterial);
 }

 _maskMaterial(source,used){
  used.add(source);
  let record=this._materials.get(source);
  if(record&&(record.version!==source.version||record.hook!==source.onBeforeCompile||record.map!==source.emissiveMap)){
   record.material.dispose();this._materials.delete(source);record=null;
  }
  if(!record){
   const physical=Boolean(source.isMeshStandardMaterial||source.isMeshPhysicalMaterial);
   const material=physical?source.clone():new THREE.MeshBasicMaterial({color:0x000000});
   material.name='Emission mask / '+source.name;
   if(physical){
    const sourceHook=source.onBeforeCompile;
    material.onBeforeCompile=(shader,renderer)=>{
     // In particular, retain Anubis's source-coordinate attenuation. The
     // source hook is not copied by Material.clone() in this Three version.
     sourceHook.call(source,shader,renderer);
     const output='#include <tonemapping_fragment>';
     if(!shader.fragmentShader.includes(output))throw Error('Unsupported physical emission shader output');
     shader.fragmentShader=shader.fragmentShader.replace(output,'gl_FragColor.rgb=totalEmissiveRadiance;\n'+output);
    };
    material.customProgramCacheKey=()=> 'fleet-emission-only-v1/'+source.customProgramCacheKey.call(source);
   }
   record={material,physical,version:source.version,hook:source.onBeforeCompile,map:source.emissiveMap};
   this._materials.set(source,record);
  }
  const mask=record.material;
  // Controls change intensity without bumping Material.version. Refresh the
  // values each frame; never recalibrate a source colour or strength.
  if(record.physical){mask.emissive.copy(source.emissive);mask.emissiveIntensity=source.emissiveIntensity;mask.emissiveMap=source.emissiveMap;}
  else{mask.map=source.map||null;mask.alphaMap=source.alphaMap||null;mask.color.set(0x000000);mask.vertexColors=Boolean(source.vertexColors);}
  for(const key of ['visible','side','opacity','transparent','alphaTest','alphaHash','depthTest','depthWrite','colorWrite',
   'alphaToCoverage','premultipliedAlpha','blending','blendSrc','blendDst','blendEquation','blendSrcAlpha','blendDstAlpha','blendEquationAlpha',
   'polygonOffset','polygonOffsetFactor','polygonOffsetUnits','clippingPlanes','clipIntersection','clipShadows']){
   if(source[key]!==undefined)mask[key]=source[key];
  }
  mask.fog=false;mask.toneMapped=false;
  return mask;
 }

 setSize(width,height){
  this._maskTarget.setSize(width,height);this._bloom.setSize(width,height);
 }

 reset(){
  // Called before disposing the previous model. Clones share source maps;
  // disposing a material releases its program, never those source textures.
  for(const {material} of this._materials.values())material.dispose();
  this._materials.clear();
  this._compositeMaterial.uniforms.beauty.value=null;
  this._compositeMaterial.uniforms.glow.value=null;
 }

 render(renderer,writeBuffer,readBuffer,deltaTime,maskActive){
  const scene=this.scene,swapped=[],hidden=[],used=new Set();
  const sceneState={background:scene.background,environment:scene.environment,fog:scene.fog,overrideMaterial:scene.overrideMaterial};
  const rendererState={target:renderer.getRenderTarget(),face:renderer.getActiveCubeFace?.()??0,mip:renderer.getActiveMipmapLevel?.()??0,
   color:renderer.getClearColor(new THREE.Color()),alpha:renderer.getClearAlpha(),autoClear:renderer.autoClear,
   shadow:renderer.shadowMap.enabled,xr:renderer.xr?.enabled};
  const restoreScene=()=>{
   for(const [object,material] of swapped)object.material=material;
   for(const object of hidden)object.visible=true;
   swapped.length=hidden.length=0;Object.assign(scene,sceneState);
  };
  try{
   scene.background=null;scene.environment=null;scene.fog=null;scene.overrideMaterial=null;
   scene.traverseVisible(object=>{
    if(object.isMesh){
     const original=object.material;swapped.push([object,original]);
     object.material=Array.isArray(original)?original.map(material=>this._maskMaterial(material,used)):this._maskMaterial(original,used);
    }else if(object.isSprite||object.isPoints||object.isLine){object.visible=false;hidden.push(object);}
   });
   // Also prune on ordinary model replacement, even if a caller forgot reset.
   for(const [source,record] of this._materials)if(!used.has(source)){record.material.dispose();this._materials.delete(source);}
   renderer.autoClear=false;renderer.shadowMap.enabled=false;if(renderer.xr)renderer.xr.enabled=false;
   if(maskActive)renderer.state.buffers.stencil.setTest(false);
   renderer.setClearColor(0x000000,0);renderer.setRenderTarget(this._maskTarget);renderer.clear();
   renderer.render(scene,this.camera);
   restoreScene();
   // Pinned vendored UnrealBloomPass writes the combined blurred mips here.
   // Its additive input buffer also contains the unblurred mask: do not use it.
   this._bloom.render(renderer,null,this._maskTarget,deltaTime,maskActive);
   this._compositeMaterial.uniforms.beauty.value=readBuffer.texture;
   this._compositeMaterial.uniforms.glow.value=this._bloom.renderTargetsHorizontal[0].texture;
   renderer.setRenderTarget(this.renderToScreen?null:writeBuffer);
   if(this.clear)renderer.clear();
   this._quad.render(renderer);
  }finally{
   restoreScene();renderer.autoClear=rendererState.autoClear;renderer.shadowMap.enabled=rendererState.shadow;
   if(renderer.xr)renderer.xr.enabled=rendererState.xr;
   renderer.setClearColor(rendererState.color,rendererState.alpha);
   renderer.setRenderTarget(rendererState.target,rendererState.face,rendererState.mip);
   if(maskActive)renderer.state.buffers.stencil.setTest(true);
  }
 }

 dispose(){
  this.reset();this._maskTarget.dispose();this._bloom.dispose();
  // The pinned vendor's dispose omits this extraction material.
  this._bloom.materialHighPassFilter.dispose();
  this._compositeMaterial.dispose();this._quad.dispose();
 }
}
