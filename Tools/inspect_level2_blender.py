import bpy,json
from mathutils import Vector
result=[]
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 p=[o.matrix_world@Vector(v) for v in o.bound_box]
 result.append(dict(name=o.name,vertices=len(o.data.vertices),faces=len(o.data.polygons),min=[min(v[i] for v in p) for i in range(3)],max=[max(v[i] for v in p) for i in range(3)],materials=[m.name if m else None for m in o.data.materials]))
open('D:/Unity/GP1/Artifacts/Level2Blender/source_inspection.json','w').write(json.dumps(result,indent=2))
