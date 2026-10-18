from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json
ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'output'/'realism'
report={r['name']:r for r in json.loads((OUT/'build-report.json').read_text())}
names=['mage','arena-pillar','arena-crate','arena-platform','arena-wall-segment','arena-low-wall',
       'arena-broken-pillar','arena-rubble','arena-stairs','arena-brazier','arena-spawn-circle','arena-banner',
       'stone-floor','stone-wall']
sheet=Image.new('RGB',(1600,1930),(16,20,26));draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',23)
small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
title=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',34)
draw.text((24,14),'TEMNORID ARENA  /  SHARED MODEL REALISM PASS',font=title,fill=(226,232,240))
draw.text((25,57),'Actual Unity URP renders of the exported game assets',font=small,fill=(154,171,190))
for i,name in enumerate(names):
    x,y=(i%4)*400+10,95+(i//4)*455
    image=Image.open(OUT/(name+'-unity.png')).convert('RGB');image.thumbnail((380,380),Image.Resampling.LANCZOS)
    sheet.paste(image,(x+(380-image.width)//2,y+(380-image.height)//2))
    label=name.replace('arena-','').replace('-',' ').capitalize()
    draw.text((x+4,y+384),label,font=font,fill=(213,224,238))
    detail=f"{report[name]['triangles']:,} triangles" if name in report else '2K PBR surface'
    draw.text((x+4,y+416),detail,font=small,fill=(148,168,191))
sheet.save(OUT/'model-preview-sheet.jpg',quality=94)
print(OUT/'model-preview-sheet.jpg')
