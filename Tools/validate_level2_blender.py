import bpy,math,json
from mathutils import Vector
from mathutils.bvhtree import BVHTree
verts=[]; faces=[]
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 offset=len(verts); verts.extend(o.matrix_world@v.co for v in o.data.vertices); faces.extend([offset+i for i in p.vertices] for p in o.data.polygons)
tree=BVHTree.FromPolygons(verts,faces)
miss=[]
for z in [455.5,455,454.5,454,453,452,451,450,449,448,447,446,445,440,435,430]:
 for i in range(72):
  a=i*2*math.pi/72
  if tree.ray_cast(Vector((-293,-77,z)),Vector((math.cos(a),math.sin(a),0)),50)[0] is None:miss.append([z,i])
blocked=[]
for x,y in [(0,0),(2,0),(-2,0),(0,2),(0,-2)]:
 hit=tree.ray_cast(Vector((-293+x,-77+y,458)),Vector((0,0,-1)),30)
 if hit[0] is not None:blocked.append(list(hit[0]))
out='D:/Unity/GP1/Artifacts/Level2Blender/'
open(out+'validation.json','w').write(json.dumps({'radial_checks':1152,'leaks':miss,'descent_blockers':blocked},indent=2))
scene=bpy.context.scene; scene.render.engine='BLENDER_WORKBENCH'; scene.render.resolution_x=1200; scene.render.resolution_y=800; scene.render.resolution_percentage=100
scene.display.shading.light='STUDIO'; scene.display.shading.color_type='MATERIAL'; scene.display.shading.show_cavity=True
camdata=bpy.data.cameras.new('Review'); cam=bpy.data.objects.new('Review',camdata); scene.collection.objects.link(cam); scene.camera=cam
for name,position,target in [('connection',(-293,-77,454),(-284,-76,450)),('overview',(-258,-120,493),(-293,-77,450))]:
 cam.location=position; cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();camdata.lens=28;camdata.clip_end=1000
 scene.render.filepath=out+name+'.png';bpy.ops.render.render(write_still=True)
assert not miss and not blocked,(miss,blocked)
