// Exercise the real scheduling loop without claiming rendered/native evidence.
import fs from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import * as THREE from '../FleetStudio/vendor/three/build/three.module.js';
const base=new URL('../',import.meta.url),source=fs.readFileSync(new URL('Assets/FleetViewer/viewer.js',base),'utf8');
const posePath=new URL('Assets/FleetViewer/showcase-motion.js',base);
const pose=fs.existsSync(posePath)?await import('data:text/javascript;base64,'+fs.readFileSync(posePath).toString('base64')):{showcaseCameraAt:()=>[3.8,3.2,6]};
const tick=source.slice(source.indexOf(' function tick(t){'),source.indexOf(' function diagnose(){'));
function loop(options={}){
 const state={THREE,showcaseCameraAt:pose.showcaseCameraAt,root:{rotation:{y:-.2}},camera:{position:new THREE.Vector3(3.8,3.2,6),lookAt(){}},controls:{update(){}},
  document:{hidden:false},renderer:{shadowMap:{needsUpdate:false}},active:true,motion:true,holotable:false,interactive:false,efficient:false,
  displayStage:{visible:false},hologramMotion:true,stageSeconds:0,
  last:0,seconds:0,showcaseSeconds:0,raf:0,nextMotionFrame:0,motionFrameInterval:1000/30,dirty:true,frame:0,draws:0,schedule(){},...options};
 state.controls.update=()=>{state.dirty=true;}; // OrbitControls emits change when the camera moves.
 state.render=()=>{state.draws++;state.dirty=false;};vm.createContext(state);vm.runInContext(tick+'\nthis.step=tick;',state);
 let time=1;state.step(time);
 return {state,advance(seconds,hz=10){for(let i=0;i<Math.round(seconds*hz);i++){time+=1000/hz;state.step(time);}}};
}
const demo=loop(),start=demo.state.camera.position.toArray(),yaw=demo.state.root.rotation.y;
demo.advance(8);
assert.ok(demo.state.camera.position.y<-.5,'Showcase rotation must also tilt down during its brief sweep');
assert.ok(Math.abs(demo.state.root.rotation.y-yaw-8*Math.PI*2/30)<1e-8,'Keep the accepted 30-second rotation speed');
const frozen={camera:demo.state.camera.position.toArray(),yaw:demo.state.root.rotation.y,time:demo.state.showcaseSeconds};
demo.state.motion=false;demo.advance(4);
assert.deepEqual(demo.state.camera.position.toArray(),frozen.camera,'Pause must retain the tilted view');
assert.equal(demo.state.root.rotation.y,frozen.yaw);assert.equal(demo.state.showcaseSeconds,frozen.time);
demo.state.motion=true;demo.advance(6);
assert.ok(demo.state.camera.position.distanceTo(new THREE.Vector3(...start))<1e-8,'The tilt must return to the original view');
for(const stopped of [{active:false},{document:{hidden:true}}]){
 const hidden=loop(stopped);hidden.advance(8);assert.deepEqual(hidden.state.camera.position.toArray(),start);assert.equal(hidden.state.showcaseSeconds,0);assert.equal(hidden.state.root.rotation.y,-.2);
}
for(const mode of [{holotable:true},{interactive:true}]){
 const inspection=loop(mode);inspection.advance(8);assert.deepEqual(inspection.state.camera.position.toArray(),start,'Manual inspection camera must not acquire showcase tilt');
}
const full=loop(),efficient=loop({efficient:true});full.advance(1,60);efficient.advance(1,60);
assert.ok(full.state.draws>=59&&efficient.state.draws>=29&&efficient.state.draws<=32,'Tilt must retain Full Fidelity and Efficient drawing cadence');
console.log('PASS: actual native-viewer loop tilts and returns, preserves 30-second rotation, pauses both motions, stops while hidden/inactive, leaves manual inspection alone and retains rendering cadence. No native appearance/GPU claim.');
