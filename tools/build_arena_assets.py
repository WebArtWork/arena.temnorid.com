"""Build all non-character assets from models-to-do.md with Blender, no external services.
Blender coordinates: Z up, -Y front. glTF export: +Y up, +Z front.
"""
import bpy, numpy as np, math, os, json, random, sys
from mathutils import Vector
from math import sin, cos, pi
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT=os.path.join(ROOT,'Assets','Models','arena')
TEX=os.path.join(ROOT,'Assets','Models','materials')
PRE=os.path.join(ROOT,'output','arena')
for p in [OUT,TEX,PRE]:os.makedirs(p,exist_ok=True)
random.seed(48)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene;scene.unit_settings.system='METRIC'
assets=[];report=[];parts=[]

def image(name,pixels,folder=PRE,noncolor=False):
    h,w=pixels.shape[:2]
    if pixels.shape[2]==3:pixels=np.concatenate([pixels,np.ones((h,w,1),dtype=np.float32)],axis=2)
    img=bpy.data.images.new(name,width=w,height=h,alpha=True)
    if noncolor:img.colorspace_settings.name='Non-Color'
    img.pixels.foreach_set(pixels.astype(np.float32).ravel())
    img.filepath_raw=os.path.join(folder,name+'.png');img.file_format='PNG';img.save();img.pack()
    return img

def normal(height,strength=4):
    dx=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*strength
    dy=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))*strength
    v=np.stack([-dx,-dy,np.ones_like(dx)],axis=2)
    v/=np.linalg.norm(v,axis=2)[:,:,None]
    return .5+.5*v

def periodic_noise(n):
    y,x=np.mgrid[:n,:n]/n
    noise=np.zeros((n,n),dtype=np.float32)
    for a,b,amp in [(3,5,.20),(17,11,.11),(37,41,.045),(109,87,.02)]:
        noise+=amp*np.sin(2*pi*(x*a+y*b)+.8)*np.cos(2*pi*(y*a-x*b)+1.7)
    return noise

# Prop atlas: stone, wood, iron, cloth, and a small emissive rune strip.
def atlas(n):
    yy,xx=np.mgrid[:n,:n];u=xx/n;v=yy/n;noise=periodic_noise(n)
    base=np.ones((n,n,3),dtype=np.float32)
    rough=np.full((n,n),.86,dtype=np.float32);metal=np.zeros((n,n),dtype=np.float32)
    h=.5+noise*.17
    stone=(u<.5)&(v<.5);wood=(u>=.5)&(v<.5)
    iron=(u<.5)&(v>=.5)&(v<.90);cloth=(u>=.5)&(v>=.5)&(v<.90)
    base[stone]=np.stack([.47+noise*.17,.425+noise*.15,.35+noise*.13],axis=2)[stone]
    grain=np.sin(2*pi*(u*82+np.sin(v*2*pi*8)*.6))*.035+noise*.05
    base[wood]=np.stack([.19+grain,.102+grain*.6,.047+grain*.3],axis=2)[wood]
    h[wood]=.5+grain[wood]*1.4
    base[iron]=np.stack([.12+noise*.09,.115+noise*.08,.102+noise*.07],axis=2)[iron]
    metal[iron]=.86;rough[iron]=.55+noise[iron]*.2
    weave=.012*np.sin(u*2*pi*512)*np.sin(v*2*pi*512)
    base[cloth]=np.stack([.095+noise*.025+weave,.085+noise*.022+weave,.105+noise*.025+weave],axis=2)[cloth]
    h[cloth]=.5+weave[cloth]*2
    rune=v>=.90;base[rune]=(.17,.22,.65);rough[rune]=.6
    emit=np.zeros_like(base);emit[rune]=(.13,.18,.7)
    mr=np.stack([np.ones_like(rough),rough,metal],axis=2)
    return [image('arena-atlas-'+str(n)+'-basecolor',np.clip(base,0,1)),
            image('arena-atlas-'+str(n)+'-normal',normal(h,2.2),noncolor=True),
            image('arena-atlas-'+str(n)+'-metallic-roughness',mr,noncolor=True),
            image('arena-atlas-'+str(n)+'-emissive',emit)]

def pbr(name,textures,color=True):
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    nd,lk=mat.node_tree.nodes,mat.node_tree.links;p=nd.get('Principled BSDF')
    tx=[]
    for img in textures:
        node=nd.new('ShaderNodeTexImage');node.image=img;tx.append(node)
    if color:
        attr=nd.new('ShaderNodeVertexColor');attr.layer_name='Color'
        mult=nd.new('ShaderNodeMixRGB');mult.blend_type='MULTIPLY';mult.inputs[0].default_value=1
        lk.new(tx[0].outputs['Color'],mult.inputs[1]);lk.new(attr.outputs['Color'],mult.inputs[2])
        lk.new(mult.outputs[0],p.inputs['Base Color'])
    else:lk.new(tx[0].outputs['Color'],p.inputs['Base Color'])
    nm=nd.new('ShaderNodeNormalMap');lk.new(tx[1].outputs['Color'],nm.inputs['Color']);lk.new(nm.outputs[0],p.inputs['Normal'])
    split=nd.new('ShaderNodeSeparateColor');lk.new(tx[2].outputs['Color'],split.inputs[0])
    lk.new(split.outputs['Green'],p.inputs['Roughness']);lk.new(split.outputs['Blue'],p.inputs['Metallic'])
    if len(tx)>3:
        lk.new(tx[3].outputs['Color'],p.inputs['Emission Color']);p.inputs['Emission Strength'].default_value=.65
    return mat

MAT1024=pbr('arena-pbr',atlas(1024));MAT2048=pbr('arena-pbr-2k',atlas(2048))
material=MAT1024
zones={'stone':(.015,.015,.485,.485),'wood':(.515,.015,.985,.485),
       'iron':(.015,.515,.485,.88),'cloth':(.515,.515,.985,.88),'rune':(.1,.93,.9,.97)}

def finish(ob,zone='stone',tint=None,smooth=False):
    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    ob.data.materials.clear();ob.data.materials.append(material)
    attr=ob.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
    tint=tint or (random.uniform(.80,1.08),)*3
    for c in attr.data:c.color=(*tint,1)
    if not ob.data.uv_layers:
        uv=ob.data.uv_layers.new(name='UVMap')
        for poly in ob.data.polygons:
            axis=max(range(3),key=lambda i:abs(poly.normal[i]))
            axes=[i for i in range(3) if i!=axis]
            for li in poly.loop_indices:
                co=ob.data.vertices[ob.data.loops[li].vertex_index].co
                uv.data[li].uv=(co[axes[0]]*.6%1,co[axes[1]]*.6%1)
    u0,v0,u1,v1=zones[zone]
    for loop in ob.data.uv_layers.active.data:
        u,v=loop.uv;loop.uv=(u0+max(0,min(1,u))*(u1-u0),v0+max(0,min(1,v))*(v1-v0))
    for poly in ob.data.polygons:poly.use_smooth=smooth
    parts.append(ob);ob.select_set(False);return ob

def box(name,pos,size,zone='stone',bevel=.015,segments=1,tint=None):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos)
    ob=bpy.context.object;ob.name=name;ob.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=ob.modifiers.new('worn-edges','BEVEL');mod.width=bevel;mod.segments=segments
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(ob,zone,tint)

def mesh(name,verts,faces,zone='stone',tint=None):
    data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
    ob=bpy.data.objects.new(name,data);scene.collection.objects.link(ob)
    return finish(ob,zone,tint)

def tube(name,a,b,r,zone='iron',sides=12):
    mid=(Vector(a)+Vector(b))/2
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides,radius=r,depth=(Vector(b)-Vector(a)).length,location=mid)
    ob=bpy.context.object;ob.name=name;ob.rotation_euler=(Vector(b)-Vector(a)).to_track_quat('Z','Y').to_euler()
    return finish(ob,zone,smooth=True)

def line(name,pts,r=.008,zone='rune'):
    cv=bpy.data.curves.new(name,'CURVE');cv.dimensions='3D';cv.bevel_depth=r;cv.bevel_resolution=0
    sp=cv.splines.new('POLY');sp.points.add(len(pts)-1)
    for p,co in zip(sp.points,pts):p.co=(*co,1)
    ob=bpy.data.objects.new(name,cv);scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.convert(target='MESH')
    return finish(bpy.context.object,zone,smooth=True)

def ring(name,r,z,width=.012,segments=48):
    vs=[];fs=[]
    for rr in [r-width/2,r+width/2]:
        vs += [(rr*cos(2*pi*j/segments),rr*sin(2*pi*j/segments),z) for j in range(segments)]
    for j in range(segments):fs.append((j,(j+1)%segments,(j+1)%segments+segments,j+segments))
    return mesh(name,vs,fs,'rune',(1,1,1))

def begin():
    global parts
    parts=[]
    bpy.ops.object.select_all(action='DESELECT')

def end(name,size,budget,helpers=None,normalize=True):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in parts:ob.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();ob=bpy.context.object;ob.name=name
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    mod=ob.modifiers.new('triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
    count=len(ob.data.polygons)
    if count>budget[1]:
        raise RuntimeError(name+' exceeds triangle budget: '+str(count))
    if normalize:
        coords=[v.co for v in ob.data.vertices]
        lo=Vector([min(v[i] for v in coords) for i in range(3)])
        hi=Vector([max(v[i] for v in coords) for i in range(3)])
        for v in ob.data.vertices:
            v.co.x=(v.co.x-(hi.x+lo.x)/2)*size[0]/(hi.x-lo.x)
            v.co.y=(v.co.y-(hi.y+lo.y)/2)*size[2]/(hi.y-lo.y)
            v.co.z=(v.co.z-lo.z)*size[1]/(hi.z-lo.z)
    for helper in helpers or []:helper.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT,name+'.glb'),export_format='GLB',
        use_selection=True,export_animations=False,export_yup=True,export_materials='EXPORT',export_tangents=True)
    coords=[v.co for v in ob.data.vertices]
    bounds=[[min(v[i] for v in coords),max(v[i] for v in coords)] for i in range(3)]
    report.append({'name':name,'triangles':count,'budget':budget,'size_xyz_gltf':size,
        'bounds_blender':bounds,'materials':[m.name for m in ob.data.materials],
        'helpers':[h.name for h in helpers or []]})
    assets.append(ob)
    for helper in helpers or []:helper.hide_render=True
    return ob

def pillar(broken=False):
    begin()
    for z,w,h in [(.075,1.2,.15),(.205,1.08,.11),(.315,.94,.11)]:
        box('stepped-plinth',(0,0,z),(w,w,h),segments=2,bevel=.022)
    top=2.04 if broken else 3.65
    rows=5 if broken else 8
    height=(top-.37)/rows
    for i in range(rows):
        z=.37+(i+.5)*height
        box('chipped-shaft',(0,0,z),(.82,.82,height-.006),bevel=.025,segments=2)
    # Engraved-looking runic band at eye level, with aged darker stone backing.
    box('rune-band',(0,0,1.66),(.85,.85,.20),bevel=.01,tint=(.65,.65,.65))
    for face in range(4):
        for j in [-1,0,1]:
            pts=[(j*.20-.045,-.429,1.63),(j*.20,-.429,1.71),(j*.20+.045,-.429,1.63)]
            a=face*pi/2
            pts=[(x*cos(a)-y*sin(a),x*sin(a)+y*cos(a),z) for x,y,z in pts]
            line('pillar-rune',pts,.006,'rune')
    for face in range(2):
        y=-.414 if face==0 else .414
        line('dark-stone-fracture',[(-.22,y,.72),(-.13,y,.87),(-.17,y,1.02),(-.07,y,1.12)],.006,'iron')
    if broken:
        # Fractured cap has uneven individual top vertices but a closed top.
        vs=[(-.41,-.41,2.04),(.41,-.41,2.04),(.41,.41,2.04),(-.41,.41,2.04),
            (-.41,-.41,2.10),(.41,-.41,2.20),(.41,.41,2.14),(-.41,.41,2.07),(0,0,2.13)]
        mesh('fractured-column-top',vs,[(0,3,2,1),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),
            (4,5,8),(5,6,8),(6,7,8),(7,4,8)])
    else:
        for z,w,h in [(3.70,.96,.10),(3.84,1.08,.18),(3.965,1.2,.07)]:
            box('carved-capital',(0,0,z),(w,w,h),bevel=.02,segments=2)
    return end('arena-broken-pillar' if broken else 'arena-pillar',(1.2,2.2 if broken else 4,1.2),(1000,3000))

pillar();pillar(True)
begin()
# An unbroken core seals plank gaps and keeps the crate a full collision silhouette.
box('crate-core',(0,0,.60),(1.16,1.16,1.16),'wood',.005)
for side in range(4):
    for j in range(4):
        x=-.435+j*.29
        pos=(x,-.583,.60);size=(.282,.026,1.15)
        if side==1:pos=(x,.583,.60)
        elif side==2:pos=(-.583,x,.60);size=(.026,.282,1.15)
        elif side==3:pos=(.583,x,.60);size=(.026,.282,1.15)
        box('weathered-plank',pos,size,'wood',0)
box('continuous-crate-lid',(0,0,1.189),(1.2,1.2,.020),'wood',0)
cuts=[-.6,-.304,-.296,-.004,.004,.296,.304,.6]
lid=mesh('flat-planked-lid',[(x,y,1.2) for y in [-.6,.6] for x in cuts],
    [(j,j+1,j+9,j+8) for j in range(7)],'wood',(1,1,1))
for polygon in lid.data.polygons:
    if polygon.index%2:
        for li,uv in zip(polygon.loop_indices,[(.1,.6),(.2,.6),(.2,.7),(.1,.7)]):
            lid.data.uv_layers.active.data[li].uv=uv
            lid.data.color_attributes['Color'].data[li].color=(.28,.28,.28,1)
for z in [.04,1.16]:
    for x in [-.59,.59]:box('iron-edge',(x,0,z),(.02,1.2,.075),'iron',0)
    for y in [-.59,.59]:box('iron-edge',(0,y,z),(1.2,.02,.075),'iron',0)
for x in [-.58,.58]:
    for y in [-.58,.58]:box('iron-corner',(x,y,.60),(.04,.04,1.2),'iron',0)
for x in [-.48,.48]:
    for z in [.05,1.15]:tube('rivet',(x,-.602,z),(x,-.611,z),.011,'iron',8)
line('burnt-stamp',[(-.12,-.605,.50),(0,-.605,.75),(.12,-.605,.50),(0,-.605,.58),(-.12,-.605,.50)],.012,'iron')
end('arena-crate',(1.2,1.2,1.2),(300,1000))

begin();material=MAT2048
box('solid-dais-core',(0,0,.58),(5.96,5.96,1.16),bevel=0)
for side in range(4):
    for row in range(2):
        for col in range(6):
            x=-2.5+col;z=.29+row*.58
            pos=(x,-2.94,z);size=(.99,.12,.57)
            if side==1:pos=(x,2.94,z)
            elif side==2:pos=(-2.94,x,z);size=(.12,.99,.57)
            elif side==3:pos=(2.94,x,z);size=(.12,.99,.57)
            box('dais-ashlar',pos,size,bevel=.012)
# One flat slab guarantees a continuous, full-size walkable top; seams are inset-looking lines.
box('flat-dais-top',(0,0,1.168),(6,6,.06),bevel=0)
# Coplanar stone tiles and dark grout faces: continuous top, no decal z-fighting.
cuts=[-3,-1.506,-1.494,-.006,.006,1.494,1.506,3]
vs=[(x,y,1.199) for y in cuts for x in cuts];fs=[]
for row in range(7):
    for col in range(7):
        k=row*8+col;fs.append((k,k+1,k+9,k+8))
topmesh=mesh('flat-tiled-dais-surface',vs,fs,'stone',(1,1,1))
for polygon in topmesh.data.polygons:
    row,col=divmod(polygon.index,7)
    if row%2 or col%2:
        for li,uv in zip(polygon.loop_indices,[(.1,.6),(.2,.6),(.2,.7),(.1,.7)]):
            topmesh.data.uv_layers.active.data[li].uv=uv
            topmesh.data.color_attributes['Color'].data[li].color=(.35,.35,.35,1)
ring('dais-outer-sigil',2.0,1.20,.025,64);ring('dais-inner-sigil',1.72,1.20,.015,64)
for j in range(12):
    a=j*pi/6
    pts=[(r*cos(a+da),r*sin(a+da),1.2) for r,da in [(1.80,-.023),(1.91,0),(1.80,.023)]]
    mesh('dais-inlaid-rune',pts,[(0,1,2)],'rune',(1,1,1))
end('arena-platform',(6,1.2,6),(1500,4000),normalize=False)

def wall(name,width,height,rows,cols,modular=False):
    global material
    begin();material=MAT2048 if modular else MAT1024
    core_height=height if modular else height-.12
    box('solid-wall-core',(0,0,core_height/2),(width,.57,core_height),bevel=0)
    for row in range(rows):
        for col in range(cols):
            # Full uninterrupted end planes; blocks meet repeated segment copies.
            w=width/cols;h=height/rows
            block_height=h-.008
            if not modular and row==rows-1 and col>0:block_height-=random.uniform(.03,.10)
            z=row*h+.004+block_height/2
            box('wall-ashlar',(-width/2+(col+.5)*w,0,z),(w-.008,.6,block_height),bevel=.015)
    if modular:
        box('frieze',(0,-.302,3.02),(width,.015,.18),bevel=.003,tint=(.67,.67,.67))
        for j in range(10):
            x=-1.8+j*.4
            line('wall-rune',[(x-.045,-.313,2.97),(x,-.313,3.08),(x+.045,-.313,2.97)],.005,'rune')
    else:
        for j in range(4):
            x=-1.2+j*.7
            line('wall-crack',[(x,-.303,.35),(x+.06,-.303,.45),(x+.02,-.303,.64)],.004,'iron')
    return end(name,(width,height,.6),(800,2000) if modular else (500,1500))
wall('arena-wall-segment',4,4,6,5,True)
wall('arena-low-wall',3,1,3,4)

begin();material=MAT1024
for i in range(19):
    a=random.uniform(0,2*pi);r=random.uniform(.05,.72)
    x=r*cos(a);y=r*sin(a)*.7
    h=random.uniform(.12,.32)
    z=max(h/2,.30*(1-r/.9))
    ob=box('broken-rubble-block',(x,y,z),(random.uniform(.22,.48),random.uniform(.18,.40),h),bevel=.035)
    # Rotate geometry about its centre without leaving unapplied transforms.
    angle=random.uniform(-pi,pi);center=Vector((x,y,z))
    for v in ob.data.vertices:
        q=v.co-center;v.co=center+Vector((q.x*cos(angle)-q.y*sin(angle),q.x*sin(angle)+q.y*cos(angle),q.z))
end('arena-rubble',(2,.5,1.5),(500,1500))

begin()
for step in range(4):
    # Front / lowest step = Blender -Y = glTF +Z.
    y=-.9+step*.6;h=(step+1)*.3
    box('solid-step-core',(0,y+.001,h/2),(3,.598,h),bevel=0)
    for col in range(5):
        box('chipped-step-facing',(-1.2+col*.6,y-.286,h-.15),(.592,.028,.292),bevel=.006)
        if col<4:box('step-top-seam',(-1.5+(col+1)*.6,y,h-.001),(.008,.594,.002),'iron',0)
end('arena-stairs',(3,1.2,2.4),(500,1500),normalize=False)

begin()
# Lathed hollow iron bowl: profile goes around the lip then back down inside.
profile=[(.08,1.01),(.21,1.07),(.33,1.20),(.38,1.32),(.40,1.37),(.40,1.40),
         (.37,1.40),(.35,1.32),(.30,1.21),(.18,1.12),(.04,1.11)]
vs=[];fs=[];segments=24
for r,z in profile:
    vs.extend([(r*cos(2*pi*j/segments),r*sin(2*pi*j/segments),z) for j in range(segments)])
for row in range(len(profile)-1):
    for j in range(segments):
        v=row*segments+j;fs.append((v,row*segments+(j+1)%segments,(row+1)*segments+(j+1)%segments,v+segments))
mesh('hollow-fire-bowl',vs,fs,'iron',(1,1,1))
for j in range(3):
    a=2*pi*j/3
    foot=(.31*cos(a),.31*sin(a),.03);top=(.16*cos(a),.16*sin(a),1.10)
    tube('iron-tripod-leg',foot,top,.032,'iron',12)
    box('brazier-foot',foot,(.10,.10,.06),'iron',.008)
    line('bowl-rune',[(.345*cos(a-.08),.345*sin(a-.08),1.26),(.359*cos(a),.359*sin(a),1.30),
        (.345*cos(a+.08),.345*sin(a+.08),1.26)],.006,'rune')
for z,r in [(1.34,.387),(1.39,.401)]:
    line('bronze-bowl-rim',[(r*cos(a),r*sin(a),z) for a in np.linspace(0,2*pi,25)],.008,'iron')
for j in range(14):
    a=random.uniform(0,2*pi);r=random.uniform(0,.19)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.04,location=(r*cos(a),r*sin(a),1.17))
    finish(bpy.context.object,'iron',(.35,.28,.23))
flame=bpy.data.objects.new('flame',None);scene.collection.objects.link(flame);flame.location=(0,0,1.25)
# A mesh parent keeps the attachment node in the GLB as a named child.
brazier=end('arena-brazier',(.8,1.4,.8),(800,2000),helpers=[flame])
flame.parent=brazier
# Re-export now the named flame helper is a child of the model (identity mesh root).
bpy.ops.object.select_all(action='DESELECT');brazier.select_set(True);flame.select_set(True)
bpy.ops.export_scene.gltf(filepath=os.path.join(OUT,'arena-brazier.glb'),export_format='GLB',use_selection=True,export_animations=False,export_tangents=True)

# Transparent rune texture with a forward arrow, generated analytically at 1024.
n=1024;y,x=np.mgrid[:n,:n];u=(x/(n-1)-.5)*2;v=(y/(n-1)-.5)*2
r=np.sqrt(u*u+v*v);ang=np.arctan2(v,u)
mask=(abs(r-.84)<.012)|(abs(r-.69)<.007)
for j in range(12):
    a=j*pi/6
    da=np.arctan2(np.sin(ang-a),np.cos(ang-a))
    mask |= (r>.72)&(r<.80)&(abs(da)<.018)
mask |= (abs(u)<.014)&(v>.10)&(v<.58)
mask |= (v>.38)&(v<.62)&(abs(abs(u)-( .62-v)*.55)<.014)
mask |= (abs(r-.21)<.010)
rgba=np.zeros((n,n,4),dtype=np.float32);rgba[:,:,:3]=(.23,.35,.9);rgba[:,:,3]=mask.astype(float)
sigil=image('spawn-circle_basecolor',rgba,TEX)
spawnmat=bpy.data.materials.new('spawn-circle');spawnmat.use_nodes=True
nd,lk=spawnmat.node_tree.nodes,spawnmat.node_tree.links;p=nd.get('Principled BSDF')
tx=nd.new('ShaderNodeTexImage');tx.image=sigil
lk.new(tx.outputs['Color'],p.inputs['Base Color']);lk.new(tx.outputs['Alpha'],p.inputs['Alpha'])
lk.new(tx.outputs['Color'],p.inputs['Emission Color']);p.inputs['Emission Strength'].default_value=1.5
spawnmat.surface_render_method='DITHERED';p.inputs['Roughness'].default_value=.8
begin()
ob=box('spawn-decal',(0,0,.01),(2,2,.02),bevel=0)
ob.data.materials.clear();ob.data.materials.append(spawnmat)
for poly in ob.data.polygons:
    for li in poly.loop_indices:
        co=ob.data.vertices[ob.data.loops[li].vertex_index].co
        ob.data.uv_layers.active.data[li].uv=((co.x+1)/2,(1-co.y)/2)
end('arena-spawn-circle',(2,.02,2),(0,499),normalize=False)

begin();material=MAT1024
cols=13;rows=15;vs=[];fs=[]
for row in range(rows):
    t=row/(rows-1)
    for col in range(cols):
        x=-.50+col/(cols-1)
        z=2.88-2.85*t
        if row==rows-1:z+=.04*(1+sin(col*2.3))
        y=-.035-.032*sin(x*15+t*2)*sin(pi*t)-.015*sin(t*pi)
        vs.append((x,y,z))
for row in range(rows-1):
    for col in range(cols-1):
        k=row*cols+col;fs.append((k,k+1,k+1+cols,k+cols))
mesh('frayed-banner-cloth',vs,fs,'cloth',(1,1,1))
tube('wooden-hanging-pole',(-.60,0,2.96),(.60,0,2.96),.04,'wood',16)
for x in [-.44,.44]:tube('cloth-hanging-loop',(x,-.015,2.80),(x,-.015,2.97),.018,'cloth',8)
accent=bpy.data.materials.new('accent');accent.use_nodes=True
accent.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.12,.075,.25,1)
accent.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.80
crest=mesh('recolorable-emblem',[(-.24,-.080,1.65),(0,-.09,2.2),(.24,-.080,1.65),
    (0,-.095,1.32),(-.1,-.10,1.65),(0,-.10,1.93),(.10,-.10,1.65),(0,-.10,1.49)],
    [(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],'cloth',(1,1,1))
crest.data.materials.clear();crest.data.materials.append(accent)
# Visible curled/frayed hem; overall prescribed depth includes the wooden pole brackets.
for x in [-.48,.48]:box('wall-bracket',(x,.055,2.91),(.055,.19,.09),'iron',.008)
end('arena-banner',(1.2,3,.3),(300,1000))

# Seamless materials: physically consistent base / normal / roughness from one height field.
def stone_material(name,wall=False):
    n=2048;yy,xx=np.mgrid[:n,:n];u=xx/n;v=yy/n
    noise=periodic_noise(n)
    row_count=8 if wall else 4
    row=np.floor(v*row_count).astype(int)
    col_count=5 if wall else 4
    phase=(u*col_count+(row%2)*.5)%1
    vertical=np.minimum(phase,1-phase)
    if not wall:
        # 40, 60, 50, 50 cm stones across each 2 m tile, in offset rows.
        shifted=(u+(row%2)*.125)%1
        vertical=np.ones_like(u)
        for seam in [0,.2,.5,.75]:
            delta=np.abs(shifted-seam)
            vertical=np.minimum(vertical,np.minimum(delta,1-delta)*4)
    horizontal=np.minimum((v*row_count)%1,1-(v*row_count)%1)
    mortar=np.minimum(vertical,horizontal)
    stone=np.clip(mortar/.027,0,1)
    block_id=np.floor(u*col_count+(row%2)*.5).astype(int)%col_count
    variation=np.sin(block_id*7.3+row*4.1)*.055
    height=.20+stone*.35+noise*.025
    # Fine periodic cracks within occasional stones; no baked light or shadow.
    crack=(abs(np.sin(2*pi*(u*3+v*2+.015*np.sin(v*2*pi*9))))<.007)&(stone>.8)
    height[crack]-=.10
    color=np.stack([.43+variation+noise*.08,.385+variation+noise*.07,.315+variation+noise*.065],axis=2)
    color*= (.35+.65*stone)[:,:,None];color[crack]*=.45
    if wall:
        color*=(.72+.28*np.clip(v/.23,0,1))[:,:,None]
        frieze=(v>.72)&(v<.78)
        rune=(abs(np.sin(u*2*pi*10))<.11)&frieze
        color[rune]*=.40;height[rune]-=.06
    rough=.76+.13*(1-stone)+noise*.08
    # Copy border pixels explicitly, so the delivered images join without an edge discontinuity.
    for arr in [color,height,rough]:
        arr[:,-1]=arr[:,0]
        if not wall:arr[-1]=arr[0]
    norm=normal(height,3)
    norm[:,-1]=norm[:,0]
    if not wall:norm[-1]=norm[0]
    imgs=[image(name+'_basecolor',np.clip(color,0,1),TEX),image(name+'_normal',norm,TEX,True),
          image(name+'_roughness',np.repeat(rough[:,:,None],3,axis=2),TEX,True)]
    # Preview shader references the standalone roughness map, not an undocumented packed map.
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    nd,lk=mat.node_tree.nodes,mat.node_tree.links;p=nd.get('Principled BSDF')
    tex=[]
    for img in imgs:
        t=nd.new('ShaderNodeTexImage');t.image=img;tex.append(t)
    lk.new(tex[0].outputs['Color'],p.inputs['Base Color'])
    nm=nd.new('ShaderNodeNormalMap');lk.new(tex[1].outputs['Color'],nm.inputs['Color']);lk.new(nm.outputs[0],p.inputs['Normal'])
    lk.new(tex[2].outputs['Color'],p.inputs['Roughness'])
    return mat
floor_mat=stone_material('stone-floor');wall_mat=stone_material('stone-wall',True)
with open(os.path.join(PRE,'build-report.json'),'w') as f:json.dump(report,f,indent=2)
# Preserve an editable source with the exported objects separated by filename, before studio setup.
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PRE,'arena-assets.blend'))
if '--no-render' in sys.argv:sys.exit(0)

# Each asset is rendered independently, with its file's original dimensions and materials.
scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=True
scene.render.resolution_x=700;scene.render.resolution_y=700;scene.render.resolution_percentage=100
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.17,.16,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
def aim(ob,p):ob.rotation_euler=(Vector(p)-ob.location).to_track_quat('-Z','Y').to_euler()
lights=[]
for name,energy in [('key',650),('fill',300),('rim',600)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=energy;data.size=4
    ob=bpy.data.objects.new(name,data);scene.collection.objects.link(ob);lights.append(ob)
camdata=bpy.data.cameras.new('preview');cam=bpy.data.objects.new('preview',camdata)
scene.collection.objects.link(cam);camdata.type='ORTHO';scene.camera=cam
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.005));ground=bpy.context.object
gm=bpy.data.materials.new('studio');gm.diffuse_color=(.12,.11,.10,1);ground.data.materials.append(gm)
for ob in assets:ob.hide_render=True
for ob,item in zip(assets,report):
    if '--render-only' in sys.argv:
        names=sys.argv[sys.argv.index('--render-only')+1:]
        if item['name'] not in names:continue
    ob.hide_render=False
    w,h,d=item['size_xyz_gltf'];span=max(w,h,d);target=Vector((0,0,h*.48))
    camdata.ortho_scale=max(h*1.18,(w+d*.6)*1.13)
    cam.location=(span*1.6,-span*2.5,h*.65+span*1.3);aim(cam,target)
    if item['name']=='arena-spawn-circle':cam.location=(0,-.2,3);aim(cam,(0,0,0));camdata.ortho_scale=2.4
    for light,pos in zip(lights,[(-span*.8,-span,span*1.8),(span,-span*.5,span),(0,span,span*1.7)]):
        light.location=pos;light.data.size=max(span,1);light.data.energy=100*max(span,1)**2;aim(light,target)
    scene.render.filepath=os.path.join(PRE,item['name']+'.png');bpy.ops.render.render(write_still=True)
    ob.hide_render=True

# Flat texture swatches are rendered as actual PBR surfaces, not illustrated replacements.
for name,mat,wall in [('stone-floor',floor_mat,False),('stone-wall',wall_mat,True)]:
    if '--render-only' in sys.argv:continue
    bpy.ops.mesh.primitive_plane_add(size=2)
    ob=bpy.context.object;ob.data.materials.append(mat)
    cam.location=(0,-.01,3);aim(cam,(0,0,0));camdata.ortho_scale=2.3
    for light,pos in zip(lights,[(-1,-1,3),(2,1,3),(0,2,2)]):light.location=pos;light.data.energy=150;light.data.size=2;aim(light,(0,0,0))
    scene.render.filepath=os.path.join(PRE,name+'.png');bpy.ops.render.render(write_still=True)
    ob.hide_render=True
print('ARENA_BUILD_COMPLETE '+json.dumps(report))
