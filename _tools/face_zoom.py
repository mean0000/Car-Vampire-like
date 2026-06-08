import bpy, sys, math, mathutils
argv=sys.argv; glb=argv[argv.index("--")+1]; out=argv[argv.index("--")+2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
meshes=[o for o in bpy.data.objects if o.type=='MESH']
# find head top z
mins=[1e9]*3;maxs=[-1e9]*3
for m in meshes:
  for v in m.bound_box:
    wv=m.matrix_world@mathutils.Vector(v)
    for i in range(3): mins[i]=min(mins[i],wv[i]);maxs[i]=max(maxs[i],wv[i])
H=maxs[2]
# face ~ 0.90*H..0.97*H
face_z=H*0.90
center=mathutils.Vector((0,0,face_z))
sc=bpy.context.scene;sc.render.engine='BLENDER_EEVEE';sc.render.resolution_x=512;sc.render.resolution_y=512
sc.world=bpy.data.worlds.new("W");sc.world.use_nodes=True
sc.world.node_tree.nodes["Background"].inputs[0].default_value=(0.5,0.5,0.5,1)
for rot,e in [((math.radians(75),0,math.radians(20)),3.0),((math.radians(80),0,math.radians(200)),1.2)]:
  l=bpy.data.lights.new("S",'SUN');l.energy=e;o=bpy.data.objects.new("S",l);sc.collection.objects.link(o);o.rotation_euler=rot
def cam(loc,suf,lens):
  cd=bpy.data.cameras.new("c");c=bpy.data.objects.new("c",cd);sc.collection.objects.link(c);sc.camera=c
  c.location=loc;d=center-mathutils.Vector(loc);c.rotation_euler=d.to_track_quat('-Z','Y').to_euler();cd.lens=lens
  sc.render.filepath=out.replace(".png",f"_{suf}.png");bpy.ops.render.render(write_still=True);print("WROTE",sc.render.filepath);bpy.data.objects.remove(c)
cam((0,-0.55,face_z+0.02),"front",80)
cam((0.4,-0.5,face_z+0.05),"q",80)
print("DONE")
