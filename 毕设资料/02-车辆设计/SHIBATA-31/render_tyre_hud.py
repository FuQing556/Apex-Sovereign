"""Bake the chassis plan and tintable tread for the Unity tyre-temperature HUD.

Run with Python and Pillow from any directory. Body sections and cockpit stations
are read from the same SHIBATA 31 model source as the FBX. Front is +Y (page up).
"""
import ast
from pathlib import Path
from PIL import Image, ImageDraw

here = Path(__file__).resolve().parent
out = here.parents[2] / "Assets/_Project/UI/TyrePlan"
out.mkdir(exist_ok=True)
model = ast.parse((here / "build_model.py").read_text(encoding="utf-8"))
def stations(key):
    return next(ast.literal_eval(node.value) for node in model.body
                if isinstance(node, ast.Assign) and isinstance(node.targets[0], ast.Name)
                and node.targets[0].id == key)

# View box in local metres. 4.60 m includes the splitter and rear wing.
width, length = 2.40, 4.60
im = Image.new("RGBA", (768, 1536))
pen = ImageDraw.Draw(im)
def xy(x, y):
    return (round((x/width+.5)*im.width), round((.5-y/length)*im.height))
def line(points, shade, w):
    pen.line([xy(*p) for p in points], fill=shade, width=round(w*im.width/width), joint="curve")
def shape(points, fill, outline=None, w=.02):
    pen.polygon([xy(*p) for p in points], fill=fill)
    if outline: line(points+[points[0]], outline, w)

body_stations=stations("width")
half=[(w,y) for y,w in body_stations]
shape(half+[(-x,y) for x,y in reversed(half)], (67,94,104,95), (173,200,205,222), .035)
glass=stations("cab")
half=[(w,y) for y,w,z in glass]
shape(half+[(-x,y) for x,y in reversed(half)], (70,105,118,110), (128,162,170,185), .025)
for i in (2,5):
    y,w,_=glass[i]
    line([(-w,y),(0,y+.045),(w,y)],(165,192,200,174),.025)
for side in (-1,1):
    line([(side*.80,1.06),(side*.63,1.70),(side*.49,1.87)], (138,168,177,175),.022)
    line([(side*.80,1.87),(side*.75,2.04),(side*.48,2.08)], (209,230,229,232),.045)
    line([(side*.76,-1.92),(side*.67,-2.035),(side*.40,-2.04)], (151,183,189,222),.04)
    line([(side*.84,.46),(side*.98,.53),(side*.99,.44)], (157,186,194,208),.047)
    line([(side*.56,-1.84),(side*.56,-2.0)], (162,186,189,210),.035)
    line([(side*.52,1.29),(side*.80,1.29)],(198,221,222,224),.045)
line([(-.94,-2.0),(.94,-2.0)], (154,177,188,200),.060)
# The exposed axle, six cylinder banks, gearbox and prop shaft distinguish the
# see-through diagram from an opaque car silhouette.
for y in (1.2875,-1.2875):
    line([(-.82,y),(.82,y)],(179,203,206,198),.045)
line([(0,.63),(0,-1.2875)],(159,193,196,202),.055)
for side in (-1,1):
    for y in (.70,.88,1.06):
        shape([(side*.07,y-.075),(side*.29,y-.045),(side*.29,y+.085),(side*.07,y+.055)],
              (130,157,166,220))
shape([(-.16,.61),(.16,.61),(.13,.17),(0,.05),(-.13,.17)],(83,112,123,240),(161,188,192,216),.021)
shape([(-.19,-1.25),(0,-1.12),(.19,-1.25),(.15,-1.41),(-.15,-1.41)],(91,119,127,245),(176,204,206,237),.025)
im.resize((256,512),Image.Resampling.LANCZOS).save(out/"ChassisPlan.png")

im=Image.new("RGBA",(192,384)); pen=ImageDraw.Draw(im)
pen.rounded_rectangle((8,5,183,378),radius=39,fill=(255,255,255,255))
# Cut a central groove and angled grooves from each shoulder; tint is applied in uGUI.
pen.line([(96,15),(96,370)],fill=(0,0,0,108),width=10)
for y in range(45,370,69):
    pen.line([(10,y),(54,y+23)],fill=(0,0,0,135),width=11)
    pen.line([(182,y),(138,y+23)],fill=(0,0,0,135),width=11)
im.resize((64,128),Image.Resampling.LANCZOS).save(out/"TyreTread.png")
