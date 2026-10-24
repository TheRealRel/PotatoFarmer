# Like pix.py, with a brightness and saturation lift so new icons match the earlier from-above ones.
from PIL import Image
import numpy as np
from matplotlib.colors import rgb_to_hsv, hsv_to_rgb
def pix(path,vgain=1.0,sgain=1.0,art=40,scale=2):
    im=Image.open(path).convert('RGBA'); im=im.crop(im.getbbox()); w,h=im.size; s=max(w,h)
    sq=Image.new('RGBA',(s,s),(0,0,0,0)); sq.paste(im,((s-w)//2,(s-h)//2))
    inner=art-4; a=np.array(sq.resize((inner,inner),Image.BOX)); alpha=a[...,3]>110
    rgb=np.clip(a[...,:3].astype(float)*1.12+6,0,255)/255
    hsv=rgb_to_hsv(rgb); hsv[...,2]=np.clip(hsv[...,2]*vgain,0,1); hsv[...,1]=np.clip(hsv[...,1]*sgain,0,1)
    a[...,:3]=(hsv_to_rgb(hsv)*255).astype(np.uint8); a[...,3]=np.where(alpha,255,0)
    q=np.array(Image.fromarray(a).convert('RGB').quantize(28,method=Image.Quantize.MEDIANCUT).convert('RGBA')); q[...,3]=a[...,3]
    can=np.zeros((art,art,4),np.uint8); can[2:2+inner,2:2+inner]=q; m=can[...,3]>0; ring=np.zeros_like(m)
    for dy,dx in ((1,0),(-1,0),(0,1),(0,-1)): ring|=np.roll(np.roll(m,dy,0),dx,1)
    can[ring&~m]=(38,24,16,255)
    return Image.fromarray(can).resize((art*scale,art*scale),Image.NEAREST)
def stats(img):
    a=np.array(img.convert('RGBA')).astype(float); m=a[...,3]>0; hsv=rgb_to_hsv(a[...,:3][m]/255)
    return np.percentile(hsv[:,2],90), hsv[:,1].mean()
