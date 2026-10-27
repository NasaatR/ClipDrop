"""Render the original ClipDrop vector geometry into a multi-size Windows icon.
Requires Pillow only for developers rebuilding the committed icon; no app dependency.
Coordinates match src/ClipDrop.Windows/Assets/ClipDrop.svg.
"""
from pathlib import Path
from PIL import Image, ImageDraw
scale = 4
image = Image.new('RGBA', (256*scale, 256*scale))
draw = ImageDraw.Draw(image)
def box(coords, radius, color):
    draw.rounded_rectangle(tuple(int(v*scale) for v in coords), radius=radius*scale, fill=color)
box((8,8,248,248),58,'#24262e')
box((55,52,173,196),25,'#689df5')
box((82,66,200,210),25,'#ffffff')
for end,y in ((171,111),(158,134)):
    draw.line([(111*scale,y*scale),(end*scale,y*scale)],fill='#596477',width=12*scale)
    for x in (111,end): draw.ellipse(((x-6)*scale,(y-6)*scale,(x+6)*scale,(y+6)*scale),fill='#596477')
draw.polygon([(176*scale,152*scale),(153*scale,188*scale),(150*scale,198*scale),(202*scale,198*scale),(199*scale,188*scale)], fill='#689df5')
draw.ellipse((150*scale,172*scale,202*scale,224*scale), fill='#689df5')
image = image.resize((256,256), Image.Resampling.LANCZOS)
root = Path(__file__).resolve().parents[1]/'src/ClipDrop.Windows/Assets'
image.save(root/'ClipDrop.ico', sizes=[(s,s) for s in (16,24,32,48,64,128,256)])
