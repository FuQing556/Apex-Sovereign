"""Generate seamless aggregate at several scales and a feathered particle opacity mask."""
from pathlib import Path
import numpy as np
from PIL import Image
root=Path(__file__).resolve().parents[3]
map_path=root/"Assets/_Project/Tracks/RidgeArt"
vehicle=root/"Assets/_Project/Vehicles/SHIBATA31"
rng=np.random.default_rng(31);size=512
def octave(n):
    grid=rng.random((n,n));x=np.arange(size)*n/size;i=x.astype(int);t=x-i;t=t*t*(3-2*t)
    a=grid[i,:]*(1-t[:,None])+grid[(i+1)%n,:]*t[:,None]
    return a[:,i]*(1-t)+a[:,(i+1)%n]*t
fine=rng.normal(0,1,(size,size))
luma=83+(octave(4)-.5)*9+(octave(26)-.5)*15+(octave(158)-.5)*22+fine*6+np.maximum(fine-1.65,0)*21
rgb=np.stack([luma*.975,luma*.985,luma],axis=-1)
Image.fromarray(np.uint8(np.clip(rgb,0,255))).save(map_path/"Aggregate.png")
h=octave(150)*.8+fine*.06
dx=(np.roll(h,-1,1)-np.roll(h,1,1))*.42;dy=(np.roll(h,-1,0)-np.roll(h,1,0))*.42
nz=1/np.sqrt(1+dx*dx+dy*dy)
normal=np.stack([-dx*nz,-dy*nz,nz],axis=-1)
Image.fromarray(((normal*.5+.5)*255).astype('uint8')).save(map_path/"AggregateNormal.png")
x,y=np.meshgrid(np.linspace(-1,1,size),np.linspace(-1,1,size))
r=np.sqrt(x*x+y*y)
noise=.53*octave(5)+.27*octave(11)+.16*octave(23)+.04*octave(57)
alpha=np.clip((1-r+(noise-.5)*.42)*1.12,0,1)**1.9*(.27+noise*.88)
rgba=np.full((size,size,4),255,dtype='uint8');rgba[:,:,3]=np.uint8(alpha*255)
Image.fromarray(rgba).resize((128,128),Image.Resampling.LANCZOS).save(vehicle/"TyrePlume.png")
