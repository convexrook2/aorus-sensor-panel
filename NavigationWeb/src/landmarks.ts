import type { Ephemeris } from '../vendor/belter-charts/src/ephemeris';
import type { Vec3 } from '../vendor/belter-charts/src/ephemeris/vec';

export const AU_KM = 149_597_870.7;
export const ERA = new Date('2356-01-01T00:00:00Z');
// Story-era geometry, not a claim that the series specifies this calendar date.
export const GATE_RADIUS_KM = (19.19126393 + 2) * AU_KM;
const gateAngle = 200 * Math.PI / 180;
export const GATE_POSITION: Vec3 = {
  x: GATE_RADIUS_KM * Math.cos(gateAngle), y: GATE_RADIUS_KM * Math.sin(gateAngle), z: 0,
};
export const RING_SPACE_RADIUS_KM = 500_000;
export const MEDINA_POSITION: Vec3 = { x: 24_000, y: -10_000, z: 4_000 };
export const RING_SPACE_SOL_GATE: Vec3 = { x: -RING_SPACE_RADIUS_KM, y: 0, z: 0 };
export const MEDINA_LENGTH_KM = 2.46;
export const TYCHO_DIAMETER_KM = .75;

export function solPosition(eph: Ephemeris, id: string, date = ERA): Vec3 {
  // The published Belter Charts snapshot incorrectly orbits the Ring. Keep it
  // fixed; the 200-degree azimuth is an explicitly illustrative scene choice.
  return id === 'ring' ? { ...GATE_POSITION } : eph.stateOf(id, date).pos;
}

export const ID_TO_NATIVE: Record<string, string> = {
  sun:'Sun', mercury:'Mercury', venus:'Venus', earth:'Earth', luna:'Luna', mars:'Mars',
  phobos:'Phobos', deimos:'Deimos', jupiter:'Jupiter', io:'Io', europa:'Europa',
  ganymede:'Ganymede', callisto:'Callisto', saturn:'Saturn', titan:'Titan', phoebe:'Phoebe',
  uranus:'Uranus', neptune:'Neptune', ceres:'Ceres', eros:'Eros', vesta:'Vesta',
  pallas:'Pallas', hygiea:'Hygiea', juno:'Juno', tycho:'Tycho', anderson:'Anderson',
  ring:'SolGate', medina:'Medina', 'ring-station':'RingStation', 'ring-space':'RingSpace',
};
export const NATIVE_TO_ID = Object.fromEntries(Object.entries(ID_TO_NATIVE).map(([a,b])=>[b,a]));
export function normaliseId(id: string): string {
  return NATIVE_TO_ID[id] ?? (ID_TO_NATIVE[id] ? id : 'sun');
}

export function destinationNote(id: string): string {
  if (id === 'tycho') return 'MOBILE CONSTRUCTION PLATFORM · 750 m\nBelt location approximate · Ceres +60° fan convention';
  if (id === 'ring') return 'SOL → RING SPACE · 1,000 km gate\n2 AU beyond Uranus orbit · direction illustrative';
  if (id === 'medina') return 'FORMER NAUVOO / BEHEMOTH · 2.46 km\nInside Ring Space · local placement illustrative';
  if (id === 'ring-station') return 'ALIEN CONTROL STATION · 5 km sphere\nCentre of Ring Space · distinct from Medina';
  if (id === 'ring-space') return 'SEPARATE SPACE · 1,000,000 km across\nSol gate on boundary · Medina inside';
  if (id === 'anderson') return 'BELT STATION\nLocation and dimensions illustrative';
  return 'PHYSICAL SIZE · HELIOCENTRIC ECLIPTIC J2000\nExpanse-era chart · 2356-01-01 illustrative epoch';
}

export function checkLandmarks(eph: Ephemeris) {
  const length = (v: Vec3) => Math.hypot(v.x,v.y,v.z);
  const a = solPosition(eph,'ring',ERA), b = solPosition(eph,'ring',new Date('2360-01-01'));
  if (a.x!==b.x || a.y!==b.y || a.z!==b.z) throw new Error('The Sol gate moved with time.');
  if (Math.abs(length(a)/AU_KM - (19.19126393+2))>1e-9) throw new Error('Gate radius lost its AU scale.');
  const ceres=solPosition(eph,'ceres'), tycho=solPosition(eph,'tycho');
  if (Math.abs(length(ceres)-length(tycho))>.01) throw new Error('Tycho left the chosen Belt placement.');
  if (length(MEDINA_POSITION)>=RING_SPACE_RADIUS_KM) throw new Error('Medina is outside Ring Space.');
  if (length(RING_SPACE_SOL_GATE)!==RING_SPACE_RADIUS_KM) throw new Error('Ring Space boundary scale failed.');
  return { gateStationary:true, gateRadiusAu:length(a)/AU_KM, tychoPlacement:'Approximate: Ceres +60 degrees',
    tychoDiameterKm:TYCHO_DIAMETER_KM, medinaScene:'RingSpace', medinaLengthKm:MEDINA_LENGTH_KM,
    medinaPlacement:'Illustrative within Ring Space', ringSpaceDiameterKm:RING_SPACE_RADIUS_KM*2,
    physicalMeshScale:1, worldUnits:'kilometres', epoch:ERA.toISOString(), epochIsIllustrative:true };
}
