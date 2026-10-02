"""Run on repaired Blender copy; add a textured, closed annular shore solid."""
import bpy, math, os
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
cx,cy=-293.,-77.
sources=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('Model_material')]
trees=[]
for o in sources:
 o.data.calc_loop_triangles()
 tris=list(o.data.loop_triangles)
 trees.append((o,BVHTree.FromPolygons([v.co for v in o.data.vertices],[t.vertices for t in tris],all_triangles=True),tris))
def sample(x,y):
 best=None
 for o,tree,tris in trees:
  hit=tree.ray_cast(Vector((x,y,470)),Vector((0,0,-1)),30)
  if hit[0] is not None and (best is None or hit[0].z>best[0].z):best=(hit[0],o,tris[hit[2]])
 if best is None:
  bestdist=1e9
  for o,tree,tris in trees:
   hit=tree.find_nearest(Vector((x,y,456.4)))
   if hit[0] is not None and hit[3]<bestdist:bestdist=hit[3];best=(hit[0],o,tris[hit[2]])
 p,o,t=best
 uv=o.data.uv_layers.active.data
 coords=[Vector((*uv[i].uv,0)) for i in t.loops]
 mapped=barycentric_transform(p,*[o.data.vertices[i].co for i in t.vertices],*coords)
 return p.z,(mapped.x,mapped.y)
N=192; R=25; vertices=[]; uvs=[]
for layer in range(2):
 for j in range(R):
  r=6.0+6.5*j/(R-1)
  for i in range(N):
   a=2*math.pi*i/N;x=cx+r*math.cos(a);y=cy+r*math.sin(a)
   z,uv=sample(x,y)
   # Fill all depressions above water, preserving the scanned texture and outer relief.
   h=max(456.2,z+.025)
   vertices.append((x,y,h if layer==0 else 446.0));uvs.append(uv)
faces=[]; S=R*N
for j in range(R-1):
 for i in range(N):
  a=j*N+i;b=j*N+(i+1)%N;c=b+N;d=a+N
  faces.extend([(a,d,c,b),(S+a,S+b,S+c,S+d)])
for i in range(N):
 a=i;b=(i+1)%N; faces.append((a,b,S+b,S+a))
 a=(R-1)*N+i;b=(R-1)*N+(i+1)%N;faces.append((b,a,S+a,S+b))
m=bpy.data.meshes.new('Solid shore ring mesh');m.from_pydata(vertices,[],faces);m.update()
o=bpy.data.objects.new('Level2 solid shore ring',m);bpy.context.collection.objects.link(o)
# Project the original textured shore into one continuous map; UV atlas islands
# must not be interpolated across a new face, which would produce stripes.
out='D:/Unity/GP1/Artifacts/Level2Blender'
scene=bpy.context.scene
hidden={obj:obj.hide_render for obj in scene.objects}
for obj in scene.objects:obj.hide_render=obj not in sources
original=[]
for obj in sources:
 for slot in obj.material_slots:
  original.append((slot,slot.material)); mat=slot.material.copy();slot.material=mat
  if mat.use_nodes:
   bsdf=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
   output=next(n for n in mat.node_tree.nodes if n.type=='OUTPUT_MATERIAL')
   emission=mat.node_tree.nodes.new('ShaderNodeEmission')
   if bsdf and bsdf.inputs['Base Color'].is_linked:mat.node_tree.links.new(bsdf.inputs['Base Color'].links[0].from_socket,emission.inputs['Color'])
   elif bsdf:emission.inputs['Color'].default_value=bsdf.inputs['Base Color'].default_value
   mat.node_tree.links.new(emission.outputs[0],output.inputs['Surface'])
camdata=bpy.data.cameras.new('Shore texture projection');cam=bpy.data.objects.new('Shore texture projection',camdata);scene.collection.objects.link(cam)
cam.location=(cx,cy,490);cam.rotation_euler=(0,0,0);camdata.type='ORTHO';camdata.ortho_scale=26;scene.camera=cam
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=2048;scene.render.resolution_y=2048;scene.render.resolution_percentage=100
scene.render.film_transparent=True;scene.view_settings.view_transform='Standard';scene.render.image_settings.file_format='PNG';scene.render.filepath=out+'/shore_surface.png'
bpy.ops.render.render(write_still=True)
for slot,mat in original:slot.material=mat
for obj,value in hidden.items():obj.hide_render=value
bpy.data.objects.remove(cam,do_unlink=True)
mat=bpy.data.materials.new('Original shore projected texture');mat.use_nodes=True
bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bsdf.inputs['Roughness'].default_value=.95
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(out+'/shore_surface.png');tex.image.pack()
mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
m.materials.append(mat);uv=m.uv_layers.new(name='UVMap')
for loop in m.loops:
 v=m.vertices[loop.vertex_index].co;uv.data[loop.index].uv=((v.x-cx)/26+.5,(v.y-cy)/26+.5)
for p in m.polygons:p.use_smooth=True
# Closed volume has no boundary edges; verify continuous dry support over its entire width.
import bmesh,json
bm=bmesh.new();bm.from_mesh(m)
assert all(e.is_manifold for e in bm.edges),'Non-manifold shore'
bm.free()
tree=BVHTree.FromPolygons(vertices,faces)
checks=0
for radius in [6.1+i*.1 for i in range(64)]:
 for angle in range(360):
  a=math.radians(angle);p,_,_,_=tree.ray_cast(Vector((cx+radius*math.cos(a),cy+radius*math.sin(a),480)),Vector((0,0,-1)),40)
  assert p is not None and p.z>=456.19
  checks+=1
out='D:/Unity/GP1/Artifacts/Level2Blender'
open(out+'/shore_validation.json','w').write(json.dumps({'closed_manifold':True,'dry_shore_samples':checks,'inner_radius_world':12,'outer_radius_world':25},indent=2))
for obj in bpy.context.scene.objects:obj.select_set(obj.type=='MESH')
bpy.context.view_layer.objects.active=o
bpy.ops.wm.save_as_mainfile(filepath=out+'/Level2_Cave_Solid_Shore.blend')
bpy.ops.export_scene.gltf(filepath=out+'/level2_cave_repaired.glb',export_format='GLB',use_selection=True,export_cameras=False,export_lights=False,export_apply=True)
