import * as THREE from 'three';

// This module adds inspection effects to a display copy. Source models and
// their materials stay separately recoverable; no monitoring data is touched.
export function makeShipEffects(root,model,anchors,center,scale){
 const group=new THREE.Group();group.name='Browser engine effects';root.add(group);
 const emitters=new Map(),lights=[],cores=[],plumeLights=[],plumes=[],geometries=[],materials=[];
 model.traverse(o=>{
  if(o.isLight)lights.push({object:o,intensity:o.intensity});
  if(o.isMesh)for(const m of [].concat(o.material))if(m.emissive&&(m.emissiveMap||m.emissive.r+m.emissive.g+m.emissive.b>0))emitters.set(m,m.emissiveIntensity??1);
 });
 const clock={value:0};
 const vertex=`varying vec2 vUv;varying vec3 vNormal;varying vec3 vView;
 void main(){vUv=uv;vec4 p=modelViewMatrix*vec4(position,1.);vNormal=normalize(normalMatrix*normal);vView=normalize(-p.xyz);gl_Position=projectionMatrix*p;}`;
 const fragment=`uniform float time;uniform vec3 tint;varying vec2 vUv;varying vec3 vNormal;varying vec3 vView;
 void main(){float t=clamp(vUv.y,0.,1.);float ripple=.90+.06*sin(t*73.-time*22.)+.04*sin(vUv.x*43.+t*111.-time*37.);
 float edge=.32+.68*pow(abs(dot(normalize(vNormal),normalize(vView))),.7);
 float diamonds=1.+.16*sin(t*51.-pow(t,.65)*21.);
 float a=pow(max(0.,1.-t),1.65)*ripple*edge*diamonds;
 vec3 col=mix(vec3(.77,.94,1.),tint,smoothstep(0.,.23,t));
 gl_FragColor=vec4(col*(1.7+.9*exp(-t*9.)),a*.72);}`;
 function plumeGeometry(radius,length){
  const v=[],n=[],uv=[],indices=[],rings=36,sides=40;
  for(let j=0;j<=rings;j++){
   const t=j/rings,r=radius*(.85+.20*Math.sin(Math.min(1,t*4)*Math.PI*.5))*Math.pow(1-t,.72)+radius*.012;
   for(let i=0;i<=sides;i++){const a=i/sides*Math.PI*2;v.push(r*Math.cos(a),length*t,r*Math.sin(a));n.push(Math.cos(a),.15,Math.sin(a));uv.push(i/sides,t);}
  }
  for(let j=0;j<rings;j++)for(let i=0;i<sides;i++){const a=j*(sides+1)+i,b=a+1,c=a+sides+1,d=c+1;indices.push(a,c,b,b,c,d);}
  const g=new THREE.BufferGeometry();g.setAttribute('position',new THREE.Float32BufferAttribute(v,3));g.setAttribute('normal',new THREE.Float32BufferAttribute(n,3));g.setAttribute('uv',new THREE.Float32BufferAttribute(uv,2));g.setIndex(indices);geometries.push(g);return g;
 }
 for(const a of anchors){
  const p=new THREE.Vector3(...a.position).sub(center),d=new THREE.Vector3(...a.direction).normalize();
  const node=new THREE.Group();node.position.copy(p);node.quaternion.setFromUnitVectors(new THREE.Vector3(0,1,0),d);group.add(node);
  const color=new THREE.Color(...(a.color||[.09,.42,1]));
  const m=new THREE.ShaderMaterial({uniforms:{time:clock,tint:{value:color}},vertexShader:vertex,fragmentShader:fragment,transparent:true,depthWrite:false,side:THREE.DoubleSide,blending:THREE.AdditiveBlending,toneMapped:true});
  materials.push(m);const plume=new THREE.Mesh(plumeGeometry(a.radius,a.length),m);plume.name='Animated engine plume';plume.castShadow=false;plume.receiveShadow=false;node.add(plume);plumes.push(plume);
  const spill=new THREE.PointLight(color,.06,Math.min(.5,a.radius*scale*5),2);spill.position.set(0,a.radius*.6,0);node.add(spill);plumeLights.push(spill);
  if(a.addCore){
   const g=new THREE.CircleGeometry(a.radius*.79,64);geometries.push(g);
   const mat=new THREE.MeshStandardMaterial({color:0x03070a,emissive:color,emissiveIntensity:5,roughness:.7,metalness:0,side:THREE.DoubleSide});materials.push(mat);
   const core=new THREE.Mesh(g,mat);core.rotation.x=-Math.PI/2;core.position.y=-a.radius*.14;core.name='Recessed inspection drive core';node.add(core);cores.push({object:core,material:mat});
  }
 }
 let lit=true,burning=false;
 function apply(){
  for(const [m,intensity] of emitters)m.emissiveIntensity=lit?intensity:0;
  for(const a of lights)a.object.intensity=lit?a.intensity:0;
  for(const p of plumes)p.visible=burning;
  for(const l of plumeLights)l.visible=burning;
  for(const c of cores)c.material.emissiveIntensity=lit||burning?5:0;
 }
 return {
  engineCount:anchors.length,emitterMaterials:emitters.size,sourceLightCount:lights.length,
  setLights(value){lit=Boolean(value);apply();},
  setPlumes(value){burning=Boolean(value);apply();},
  tick(t){clock.value=t;return burning&&plumes.length>0;},
  stats(){return {lit,burning,engineCount:anchors.length,activeEmitters:lit?emitters.size:0,activeSourceLights:lit?lights.length:0,visiblePlumes:burning?plumes.length:0};},
  dispose(){root.remove(group);for(const g of geometries)g.dispose();for(const m of materials)m.dispose();}
 };
}
