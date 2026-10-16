import * as THREE from 'three';
import { MEDINA_LENGTH_KM, TYCHO_DIAMETER_KM } from './landmarks';

const steel = new THREE.MeshStandardMaterial({color:0x82909c,metalness:.68,roughness:.52});
const dark = new THREE.MeshStandardMaterial({color:0x192b3c,metalness:.7,roughness:.63});
const white = new THREE.MeshStandardMaterial({color:0xd2d1bf,metalness:.4,roughness:.55});
const brass = new THREE.MeshStandardMaterial({color:0x8e7852,metalness:.6,roughness:.6});
const blue = new THREE.MeshBasicMaterial({color:0x9cecff});
const amber = new THREE.MeshBasicMaterial({color:0xffce8a});
const box = new THREE.BoxGeometry(1,1,1);
function block(g: THREE.Group, size: [number,number,number], p: [number,number,number], mat:THREE.Material=steel) {
  const m=new THREE.Mesh(box,mat); m.scale.set(...size); m.position.set(...p);g.add(m);return m;
}
function cylinder(g: THREE.Group,r:number,h:number,z:number,mat:THREE.Material=steel,r2=r) {
  const m=new THREE.Mesh(new THREE.CylinderGeometry(r,r2,h,48),mat);
  m.rotation.x=Math.PI/2;m.position.z=z;g.add(m);return m;
}
function torus(g:THREE.Group,r:number,t:number,z:number,mat:THREE.Material=steel) {
  const m=new THREE.Mesh(new THREE.TorusGeometry(r,t,8,96),mat);m.position.z=z;g.add(m);return m;
}
function windows(g:THREE.Group,r:number,z:number,count:number,size:number,mat=amber) {
  const inst=new THREE.InstancedMesh(box,mat,count), o=new THREE.Object3D();
  for(let i=0;i<count;i++) {const a=i*Math.PI*2/count;o.position.set(Math.cos(a)*r,Math.sin(a)*r,z);o.rotation.z=a;o.scale.set(size,size*.35,size*.25);o.updateMatrix();inst.setMatrixAt(i,o.matrix)}
  g.add(inst);
}
function normalise(g:THREE.Group,longestKm:number) {
  g.updateMatrixWorld(true);const b=new THREE.Box3().setFromObject(g),s=b.getSize(new THREE.Vector3());
  const factor=longestKm/Math.max(s.x,s.y,s.z);g.scale.setScalar(factor);
  return g;
}
// Original detail over Belter Charts' ring/spokes/hub construction. Geometry is
// authored here, not copied from a restricted print or TV production mesh.
export function tychoModel() {
  const g=new THREE.Group();g.name='Tycho physical model';
  const hub=new THREE.Mesh(new THREE.SphereGeometry(.19,32,20),steel);hub.scale.z=1.1;g.add(hub);
  for(const z of [-.065,.065]) {
    torus(g,.33,.022,z,white);torus(g,.31,.009,z,dark);windows(g,.342,z,120,.009);
    for(let i=0;i<8;i++){const a=i*Math.PI/4;const spoke=block(g,[.2,.013,.018],[Math.cos(a)*.22,Math.sin(a)*.22,z]);spoke.rotation.z=a;}
  }
  cylinder(g,.075,.1,-.25,dark);cylinder(g,.032,.13,-.34,steel,.06);
  cylinder(g,.05,.12,.23,dark);windows(g,.06,.29,12,.009,blue);
  for(let i=0;i<6;i++) {
    const a=i*Math.PI/3, x=Math.cos(a),y=Math.sin(a);
    const leg=block(g,[.025,.025,.25],[x*.17,y*.17,.27],dark);leg.rotation.y=.25;
    const arm=block(g,[.15,.018,.018],[x*.22,y*.22,.39],brass);arm.rotation.z=a;
    block(g,[.035,.035,.05],[x*.29,y*.29,.40],steel);
  }
  for(let i=0;i<16;i++){const a=i*Math.PI/8;const pod=block(g,[.032,.024,.035],[Math.cos(a)*.20,Math.sin(a)*.20,-.05],dark);pod.rotation.z=a;}
  return normalise(g,TYCHO_DIAMETER_KM);
}
export function medinaModel() {
  const g=new THREE.Group();g.name='Medina physical model';
  cylinder(g,.43,1.70,0,white);cylinder(g,.445,.11,-.86,dark);cylinder(g,.445,.11,.86,dark);
  cylinder(g,.34,.1,.97,steel,.43);cylinder(g,.30,.11,-.98,dark,.43);
  for(const z of [-.75,-.4,0,.4,.75]) {torus(g,.433,.012,z,brass);windows(g,.437,z,100,.009)}
  // End trusses, command hull and Epstein-drive support frame.
  for(let i=0;i<12;i++){
    const a=i*Math.PI/6,x=Math.cos(a),y=Math.sin(a);
    block(g,[.016,.016,.35],[x*.29,y*.29,1.08],dark);
    block(g,[.02,.02,.27],[x*.31,y*.31,-1.07],steel);
    if(i%2===0){const m=block(g,[.04,.04,1.68],[x*.433,y*.433,0],steel);m.rotation.z=a;}
  }
  cylinder(g,.20,.17,1.22,steel,.31);cylinder(g,.10,.12,1.36,white);
  cylinder(g,.32,.14,-1.24,steel,.23);
  for(let i=0;i<6;i++){
    const a=i*Math.PI/3,r=.23;
    const engine=new THREE.Mesh(new THREE.ConeGeometry(.055,.13,12,1,true),dark);
    engine.rotation.x=-Math.PI/2;engine.position.set(Math.cos(a)*r,Math.sin(a)*r,-1.34);g.add(engine);
  }
  block(g,[.035,.035,.3],[.09,0,1.49],dark);block(g,[.32,.015,.015],[0,0,1.58],steel);
  return normalise(g,MEDINA_LENGTH_KM);
}
export function gateModel() {
  const g=new THREE.Group();g.name='Sol gate physical model';
  const metal=new THREE.MeshStandardMaterial({color:0x233d42,metalness:.78,roughness:.42,emissive:0x092c34,emissiveIntensity:.6});
  torus(g,488,12,0,metal);torus(g,478,1.2,1,blue);torus(g,499,1,0,dark);
  const inst=new THREE.InstancedMesh(box,steel,128),o=new THREE.Object3D();
  for(let i=0;i<128;i++){const a=i*Math.PI/64;o.position.set(Math.cos(a)*488,Math.sin(a)*488,0);o.rotation.z=a;o.scale.set(22,2.2,17);o.updateMatrix();inst.setMatrixAt(i,o.matrix)}g.add(inst);
  const aperture=new THREE.Mesh(new THREE.CircleGeometry(476,128),new THREE.ShaderMaterial({
    transparent:true,side:THREE.DoubleSide,depthWrite:false,
    vertexShader:'varying vec2 p;void main(){p=uv*2.-1.;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}',
    fragmentShader:'varying vec2 p;void main(){float r=length(p);float a=pow(r,8.)*.17+.018;gl_FragColor=vec4(.12,.60,.83,a);}',
  }));g.add(aperture);return g;
}
export function alienStationModel() {
  const g=new THREE.Group();g.name='Alien Ring Station';
  const sphere=new THREE.Mesh(new THREE.IcosahedronGeometry(2.5,5),new THREE.MeshStandardMaterial({color:0x546472,roughness:.38,metalness:.8,flatShading:true}));g.add(sphere);
  const glow=new THREE.MeshBasicMaterial({color:0x8be8f3});
  for(const angle of [0,Math.PI/3,-Math.PI/3]) {const ring=torus(g,2.501,.005,0,glow);ring.rotation.y=angle;ring.rotation.x=.6;}
  return g;
}
