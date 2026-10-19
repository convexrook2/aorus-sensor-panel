"""Prepare Martin's CC BY 4.0 Pella mesh for the panel's triangle budget.

The source attribution belongs in Assets/Models/Source/Fleet and sources.json.
This helper does not create or change application assets. It returns meshes in
the source coordinate system: -X is forward, +Y is dorsal, engines are at +X.
Connected-component indices match the recorded original-mesh audit. No component
is discarded: even small, inward-facing cavity shells remain present.
"""
from __future__ import annotations

import argparse
import gzip
import hashlib
import io
import importlib.metadata
import json
import math
import os
from pathlib import Path
import sys
import time

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
DEPENDENCIES = ROOT / 'artifacts' / 'ship-model-build-deps'
if DEPENDENCIES.is_dir():
    sys.path.insert(0, str(DEPENDENCIES))
import numpy as np
import trimesh
import pymeshlab

SOURCE = ROOT / 'Assets/Models/Source/Fleet/Pella-Martin-CCBY4.stl.gz'
SOURCE_SHA256 = 'c06878f57e5b9f04043793226f8c6a06b082848273ea6fc9e9369f474c8dedd6'
EXPECTED_FACES = 1_513_390
EXPECTED_COMPONENTS = 291
CACHE = ROOT / 'artifacts/pella-mesh-cache'
_LAST_REPORT = {}


class _UnionFind:
    def __init__(self, count):
        self.parent = np.arange(count, dtype=np.int32)
        self.size = np.ones(count, dtype=np.int32)

    def find(self, value):
        parent = self.parent
        while parent[value] != value:
            parent[value] = parent[parent[value]]
            value = parent[value]
        return value

    def union(self, a, b):
        a, b = self.find(a), self.find(b)
        if a != b:
            if self.size[a] < self.size[b]:
                a, b = b, a
            self.parent[b] = a
            self.size[a] += self.size[b]

    def labels(self):
        return np.fromiter((self.find(i) for i in range(len(self.parent))),
                           dtype=np.int32, count=len(self.parent))


def _components(vertices, faces):
    # Same face order, union rule and NumPy tie order as whole-mesh-analysis.json.
    uf = _UnionFind(len(vertices))
    for a, b, c in faces:
        uf.union(a, b)
        uf.union(a, c)
    face_labels = uf.labels()[faces[:, 0]]
    ids, counts = np.unique(face_labels, return_counts=True)
    order = np.argsort(-counts)
    result = []
    for label in ids[order]:
        selected = faces[face_labels == label]
        original_ids, local_faces = np.unique(selected, return_inverse=True)
        result.append((vertices[original_ids], local_faces.reshape(-1, 3)))
    return result


def _budgets(counts, target):
    counts = np.asarray(counts, dtype=np.int64)
    # Preserve tiny structural/cavity components verbatim. Minimum budgets keep
    # every other component represented rather than sacrificing entire guns.
    base = np.where(counts <= 250, counts, np.minimum(counts, 72))
    if target < int(base.sum()):
        raise ValueError(f'Target {target} is below the component-preservation minimum {base.sum()}')
    if target >= int(counts.sum()):
        return counts.copy()
    weights = counts.astype(float) ** .95
    weights[counts <= 250] = 0
    weights[0] *= 1.25                     # Large forward hull and armour steps.
    weights[8:12] *= 1.7                   # Nacelle channels and hood bevels.
    weights[27:36] *= 1.3                  # Nine gun details.
    weights[41:50] *= 1.3                  # Nine turret assemblies.
    result = base.copy()
    while int(result.sum()) < target:
        capacity = counts - result
        active = capacity > 0
        w = np.where(active, weights, 0)
        if w.sum() == 0:
            break
        remaining = target - int(result.sum())
        shares = w / w.sum() * remaining
        add = np.minimum(np.floor(shares).astype(np.int64), capacity)
        if not add.any():
            # Stable ordering makes fractional rounding repeatable.
            order = np.argsort(-shares, kind='stable')
            for index in order:
                if capacity[index] and remaining:
                    add[index] = 1
                    remaining -= 1
        result += add
    return result


def split_smoothing_groups(vertices, faces, angle_degrees=35.0):
    """Split only hard edges; preserve smooth rounds without SciPy.

    Union face corners across manifold edges whose face normals meet within the
    smoothing threshold. The result's vertex topology encodes those groups, so
    Hull.export's area-weighted normal calculation respects them automatically.
    """
    vertices = np.asarray(vertices, dtype=np.float64)
    faces = np.asarray(faces, dtype=np.int64)
    triangles = vertices[faces]
    area = np.cross(triangles[:, 1] - triangles[:, 0], triangles[:, 2] - triangles[:, 0])
    lengths = np.linalg.norm(area, axis=1)
    normals = area / np.maximum(lengths[:, None], 1e-30)
    edge = faces[:, [[0, 1], [1, 2], [2, 0]]].reshape(-1, 2)
    corners = np.arange(len(faces) * 3, dtype=np.int32)
    first = corners.copy()
    second = np.repeat(np.arange(len(faces), dtype=np.int32) * 3, 3) + np.tile([1, 2, 0], len(faces))
    reverse = edge[:, 0] > edge[:, 1]
    edge = np.sort(edge, axis=1)
    endpoint_a = np.where(reverse, second, first)
    endpoint_b = np.where(reverse, first, second)
    order = np.lexsort((edge[:, 1], edge[:, 0]))
    sorted_edges = edge[order]
    starts = np.r_[0, np.flatnonzero(np.any(sorted_edges[1:] != sorted_edges[:-1], axis=1)) + 1]
    counts = np.diff(np.r_[starts, len(edge)])
    starts = starts[counts == 2]
    a, b = order[starts], order[starts + 1]
    smooth = np.einsum('ij,ij->i', normals[a // 3], normals[b // 3]) >= math.cos(math.radians(angle_degrees))
    uf = _UnionFind(len(corners))
    for left, right in zip(a[smooth], b[smooth]):
        uf.union(endpoint_a[left], endpoint_a[right])
        uf.union(endpoint_b[left], endpoint_b[right])
    _, representatives, inverse = np.unique(uf.labels(), return_index=True, return_inverse=True)
    result = trimesh.Trimesh(vertices=vertices[faces.ravel()[representatives]],
                             faces=inverse.reshape(-1, 3), process=False)
    # Also supply normals for callers exporting this helper's meshes directly.
    weighted = np.zeros_like(result.vertices)
    for corner in range(3):
        np.add.at(weighted, result.faces[:, corner], area)
    result.vertex_normals = weighted / np.maximum(np.linalg.norm(weighted, axis=1)[:, None], 1e-30)
    return result


def _valid_arrays(vertices, faces, label):
    if not np.isfinite(vertices).all():
        raise ValueError(f'{label}: nonfinite vertices')
    if not len(faces) or faces.min() < 0 or faces.max() >= len(vertices):
        raise ValueError(f'{label}: empty or invalid face indices')
    triangle = vertices[faces]
    area2 = np.linalg.norm(np.cross(triangle[:, 1] - triangle[:, 0], triangle[:, 2] - triangle[:, 0]), axis=1)
    if np.any(area2 <= 1e-10):
        raise ValueError(f'{label}: zero-area triangles')


def _read_cache(path):
    with np.load(path, allow_pickle=False) as data:
        vertices, faces = data['vertices'], data['faces']
        vo, fo = data['vertex_offsets'], data['face_offsets']
        return [(i, trimesh.Trimesh(vertices=vertices[vo[i]:vo[i + 1]].copy(),
                                    faces=faces[fo[i]:fo[i + 1]].copy(), process=False))
                for i in range(len(vo) - 1)]


def load_pella_meshes(source_path=None, target_faces=140_000, smoothing_angle=35.0, *, use_cache=True):
    """Return [(stable_component_index, mesh), ...] in original coordinates.

    Cache storage is confined to ignored artifacts. The source is verified even
    for a cache hit. No reference screenshots or production mesh are consumed.
    """
    global _LAST_REPORT
    started = time.perf_counter()
    source = Path(source_path) if source_path is not None else SOURCE
    raw = gzip.decompress(source.read_bytes()) if source.suffix.lower() == '.gz' else source.read_bytes()
    sha = hashlib.sha256(raw).hexdigest()
    if sha != SOURCE_SHA256:
        raise ValueError('Pella source SHA256 differs from the reviewed CC BY 4.0 STL')
    if not 0 < smoothing_angle < 90:
        raise ValueError('smoothing_angle must lie between 0 and 90 degrees')
    settings = dict(source_sha256=sha, target_faces=int(target_faces), smoothing_angle=float(smoothing_angle),
                    helper_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                    numpy=np.__version__, trimesh=trimesh.__version__,
                    pymeshlab=importlib.metadata.version('pymeshlab'))
    cache_key = hashlib.sha256(json.dumps(settings, sort_keys=True).encode()).hexdigest()[:24]
    cache_path, report_path = CACHE / f'{cache_key}.npz', CACHE / f'{cache_key}.json'
    if use_cache and cache_path.exists() and report_path.exists():
        meshes = _read_cache(cache_path)
        _LAST_REPORT = json.loads(report_path.read_text())
        _LAST_REPORT.update(cache_hit=True, elapsed_seconds=time.perf_counter() - started)
        return meshes
    original = trimesh.load(io.BytesIO(raw), file_type='stl', force='mesh', process=False)
    del raw
    if len(original.faces) != EXPECTED_FACES:
        raise ValueError(f'Expected {EXPECTED_FACES} source triangles')
    original.merge_vertices()
    _valid_arrays(original.vertices, original.faces, 'source')
    components = _components(np.asarray(original.vertices), np.asarray(original.faces))
    if len(components) != EXPECTED_COMPONENTS:
        raise ValueError(f'Expected {EXPECTED_COMPONENTS} source components, got {len(components)}')
    budgets = _budgets([len(f) for _, f in components], int(target_faces))
    meshes, records = [], []
    for index, ((vertices, faces), budget) in enumerate(zip(components, budgets)):
        source_bounds = np.array([vertices.min(axis=0), vertices.max(axis=0)])
        if budget < len(faces):
            # CAD seams have nonmanifold vertices which lock simpler QEM
            # implementations. MeshLab handles them while respecting boundary
            # positions, face orientation, and broad planar armour surfaces.
            simplifier = pymeshlab.MeshSet()
            simplifier.add_mesh(pymeshlab.Mesh(vertex_matrix=vertices, face_matrix=faces))
            simplifier.meshing_decimation_quadric_edge_collapse(
                targetfacenum=int(budget), preserveboundary=True,
                preservenormal=True, planarquadric=True, autoclean=True)
            reduced = simplifier.current_mesh()
            vv, ff = reduced.vertex_matrix(), reduced.face_matrix()
            if len(ff) > budget:
                raise ValueError(f'Component {index}: simplifier could not reach its {budget}-triangle budget')
        else:
            vv, ff = vertices.copy(), faces.copy()
        _valid_arrays(vv, ff, f'component {index}')
        mesh = split_smoothing_groups(vv, ff, smoothing_angle)
        mesh.metadata.update(pella_component_index=index, source_triangles=len(faces))
        meshes.append((index, mesh))
        records.append(dict(index=index, source_triangles=len(faces), target_triangles=int(budget),
                            triangles=len(ff), vertices=len(mesh.vertices), preserved=bool(budget == len(faces)),
                            bounds=mesh.bounds.tolist(),
                            bounds_max_delta=float(np.abs(mesh.bounds - source_bounds).max())))
    total = sum(len(mesh.faces) for _, mesh in meshes)
    if total > target_faces:
        raise ValueError(f'Pella output exceeds {target_faces} triangles')
    _LAST_REPORT = dict(settings=settings, source_faces=EXPECTED_FACES, source_vertices=len(original.vertices),
                        source_components=len(components), triangles=total,
                        vertices=sum(len(mesh.vertices) for _, mesh in meshes),
                        source_bounds=original.bounds.tolist(),
                        output_bounds=[np.min([m.bounds[0] for _, m in meshes], axis=0).tolist(),
                                       np.max([m.bounds[1] for _, m in meshes], axis=0).tolist()],
                        finite=True, zero_area_faces=0, components=records, cache_hit=False,
                        elapsed_seconds=time.perf_counter() - started)
    if use_cache:
        CACHE.mkdir(parents=True, exist_ok=True)
        temp = CACHE / f'{cache_key}.{os.getpid()}.tmp.npz'
        np.savez_compressed(temp, vertices=np.vstack([m.vertices for _, m in meshes]),
                            faces=np.vstack([m.faces for _, m in meshes]),
                            vertex_offsets=np.r_[0, np.cumsum([len(m.vertices) for _, m in meshes])],
                            face_offsets=np.r_[0, np.cumsum([len(m.faces) for _, m in meshes])])
        os.replace(temp, cache_path)
        temp_report = CACHE / f'{cache_key}.{os.getpid()}.tmp.json'
        temp_report.write_text(json.dumps(_LAST_REPORT, indent=2), encoding='utf-8')
        os.replace(temp_report, report_path)
    return meshes


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path)
    parser.add_argument('--target-faces', type=int, default=140_000)
    parser.add_argument('--smoothing-angle', type=float, default=35.0)
    parser.add_argument('--report', type=Path)
    parser.add_argument('--no-cache', action='store_true')
    args = parser.parse_args()
    load_pella_meshes(args.source, args.target_faces, args.smoothing_angle, use_cache=not args.no_cache)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(_LAST_REPORT, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in _LAST_REPORT.items() if k != 'components'}, indent=2))
