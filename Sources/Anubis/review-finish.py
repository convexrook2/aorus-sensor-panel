"""Display-only finish and source-proven fitting registrations for Anubis.

No original STL is changed. Colours are an interpretation of the 1080p
production turntable, not measurements or a production texture extraction.
"""
import math
import random
import struct
import zlib


MATERIALS = [
    {"name": "Charcoal ceramic armour / production-reference interpretation",
     "pbrMetallicRoughness": {"baseColorFactor": [.075, .082, .092, 1],
                              "baseColorTexture": {"index": 0},
                              "metallicFactor": .50, "roughnessFactor": .64}},
    {"name": "Exposed gunmetal fittings",
     "pbrMetallicRoughness": {"baseColorFactor": [.105, .115, .127, 1],
                              "metallicFactor": .78, "roughnessFactor": .43}},
    {"name": "Dark mechanical recesses",
     "pbrMetallicRoughness": {"baseColorFactor": [.018, .022, .028, 1],
                              "metallicFactor": .42, "roughnessFactor": .76}},
    {"name": "Existing segmented cyan drive rim caps",
     "pbrMetallicRoughness": {"baseColorFactor": [.02, .07, .08, 1],
                              "metallicFactor": .3, "roughnessFactor": .35},
     "emissiveFactor": [.015, .68, .78],
     "extensions": {"KHR_materials_emissive_strength": {"emissiveStrength": 2.4}}},
    {"name": "Existing compact blue-white drive throat",
     "pbrMetallicRoughness": {"baseColorFactor": [.09, .13, .18, 1],
                              "metallicFactor": .1, "roughnessFactor": .4},
     "emissiveFactor": [.55, .79, 1],
     "extensions": {"KHR_materials_emissive_strength": {"emissiveStrength": 3.6}}},
    {"name": "Recessed red collar cheek caps / production-reference interpretation",
     "pbrMetallicRoughness": {"baseColorFactor": [.12, .009, .014, 1],
                              "metallicFactor": .2, "roughnessFactor": .38},
     "emissiveFactor": [1, .018, .045],
     "extensions": {"KHR_materials_emissive_strength": {"emissiveStrength": 2.3}}},
    {"name": "Anubis complete inner bow lining / blue-cyan recess glow",
     "pbrMetallicRoughness": {"baseColorFactor": [.075, .082, .092, 1],
                              "baseColorTexture": {"index": 0},
                              "metallicFactor": .50, "roughnessFactor": .64},
     "emissiveFactor": [.04, .50, 1],
     "extensions": {"KHR_materials_emissive_strength": {"emissiveStrength": 7.5}},
     "extras": {"anubisInteriorGlow": True}},
]

# Exact original amun10 lining: the complete inward perimeter, including lower
# bevels and vertical step returns. The display shader confines light near the
# mouth; the port, rear bulkhead, outward caps and slotted banks are not emitters.
BOW_INTERIOR_FACE_GROUPS = {
    "upper": frozenset([209,210,211,212,213,214,393,394,395,396,399,487,
        488,490,491,493,644,645,397,398,574,576,577,578,581,637,634,636,638,640,
        648,649,571,572]),
    "lower": frozenset([100,101,102,103,104,107,406,409,410,411,414,471,
        562,563,564,565,567,624,477,481,482,485,642,643,619,623,625,629,
        646,647,403,404,566,568]),
    "sides": frozenset([400,401,569,570]),
    "step_returns": frozenset([391,392,579,580,412,413,560,561]),
}
BOW_INTERIOR_FACES = frozenset().union(*BOW_INTERIOR_FACE_GROUPS.values())


def armour_png(size=1024):
    """A restrained material variation, not added/raised geometry or TV artwork.

    Fine irregular rectangular tile variation follows the production surface.
    The source's existing geometric panel boundaries and physical creases remain.
    """
    rng = random.Random(123750)
    rows = []
    y = 0
    while y < size:
        height = rng.randrange(16, 38)
        tiles = []
        x = -rng.randrange(50)
        while x < size:
            width = rng.randrange(20, 78)
            tiles.append((x, x + width, rng.randrange(137, 165)))
            x += width
        rows.append((y, y + height, tiles))
        y += height
    pixels = bytearray()
    row_index = 0
    for y in range(size):
        while y >= rows[row_index][1]:
            row_index += 1
        y0, y1, tiles = rows[row_index]
        tile_index = 0
        pixels.append(0)
        for x in range(size):
            while x >= tiles[tile_index][1]:
                tile_index += 1
            x0, x1, value = tiles[tile_index]
            noise = rng.randrange(-5, 6)
            # Low contrast seams are optical finish, not deep black cuts.
            seam = -15 if x == x0 or y == y0 else 0
            grain = 2 * math.sin(x * .11 + math.sin(y * .08))
            c = max(0, min(255, round(value + noise + seam + grain)))
            pixels.extend((c, c, c))
    def chunk(kind, payload):
        return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload) & 0xffffffff)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 2, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(pixels, 9)) + chunk(b"IEND", b""))


def material_for(part, vertices, normal, face_index=None):
    if part == "amun10" and face_index in BOW_INTERIOR_FACES:
        if abs(normal[0]) > .001:
            raise ValueError("Bow light crossed onto a front cap or rear bulkhead")
        if face_index in BOW_INTERIOR_FACE_GROUPS["upper"] and normal[1] >= -.1:
            raise ValueError("Upper bow face does not face into the recess")
        if face_index in BOW_INTERIOR_FACE_GROUPS["lower"] and normal[1] <= .1:
            raise ValueError("Lower bow face does not face into the recess")
        if face_index in (BOW_INTERIOR_FACE_GROUPS["sides"] | BOW_INTERIOR_FACE_GROUPS["step_returns"]):
            z = sum(v[2] for v in vertices) / 3
            if abs(normal[1]) > .001 or z * normal[2] >= 0:
                raise ValueError("Bow side/step return does not face into the recess")
        return 6
    if part == "amun7" and red_collar_cap(vertices, normal):
        return 5
    if part in ("am7c", "am8c_2x"):
        return 0
    if part == "am4_14x" and all(abs(v[2] - 6) < .00001 for v in vertices) and normal[2] > .99999:
        return 0
    if part == "amun1":
        if all(abs(v[0] - 173.5) < .0001 for v in vertices) and normal[0] > .99999:
            return 3
        if all(abs(v[0] - 142.5) < .0001 for v in vertices) and normal[0] > .99999:
            return 4
    if not part.startswith("amun"):
        return 1
    return 0


def red_collar_cap(vertices, normal):
    """Six existing recessed caps on the two cheeks of the +Y spine.

    Production turntable: two localized red glow groups beneath this spine,
    between the pod banks. Individual lenses merge in the footage; retain the
    licensed source's three caps per cheek. Do not light the unrelated top row,
    opposite spine or drive brackets, and do not add or move any source face.
    """
    centroid = [sum(v[i] for v in vertices) / 3 for i in range(3)]
    centers = [(81.04817963, 27.57275277, 11.94375218),
               (84.98663104, 28.13433007, 11.52754519),
               (88.92508327, 28.69590733, 11.11133837)]
    for sign in (-1, 1):
        axis = [.013579535161, .532162839751, sign * .846533111114]
        if sum(normal[i] * axis[i] for i in range(3)) <= .9999:
            continue
        for x, y, z in centers:
            delta = [centroid[0] - x, centroid[1] - y, centroid[2] - sign * z]
            depth = sum(delta[i] * axis[i] for i in range(3))
            radial2 = sum((delta[i] - depth * axis[i]) ** 2 for i in range(3))
            if abs(depth) < .001 and radial2 < .5001 ** 2:
                return True
    return False


def pdcs():
    registrations = [
        ("amun2", [[.265343617, 0, -.964153911, -21.733887],
                   [.931301164, -.258819045, .256302252, -30.298189],
                   [-.249541395, -.965925826, -.068675982, -53.187607], [0, 0, 0, 1]], False),
        ("amun5", [[-.003391698, .0174524, -.999841943, 48.418612],
                   [-.663018015, .7484468, .01531337, 8.564266],
                   [.748595757, .662965159, .00903275, 41.561658], [0, 0, 0, 1]], False),
        ("amun7", [[0, 0, -1, 130.252064], [0, 1, 0, 25.8762],
                   [1, 0, 0, 0], [0, 0, 0, 1]], True),
        ("amun8", [[0, 0, -1, 111.323807], [0, -1, 0, -55.6001],
                   [-1, 0, 0, 0], [0, 0, 0, 1]], True),
    ]
    result = []
    for mount, matrix, aft in registrations:
        result.append({"name": mount + " / PDC housing", "part": "am5_4x", "matrix": matrix,
                       "evidence": "Radius 2.5 source peg/socket fit; housing underside local Y=0. Creator assembly 8:55."})
        gun = [row[:] for row in matrix]
        for i in range(3):
            gun[i][3] += 5.5 * matrix[i][1]
            if aft:
                gun[i][0] *= -1
                gun[i][2] *= -1
        result.append({"name": mount + " / PDC gun", "part": "am6_4x", "matrix": gun,
                       "evidence": "Source peg inserted 3.5 into housing; shoulder at local Y=5.5. Fore/aft parked yaw follows creator 8:45; gun elevation unchanged."})
    return result


def pods():
    families = {
        "A": [[.988221, .074114, .133889], [.090533, -.988513, -.121023], [.123381, .131719, -.983579]],
        "B": [[-.988221, -.074114, .133889], [-.090533, .988513, -.121023], [-.123381, -.131719, -.983579]],
        "C": [[-.956141, 0, -.292906], [.146574, .865788, -.478464], [.253595, -.500411, -.827815]],
        "D": [[.956141, 0, -.292906], [-.146574, -.865788, -.478464], [-.253595, .500411, -.827815]],
        "E": [[.956313, .152399, -.249479], [-.209161, .952886, -.219679], [.204246, .262263, .943134]],
        "F": [[.992789, .062214, -.102469], [-.119050, .411498, -.903602], [-.014051, .909285, .415938]],
        "G": [[-.992789, -.062214, -.102469], [.119050, -.411498, -.903602], [.014051, -.909285, .415938]],
        "H": [[-.956314, -.152399, -.249478], [.209161, -.952886, -.219679], [-.204245, -.262263, .943134]],
    }
    seats = [
        ("amun3a", "A", (53.420889, -14.403689, -41.477578)),
        ("amun3a", "A", (91.124201, -10.949615, -36.770244)),
        ("amun3a", "B", (34.551900, -16.132313, -43.833411)),
        ("amun3a", "B", (72.272546, -12.676652, -39.123911)),
        ("amun3a", "B", (109.993190, -9.220991, -34.414414)),
        ("amun4", "C", (45.447003, -52.647994, -39.919079)),
        ("amun4", "C", (81.943154, -58.242748, -49.598855)),
        ("amun4", "D", (63.686691, -55.444086, -44.756742)),
        ("amun5", "E", (53.804696, -22.634508, 48.393900)),
        ("amun5", "F", (113.823589, -22.749404, 50.914183)),
        ("amun5", "G", (94.884800, -20.478356, 51.182224)),
        ("amun5", "G", (132.779799, -25.022542, 50.645895)),
        ("amun6", "H", (35.561720, -18.644472, 44.497642)),
        ("amun6", "H", (72.064443, -26.628213, 52.293739)),
    ]
    def unit(v):
        length = math.sqrt(sum(x*x for x in v))
        return [x / length for x in v]
    def cross(a, b):
        return [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]
    result = []
    for i, (part, family, origin) in enumerate(seats, 1):
        raw = families[family]
        y = unit([raw[r][1] for r in range(3)])
        z = unit([raw[r][2] for r in range(3)])
        x = unit(cross(y, z))
        z = unit(cross(x, y))
        matrix = [[x[r], y[r], z[r], origin[r]] for r in range(3)] + [[0, 0, 0, 1]]
        result.append({"name": f"{part} / breaching pod {i:02}", "part": "am4_14x", "matrix": matrix,
                       "evidence": "Matched triangular source socket and 13-unit height; underside seated on existing 0.5-unit support ridge. Kit has approximately 0.1-unit hidden press-fit tolerance at one wall; no mesh scaled or reshaped."})
    return result


def torpedo_covers():
    frames = [
        ("amun3", [[-.998966508487, .045452336798, .000000095590, 46.560184507057],
                   [.039898169521, .876894173547, .479024784817, 3.648644981447],
                   [.021772712032, .478529720581, -.877801375900, -34.634386828326], [0, 0, 0, 1]]),
        ("amun6", [[.888491034418, -.283076139071, .361180815171, 44.096922498033],
                   [-.436604499788, -.763774695218, .475420577709, -32.817396379092],
                   [.141280545453, -.580100090014, -.802199285117, 40.213548064511], [0, 0, 0, 1]]),
    ]
    return [{"name": mount + " / closed torpedo cover", "part": "am8c_2x", "matrix": matrix,
             "evidence": "All five asymmetric source socket walls match with uniform 0.10000-unit clearance, residual below 0.000003. Bottom local Y=0 seats on actual recess floor, 14.5 below mouth. Closed source variant retained, no superposed open carrier."}
            for mount, matrix in frames]


def hangar_cover():
    return [{"name": "amun2 / closed hangar cover", "part": "am7c", "matrix": [
        [.977286640946, -.000000055986, .211921734207, 12.476509006300],
        [.059486156939, -.959795861807, -.274323350792, -39.269311501935],
        [.203401618878, .280698955569, -.937995670449, -52.291377692964], [0, 0, 0, 1]],
        "evidence": "Underside flange Z=0 seats actual amun2 rim; four main inner-profile walls match within 0.000001, outer flange overlaps 1.5. Source corner chamfer clears unchamfered socket. Closed top Z=1, plug Z=-4 fits cavity floor Z=-26. Independent registrations agree within 0.000002; open alternative omitted."}]
