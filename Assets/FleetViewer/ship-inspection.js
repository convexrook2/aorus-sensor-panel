import * as THREE from 'three';

// OrbitControls reads gestures through a separate camera. Apply only its
// incremental orientation/zoom to the ship; the rendered platform stays put.
export function makeShipInspection(renderCamera){
 const camera=renderCamera.clone(),previous=new THREE.Quaternion();
 return {camera,
  sync(){camera.copy(renderCamera);previous.identity();},
  syncProjection(){
   for(const key of ['left','right','top','bottom','near','far'])camera[key]=renderCamera[key];
   camera.updateProjectionMatrix();
  },
  apply(ship){
   const next=renderCamera.quaternion.clone().multiply(camera.quaternion.clone().invert());
   ship.quaternion.premultiply(next.clone().multiply(previous.clone().invert()));
   ship.scale.setScalar(camera.zoom);previous.copy(next);
  }
 };
}
