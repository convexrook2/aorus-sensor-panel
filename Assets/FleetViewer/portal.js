// Authored real-time approximation of the saved Blender caustic field.
// Screen references are not pasted onto the mesh as textures.
import * as THREE from 'three';
export function makePortal(){
  return new THREE.ShaderMaterial({name:'Browser animated transit field',side:THREE.DoubleSide,
    uniforms:{time:{value:0}},
    vertexShader:`varying vec2 p;void main(){p=position.xy/2.75;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}`,
    fragmentShader:`
      uniform float time;varying vec2 p;
      vec2 hash(vec2 v){v=vec2(dot(v,vec2(127.1,311.7)),dot(v,vec2(269.5,183.3)));return fract(sin(v)*43758.5453);}
      float noise(vec2 v){vec2 i=floor(v),f=fract(v);f=f*f*(3.-2.*f);return mix(mix(hash(i).x,hash(i+vec2(1,0)).x,f.x),mix(hash(i+vec2(0,1)).x,hash(i+1.).x,f.x),f.y);}
      float edge(vec2 v){vec2 i=floor(v),f=fract(v),nearest=vec2(0);float m=10.;vec2 cell=vec2(0);
        for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){vec2 g=vec2(float(x),float(y));vec2 rnd=hash(i+g);vec2 r=g+.5+.35*sin(time*.12+6.2831*rnd)-f;float d=dot(r,r);if(d<m){m=d;nearest=r;cell=g;}}
        float b=10.;for(int y=-2;y<=2;y++)for(int x=-2;x<=2;x++){vec2 g=cell+vec2(float(x),float(y));vec2 rnd=hash(i+g);vec2 r=g+.5+.35*sin(time*.12+6.2831*rnd)-f;if(dot(r-nearest,r-nearest)>.0001)b=min(b,dot(.5*(nearest+r),normalize(r-nearest)));}return b;}
      void main(){vec2 q=p*2.9+vec2(.15*time*.025,-.1*time*.025);float warp=noise(q*1.9+time*.016);q+=vec2(warp,noise(q+4.1-time*.012))*.65;
        float e=edge(q);float vein=exp(-e*18.);float veil=noise(q*1.3-time*.018);vec3 col=mix(vec3(.001,.006,.024),vec3(.009,.050,.085),veil*.45);col+=vec3(.005,.046,.062)*vein;
        gl_FragColor=vec4(col,1.);
        #include <tonemapping_fragment>
        #include <colorspace_fragment>
      }`});
}
