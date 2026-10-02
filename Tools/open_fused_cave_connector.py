"""Open the vertical connector between Kelkaya and the lower Zacaton cave.

The fused GLB already contains both authored maps.  The remaining obstruction
is made of triangles crossing the shared sinkhole shaft.  This tool removes
only triangles inside that connector cylinder and preserves every original
vertex stream, material, texture and node transform.
"""

from __future__ import annotations

import argparse
import copy
from pathlib import Path

import numpy as np
from pygltflib import Accessor, BufferView, GLTF2


COMPONENT_DTYPES = {
    5121: np.dtype("<u1"),
    5122: np.dtype("<i2"),
    5123: np.dtype("<u2"),
    5125: np.dtype("<u4"),
    5126: np.dtype("<f4"),
}
TYPE_COUNTS = {
    "SCALAR": 1,
    "VEC2": 2,
    "VEC3": 3,
    "VEC4": 4,
    "MAT2": 4,
    "MAT3": 9,
    "MAT4": 16,
}


def accessor_array(gltf: GLTF2, blob: bytes, accessor_index: int) -> np.ndarray:
    accessor = gltf.accessors[accessor_index]
    view = gltf.bufferViews[accessor.bufferView]
    dtype = COMPONENT_DTYPES[accessor.componentType]
    width = TYPE_COUNTS[accessor.type]
    offset = (view.byteOffset or 0) + (accessor.byteOffset or 0)
    packed_stride = dtype.itemsize * width
    stride = view.byteStride or packed_stride

    if stride == packed_stride:
        values = np.frombuffer(blob, dtype=dtype, count=accessor.count * width, offset=offset)
        return values.reshape((accessor.count, width))

    return np.ndarray(
        shape=(accessor.count, width),
        dtype=dtype,
        buffer=blob,
        offset=offset,
        strides=(stride, dtype.itemsize),
    )


def node_matrix(node) -> np.ndarray:
    if node.matrix:
        return np.asarray(node.matrix, dtype=np.float64).reshape((4, 4), order="F")

    translation = np.asarray(node.translation or [0.0, 0.0, 0.0], dtype=np.float64)
    scale = np.asarray(node.scale or [1.0, 1.0, 1.0], dtype=np.float64)
    x, y, z, w = node.rotation or [0.0, 0.0, 0.0, 1.0]
    rotation = np.array(
        [
            [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w), 0],
            [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w), 0],
            [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y), 0],
            [0, 0, 0, 1],
        ],
        dtype=np.float64,
    )
    transform = np.eye(4, dtype=np.float64)
    transform[:3, 3] = translation
    scaling = np.diag([scale[0], scale[1], scale[2], 1.0])
    return transform @ rotation @ scaling


def collect_mesh_transforms(gltf: GLTF2) -> dict[int, np.ndarray]:
    result: dict[int, np.ndarray] = {}

    def visit(node_index: int, parent: np.ndarray) -> None:
        node = gltf.nodes[node_index]
        world = parent @ node_matrix(node)
        if node.mesh is not None and node.mesh not in result:
            result[node.mesh] = world
        for child in node.children or []:
            visit(child, world)

    scene_index = gltf.scene or 0
    for root in gltf.scenes[scene_index].nodes or []:
        visit(root, np.eye(4, dtype=np.float64))
    return result


def append_indices(gltf: GLTF2, blob: bytearray, values: np.ndarray, component_type: int) -> int:
    while len(blob) % 4:
        blob.append(0)
    byte_offset = len(blob)
    dtype = COMPONENT_DTYPES[component_type]
    packed = np.asarray(values, dtype=dtype).reshape(-1).tobytes()
    blob.extend(packed)

    view_index = len(gltf.bufferViews)
    gltf.bufferViews.append(
        BufferView(buffer=0, byteOffset=byte_offset, byteLength=len(packed), target=34963)
    )
    accessor_index = len(gltf.accessors)
    flat = np.asarray(values).reshape(-1)
    gltf.accessors.append(
        Accessor(
            bufferView=view_index,
            byteOffset=0,
            componentType=component_type,
            count=len(flat),
            type="SCALAR",
            min=[int(flat.min())] if len(flat) else [0],
            max=[int(flat.max())] if len(flat) else [0],
        )
    )
    return accessor_index


def open_connector(
    input_path: Path,
    output_path: Path,
    center_x: float,
    center_y: float,
    center_z: float,
    radius: float,
    bottom_y: float,
    top_y: float,
) -> tuple[int, int]:
    gltf = GLTF2().load_binary(str(input_path))
    original_blob = gltf.binary_blob()
    blob = bytearray(original_blob)
    mesh_transforms = collect_mesh_transforms(gltf)
    removed_total = 0
    changed_primitives = 0

    for mesh_index, mesh in enumerate(gltf.meshes):
        world = mesh_transforms.get(mesh_index)
        if world is None:
            continue

        for primitive in mesh.primitives:
            if primitive.mode not in (None, 4) or primitive.indices is None:
                continue
            position_index = getattr(primitive.attributes, "POSITION", None)
            if position_index is None:
                continue

            positions = accessor_array(gltf, original_blob, position_index).astype(np.float64, copy=False)
            indices_accessor = gltf.accessors[primitive.indices]
            indices = accessor_array(gltf, original_blob, primitive.indices).reshape(-1)
            if len(indices) % 3:
                continue
            faces = indices.reshape((-1, 3))

            homogeneous = np.concatenate((positions[:, :3], np.ones((len(positions), 1))), axis=1)
            world_positions = (homogeneous @ world.T)[:, :3]
            triangles = world_positions[faces]
            centroids = triangles.mean(axis=1)
            # Test the whole projected triangle, not just its centroid: the
            # lower cave's lid is a few large triangles spanning the opening.
            projected = triangles[:, :, [0, 2]] - np.array([center_x, center_z])
            radial_sq = np.full(len(faces), np.inf)
            crosses = []
            for edge in range(3):
                a = projected[:, edge]
                d = projected[:, (edge + 1) % 3] - a
                t = np.clip(-np.sum(a*d, axis=1) / np.maximum(np.sum(d*d, axis=1), 1e-20), 0, 1)
                radial_sq = np.minimum(radial_sq, np.sum((a + t[:, None]*d)**2, axis=1))
                crosses.append(a[:, 0]*d[:, 1] - a[:, 1]*d[:, 0])
            crosses = np.array(crosses)
            inside = np.all(crosses >= 0, axis=0) | np.all(crosses <= 0, axis=0)
            radial_sq[inside] = 0
            cut = (
                (radial_sq <= radius * radius)
                & (centroids[:, 1] >= bottom_y)
                & (centroids[:, 1] <= top_y)
            )
            removed = int(np.count_nonzero(cut))
            lower_shell = mesh.name == "imagetostl_mesh0"
            if removed == 0 and not lower_shell:
                continue

            kept_faces = faces[~cut]
            if lower_shell:
                # The STL is an outward-facing solid. This is a playable
                # interior: PhysX must see the floor/walls from inside it.
                kept_faces = kept_faces[:, ::-1]
                normal_index = getattr(primitive.attributes, "NORMAL", None)
                if normal_index is not None:
                    normals = -accessor_array(gltf, original_blob, normal_index)
                    while len(blob) % 4: blob.append(0)
                    offset = len(blob)
                    packed = np.asarray(normals, dtype='<f4').tobytes()
                    blob.extend(packed)
                    view_index = len(gltf.bufferViews)
                    gltf.bufferViews.append(BufferView(buffer=0,byteOffset=offset,byteLength=len(packed),target=34962))
                    accessor = copy.deepcopy(gltf.accessors[normal_index])
                    accessor.bufferView = view_index
                    accessor.byteOffset = 0
                    accessor.min = None
                    accessor.max = None
                    primitive.attributes.NORMAL = len(gltf.accessors)
                    gltf.accessors.append(accessor)
                if primitive.material is not None:
                    gltf.materials[primitive.material].doubleSided = True
            primitive.indices = append_indices(
                gltf, blob, kept_faces, indices_accessor.componentType
            )
            removed_total += removed
            changed_primitives += 1
            print(f"opened mesh={mesh.name or mesh_index} removed_triangles={removed}")

    if removed_total == 0:
        raise RuntimeError("No connector triangles matched; refusing to overwrite the map")

    gltf.buffers[0].byteLength = len(blob)
    gltf.set_binary_blob(bytes(blob))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    gltf.save_binary(str(output_path))
    return removed_total, changed_primitives


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--center-x", type=float, default=-293.0)
    parser.add_argument("--center-y", type=float, default=455.59)
    parser.add_argument("--center-z", type=float, default=77.0)
    parser.add_argument("--radius", type=float, default=6.0)
    parser.add_argument("--bottom-y", type=float, default=447.0)
    parser.add_argument("--top-y", type=float, default=457.0)
    args = parser.parse_args()

    removed, changed = open_connector(
        args.input,
        args.output,
        args.center_x,
        args.center_y,
        args.center_z,
        args.radius,
        args.bottom_y,
        args.top_y,
    )
    print(f"CONNECTOR_OPEN removed={removed} changed_primitives={changed} output={args.output}")


if __name__ == "__main__":
    main()
