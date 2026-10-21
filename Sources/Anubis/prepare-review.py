"""Build an isolated, reversible source-assembly inspection, never a runtime asset.

Source-located hull pieces retain all coordinates and facets. Separately fitted
parts require a recorded source registration. No Sketchfab asset data is used.
"""
import array
import gzip
import hashlib
import json
import math
import pathlib
import shutil
import struct
import sys
import importlib.util

ROOT = pathlib.Path(__file__).resolve().parent
REPO = next(parent for parent in ROOT.parents if (parent / "X890.code-workspace").is_file())
DEST = ROOT / "DesktopReview" / "FleetStudio"
PARTS = ["amun1", "amun2", "amun2a", "amun3", "amun3a", "amun4", "amun4a",
         "amun5", "amun6", "amun7", "amun7a", "amun8", "amun9", "amun10", "amun11"]
finish_spec = importlib.util.spec_from_file_location("review_finish", ROOT / "review-finish.py")
finish = importlib.util.module_from_spec(finish_spec)
finish_spec.loader.exec_module(finish)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def floats(values):
    result = array.array("f", values)
    if sys.byteorder != "little":
        result.byteswap()
    return result.tobytes()


def main():
    source_receipt = json.loads((ROOT / "source-acquisition.json").read_text(encoding="utf-8"))
    expected = {pathlib.Path(r["file"].replace("\\", "/")).name: r for r in source_receipt["files"]}
    DEST.mkdir(parents=True, exist_ok=True)
    (DEST / "models").mkdir(exist_ok=True)
    (DEST / "references").mkdir(exist_ok=True)
    for pattern in ("*.html", "*.js", "*.css"):
        for item in (REPO / "FleetStudio").glob(pattern):
            shutil.copyfile(item, DEST / item.name)
    shutil.copytree(REPO / "FleetStudio" / "vendor", DEST / "vendor", dirs_exist_ok=True)

    document = {
        "asset": {"version": "2.0", "generator": "X890 source assembly inspector",
                  "copyright": "Martin Taichl, CC BY 4.0; format conversion by X890"},
        "scene": 0, "scenes": [{"nodes": []}], "nodes": [], "meshes": [],
        "buffers": [], "bufferViews": [], "accessors": [],
        "materials": finish.MATERIALS,
        "extensionsUsed": ["KHR_materials_emissive_strength"],
        "textures": [{"source": 0, "sampler": 0}],
        "samplers": [{"magFilter": 9729, "minFilter": 9987, "wrapS": 10497, "wrapT": 10497}],
        "extras": {"source": source_receipt["printables"], "author": source_receipt["author"],
                   "license": source_receipt["license"],
                   "status": "Owner-accepted source assembly with production-reference finish; red collar finish retains existing cap geometry",
                   "sketchfabUsage": "Reference screenshots only; no downloaded model or textures",
                   "transforms": "Identity per source part; no mirroring, rounding, stretching or smoothing"}}
    binary = bytearray()
    assembly = []
    global_min = [float("inf")] * 3
    global_max = [float("-inf")] * 3

    def attribute(data, count, minimum=None, maximum=None, kind="VEC3"):
        while len(binary) % 4:
            binary.append(0)
        view = len(document["bufferViews"])
        document["bufferViews"].append({"buffer": 0, "byteOffset": len(binary),
                                       "byteLength": len(data), "target": 34962})
        binary.extend(data)
        accessor = {"bufferView": view, "componentType": 5126, "count": count, "type": kind}
        if minimum is not None:
            accessor.update(min=minimum, max=maximum)
        index = len(document["accessors"])
        document["accessors"].append(accessor)
        return index

    fittings = finish.pdcs() + finish.pods() + finish.torpedo_covers() + finish.hangar_cover()
    registration_file = ROOT / "fitting-registrations.json"
    if registration_file.exists():
        fittings.extend(json.loads(registration_file.read_text(encoding="utf-8"))["instances"])
    mesh_indices = {}
    for part in PARTS + list(dict.fromkeys(f["part"] for f in fittings)):
        filename = "xxx_-_" + part + ".stl"
        raw = (ROOT / "source-printables-123750" / filename).read_bytes()
        if sha(raw) != expected[filename]["sha256"]:
            raise ValueError("Original source hash changed: " + filename)
        triangles = struct.unpack_from("<I", raw, 80)[0]
        if len(raw) != 84 + 50 * triangles:
            raise ValueError("Invalid binary STL: " + filename)
        buckets = {}
        minimum, maximum = [float("inf")] * 3, [float("-inf")] * 3
        for offset in range(84, len(raw), 50):
            data = struct.unpack_from("<12fH", raw, offset)
            vertices = [data[3:6], data[6:9], data[9:12]]
            # Flat triangle normals preserve physical creases; no averaging or smoothing.
            a = [vertices[1][i] - vertices[0][i] for i in range(3)]
            b = [vertices[2][i] - vertices[0][i] for i in range(3)]
            n = [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]
            length = math.sqrt(sum(v*v for v in n))
            n = [v / length for v in n] if length > 1e-12 else [0.0, 0.0, 1.0]
            mat = finish.material_for(part, vertices, n, (offset - 84) // 50)
            positions, normals, uvs = buckets.setdefault(mat, (array.array("f"), array.array("f"), array.array("f")))
            major = max(range(3), key=lambda i: abs(n[i]))
            projection = ((1, 2), (0, 2), (0, 1))[major]
            for vertex in vertices:
                if not all(math.isfinite(v) for v in vertex):
                    raise ValueError("Non-finite vertex: " + filename)
                positions.extend(vertex)
                normals.extend(n)
                uvs.extend((vertex[projection[0]] / 70, vertex[projection[1]] / 70))
                for i in range(3):
                    minimum[i] = min(minimum[i], vertex[i])
                    maximum[i] = max(maximum[i], vertex[i])
                    if part in PARTS:
                        global_min[i] = min(global_min[i], vertex[i])
                        global_max[i] = max(global_max[i], vertex[i])
        primitives = []
        for mat, (positions, normals, uvs) in sorted(buckets.items()):
            count = len(positions) // 3
            pmin = [min(positions[i::3]) for i in range(3)]
            pmax = [max(positions[i::3]) for i in range(3)]
            primitives.append({"attributes": {"POSITION": attribute(floats(positions), count, pmin, pmax),
                                               "NORMAL": attribute(floats(normals), count),
                                               "TEXCOORD_0": attribute(floats(uvs), count, kind="VEC2")},
                               "material": mat, "mode": 4})
        mesh = len(document["meshes"])
        mesh_indices[part] = mesh
        document["meshes"].append({"name": part, "primitives": primitives})
        if part in PARTS:
            node = len(document["nodes"])
            document["nodes"].append({"name": part, "mesh": mesh,
                                      "extras": {"sourceFile": filename, "sourceSha256": sha(raw)}})
            document["scenes"][0]["nodes"].append(node)
        assembly.append({"part": part, "triangles": triangles, "sourceSha256": sha(raw),
                         "identityTransform": part in PARTS, "bounds": {"min": minimum, "max": maximum},
                         "materialTriangles": {str(m): len(b[0]) // 9 for m, b in buckets.items()}})
        print(f"Source mesh {part}: {triangles:,} triangles, unchanged local coordinates", flush=True)

    collar_faces = next(p for p in assembly if p["part"] == "amun7")["materialTriangles"].get("5", 0)
    if collar_faces != 516 or any(p["materialTriangles"].get("5", 0) for p in assembly if p["part"] != "amun7"):
        raise ValueError("Red finish must select exactly the six existing amun7 cheek caps (516 faces)")
    bow_faces = next(p for p in assembly if p["part"] == "amun10")["materialTriangles"].get("6", 0)
    if bow_faces != 80 or any(p["materialTriangles"].get("6", 0) for p in assembly if p["part"] != "amun10"):
        raise ValueError("Blue bow glow must select the 80 existing upper/lower/side/step-return lining faces")

    for fitting in fittings:
        matrix = fitting["matrix"]
        node = len(document["nodes"])
        document["nodes"].append({"name": fitting["name"], "mesh": mesh_indices[fitting["part"]],
                                  "matrix": [matrix[r][c] for c in range(4) for r in range(4)],
                                  "extras": {"registrationEvidence": fitting["evidence"]}})
        document["scenes"][0]["nodes"].append(node)
    png = finish.armour_png()
    while len(binary) % 4:
        binary.append(0)
    image_view = len(document["bufferViews"])
    document["bufferViews"].append({"buffer": 0, "byteOffset": len(binary), "byteLength": len(png)})
    binary.extend(png)
    document["images"] = [{"name": "Procedural charcoal finish / no production textures extracted", "bufferView": image_view, "mimeType": "image/png"}]

    document["buffers"] = [{"byteLength": len(binary)}]
    encoded = json.dumps(document, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    encoded += b" " * (-len(encoded) % 4)
    binary.extend(b"\0" * (-len(binary) % 4))
    glb = struct.pack("<III", 0x46546C67, 2, 28 + len(encoded) + len(binary))
    glb += struct.pack("<I4s", len(encoded), b"JSON") + encoded
    glb += struct.pack("<I4s", len(binary), b"BIN\0") + binary
    candidate_hash = sha(glb)
    (ROOT / "Anubis-source-assembly.glb").write_bytes(glb)
    (DEST / "models" / "Anubis-source-assembly.glb.gz").write_bytes(gzip.compress(glb, mtime=0))
    original = (REPO / "Assets" / "Models" / "Source" / "Fleet" / "Anubis.glb").read_bytes()
    if sha(original) != "7fd35b3de9e64b214a6597691549295f83653b149ec0f74974cb4ae733da812b":
        raise ValueError("Retained Anubis changed since the source audit")
    (DEST / "models" / "Anubis-retained-original.glb.gz").write_bytes(gzip.compress(original, mtime=0))

    captions = {
        "owner-bow-interior-guide.png": "OWNER CORRECTION: glow belongs inside the existing bow recess along its stepped inner edges. Cyan markup is placement guidance, not a new tube or literal uniform neon line. Exterior strip was rejected.",
        "owner-bow-interior-production.png": "OWNER-SUPPLIED PRODUCTION CLOSE-UP: localized blue/cyan interior highlights and soft spill inside the bow opening; outer armour remains dark. Exact source lens segmentation is unresolved.",
        "development-native1080-275.png": "PRIMARY FINAL DESIGN SHEET: Chris Danelon / Early Development of Anubis 4:35, decoded1920x1080. Narration4:29-4:34 identifies final ship design. Asymmetric sides, bow, drive, railgun, pod rows and closed/open torpedo configurations.",
        "development-native1080-265.png": "DEVELOPMENT CONTEXT: 4:25 matte finish sheet, before final asymmetry tweaks. Do not substitute its earlier geometry for the final4:35 design or production turntable.",
        "sketchfab-oblique-01.png": "Anton Perfilev fan model: oblique whole hull, screenshot only.",
        "sketchfab-pod-flank-02.png": "Pod-side recesses. Detached demonstration parts are not mounted fittings.",
        "sketchfab-drive-quarter-03.png": "Drive-quarter reference: unequal armour volumes around the drive.",
        "sketchfab-hangar-flank-04.png": "Opposite hangar flank; separate recessed frame and outer armour.",
        "sketchfab-spine-view-05.png": "Long spine and adjacent angled armour; perspective view.",
        "sketchfab-opposite-surface-06.png": "Opposite broad surface; dark lighting limits detail evidence.",
        "sketchfab-bow-quarter-07.png": "Bow quarter: projecting spine and unequal shoulder profile.",
        "sketchfab-aft-detail-08.png": "Aft detail: drive recess and adjoining armour.",
        "sketchfab-underside-drive-09.png": "Underside toward drive: separate surfaces and deep recesses.",
        "sketchfab-drive-end-10.png": "Drive-end cross-section; fan-reference view, not production dimensions.",
        "sketchfab-bow-end-11.png": "Bow-end cross-section and railgun-side structure.",
    }
    source_views = ["Unequal bow silhouette", "Oblique drive and segmented cyan rim", "Armour finish and inset panels",
                    "Raised spine and red lamp group", "End-quarter structure", "Pod flank and triangular framing",
                    "Railgun-side projection", "Opposite armour facets", "Drive aperture and compact hot core",
                    "Second flank and recessed hardware"]
    for i, caption in enumerate(source_views, 1):
        seconds = 180 + 5 * i
        captions[f"production-native1080-{i:02}.png"] = f"PRIMARY: Chris Danelon {seconds//60}:{seconds%60:02}, manually selected 1080p; decoded 1920 x 1080. {caption}. Source camera crops some angles."
    for seconds, caption in [(475, "Breaching pod rows"), (505, "Two torpedoes per carrier"),
                              (525, "Four PDC assemblies and parked directions"), (535, "PDC socket and gun shoulder")]:
        captions[f"creator-fit-{seconds}.png"] = f"KIT ASSEMBLY: Martin Taichl {seconds//60}:{seconds%60:02} / {caption}. CAD colours are not TV paint evidence."
    references = []
    def reference_order(item):
        name = item[0]
        priority = (-2 if name == "owner-bow-interior-production.png" else
                    -1 if name.startswith("owner-bow-interior") else
                    0 if name == "development-native1080-275.png" else
                    1 if name.startswith("production-native1080") else
                    2 if name.startswith("creator-fit") else
                    3 if name.startswith("development") else 4)
        return priority, name
    ordered_captions = sorted(captions.items(), key=reference_order)
    for filename, caption in ordered_captions:
        source = ROOT / "reference-captures" / filename
        if source.exists():
            shutil.copyfile(source, DEST / "references" / filename)
            references.append({"image": "references/" + filename, "caption": caption})
    by_part = {p["part"]: p for p in assembly}
    triangles = sum(by_part[p]["triangles"] for p in PARTS) + sum(by_part[f["part"]]["triangles"] for f in fittings)
    note = ("Owner-accepted production-reference finish and fittings, with localized red collar finish. "
            "15 source-located Martin Taichl hull pieces keep their exact coordinates and creases. "
            "Source-proven fittings are instanced with recorded mating transforms. "
            "Closed torpedo and hangar covers are fitted; open alternatives and print accessories are omitted. "
            "The creator describes this fan kit as bulkier than the studio ship. "
            "Charcoal surface texture is a procedural interpretation, not production artwork. "
            "Two localized red glow groups follow the recessed amun7 cheek caps beneath the raised spine. "
            "Individual production lenses merge in the glow; the source kit's three caps per cheek remain intact. "
            "Blue-cyan emission follows the entire existing inner bow perimeter: upper/lower lining, sidewalls and stepped returns. "
            "Uneven bright patches, perimeter spill and concealed rear fills illuminate the real recess and port surround. "
            "The rejected exterior strip is charcoal again; outer banks, central port and rear bulkhead retain their materials. "
            "Original files and the installed fleet remain unchanged.")
    entry = {"id": "AnubisSourceReview", "title": "Anubis / production finish and fittings review",
             "flipLongitudinal": True,
             "model": "models/Anubis-source-assembly.glb.gz", "sha256": candidate_hash,
             "triangles": triangles, "materials": len(finish.MATERIALS), "images": 1, "note": note,
             "credit": "Martin Taichl multipart source, CC BY 4.0; X890 source-preserving assembly and display finish. Anton Perfilev model used through screenshots only. Primary production reference: Chris Danelon.",
             "referenceUrl": "https://www.youtube.com/watch?v=vefkFrWLuNI&t=184s",
             "relatedSources": [{"label": "Production Anubis turntable / 3:04", "url": "https://www.youtube.com/watch?v=vefkFrWLuNI&t=184s"},
                                {"label": "Anton Perfilev / screenshots only", "url": "https://sketchfab.com/3d-models/amun-ra-class-stealth-ship-the-expanse-4956e4648f4e407ab06ef83abbbdc91a"},
                                {"label": "Martin Taichl source kit / CC BY 4.0", "url": source_receipt["printables"]}],
             "references": references, "beforeModel": "models/Anubis-retained-original.glb.gz",
             "beforeSha256": sha(original), "beforeTitle": "Anubis / retained original approximation",
             "beforeFlipLongitudinal": False, "beforeEngineId": "AnubisRetainedOriginal",
             "beforeCredit": "Retained X890 project-authored approximation, not the Martin Taichl source kit.",
             "beforeLabel": "Compare retained original", "beforeNote": "Retained 2,754-triangle approximation. Research comparison only.",
             "beforeInfo": {"triangles": 2754, "materials": 9}}
    # Review the revised lighting beside the accepted fleet, including the
    # Guanshiyin benchmark. Model bytes are copied losslessly; no other source
    # model or production catalogue is edited by this review preparation.
    studio = REPO / "FleetStudio"
    fleet = json.loads((studio / "catalog.json").read_text(encoding="utf-8"))
    compressed_hashes = {item["file"]: item["gzip_sha256"]
                         for item in json.loads((studio / "manifest.json").read_text(encoding="utf-8"))["assets"]}
    comparison_fleet = [item for item in fleet if item["id"] != "Anubis"]
    for item in comparison_fleet:
        for field in ("model", "beforeModel"):
            if not item.get(field):
                continue
            name = item[field]
            source_model, target_model = (studio / name).resolve(), (DEST / name).resolve()
            if not source_model.is_relative_to(studio.resolve()) or not target_model.is_relative_to(DEST.resolve()):
                raise ValueError("Fleet comparison asset is outside its review root")
            expected_hash = compressed_hashes[name]
            if sha(source_model.read_bytes()) != expected_hash:
                raise ValueError("Fleet comparison source checksum: " + name)
            if not target_model.exists() or sha(target_model.read_bytes()) != expected_hash:
                target_model.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(source_model, target_model)
                if sha(target_model.read_bytes()) != expected_hash:
                    raise ValueError("Fleet comparison copy checksum: " + name)
    (DEST / "catalog.json").write_text(json.dumps([entry] + comparison_fleet, indent=2), encoding="utf-8")
    engine = {"position": [173.6, -30, 0], "direction": [1, 0, 0], "radius": 26,
              "length": 190, "color": [.09, .42, 1], "style": "anubis-production", "addCore": False,
              "evidence": "Existing outlet X173.5, axis +X through Y=-30/Z=0. Length is display tuning, not a production measurement."}
    review_engines = json.loads((studio / "engines.json").read_text(encoding="utf-8"))
    review_engines.update({entry["id"]: {"engines": [engine]}, "AnubisRetainedOriginal": {"engines": []}})
    (DEST / "engines.json").write_text(json.dumps(review_engines, indent=2), encoding="utf-8")
    viewer = (DEST / "review.js").read_text(encoding="utf-8")
    viewer = viewer.replace("entry.flipLongitudinal?-1:1", "(requestedPrevious?entry.beforeFlipLongitudinal:entry.flipLongitudinal)?-1:1")
    viewer = viewer.replace("'No engines / gate'", "'No comparison drive effects'")
    viewer = viewer.replace("'The Sol gate has an animated aperture, not drive engines'", "'This retained comparison has no registered drive effects'")
    viewer = viewer.replace("'Gate rim lights '+(shipLit?'on':'off')+' · aperture controlled separately'", "'Retained comparison · drive effects unregistered'")
    viewer = viewer.replace("['RocinanteS6','Anubis']", "['RocinanteS6','Anubis','AnubisSourceReview']")
    initial_load = " await load(new URL(location.href).searchParams.get('ship')||'Pella');"
    if initial_load not in viewer:
        raise ValueError("The review startup changed; inspect it before applying bow framing")
    bow_startup = """
 if(entry.id==='AnubisSourceReview'){
  view('nose');
  const mouth=root.getObjectByName('amun10');
  if(!mouth)throw Error('The source bow lining was not found');
  const focus=mouth.localToWorld(new THREE.Vector3(-66.5,-7.5,0));
  camera.position.add(focus.clone().sub(controls.target));controls.target.copy(focus);
  camera.zoom=9;camera.updateProjectionMatrix();controls.update();toggleReferences(true);invalidate();
 }
"""
    viewer = viewer.replace(initial_load, initial_load.replace("||'Pella'", "||'AnubisSourceReview'") + bow_startup)
    (DEST / "review.js").write_text(viewer, encoding="utf-8")
    report = {"stage": "Accepted assembly preserved; owner-requested interior bow correction prepared for native review",
              "sourceParts": assembly, "triangles": triangles, "bounds": {"min": global_min, "max": global_max},
              "fittings": fittings, "engine": engine,
              "sha256": candidate_hash, "originalSha256": sha(original), "referenceScreenshots": references,
              "sourceGeometryModified": False, "runtimeAdopted": False, "sketchfabModelDownloaded": False,
              "redCollarFaces": collar_faces,
              "blueBowFaces": bow_faces,
              "blueBowFaceGroups": {name: len(faces) for name, faces in finish.BOW_INTERIOR_FACE_GROUPS.items()},
              "redCollarEvidence": "Two cheek groups beneath the raised spine, between the pod-bearing banks; production turntable. Source cap geometry retained; individual TV lens count obscured by glow. Other top-row, opposite-spine and aft-bracket caps remain dark."}
    (ROOT / "assembly-review.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(f"Prepared {triangles:,}-triangle source assembly: {candidate_hash}", flush=True)
    print("Separate Fleet Studio review: " + str(DEST), flush=True)
    print("No original model, canonical catalogue, installed app or release changed.", flush=True)


if __name__ == "__main__":
    main()
