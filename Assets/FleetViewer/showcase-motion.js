// One brief camera-elevation sweep per 30-second showcase revolution. The
// fixed distance and azimuth avoid zoom/roll; the normal view returns exactly.
export function showcaseCameraAt(seconds){
 const phase=((seconds%30)+30)%30,u=(phase-3)/10;
 if(u<=0||u>=1)return [3.8,3.2,6];
 const horizontal=Math.hypot(3.8,6),distance=Math.hypot(horizontal,3.2);
 const elevation=Math.atan2(3.2,horizontal)-.85*Math.sin(Math.PI*u)**4;
 const reach=distance*Math.cos(elevation);
 return [reach*3.8/horizontal,distance*Math.sin(elevation),reach*6/horizontal];
}
