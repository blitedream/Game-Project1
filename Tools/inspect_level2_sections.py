import bpy, json, math
from mathutils import Vector
from mathutils.bvhtree import BVHTree
trees=[]
for o in bpy.context.scene.objects:
 if o.type!='MESH': continue
 trees.append((o.name,BVHTree.FromPolygons([o.matrix_world@v.co for v in o.data.vertices],[list(p.vertices) for p in o.data.polygons])))
def hit(p,d):
 found=[]
 for name,t in trees:
  loc,n,i,dist=t.ray_cast(Vector(p),Vector(d),200)
  if loc is not None: found.append((round(dist,3),name,list(loc)))
 return sorted(found)[:2]
out={}
for z in [458,456,455,453,450,448,447,445,440,430,410,390]:
 out[str(z)]=[hit((-293,-77,z),(math.cos(a*math.pi/8),math.sin(a*math.pi/8),0)) for a in range(16)]
out['down']=hit((-293,-77,460),(0,0,-1))
open('D:/Unity/GP1/Artifacts/Level2Blender/sections.json','w').write(json.dumps(out,indent=2))
