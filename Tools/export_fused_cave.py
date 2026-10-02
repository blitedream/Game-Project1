import os
import math
import sys

import bpy
from mathutils import Vector


def clamp01(value):
    return max(0.0, min(1.0, value))


def mix(a, b, factor):
    factor = clamp01(factor)
    return tuple(a[i] * (1.0 - factor) + b[i] * factor for i in range(3))


def color_lower_cave(mesh_objects):
    """Give the untextured lower cave a photogrammetry-style rock palette.

    The source mesh has no UVs, so a vertex-colour material is the reliable
    way to preserve colour through GLB export without altering the .blend.
    """
    cave = next((obj for obj in mesh_objects if obj.name == "imagetostl_mesh0"), None)
    if cave is None:
        raise RuntimeError("The fused file is missing the lower cave mesh")

    mesh = cave.data
    old = mesh.color_attributes.get("CaveRockColor")
    if old is not None:
        mesh.color_attributes.remove(old)
    colors = mesh.color_attributes.new(
        name="CaveRockColor", type="BYTE_COLOR", domain="POINT"
    )

    world_bounds = [cave.matrix_world @ Vector(corner) for corner in cave.bound_box]
    min_z = min(position.z for position in world_bounds)
    max_z = max(position.z for position in world_bounds)
    height_span = max(0.001, max_z - min_z)
    normal_matrix = cave.matrix_world.to_3x3().inverted().transposed()

    # Muted colours sampled by eye from the upper Kelkaya photogrammetry:
    # limestone, earth, damp blue-grey rock and sparse moss near the opening.
    wet_rock = (0.055, 0.073, 0.072)
    grey_rock = (0.205, 0.205, 0.180)
    limestone = (0.390, 0.345, 0.270)
    moss = (0.105, 0.185, 0.070)

    flat_colors = []
    for vertex in mesh.vertices:
        position = cave.matrix_world @ vertex.co
        height = clamp01((position.z - min_z) / height_span)
        normal = (normal_matrix @ vertex.normal).normalized()
        upward = clamp01(normal.z * 0.5 + 0.5)

        broad_noise = (
            math.sin(position.x * 0.23 + position.y * 0.17) * 0.50
            + math.sin(position.y * 0.51 - position.z * 0.19) * 0.30
            + math.sin((position.x + position.z) * 0.83) * 0.20
        ) * 0.5 + 0.5
        fine_noise = math.sin(position.x * 2.31 + position.y * 1.67 + position.z * 1.13) * 0.5 + 0.5
        variation = clamp01(broad_noise * 0.72 + fine_noise * 0.28)

        dry_amount = clamp01((height - 0.36) * 1.55 + upward * 0.28)
        base = mix(wet_rock, grey_rock, dry_amount)
        base = mix(base, limestone, clamp01((variation - 0.46) * 1.15) * dry_amount)

        moss_amount = clamp01((height - 0.73) * 3.1) * clamp01(upward + 0.15)
        moss_amount *= clamp01((broad_noise - 0.42) * 2.4) * 0.62
        base = mix(base, moss, moss_amount)

        shade = 0.74 + variation * 0.34
        color = tuple(clamp01(channel * shade) for channel in base)
        flat_colors.extend((*color, 1.0))

    colors.data.foreach_set("color", flat_colors)

    material = bpy.data.materials.get("Fused Cave Photogrammetry Rock")
    if material is None:
        material = bpy.data.materials.new("Fused Cave Photogrammetry Rock")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    vertex_color = nodes.new("ShaderNodeVertexColor")
    vertex_color.layer_name = colors.name
    material.node_tree.links.new(vertex_color.outputs["Color"], shader.inputs["Base Color"])
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    shader.inputs["Roughness"].default_value = 0.88
    shader.inputs["Metallic"].default_value = 0.0

    mesh.materials.clear()
    mesh.materials.append(material)


def main():
    if "--" not in sys.argv:
        raise RuntimeError("Expected the output GLB path after --")

    output_path = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
    os.makedirs(os.path.dirname(output_path), exist_ok=True)

    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("The fused cave file contains no mesh objects")

    color_lower_cave(meshes)

    # Export only the finished geometry. The .blend also contains its authoring
    # cameras, lamps, and hundreds of photogrammetry helper empties; none of
    # those belong in the Unity level and imported cameras can cover Game view.
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.hide_viewport = False
        obj.hide_render = False
        # Repair invalid/duplicate mesh records in the export copy held in
        # memory. The source .blend is never saved or modified.
        obj.data.validate(clean_customdata=False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]

    bpy.ops.export_scene.gltf(
        filepath=output_path,
        export_format="GLB",
        use_selection=True,
        export_cameras=False,
        export_lights=False,
        export_apply=True,
    )
    print(f"CODEX_EXPORTED {output_path} meshes={len(meshes)}")


if __name__ == "__main__":
    main()
