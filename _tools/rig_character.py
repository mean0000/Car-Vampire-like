import bpy, sys, math, mathutils
from mathutils import Vector

argv = sys.argv
glb_path = argv[argv.index("--") + 1]
out_fbx  = argv[argv.index("--") + 2]
render_base = argv[argv.index("--") + 3]
target_tris = 40000

# ---------------------------------------------------------------- import + clean
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb_path)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for m in meshes: m.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
obj.name = "PlayerMesh"

# normals + apply transforms
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

# decimate
me = obj.data; me.calc_loop_triangles()
cur = len(me.loop_triangles)
if cur > target_tris:
    d = obj.modifiers.new("Dec", 'DECIMATE'); d.ratio = target_tris/cur
    d.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=d.name)

def bbox():
    mins=[1e9]*3; maxs=[-1e9]*3
    for v in obj.bound_box:
        wv = obj.matrix_world @ Vector(v)
        for i in range(3): mins[i]=min(mins[i],wv[i]); maxs[i]=max(maxs[i],wv[i])
    return mins,maxs

mins,maxs = bbox()
H = 1.8
s = H/(maxs[2]-mins[2])
obj.scale=(s,s,s)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
mins,maxs = bbox()
obj.location.x -= (mins[0]+maxs[0])/2
obj.location.y -= (mins[1]+maxs[1])/2
obj.location.z -= mins[2]
bpy.ops.object.transform_apply(location=True,rotation=False,scale=False)
print("Mesh normalized. Height=1.8, feet at origin.")

# ---------------------------------------------------------------- build skeleton
# Z up, X lateral (+X = character LEFT), Y depth (-Y = front/forward)
def B(x,y,z): return Vector((x,y,z))
# (name, head, tail, parent)
bones = [
 ("Hips",        B(0,0,0.99),        B(0,0,1.12),        None),
 ("Spine",       B(0,0,1.12),        B(0,0,1.27),        "Hips"),
 ("Chest",       B(0,0,1.27),        B(0,0,1.44),        "Spine"),
 ("Neck",        B(0,0,1.47),        B(0,0,1.56),        "Chest"),
 ("Head",        B(0,0,1.56),        B(0,0,1.74),        "Neck"),

 ("LeftShoulder",B(0.04,0,1.42),     B(0.16,0,1.43),     "Chest"),
 ("LeftUpperArm",B(0.16,0,1.43),     B(0.48,0,1.41),     "LeftShoulder"),
 ("LeftLowerArm",B(0.48,0,1.41),     B(0.78,0,1.39),     "LeftUpperArm"),
 ("LeftHand",    B(0.78,0,1.39),     B(0.93,0,1.38),     "LeftLowerArm"),

 ("RightShoulder",B(-0.04,0,1.42),   B(-0.16,0,1.43),    "Chest"),
 ("RightUpperArm",B(-0.16,0,1.43),   B(-0.48,0,1.41),    "RightShoulder"),
 ("RightLowerArm",B(-0.48,0,1.41),   B(-0.78,0,1.39),    "RightUpperArm"),
 ("RightHand",   B(-0.78,0,1.39),    B(-0.93,0,1.38),    "RightLowerArm"),

 ("LeftUpperLeg",B(0.11,0,0.96),     B(0.12,-0.03,0.52), "Hips"),
 ("LeftLowerLeg",B(0.12,-0.03,0.52), B(0.13,0,0.10),     "LeftUpperLeg"),
 ("LeftFoot",    B(0.13,0,0.10),     B(0.13,-0.15,0.03), "LeftLowerLeg"),
 ("LeftToes",    B(0.13,-0.15,0.03), B(0.13,-0.21,0.02), "LeftFoot"),

 ("RightUpperLeg",B(-0.11,0,0.96),   B(-0.12,-0.03,0.52),"Hips"),
 ("RightLowerLeg",B(-0.12,-0.03,0.52),B(-0.13,0,0.10),   "RightUpperLeg"),
 ("RightFoot",   B(-0.13,0,0.10),    B(-0.13,-0.15,0.03),"RightLowerLeg"),
 ("RightToes",   B(-0.13,-0.15,0.03),B(-0.13,-0.21,0.02),"RightFoot"),
]

arm_data = bpy.data.armatures.new("PlayerArmature")
arm = bpy.data.objects.new("PlayerArmature", arm_data)
bpy.context.scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones
created = {}
for name,head,tail,parent in bones:
    b = eb.new(name); b.head=head; b.tail=tail; b.use_deform=True
    created[name]=b
    if parent: b.parent=created[parent]
    # connect spine/limb chains visually (not strictly needed)
bpy.ops.object.mode_set(mode='OBJECT')
print("Skeleton built:", len(bones), "bones")

# ---------------------------------------------------------------- auto skin
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True); arm.select_set(True)
bpy.context.view_layer.objects.active = arm
skinned_ok = True
try:
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    print("Auto (bone-heat) weights OK")
except RuntimeError as e:
    print("Bone-heat FAILED -> envelope:", e)
    skinned_ok = False
    bpy.ops.object.parent_set(type='ARMATURE_ENVELOPE')

# ---------------------------------------------------------------- coat clamp
# reassign baggy coat skirt verts (below crotch, far from legs) -> Hips rigid
def seg_dist(p, a, b):
    ab = b-a; t = (p-a).dot(ab)/max(ab.dot(ab),1e-9)
    t = max(0.0,min(1.0,t)); return (p-(a+ab*t)).length

leg_segs = [
 (B(0.11,0,0.96),B(0.12,-0.03,0.52)),(B(0.12,-0.03,0.52),B(0.13,0,0.10)),(B(0.13,0,0.10),B(0.13,-0.15,0.03)),
 (B(-0.11,0,0.96),B(-0.12,-0.03,0.52)),(B(-0.12,-0.03,0.52),B(-0.13,0,0.10)),(B(-0.13,0,0.10),B(-0.13,-0.15,0.03)),
]
CROTCH_Z = 0.94
LEG_R = 0.15
hips_vg = obj.vertex_groups.get("Hips") or obj.vertex_groups.new(name="Hips")
coat_count = 0
mw = obj.matrix_world
for v in obj.data.vertices:
    p = mw @ v.co
    if p.z < CROTCH_Z:
        dmin = min(seg_dist(p,a,b) for a,b in leg_segs)
        if dmin > LEG_R:  # baggy coat, not leg
            for g in list(v.groups):
                obj.vertex_groups[g.group].remove([v.index])
            hips_vg.add([v.index], 1.0, 'REPLACE')
            coat_count += 1
print("Coat skirt verts clamped to Hips:", coat_count)

# ---------------------------------------------------------------- export rest FBX
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL', path_mode='COPY', embed_textures=True,
    add_leaf_bones=False, bake_anim=False, mesh_smooth_type='FACE')
print("EXPORTED:", out_fbx)

# ---------------------------------------------------------------- stride test pose + render
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
def rot_bone(name, ax, deg):
    pb = arm.pose.bones[name]
    pb.rotation_mode='XYZ'
    cur = list(pb.rotation_euler)
    cur[ax]+= math.radians(deg); pb.rotation_euler=cur
rot_bone("LeftUpperLeg",0,35)    # forward stride
rot_bone("LeftLowerLeg",0,-25)
rot_bone("RightUpperLeg",0,-35)  # back stride
rot_bone("RightUpperArm",0,25)
rot_bone("LeftUpperArm",0,-25)
bpy.context.view_layer.update()
bpy.ops.object.mode_set(mode='OBJECT')

# render setup
sc=bpy.context.scene; sc.render.engine='BLENDER_EEVEE'; sc.render.resolution_x=512; sc.render.resolution_y=640
sc.world=bpy.data.worlds.new("W"); sc.world.use_nodes=True
sc.world.node_tree.nodes["Background"].inputs[0].default_value=(0.5,0.5,0.5,1)
for rot,e in [((math.radians(60),0,math.radians(30)),3.0),((math.radians(70),0,math.radians(210)),1.5)]:
    l=bpy.data.lights.new("S",'SUN'); l.energy=e; o=bpy.data.objects.new("S",l)
    sc.collection.objects.link(o); o.rotation_euler=rot
center=Vector((0,0,0.95))
def cam(loc, suffix):
    cd=bpy.data.cameras.new("c"); c=bpy.data.objects.new("c",cd); sc.collection.objects.link(c); sc.camera=c
    c.location=loc; d=center-Vector(loc); c.rotation_euler=d.to_track_quat('-Z','Y').to_euler(); cd.lens=55
    sc.render.filepath=render_base.replace(".png",f"_{suffix}.png"); bpy.ops.render.render(write_still=True)
    print("WROTE",sc.render.filepath); bpy.data.objects.remove(c)
cam((0,-3.2,1.1), "stride_front")
cam((2.2,-2.2,1.3), "stride_q")
cam((0,-0.5,3.6), "stride_top")
print("DONE")
