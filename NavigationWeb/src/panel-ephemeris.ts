import { Ephemeris, type StateVector } from '../vendor/belter-charts/src/ephemeris';
import { ERA, GATE_POSITION, MEDINA_POSITION, RING_SPACE_SOL_GATE } from './landmarks';

/** Keep the original astronomical model; fictional local space is separate. */
export class PanelEphemeris extends Ephemeris {
  region: 'Sol' | 'RingSpace' = 'Sol';
  private epoch = new Map<string, StateVector>();
  constructor(private readonly original: Ephemeris) { super(new Map()); }

  override stateOf(id: string, date: Date): StateVector {
    const fixed = id === 'ring' ? GATE_POSITION : id === 'medina' ? MEDINA_POSITION :
      id === 'ring-station' ? { x: 0, y: 0, z: 0 } : id === 'sol-gate-local' ? RING_SPACE_SOL_GATE : null;
    if (fixed) return { pos: { ...fixed }, vel: { x: 0, y: 0, z: 0 } };
    if (date.getTime() !== ERA.getTime()) return this.original.stateOf(id, date);
    let state = this.epoch.get(id);
    if (!state) { state = this.original.stateOf(id, date); this.epoch.set(id, state); }
    return state;
  }

  override exists(id: string, date: Date): boolean {
    const local = ['medina', 'ring-station', 'sol-gate-local'].includes(id);
    return this.region === 'RingSpace' ? local : !local && this.original.exists(id, date);
  }

  override coveredRange() { return this.original.coveredRange(); }
}
