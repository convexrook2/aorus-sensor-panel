import * as THREE from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {OrbitControls} from 'three/addons/controls/OrbitControls.js';
import {RoomEnvironment} from 'three/addons/environments/RoomEnvironment.js';
import {EffectComposer} from 'three/addons/postprocessing/EffectComposer.js';
import {RenderPass} from 'three/addons/postprocessing/RenderPass.js';
import {SSAOPass} from 'three/addons/postprocessing/SSAOPass.js';
import {OutputPass} from 'three/addons/postprocessing/OutputPass.js';

const canvas=document.querySelector('canvas'),status=document.querySelector('#status');
const post=message=>window.chrome?.webview?.postMessage(message);
function failure(error){status.hidden=false;status.textContent='Ship unavailable · '+error.message;post({type:'error',message:error.message});console.error(error);}

try {
 const response=await fetch('./catalog.json');if(!response.ok)throw Error('Missing fleet catalogue');
 const catalog=await response.json();
 // Same PBR pipeline as Fleet Studio. Meshes and image data are not resampled.
 const renderer=new THREE.WebGLRenderer({canvas,alpha:true,antialias:true,powerPreference:'high-performance',preserveDrawingBuffer:true});
 renderer.setPixelRatio(devicePixelRatio);renderer.outputColorSpace=THREE.SRGBColorSpace;
 renderer.setClearColor(0x000000,0);renderer.toneMapping=THREE.AgXToneMapping;renderer.toneMappingExposure=1;
 renderer.shadowMap.enabled=true;renderer.shadowMap.type=THREE.PCFShadowMap;renderer.shadowMap.autoUpdate=false;
 const scene=new THREE.Scene(),camera=new THREE.OrthographicCamera(-2,2,2,-2,.01,40);
 const pmrem=new THREE.PMREMGenerator(renderer),room=new RoomEnvironment(),environment=pmrem.fromScene(room,.04);
 scene.environment=environment.texture;scene.environmentIntensity=.22;room.dispose();pmrem.dispose();
 const key=new THREE.DirectionalLight(0xfff7eb,4);key.position.set(4,6,5);key.castShadow=true;
 key.shadow.mapSize.set(2048,2048);Object.assign(key.shadow.camera,{left:-2.7,right:2.7,top:2.7,bottom:-2.7,near:.1,far:18});
 key.shadow.bias=-.00008;key.shadow.normalBias=.004;scene.add(key,key.target);
 const fill=new THREE.DirectionalLight(0xe7efff,.45);fill.position.set(-5,1,3);scene.add(fill);
 const rim=new THREE.DirectionalLight(0xbcd4ed,1.2);rim.position.set(2,-1,-6);scene.add(rim);
 const target=new THREE.WebGLRenderTarget(1,1,{type:THREE.HalfFloatType,samples:4});
 const composer=new EffectComposer(renderer,target),renderPass=new RenderPass(scene,camera),ao=new SSAOPass(scene,camera,1,1,24);
 ao.ssaoMaterial.defines.PERSPECTIVE_CAMERA=0;ao.depthRenderMaterial.defines.PERSPECTIVE_CAMERA=0;
 ao.kernelRadius=.075;ao.minDistance=.0005;ao.maxDistance=.035;
 composer.addPass(renderPass);composer.addPass(ao);composer.addPass(new OutputPass());
 const controls=new OrbitControls(camera,canvas);controls.enableDamping=false;controls.minZoom=.15;controls.maxZoom=16;
 let root=null,entry=null,interactive=false,motion=true,active=true,dirty=true,frame=0,last=0,frames=0,loadToken=0;
 let radius=1,height=3.2,modelSha='',modelStats={},raf=0,seconds=0,pendingId=null,viewWidth=0,viewHeight=0,viewRatio=0;
 const gl=renderer.getContext(),gpuInfo=gl.getExtension('WEBGL_debug_renderer_info');
 const gpu=gpuInfo?gl.getParameter(gpuInfo.UNMASKED_RENDERER_WEBGL):gl.getParameter(gl.RENDERER);
 function invalidate(){dirty=true;schedule();}
 function schedule(){if(!raf&&active&&!document.hidden)raf=requestAnimationFrame(tick);}
 controls.addEventListener('change',invalidate);
 function fit(){
   const aspect=Math.max(1,innerWidth)/Math.max(1,innerHeight);
   const halfHeight=Math.max(height/2,radius/aspect)*1.14;
   camera.left=-halfHeight*aspect;camera.right=halfHeight*aspect;camera.top=halfHeight;camera.bottom=-halfHeight;
   camera.updateProjectionMatrix();
 }
 function resize(){if(viewWidth===innerWidth&&viewHeight===innerHeight&&viewRatio===devicePixelRatio)return;viewWidth=innerWidth;viewHeight=innerHeight;viewRatio=devicePixelRatio;renderer.setPixelRatio(devicePixelRatio);renderer.setSize(innerWidth,innerHeight,false);composer.setPixelRatio(devicePixelRatio);composer.setSize(innerWidth,innerHeight);fit();invalidate();}
 function reset(){
   if(root)root.rotation.y=interactive?0:-.62;
   camera.up.set(0,1,0);camera.position.set(...(interactive?[4,1.4,6]:[0,.05,8]));
   controls.target.set(0,0,0);camera.zoom=1;controls.update();fit();renderer.shadowMap.needsUpdate=true;invalidate();
 }
 function disposeRoot(obj){
   const geometries=new Set(),materials=new Set(),textures=new Set(),images=new Set();
   obj.traverse(o=>{if(o.isMesh){geometries.add(o.geometry);for(const m of Array.isArray(o.material)?o.material:[o.material]){materials.add(m);for(const t of Object.values(m))if(t?.isTexture)textures.add(t);}}});
   for(const g of geometries)g.dispose();for(const m of materials)m.dispose();
   for(const t of textures){for(const image of Array.isArray(t.source?.data)?t.source.data:[t.source?.data])if(image instanceof ImageBitmap)images.add(image);t.dispose();}
   for(const image of images)image.close();
 }
 function stats(obj){
   const materials=new Set(),textures=new Set();let triangles=0,meshes=0;
   obj.traverse(o=>{if(!o.isMesh)return;meshes++;triangles+=(o.geometry.index?.count??o.geometry.attributes.position.count)/3;for(const m of Array.isArray(o.material)?o.material:[o.material])materials.add(m);});
   const maps={colour:0,normal:0,occlusion:0,metalRough:0,emissive:0};
   for(const m of materials){if(m.map)maps.colour++;if(m.normalMap)maps.normal++;if(m.aoMap)maps.occlusion++;if(m.metalnessMap||m.roughnessMap)maps.metalRough++;if(m.emissiveMap)maps.emissive++;for(const t of Object.values(m))if(t?.isTexture)textures.add(t);}
   return {triangles,meshes,materials:materials.size,textures:textures.size,maps};
 }
 async function load(id){
   if(entry?.id===id&&root||pendingId===id)return;
   const selected=catalog.find(row=>row.id===id);if(!selected)throw Error('Unknown ship: '+id);
   const token=++loadToken;status.hidden=false;status.textContent='Loading '+selected.title.split(' / ')[0]+'…';
   pendingId=id;
   try {
   // Release the previous GPU resources before allocating the next full model.
   if(root){scene.remove(root);disposeRoot(root);root=null;}entry=null;invalidate();
   const response=await fetch(selected.model);if(!response.ok)throw Error('Missing model: '+id);
   const raw=await new Response(response.body.pipeThrough(new DecompressionStream('gzip'))).arrayBuffer();
   const digest=Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',raw)),b=>b.toString(16).padStart(2,'0')).join('');
   if(digest!==selected.sha256)throw Error('Model integrity failed: '+id);
   if(token!==loadToken)return;
   const gltf=await new GLTFLoader().parseAsync(raw,'');if(token!==loadToken){disposeRoot(gltf.scene);return;}
   entry=selected;modelSha=digest;modelStats=stats(gltf.scene);
   if(modelStats.triangles!==entry.triangles)throw Error('Model triangle count differs from the reviewed source: '+id);
   for(const [kind,count] of Object.entries(entry.renderMaps))if(modelStats.maps[kind]!==count)throw Error('Missing '+kind+' texture maps: '+id+' ('+modelStats.maps[kind]+'/'+count+')');
   const oriented=new THREE.Group();oriented.add(gltf.scene);gltf.scene.updateMatrixWorld(true);
   const bounds=new THREE.Box3().setFromObject(gltf.scene),size=bounds.getSize(new THREE.Vector3()),center=bounds.getCenter(new THREE.Vector3());
   const max=Math.max(size.x,size.y,size.z);if(!(max>0&&Number.isFinite(max)))throw Error('Invalid ship bounds');
   gltf.scene.position.sub(center);oriented.scale.setScalar(3.2/max);
   const axis=size.x>=size.y&&size.x>=size.z?new THREE.Vector3(1,0,0):size.z>size.y?new THREE.Vector3(0,0,1):new THREE.Vector3(0,1,0);
   oriented.quaternion.setFromUnitVectors(axis,new THREE.Vector3(0,entry.flipLongitudinal?-1:1,0));
   oriented.traverse(o=>{
     // Updated catalogue entries use the same source-light calibration as Fleet Studio.
     if((o.isPointLight||o.isSpotLight)&&entry.lightUnitScale){o.intensity*=entry.lightUnitScale*(3.2/max)**2;if(o.distance)o.distance*=3.2/max;}
     if(!o.isMesh)return;o.castShadow=true;o.receiveShadow=true;for(const m of Array.isArray(o.material)?o.material:[o.material])for(const t of Object.values(m))if(t?.isTexture)t.anisotropy=renderer.capabilities.getMaxAnisotropy();
   });
   root=new THREE.Group();root.add(oriented);root.updateMatrixWorld(true);
   const visible=new THREE.Box3().setFromObject(root),extent=visible.getSize(new THREE.Vector3());
   radius=Math.hypot(extent.x,extent.z)/2;height=extent.y;scene.add(root);reset();
   status.hidden=true;renderer.shadowMap.needsUpdate=true;invalidate();
   // Signal only after an actual full-quality frame has reached the canvas.
   render();post({type:'model-ready',...diagnose()});
   } finally {if(token===loadToken)pendingId=null;}
 }
 function render(){
   ao.ssaoMaterial.uniforms.cameraProjectionMatrix.value.copy(camera.projectionMatrix);
   ao.ssaoMaterial.uniforms.cameraInverseProjectionMatrix.value.copy(camera.projectionMatrixInverse);
   composer.render();dirty=false;frames++;
 }
 function tick(t){
   raf=0;const dt=last?Math.min((t-last)/1000,.1):0;last=t;
   if(!active||document.hidden)return;
   if(root&&motion&&!interactive){root.rotation.y+=dt*Math.PI*2/30;seconds+=dt;renderer.shadowMap.needsUpdate=true;dirty=true;}
   if(dirty)render();frame++;
   if(motion&&!interactive||dirty)schedule();
 }
 function diagnose(){return {id:entry?.id??null,title:entry?.title??null,sourceSha256:modelSha,expectedSha256:entry?.sha256,
   ...modelStats,expectedTriangles:entry?.triangles,expectedMaps:entry?.renderMaps,gpu,webgl2:gl instanceof WebGL2RenderingContext,
   shadows:renderer.shadowMap.enabled,ambientOcclusion:ao.enabled,interactive,motion,active,
   transparent:true,pixelRatio:renderer.getPixelRatio(),width:canvas.width,height:canvas.height,
   frames,animationSeconds:seconds,modelReady:!!root,renderPipeline:'Fleet Studio / Three.js PBR',
   toneMapping:'AgX',geometryReduced:false,texturesResampled:false};}
 function receive(message){
   if(message.type==='scene'){
     const changed=interactive!==message.interactive;interactive=!!message.interactive;controls.enabled=interactive;
     if(changed)reset();load(message.id).catch(failure);
   } else if(message.type==='activity'){
     motion=!!message.motion;active=!!message.active;last=0;
     if(!active&&raf){cancelAnimationFrame(raf);raf=0;}else invalidate();
   } else if(message.type==='resize')resize();
   else if(message.type==='reset')reset();
   else if(message.type==='orbit'&&interactive){
     const offset=camera.position.clone().sub(controls.target),spherical=new THREE.Spherical().setFromVector3(offset);
     spherical.theta+=message.x;spherical.phi=THREE.MathUtils.clamp(spherical.phi+message.y,.08,Math.PI-.08);
     camera.position.copy(controls.target).add(new THREE.Vector3().setFromSpherical(spherical));controls.update();invalidate();
   } else if(message.type==='zoom'&&interactive){camera.zoom=THREE.MathUtils.clamp(camera.zoom*message.factor,.15,16);camera.updateProjectionMatrix();invalidate();}
 }
 window.chrome?.webview?.addEventListener('message',event=>receive(event.data));
 window.x890Fleet={diagnose};
 addEventListener('resize',resize);document.addEventListener('visibilitychange',()=>{last=0;if(document.hidden&&raf){cancelAnimationFrame(raf);raf=0;}else if(!document.hidden)invalidate();});
 canvas.addEventListener('webglcontextlost',event=>{event.preventDefault();failure(Error('Graphics context was lost. Reopen the panel to restore it.'));});
 addEventListener('keydown',event=>{
   if(!interactive)return;
   const action={ArrowLeft:{type:'orbit',x:.14,y:0},ArrowRight:{type:'orbit',x:-.14,y:0},ArrowUp:{type:'orbit',x:0,y:-.09},ArrowDown:{type:'orbit',x:0,y:.09},Home:{type:'reset'},'+':{type:'zoom',factor:1.12},'-':{type:'zoom',factor:1/1.12}}[event.key];
   if(event.key==='Escape'){post({type:'close'});event.preventDefault();}else if(action){receive(action);event.preventDefault();}
 });
 resize();controls.enabled=false;post({type:'engine-ready'});
 // Also runs as a normal offline browser preview, using the same packaged files.
 if(!window.chrome?.webview)await load(new URL(location.href).searchParams.get('ship')||'Pella');
} catch(error){failure(error);}
