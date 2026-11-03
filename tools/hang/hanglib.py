import json, numpy as np
H='/root/rackview/hang/'
def verts(name):
    parts=json.load(open(H+name+'.json')); return np.concatenate([np.array(p['v']) for p in parts])
def colmajor(M): return M.T.reshape(-1).tolist()
def T(t): M=np.eye(4); M[:3,3]=t; return M
def S(s): M=np.eye(4); M[:3,:3]*=s; return M
def R(axis,deg):
    a=np.radians(deg); c,s=np.cos(a),np.sin(a); M=np.eye(4)
    if axis=='x': M[1:3,1:3]=[[c,-s],[s,c]]
    if axis=='y': M[0,0],M[0,2],M[2,0],M[2,2]=c,s,-s,c
    if axis=='z': M[:2,:2]=[[c,-s],[s,c]]
    return M
ATT={'Honey':[-0.089,0,0]}
import json as _j
try:
    _a=_j.load(open(H+'attach_trs.json'))
    for k,v in _a.items(): ATT[k]=(v.get('attach') or v.get('model'))[0]
except Exception: pass
def Q(q):
    x,y,z,w=q; M=np.eye(4)
    M[:3,:3]=[[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]
    return M
def on_slot(item, pos, rot=(-0.5,0.5,0.5,0.5)):
    # what CookingStation.SetSlotVisual does: the attach copy at the slot's position and rotation, own scale kept
    return T(pos) @ Q(rot) @ T(-np.array(ATT.get(item,[0,0,0])))
