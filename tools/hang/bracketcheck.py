import struct, numpy as np, colorsys, sys
from PIL import Image
def load(p):
    d=open(p,'rb').read(); nv,ni=struct.unpack('<ii',d[4:12]); o=12
    V=np.frombuffer(d,'<f4',nv*3,o).reshape(-1,3).astype(float); o+=nv*24
    UV=np.frombuffer(d,'<f4',nv*2,o).reshape(-1,2).astype(float); o+=nv*8
    F=np.frombuffer(d,'<i4',ni,o).reshape(-1,3); return V,UV,F
T=np.asarray(Image.open('hang/laradr_rack_d.png').convert('RGB')).astype(float)/255; H,W=T.shape[:2]
hsv=np.array([colorsys.rgb_to_hsv(*p) for p in T.reshape(-1,3)]).reshape(H,W,3); metal=hsv[:,:,1]<0.30
def occluded(A,B,C,cam,P):
    e1,e2=B-A,C-A; out=np.zeros(len(P),bool)
    for k,p in enumerate(P):
        d=p-cam; L=np.linalg.norm(d); d/=L
        h=np.cross(d,e2); a=(e1*h).sum(1); ok=np.abs(a)>1e-12; a=np.where(ok,a,1.0); f=1/a; s=cam-A
        u=f*(s*h).sum(1); q=np.cross(s,e1); v=f*(q*d).sum(1); t=f*(e2*q).sum(1)
        out[k]=(ok&(u>=0)&(v>=0)&(u+v<=1)&(t>1e-4)&(t<L-2e-3)).any()
    return out
for path in sys.argv[1:]:
    V,UV,F=load(path); cent=V[F].mean(1); tot_wood=tot=0
    for sx in (1,-1):
        for sz in (1,-1):
            m=(cent[:,0]*sx>0.6)&(cent[:,2]*sz>0.2)
            leg=V[F[m&(cent[:,1]>0.55)&(cent[:,1]<0.66)]].reshape(-1,3); blk=V[F[m&(cent[:,1]>0.93)&(cent[:,1]<1.0)]].reshape(-1,3)
            ox=max((leg[:,0]*sx).max(),(blk[:,0]*sx).max()); oz=max((leg[:,2]*sz).max(),(blk[:,2]*sz).max())
            band=np.nonzero(m&(cent[:,1]>0.70)&(cent[:,1]<0.90))[0]
            br=[t for t in band if ((V[F[t]][:,0]*sx)>ox+0.001).any() or ((V[F[t]][:,2]*sz)>oz+0.001).any()]
            # sample points on those faces (with their texel class), keep the ones seen from the user's kind of views
            near=np.nonzero(m&(cent[:,1]>0.5))[0]; A,B,C=V[F[near,0]],V[F[near,1]],V[F[near,2]]
            cams=[np.array([0.93*sx+0.9*sx,0.89+0.45,0.55*sz+0.9*sz]),np.array([0.93*sx+1.2*sx,0.89+0.2,0.55*sz+0.2*sz]),np.array([0.93*sx+0.2*sx,0.89+0.2,0.55*sz+1.2*sz])]
            for t in br:
                a,b,c=V[F[t]]; ua,ub,uc=UV[F[t]]
                for (wu,wv) in ((1/3,1/3),(0.6,0.2),(0.2,0.6),(0.2,0.2)):
                    p=a*(1-wu-wv)+b*wu+c*wv; uv=ua*(1-wu-wv)+ub*wu+uc*wv
                    p_out=p+0.0005*np.cross(b-a,c-a)/max(np.linalg.norm(np.cross(b-a,c-a)),1e-12)
                    seen=any(not occluded(A,B,C,cam,[p_out])[0] for cam in cams)
                    if not seen: continue
                    tot+=1; tot_wood+= not metal[int(np.clip((1-uv[1]%1)*H,0,H-1)),int(np.clip(uv[0]%1*W,0,W-1))]
    print(f"{path}: visible bracket sample points {tot}, wood-coloured {tot_wood} ({100*tot_wood/max(tot,1):.1f} %)")
