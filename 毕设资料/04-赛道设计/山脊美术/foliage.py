"""Phototropism-inspired branching silhouettes for the crossed tree cards and ground grain."""
from pathlib import Path
from random import Random
from math import sin,cos,pi
import numpy as np
from PIL import Image, ImageDraw
dst=Path(__file__).resolve().parents[3]/"Assets/_Project/Tracks/RidgeArt"
r=Random(41);W,H=512,768
def sprite(name,pine):
    im=Image.new("RGBA",(W,H));d=ImageDraw.Draw(im)
    d.polygon([(241,765),(265,765),(257,65),(251,37)],fill=(83,72,55,255))
    if pine:
        limbs=[]
        for j in range(125):
            y=61+j*4.8;w=(y/H)**.85*217*(.75+.25*sin(j*.54))
            for s in [-1,1]:
                length=w*r.uniform(.62,1.16);drop=r.uniform(12,43)*(.45+y/H)
                limbs.append((y,s,length,drop))
        for y,s,length,drop in limbs[::-1]:
            d.line([(254,y-13),(254+s*length*.7,y+drop),(254+s*length,y+drop*.68)],fill=(64,69,47,255),width=max(1,int(length/55)))
            for i in range(int(length*.89)+1):
                t=r.random();x=254+s*t*length+r.gauss(0,2.7)
                yy=y+drop*sin(t*pi*.77)+r.gauss(0,2.6)
                spread=(1-.65*t)*r.uniform(7,32)*(.6+y/H)
                c=r.randrange(39,68)+int(12*(1-t))
                color=(int(c*.85),int(c*1.25),int(c*.90),255)
                d.polygon([(x,yy-spread*.55),(x-3.5,yy+spread*.23),(x+s*r.uniform(2,12),yy+spread)],fill=color)
    else:
        def branch(x,y,angle,length,width,level):
            xx=x+length*sin(angle);yy=y-length*cos(angle)
            d.line([x,y,xx,yy],fill=(85,76,57,255),width=max(1,int(width)))
            if level>0:
                for s in [-1,1]:branch(xx,yy,angle*.61+s*r.uniform(.29,.78),length*r.uniform(.57,.78),width*.60,level-1)
                return
            for _ in range(140):
                a=r.random()*2*pi;rr=(r.random()**.55);lx=xx+cos(a)*rr*32;ly=yy+sin(a)*rr*25
                off=r.randrange(-13,24)-int(rr*9);col=(82+off,102+off,48+off//2,255)
                rx=r.uniform(2,6.7);ry=r.uniform(2,5.4);d.ellipse((lx-rx,ly-ry,lx+rx,ly+ry),fill=col)
        for k in range(8):
            branch(253,638-k*38,(-1 if k%2 else 1)*r.uniform(.18,.63),r.uniform(81,129),11,5)
    box=im.getbbox();im=im.crop(box)
    target=Image.new("RGBA",(256,384));im=im.resize((246,376),Image.Resampling.LANCZOS);target.alpha_composite(im,(5,5))
    target.save(dst/name)
sprite("Japanese cedar.png",True);sprite("Mountain birch.png",False)
rng=np.random.default_rng(123);sz=384;grass=Image.new("RGB",(sz,sz),(139,154,102));draw=ImageDraw.Draw(grass)
for _ in range(22500):
    x,y=rng.integers(0,sz,size=2);k=int(rng.integers(-34,34));off=int(rng.integers(1,7))
    draw.line((x,y,x-2,y+off),fill=(139+k,149+k,100+k//2),width=1)
grass.save(dst/"Meadow grain.png")
