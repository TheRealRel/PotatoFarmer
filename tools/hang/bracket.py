import struct, numpy as np, colorsys
from PIL import Image
d=open('hang/laradr_rack.mesh','rb').read(); nv,ni=struct.unpack('<ii',d[4:12])
o=12; V=np.frombuffer(d,'<f4',nv*3,o).reshape(-1,3).astype(float); o+=nv*12; N=np.frombuffer(d,'<f4',nv*3,o).reshape(-1,3); o+=nv*12
UV=np.frombuffer(d,'<f4',nv*2,o).reshape(-1,2).astype(float); o+=nv*8; F=np.frombuffer(d,'<i4',ni,o).reshape(-1,3)
T=np.asarray(Image.open('hang/laradr_rack_d.png').convert('RGB')).astype(float)/255; H,W=T.shape[:2]
hsv=np.array([colorsys.rgb_to_hsv(*p) for p in T.reshape(-1,3)]).reshape(H,W,3)
metal=(hsv[:,:,1]<0.30)   # grey: low saturation; wood is orange (sat ~0.6)
print('metal texel share', metal.mean().round(3))
def texels(tri_uv, n=7):
    # sample a triangle in UV space on an n-grid of barycentric points (nearest texel, v flipped like Unity)
    a,b,c=tri_uv; out=[]
    for i in range(n+1):
        for j in range(n+1-i):
            u=(i/n); v=(j/n); w=1-u-v; p=a*w+b*u+c*v
            x=int(np.clip(p[0]%1*W,0,W-1)); y=int(np.clip((1-p[1]%1)*H,0,H-1)); out.append(metal[y,x])
    return np.array(out)
np.save('/tmp/rack_V.npy',V); np.save('/tmp/rack_F.npy',F)
frac=np.array([texels(UV[f]).mean() for f in F])
np.save('/tmp/rack_metalfrac.npy',frac)
cent=V[F].mean(1)
# one corner: x>0.7, z>0.35 (model units)
m=(cent[:,0]>0.7)&(cent[:,2]>0.3)
for y0 in np.arange(0.70,1.12,0.02):
    s=m&(cent[:,1]>=y0)&(cent[:,1]<y0+0.02)
    if s.sum(): print(f"y {y0:.2f}: tris {s.sum():4d}  metal share {frac[s].mean():.2f}  mixed (10-90%) {((frac[s]>0.1)&(frac[s]<0.9)).sum()}")
