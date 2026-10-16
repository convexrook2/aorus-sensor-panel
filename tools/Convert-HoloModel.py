"""Extract static triangle geometry from an authorised local GLB download.

Keeps the original geometry and node transforms; omits texture atlases, UVs,
animations and materials for the native monochrome holotable. No network use.
Usage: python tools/Convert-HoloModel.py source.glb Assets/Models/Rocinante.mesh.gz
"""
import gzip
import json
import math
from pathlib import Path
import struct
import sys


def convert(source, destination, max_triangles=None):
    raw = Path(source).read_bytes()
    magic, version, length = struct.unpack_from('<III', raw)
    if magic != 0x46546C67 or version != 2 or length != len(raw):
        raise ValueError('Expected a complete GLB 2.0 file')
    chunks, offset = {}, 12
    while offset < length:
        size, kind = struct.unpack_from('<II', raw, offset)
        chunks[kind] = raw[offset + 8:offset + 8 + size]
        offset += 8 + size
    doc = json.loads(chunks[0x4E4F534A])
    binary = chunks[0x004E4942]
    if doc.get('extensionsRequired'):
        raise ValueError('Unsupported required GLB extensions')
    identity = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]

    def multiply(a, b):
        return [sum(a[k * 4 + r] * b[c * 4 + k] for k in range(4)) for c in range(4) for r in range(4)]

    def local(node):
        if 'matrix' in node:
            return node['matrix']
        x, y, z, w = node.get('rotation', [0, 0, 0, 1])
        sx, sy, sz = node.get('scale', [1, 1, 1])
        tx, ty, tz = node.get('translation', [0, 0, 0])
        return [(1-2*(y*y+z*z))*sx, 2*(x*y+z*w)*sx, 2*(x*z-y*w)*sx, 0,
                2*(x*y-z*w)*sy, (1-2*(x*x+z*z))*sy, 2*(y*z+x*w)*sy, 0,
                2*(x*z+y*w)*sz, 2*(y*z-x*w)*sz, (1-2*(x*x+y*y))*sz, 0, tx, ty, tz, 1]

    def accessor(index):
        a = doc['accessors'][index]
        if 'sparse' in a or a.get('normalized'):
            raise ValueError('Unsupported sparse or normalized geometry')
        view = doc['bufferViews'][a['bufferView']]
        if view.get('buffer', 0) != 0:
            raise ValueError('External geometry is unsupported')
        components = {'SCALAR': 1, 'VEC3': 3}[a['type']]
        code = {5121: 'B', 5123: 'H', 5125: 'I', 5126: 'f'}[a['componentType']]
        fmt = '<' + code * components
        stride = view.get('byteStride', struct.calcsize(fmt))
        start = view.get('byteOffset', 0) + a.get('byteOffset', 0)
        return [struct.unpack_from(fmt, binary, start + i * stride) for i in range(a['count'])]

    points, triangles = [], []

    def visit(index, parent):
        node = doc['nodes'][index]
        matrix = multiply(parent, local(node))
        if 'skin' in node:
            raise ValueError('Skinned models need a static export')
        if 'mesh' in node:
            for primitive in doc['meshes'][node['mesh']]['primitives']:
                if primitive.get('mode', 4) != 4:
                    raise ValueError('Only triangle primitives are supported')
                positions = accessor(primitive['attributes']['POSITION'])
                start = len(points)
                for p in positions:
                    points.append(tuple(sum(matrix[k * 4 + r] * p[k] for k in range(3)) + matrix[12 + r] for r in range(3)))
                indices = [v[0] for v in accessor(primitive['indices'])] if 'indices' in primitive else list(range(len(positions)))
                if len(indices) % 3 or any(i >= len(positions) for i in indices):
                    raise ValueError('Invalid triangle indices')
                determinant = (matrix[0]*(matrix[5]*matrix[10]-matrix[9]*matrix[6])
                               - matrix[4]*(matrix[1]*matrix[10]-matrix[9]*matrix[2])
                               + matrix[8]*(matrix[1]*matrix[6]-matrix[5]*matrix[2]))
                for i in range(0, len(indices), 3):
                    a, b, c = indices[i:i+3]
                    triangles.extend([start+a, start+c, start+b] if determinant < 0 else [start+a, start+b, start+c])
        for child in node.get('children', []):
            visit(child, matrix)

    for root in doc['scenes'][doc.get('scene', 0)]['nodes']:
        visit(root, identity)
    if not points or len(triangles) > 6_000_000 or any(not math.isfinite(x) for p in points for x in p):
        raise ValueError('Empty, oversized or nonfinite geometry')
    lo = [min(p[i] for p in points) for i in range(3)]
    hi = [max(p[i] for p in points) for i in range(3)]
    scale = max(hi[i] - lo[i] for i in range(3))
    if scale <= 0:
        raise ValueError('Degenerate model')
    points = [tuple((p[i]-(lo[i]+hi[i])/2)*3.2/scale for i in range(3)) for p in points]
    original_triangles = len(triangles)//3
    if max_triangles and original_triangles > int(max_triangles):
        # Offline level of detail: collapse nearby original vertices and remove
        # collapsed/duplicate faces. No invented parts or sampled triangle holes.
        import numpy as np
        vertices = np.asarray(points, dtype=np.float32)
        faces = np.asarray(triangles, dtype=np.uint32).reshape(-1, 3)
        for cells in [768, 512, 384, 256, 192, 128, 96, 64, 48, 32]:
            _, first, inverse = np.unique(np.rint(vertices/(3.2/cells)).astype(np.int32), axis=0, return_index=True, return_inverse=True)
            reduced = inverse[faces]
            reduced = reduced[(reduced[:, 0] != reduced[:, 1]) & (reduced[:, 0] != reduced[:, 2]) & (reduced[:, 1] != reduced[:, 2])]
            _, keep = np.unique(np.sort(reduced, axis=1), axis=0, return_index=True)
            reduced = reduced[np.sort(keep)]
            if len(reduced) <= int(max_triangles):
                used, remap = np.unique(reduced, return_inverse=True)
                points = vertices[first[used]].tolist()
                triangles = remap.ravel().tolist()
                break
        else:
            raise ValueError('Could not meet the requested geometry budget')
    out = Path(destination)
    out.parent.mkdir(parents=True, exist_ok=True)
    with gzip.open(out, 'wb', compresslevel=9) as stream:
        stream.write(struct.pack('<4sII', b'HLM1', len(points), len(triangles)))
        for p in points:
            stream.write(struct.pack('<fff', *p))
        stream.write(struct.pack('<' + 'I' * len(triangles), *triangles))
    print(json.dumps({'output': str(out), 'vertices': len(points), 'triangles': len(triangles)//3,
                      'bytes': out.stat().st_size, 'sourceTriangles': original_triangles, 'originalBounds': [lo, hi]}))


if __name__ == '__main__':
    convert(*sys.argv[1:])
