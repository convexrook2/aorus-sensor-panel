import { describe,it,expect } from 'vitest';
import { readFileSync } from 'node:fs';
import * as THREE from 'three';
import { Ephemeris } from '../vendor/belter-charts/src/ephemeris';
import { SmallBodyEphemeris } from '../vendor/belter-charts/src/ephemeris/smallbody';
import { AU_KM,ERA,GATE_POSITION,RING_SPACE_RADIUS_KM,MEDINA_POSITION,MEDINA_LENGTH_KM,TYCHO_DIAMETER_KM,solPosition,checkLandmarks,normaliseId } from './landmarks';
import { tychoModel,medinaModel,gateModel } from './models';
import { PanelEphemeris } from './panel-ephemeris';
const small=new Map(['ceres','eros','vesta','pallas','hygiea','juno'].map(id=>{
  const data=readFileSync(new URL(`../public/ephem/${id}.fnb`,import.meta.url));
  return [id,new SmallBodyEphemeris(data.buffer.slice(data.byteOffset,data.byteOffset+data.byteLength) as ArrayBuffer)] as const;
}));
const eph=new Ephemeris(small);
const size=(g:THREE.Object3D)=>new THREE.Box3().setFromObject(g).getSize(new THREE.Vector3());
describe('Expanse geography and physical geometry',()=>{
  it('keeps the Sol gate stationary across the ephemeris era',()=>{
    for(const year of [2345,2356,2364])expect(solPosition(eph,'ring',new Date(`${year}-06-01`))).toEqual(GATE_POSITION);
    expect(Math.hypot(GATE_POSITION.x,GATE_POSITION.y,GATE_POSITION.z)/AU_KM).toBeCloseTo(21.19126393,8);
  });
  it('places mobile Tycho in the Belt at the admitted approximate convention',()=>{
    const a=solPosition(eph,'tycho',ERA),b=solPosition(eph,'ceres',ERA);
    expect(Math.hypot(a.x,a.y,a.z)).toBeCloseTo(Math.hypot(b.x,b.y,b.z),3);
    expect(Math.hypot(a.x,a.y,a.z)/AU_KM).toBeGreaterThan(2);
    expect(solPosition(eph,'tycho',new Date('2357-01-01'))).not.toEqual(a);
  });
  it('keeps Medina inside a separate million-kilometre Ring Space',()=>{
    expect(Math.hypot(MEDINA_POSITION.x,MEDINA_POSITION.y,MEDINA_POSITION.z)).toBeLessThan(RING_SPACE_RADIUS_KM);
    expect(RING_SPACE_RADIUS_KM*2).toBe(1_000_000);
    expect(checkLandmarks(eph).medinaScene).toBe('RingSpace');
  });
  it('uses the same physical kilometre scale for station and gate meshes',()=>{
    const tycho=size(tychoModel()),medina=size(medinaModel()),gate=size(gateModel());
    expect(Math.max(...tycho.toArray())).toBeCloseTo(TYCHO_DIAMETER_KM,6);
    expect(Math.max(...medina.toArray())).toBeCloseTo(MEDINA_LENGTH_KM,6);
    expect(gate.x).toBeCloseTo(1000,3);
    expect(gate.y).toBeCloseTo(1000,3);
  });
  it('preserves the native ship navigation destinations',()=>{
    expect(normaliseId('Tycho')).toBe('tycho');expect(normaliseId('Medina')).toBe('medina');
    expect(normaliseId('SolGate')).toBe('ring');expect(normaliseId('Callisto')).toBe('callisto');
    expect(normaliseId('Luna')).toBe('luna');
  });
  it('retains the original astronomical states in the full scene adapter',()=>{
    const adapted=new PanelEphemeris(eph);
    for(const id of ['sun','earth','luna','saturn','ceres','tycho'])expect(adapted.stateOf(id,ERA)).toEqual(eph.stateOf(id,ERA));
    for(const year of [2345,2356,2364])expect(adapted.stateOf('ring',new Date(`${year}-01-01`)).pos).toEqual(GATE_POSITION);
  });
  it('never places Ring Space objects among the solar bodies',()=>{
    const adapted=new PanelEphemeris(eph);
    expect(adapted.exists('earth',ERA)).toBe(true);expect(adapted.exists('medina',ERA)).toBe(false);
    adapted.region='RingSpace';
    expect(adapted.exists('earth',ERA)).toBe(false);expect(adapted.exists('medina',ERA)).toBe(true);
    expect(adapted.stateOf('medina',ERA).pos).toEqual(MEDINA_POSITION);
  });
});
