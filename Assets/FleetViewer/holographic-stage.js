import * as THREE from 'three';

// A separate light structure below the inspected model: no filled floor,
// projector housing or source-model changes. All geometry exists in 3D.
export function makeHolographicStage(){
 const group=new THREE.Group();group.name='Holotable / projected light';
 const geometries=new Set(),materials=new Set(),arcs=[];
 const material=(name,opacity,intensity,white=0)=>{
  const m=new THREE.MeshStandardMaterial({name,color:0x000000,emissive:0x73dfff,
   emissiveIntensity:intensity,transparent:true,opacity,depthTest:true,depthWrite:false,
   blending:THREE.AdditiveBlending,roughness:1,metalness:0});
  m.userData.hologramWhite=white;materials.add(m);return m;
 };
 const rim=material('Hologram / fine rim',.62,2.4,.48);
 const trace=material('Hologram / moving traces',.45,2.2,.12);
 const beam=material('Hologram / open projection lattice',.12,1.1,.1);
 const scan=material('Hologram / rising scan',.26,1.8,.25);
 function add(geometry,m,parent=group){
  geometries.add(geometry);const mesh=new THREE.Mesh(geometry,m);
  // Layer 30 is already omitted from opaque AO and plume hull-depth passes.
  // Ordinary ship depth still hides projected light behind solid hardware.
  mesh.layers.set(30);mesh.castShadow=false;mesh.receiveShadow=false;parent.add(mesh);return mesh;
 }
 function ring(radius,y,tube,m,arc=Math.PI*2,parent=group){
  const mesh=add(new THREE.TorusGeometry(radius,tube,5,128,arc),m,parent);
  mesh.rotation.x=-Math.PI/2;mesh.position.y=y;return mesh;
 }
 ring(1,.025,.003,rim);ring(.94,-.12,.002,trace);ring(.71,.075,.0018,trace);
 for(const [radius,y,speed] of [[1.025,.08,.22],[.79,.135,-.31]]){
  const tier=new THREE.Group();tier.position.y=y;group.add(tier);
  for(let i=0;i<3;i++){
   const arc=ring(radius,0,.003,trace,Math.PI*.38,tier);arc.rotation.z=i*Math.PI*2/3;
  }
  arcs.push({group:tier,speed});
 }
 // Thin real filaments form an open bowl and vertical edge. Instancing keeps
 // the lattice to one draw rather than drawing every spoke separately.
 const segments=[];
 for(let i=0;i<24;i++){
  const angle=i*Math.PI*2/24,c=Math.cos(angle),s=Math.sin(angle);
  segments.push([new THREE.Vector3(c*.18,-.12,s*.18),new THREE.Vector3(c*.96,.025,s*.96)]);
  if(i%2===0)segments.push([new THREE.Vector3(c*.94,-.12,s*.94),new THREE.Vector3(c*.94,.17,s*.94)]);
 }
 const filament=new THREE.CylinderGeometry(.0015,.0015,1,4,1,true);geometries.add(filament);
 const lattice=new THREE.InstancedMesh(filament,beam,segments.length),pose=new THREE.Object3D(),up=new THREE.Vector3(0,1,0);
 lattice.layers.set(30);lattice.castShadow=false;lattice.receiveShadow=false;
 for(const [i,[start,end]] of segments.entries()){
  const direction=end.clone().sub(start);pose.position.copy(start).add(end).multiplyScalar(.5);
  pose.quaternion.setFromUnitVectors(up,direction.clone().normalize());pose.scale.set(1,direction.length(),1);
  pose.updateMatrix();lattice.setMatrixAt(i,pose.matrix);
 }
 lattice.instanceMatrix.needsUpdate=true;group.add(lattice);
 const scanRing=ring(.9,-.10,.002,scan);let elapsed=0;
 function tick(seconds){
  elapsed=seconds;
  for(const arc of arcs)arc.group.rotation.y=seconds*arc.speed;
  lattice.rotation.y=seconds*.045;
  const phase=(seconds%3.6)/3.6;
  scanRing.position.y=-.10+phase*.34;
  scanRing.scale.setScalar(.88+phase*.18);
  scan.opacity=.26*Math.sin(Math.PI*phase)**2;
  beam.opacity=.10+.025*Math.sin(seconds*1.7);
 }
 function setColour(colour){
  const tint=new THREE.Color(colour),white=new THREE.Color(0xc9f4ff);
  for(const m of materials)m.emissive.copy(tint).lerp(white,m.userData.hologramWhite);
 }
 function corners(){
  group.updateWorldMatrix(true,true);const box=new THREE.Box3().setFromObject(group),points=[];
  for(const x of [box.min.x,box.max.x])for(const y of [box.min.y,box.max.y])for(const z of [box.min.z,box.max.z])points.push(new THREE.Vector3(x,y,z));
  return points;
 }
 function requiredHalfHeight(camera,aspect){
  camera.updateMatrixWorld(true);let half=0;
  for(const point of corners()){
   point.applyMatrix4(camera.matrixWorldInverse);half=Math.max(half,Math.abs(point.y),Math.abs(point.x)/aspect);
  }
  return half*1.12;
 }
 setColour('#73dfff');tick(0);
 return {group,tick,setColour,requiredHalfHeight,
  diagnose(camera){
   let bounds=null;
   if(camera&&group.visible){
    camera.updateMatrixWorld(true);const points=corners().map(p=>p.project(camera));
    bounds={left:Math.min(...points.map(p=>p.x)),right:Math.max(...points.map(p=>p.x)),bottom:Math.min(...points.map(p=>p.y)),top:Math.max(...points.map(p=>p.y))};
   }
   return {type:'holographic-light-field-v1',elapsed,arcRadians:arcs[0].group.rotation.y,scanHeight:scanRing.position.y,solidPlatform:false,bounds};
  },
  dispose(){group.removeFromParent();group.clear();lattice.dispose();for(const g of geometries)g.dispose();for(const m of materials)m.dispose();geometries.clear();materials.clear();}
 };
}
