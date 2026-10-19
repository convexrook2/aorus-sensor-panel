"""Isolated, non-destructive GLB look development for Blender 5.2.

blender --background --python tools/Blender-FleetLookdev.py -- \
    --input ship.glb --output artifacts/new-lookdev --title "Pella"

The output directory must not exist. Writes an editable, packed .blend, six
orthographic PNGs and a receipt. Never exports or edits the input GLB. Front
and side mean Blender -Y and +X views after the longest world axis is aligned
to +Z; they do not assert which end of an arbitrary GLB is its bow.
"""

import argparse
import hashlib
import json
import math
from pathlib import Path
import sys
import time
import traceback
from datetime import datetime, timezone

import bpy
from mathutils import Matrix, Vector


RESOLUTION = 1200
SAMPLES = 48
FIT_PADDING = 1.16
VIEWS = {"hero": (1.1, -1.5, 0.8), "front": (0, -1, 0), "side": (1, 0, 0)}


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def utc_now():
    return datetime.now(timezone.utc).isoformat()


def vector_list(vector):
    return [round(float(value), 9) for value in vector]


def world_corners(objects):
    """Evaluated mesh boxes include every vertex without including stage props."""
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    points = []
    for obj in objects:
        if obj.type != "MESH" or not obj.data.vertices:
            continue
        evaluated = obj.evaluated_get(depsgraph)
        points.extend(evaluated.matrix_world @ Vector(corner)
                      for corner in evaluated.bound_box)
    if not points or any(not math.isfinite(v) for p in points for v in p):
        raise ValueError("The GLB has no finite mesh bounds.")
    return points


def bounds(points):
    low = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    high = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    return low, high


def inventory(objects):
    mesh_objects = [obj for obj in objects if obj.type == "MESH"]
    unique_meshes = {obj.data.as_pointer(): obj.data for obj in mesh_objects}
    materials = {material.name for obj in mesh_objects for material in obj.data.materials
                 if material is not None}
    triangles = {}
    for key, mesh in unique_meshes.items():
        mesh.calc_loop_triangles()
        triangles[key] = len(mesh.loop_triangles)
    return {
        "objects": len(objects), "mesh_objects": len(mesh_objects),
        "unique_meshes": len(unique_meshes),
        "vertices_unique": sum(len(mesh.vertices) for mesh in unique_meshes.values()),
        "polygons_unique": sum(len(mesh.polygons) for mesh in unique_meshes.values()),
        "triangles_unique": sum(triangles.values()),
        "triangles_with_instances": sum(triangles[obj.data.as_pointer()]
                                        for obj in mesh_objects),
        "material_names": sorted(materials),
        "custom_normals": {mesh.name: getattr(mesh, "has_custom_normals", None)
                           for mesh in unique_meshes.values()},
    }


def collection(name, scene):
    result = bpy.data.collections.new(name)
    scene.collection.children.link(result)
    return result


def move_to_collection(obj, target):
    target.objects.link(obj)
    for previous in tuple(obj.users_collection):
        if previous != target:
            previous.objects.unlink(obj)


def normalize_model(imported, scene, flip_longitudinal=False):
    points = world_corners(imported)
    low, high = bounds(points)
    extent = high - low
    longest = max(range(3), key=lambda axis: extent[axis])
    if extent[longest] <= 1e-12:
        raise ValueError("The GLB bounds have zero extent.")
    center = (low + high) * 0.5
    scale = 4.0 / extent[longest]
    source_axis = Vector(tuple(1.0 if axis == longest else 0.0 for axis in range(3)))
    rotation = source_axis.rotation_difference(Vector((0, 0, -1 if flip_longitudinal else 1))).to_matrix().to_4x4()
    transform = Matrix.Scale(scale, 4) @ rotation @ Matrix.Translation(-center)

    model = collection("MODEL — imported geometry", scene)
    source_rig = collection("SOURCE — cameras and lights disabled", scene)
    parent = bpy.data.objects.new("MODEL — normalization only", None)
    model.objects.link(parent)
    imported_set = set(imported)
    for obj in imported:
        move_to_collection(obj, source_rig if obj.type in {"CAMERA", "LIGHT"} else model)
        if obj.type in {"CAMERA", "LIGHT"}:
            obj.hide_render = True
        if obj.parent not in imported_set:
            original_world = obj.matrix_world.copy()
            obj.parent = parent
            obj.matrix_parent_inverse = Matrix.Identity(4)
            obj.matrix_world = original_world
    parent.matrix_world = transform
    bpy.context.view_layer.update()
    normalized = world_corners(imported)
    final_low, final_high = bounds(normalized)
    if abs(max(final_high - final_low) - 4.0) > 1e-4:
        raise ValueError("Normalization did not produce the requested maximum span.")
    return model, source_rig, normalized, {
        "method": "parent translation, proper rotation and uniform scale; no mesh transforms applied",
        "original_bounds": {"min": vector_list(low), "max": vector_list(high)},
        "original_longest_world_axis": "XYZ"[longest],
        "uniform_scale": scale,
        "parent_matrix": [list(row) for row in transform],
        "normalized_bounds": {"min": vector_list(final_low), "max": vector_list(final_high)},
        "maximum_span": 4.0,
    }


def choose_device():
    """Use an available GPU without changing saved Blender preferences."""
    preferences = bpy.context.preferences.addons["cycles"].preferences
    errors = []
    for backend in ("OPTIX", "CUDA", "HIP", "METAL", "ONEAPI"):
        try:
            preferences.compute_device_type = backend
            devices = preferences.get_devices_for_type(backend)
            accelerated = [device for device in devices if device.type == backend]
            if accelerated:
                for device in preferences.devices:
                    device.use = device.type == backend
                return "GPU", {"backend": backend,
                               "names": [device.name for device in accelerated]}
        except (TypeError, RuntimeError, ValueError) as error:
            errors.append(f"{backend}: {error}")
    return "CPU", {"backend": "CPU", "gpu_selection_notes": errors}


def configure_scene(scene, device):
    scene.render.engine = "CYCLES"
    scene.cycles.device = device
    scene.cycles.samples = SAMPLES
    scene.cycles.use_adaptive_sampling = False
    scene.cycles.use_denoising = True
    scene.cycles.seed = 0
    scene.render.resolution_x = RESOLUTION
    scene.render.resolution_y = RESOLUTION
    scene.render.resolution_percentage = 100
    scene.render.pixel_aspect_x = 1.0
    scene.render.pixel_aspect_y = 1.0
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = False
    scene.render.use_border = False
    scene.render.use_file_extension = True
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Base Contrast"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0


def make_world(scene, color, strength):
    world = bpy.data.worlds.new(f"{scene.name} — world")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (*color, 1.0)
    background.inputs["Strength"].default_value = strength
    scene.world = world


def area_light(target, name, position, power, size, color):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = power
    data.shape = "DISK"
    data.size = size
    data.color = color
    if hasattr(data, "use_shadow"):
        data.use_shadow = True
    obj = bpy.data.objects.new(name, data)
    target.objects.link(obj)
    obj.location = position
    obj.rotation_euler = (-obj.location).to_track_quat("-Z", "Y").to_euler()


def studio_floor(target, z):
    mesh = bpy.data.meshes.new("Studio floor — outside model")
    mesh.from_pydata([(-15, -15, z), (15, -15, z), (15, 15, z), (-15, 15, z)],
                     [], [(0, 1, 2, 3)])
    obj = bpy.data.objects.new("Studio floor — shadow receiver", mesh)
    target.objects.link(obj)
    material = bpy.data.materials.new("Studio floor — neutral matte")
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (0.075, 0.075, 0.075, 1)
    shader.inputs["Roughness"].default_value = 0.85
    obj.data.materials.append(material)


def lighting(scene, mode, minimum_z):
    rig = collection(f"LIGHTING — {mode}", scene)
    if mode == "neutral":
        make_world(scene, (0.08, 0.08, 0.08), 0.4)
        settings = [((5, -6, 7), 1100, 5, (1, 1, 1)),
                    ((-5, -2, 3), 450, 4, (1, 1, 1)),
                    ((2, 5, 5), 850, 3, (1, 1, 1))]
        studio_floor(collection("STAGE — neutral shadow receiver", scene), minimum_z - 0.2)
    else:
        make_world(scene, (0.018, 0.025, 0.04), 0.15)
        settings = [((4, -5, 6), 1000, 3, (1, 0.94, 0.86)),
                    ((-4, -3, 1), 130, 4, (0.72, 0.82, 1)),
                    ((1, 5, 4), 950, 2.5, (0.65, 0.8, 1))]
    for role, (position, power, size, color) in zip(("key", "fill", "rim"), settings):
        area_light(rig, f"{mode} — {role}", position, power, size, color)
    return [{"role": role, "position": position, "power_watts": power,
             "size": size, "color_linear": color}
            for role, (position, power, size, color)
            in zip(("key", "fill", "rim"), settings)]


def fit_camera(target, name, direction, points):
    normal = Vector(direction).normalized()
    rotation = (-normal).to_track_quat("-Z", "Y")
    right, up = rotation @ Vector((1, 0, 0)), rotation @ Vector((0, 1, 0))
    xs, ys = [point.dot(right) for point in points], [point.dot(up) for point in points]
    middle_x, middle_y = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    center = right * middle_x + up * middle_y
    width, height = max(xs) - min(xs), max(ys) - min(ys)
    scale = max(width, height, 1e-6) * FIT_PADDING
    low, high = bounds(points)
    distance = max(10.0, (high - low).length * 3.0)
    data = bpy.data.cameras.new(name)
    data.type = "ORTHO"
    data.ortho_scale = scale
    depths = [distance - (point - center).dot(normal) for point in points]
    data.clip_start = max(0.01, min(depths) - 1.0)
    data.clip_end = max(depths) + 1.0
    obj = bpy.data.objects.new(name, data)
    target.objects.link(obj)
    obj.location = center + normal * distance
    obj.rotation_euler = rotation.to_euler()
    margins = {"horizontal": (1 - width / scale) * RESOLUTION / 2,
               "vertical": (1 - height / scale) * RESOLUTION / 2}
    if min(margins.values()) < 1:
        raise ValueError(f"Camera fit has inadequate margin: {name}")
    return obj, {"type": "ORTHO", "location": vector_list(obj.location),
                 "rotation_euler": vector_list(obj.rotation_euler),
                 "ortho_scale": scale, "clip_start": data.clip_start,
                 "clip_end": data.clip_end, "projected_width": width,
                 "projected_height": height, "padding_factor": FIT_PADDING,
                 "minimum_edge_margin_pixels": margins,
                 "fit_basis": "all evaluated imported mesh bounding-box corners, including antennae"}


def run(args, source, output, receipt):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.name = "Lookdev — neutral"
    result = bpy.ops.import_scene.gltf(filepath=str(source), import_shading="NORMALS",
                                     merge_vertices=False, import_merge_material_slots=False,
                                     import_pack_images=True)
    if "FINISHED" not in result:
        raise RuntimeError(f"GLB import did not finish: {result}")
    imported = list(scene.objects)
    before = inventory(imported)
    model, source_rig, points, normalization = normalize_model(imported, scene, args.flip_longitudinal)
    after = inventory(imported)
    if before != after:
        raise RuntimeError("Imported mesh/material inventory changed during normalization.")
    device, device_info = choose_device()
    camera_collection = collection("CAMERAS — orthographic fitted views", scene)
    cameras, fit = {}, {}
    for view, direction in VIEWS.items():
        cameras[view], fit[view] = fit_camera(camera_collection, view, direction, points)

    modes = {"neutral": scene, "cinematic": bpy.data.scenes.new("Lookdev — cinematic")}
    light_receipt = {}
    for mode, current in modes.items():
        if current != scene:
            for shared in (model, source_rig, camera_collection):
                current.collection.children.link(shared)
        configure_scene(current, device)
        current["lookdev_title"] = args.title
        current["source_glb"] = str(source)
        current["source_sha256"] = receipt["input_sha256"]
        current.camera = cameras["hero"]
        current.render.filepath = str(output / f"{mode}-hero.png")
        light_receipt[mode] = lighting(current, mode, min(point.z for point in points))

    receipt.update({"inventory_before": before, "inventory_after": after,
                    "normalization": normalization, "flip_longitudinal": args.flip_longitudinal, "camera_fit": fit,
                    "render_device": device_info, "lighting": light_receipt,
                    "scenes": {mode: current.name for mode, current in modes.items()},
                    "input_materials_modified": False,
                    "normal_policy": "glTF NORMALS import; no vertex merge, smoothing, remesh or mesh modifiers",
                    "views": "front = -Y; side = +X; hero = angled orthographic; +Z is longest axis, not an inferred bow",
                    "render_settings": {"engine": "CYCLES", "samples": SAMPLES,
                                        "denoising": True, "resolution": [RESOLUTION, RESOLUTION],
                                        "view_transform": "AgX", "look": "AgX - Base Contrast",
                                        "exposure": 0.0, "actual_ray_traced_shadows": True},
                    "stage": "neutral has a separate matte shadow receiver; cinematic has self-shadowing without floor",
                    "renders": []})
    bpy.ops.file.pack_all()
    blend_path = output / "FleetLookdev.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    receipt["blend"] = {"file": blend_path.name, "sha256": sha256(blend_path)}
    receipt["status"] = "rendering"
    write_receipt(output, receipt)
    for mode, current in modes.items():
        for view, camera in cameras.items():
            path = output / f"{mode}-{view}.png"
            if path.exists():
                raise FileExistsError(f"Refusing to replace existing output: {path}")
            current.camera = camera
            current.render.filepath = str(path)
            started = time.monotonic()
            print(f"LOOKDEV: rendering {mode}/{view} -> {path}", flush=True)
            result = bpy.ops.render.render(write_still=True, scene=current.name)
            if "FINISHED" not in result or not path.is_file():
                raise RuntimeError(f"Render did not produce {path}: {result}")
            receipt["renders"].append({"mode": mode, "view": view, "file": path.name,
                                       "sha256": sha256(path), "bytes": path.stat().st_size,
                                       "seconds": round(time.monotonic() - started, 3)})
            write_receipt(output, receipt)
        current.camera = cameras["hero"]
    receipt["input_sha256_after"] = sha256(source)
    if receipt["input_sha256_after"] != receipt["input_sha256"]:
        raise RuntimeError("The input file changed during rendering; results need a fresh isolated run.")
    receipt["status"] = "complete"
    receipt["finished_utc"] = utc_now()
    write_receipt(output, receipt)


def write_receipt(output, receipt):
    # The directory was created exclusively by this run. Only this receipt is updated.
    (output / "render-receipt.json").write_text(json.dumps(receipt, indent=2), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--title", required=True)
    parser.add_argument("--flip-longitudinal", action="store_true", help="Reverse a verified engine-at-positive-axis source for bow-up review.")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    source, output = args.input.expanduser().resolve(), args.output.expanduser().resolve()
    if not source.is_file() or source.suffix.lower() != ".glb":
        parser.error("--input must identify an existing GLB file")
    if output.exists():
        parser.error("--output must be a new directory; existing files are never replaced")
    if bpy.app.version < (5, 2, 0):
        parser.error("This helper requires Blender 5.2 or newer")
    receipt = {"title": args.title, "input": str(source), "input_sha256": sha256(source),
               "input_bytes": source.stat().st_size, "blender_version": bpy.app.version_string,
               "script_sha256": sha256(Path(__file__).resolve()), "started_utc": utc_now(),
               "status": "preparing"}
    output.mkdir(parents=True, exist_ok=False)
    write_receipt(output, receipt)
    try:
        run(args, source, output, receipt)
    except Exception as error:
        receipt.update({"status": "failed", "error": str(error), "finished_utc": utc_now()})
        write_receipt(output, receipt)
        traceback.print_exc()
        raise SystemExit(1)
    print(f"LOOKDEV COMPLETE: {output / 'render-receipt.json'}", flush=True)


if __name__ == "__main__":
    main()
