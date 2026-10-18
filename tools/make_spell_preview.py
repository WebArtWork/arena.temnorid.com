"""Contact sheet of actual Blender renders, with no generated or painted-over panels."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent.parent / 'output' / 'spells'
sheet = Image.new('RGB', (1400, 1180), (9, 12, 17))
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 26)
title = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 34)
draw.text((30, 20), 'TEMNORID ARENA  /  ELEMENTAL SPELL STUDIES', font=title, fill=(220, 230, 240))
items = [('fire/fireball-blender.png', 'FIREBALL  /  combustion and embers'),
         ('frost-nova-blender.png', 'FROST NOVA  /  freezing front'),
         ('arcane-shield-blender.png', 'ARCANE SHIELD  /  transparent field'),
         ('blink-blender.png', 'BLINK  /  displacement wisps')]
for i, (filename, label) in enumerate(items):
    x, y = (i % 2) * 700, 86 + (i // 2) * 544
    image = Image.open(root / filename).convert('RGB')
    image.thumbnail((680, 480), Image.Resampling.LANCZOS)
    sheet.paste(image, (x + (700 - image.width) // 2, y + (480 - image.height) // 2))
    draw.text((x + 24, y + 488), label, font=font, fill=(197, 210, 228))
sheet.save(root / 'spell-studies.jpg', quality=94)
print(root / 'spell-studies.jpg')
