import * as THREE from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {OrbitControls} from 'three/addons/controls/OrbitControls.js';
import {RoomEnvironment} from 'three/addons/environments/RoomEnvironment.js';
import {EffectComposer} from 'three/addons/postprocessing/EffectComposer.js';
import {RenderPass} from 'three/addons/postprocessing/RenderPass.js';
import {SSAOPass} from 'three/addons/postprocessing/SSAOPass.js';
import {OutputPass} from 'three/addons/postprocessing/OutputPass.js';
import {makeShipEffects,configurePlumeRendering,ownShipShadows} from './rocinante-effects.js';
import {EmissiveGlowPass} from './light-glow.js';
import {makePortal} from './portal.js';
import {showcaseCameraAt} from './showcase-motion.js';
import {makeHolographicStage} from './holographic-stage.js';
import {makeShipInspection} from './ship-inspection.js';

const canvas=document.querySelector('canvas'),status=document.querySelector('#status');
const post=message=>window.chrome?.webview?.postMessage(message);
function failure(error){status.hidden=false;status.textContent='Ship unavailable · '+error.message;post({type:'error',message:error.message});console.error(error);}

try {
 const previewFetch=window.chrome?.webview?{}:{cache:'no-store'};
 const response=await fetch('./catalog.json',previewFetch);if(!response.ok)throw Error('Missing fleet catalogue');
 const catalog=await response.json();
 const engines=await (await fetch('./engines.json',previewFetch)).json();
 // Same PBR pipeline as Fleet Studio. Meshes and image data are not resampled.
 const renderer=new THREE.WebGLRenderer({canvas,alpha:true,antialias:true,powerPreference:'high-performance',preserveDrawingBuffer:true});
 renderer.setPixelRatio(devicePixelRatio);renderer.outputColorSpace=THREE.SRGBColorSpace;
 renderer.setClearColor(0x000000,0);renderer.toneMapping=THREE.AgXToneMapping;renderer.toneMappingExposure=1;
 renderer.shadowMap.enabled=true;renderer.shadowMap.type=THREE.PCFSoftShadowMap;renderer.shadowMap.autoUpdate=false;
 renderer.info.autoReset=false;
 const scene=new THREE.Scene(),camera=new THREE.OrthographicCamera(-2,2,2,-2,.01,40);
 const pmrem=new THREE.PMREMGenerator(renderer),room=new RoomEnvironment(),environment=pmrem.fromScene(room,.04);
 scene.environment=environment.texture;scene.environmentIntensity=.22;room.dispose();pmrem.dispose();
 // One fixed camera-left key reveals relief as the model turns. The low-level
 // environment remains for PBR reflections; it does not cast a second shadow.
 const key=new THREE.SpotLight(0xfff4e6,620,0,.5,.45,2);
 key.position.set(-4,5.5,6);key.target.position.set(0,0,0);key.castShadow=true;
 key.shadow.mapSize.set(2048,2048);key.shadow.camera.near=.5;key.shadow.camera.far=18;
 key.shadow.bias=-.00008;key.shadow.normalBias=.004;scene.add(key,key.target);
 const target=new THREE.WebGLRenderTarget(1,1,{type:THREE.HalfFloatType,samples:4});
 const composer=new EffectComposer(renderer,target),renderPass=new RenderPass(scene,camera),ao=new SSAOPass(scene,camera,1,1,24);
 ao.ssaoMaterial.defines.PERSPECTIVE_CAMERA=0;ao.depthRenderMaterial.defines.PERSPECTIVE_CAMERA=0;
 ao.kernelRadius=.075;ao.minDistance=.0005;ao.maxDistance=.035;
 const plumeDepth=configurePlumeRendering(camera,ao);
 const lightGlow=new EmissiveGlowPass(scene,camera);
 composer.addPass(plumeDepth);composer.addPass(renderPass);composer.addPass(ao);composer.addPass(lightGlow);composer.addPass(new OutputPass());
 const holographicStage=makeHolographicStage(),displayStage=holographicStage.group;
 displayStage.visible=false;scene.add(displayStage);
 addEventListener('pagehide',()=>{lightGlow.dispose();plumeDepth.dispose();holographicStage.dispose();},{once:true});
 const inspection=makeShipInspection(camera),controls=new OrbitControls(inspection.camera,canvas);controls.enableDamping=false;controls.minZoom=.15;controls.maxZoom=16;
 // Inspection rotates and zooms about the ship, never translates it off-screen.
 controls.enablePan=false;controls.zoomToCursor=false;
 let shipEffects=null,shipShadows=null;
 const motionFrameInterval=1000/30;let efficient=false;
 let root=null,entry=null,interactive=false,holotable=false,motion=true,active=true,dirty=true,frame=0,last=0,nextMotionFrame=0,frames=0,loadToken=0;
 let radius=1,stageRadius=1,height=3.2,modelSha='',modelStats={},raf=0,seconds=0,showcaseSeconds=0,stageSeconds=0,hologramMotion=true,pendingId=null,viewWidth=0,viewHeight=0,viewRatio=0;
 let lastFrameStats={},profile=null,portal=null,aperture=null,gateNormal=null;
 const gl=renderer.getContext(),gpuInfo=gl.getExtension('WEBGL_debug_renderer_info');
 const timerQuery=gl.getExtension('EXT_disjoint_timer_query_webgl2');
 const gpu=gpuInfo?gl.getParameter(gpuInfo.UNMASKED_RENDERER_WEBGL):gl.getParameter(gl.RENDERER);
 function invalidate(){dirty=true;schedule();}
 function cancelScheduled(){if(raf)cancelAnimationFrame(raf);raf=0;}
 function schedule(){if(!raf&&active&&!document.hidden)raf=requestAnimationFrame(tick);}
 controls.addEventListener('change',()=>{if(interactive&&root){inspection.apply(root);renderer.shadowMap.needsUpdate=true;}invalidate();});
 function fit(){
   const aspect=Math.max(1,innerWidth)/Math.max(1,innerHeight);
   let halfHeight=Math.max(height/2+(holotable ? .25 : 0),(holotable?stageRadius:radius)/aspect)*1.14;
   if(holotable&&displayStage.visible)halfHeight=Math.max(halfHeight,holographicStage.requiredHalfHeight(camera,aspect));
   camera.left=-halfHeight*aspect;camera.right=halfHeight*aspect;camera.top=halfHeight;camera.bottom=-halfHeight;
   camera.updateProjectionMatrix();inspection.syncProjection();
 }
 function resize(){if(viewWidth===innerWidth&&viewHeight===innerHeight&&viewRatio===devicePixelRatio)return;viewWidth=innerWidth;viewHeight=innerHeight;viewRatio=devicePixelRatio;renderer.setPixelRatio(devicePixelRatio);renderer.setSize(innerWidth,innerHeight,false);composer.setPixelRatio(devicePixelRatio);composer.setSize(innerWidth,innerHeight);fit();invalidate();}
 function reset(){
   showcaseSeconds=0;stageSeconds=0;
   if(root){root.rotation.set(0,interactive?0:-.20,0);root.scale.setScalar(1);}
   displayStage.visible=holotable&&!!root;
   displayStage.scale.set(stageRadius,1,stageRadius);
   displayStage.position.y=-height/2-.08;
   camera.up.set(0,1,0);camera.position.set(...(holotable||interactive?[4,1.7,6]:[3.8,3.2,6]));
   camera.lookAt(0,0,0);camera.zoom=1;fit();inspection.sync();controls.target.set(0,0,0);controls.update();renderer.shadowMap.needsUpdate=true;invalidate();
 }
 function disposeRoot(obj){
   const geometries=new Set(),materials=new Set(),textures=new Set(),images=new Set();
   obj.traverse(o=>{if(o.isMesh){geometries.add(o.geometry);for(const m of Array.isArray(o.material)?o.material:[o.material]){materials.add(m);for(const t of Object.values(m))if(t?.isTexture)textures.add(t);}}});
   for(const g of geometries)g.dispose();for(const m of materials)m.dispose();
   for(const t of textures){for(const image of Array.isArray(t.source?.data)?t.source.data:[t.source?.data])if(image instanceof ImageBitmap)images.add(image);t.dispose();}
   for(const image of images)image.close();
 }
 function stats(obj){
   const materials=new Set(),textures=new Set();let triangles=0,meshes=0,lights=0;
   obj.traverse(o=>{if(o.isLight)lights++;if(!o.isMesh)return;meshes++;triangles+=(o.geometry.index?.count??o.geometry.attributes.position.count)/3;for(const m of Array.isArray(o.material)?o.material:[o.material])materials.add(m);});
   const maps={colour:0,normal:0,occlusion:0,metalRough:0,emissive:0};
   for(const m of materials){if(m.map)maps.colour++;if(m.normalMap)maps.normal++;if(m.aoMap)maps.occlusion++;if(m.metalnessMap||m.roughnessMap)maps.metalRough++;if(m.emissiveMap)maps.emissive++;for(const t of Object.values(m))if(t?.isTexture)textures.add(t);}
   return {triangles,meshes,lights,materials:materials.size,textures:textures.size,maps};
 }
 async function load(id){
   if(entry?.id===id&&root||pendingId===id)return;
   const selected=catalog.find(row=>row.id===id);if(!selected)throw Error('Unknown ship: '+id);
   const token=++loadToken;status.hidden=false;status.textContent='Loading '+selected.title.split(' / ')[0]+'…';
   pendingId=id;
   try {
   // Release the previous GPU resources before allocating the next full model.
   lightGlow.reset();shipEffects?.dispose();shipEffects=null;shipShadows?.dispose();shipShadows=null;
   if(root){scene.remove(root);disposeRoot(root);root=null;}displayStage.visible=false;entry=null;portal=null;aperture=null;gateNormal=null;invalidate();
   const response=await fetch(selected.model,previewFetch);if(!response.ok)throw Error('Missing model: '+id);
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
   // Preserve source orientation for the XY gate and Y-up survey disc.
   if(!['gate','survey-disc'].includes(entry.orientation))oriented.quaternion.setFromUnitVectors(axis,new THREE.Vector3(0,entry.flipLongitudinal?-1:1,0));
   if(entry.orientation==='gate')gateNormal=new THREE.Vector3(0,0,1).applyQuaternion(oriented.quaternion).toArray();
   oriented.traverse(o=>{
     // Updated catalogue entries use the same source-light calibration as Fleet Studio.
     if((o.isPointLight||o.isSpotLight)&&entry.lightUnitScale){o.intensity*=entry.lightUnitScale*(3.2/max)**2;if(o.distance)o.distance*=3.2/max;}
     if(o.isSpotLight&&entry.localLightShadows){o.castShadow=true;o.shadow.mapSize.set(512,512);o.shadow.camera.near=.003;o.shadow.camera.far=6;o.shadow.bias=-.00002;o.shadow.normalBias=.0005;}
     if(!o.isMesh)return;o.castShadow=true;o.receiveShadow=true;for(const m of Array.isArray(o.material)?o.material:[o.material])for(const t of Object.values(m))if(t?.isTexture)t.anisotropy=renderer.capabilities.getMaxAnisotropy();
     if(entry.orientation==='gate'&&/^Active[ _]aperture/.test(o.name)){
       if(aperture)throw Error('Multiple gate apertures');
       aperture=o;portal=makePortal();
       for(const material of Array.isArray(o.material)?o.material:[o.material])material.dispose();
       o.material=portal;o.castShadow=false;o.receiveShadow=false;
     }
   });
   if(entry.orientation==='gate'&&!aperture)throw Error('The separate gate aperture was not found');
   shipShadows=ownShipShadows(gltf.scene);
   // Reviewed ships opt into their own registered exhaust. Source model
   // statistics are captured above, before separate effect meshes are added.
   if(['Rocinante','Anubis','Arboghast','Canterbury','Knight','Pella','Scirocco'].includes(entry.id)){
     const drive=engines[entry.id==='Rocinante'?'RocinanteS6':entry.id];
     if(['Arboghast','Canterbury','Knight','Pella','Scirocco'].includes(entry.id)&&drive?.modelSha256!==modelSha)throw Error('Drive registration differs from '+entry.id+' model');
     shipEffects=makeShipEffects(oriented,gltf.scene,drive.engines,center,3.2/max);
     shipEffects.setLights(true);shipEffects.setPlumes(true);
   }
   // Centre after the source's orientation/scale too, including non-identity
   // scene transforms. Both portrait rotation and inspection orbit share this pivot.
   oriented.updateMatrixWorld(true);
   oriented.position.sub(new THREE.Box3().setFromObject(oriented).getCenter(new THREE.Vector3()));
   root=new THREE.Group();root.add(oriented);root.updateMatrixWorld(true);
   const visible=new THREE.Box3().setFromObject(root),extent=visible.getSize(new THREE.Vector3());
   radius=Math.hypot(extent.x,extent.z)/2;stageRadius=Math.max(.8,Math.max(extent.x,extent.z)*.6);height=extent.y;scene.add(root);reset();
   status.hidden=true;renderer.shadowMap.needsUpdate=true;invalidate();
   // Signal only after an actual full-quality frame has reached the canvas.
   render();post({type:'model-ready',...diagnose()});
   } finally {if(token===loadToken)pendingId=null;}
 }
 function render(){
   holographicStage.tick(stageSeconds);
   shipEffects?.tick(seconds);
   if(portal)portal.uniforms.time.value=seconds;
   ao.ssaoMaterial.uniforms.cameraProjectionMatrix.value.copy(camera.projectionMatrix);
   ao.ssaoMaterial.uniforms.cameraInverseProjectionMatrix.value.copy(camera.projectionMatrixInverse);
   // Diagnostic timer queries exist only during an explicitly requested review.
   if(profile?.state==='collecting'&&timerQuery){
     const disjoint=gl.getParameter(timerQuery.GPU_DISJOINT_EXT);
     for(let i=profile.pending.length-1;i>=0;i--){
       const q=profile.pending[i];
       if(disjoint||gl.getQueryParameter(q,gl.QUERY_RESULT_AVAILABLE)){
         if(!disjoint)profile.gpu.push(gl.getQueryParameter(q,gl.QUERY_RESULT)/1e6);
         gl.deleteQuery(q);profile.pending.splice(i,1);
       }
     }
   }
   const sampling=profile?.state==='collecting'&&profile.cpu.length<profile.requested;
   const query=sampling&&timerQuery?gl.createQuery():null;
   if(query)gl.beginQuery(timerQuery.TIME_ELAPSED_EXT,query);
   renderer.info.reset();const started=performance.now();composer.render();
   if(query){gl.endQuery(timerQuery.TIME_ELAPSED_EXT);profile.pending.push(query);}
   if(sampling)profile.cpu.push(performance.now()-started);
   if(profile?.state==='collecting'&&profile.cpu.length>=profile.requested&&!profile.pending.length)profile.state='complete';
   lastFrameStats={drawCalls:renderer.info.render.calls,submittedTriangles:renderer.info.render.triangles};
   dirty=false;frames++;
 }
 function tick(t){
   raf=0;const dt=last?Math.min((t-last)/1000,.1):0;last=t;
   if(!active||document.hidden)return;
   const hologramActive=holotable&&displayStage.visible&&hologramMotion&&!!root;
   if(root&&(motion||hologramActive)){
     if(motion){root.rotation.y+=dt*Math.PI*2/30;seconds+=dt;if(!interactive&&!holotable)showcaseSeconds+=dt;}
     if(hologramActive)stageSeconds+=dt;
     if(!efficient||t>=nextMotionFrame){
       // Keep elapsed-time rotation on rAF, but draw motion at 30 Hz. Carry the
       // remainder so high-refresh displays do not round every interval down.
       // Explicit control/resize invalidations still draw on the next frame.
       if(efficient)nextMotionFrame=t+motionFrameInterval-(nextMotionFrame?(t-nextMotionFrame)%motionFrameInterval:0);
       if(!interactive&&!holotable){camera.position.set(...showcaseCameraAt(showcaseSeconds));camera.lookAt(0,0,0);}
       if(motion)renderer.shadowMap.needsUpdate=true;dirty=true;
     }
   }
   if(dirty)render();frame++;
   if(motion||hologramActive||dirty)schedule();
 }
 function diagnose(){
   camera.updateMatrixWorld(true);
   const centre=new THREE.Vector3().project(camera);
   const boundsCentre=root?new THREE.Box3().setFromObject(root).getCenter(new THREE.Vector3()).project(camera):centre;
   return {id:entry?.id??null,title:entry?.title??null,sourceSha256:modelSha,expectedSha256:entry?.sha256,
   ...modelStats,expectedTriangles:entry?.triangles,expectedMaps:entry?.renderMaps,gpu,webgl2:gl instanceof WebGL2RenderingContext,
   shadows:renderer.shadowMap.enabled,ambientOcclusion:ao.enabled,interactive,holotable,motion,active,
   displayStage:displayStage.visible,hologram:{...holographicStage.diagnose(camera),visible:displayStage.visible,animated:hologramMotion},inspection:{quaternion:root?.quaternion.toArray()??null,zoom:root?.scale.x??1},rotationRadians:root?.rotation.y??0,showcaseMotion:{enabled:!interactive&&!holotable,elapsed:showcaseSeconds},frameLimit:efficient?30:null,graphicsMode:efficient?'Efficient':'FullFidelity',
   orientation:entry?.orientation??'ship',gateNormal,
   aperture:aperture?{visible:aperture.visible,time:portal.uniforms.time.value,doubleSided:portal.side===THREE.DoubleSide,name:aperture.name}:null,
   transparent:true,pixelRatio:renderer.getPixelRatio(),width:canvas.width,height:canvas.height,
   frames,animationSeconds:seconds,modelReady:!!root,renderPipeline:'Fleet Studio / Three.js PBR',
   engineEffects:shipEffects?.stats()??null,toneMapping:'AgX',geometryReduced:false,texturesResampled:false,lastFrameStats,
   renderProfile:profile?{state:profile.state,samples:profile.cpu.length,gpuSamples:profile.gpu.length,gpuTimerSupported:!!timerQuery,
     gpuMilliseconds:profile.gpu.length?profile.gpu.reduce((a,b)=>a+b,0)/profile.gpu.length:null,
     cpuSubmissionMilliseconds:profile.cpu.length?profile.cpu.reduce((a,b)=>a+b,0)/profile.cpu.length:null}:null,
   framing:{centreNdc:{x:centre.x,y:centre.y},boundsCentreNdc:{x:boundsCentre.x,y:boundsCentre.y},cameraPosition:camera.position.toArray(),cameraQuaternion:camera.quaternion.toArray(),target:controls.target.toArray(),panEnabled:controls.enablePan,zoom:camera.zoom,
     cssWidth:innerWidth,cssHeight:innerHeight,projectionAspect:(camera.right-camera.left)/(camera.top-camera.bottom)}};}
 function receive(message){
   if(message.type==='scene'){
     const nextEfficient=message.graphicsMode==='Efficient';if(nextEfficient!==efficient){efficient=nextEfficient;nextMotionFrame=0;invalidate();}
     const changed=interactive!==!!message.interactive||holotable!==!!message.holotable;
     holotable=!!message.holotable;interactive=!!message.interactive;controls.enabled=interactive;
     // Prepared portraits bake the glow against their dark display background.
     // This avoids browser premultiplied-alpha artefacts in PNG captures.
     scene.background=holotable?null:new THREE.Color(0x040c13);
     if(message.stageColour)holographicStage.setColour(message.stageColour);
     if(changed)reset();load(message.id).catch(failure);
   } else if(message.type==='activity'){
     motion=!!message.motion;active=!!message.active;hologramMotion=!!message.hologram;last=0;nextMotionFrame=0;
     if(!active)cancelScheduled();else invalidate();
   } else if(message.type==='resize')resize();
   else if(message.type==='inspection-stage'){
     // Explicit diagnostic capture only; normal scene/reset restores the light platform.
     displayStage.visible=holotable&&!!message.visible;renderer.shadowMap.needsUpdate=true;invalidate();
   }
   else if(message.type==='reset')reset();
   else if(message.type==='orbit'&&interactive){
     const offset=inspection.camera.position.clone().sub(controls.target),spherical=new THREE.Spherical().setFromVector3(offset);
     spherical.theta+=message.x;spherical.phi=THREE.MathUtils.clamp(spherical.phi+message.y,.08,Math.PI-.08);
     inspection.camera.position.copy(controls.target).add(new THREE.Vector3().setFromSpherical(spherical));controls.update();invalidate();
   } else if(message.type==='zoom'&&interactive){inspection.camera.zoom=THREE.MathUtils.clamp(inspection.camera.zoom*message.factor,.15,16);inspection.camera.updateProjectionMatrix();inspection.apply(root);invalidate();}
 }
 window.chrome?.webview?.addEventListener('message',event=>receive(event.data));
 window.x890Fleet={diagnose,profile:(requested=30)=>{
   if(profile?.state==='collecting')return;
   profile={state:'collecting',requested:Math.min(120,Math.max(1,requested)),cpu:[],gpu:[],pending:[]};invalidate();
 }};
 addEventListener('resize',resize);document.addEventListener('visibilitychange',()=>{last=0;nextMotionFrame=0;if(document.hidden)cancelScheduled();else invalidate();});
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
