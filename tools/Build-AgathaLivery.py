"""Create a separate Agatha King V03 colour study from the verified Truman GLB.

Only the base-colour image changes. Original buffer bytes, geometry, UVs,
normals, other maps and material extensions remain intact. No production
image pixels, new geometry, stencil text or speculative emblem are used.
Requires NumPy and Pillow. Refuses existing outputs and an unfamiliar source.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import io
import json
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

SOURCE_SHA = 'ab2b2f691a76391e675ca6564369b973767532860a449c861516ffe375d63697'
REFERENCE = 'https://leefitzgerald.artstation.com/projects/X1rzxL'
SIZE = 4096


def sha(data):
    return hashlib.sha256(data).hexdigest()


def read_glb(raw):
    magic, version, length = struct.unpack_from('<III', raw)
    if (magic, version, length) != (0x46546C67, 2, len(raw)):
        raise ValueError('Invalid GLB header')
    chunks = []
    offset = 12
    while offset < len(raw):
        size, kind = struct.unpack_from('<II', raw, offset)
        end = offset + 8 + size
        if size % 4 or end > len(raw):
            raise ValueError('Invalid GLB chunk')
        chunks.append((kind, raw[offset + 8:end]))
        offset = end
    if [kind for kind, _ in chunks] != [0x4E4F534A, 0x004E4942]:
        raise ValueError('Expected one embedded JSON chunk and one BIN chunk')
    return json.loads(chunks[0][1]), chunks[1][1]


def accessor(doc, binary, index):
    item = doc['accessors'][index]
    view = doc['bufferViews'][item['bufferView']]
    if view.get('buffer', 0) != 0 or 'sparse' in item:
        raise ValueError('Unexpected accessor storage')
    count = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3}[item['type']]
    dtype = {5125: '<u4', 5126: '<f4'}[item['componentType']]
    return np.ndarray((item['count'], count), dtype=dtype, buffer=binary,
                      offset=view.get('byteOffset', 0) + item.get('byteOffset', 0),
                      strides=(view.get('byteStride', count * 4), 4))


def image_bytes(doc, binary, index):
    item = doc['images'][index]
    view = doc['bufferViews'][item['bufferView']]
    offset = view.get('byteOffset', 0)
    return binary[offset:offset + view['byteLength']]


def region_masks(doc, binary):
    """Project identified source-space surfaces into their existing UV islands.

    +Y is the source bow. Blue is limited to the paired sloping cap faces,
    not the bow wings or gun barrels. Ochre is restricted to existing narrow
    forward-body joint strips. These masks do not move or rewrite any UVs.
    """
    selected = [Image.new('L', (SIZE, SIZE)) for _ in range(2)]
    other = [Image.new('L', (SIZE, SIZE)) for _ in range(2)]
    draws = [ImageDraw.Draw(im) for im in selected]
    others = [ImageDraw.Draw(im) for im in other]
    counts = [0, 0]
    for mesh in doc['meshes']:
        for primitive in mesh['primitives']:
            vertices = accessor(doc, binary, primitive['attributes']['POSITION'])
            uv = accessor(doc, binary, primitive['attributes']['TEXCOORD_0'])
            faces = accessor(doc, binary, primitive['indices']).reshape(-1, 3)
            triangles = vertices[faces]
            centres = triangles.mean(axis=1)
            normals = np.cross(triangles[:, 1] - triangles[:, 0],
                               triangles[:, 2] - triangles[:, 0])
            normals /= np.maximum(np.linalg.norm(normals, axis=1)[:, None], 1e-15)
            x, y, z = centres.T
            blue = ((np.abs(x) < .19) & (y > .62) & (y < .68) & (z > .20)
                    & (normals[:, 1] > .93) & (normals[:, 1] < .96)
                    & (normals[:, 2] > .30) & (normals[:, 2] < .36))
            joint = (((y > .231) & (y < .245)) | ((y > -.205) & (y < -.194)))
            gold = (joint & (np.abs(x) < .25) & (np.abs(z) < .245)
                    & (np.abs(normals[:, 1]) < .4)
                    & (np.ptp(triangles[:, :, 1], axis=1) < .013))
            pixels = uv[faces] * SIZE  # glTF V=0 is the image's top edge.
            for index, choices in enumerate((blue, gold)):
                counts[index] += int(choices.sum())
                for polygon, chosen in zip(pixels, choices):
                    points = [tuple(point) for point in polygon]
                    (draws[index] if chosen else others[index]).polygon(points, fill=255)
    result, report = [], {}
    for index, name in enumerate(('blue_forward_caps', 'ochre_joint_edges')):
        target = np.asarray(selected[index]) > 0
        # Reject UV overlap with unrelated surfaces; shared mirrored cap faces
        # are already in the selected set. Ignore raster boundary overlap.
        unrelated = np.asarray(other[index].filter(ImageFilter.MinFilter(3))) > 0
        conflicts = target & unrelated
        safe = target & ~unrelated
        if counts[index] == 0 or int(safe.sum()) < 20:
            raise ValueError('Could not safely identify the ' + name + ' UV region')
        expanded = Image.fromarray(safe.astype('uint8') * 255).filter(ImageFilter.MaxFilter(3))
        mask = np.asarray(expanded, dtype=np.float32) / 255
        mask[unrelated] = 0
        result.append(mask)
        report[name] = {'source_triangles': counts[index], 'painted_texels': int(safe.sum()),
                        'rejected_overlap_texels': int(conflicts.sum())}
    return result, report


def paint(original, masks):
    # Interpret existing pigment regions instead of flattening every material.
    # Source ochre becomes pale armour; brown becomes restrained charcoal.
    rgb = np.asarray(original.convert('RGB'), dtype=np.float32) / 255
    # Decide coating from low-frequency pigment, not JPEG grain or individual
    # worn texels; otherwise neutral scratches become false white speckles.
    coating = np.asarray(original.convert('RGB').filter(ImageFilter.GaussianBlur(1.6)),
                         dtype=np.float32) / 255
    red, green, blue = coating.transpose(2, 0, 1)
    luminance = rgb @ np.array([.2126, .7152, .0722], dtype=np.float32)
    yellow_ratio = (green - blue) / np.maximum(red - blue, .015)
    pigment = np.clip((red - blue - .025) / .07, 0, 1)
    armour = np.clip((yellow_ratio - .43) / .23, 0, 1) * pigment
    shade = (luminance * 255 - 65)[..., None]
    dark = np.array([62, 72, 80], dtype=np.float32) + shade * .40
    pale = np.array([194, 201, 205], dtype=np.float32) + shade * .28
    recoloured = dark * (1 - armour[..., None]) + pale * armour[..., None]
    recoloured = Image.fromarray(np.clip(recoloured, 0, 255).astype('uint8'))
    recoloured = np.asarray(recoloured.resize((SIZE, SIZE), Image.Resampling.LANCZOS),
                            dtype=np.float32).copy()
    # The original map is 1024 square; new 4096 masks improve colour-edge
    # precision but do not claim newly recovered source detail.
    detail = Image.fromarray(np.clip(luminance * 255, 0, 255).astype('uint8'))
    detail = np.asarray(detail.resize((SIZE, SIZE), Image.Resampling.BILINEAR),
                        dtype=np.float32)
    for mask, colour in zip(masks, ([72, 107, 135], [164, 143, 91])):
        finish = np.asarray(colour, dtype=np.float32) + (detail[..., None] - 65) * .18
        recoloured = recoloured * (1 - mask[..., None]) + finish * mask[..., None]
    output = io.BytesIO()
    Image.fromarray(np.clip(recoloured, 0, 255).astype('uint8')).save(output, format='PNG')
    return output.getvalue()


def encode(doc, binary):
    text = json.dumps(doc, separators=(',', ':'), ensure_ascii=False).encode('utf-8')
    text += b' ' * (-len(text) % 4)
    binary += b'\0' * (-len(binary) % 4)
    size = 12 + 8 + len(text) + 8 + len(binary)
    return (struct.pack('<III', 0x46546C67, 2, size)
            + struct.pack('<II', len(text), 0x4E4F534A) + text
            + struct.pack('<II', len(binary), 0x004E4942) + binary)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    source = args.input.resolve(strict=True)
    output = args.output.resolve()
    receipt_path = output.with_suffix('.receipt.json')
    if output.suffix.lower() != '.glb' or output.exists() or receipt_path.exists() or output == source:
        raise ValueError('Use a new .glb output and a new adjacent .receipt.json')
    raw = source.read_bytes()
    if sha(raw) != SOURCE_SHA:
        raise ValueError('Source differs from the verified, inspected Truman asset')
    original, binary = read_glb(raw)
    doc = copy.deepcopy(original)
    if len(doc['buffers']) != 1 or 'uri' in doc['buffers'][0]:
        raise ValueError('Expected one embedded buffer')
    colour_texture = doc['materials'][0]['pbrMetallicRoughness']['baseColorTexture']['index']
    colour_image = doc['textures'][colour_texture]['source']
    masks, regions = region_masks(doc, binary)
    image = Image.open(io.BytesIO(image_bytes(doc, binary, colour_image)))
    painted = paint(image, masks)
    doc['bufferViews'].append({'buffer': 0, 'byteOffset': len(binary), 'byteLength': len(painted)})
    doc['images'][colour_image] = {'bufferView': len(doc['bufferViews']) - 1,
                                  'mimeType': 'image/png', 'name': 'AgathaKing_V03_authored_colour'}
    appended = binary + painted
    doc['buffers'][0]['byteLength'] = len(appended)
    result = encode(doc, appended)
    checked, checked_binary = read_glb(result)
    restored = copy.deepcopy(checked)
    restored['images'] = original['images']
    restored['bufferViews'] = original['bufferViews']
    restored['buffers'] = original['buffers']
    if restored != original or checked_binary[:len(binary)] != binary:
        raise ValueError('Unexpected geometry, map, material or extension change')
    preserved = {str(i): sha(image_bytes(checked, checked_binary, i))
                 for i in range(len(original['images'])) if i != colour_image}
    if any(preserved[str(i)] != sha(image_bytes(original, binary, i))
           for i in range(len(original['images'])) if i != colour_image):
        raise ValueError('A protected image changed')
    if sha(source.read_bytes()) != SOURCE_SHA:
        raise ValueError('Input changed during generation; output not written')
    receipt = dict(source=str(source), source_sha256=SOURCE_SHA, output=str(output),
                   output_sha256=sha(result), script_sha256=sha(Path(__file__).read_bytes()),
                   reference=REFERENCE, reference_revision='Exterior V03, 2017-06-23',
                   credit='Truman class by mohamedhussien, CC BY 4.0; authored Agatha colour study.',
                   scope='Separate livery study only; original input and native panel unchanged.',
                   changed=['Base-colour image only'], original_binary_prefix_sha256=sha(binary),
                   unchanged_images_sha256=preserved, geometry_accessors_materials_extensions_unchanged=True,
                   texture_size=[SIZE, SIZE], region_masks=regions,
                   confidence={'palette': 'High: both artist V03 views inspected.',
                               'cap_placement': 'Source-space sloping cap surfaces projected into existing UVs.',
                               'joint_trim': 'Interpretation confined to existing narrow body-joint strips.'},
                   limitations=['Not a production texture or a certified 1:1 reconstruction.',
                                'Original 1024 detail is retained; 4096 improves authored mask edges only.',
                                'UN emblem and small stencils omitted: exact placement not verified.',
                                'Emissive texture and strength unchanged; no new drive glow.'])
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open('xb') as handle:
        handle.write(result)
    with receipt_path.open('x', encoding='utf-8') as handle:
        json.dump(receipt, handle, indent=2)
    print(json.dumps({'output': str(output), 'receipt': str(receipt_path),
                      'sha256': sha(result), 'regions': regions}, indent=2))


if __name__ == '__main__':
    main()
