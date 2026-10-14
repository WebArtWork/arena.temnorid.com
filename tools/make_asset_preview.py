"""Assemble actual Blender renders into a labeled contact sheet."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
root=Path(__file__).resolve().parent.parent
names=['arena-pillar','arena-crate','arena-platform','arena-wall-segment','arena-low-wall',
       'arena-broken-pillar','arena-rubble','arena-stairs','arena-brazier','arena-spawn-circle',
       'arena-banner','stone-floor','stone-wall']
labels=['Stone pillar','Wooden crate','Central platform','Wall segment','Low wall',
        'Broken pillar','Rubble','Stairs','Brazier','Spawn circle','Banner','Stone floor','Stone wall']
font_path=Path('C:/Windows/Fonts/segoeui.ttf')
font=ImageFont.truetype(str(font_path),22)
title_font=ImageFont.truetype(str(font_path),36)
sheet=Image.new('RGB',(1600,1840),(29,27,26));draw=ImageDraw.Draw(sheet)
draw.text((32,22),'TEMNORID ARENA · ACTUAL ASSET RENDERS',font=title_font,fill=(232,223,205))
for i,(name,label) in enumerate(zip(names,labels)):
    col=i%4;row=i//4;x=col*400+14;y=82+row*435
    im=Image.open(root/'output/arena'/f'{name}.png').convert('RGB').resize((372,372),Image.Resampling.LANCZOS)
    sheet.paste(im,(x,y));draw.text((x+4,y+380),label,font=font,fill=(232,223,205))
sheet.save(root/'output/arena/asset-preview-sheet.jpg',quality=92)
# Verify pixel edge continuity and texture dimensions directly on delivered PNGs.
checks=[]
for kind in ['stone-floor','stone-wall']:
    for suffix in ['basecolor','normal','roughness']:
        file=root/'Assets/Models/materials'/f'{kind}_{suffix}.png'
        im=Image.open(file).convert('RGB');assert im.size==(2048,2048)
        assert im.crop((0,0,1,2048)).tobytes()==im.crop((2047,0,2048,2048)).tobytes()
        if kind=='stone-floor':
            assert im.crop((0,0,2048,1)).tobytes()==im.crop((0,2047,2048,2048)).tobytes()
        checks.append(file.name)
import json
(root/'output/arena/texture-check.json').write_text(json.dumps({'status':'PASS','checked':checks,
    'floor':'horizontal and vertical edges match','wall':'horizontal edges match'},indent=2))
print('Preview sheet saved; six 2048 PBR maps passed edge-continuity checks.')
