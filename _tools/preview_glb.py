import bpy, sys, os, math, mathutils

argv = sys.argv
glb_path = argv[argv.index("--") + 1]
out_path = argv[argv.index("--") + 2]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb_path)

meshes = [o for o in bpy.data.objects if o.type == 'MESH']

# compute bbox center + size
mins = [1e9]*3; maxs = [-1e9]*3
for m in meshes:
    for v in m.bound_box:
        wv = m.matrix_world @ mathutils.Vector(v)
        for i in range(3):
            mins[i]=min(mins[i],wv[i]); maxs[i]=max(maxs[i],wv[i])
center = mathutils.Vector([(mins[i]+maxs[i])/2 for i in range(3)])
size = max(maxs[i]-mins[i] for i in range(3))

scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 512
scene.render.resolution_y = 768
scene.render.film_transparent = False
scene.world = bpy.data.worlds.new("W")
scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.5,0.5,0.5,1)
scene.world.node_tree.nodes["Background"].inputs[1].default_value = 1.0

# lights
def add_sun(rot, energy):
    l = bpy.data.lights.new("S", 'SUN'); l.energy = energy
    o = bpy.data.objects.new("S", l); scene.collection.objects.link(o)
    o.rotation_euler = rot
add_sun((math.radians(60),0,math.radians(30)), 3.0)
add_sun((math.radians(70),0,math.radians(210)), 1.5)

def render_cam(name, angle_deg, suffix):
    cam_data = bpy.data.cameras.new(name); cam = bpy.data.objects.new(name, cam_data)
    scene.collection.objects.link(cam); scene.camera = cam
    a = math.radians(angle_deg)
    dist = size * 1.6
    cam.location = center + mathutils.Vector((math.sin(a)*dist, -math.cos(a)*dist, size*0.05))
    # look at center
    d = center - cam.location
    cam.rotation_euler = d.to_track_quat('-Z','Y').to_euler()
    cam_data.lens = 50
    scene.render.filepath = out_path.replace(".png", f"_{suffix}.png")
    bpy.ops.render.render(write_still=True)
    print("WROTE", scene.render.filepath)
    bpy.data.objects.remove(cam)

render_cam("front", 0, "front")
render_cam("side", 90, "side")
render_cam("q", 35, "quarter")
print("DONE")
