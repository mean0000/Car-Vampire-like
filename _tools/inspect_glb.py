import bpy
import sys
import os

# arg after '--'
argv = sys.argv
glb_path = argv[argv.index("--") + 1]

# clean scene
bpy.ops.wm.read_factory_settings(use_empty=True)

bpy.ops.import_scene.gltf(filepath=glb_path)

print("=" * 50)
print("FILE:", os.path.basename(glb_path))
print("=" * 50)

meshes = [o for o in bpy.data.objects if o.type == 'MESH']
armatures = [o for o in bpy.data.objects if o.type == 'ARMATURE']

print(f"Objects total: {len(bpy.data.objects)}")
print(f"Mesh objects: {len(meshes)}")
print(f"Armatures: {len(armatures)}")

total_tris = 0
total_verts = 0
for m in meshes:
    me = m.data
    me.calc_loop_triangles()
    tris = len(me.loop_triangles)
    verts = len(me.vertices)
    total_tris += tris
    total_verts += verts
    # world-space bounds
    print(f"  - '{m.name}': verts={verts} tris={tris} mats={[ms.name for ms in m.data.materials]}")

print(f"TOTAL verts={total_verts} tris={total_tris}")

# overall bounding box (world)
import mathutils
mins = [1e9, 1e9, 1e9]
maxs = [-1e9, -1e9, -1e9]
for m in meshes:
    for v in m.bound_box:
        wv = m.matrix_world @ mathutils.Vector(v)
        for i in range(3):
            mins[i] = min(mins[i], wv[i])
            maxs[i] = max(maxs[i], wv[i])
dims = [maxs[i] - mins[i] for i in range(3)]
print(f"BBOX min={['%.3f'%x for x in mins]} max={['%.3f'%x for x in maxs]}")
print(f"DIMS (X,Y,Z) = {['%.3f'%x for x in dims]}")
print(f"Height-ish (max dim) = {max(dims):.3f}")

# materials / images
print(f"Materials: {[m.name for m in bpy.data.materials]}")
print(f"Images: {[(im.name, im.size[0], im.size[1]) for im in bpy.data.images]}")
print("DONE")
