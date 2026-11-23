"""Standalone offline ghost and static evidence figure for the Ridge inverse design."""
import json
import numpy as np
from inverse_drift import OUT, road


def preview(g,f,actual,mask,corners,margin):
    pick=np.unique(np.r_[np.arange(0,len(mask),3),len(mask)-1])
    data=np.c_[f["t"],f["pos"][:,0],f["pos"][:,2],f["heading"],f["chi"],f["v"]*3.6,
               f["b"],actual,mask,f["s"],margin,f["r"]][pick].round(4).tolist()
    xy=lambda a:a[:,[0,2]].round(3).tolist()
    edges=[xy(g["mesh"]+sign*road["halfWidth"]*g["side"]) for sign in [-1,1]]
    package=json.dumps(dict(frames=data,edges=edges,corners=np.array(corners,dtype=object).tolist()),ensure_ascii=False)
    template=(__import__("pathlib").Path(__file__).with_name("drift_preview.html")).read_text(encoding="utf-8")
    (OUT/"双圈漂移幽灵.html").write_text(template.replace("__PLAN_DATA__",package),encoding="utf-8")
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib import font_manager
    from pathlib import Path
    font=Path("C:/Windows/Fonts/msyh.ttc")
    if font.exists():
        font_manager.fontManager.addfont(str(font))
        plt.rcParams["font.family"]=font_manager.FontProperties(fname=str(font)).get_name()
    plt.rcParams["axes.unicode_minus"]=False
    fig,axes=plt.subplots(2,1,figsize=(11.5,6.7),height_ratios=[1,1],layout="constrained",sharex=True)
    row=max(corners,key=lambda r:abs(r[11]))
    t0=row[4]; sel=(f["t"]>=t0-3.8)&(f["t"]<=t0+2.7)
    rel=f["t"][sel]-t0
    axes[0].plot(rel,f["d"][sel],color="#5d9cbd",linestyle="--",label="逆解所需前轮转角")
    axes[0].plot(rel,actual[sel],color="#126e80",label="按键换算后的前轮转角")
    axes[0].plot(rel,f["b"][sel],color="#b6532f",label="车身侧滑角")
    ax=axes[1]
    mid=np.searchsorted(f["t"],t0)
    ax.plot(rel,np.degrees(f["heading"][sel]-f["heading"][mid]),label="车头朝向变化",color="#88499a")
    ax.plot(rel,np.degrees(f["chi"][sel]-f["chi"][mid]),label="速度方向变化",color="#247f69")
    for a in axes:
        a.axvline(0,color="#454545",ls=":",lw=1)
        a.axhline(0,color="#888",lw=.6)
        a.set_ylabel("角度（°）");a.grid(alpha=.14);a.legend(loc="upper left",fontsize=9)
        for key,color in [(2,"#7288cd"),(4,"#edb463")]:
            a.fill_between(rel,0,1,where=(mask[sel]&key)!=0,transform=a.get_xaxis_transform(),
                           color=color,alpha=.16,step="post")
    ax.set_xlabel("相对弯心时间（秒），橙色为 D，蓝色为 A")
    fig.suptitle(f"山脊 · 第 {row[0]} 圈第 {row[1]} 弯    |    提前给舵与弯内回正（计算预测）",fontsize=15)
    fig.savefig(OUT/"提前给舵.png",dpi=145)
    plt.close(fig)
