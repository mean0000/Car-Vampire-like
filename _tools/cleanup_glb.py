import bpy, sys, os, math, mathutils

argv = sys.argv
glb_path = argv[argv.index("--") + 1]
out_fbx  = argv[argv.index("--") + 2]
target_tris = int(argv[argv.index("--") + 3])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb_path)

meshes = [o for o in bpy.data.objects if o.type == 'MESH']
print("Imported meshes:", [m.name for m in meshes])

# join into one if multiple
bpy.ops.object.select_all(action='DESELECT')
for m in meshes:
    m.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active

# --- normals recalc ---
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')

# --- apply transforms (rotation+scale) ---
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

# --- current tri count ---
me = obj.data
me.calc_loop_triangles()
cur_tris = len(me.loop_triangles)
print("Current tris:", cur_tris)

# --- decimate to target ---
if cur_tris > target_tris:
    ratio = target_tris / cur_tris
    dec = obj.modifiers.new("Decimate", 'DECIMATE')
    dec.decimate_type = 'COLLAPSE'
    dec.ratio = ratio
    dec.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=dec.name)
    me = obj.data
    me.calc_loop_triangles()
    print("After decimate tris:", len(me.loop_triangles), "(ratio %.3f)" % ratio)

# --- normalize height to ~1.8 along Z (up) ---
mins=[1e9]*3; maxs=[-1e9]*3
for v in obj.bound_box:
    wv = obj.matrix_world @ mathutils.Vector(v)
    for i in range(3):
        mins[i]=min(mins[i],wv[i]); maxs[i]=max(maxs[i],wv[i])
height = maxs[2]-mins[2]
target_h = 1.8
s = target_h/height
obj.scale = (s,s,s)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

# place feet at origin, centered XY
mins=[1e9]*3; maxs=[-1e9]*3
for v in obj.bound_box:
    wv = obj.matrix_world @ mathutils.Vector(v)
    for i in range(3):
        mins[i]=min(mins[i],wv[i]); maxs[i]=max(maxs[i],wv[i])
cx=(mins[0]+maxs[0])/2; cy=(mins[1]+maxs[1])/2
obj.location.x -= cx; obj.location.y -= cy; obj.location.z -= mins[2]
bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

print("Final height (Z):", maxs[2]-mins[2], "-> normalized to", target_h)

# --- export FBX with textures embedded ---
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.fbx(
    filepath=out_fbx,
    use_selection=True,
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL',
    path_mode='COPY',
    embed_textures=True,
    mesh_smooth_type='FACE',
    add_leaf_bones=False,
)
print("EXPORTED:", out_fbx)
print("DONE")
