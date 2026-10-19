"""Pella: Martin Taichl's CC BY 4.0 mesh with authored charcoal materials.

Geometry credit/licence: Assets/Models/Source/Fleet/Pella-Martin-LICENSE.txt.
Production-inspired materials and textures are authored here; no production
image pixels are used. The quiet panel finish is an interpretation, not the
production UV layout. +Y forward, +Z dorsal. Uniform model scale, not certified
physical dimensions. This is a fan model, not the production asset.
"""
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
import trimesh
from trimesh.visual.material import PBRMaterial
from trimesh.visual.texture import TextureVisuals
from PellaMesh import load_pella_meshes


def build_pella(Hull):
    h = Hull('Pella', (53, 61, 67), (154, 139, 79))
    h.materials.clear()
    # UV seams duplicate vertices, not shading boundaries. Hull.export's
    # guarded opt-in retains the same smooth normal on either side of a seam.
    h.preserve_authored_normals = True

    def linear(color):
        c = np.asarray(color) / 255.
        return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)

    def surface_maps(color, seed, roughness, metallic):
        """Quiet long plate courses, coating variation and shallow seam normals.

        These are original surface finishes, not a geometry/AO bake. The packed
        map reserves R=1 for future baked occlusion; G is roughness and B is
        metallic. Its factors must stay at one so maps are not multiplied twice.
        """
        size = 1024  # Also the native panel's texture decode resolution.
        rng = np.random.default_rng(seed)
        colour = Image.new('RGB', (size, size), color)
        packed = Image.new('RGB', (size, size),
                           (255, round(255 * roughness), round(255 * metallic)))
        height = Image.new('L', (size, size), 128)
        dc, dp, dh = ImageDraw.Draw(colour), ImageDraw.Draw(packed), ImageDraw.Draw(height)

        def tint(delta):
            return tuple(int(np.clip(v + delta, 0, 255)) for v in color)

        # Unequal courses and mostly long panels avoid the former regular
        # checker/brick pattern. Native geometry supplies the large armour steps.
        courses = [0, 91, 205, 302, 431, 540, 658, 758, 890, size]
        for row, (top, bottom) in enumerate(zip(courses[:-1], courses[1:])):
            left = -int(rng.integers(20, 170))
            while left < size:
                right = min(size + 40, left + int(rng.integers(190, 380)))
                delta = int(rng.choice([-2, -1, 0, 0, 0, 1, 2]))
                r = float(np.clip(roughness + rng.uniform(-.028, .035), .1, .95))
                dc.rectangle((left, top, right, bottom), fill=tint(delta))
                dp.rectangle((left, top, right, bottom),
                             fill=(255, round(r * 255), round(metallic * 255)))
                # Fine dark gaskets, not bright outlines around every plate.
                dc.line((left, top, right, top), fill=tint(-6), width=1)
                dh.line((left, top, right, top), fill=125, width=1)
                if 0 < left < size:
                    dc.line((left, top + 2, left, bottom - 2), fill=tint(-4), width=1)
                    dh.line((left, top + 2, left, bottom - 2), fill=126, width=1)

                # Local access plates appear on a minority of panels. They are
                # smaller than the physical hatches already present in the mesh.
                if rng.random() < .2 and right - left > 240:
                    x = max(4, left + int((right - left) * .64))
                    y = top + int((bottom - top) * .55)
                    w, ht = int(rng.integers(28, 49)), int(rng.integers(14, 25))
                    dc.rectangle((x, y, x + w, y + ht), outline=tint(-7), width=1)
                    dh.rectangle((x, y, x + w, y + ht), outline=125, width=1)
                    dp.rectangle((x + 1, y + 1, x + w - 1, y + ht - 1),
                                 fill=(255, round(min(.95, r + .04) * 255), round(metallic * 255)))

                # A few short rubbed edges alter coating response as well as
                # colour. No ungrounded full-hull speckles, decals or insignia.
                if row not in (0, len(courses) - 2) and rng.random() < .13:
                    x = max(4, left + 22)
                    end = min(size - 4, x + int(rng.integers(7, 19)))
                    if end > x:
                        dc.line((x, top + 2, end, top + 2), fill=tint(9), width=1)
                        dp.line((x, top + 2, end, top + 2), fill=(255, 104, 168), width=1)
                left = right

        # Low-frequency coating variation is chiefly in roughness. A quiet
        # albedo variation avoids the uniformly noisy/chalky former finish.
        cloud = Image.fromarray(np.uint8(np.clip(rng.normal(128, 22, (24, 24)), 0, 255)))
        cloud = cloud.resize((size, size), Image.Resampling.BICUBIC).filter(ImageFilter.GaussianBlur(8))
        variation = (np.asarray(cloud, dtype=np.float32) - 128) / 32
        pixels = np.asarray(colour, dtype=np.float32).copy()
        pixels += variation[:, :, None] * .75
        colour = Image.fromarray(np.uint8(np.clip(np.rint(pixels), 0, 255)))
        response = np.asarray(packed, dtype=np.float32).copy()
        response[:, :, 1] += variation * 5
        packed = Image.fromarray(np.uint8(np.clip(np.rint(response), 0, 255)))

        # Both exported image coordinates increase toward their array indices.
        # Encode -dH/du and -dH/dv for the exported UV tangent basis; the glTF
        # exporter performs its usual V-coordinate conversion. Major bevels and
        # cavity relief remain geometry, not this deliberately shallow finish.
        relief = np.asarray(height.filter(ImageFilter.GaussianBlur(.55)), dtype=np.float32) / 255
        dy, dx = np.gradient(relief)
        normal = np.dstack((-dx * 5, -dy * 5, np.ones_like(dx)))
        normal /= np.linalg.norm(normal, axis=2, keepdims=True)
        normal = Image.fromarray(np.uint8(np.clip(np.rint((normal * .5 + .5) * 255), 0, 255)))
        return colour, packed, normal

    def material(key, color, metal=.35, rough=.6, texture=False, glow=None):
        maps = surface_maps(color, sum(map(ord, key)) + 32617, rough, metal) if texture else None
        h.materials[key] = PBRMaterial(
            name=key, baseColorFactor=np.ones(4) if texture else np.r_[linear(color), 1],
            metallicFactor=1.0 if texture else metal,
            roughnessFactor=1.0 if texture else rough,
            baseColorTexture=maps[0] if maps else None,
            metallicRoughnessTexture=maps[1] if maps else None,
            normalTexture=maps[2] if maps else None,
            emissiveFactor=np.asarray(glow or (0, 0, 0)))

    # Painted armour is a dielectric coating; exposed brackets, gun fittings,
    # collars and bell surfaces provide the contrasting metal response.
    material('paint', (47, 52, 57), .04, .65, True)
    material('armor', (59, 64, 68), .07, .59, True)
    material('black', (27, 31, 35), .02, .74, True)
    material('frame', (77, 82, 85), .82, .45)
    material('machinery', (48, 53, 57), .62, .56)
    material('silver', (111, 116, 118), .86, .38)
    material('bell', (43, 46, 47), .78, .54)
    material('collar', (106, 111, 113), .82, .38)
    material('lamp', (205, 220, 220), 0, .5, glow=(.65, .72, .72))
    material('blue', (48, 126, 193), .1, .5, glow=(.04, .23, .55))

    components = load_pella_meshes(target_faces=140000)
    source_lo = np.array([-746.1000366210938, -183.6095733642578, -183.6095733642578])
    source_hi = np.array([338.4079895019531, 183.6095733642578, 183.6095733642578])
    source_center = (source_lo + source_hi) / 2
    scale = 3.2 / (source_hi[0] - source_lo[0])

    def transformed(vertices):
        v = (np.asarray(vertices) - source_center) * scale
        return np.column_stack((-v[:, 2], -v[:, 0], v[:, 1]))

    def source_coordinates(vertices):
        p = np.asarray(vertices) / scale
        return np.column_stack((-p[:, 1], p[:, 2], -p[:, 0])) + source_center

    # An original vector interpretation of the pale bird visible in production
    # turntable-039 and production-exact-08s. It is painted on ONE forward flank,
    # not placed on an opaque floating decal or copied around the four sides.
    # The atlas is otherwise identical to paint, including its existing seams.
    bird_center = np.array([-575., 94., -82.089])
    bird_across = np.array([0., .81221, .58337])
    bird_across /= np.linalg.norm(bird_across)
    bird_normal = np.array([0., bird_across[2], -bird_across[1]])
    bird_forward = np.array([-1., 0., 0.])
    bird_mask = Image.new('L', (4096, 4096), 0)
    bird_draw = ImageDraw.Draw(bird_mask)

    def bird_polygon(points):
        points = np.asarray(points, dtype=float)
        source = (bird_center + points[:, :1] * bird_across
                  + points[:, 1:] * bird_forward)
        p = transformed(source)
        # Positive-X projection chart, with the exporter's V conversion.
        uv = np.column_stack((.5 - p[:, 2] / 3.5, .5 + p[:, 1] / 3.5))
        pixels = np.column_stack((uv[:, 0], 1 - uv[:, 1])) * 4096
        bird_draw.polygon([tuple(q) for q in pixels], fill=255)

    def bird_path(start, curves):
        # Original quadratic outlines, with pointed feather tips and curved
        # feather roots. Artwork -X faces the bow; its two fans are deliberately
        # unequal. This creates the reference's open, curling bird silhouette
        # rather than two mirrored upright wings around a long straight tail.
        current = np.asarray(start, dtype=float)
        points = [current]
        for control, end in curves:
            control, end = np.asarray(control, dtype=float), np.asarray(end, dtype=float)
            for t in np.linspace(0, 1, 10)[1:]:
                points.append((1 - t) ** 2 * current + 2 * t * (1 - t) * control + t ** 2 * end)
            current = end
        bird_polygon([(.88 * y, -.88 * x) for x, y in points])

    # Higher fan curls around the back of the neck. Successive long feathers
    # turn around the perimeter instead of forming a straight sawtooth wing.
    bird_path((5, -13), [
        ((20, -10), (26, -4)), ((31, 1), (32, 9)),
        ((28, 8), (23, 6)), ((30, 14), (29, 21)),
        ((25, 20), (20, 16)), ((24, 26), (21, 31)),
        ((17, 28), (15, 22)), ((16, 33), (10, 36)),
        ((8, 31), (7, 25)), ((5, 37), (-2, 38)),
        ((-3, 31), (-2, 25)), ((-8, 35), (-16, 34)),
        ((-14, 28), (-9, 22)), ((-19, 29), (-29, 26)),
        ((-24, 20), (-16, 17)), ((-3, 14), (5, 8)),
        ((11, 1), (8, -6)), ((6, -10), (5, -13))])

    # The lower fan is broader at its leading tips, with shorter trailing
    # feathers. Its upper edge leaves a large open notch ahead of the body.
    bird_path((5, 1), [
        ((8, -3), (11, -11)), ((17, -10), (25, -13)),
        ((22, -18), (16, -18)), ((22, -19), (23, -24)),
        ((16, -26), (11, -22)), ((15, -29), (12, -33)),
        ((6, -30), (5, -25)), ((5, -33), (-1, -35)),
        ((-5, -30), (-4, -25)), ((-9, -32), (-16, -31)),
        ((-16, -26), (-12, -22)), ((-20, -26), (-26, -21)),
        ((-22, -18), (-17, -16)), ((-28, -18), (-33, -11)),
        ((-24, -9), (-17, -9)), ((-27, -7), (-30, 0)),
        ((-18, -1), (-11, -4)), ((-2, -9), (5, 1))])

    # A bent neck and short hooked profile beak remain separate from the wing
    # tips. The negative space above and below the beak is part of the outline;
    # there is no central butterfly-like oval or long symmetrical tail.
    bird_path((8, -15), [
        ((9, -6), (4, 1)), ((1, 3), (-2, 6)),
        ((-5, 12), (-11, 11)), ((-14, 11), (-15, 8)),
        ((-17, 7), (-22, 7)), ((-21, 4), (-18, 3)),
        ((-18, 5), (-15, 5)), ((-12, 3), (-11, -1)),
        ((-7, -7), (0, -9)), ((5, -11), (8, -15))])
    bird_mask = bird_mask.resize((1024, 1024), Image.Resampling.LANCZOS)
    original_paint = h.materials['paint']
    marked_colour = original_paint.baseColorTexture.copy()
    marked_colour.paste((153, 156, 151), (0, 0, 1024, 1024), bird_mask)
    marked_response = original_paint.metallicRoughnessTexture.copy()
    marked_response.paste((255, 184, 0), (0, 0, 1024, 1024), bird_mask)
    h.materials['paint_marked'] = PBRMaterial(
        name='paint_marked', baseColorFactor=np.ones(4),
        metallicFactor=1., roughnessFactor=1.,
        baseColorTexture=marked_colour, metallicRoughnessTexture=marked_response,
        normalTexture=original_paint.normalTexture, emissiveFactor=np.zeros(3))

    def add(mesh, key, component_index=None):
        # Calculate smooth normals before UV-only splitting. The source helper
        # has already split actual hard edges, so rounded bells retain their
        # smooth normals while the broad armour planes keep their sharp breaks.
        p = np.asarray(mesh.vertices)
        faces = np.asarray(mesh.faces).copy()
        triangles = p[faces]
        area = np.cross(triangles[:, 1]-triangles[:, 0], triangles[:, 2]-triangles[:, 0])
        normals = np.zeros_like(p)
        for corner in range(3):
            np.add.at(normals, faces[:, corner], area)
        lengths = np.linalg.norm(normals, axis=1)
        if not np.isfinite(normals).all() or np.any(lengths <= np.finfo(float).tiny):
            raise ValueError('Pella component contains an invalid shading normal')
        normals /= lengths[:, None]

        face_lengths = np.linalg.norm(area, axis=1)
        if np.any(face_lengths <= np.finfo(float).tiny):
            raise ValueError('Pella component contains a degenerate face')
        face_normals = area / face_lengths[:, None]
        source_triangles = source_coordinates(triangles.reshape(-1, 3)).reshape(-1, 3, 3)
        source_centroids = source_triangles.mean(axis=1)
        corrected = np.zeros(len(faces), dtype=bool)
        if component_index in (8, 9, 10, 11):
            # The measured shoulder junction inherited a nearly 40-degree
            # corner-normal error from the print mesh. Flatten only outlying
            # bevel triangles in that small repeated junction. Positions,
            # topology elsewhere, round engines and other normals stay intact.
            radial = (np.abs(source_centroids[:, 1]) + np.abs(source_centroids[:, 2])) / math.sqrt(2)
            tangent = (np.abs(source_centroids[:, 1]) - np.abs(source_centroids[:, 2])) / math.sqrt(2)
            agreement = np.einsum('fci,fi->fc', normals[faces], face_normals).min(axis=1)
            corrected = ((source_centroids[:, 0] >= 20) & (source_centroids[:, 0] <= 55)
                         & (radial >= 125) & (radial <= 175) & (np.abs(tangent) < 35)
                         & (agreement < math.cos(math.radians(25))))
            count = int(corrected.sum())
            if count:
                first = len(p)
                p = np.vstack((p, triangles[corrected].reshape(-1, 3)))
                normals = np.vstack((normals, np.repeat(face_normals[corrected], 3, axis=0)))
                faces[corrected] = np.arange(first, first + count * 3).reshape(-1, 3)

        marked = np.zeros(len(faces), dtype=bool)
        if component_index == 0:
            relative = source_triangles - bird_center
            along = relative @ bird_forward
            across = relative @ bird_across
            distance = (source_centroids - bird_center) @ bird_normal
            native_normal = np.array([-bird_normal[2], -bird_normal[0], bird_normal[1]])
            marked = ((face_normals @ native_normal > .985) & (np.abs(distance) < 2.5)
                      & (along.min(axis=1) <= 42) & (along.max(axis=1) >= -42)
                      & (across.min(axis=1) <= 35) & (across.max(axis=1) >= -35))
            if not marked.any():
                raise ValueError('Pella bird has no matching forward-flank faces')

        if h.materials[key].baseColorTexture is not None:
            # A face receives one signed projection chart. Duplicate only its
            # shared UV-seam vertices, rather than choosing a different axis at
            # each corner (which stretches or folds UVs within a triangle).
            axis = np.argmax(np.abs(area), axis=1)
            negative = area[np.arange(len(faces)), axis] < 0
            chart = axis * 2 + negative.astype(np.int64)
            keys = np.column_stack((faces.ravel(), np.repeat(chart, 3)))
            unique, inverse = np.unique(keys, axis=0, return_inverse=True)
            old_vertices, charts = unique[:, 0], unique[:, 1]
            vertices = p[old_vertices]
            projection = charts // 2
            u = np.where(projection == 0, -vertices[:, 2], vertices[:, 0])
            v = np.where(projection == 1, -vertices[:, 2], vertices[:, 1])
            u = np.where(charts % 2, -u, u)
            uv = np.column_stack((.5 + u / 3.5, .5 + v / 3.5))
            prepared = trimesh.Trimesh(vertices=vertices, faces=inverse.reshape(-1, 3), process=False)
            prepared.vertex_normals = normals[old_vertices]
        else:
            prepared = mesh.copy()
            prepared.vertex_normals = normals
            uv = np.zeros((len(p), 2))
        prepared.metadata.update(mesh.metadata)
        prepared.metadata['shoulder_flattened_triangles'] = int(corrected.sum())
        if marked.any():
            # Material assignment is per face, after normals and UVs have been
            # authored for the complete component. Do not recompute normals at
            # the new material boundary or duplicate any triangle.
            for destination, chosen in ((key, ~marked), ('paint_marked', marked)):
                if not chosen.any():
                    continue
                used, inverse = np.unique(np.asarray(prepared.faces)[chosen], return_inverse=True)
                part = trimesh.Trimesh(vertices=np.asarray(prepared.vertices)[used],
                                       faces=inverse.reshape(-1, 3), process=False)
                part.vertex_normals = np.asarray(prepared.vertex_normals)[used]
                part.metadata.update(prepared.metadata)
                part.visual = TextureVisuals(uv=uv[used], material=h.materials[destination])
                h.parts[destination].append(part)
        else:
            prepared.visual = TextureVisuals(uv=uv, material=h.materials[key])
            h.parts[key].append(prepared)

    for index, mesh in components:
        if index == 0 or 8 <= index <= 11:
            key = 'paint'
        elif 3 <= index <= 6:
            key = 'bell'
        elif 18 <= index <= 21:
            key = 'collar'
        elif index in (7, 15, 16, 17) or 41 <= index <= 49:
            key = 'armor'
        elif index in (12, 13, 22):
            key = 'black'
        elif index in (1, 2, 14) or 23 <= index <= 26:
            key = 'frame'
        elif 27 <= index <= 35 or 37 <= index <= 40 or 52 <= index <= 65:
            key = 'silver'
        else:
            key = 'machinery'
        m = mesh.copy()
        m.vertices = transformed(m.vertices)
        add(m, key, index)

    def add_service_lamp(mesh, longitudinal, radial=220.):
        """Project a small service-edge lamp onto component 8's actual surface.

        Production-7 establishes this sparse edge-light family. Longitudinal
        placement is an interpretation; it is not replicated onto unseen pods.
        """
        source = np.asarray(mesh.vertices)
        local = np.column_stack((source[:, 0], (source[:, 1] - source[:, 2]) / math.sqrt(2),
                                 (source[:, 1] + source[:, 2]) / math.sqrt(2)))
        triangles = local[np.asarray(mesh.faces)]
        a = triangles[:, 0, :2]
        ab = triangles[:, 1, :2] - a
        ac = triangles[:, 2, :2] - a
        d = np.array([longitudinal, radial]) - a
        determinant = ab[:, 0] * ac[:, 1] - ab[:, 1] * ac[:, 0]
        valid = np.abs(determinant) > 1e-10
        b = np.zeros(len(triangles))
        c = np.zeros(len(triangles))
        b[valid] = (d[valid, 0] * ac[valid, 1] - d[valid, 1] * ac[valid, 0]) / determinant[valid]
        c[valid] = (ab[valid, 0] * d[valid, 1] - ab[valid, 1] * d[valid, 0]) / determinant[valid]
        hits = np.flatnonzero(valid & (b >= -1e-7) & (c >= -1e-7) & (b + c <= 1 + 1e-7))
        if not len(hits):
            raise ValueError('Pella service lamp does not intersect its measured pod surface')
        tangent = (triangles[hits, 0, 2] * (1 - b[hits] - c[hits])
                   + triangles[hits, 1, 2] * b[hits] + triangles[hits, 2, 2] * c[hits])
        face = hits[np.argmax(tangent)]
        t = float(tangent.max())
        center = np.array([longitudinal, (radial + t) / math.sqrt(2), (-radial + t) / math.sqrt(2)])
        actual = source[np.asarray(mesh.faces)[face]]
        n = np.cross(actual[1] - actual[0], actual[2] - actual[0])
        n /= np.linalg.norm(n)
        if n @ np.array([0., 1., 1.]) < 0:
            n = -n
        long = np.array([1., 0., 0.]) - n * n[0]
        long /= np.linalg.norm(long)
        across = np.cross(n, long)
        # Two flush, shallow quads: a dark bezel and the smaller white lens.
        # No scene lights, exhaust cone, or large glowing strips are introduced.
        for length, width, offset, key in ((8., 3.4, .18, 'machinery'), (6., 1.7, .32, 'lamp')):
            corners = np.array([center + n * offset + long * x * length / 2 + across * y * width / 2
                                for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
            lamp = trimesh.Trimesh(vertices=transformed(corners), faces=[[0, 1, 2], [0, 2, 3]], process=False)
            lamp.metadata['source_service_lamp_center'] = center.tolist()
            add(lamp, key)

    service_pod = next(mesh for index, mesh in components if index == 8)
    for longitudinal in (0., 120.):
        add_service_lamp(service_pod, longitudinal)

    # Recessed drive throats remain optional in the review viewer; physical
    # nozzles and collars come from the credited mesh. No long exhaust plumes.
    for y in (-120.33475875854492, 120.33475875854492):
        for z in (-120.33475875854492, 120.33475875854492):
            center = transformed([[302., y, z]])[0]
            radius = 23.0 * scale
            vertices = [center] + [center + [radius*math.cos(a), 0, radius*math.sin(a)]
                                      for a in np.arange(48)*2*math.pi/48]
            faces = [[0, i+1, (i+1)%48+1] for i in range(48)]
            add(trimesh.Trimesh(vertices=vertices, faces=faces, process=False), 'blue')
    return h
