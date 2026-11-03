import colorsys, json
def hsv(c): return colorsys.rgb_to_hsv(*[x/255 for x in c])
pairs={'boar':((98,64,40),(74,54,37)),'wolf':((106,43,25),(82,36,25))}
rs=[];rv=[]
for k,(ck,jk) in pairs.items():
    a,b=hsv(ck),hsv(jk); rs.append(b[1]/a[1]); rv.append(b[2]/a[2])
    print(k,'cooked',[round(x,3) for x in a],'jerky',[round(x,3) for x in b],'s',round(b[1]/a[1],3),'v',round(b[2]/a[2],3),'dh',round((b[0]-a[0])*360,1))
ks=sum(rs)/2; kv=sum(rv)/2; print('ratio s',round(ks,3),'v',round(kv,3))
cooked={'necktail':(98,64,40),'deer':(111,67,39),'horse':(80,35,32),'grizzly':(106,83,61),'bloodgill':(62,42,28),
'lox':(133,88,75),'chicken':(111,74,63),'hare':(115,79,68),'seeker':(215,150,119),'serpent':(106,87,72),
'asksvin':(70,82,63),'volture':(90,62,47),'moose':(40,20,13),'bonemaw':(114,63,36)}
out={}
for k,c in cooked.items():
    h,s,v=hsv(c); r=colorsys.hsv_to_rgb(h,min(1,s*ks),v*kv); out[k]=[round(x*255) for x in r]
    print(f'{k:10s} cooked {c} -> jerky {tuple(out[k])}  h{h*360:.0f} s{s*ks:.2f} v{v*kv:.2f}')
json.dump(out,open('targets.json','w'))
