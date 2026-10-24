// Exercise the actual viewer scheduler and real Three objects. Native pixels,
// accepted appearance and whole-process resource use remain separate evidence.
import fs from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import * as THREE from '../Assets/FleetViewer/vendor/three/build/three.module.js';
const base=new URL('../',import.meta.url);
const source=fs.readFileSync(new URL('Assets/FleetViewer/viewer.js',base),'utf8');
const tick=source.slice(source.indexOf(' function tick(t){'),source.indexOf(' function diagnose(){'));
function loop(options={}){
 const state={root:{rotation:{y:.25}},camera:{position:new THREE.Vector3(4,1.7,6)},controls:{update(){}},
  renderer:{shadowMap:{needsUpdate:false}},document:{hidden:false},displayStage:{visible:true},
  active:true,motion:false,holotable:true,interactive:true,hologramMotion:true,efficient:false,
  last:0,seconds:0,showcaseSeconds:0,stageSeconds:0,raf:0,nextMotionFrame:0,motionFrameInterval:1000/30,
  dirty:true,frame:0,draws:0,schedule(){},...options};
 state.render=()=>{state.draws++;state.dirty=false;};
 vm.createContext(state);vm.runInContext(tick+'\nthis.step=tick;',state);
 let time=1;state.step(time);
 return {state,advance(seconds,hz=60){for(let i=0;i<Math.round(seconds*hz);i++){time+=1000/hz;state.step(time);}}};
}
const hologram=loop();hologram.advance(1);
assert.ok(hologram.state.stageSeconds>.95&&hologram.state.draws>=59,'Visible hologram must animate with ship AUTO ROTATE off');
assert.equal(hologram.state.root.rotation.y,.25,'Hologram animation must not rotate the inspected ship');
assert.deepEqual(hologram.state.camera.position.toArray(),[4,1.7,6],'Hologram must not move the inspection camera');
for(const mode of [{active:false},{document:{hidden:true}},{hologramMotion:false},{displayStage:{visible:false}},{holotable:false}]){
 const stopped=loop(mode);stopped.advance(1);
 assert.equal(stopped.state.stageSeconds,0,'Hidden, inactive or paused hologram must retain its phase');
 assert.ok(stopped.state.draws<=1,'A stopped hologram must not keep drawing');
}
const efficient=loop({efficient:true});efficient.advance(1);
assert.ok(efficient.state.draws>=29&&efficient.state.draws<=32,'Hologram must respect Efficient drawing cadence');
hologram.state.hologramMotion=false;const held=hologram.state.stageSeconds;hologram.advance(2);
assert.equal(hologram.state.stageSeconds,held);
hologram.state.hologramMotion=true;hologram.advance(1);assert.ok(hologram.state.stageSeconds>held+.95,'Resume must continue from the held phase');

const threeURL=new URL('Assets/FleetViewer/vendor/three/build/three.module.js',base).href;
const module=fs.readFileSync(new URL('Assets/FleetViewer/holographic-stage.js',base),'utf8').replace("from 'three'",'from '+JSON.stringify(threeURL));
const {makeHolographicStage}=await import('data:text/javascript;base64,'+Buffer.from(module).toString('base64'));
const stage=makeHolographicStage(),geometry=new Set(),materials=new Set(),instances=new Set();
stage.group.traverse(object=>{
 if(!object.isMesh)return;
 if(object.isInstancedMesh)instances.add(object);
 geometry.add(object.geometry);materials.add(object.material);
 assert.ok(object.layers.isEnabled(30),'Light geometry must stay out of opaque AO and hull-depth passes');
 assert.equal(object.castShadow,false);assert.equal(object.receiveShadow,false);
 const m=object.material;
 assert.equal(m.transparent,true);assert.equal(m.depthWrite,false);assert.equal(m.depthTest,true);
 assert.equal(m.blending,THREE.AdditiveBlending);assert.ok(m.emissiveIntensity>0);
 if(object.geometry.type==='CylinderGeometry')assert.equal(object.geometry.parameters.openEnded,true,'No capped physical plinth');
});
stage.group.updateMatrixWorld(true);const bounds=new THREE.Box3().setFromObject(stage.group),size=bounds.getSize(new THREE.Vector3());
assert.ok(size.y>.2&&size.x>1.8&&size.z>1.8,'The light platform must have actual height and circular depth');
stage.tick(0);const start=stage.diagnose();stage.tick(1);const moved=stage.diagnose();
assert.notEqual(moved.arcRadians,start.arcRadians);assert.notEqual(moved.scanHeight,start.scanHeight);
stage.setColour('#80aaff');assert.ok([...materials].every(m=>m.emissive.r+m.emissive.g+m.emissive.b>0));
stage.group.scale.set(2,1,2);stage.group.position.y=-1.7;
const camera=new THREE.OrthographicCamera(-1,1,1,-1,.01,40);camera.position.set(4,1.7,6);camera.lookAt(0,0,0);
for(const aspect of [16/9,.75]){
 const half=stage.requiredHalfHeight(camera,aspect);
 camera.left=-half*aspect;camera.right=half*aspect;camera.top=half;camera.bottom=-half;camera.updateProjectionMatrix();
 const box=stage.diagnose(camera).bounds;
 assert.ok(Object.values(box).every(n=>Math.abs(n)<.95),'Fit the full light structure above the footer in wide and portrait views');
}
let disposed=0;for(const item of [...geometry,...materials,...instances])item.addEventListener('dispose',()=>disposed++);
stage.dispose();assert.equal(disposed,geometry.size+materials.size+instances.size,'Release each owned geometry, material and instance buffer exactly once');
console.log('PASS: 3D emissive light structure, independent animation with a stationary ship/camera, selected frame cadence, hidden/inactive/paused freeze and resource disposal. Source checks only.');
