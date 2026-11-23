"""Regenerate the engineering sheet from RidgeCornerPace and verified keyboard runs."""
import csv
import json
import zipfile
from pathlib import Path
from shutil import copyfile
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib import font_manager
from matplotlib.collections import PolyCollection
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
LOG = ROOT / "Logs/RidgeSearch"
OUT = ROOT / "毕设资料/05-试验记录/山脊弯速"
OUT.mkdir(exist_ok=True)
CORNERS = np.array([116,212,304,374,442,506,588,664,734,826,920,992,1082,1198,1298,1374])
FONT = ROOT / "Assets/_Project/UI/Fonts/NotoSansSC-Medium.otf"
font_manager.fontManager.addfont(str(FONT))
plt.rcParams.update({"font.family": font_manager.FontProperties(fname=str(FONT)).get_name(),
    "axes.unicode_minus": False, "axes.spines.top": False, "axes.spines.right": False,
    "font.size": 10, "axes.labelcolor": "#3c4953", "xtick.color": "#3c4953", "ytick.color": "#3c4953"})

def read(name):
    return np.genfromtxt(LOG / (name+".csv"), delimiter=",", names=True, encoding="utf-8")

def write(name, rows):
    with (OUT/name).open("w",encoding="utf-8-sig",newline="") as f:
        writer=csv.DictWriter(f,fieldnames=rows[0]);writer.writeheader();writer.writerows(rows)

line=read("line")
road=read("road")
spacing=line["s"][1];length=spacing*len(line)
centre=np.column_stack([line[k] for k in ("x","y","z")])
tangent=np.roll(centre,-1,axis=0)-np.roll(centre,1,axis=0)
tangent/=np.linalg.norm(tangent,axis=1)[:,None]

def stations(trace):
    pos=np.column_stack([trace[k] for k in ("x","y","z")])
    i=np.rint(trace["station_m"]/spacing).astype(int)%len(line)
    return (line["s"][i]+np.sum((pos-centre[i])*tangent[i],axis=1))%length

def measure(trace,s,apex,lap):
    win=(trace["lap"]==lap)&(np.abs(s-apex)<47)
    ix=np.flatnonzero(win); a=apex-40;b=apex+26
    sample=s[ix]; start=np.flatnonzero(sample>=a)[0];end=np.flatnonzero(sample>=b)[0]
    i,j=ix[start],ix[end]
    def cross(field,k,where):
        return np.interp(where,s[k-1:k+1],trace[field][k-1:k+1])
    enter=cross("kph",i,a);ex=cross("kph",j,b);t0=cross("t",i,a);t1=cross("t",j,b)
    sl=slice(i,j+1);keys=trace["keys"][sl].astype(int)
    times=trace["t"][sl]; dt=np.maximum(0,np.minimum(times,t1)-np.maximum(times-.02,t0))
    return dict(entry=round(enter,2),minimum=round(min(enter,ex,*trace["kph"][i:j]),2),exit=round(ex,2),
        time=round(t1-t0,4),foot=round(sum(dt[(keys&8)!=0]),3),
        hand=round(sum(dt[(keys&16)!=0]),3),gas=round(sum(dt[(keys&1)!=0]),3),
        angle=round(np.max(abs(trace["beta"][sl])),2))

names={"all-drift":"原逐弯","carry32":"保速逐弯","best-time":"最快实测"}
traces={k:read(k+"-keys") for k in names}
dist={k:stations(v) for k,v in traces.items()}
metrics=[]; rows={}
for name,trace in traces.items():
    for lap in (1,2):
        for n,apex in enumerate(CORNERS,1):
            m=measure(trace,dist[name],apex,lap);rows[name,lap,n]=m
            metrics.append(dict(方案=names[name],圈=lap,弯=n,弯心里程=int(apex),入口kmh=m["entry"],
                最低kmh=m["minimum"],出口kmh=m["exit"],区间秒=m["time"],脚刹秒=m["foot"],
                手刹秒=m["hand"],给油秒=m["gas"],峰值侧滑度=m["angle"]))
write("整圈录像的逐弯速度.csv",metrics)
copyfile(LOG/"corner-controls.csv",OUT/"同初态三种过弯.csv")
geometry=[]
for n,apex in enumerate(CORNERS,1):
    idx=np.flatnonzero(abs(line["s"]-apex)<=18)
    j=idx[np.argmax(abs(line["curvature_per_m"][idx]))]
    geometry.append(dict(弯=n,弯心里程米=apex,方向="右" if line["curvature_per_m"][j]>0 else "左",
        拟定线最小平面半径米=round(1/abs(line["curvature_per_m"][j]),2),
        该点高程米=round(line["line_y"][j],2),该点计划车速kmh=round(line["plan_kph"][j],2)))
write("山脊平面曲率与计划弯速.csv",geometry)

fig=plt.figure(figsize=(15.5,10.5),facecolor="#faf9f5",layout="constrained")
grid=fig.add_gridspec(2,2,height_ratios=[1.9,1],width_ratios=[1.12,1])
ax=fig.add_subplot(grid[0,0]);bars=fig.add_subplot(grid[0,1]);speed=fig.add_subplot(grid[1,:])
for axis in (ax,bars,speed):axis.set_facecolor("#faf9f5")
left=np.column_stack((road["x_left"],road["z_left"]));right=np.column_stack((road["x_right"],road["z_right"]))
quads=np.stack((left,right,np.roll(right,-1,axis=0),np.roll(left,-1,axis=0)),axis=1)
ax.add_collection(PolyCollection(quads,facecolor="#d5dad6",edgecolor="none"))
for boundary in [left,right]:
    ring=np.vstack([boundary,boundary[0]]);ax.plot(ring[:,0],ring[:,1],color="#9ca9a4",lw=.6)
colors={"all-drift":"#8c9195","carry32":"#007769","best-time":"#cc662a"}
for key in ["carry32","best-time"]:
    tr=traces[key];m=tr["lap"]==2
    ax.plot(tr["x"][m],tr["z"][m],color=colors[key],lw=1.45,label=names[key])
for n,apex in enumerate(CORNERS,1):
    idx=np.abs(line["s"]-apex).argmin();x,z=line["x"][idx],line["z"][idx]
    dx,dz=tangent[idx,[0,2]];sgn=np.sign(line["curvature_per_m"][idx])
    ax.annotate(str(n),(x,z),xytext=(-sgn*dz*16,sgn*dx*16),textcoords="offset points",ha="center",va="center",
        fontsize=9,bbox=dict(boxstyle="round,pad=.14",fc="#faf9f5",ec="none"),arrowprops=dict(arrowstyle="-",lw=.75,color="#64716f"))
ax.scatter([centre[0,0]],[centre[0,2]],marker="s",s=28,color="#273b41",label="起终点",zorder=5)
ax.set(aspect="equal",xlabel="东西位置（米）",ylabel="南北位置（米）")
ax.autoscale_view();ax.legend(loc="upper left",fontsize=9,frameon=False)
ax.set_title("赛道平面与第二圈实跑轨迹",loc="left",fontweight="bold")

ctrl=list(csv.DictReader((LOG/"corner-controls.csv").open(encoding="utf-8")))
mode_names=["抓地","手刹","钟摆"];bar_colors=["#737e86","#9855a0","#2071a2"]
for mode in range(3):
    col={int(r["corner"]):float(r["exit_kph"]) for r in ctrl if r["lap"]=="2" and r["mode"]==str(mode) and r["status"]=="ok"}
    series=[col.get(i,np.nan) for i in range(1,17)]
    bars.plot(range(1,17),series,marker=["o","s","^"][mode],ms=4,lw=1.1,color=bar_colors[mode],label=mode_names[mode])
bars.set(xticks=range(1,17),xlabel="弯号",ylabel="出口时速（km/h）")
bars.set_title("同初态对照  ·  第二圈各弯出口",loc="left",fontweight="bold")
bars.grid(axis="y",alpha=.25);bars.legend(ncol=3,frameon=False,fontsize=9)
for k,tr in traces.items():
    m=(tr["lap"]==2)&(dist[k]>4)&(dist[k]<length-4);sec=dist[k][m];order=np.argsort(sec)
    speed.plot(sec[order],tr["kph"][m][order],color=colors[k],lw=1.5 if k=="carry32" else 1.1,label=names[k])
for n,apex in enumerate(CORNERS,1):
    speed.axvline(apex,color="#d0ceca",lw=.6,zorder=0)
    speed.text(apex,.99,str(n),transform=speed.get_xaxis_transform(),ha="center",va="top",fontsize=9)
speed.set(xlabel="沿中心线里程（米）",ylabel="实测速率（km/h）",xlim=(0,length))
speed.grid(axis="y",alpha=.25);speed.legend(ncol=3,frameon=False,loc="lower right")
speed.set_title("第二圈速度随里程变化",loc="left",fontweight="bold")
fig.suptitle("山脊大奖赛道     柴田 31 号的弯速与线路",fontsize=21,fontweight="bold")
fig.savefig(OUT/"山脊平面与弯速.png",dpi=155);plt.close(fig)

# Keep the inputs with the graduation materials even if Unity's Logs directory is cleared.
required=["road.csv","line.csv","corner-controls.csv"]
if (LOG/"exit-search.csv").exists():
    required.append("exit-search.csv")
    copyfile(LOG/"exit-search.csv",OUT/"第2与第6弯起漂时机试验.csv")
for name in names:
    required.extend([name+".csv",name+"-keys.csv"])
comparison_ids=["control-21-0-full","exit-21-1-64-full"]
for name in comparison_ids:
    if (LOG/(name+"-keys.csv")).exists():
        required.append(name+"-keys.csv")
policies={}
with (LOG/"policies.jsonl").open(encoding="utf-8") as source:
    for record in source:
        p=json.loads(record)
        if p["name"] in names or p["name"]=="pair-base" or p["name"] in comparison_ids:
            policies[p["name"]]=record.strip()
with zipfile.ZipFile(OUT/"山脊幽灵复算数据.zip","w",compression=zipfile.ZIP_DEFLATED) as archive:
    for name in required:
        archive.write(LOG/name,name)
    archive.writestr("policies.jsonl","\n".join(policies.values())+"\n")
    summary=(LOG/"trials.csv").read_text(encoding="utf-8").splitlines()
    ids=set(names)|{k+"-keys" for k in names}|{k+"-keys" for k in comparison_ids}
    archive.writestr("trials.csv","\n".join([summary[0]]+[s for s in summary[1:] if s.partition(",")[0] in ids])+"\n")
