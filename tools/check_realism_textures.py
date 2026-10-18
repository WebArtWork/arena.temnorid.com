from pathlib import Path
from PIL import Image
import numpy as np,json
ROOT=Path(__file__).resolve().parent.parent
results=[]
for name in ['stone-floor','stone-wall']:
    data={}
    for suffix in ['basecolor','normal','roughness','ao','metallic-smoothness']:
        image=Image.open(ROOT/'Assets'/'Models'/'materials'/(name+'_'+suffix+'.png')).convert('RGBA')
        assert image.size==(2048,2048),(name,suffix,image.size)
        pixels=np.asarray(image)
        assert np.array_equal(pixels[:,0],pixels[:,-1]),(name,suffix,'horizontal seam')
        if name=='stone-floor':assert np.array_equal(pixels[0],pixels[-1]),(name,suffix,'vertical seam')
        data[suffix]=pixels
    error=np.abs(data['roughness'][...,0].astype(int)+data['metallic-smoothness'][...,3].astype(int)-255)
    assert error.max()<=1,(name,'roughness/smoothness packing')
    results.append({'name':name,'resolution':[2048,2048],'horizontal_seam':'PASS','vertical_seam':'PASS' if name=='stone-floor' else 'not required','smoothness_alpha':'PASS'})
(ROOT/'output'/'realism'/'texture-validation.json').write_text(json.dumps(results,indent=2))
print(json.dumps(results,indent=2))
