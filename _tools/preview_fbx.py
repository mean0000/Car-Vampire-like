import bpy, sys, math, mathutils
argv=sys.argv; fbx=argv[argv.index("--")+1]; out=argv[argv.index("--")+2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
meshes=[o for o in bpy.data.objects if o.type=='MESH']
mins=[1e9]*3;maxs=[-1e9]*3
for m in meshes:
  for v in m.bound_box:
    wv=m.matrix_world@mathutils.Vector(v)
    for i in range(3): mins[i]=min(mins[i],wv[i]);maxs[i]=max(maxs[i],wv[i])
center=mathutils.Vector([(mins[i]+maxs[i])/2 for i in range(3)]);size=max(maxs[i]-mins[i] for i in range(3))
sc=bpy.context.scene; sc.render.engine='BLENDER_EEVEE'; sc.render.resolution_x=512; sc.render.resolution_y=768
sc.world=bpy.data.worlds.new("W");sc.world.use_nodes=True
sc.world.node_tree.nodes["Background"].inputs[0].default_value=(0.5,0.5,0.5,1)
for rot,e in [((math.radians(60),0,math.radians(30)),3.0),((math.radians(70),0,math.radians(210)),1.5)]:
  l=bpy.data.lights.new("S",'SUN');l.energy=e;o=bpy.data.objects.new("S",l);sc.collection.objects.link(o);o.rotation_euler=rot
def cam(angle,suf):
  cd=bpy.data.cameras.new("c");c=bpy.data.objects.new("c",cd);sc.collection.objects.link(c);sc.camera=c
  a=math.radians(angle);dist=size*1.6
  c.location=center+mathutils.Vector((math.sin(a)*dist,-math.cos(a)*dist,size*0.18))
  d=center-c.location;c.rotation_euler=d.to_track_quat('-Z','Y').to_euler();cd.lens=70
  sc.render.filepath=out.replace(".png",f"_{suf}.png");bpy.ops.render.render(write_still=True)
  print("WROTE",sc.render.filepath);bpy.data.objects.remove(c)
cam(0,"front");cam(20,"face")
print("DONE")
