"""Reproducible Blender build of the first Temnorid mage prototype.
Run: blender --background --python tools/build_mage.py
"""
import bpy, math, random, os, json, sys
from mathutils import Vector
from math import sin, cos, pi

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Models', 'characters')
PREVIEW = os.path.join(ROOT, 'output', 'mage')
os.makedirs(OUT, exist_ok=True)
os.makedirs(PREVIEW, exist_ok=True)
random.seed(27)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.render.fps = 30

# Coordinate convention while authoring: Z up, -Y forward. glTF exports Y up, +Z forward.
cloth = (.045, .038, .032, 1)
cloth_light = (.073, .063, .051, 1)
leather = (.028, .014, .007, 1)
edge = (.095, .052, .022, 1)
skin = (.49, .29, .18, 1)
metal = (.36, .26, .13, 1)
dark = (.025, .020, .016, 1)
purple = (.045, .025, .095, 1)

# Embedded 2K microfabric texture. COLOR_0 supplies the garment / skin / leather palette.
import numpy as np
rng = np.random.default_rng(27)
n = 2048
yy, xx = np.mgrid[:n, :n]
grain = rng.random((n,n))
weave = .83 + .07*np.sin(xx*pi/3)*np.sin(yy*pi/3) + .08*grain
rgba = np.ones((n,n,4), dtype=np.float32)
rgba[:,:,:3] = weave[:,:,None]
texture = bpy.data.images.new('mage-fabric-basecolor', width=n, height=n)
texture.pixels.foreach_set(rgba.ravel())
texture.filepath_raw = os.path.join(PREVIEW, 'mage-basecolor.png')
texture.file_format = 'PNG'
texture.save()
texture.pack()

def material(name, accent=False):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    p = nodes.get('Principled BSDF')
    p.inputs['Roughness'].default_value = .78 if not accent else .64
    if accent:
        p.inputs['Base Color'].default_value = purple
        p.inputs['Emission Color'].default_value = (.13,.065,.27,1)
        p.inputs['Emission Strength'].default_value = .22
    else:
        attr = nodes.new('ShaderNodeVertexColor'); attr.layer_name = 'Color'
        tex = nodes.new('ShaderNodeTexImage'); tex.image = texture
        multiply = nodes.new('ShaderNodeMixRGB'); multiply.blend_type = 'MULTIPLY'
        multiply.inputs[0].default_value = 1
        links.new(attr.outputs['Color'], multiply.inputs[1])
        links.new(tex.outputs['Color'], multiply.inputs[2])
        links.new(multiply.outputs[0], p.inputs['Base Color'])
    return mat
BODY, ACCENT = material('body'), material('accent', True)
# Exportable PBR materials: surface relief is an embedded tangent-space normal map.
def surface(name, roughness, metallic=0, relief=.25):
    mat = material(name)
    p = mat.node_tree.nodes.get('Principled BSDF')
    p.inputs['Roughness'].default_value = roughness
    p.inputs['Metallic'].default_value = metallic
    h = (.5*np.sin(xx*pi/3)*np.sin(yy*pi/3) if name=='woven-wool'
         else rng.random((n,n))*.25)
    gy,gx = np.gradient(h)
    normal = np.stack((-gx*relief,-gy*relief,np.ones_like(h)),axis=-1)
    normal /= np.linalg.norm(normal,axis=-1,keepdims=True)
    pixels = np.ones((n,n,4),dtype=np.float32)
    pixels[:,:,:3] = normal*.5+.5
    im=bpy.data.images.new(name+'-normal',width=n,height=n)
    im.colorspace_settings.name='Non-Color'
    im.pixels.foreach_set(pixels.ravel()); im.pack()
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im
    nm=mat.node_tree.nodes.new('ShaderNodeNormalMap')
    mat.node_tree.links.new(tex.outputs['Color'],nm.inputs['Color'])
    mat.node_tree.links.new(nm.outputs['Normal'],p.inputs['Normal'])
    return mat
WOOL=surface('woven-wool',.9,relief=.18)
LEATHER=surface('worn-leather',.56,relief=.8)
SKIN=surface('skin',.52,relief=.12)
BRONZE=surface('aged-bronze',.38,.78,relief=.18)
HAIR=surface('hair',.83,relief=.3)
EYE=material('eyes');EYE.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.24
ACCENT.node_tree.nodes.get('Principled BSDF').inputs['Emission Strength'].default_value=0
parts = []
bone_specs = {}
def bone(name, head, tail, parent=None):
    bone_specs[name] = (head, tail, parent)

bone('Hips',(0,0,.91),(0,0,1.04))
bone('Spine',(0,0,1.04),(0,0,1.20),'Hips')
bone('Spine1',(0,0,1.20),(0,0,1.36),'Spine')
bone('Spine2',(0,0,1.36),(0,0,1.46),'Spine1')
bone('Neck',(0,0,1.46),(0,0,1.54),'Spine2')
bone('Head',(0,0,1.54),(0,0,1.76),'Neck')
for sign, side in [(-1,'Left'),(1,'Right')]:
    s=sign
    bone(side+'Shoulder',(s*.055,0,1.43),(s*.19,0,1.43),'Spine2')
    bone(side+'Arm',(s*.19,0,1.43),(s*.275,0,1.17),side+'Shoulder')
    bone(side+'ForeArm',(s*.275,0,1.17),(s*.32,-.018,.96),side+'Arm')
    bone(side+'Hand',(s*.32,-.018,.96),(s*.33,-.02,.86),side+'ForeArm')
    bone(side+'UpLeg',(s*.09,0,.98),(s*.10,-.02,.57),'Hips')
    bone(side+'Leg',(s*.10,-.02,.57),(s*.10,0,.12),side+'UpLeg')
    bone(side+'Foot',(s*.10,0,.12),(s*.10,-.12,.055),side+'Leg')
    bone(side+'ToeBase',(s*.10,-.12,.055),(s*.10,-.20,.055),side+'Foot')
    for f, label in enumerate(['Thumb','Index','Middle','Ring','Pinky']):
        bx=s*(.305+f*.013)
        for j in range(3):
            z=.89-j*.022
            bone(side+'Hand'+label+str(j+1),(bx,-.025,z),(bx,-.025,z-.022),
                 side+'Hand' if j==0 else side+'Hand'+label+str(j))

def finish(obj, color, joint, accent=False, smooth=True):
    bpy.context.view_layer.objects.active=obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    surf = (SKIN if color==skin else BRONZE if color==metal else
            LEATHER if color in (leather,edge) else WOOL if color in (cloth,cloth_light) else HAIR)
    if obj.name.startswith(('eye','iris')): surf=EYE
    obj.data.materials.append(ACCENT if accent else surf)
    ca=obj.data.color_attributes.new(name='Color', type='BYTE_COLOR', domain='CORNER')
    for item in ca.data:
        v=1+random.uniform(-.025,.025)
        item.color=tuple(min(1,c*v) for c in color[:3])+(1,)
    if not obj.data.uv_layers:
        uv=obj.data.uv_layers.new(name='UVMap')
        for polygon in obj.data.polygons:
            for li in polygon.loop_indices:
                co=obj.data.vertices[obj.data.loops[li].vertex_index].co
                uv.data[li].uv=(co.x*2+.5,co.z*.8)
    for p in obj.data.polygons: p.use_smooth=smooth
    vg=obj.vertex_groups.new(name=joint)
    vg.add(list(range(len(obj.data.vertices))),1,'REPLACE')
    parts.append(obj)
    obj.select_set(False)
    return obj

def mesh(name, verts, faces, color, joint, accent=False):
    data=bpy.data.meshes.new(name); data.from_pydata(verts,[],faces); data.update()
    obj=bpy.data.objects.new(name,data); scene.collection.objects.link(obj)
    return finish(obj,color,joint,accent)

def loft(name, rings, color, joint, sides=32, folds=.0, accent=False, caps=True):
    # Rings: cx, cy, z, radius-x, radius-y.
    # Dense vertical sampling gives cloth compression folds a rounded silhouette.
    if folds and len(rings)>2:
        dense=[]
        for a,b in zip(rings,rings[1:]):
            for k in range(5):
                t=k/5;dense.append(tuple(x*(1-t)+y*t for x,y in zip(a,b)))
        rings=dense+[rings[-1]]
    vs=[]; fs=[]
    for ri,(cx,cy,z,rx,ry) in enumerate(rings):
        for j in range(sides):
            a=2*pi*j/sides
            f=1+folds*(sin(a*9+z*3)+.4*sin(a*15-z*5))
            if folds:
                f+=folds*.65*sin(z*95+a*3)*(.4+.6*sin(a*2)**2)
            vs.append((cx+rx*cos(a)*f,cy+ry*sin(a)*f,z))
    for r in range(len(rings)-1):
        for j in range(sides):
            a=r*sides+j; b=r*sides+(j+1)%sides
            fs.append((a,b,b+sides,a+sides))
    if caps:
        fs.extend([tuple(reversed(range(sides))),tuple((len(rings)-1)*sides+j for j in range(sides))])
    return mesh(name,vs,fs,color,joint,accent)

def orb(name, pos, scale, color, joint, accent=False, segments=24, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=pos)
    ob=bpy.context.object; ob.name=name; ob.scale=scale
    return finish(ob,color,joint,accent)

def cube(name,pos,scale,color,joint,bevel=.01,accent=False):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos)
    ob=bpy.context.object; ob.name=name; ob.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=ob.modifiers.new('worn-edge','BEVEL');mod.width=bevel;mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(ob,color,joint,accent)

def cord(name, points, radius, color, joint, accent=False):
    cv=bpy.data.curves.new(name,'CURVE');cv.dimensions='3D';cv.bevel_depth=radius;cv.bevel_resolution=2
    sp=cv.splines.new('POLY');sp.points.add(len(points)-1)
    for p,co in zip(sp.points,points):p.co=(*co,1)
    ob=bpy.data.objects.new(name,cv);scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active=ob; ob.select_set(True)
    bpy.ops.object.convert(target='MESH')
    return finish(bpy.context.object,color,joint,accent)

# Torso: tailored tunic, folds, shoulder mantle and separate waist layer.
loft('tailored-tunic',[(0,0,1.0,.16,.095),(0,0,1.08,.145,.087),(0,0,1.18,.16,.09),
    (0,0,1.30,.19,.106),(0,0,1.39,.195,.10),(0,0,1.45,.13,.085)],cloth,'Spine1',48,.022)
loft('hips-trousers',[(0,0,.88,.145,.087),(0,0,1.04,.155,.087)],cloth,'Hips')
loft('shoulder-cowl',[(0,.012,1.31,.21,.115),(0,.01,1.34,.235,.133),
    (0,.005,1.39,.238,.14),(0,0,1.43,.21,.126),(0,0,1.48,.12,.095)],cloth_light,'Spine2',64,.025)
pts=[(.214*cos(a),.012+.120*sin(a),1.32) for a in np.linspace(0,2*pi,65)]
cord('cowl-layer-seam',pts,.002,edge,'Spine2')

# Split skirt panels, double-layered and stitched; hem stays above knees.
for i in range(10):
    a0=2*pi*i/10+.035; a1=2*pi*(i+1)/10-.035
    vs=[]; fs=[]
    for row in range(8):
        t=row/7
        for col in range(9):
            a=a0+(a1-a0)*col/8
            z=1.04-.32*t + .012*sin(col*pi/4)*t
            rx=.157+.072*t; ry=.10+.031*t
            rr=1+.028*sin(col*pi/2)*sin(t*pi)
            vs.append((rx*cos(a)*rr,ry*sin(a)*rr,z))
    for row in range(7):
        for col in range(8):
            v=row*9+col;fs.append((v,v+1,v+10,v+9))
    ob=mesh('split-robe-panel',vs,fs,cloth_light if i%2 else cloth,'Hips')
    bpy.context.view_layer.objects.active=ob
    mod=ob.modifiers.new('cloth-thickness','SOLIDIFY'); mod.thickness=.004
    bpy.ops.object.modifier_apply(modifier=mod.name)
    cord('robe-hem',[vs[63+j] for j in range(9)],.003,edge,'Hips')
    for col in [0,8]:cord('panel-seam',[vs[row*9+col] for row in range(8)],.0025,edge,'Hips')

# Belt, inset bronze buckle, pouches and accent sash.
loft('leather-waist-belt',[(0,0,1.035,.166,.109),(0,0,1.075,.161,.105)],leather,'Hips',64)
for z in [1.039,1.071]:
    cord('belt-edge',[(.166*cos(a),.109*sin(a),z) for a in np.linspace(0,2*pi,65)],.0025,edge,'Hips')
cube('buckle',(0,-.12,1.055),(.055,.018,.05),metal,'Hips',.004)
cube('buckle-inset',(0,-.131,1.055),(.034,.005,.028),dark,'Hips',.002)
for s in [-1,1]:
    cube('belt-pouch',(s*.15,-.076,.99),(.07,.058,.10),leather,'Hips',.012)
    cube('pouch-flap',(s*.15,-.109,1.018),(.075,.018,.040),edge,'Hips',.006)
    orb('pouch-button',(s*.15,-.122,1.014),(.007,.004,.007),metal,'Hips',segments=12,rings=6)
loft('accent-waist-sash',[(0,0,1.085,.163,.103),(0,0,1.109,.162,.10)],purple,'Hips',64,.01,True)
mesh('hanging-accent-sash',[(.06,-.118,1.10),(.125,-.11,1.10),(.148,-.148,.76),
    (.122,-.15,.715),(.089,-.152,.77)],[(0,1,2,3,4)],purple,'Hips',True)

for s,side in [(-1,'Left'),(1,'Right')]:
    # Leg folds and boots, with visible cuffs, soles, straps and buckles.
    loft(side+'-thigh',[(s*.09,0,.98,.077,.072),(s*.095,0,.88,.078,.069),
        (s*.10,-.012,.73,.068,.063),(s*.10,-.02,.57,.058,.055)],cloth,side+'UpLeg',32,.045)
    loft(side+'-trouser-calf',[(s*.10,-.02,.58,.058,.055),(s*.10,-.01,.48,.057,.052),
        (s*.10,0,.35,.043,.042),(s*.10,0,.13,.035,.037)],cloth,side+'Leg',32,.04)
    loft(side+'-boot',[(s*.10,0,.075,.048,.053),(s*.10,0,.16,.044,.044),
        (s*.10,0,.30,.050,.047),(s*.10,-.002,.39,.056,.05)],leather,side+'Leg',32,.02)
    loft(side+'-boot-cuff',[(s*.10,-.001,.35,.059,.054),(s*.10,-.001,.392,.059,.054)],edge,side+'Leg')
    orb(side+'-foot',(s*.10,-.063,.075),(.058,.136,.07),leather,side+'Foot',segments=32,rings=12)
    cube(side+'-sole',(s*.10,-.063,.017),(.119,.272,.034),dark,side+'Foot',.012)
    for z in [.15,.31]:
        loft(side+'-boot-strap',[(s*.10,0,z,.054,.052),(s*.10,0,z+.021,.054,.052)],edge,side+'Leg')
        cube('boot-buckle',(s*.156,-.008,z+.011),(.014,.026,.026),metal,side+'Leg',.002)
    # Sleeves track upper and lower arm bones; elbow overlaps avoid exposed cracks.
    loft(side+'-upper-sleeve',[(s*.185,0,1.445,.075,.077),(s*.22,0,1.37,.079,.079),
        (s*.25,0,1.27,.064,.065),(s*.275,0,1.155,.053,.054)],cloth,side+'Arm',32,.045)
    orb('elbow-cloth',(s*.275,0,1.17),(.055,.055,.065),cloth,side+'ForeArm')
    loft(side+'-lower-sleeve',[(s*.273,0,1.18,.054,.053),(s*.294,-.008,1.08,.05,.047),
        (s*.319,-.018,.955,.036,.036)],cloth,side+'ForeArm',32,.035)
    loft(side+'-bracer',[(s*.293,-.007,1.10,.052,.051),(s*.302,-.011,1.055,.048,.048),
        (s*.321,-.018,.965,.041,.04)],leather,side+'ForeArm',32)
    for z,rx,cx in [(1.10,.054,.293),(.975,.044,.319)]:
        loft('bracer-rim',[(s*cx,-.015,z,rx,.052 if z>1 else .043),
            (s*cx,-.015,z+.012,rx,.052 if z>1 else .043)],edge,side+'ForeArm')
    # Arcane inset rune: diamond with inner stroke, the recolorable accent material.
    x=s*.307; y=-.065; z=1.035
    cord('bracer-rune',[(x,y,z+.035),(x+s*.022,y,z),(x,y,z-.035),(x-s*.022,y,z),(x,y,z+.035)],.003,purple,side+'ForeArm',True)
    cord('rune-spine',[(x,y-.001,z-.019),(x,y-.001,z+.019)],.0025,purple,side+'ForeArm',True)
    orb(side+'-palm',(s*.325,-.02,.911),(.029,.019,.050),skin,side+'Hand')
    for f,label in enumerate(['Thumb','Index','Middle','Ring','Pinky']):
        bx=s*(.305+f*.013)
        for j in range(3):
            z=.89-j*.022
            orb(side+'-'+label+'-'+str(j),(bx,-.025,z-.011),(.0065,.008,.014),skin,
                side+'Hand'+label+str(j+1),segments=12,rings=6)

# Face silhouette with cheekbones, brow, nose, lips, eyelids, ears and hair.
loft('neck',[(0,0,1.45,.043,.043),(0,0,1.56,.043,.044)],skin,'Neck')
loft('high-cowl-collar',[(0,0,1.44,.088,.075),(0,0,1.49,.077,.069),
    (0,0,1.52,.058,.056)],cloth_light,'Neck',48,.035)
face_start=len(parts)
orb('head',(0,-.012,1.641),(.072,.064,.108),skin,'Head',segments=48,rings=32)
orb('jaw',(0,-.027,1.583),(.052,.048,.046),skin,'Head')
for s in [-1,1]:
    orb('cheek',(s*.039,-.051,1.625),(.025,.019,.027),skin,'Head')
    orb('ear',(s*.079,-.009,1.63),(.013,.012,.027),skin,'Head')
orb('nose-bridge',(0,-.069,1.631),(.009,.013,.026),skin,'Head')
orb('nose-tip',(0,-.081,1.613),(.011,.013,.009),skin,'Head')
orb('chin',(0,-.048,1.572),(.029,.025,.021),skin,'Head')
# Fuse facial anatomy into one continuous surface instead of intersecting primitives.
bpy.ops.object.select_all(action='DESELECT')
face_parts=parts[face_start:]
for ob in face_parts:ob.select_set(True)
bpy.context.view_layer.objects.active=face_parts[0];bpy.ops.object.join()
face=bpy.context.object
rem=face.modifiers.new('continuous-anatomy','REMESH');rem.mode='VOXEL';rem.voxel_size=.0018
bpy.ops.object.modifier_apply(modifier=rem.name)
sm=face.modifiers.new('soft-tissue','SMOOTH');sm.factor=.6;sm.iterations=5
bpy.ops.object.modifier_apply(modifier=sm.name)
parts[face_start:]=[]
face.data.materials.clear()
for ca in list(face.data.color_attributes):face.data.color_attributes.remove(ca)
face.vertex_groups.clear()
finish(face,skin,'Head')
for s in [-1,1]:
    orb('eye',(s*.028,-.076,1.649),(.012,.005,.0045),(.34,.32,.28,1),'Head')
    orb('iris',(s*.028,-.081,1.649),(.0028,.0018,.0034),dark,'Head',segments=16,rings=8)
    for upper in [True,False]:
        pts=[]
        for t in np.linspace(0,pi,18):
            pts.append((s*.028+.013*cos(t),-.076-.003*sin(t),1.649+(.0055 if upper else -.004)*sin(t)))
        cord('eyelid',pts,.002,skin,'Head')
    cord('brow',[(s*.014,-.068,1.663),(s*.028,-.070,1.666),(s*.045,-.061,1.662)],.0022,dark,'Head')
cord('mouth',[(-.018,-.067,1.594),(-.007,-.073,1.594),(0,-.074,1.593),(.007,-.073,1.594),(.018,-.067,1.594)],.0012,(.20,.09,.055,1),'Head')
orb('hair-cap',(0,.012,1.705),(.073,.058,.049),dark,'Head')
for i in range(23):
    x=(i-11)*.004
    cord('swept-hair',[(x,.0,1.737),(x-.006,-.042,1.718),(x-.010,-.058,1.690+abs(x)*.15)],.0035,dark,'Head')

# Open hood: no surface across the face. Outer/back shell with an actual face opening.
vs=[];fs=[];sides=48;rows=18
for r in range(rows):
    t=r/(rows-1)
    z=1.50+.30*t
    # Broad neck opening tightens toward the pointed crown.
    rx=.13*(1-t)**.45+.006; ry=.105*(1-t)**.4+.006
    for j in range(sides):
        a=-.35*pi+1.70*pi*j/(sides-1)
        # positive Y back, opening toward -Y
        vs.append((rx*cos(a),.015+ry*sin(a),z+.008*sin(a*6)*sin(t*pi)))
for r in range(rows-1):
    for j in range(sides-1):
        v=r*sides+j;fs.append((v,v+1,v+1+sides,v+sides))
hood=mesh('open-cowl-hood',vs,fs,cloth,'Head')
bpy.context.view_layer.objects.active=hood
mod=hood.modifiers.new('hood-thickness','SOLIDIFY');mod.thickness=.006
bpy.ops.object.modifier_apply(modifier=mod.name)
for col in [0,sides-1]:
    pts=[vs[r*sides+col] for r in range(rows)]
    cord('violet-hood-piping',pts,.007,purple,'Head',True)
    cord('hood-outer-seam',[(x*1.045,y,z) for x,y,z in pts],.003,edge,'Head')
cord('hood-centre-seam',[(0,.015+.105*(1-t)**.4,1.5+.30*t) for t in np.linspace(0,1,24)],.003,edge,'Head')

# Diagonal chest binding and bronze cowl clasp.
cord('chest-binding',[(-.11,-.101,1.40),(-.055,-.12,1.29),(.04,-.105,1.13)],.012,leather,'Spine1')
orb('cowl-clasp',(-.135,-.104,1.39),(.023,.009,.023),metal,'Spine2')
orb('clasp-inset',(-.135,-.114,1.39),(.014,.003,.014),dark,'Spine2')

# Join to one skinned mesh and improve silhouette resolution within the triangle budget.
bpy.ops.object.select_all(action='DESELECT')
for ob in parts:ob.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.join()
model=bpy.context.object;model.name='mage-mesh'
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')
tri=model.modifiers.new('triangulate','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
tri_count=len(model.data.polygons)
if tri_count>65000:
    dec=model.modifiers.new('budget','DECIMATE');dec.ratio=64000/tri_count
    bpy.ops.object.modifier_apply(modifier=dec.name)
tri_count=sum(len(p.vertices)-2 for p in model.data.polygons)
# Normalize exact rest bounds without leaving object scale transforms.
zmax=max(v.co.z for v in model.data.vertices)
xmax=max(abs(v.co.x) for v in model.data.vertices)
sx=min(1,.345/xmax); sz=1.8/zmax
for v in model.data.vertices:
    v.co.x*=sx;v.co.z*=sz
bone_specs={name:((h[0]*sx,h[1],h[2]*sz),(t[0]*sx,t[1],t[2]*sz),parent)
            for name,(h,t,parent) in bone_specs.items()}

# Mixamo-style humanoid skeleton, no root translation in locomotion.
arm=bpy.data.armatures.new('mage-humanoid')
rig=bpy.data.objects.new('mage',arm);scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
for name,(head,tail,parent) in bone_specs.items():
    eb=arm.edit_bones.new(name);eb.head=head;eb.tail=tail
    if parent:eb.parent=arm.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
# Keep the skinned mesh at scene root (glTF skin transforms ignore mesh parents).
model.parent=None
mod=model.modifiers.new('humanoid-skin','ARMATURE');mod.object=rig
# Blend garment sections across the spine and elbow/knee intersections.
for vertex in model.data.vertices:
    groups=[(model.vertex_groups[g.group].name,g.weight) for g in vertex.groups]
    if not groups:continue
    name=groups[0][0];z=vertex.co.z
    if name=='Spine1':
        weights={'Hips':max(0,1-(z-1.0)/.19),'Spine':max(0,1-abs(z-1.17)/.19),
                 'Spine1':max(0,1-abs(z-1.31)/.19),'Spine2':max(0,(z-1.32)/.13)}
        total=sum(weights.values())
        if total:
            for vg in model.vertex_groups:vg.remove([vertex.index])
            for bn,w in weights.items():
                if w:
                    vg=model.vertex_groups.get(bn) or model.vertex_groups.new(name=bn)
                    vg.add([vertex.index],w/total,'REPLACE')

clips=[('idle',2.4),('walk',1),('run',.7),('strafe-left',1),('strafe-right',1),
       ('walk-back',1),('crouch-idle',1),('crouch-walk',1),('jump-start',.2),('fall',1),
       ('land',.3),('cast-projectile',.4),('cast-nova',.6),('cast-shield',.5),
       ('cast-blink',.3),('hit',.3),('death',1.2)]
rig.animation_data_create()
for name,duration in clips:
    action=bpy.data.actions.new(name);rig.animation_data.action=action
    frames=round(duration*30)
    for frame in range(frames+1):
        t=frame/frames; wave=sin(t*2*pi);pulse=sin(t*pi)
        for pb in rig.pose.bones:
            pb.rotation_mode='XYZ';pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
        def rot(b,x=0,y=0,z=0):rig.pose.bones[b].rotation_euler=(x,y,z)
        if name in ['walk','run','walk-back','crouch-walk','strafe-left','strafe-right']:
            amp=.6 if name=='run' else .35
            if name=='walk-back':amp=-.28
            for side,sign in [('Left',1),('Right',-1)]:
                rot(side+'UpLeg',sign*amp*wave)
                rot(side+'Leg',max(0,-sign*wave)*.6)
                rot(side+'Arm',-sign*amp*.65*wave)
                rot(side+'ForeArm',-.20)
            if name.startswith('strafe'):
                direction=1 if name=='strafe-left' else -1
                rot('LeftUpLeg',.08,y=direction*.25*wave)
                rot('RightUpLeg',-.08,y=-direction*.25*wave)
            rot('Spine',.12 if name=='run' else .035)
        elif name=='idle':
            rot('Spine1',.018*wave);rot('LeftArm',-.08+.025*wave);rot('RightArm',-.08-.025*wave)
        elif name.startswith('cast'):
            if name=='cast-projectile':
                rot('RightArm',-1.3*pulse);rot('RightForeArm',-.45*pulse);rot('Spine2',0,0,.18*pulse)
            elif name=='cast-nova':
                rot('LeftArm',-1.1*pulse,0,-.45*pulse);rot('RightArm',-1.1*pulse,0,.45*pulse)
                rot('Spine',.3*pulse)
            elif name=='cast-shield':
                rot('LeftArm',-1.1*pulse,0,.35*pulse);rot('RightArm',-1.1*pulse,0,-.35*pulse)
                rot('LeftForeArm',-1.1*pulse);rot('RightForeArm',-1.1*pulse)
            else:rot('Spine',.5*pulse);rot('LeftUpLeg',-.5*pulse);rot('RightUpLeg',-.5*pulse)
        elif name in ['jump-start','land']:
            for side in ['Left','Right']:
                rot(side+'UpLeg',-.5*pulse);rot(side+'Leg',.8*pulse)
            rot('Spine',.3*pulse)
        elif name=='fall':
            rot('LeftArm',-.3,0,-.25);rot('RightArm',-.3,0,.25);rot('LeftLeg',.2)
        elif name=='hit':rot('Spine1',-.25*pulse);rot('Head',.18*pulse)
        elif name=='death':
            rot('Hips',-pi/2*t);rig.pose.bones['Hips'].location.y=-.77*t
            rot('LeftArm',-.4*t,0,-.35*t);rot('RightArm',-.4*t,0,.35*t)
        if name.startswith('crouch'):
            rig.pose.bones['Hips'].location.y=-.58
            for side in ['Left','Right']:
                rot(side+'UpLeg',-1.13+(.15*wave if name=='crouch-walk' else 0))
                rot(side+'Leg',1.8);rot(side+'Foot',-.67)
            rot('Spine',.35)
        for pb in rig.pose.bones:
            pb.keyframe_insert('rotation_euler',frame=frame+1,group=pb.name)
            if pb.name=='Hips':pb.keyframe_insert('location',frame=frame+1,group=pb.name)
    track=rig.animation_data.nla_tracks.new();track.name=name
    strip=track.strips.new(name,1,action);track.mute=True
rig.animation_data.action=None
for pb in rig.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
scene.frame_set(1)
scene.frame_end=72

bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);model.select_set(True)
bpy.context.view_layer.objects.active=rig
glb=os.path.join(OUT,'mage.glb')
bpy.ops.export_scene.gltf(filepath=glb,export_format='GLB',use_selection=True,
    export_animations=True,export_animation_mode='NLA_TRACKS',export_nla_strips=True,
    export_force_sampling=True,export_frame_range=False,export_yup=True,
    export_materials='EXPORT',export_skins=True,export_all_influences=False)
rig.animation_data.action=None
for track in rig.animation_data.nla_tracks:track.mute=True
for pb in rig.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
scene.frame_set(1)
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PREVIEW,'mage.blend'))
coords=[v.co for v in model.data.vertices]
report={'triangles':tri_count,'bones':len(bone_specs),'clips':[n for n,d in clips],
    'bounds_blender':[[min(v[i] for v in coords),max(v[i] for v in coords)] for i in range(3)],
    'glb_bytes':os.path.getsize(glb),'notes':['Procedural prototype, not a reconstruction of the concept image.',
    'Basic procedural animation; requires animation and deformation polish.',
    'Separate wool, leather, skin, bronze, hair, eye and accent PBR materials.',
    'Embedded 2K surface normal textures; continuous remeshed facial anatomy.']}
with open(os.path.join(PREVIEW,'build-report.json'),'w') as f:json.dump(report,f,indent=2)
if '--no-render' in sys.argv:
    print('MAGE_BUILD_REPORT '+json.dumps(report))
    sys.exit(0)

# Render the actual mesh; studio objects are added only AFTER GLB export and source save.
rig.animation_data_clear()
for pb in rig.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
bpy.context.view_layer.update()
scene.render.engine='CYCLES';scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.render.resolution_x=1200;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
scene.world.color=(.15,.15,.15)
world=scene.world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.19,.17,.15,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.5
def aim(obj,point):obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()
def area(name,pos,power,size):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    ob=bpy.data.objects.new(name,data);scene.collection.objects.link(ob);ob.location=pos;aim(ob,(0,0,1))
area('key',(-2,-3,4),450,3);area('fill',(2,-2,2),220,2);area('rim',(0,2,3),500,2)
bpy.ops.mesh.primitive_plane_add(size=200)
floor=bpy.context.object;floor.name='studio-floor';floor.location.z=-.003
fm=bpy.data.materials.new('studio');fm.diffuse_color=(.16,.14,.12,1);floor.data.materials.append(fm)
camdata=bpy.data.cameras.new('preview');cam=bpy.data.objects.new('preview',camdata)
scene.collection.objects.link(cam);scene.camera=cam;camdata.type='ORTHO';camdata.ortho_scale=2.14
for name,pos in [('front',(0,-4,1.15)),('three-quarter',(2.6,-4,1.65)),('back',(0,4,1.15))]:
    cam.location=pos;aim(cam,(0,0,.91))
    scene.render.filepath=os.path.join(PREVIEW,'mage-'+name+'.png')
    bpy.ops.render.render(write_still=True)
print('MAGE_BUILD_REPORT '+json.dumps(report))
