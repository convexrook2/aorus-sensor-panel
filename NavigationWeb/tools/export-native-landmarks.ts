// Offline export of the already-authored panel landmarks. No renderer is embedded.
import * as THREE from 'three';
import { writeFileSync, mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { gateModel, medinaModel, tychoModel } from '../src/models';

const output = resolve('Assets/SolarNavigation/models');
mkdirSync(output, { recursive: true });
for (const [name, group, radius] of [
  ['tycho', tychoModel(), .375], ['medina', medinaModel(), 1.23], ['solgate', gateModel(), 500],
] as const) {
  group.updateMatrixWorld(true);
  const centre = new THREE.Box3().setFromObject(group).getCenter(new THREE.Vector3());
  const positions: number[] = [], indices: number[] = [], colours: number[] = [];
  group.traverse(object => {
    if (!(object instanceof THREE.Mesh)) return;
    const material = object.material as THREE.Material & { color?: THREE.Color };
    // The web-only transparent portal shader is not part of the physical gate.
    if (!material.color) return;
    const colour = material.color.clone().convertLinearToSRGB();
    const geometry = object.geometry, source = geometry.attributes.position;
    const instances = object instanceof THREE.InstancedMesh ? object.count : 1;
    for (let instance = 0; instance < instances; instance++) {
      const matrix = object.matrixWorld.clone();
      if (object instanceof THREE.InstancedMesh) {
        const local = new THREE.Matrix4(); object.getMatrixAt(instance, local); matrix.multiply(local);
      }
      const base = positions.length / 3;
      for (let i = 0; i < source.count; i++) {
        const p = new THREE.Vector3().fromBufferAttribute(source, i).applyMatrix4(matrix).sub(centre).divideScalar(radius);
        positions.push(p.x, p.y, p.z);
        colours.push(...[colour.r, colour.g, colour.b].map(c => Math.round(c * 255)));
      }
      const index = geometry.index;
      for (let i = 0; i < (index?.count ?? source.count); i++) indices.push(base + (index ? index.getX(i) : i));
    }
  });
  const count = positions.length / 3, colourBytes = (colours.length + 3) & ~3;
  const data = Buffer.alloc(12 + positions.length * 4 + indices.length * 4 + colourBytes);
  data.write('FNM2'); data.writeUInt32LE(count, 4); data.writeUInt32LE(indices.length / 3, 8);
  let offset = 12;
  for (const p of positions) { data.writeFloatLE(p, offset); offset += 4; }
  for (const i of indices) { data.writeUInt32LE(i, offset); offset += 4; }
  Buffer.from(colours).copy(data, offset);
  writeFileSync(resolve(output, name + '.fnm'), data);
  process.stdout.write(`${name}: ${count} vertices, ${indices.length / 3} original faces\n`);
}
