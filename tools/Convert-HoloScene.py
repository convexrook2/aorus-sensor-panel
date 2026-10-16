"""Prepare native WPF ship assets from authorised local GLB downloads.

Preserves original base-colour textures, UVs, normals, material groups and
emissive colours. Omits shader maps unsupported by WPF. NumPy is build-time only.
Usage: python tools/Convert-HoloScene.py source.glb Assets/Models/Ship.ship 150000
"""
import io
import json
from pathlib import Path
import struct
import sys
import zipfile
import numpy as np


def convert(source, destination, budget='150000'):
    raw = Path(source).read_bytes()
    magic, version, length = struct.unpack_from('<III', raw)
    if magic != 0x46546C67 or version != 2 or length != len(raw):
        raise ValueError('Expected GLB 2.0')
    chunks, offset = {}, 12
    while offset < length:
        size, kind = struct.unpack_from('<II', raw, offset)
        chunks[kind] = raw[offset+8:offset+8+size]
        offset += 8+size
    doc, binary = json.loads(chunks[0x4E4F534A]), chunks[0x004E4942]
    if doc.get('extensionsRequired'):
        raise ValueError('Required extensions need explicit conversion support')

    def data(index):
        accessor = doc['accessors'][index]
        if 'sparse' in accessor:
            raise ValueError('Sparse geometry unsupported')
        view = doc['bufferViews'][accessor['bufferView']]
        width = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}[accessor['type']]
        kind = {5121: '<u1', 5123: '<u2', 5125: '<u4', 5126: '<f4'}[accessor['componentType']]
        dtype = np.dtype(kind)
        values = np.ndarray((accessor['count'], width), dtype=dtype, buffer=binary,
            offset=view.get('byteOffset', 0)+accessor.get('byteOffset', 0),
            strides=(view.get('byteStride', width*dtype.itemsize), dtype.itemsize)).copy()
        if accessor.get('normalized'):
            values = values.astype(np.float32)/np.iinfo(dtype).max
        return values

    def transform(node):
        if 'matrix' in node:
            return np.asarray(node['matrix']).reshape(4, 4).T
        x, y, z, w = node.get('rotation', [0, 0, 0, 1])
        matrix = np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w), 0],
                           [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w), 0],
                           [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y), 0], [0, 0, 0, 1]], dtype=float)
        matrix[:3, :3] *= np.array(node.get('scale', [1, 1, 1]))
        matrix[:3, 3] = node.get('translation', [0, 0, 0])
        return matrix

    groups = {}

    def visit(index, parent):
        node = doc['nodes'][index]
        matrix = parent @ transform(node)
        if 'skin' in node:
            raise ValueError('Skinned model requires a static export')
        if 'mesh' in node:
            for primitive in doc['meshes'][node['mesh']]['primitives']:
                if primitive.get('mode', 4) != 4:
                    raise ValueError('Expected triangle mesh')
                attrs = primitive['attributes']
                positions = data(attrs['POSITION']) @ matrix[:3, :3].T + matrix[:3, 3]
                normals = data(attrs['NORMAL']) @ np.linalg.inv(matrix[:3, :3]) if 'NORMAL' in attrs else np.zeros_like(positions)
                sizes = np.linalg.norm(normals, axis=1)
                normals /= np.maximum(sizes[:, None], 1e-15)
                uv = data(attrs['TEXCOORD_0']) if 'TEXCOORD_0' in attrs else np.zeros((len(positions), 2))
                faces = (data(primitive['indices']).ravel() if 'indices' in primitive else np.arange(len(positions))).reshape(-1, 3)
                if np.linalg.det(matrix[:3, :3]) < 0:
                    faces = faces[:, [0, 2, 1]]
                key = primitive.get('material', -1)
                groups.setdefault(key, []).append((np.hstack([positions, normals, uv]), faces))
        for child in node.get('children', []):
            visit(child, matrix)

    for root in doc['scenes'][doc.get('scene', 0)]['nodes']:
        visit(root, np.eye(4))
    joined = {}
    for key, items in groups.items():
        vertices, faces, start = [], [], 0
        for v, f in items:
            vertices.append(v); faces.append(f+start); start += len(v)
        joined[key] = (np.vstack(vertices), np.vstack(faces).astype(np.uint32))
    lo = np.min([v[:, :3].min(axis=0) for v, _ in joined.values()], axis=0)
    hi = np.max([v[:, :3].max(axis=0) for v, _ in joined.values()], axis=0)
    scale = np.max(hi-lo)
    if scale <= 0:
        raise ValueError('Degenerate model')
    for vertices, _ in joined.values():
        vertices[:, :3] = (vertices[:, :3]-(lo+hi)/2)*3.2/scale
        if not np.isfinite(vertices).all():
            raise ValueError('Nonfinite geometry')
    source_triangles = sum(len(f) for _, f in joined.values())
    if source_triangles > int(budget):
        for cells in [768, 512, 384, 256, 192, 128, 96, 64, 48, 32]:
            reduced = {}
            for key, (vertices, faces) in joined.items():
                # Cluster only within the same material and surface direction.
                # Retain a source vertex's original UV and normal at every merge.
                keys = np.hstack([np.rint(vertices[:, :3]/(3.2/cells)), np.rint(vertices[:, 3:6]*2)]).astype(np.int32)
                _, first, inverse = np.unique(keys, axis=0, return_index=True, return_inverse=True)
                mapped = inverse[faces]
                mapped = mapped[(mapped[:, 0]!=mapped[:, 1]) & (mapped[:, 0]!=mapped[:, 2]) & (mapped[:, 1]!=mapped[:, 2])]
                _, keep = np.unique(np.sort(mapped, axis=1), axis=0, return_index=True)
                mapped = mapped[np.sort(keep)]
                used, remap = np.unique(mapped, return_inverse=True)
                reduced[key] = (vertices[first[used]], remap.reshape(-1, 3).astype(np.uint32))
            if sum(len(f) for _, f in reduced.values()) <= int(budget):
                joined = reduced
                break
        else:
            raise ValueError('Could not meet geometry budget')

    textures, materials = {}, []
    material_indices = {}
    for key in joined:
        material = doc.get('materials', [])[key] if key >= 0 else {}
        pbr = material.get('pbrMetallicRoughness', {})
        texture = pbr.get('baseColorTexture')
        texture_name = None
        if texture:
            if texture.get('texCoord', 0) != 0 or texture.get('extensions'):
                raise ValueError('Texture transform or alternate UVs need explicit support')
            image_index = doc['textures'][texture['index']]['source']
            image = doc['images'][image_index]
            view = doc['bufferViews'][image['bufferView']]
            extension = {'image/png': 'png', 'image/jpeg': 'jpg'}[image['mimeType']]
            texture_name = f'texture-{image_index}.{extension}'
            textures[texture_name] = binary[view.get('byteOffset', 0):view.get('byteOffset', 0)+view['byteLength']]
            if pbr.get('baseColorFactor', [1, 1, 1, 1]) != [1, 1, 1, 1]:
                raise ValueError('Textured colour factor needs explicit baking')
        material_indices[key] = len(materials)
        emission_texture = None
        if material.get('emissiveTexture'):
            emission = material['emissiveTexture']
            if emission.get('texCoord', 0) != 0 or emission.get('extensions'):
                raise ValueError('Emissive texture transforms need explicit support')
            image_index = doc['textures'][emission['index']]['source']
            image = doc['images'][image_index]
            view = doc['bufferViews'][image['bufferView']]
            extension = {'image/png': 'png', 'image/jpeg': 'jpg'}[image['mimeType']]
            emission_texture = f'texture-{image_index}.{extension}'
            textures[emission_texture] = binary[view.get('byteOffset', 0):view.get('byteOffset', 0)+view['byteLength']]
        materials.append({'name': material.get('name', 'Default'), 'texture': texture_name,
            'colour': pbr.get('baseColorFactor', [1, 1, 1, 1]), 'emissive': material.get('emissiveFactor', [0, 0, 0]),
            'emissiveTexture': emission_texture,
            'doubleSided': material.get('doubleSided', False), 'alpha': material.get('alphaMode', 'OPAQUE')})
    mesh = io.BytesIO()
    parts = []
    for key, (vertices, faces) in joined.items():
        # Keep each native WPF mesh below 20k vertices / 60k indices.
        for start in range(0, len(faces), 6000):
            selected = faces[start:start+6000]
            used, remap = np.unique(selected, return_inverse=True)
            points = vertices[used].astype('<f4')
            indices = remap.ravel().astype('<u4')
            parts.append((material_indices[key], points, indices))
    mesh.write(struct.pack('<4sI', b'HSC1', len(parts)))
    for material, points, indices in parts:
        mesh.write(struct.pack('<III', material, len(points), len(indices)))
        mesh.write(points.tobytes()); mesh.write(indices.tobytes())
    metadata = {'format': 1, 'sourceTriangles': source_triangles, 'triangles': sum(len(f) for _, f in joined.values()),
                'materials': materials, 'parts': len(parts)}
    out = Path(destination)
    out.parent.mkdir(parents=True, exist_ok=True)
    temporary = out.with_suffix(out.suffix+'.tmp')
    with zipfile.ZipFile(temporary, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        archive.writestr('scene.json', json.dumps(metadata))
        archive.writestr('mesh.bin', mesh.getvalue())
        for name, texture in textures.items():
            archive.writestr(name, texture)
    temporary.replace(out)
    print(json.dumps({'output': str(out), 'bytes': out.stat().st_size, **metadata}))


if __name__ == '__main__':
    convert(*sys.argv[1:])
