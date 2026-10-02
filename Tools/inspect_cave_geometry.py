import sys
from pathlib import Path
import numpy as np
import open_fused_cave_connector as c

g = c.GLTF2().load_binary(sys.argv[1])
b = g.binary_blob()
ts = c.collect_mesh_transforms(g)
triangles = []
for i, mesh in enumerate(g.meshes):
    for p in mesh.primitives:
        v = c.accessor_array(g, b, p.attributes.POSITION)
        v = (np.c_[v, np.ones(len(v))] @ ts[i].T)[:, :3]
        f = c.accessor_array(g,b,p.indices).reshape(-1,3)
        triangles.append((i,v[f]))

def hits(x,z):
    result=[]
    for i,t in triangles:
        a=t[:,0]; u=t[:,1]-a; v=t[:,2]-a
        den=u[:,0]*v[:,2]-u[:,2]*v[:,0]
        valid=np.abs(den)>1e-8
        den=np.where(valid,den,1)
        dx=x-a[:,0]; dz=z-a[:,2]
        s=(dx*v[:,2]-dz*v[:,0])/den
        q=(u[:,0]*dz-u[:,2]*dx)/den
        mask=valid&(s>=0)&(q>=0)&(s+q<=1)
        ys=a[:,1]+s*u[:,1]+q*v[:,1]
        result.extend((round(float(y),2),i) for y in ys[mask])
    return sorted(result,reverse=True)

for x,z in [(-250.79,102.9),(-293,77),(-290,77),(-295,77),(-290,82),(-300,77)]:
    print(x,z,hits(x,z))
lower=triangles[0][1]
for h in [447,445,440,430,410,390,375]:
    pts=lower.reshape(-1,3)
    pts=pts[np.abs(pts[:,1]-h)<1]
    print('slice',h,'bounds',pts.min(0) if len(pts) else None,pts.max(0) if len(pts) else None)

if '--verify' in sys.argv:
    failures=[]
    for dx in np.linspace(-4.5,4.5,7):
        for dz in np.linspace(-4.5,4.5,7):
            if dx*dx+dz*dz>4.5**2:
                continue
            blockers=[h for h in hits(-293+dx,77+dz) if 447 <= h[0] <= 457]
            if blockers: failures.append((dx,dz,blockers))
    print('SHAFT_CHECK',len(failures),'blocked samples',failures)
    assert not failures
    assert any(y<400 for y,i in hits(-293,77)), 'Cave floor lost'
    print('PASS: 9m-wide passage clear; original bottom preserved')
    sys.exit(0)

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
fig,ax=plt.subplots(figsize=(10,8))
for i,t in triangles:
    pts=t.mean(1)[::8]
    if i==0:
        ax.scatter(pts[:,0],pts[:,2],s=2,c='red',alpha=.3)
    else:
        ax.scatter(pts[:,0],pts[:,2],s=1,c=pts[:,1],vmin=447,vmax=478,cmap='terrain')
ax.set_aspect('equal'); ax.grid(); fig.savefig('Artifacts/cave_geometry.png')
