"""Repair the connector in Blender, retaining UVs and the authored cave meshes."""
import bpy, bmesh, math, os, sys, json
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
sys.path.insert(0,os.path.dirname(__file__))
from export_fused_cave import color_lower_cave
out='D:/Unity/GP1/Artifacts/Level2Blender'
os.makedirs(out,exist_ok=True)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
cx,cy=-293.,-77.
radius=6.; bottom=446.5; top=457.01; count=48
report=[]
for o in meshes:
 world=o.matrix_world.copy(); o.parent=None; o.matrix_world=Matrix.Identity(4)
 o.data.transform(world)
 bm=bmesh.new(); bm.from_mesh(o.data)
 candidates=[f for f in bm.faces if min(v.co.z for v in f.verts)<=top and max(v.co.z for v in f.verts)>=bottom and min(v.co.x for v in f.verts)<=cx+radius and max(v.co.x for v in f.verts)>=cx-radius and min(v.co.y for v in f.verts)<=cy+radius and max(v.co.y for v in f.verts)>=cy-radius]
 if candidates:
  geom=set(candidates)
  for f in candidates: geom.update(f.edges); geom.update(f.verts)
  planes=[((cx,cy,bottom),(0,0,1)),((cx,cy,top),(0,0,1))]
  for i in range(count):
   a=2*math.pi*i/count; n=(math.cos(a),math.sin(a),0)
   planes.append(((cx+radius*n[0],cy+radius*n[1],0),n))
  for p,n in planes:
   result=bmesh.ops.bisect_plane(bm,geom=[g for g in geom if g.is_valid],dist=0.00001,plane_co=p,plane_no=n,clear_inner=False,clear_outer=False)
   geom.update(result['geom']); geom.update(result['geom_cut'])
  remove=[]
  for f in geom:
   if not isinstance(f,bmesh.types.BMFace) or not f.is_valid: continue
   c=f.calc_center_median()
   if bottom-1e-5<=c.z<=top+1e-5 and all((c.x-cx)*math.cos(i*2*math.pi/count)+(c.y-cy)*math.sin(i*2*math.pi/count)<radius+1e-5 for i in range(count)):remove.append(f)
  report.append({'mesh':o.name,'localized_faces':len(candidates),'cut_faces':len(remove)})
  bmesh.ops.delete(bm,geom=remove,context='FACES')
 if o.name=='imagetostl_mesh0': bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
 bm.to_mesh(o.data); bm.free(); o.data.update()
 print('REPAIRED',o.name,flush=True)
# A short internal sleeve seals the missing connection, wholly below the shore.
# Its polygon matches the precisely split opening, not a new above-ground wall.
verts=[]; faces=[]
surface_trees=[BVHTree.FromPolygons([v.co for v in o.data.vertices],[list(p.vertices) for p in o.data.polygons]) for o in meshes if o.name!='imagetostl_mesh0']
rim=[]
for i in range(count):
 a=(i+.5)*2*math.pi/count; r=radius/math.cos(math.pi/count)+.04
 origin=Vector((cx+r*math.cos(a),cy+r*math.sin(a),460))
 heights=[hit[0].z for tree in surface_trees if (hit:=tree.ray_cast(origin,Vector((0,0,-1)),15))[0] is not None]
 rim.append(min(top,max(455.65,max(heights) if heights else 455.65)))
for layer in range(17):
 t=layer/16
 for i in range(count):
  z=rim[i]+(bottom-rim[i])*t
  a=(i+.5)*2*math.pi/count; r=radius/math.cos(math.pi/count)
  r+=math.sin(math.pi*t)*(.20+.16*math.sin(a*7+t*15)+.12*math.cos(a*11-t*20))
  verts.append((cx+r*math.cos(a),cy+r*math.sin(a),z))
for layer in range(16):
 for i in range(count):
  j=(i+1)%count; k=layer*count; faces.append((k+i,k+j,k+count+j,k+count+i))
mesh=bpy.data.meshes.new('Repaired connector rock'); mesh.from_pydata(verts,[],faces); mesh.update()
collar=bpy.data.objects.new('Level2 repaired underwater connection',mesh); bpy.context.collection.objects.link(collar)
color_lower_cave(meshes)
mat=bpy.data.materials.new('Connector limestone'); mat.diffuse_color=(.19,.20,.17,1); mat.use_nodes=True
shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
shader.inputs['Base Color'].default_value=(.19,.20,.17,1)
shader.inputs['Roughness'].default_value=.9
mesh.materials.append(mat)
colors=mesh.color_attributes.new(name='CaveRockColor',type='BYTE_COLOR',domain='POINT')
for v,c in zip(mesh.vertices,colors.data):
 shade=.16+.04*math.sin(v.co.x*3+v.co.z*5)+.025*math.cos(v.co.y*6-v.co.z*2)
 c.color=(shade,shade*.99,shade*.85,1)
color_node=mat.node_tree.nodes.new('ShaderNodeVertexColor'); color_node.layer_name=colors.name
mat.node_tree.links.new(color_node.outputs['Color'],shader.inputs['Base Color'])
for p in mesh.polygons:p.use_smooth=True
meshes.append(collar)
for o in meshes:
 for mat in o.data.materials:
  if mat: mat.use_backface_culling=False
for o in bpy.context.scene.objects:
 o.select_set(o.type=='MESH')
 if o.type!='MESH':o.hide_set(True)
bpy.context.view_layer.objects.active=collar
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.region_3d.view_location=Vector((cx,cy,450))
   area.spaces.active.region_3d.view_distance=45
bpy.ops.wm.save_as_mainfile(filepath=out+'/Level2_Cave_Repaired.blend')
bpy.ops.export_scene.gltf(filepath=out+'/level2_cave_repaired.glb',export_format='GLB',use_selection=True,export_cameras=False,export_lights=False,export_apply=True)
open(out+'/repair_report.json','w').write(json.dumps(report,indent=2))
