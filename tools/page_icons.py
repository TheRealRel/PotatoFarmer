# Pixel-art page icons from model renders: crop, shrink to 36 px art inside a 40 px canvas, posterise, dark outline, x2.
from PIL import Image
import numpy as np, os, sys
def pix(path,art=40,scale=2):
    im=Image.open(path).convert('RGBA'); im=im.crop(im.getbbox()); w,h=im.size; s=max(w,h)
    sq=Image.new('RGBA',(s,s),(0,0,0,0)); sq.paste(im,((s-w)//2,(s-h)//2))
    inner=art-4; a=np.array(sq.resize((inner,inner),Image.BOX)); alpha=a[...,3]>110
    a[...,:3]=np.clip(a[...,:3].astype(float)*1.12+6,0,255); a[...,3]=np.where(alpha,255,0)
    q=np.array(Image.fromarray(a).convert('RGB').quantize(28,method=Image.Quantize.MEDIANCUT).convert('RGBA')); q[...,3]=a[...,3]
    can=np.zeros((art,art,4),np.uint8); can[2:2+inner,2:2+inner]=q; m=can[...,3]>0; ring=np.zeros_like(m)
    for dy,dx in ((1,0),(-1,0),(0,1),(0,-1)): ring|=np.roll(np.roll(m,dy,0),dx,1)
    can[ring&~m]=(38,24,16,255)
    return Image.fromarray(can).resize((art*scale,art*scale),Image.NEAREST)
names=['trough','roost','cask','lick','post']; os.makedirs('final',exist_ok=True)
sheet=Image.new('RGBA',(3*85+10,5*90),(22,22,34,255))
for r,n in enumerate(names):
    for i in range(3):
        ic=pix(f'out/{n}_{i}.png'); ic.save(f'final/icon-{n}-{i+1}.png'); sheet.paste(ic,(10+i*85,5+r*90),ic)
sheet.resize((sheet.width*2,sheet.height*2),Image.NEAREST).save('/tmp/claude-0/pix.png')
