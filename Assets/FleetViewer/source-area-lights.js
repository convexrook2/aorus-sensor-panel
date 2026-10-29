import * as THREE from 'three';
import {RectAreaLightUniformsLib} from 'three/addons/lights/RectAreaLightUniformsLib.js';

let initialized=false;
// glTF has no rectangular-light type. Carry Blender's source dimensions/power
// in node extras, and restore the real light shape in this display copy.
export function restoreSourceAreaLights(model,fitScale){
 const sources=[];
 model.traverse(light=>{
  if(!light.isSpotLight)return;
  let node=light,definition;
  while(node&&node!==model.parent){if(node.userData?.sourceArea){definition=node.userData.sourceArea;break;}node=node.parent;}
  if(definition)sources.push({light,definition});
 });
 if(!sources.length)throw Error('Source area-light definitions are missing');
 if(!initialized){RectAreaLightUniformsLib.init();initialized=true;}
 for(const {light,definition:d} of sources){
  if(!(d.width>0&&d.height>0&&d.watts>=0))throw Error('Invalid source area-light dimensions');
  const area=new THREE.RectAreaLight(0xffffff,1,d.width*fitScale,d.height*fitScale);
  area.name=light.name;area.color.copy(light.color);area.position.copy(light.position);
  area.quaternion.copy(light.quaternion);area.scale.copy(light.scale);
  // Keep source power relative to the normal model fit, as for punctual lamps.
  // RectAreaLight.power derives luminance from the actual rectangular area.
  area.power=d.watts*fitScale*fitScale;
  area.userData={...light.userData,sourceArea:d};
  const parent=light.parent;parent.add(area);parent.remove(light);
 }
 return sources.length;
}
