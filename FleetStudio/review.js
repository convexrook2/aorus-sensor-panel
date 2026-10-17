import * as THREE from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {OrbitControls} from 'three/addons/controls/OrbitControls.js';
import {RoomEnvironment} from 'three/addons/environments/RoomEnvironment.js';
import {EffectComposer} from 'three/addons/postprocessing/EffectComposer.js';
import {RenderPass} from 'three/addons/postprocessing/RenderPass.js';
import {SSAOPass} from 'three/addons/postprocessing/SSAOPass.js';
import {UnrealBloomPass} from './vendor/three/examples/jsm/postprocessing/UnrealBloomPass.js';
import {OutputPass} from 'three/addons/postprocessing/OutputPass.js';
import {makePortal} from './portal.js';
import {makeShipEffects} from './ship-effects.js';
const $=s=>document.querySelector(s);
let renderer,scene,root,entry,portal,field,composer,shipEffects;
const originals=new Map(),specialMaterials=new Set();
const fail=error=>{console.error(error);$('#status').hidden=false;$('#status').textContent='Preview error: '+error.message;};
try{
 const response=await fetch('./catalog.json');if(!response.ok)throw Error('Catalogue unavailable');
 const catalog=await response.json();
 const engineResponse=await fetch('./engines.json');if(!engineResponse.ok)throw Error('Drive catalogue unavailable');const engines=await engineResponse.json();
 renderer=new THREE.WebGLRenderer({canvas:$('canvas'),antialias:true,powerPreference:'high-performance'});
 renderer.setPixelRatio(devicePixelRatio);renderer.outputColorSpace=THREE.SRGBColorSpace;
 renderer.toneMapping=THREE.AgXToneMapping;renderer.toneMappingExposure=1;
 renderer.shadowMap.enabled=true;renderer.shadowMap.type=THREE.PCFShadowMap;renderer.shadowMap.autoUpdate=false;
 renderer.debug.onShaderError=()=>fail(Error('A GPU shader failed to compile'));
 scene=new THREE.Scene();scene.background=new THREE.Color('#0b121a');
 const pmrem=new THREE.PMREMGenerator(renderer),room=new RoomEnvironment(),environment=pmrem.fromScene(room,.04);
 scene.environment=environment.texture;scene.environmentIntensity=.22;room.dispose();pmrem.dispose();
 const camera=new THREE.OrthographicCamera(-2,2,2,-2,.01,60);
 const controls=new OrbitControls(camera,renderer.domElement);controls.enableDamping=false;
 controls.autoRotate=true;controls.autoRotateSpeed=.65;controls.minZoom=.3;controls.maxZoom=40;
 const key=new THREE.DirectionalLight(0xfff7eb,4);key.position.set(4,6,5);key.castShadow=true;
 key.shadow.mapSize.set(2048,2048);Object.assign(key.shadow.camera,{left:-2.5,right:2.5,top:2.5,bottom:-2.5,near:.1,far:18});
 key.shadow.bias=-.00008;key.shadow.normalBias=.003;scene.add(key,key.target);
 const fill=new THREE.DirectionalLight(0xe7efff,.45);fill.position.set(-5,1,3);scene.add(fill);
 const rim=new THREE.DirectionalLight(0xbcd4ed,1.2);rim.position.set(2,-1,-6);scene.add(rim);
 const target=new THREE.WebGLRenderTarget(1,1,{type:THREE.HalfFloatType,samples:4});
 composer=new EffectComposer(renderer,target);composer.addPass(new RenderPass(scene,camera));
 const ao=new SSAOPass(scene,camera,1,1,24);ao.ssaoMaterial.defines.PERSPECTIVE_CAMERA=0;ao.depthRenderMaterial.defines.PERSPECTIVE_CAMERA=0;
 ao.kernelRadius=.06;ao.minDistance=.0005;ao.maxDistance=.03;composer.addPass(ao);
 composer.addPass(new UnrealBloomPass(new THREE.Vector2(1,1),.18,.35,1.5));composer.addPass(new OutputPass());
 const clay=new THREE.MeshStandardMaterial({color:0x7f868a,roughness:.63,metalness:0});
 const normals=new THREE.MeshNormalMaterial(),wire=new THREE.MeshBasicMaterial({color:0x9ad7e3,wireframe:true});
 let dirty=true,spin=true,animateField=true,previous=false,loadId=0,last=0,seconds=0,effectTime=0,frames=0,rateTime=0,rateFrames=0,shipLit=true,plumesOn=false;
 const invalidate=()=>{dirty=true;};controls.addEventListener('change',invalidate);
 function resize(){const w=innerWidth,h=innerHeight,a=w/h,frame=plumesOn&&shipEffects?.engineCount?2.6:2.16;renderer.setSize(w,h,false);composer.setSize(w,h);camera.left=-frame*a;camera.right=frame*a;camera.top=frame;camera.bottom=-frame;camera.updateProjectionMatrix();invalidate();}
 addEventListener('resize',resize);resize();
 function surface(){const mode=$('#surface').value;root?.traverse(o=>{if(!o.isMesh||o===field||!originals.has(o))return;o.material=mode==='materials'?originals.get(o):mode==='clay'?clay:mode==='normals'?normals:wire;});renderer.shadowMap.needsUpdate=true;invalidate();}
 function shipState(){
   if(!shipEffects)return;shipEffects.setLights(shipLit);shipEffects.setPlumes(plumesOn);
   const state=shipEffects.stats();$('#ship-lights').setAttribute('aria-pressed',shipLit);$('#ship-lights').textContent='Ship lights '+(shipLit?'on':'off');
   $('#plumes').disabled=!state.engineCount;$('#plumes').setAttribute('aria-pressed',Boolean(plumesOn&&state.engineCount));
   $('#plumes').textContent=state.engineCount?'Engine plumes '+(plumesOn?'on':'off'):'No engines / gate';
   $('#plumes').title=state.engineCount?'Independent animated exhaust at the model’s drive outlets':'The Sol gate has an animated aperture, not drive engines';
   $('#engine-state').textContent=state.engineCount?state.engineCount+' drive'+(state.engineCount===1?'':'s')+' · ship lights '+(shipLit?'lit':'unlit')+' · plumes '+(plumesOn?'on':'off'):'Gate rim lights '+(shipLit?'on':'off')+' · aperture controlled separately';
   document.body.dataset.lit=String(shipLit);document.body.dataset.plumes=String(plumesOn&&state.engineCount>0);document.body.dataset.engineCount=state.engineCount;
   document.body.dataset.activeEmitters=state.activeEmitters;document.body.dataset.activeSourceLights=state.activeSourceLights;document.body.dataset.visiblePlumes=state.visiblePlumes;
   resize();invalidate();
 }
 function view(name='hero'){
   if(!root)return;const gate=entry.orientation==='gate';
   const dirs={hero:gate?[2.5,.9,7]:[4,1.4,6],front:[0,0,8],side:[8,0,0],rear:[0,0,-8],opposite:[-8,0,0],aft:[.001,-8,.001],nose:[.001,8,.001],rim:[.5,1.6,2]};
   controls.target.set(0,0,0);camera.up.set(0,1,0);camera.position.set(...dirs[name]);camera.zoom=1;
   if(name==='aft'||name==='nose')camera.up.set(0,0,-1);
   if(name==='rim'){controls.target.set(0,1.55,0);camera.position.set(.25,1.8,1.8);camera.zoom=7;}
   camera.updateProjectionMatrix();controls.update();document.querySelectorAll('[data-view]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.view===name)));invalidate();
 }
 function lighting(){const mode=$('#lighting').value;
   const own=mode==='ship',cinema=mode==='cinema',darkHull=entry?.id==='Pella';scene.environmentIntensity=own?0:cinema?.08:darkHull?.30:.22;
   key.intensity=own?0:cinema?(darkHull?6.5:4.8):(darkHull?8:4);fill.intensity=own?0:cinema?.12:darkHull?.75:.45;rim.intensity=own?0:cinema?(darkHull?2.5:1.9):(darkHull?2:1.2);
   scene.background.set(own?'#02060b':cinema?'#050a10':'#0b121a');renderer.shadowMap.needsUpdate=true;invalidate();
 }
 function dispose(obj){const materials=new Set(),textures=new Set();obj.traverse(o=>{if(!o.isMesh)return;o.geometry.dispose();for(const m of [].concat(originals.get(o)||o.material)){materials.add(m);for(const t of Object.values(m))if(t?.isTexture)textures.add(t);}});for(const t of textures){const img=t.source?.data;if(typeof ImageBitmap!=='undefined'&&img instanceof ImageBitmap)img.close();t.dispose();}for(const m of materials)m.dispose();for(const m of specialMaterials)m.dispose();specialMaterials.clear();}
 function references(){const box=$('#reference-images');box.replaceChildren();$('#reference-note').textContent=entry.note;
   for(const r of entry.references||[]){const figure=document.createElement('figure'),a=document.createElement('a'),img=document.createElement('img'),caption=document.createElement('figcaption');a.href=r.image;a.target='_blank';a.rel='noopener';img.src=r.image;img.alt=r.caption;img.loading='lazy';caption.textContent=r.caption;a.append(img);figure.append(a,caption);box.append(figure);}
   if(!box.children.length){const p=document.createElement('p');p.textContent='Open the artist source below for the original reference views.';box.append(p);}
   for(const source of entry.relatedSources||[]){const p=document.createElement('p'),a=document.createElement('a');a.href=source.url;a.textContent=source.label;a.target='_blank';a.rel='noopener noreferrer';p.append(a);box.append(p);}
   $('#artist').href=entry.referenceUrl||entry.sourceUrl||'#';
 }
 async function load(id,reset=true){
   const token=++loadId,selected=catalog.find(e=>e.id===id)||catalog[0];$('#status').hidden=false;$('#status').textContent='Loading '+selected.title+'…';
   const file=previous&&selected.beforeModel?selected.beforeModel:selected.model;
   const response=await fetch(file);if(!response.ok)throw Error('Model unavailable: '+response.status);
   const buffer=await new Response(response.body.pipeThrough(new DecompressionStream('gzip'))).arrayBuffer();
   const hash=Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',buffer)),b=>b.toString(16).padStart(2,'0')).join('');
   const expected=previous&&selected.beforeModel?selected.beforeSha256:selected.sha256;
   if(hash!==expected)throw Error('Model checksum differs from catalogue');
   const gltf=await new GLTFLoader().parseAsync(buffer,'./');
   if(token!==loadId){dispose(gltf.scene);return;}
   if(root){shipEffects?.dispose();scene.remove(root);dispose(root);}originals.clear();field=null;portal=null;entry=selected;
   root=new THREE.Group();root.add(gltf.scene);gltf.scene.updateMatrixWorld(true);
   const bounds=new THREE.Box3().setFromObject(gltf.scene),size=bounds.getSize(new THREE.Vector3()),center=bounds.getCenter(new THREE.Vector3());
   const maximum=Math.max(size.x,size.y,size.z);if(!(maximum>0&&Number.isFinite(maximum)))throw Error('Invalid mesh bounds');
   const scale=3.2/maximum;gltf.scene.position.sub(center);root.scale.setScalar(scale);
   document.body.dataset.sourceMaximum=maximum.toFixed(6);document.body.dataset.fitScale=scale.toFixed(6);document.body.dataset.modelFile=previous&&entry.beforeModel?entry.beforeModel:entry.model;
   if(entry.orientation!=='gate'){const axis=size.x>=size.y&&size.x>=size.z?new THREE.Vector3(1,0,0):size.z>size.y?new THREE.Vector3(0,0,1):new THREE.Vector3(0,1,0);root.quaternion.setFromUnitVectors(axis,new THREE.Vector3(0,entry.flipLongitudinal?-1:1,0));}
   let lights=0;
   root.traverse(o=>{
     // Inspection exposure calibration: Blender's glTF exporter converts watts
     // using 683 lm/W; the existing studio rig uses artistic unit intensities.
     // Keep source light placement/colour, and calibrate only the display copy.
     if(o.isPointLight||o.isSpotLight){o.intensity*=scale*scale/683;if(o.distance)o.distance*=scale;lights++;}
     if(!o.isMesh)return;o.castShadow=true;o.receiveShadow=true;
     if(entry.id==='SolRing'&&o.name.startsWith('Active_aperture')){field=o;portal=makePortal();specialMaterials.add(portal);o.material.dispose();o.material=portal;o.castShadow=false;o.receiveShadow=false;}
     originals.set(o,o.material);
     for(const m of [].concat(o.material))for(const t of Object.values(m))if(t?.isTexture)t.anisotropy=Math.min(16,renderer.capabilities.getMaxAnisotropy());
   });
   // Blender keeps spaces in node names in some exporter revisions.
   if(entry.id==='SolRing'&&!field){root.traverse(o=>{if(o.isMesh&&o.name.includes('Active aperture')){field=o;portal=makePortal();specialMaterials.add(portal);o.material.dispose();o.material=portal;originals.set(o,portal);o.castShadow=false;o.receiveShadow=false;}});if(!field)throw Error('The separate gate aperture was not found');}
   const anchorEntry=previous&&entry.beforeEngineId?entry.beforeEngineId:entry.id;
   shipEffects=makeShipEffects(root,gltf.scene,engines[anchorEntry]?.engines||[],center,scale);
   scene.add(root);root.updateMatrixWorld(true);renderer.shadowMap.needsUpdate=true;
   const displayTitle=previous&&entry.beforeTitle?entry.beforeTitle:entry.title+(previous?' / previous':'');
   $('#title').textContent=displayTitle;document.title=displayTitle+' | rotating preview';$('#ship').value=entry.id;
   $('#note').textContent=previous&&entry.beforeNote?entry.beforeNote:entry.note;$('#credit').textContent=previous&&entry.beforeCredit?entry.beforeCredit:entry.credit;
   const info=previous&&entry.beforeInfo?entry.beforeInfo:entry;
   $('#technical').textContent=info.triangles.toLocaleString()+' triangles · '+info.materials+' materials · '+lights+' local lights · '+shipEffects.emitterMaterials+' emissive materials · full geometry';
   $('#before').hidden=!entry.beforeModel;$('#before').textContent=entry.beforeLabel||'Previous version';$('#portal').hidden=$('#effect').hidden=$('#rim-view').hidden=entry.id!=='SolRing';
   if(field)field.visible=$('#portal').getAttribute('aria-pressed')==='true';
   references();surface();lighting();shipState();if(reset)view();$('#status').hidden=true;
   document.body.dataset.loaded=entry.id;document.body.dataset.shipLights=lights;document.body.dataset.portalAnimated=Boolean(portal);
   const url=new URL(location.href);url.searchParams.set('ship',entry.id);history.replaceState(null,'',url);invalidate();
 }
 for(const e of catalog){const option=document.createElement('option');option.value=e.id;option.textContent=e.title;$('#ship').append(option);}
 $('#ship').onchange=()=>{previous=false;$('#before').setAttribute('aria-pressed','false');load($('#ship').value).catch(fail);};
 $('#rotate').onclick=()=>{spin=!spin;controls.autoRotate=spin;$('#rotate').setAttribute('aria-pressed',spin);$('#rotate').textContent=spin?'Pause rotation':'Resume rotation';invalidate();};
 $('#lighting').onchange=lighting;$('#surface').onchange=surface;
 $('#ship-lights').onclick=()=>{shipLit=!shipLit;shipState();};$('#plumes').onclick=()=>{plumesOn=!plumesOn;shipState();};
 $('#ao').onclick=()=>{ao.enabled=!ao.enabled;$('#ao').setAttribute('aria-pressed',ao.enabled);invalidate();};
 $('#portal').onclick=()=>{if(!field)return;field.visible=!field.visible;$('#portal').setAttribute('aria-pressed',field.visible);$('#portal').textContent=field.visible?'Portal on':'Portal off';invalidate();};
 $('#effect').onclick=()=>{animateField=!animateField;$('#effect').setAttribute('aria-pressed',animateField);invalidate();};
 $('#before').onclick=()=>{previous=!previous;$('#before').setAttribute('aria-pressed',previous);load(entry.id,false).catch(fail);};
 $('#reset').onclick=()=>view();document.querySelectorAll('[data-view]').forEach(b=>b.onclick=()=>view(b.dataset.view));
 const toggleReferences=open=>{$('#reference-panel').hidden=!open;$('#references').setAttribute('aria-expanded',open);};
 $('#references').onclick=()=>toggleReferences($('#reference-panel').hidden);$('#close-references').onclick=()=>toggleReferences(false);
 addEventListener('keydown',e=>{if(e.key==='Escape')toggleReferences(false);});
 function tick(t){requestAnimationFrame(tick);const dt=Math.min(Math.max((t-last)*.001,0),.1);last=t;if(document.hidden)return;
   if(spin)controls.update(dt);if(portal&&animateField&&field.visible){seconds+=dt;portal.uniforms.time.value=seconds;dirty=true;}
   effectTime+=dt;if(shipEffects?.tick(effectTime))dirty=true;
   if(dirty||spin){ao.ssaoMaterial.uniforms.cameraProjectionMatrix.value.copy(camera.projectionMatrix);ao.ssaoMaterial.uniforms.cameraInverseProjectionMatrix.value.copy(camera.projectionMatrixInverse);composer.render();dirty=false;frames++;rateFrames++;}
   if(t-rateTime>=1000){const fps=Math.round(rateFrames*1000/(t-rateTime));$('#motion').textContent=(spin?'Rotating':'Rotation paused')+' · '+(fps>1?fps+' fps':'idle');$('#motion').dataset.frames=frames;$('#motion').dataset.cameraAngle=controls.getAzimuthalAngle().toFixed(4);$('#motion').dataset.fieldTime=seconds.toFixed(3);$('#motion').dataset.plumeTime=effectTime.toFixed(3);rateFrames=0;rateTime=t;}
 }
 document.addEventListener('visibilitychange',()=>{last=performance.now();rateTime=last;rateFrames=0;invalidate();});requestAnimationFrame(tick);
 await load(new URL(location.href).searchParams.get('ship'));
}catch(error){fail(error);}
