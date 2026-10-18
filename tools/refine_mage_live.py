"""Run in the live Blender MCP scene; preserves the baseline and exports a review asset."""
import bpy, bmesh, math, os
from math import sin, cos, pi
from mathutils import Vector

ROOT = 'C:/Users/Unity/work/temnorid-arena'
OUT = ROOT + '/output/mage/review-v2'
os.makedirs(OUT, exist_ok=True)
# Load output/mage/mage.blend in a separate MCP call before running this script.
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/mage-before-live.blend', copy=True)
scene=bpy.context.scene
model=bpy.data.objects['mage-mesh']; rig=bpy.data.objects['mage']
rig.animation_data.action=None
for track in rig.animation_data.nla_tracks: track.mute=True
for pb in rig.pose.bones: pb.rotation_euler=(0,0,0);pb.location=(0,0,0)

# Remove complete garment islands, keeping the original rig and weighted anatomy.
bm=bmesh.new();bm.from_mesh(model.data)
remaining=set(bm.verts);removed=[]
while remaining:
    seed=remaining.pop();group=[seed];stack=[seed]
    while stack:
        v=stack.pop()
        for e in v.link_edges:
            n=e.other_vert(v)
            if n in remaining:remaining.remove(n);group.append(n);stack.append(n)
    lo=[min(v.co[i] for v in group) for i in range(3)]
    hi=[max(v.co[i] for v in group) for i in range(3)]
    mats={model.data.materials[f.material_index].name for v in group for f in v.link_faces}
    hood=lo[2]>1.48 and hi[2]>1.77
    mantle=lo[2]>1.30 and hi[2]<1.49 and hi[0]-lo[0]>.38
    skirt=lo[2]>.69 and lo[2]<.72 and hi[2]<1.045
    tunic=lo[2]>.99 and lo[2]<1.01 and hi[2]>1.44 and 'woven-wool' in mats
    sleeve='woven-wool' in mats and (lo[0]>.09 or hi[0]<-.09) and lo[2]>.95 and hi[2]>1.17
    collar='woven-wool' in mats and lo[2]>1.43 and hi[2]<1.52
    loose_hair='hair' in mats and lo[2]>1.66
    if hood or mantle or skirt or tunic or sleeve or collar or loose_hair:removed.extend(group)
    # Eyelids inherited the eye material in the original generator.
    if 'eyes' in mats and 110<len(group)<150:
        for v in group:
            for f in v.link_faces:f.material_index=list(model.data.materials).index(bpy.data.materials['skin'])
bmesh.ops.delete(bm,geom=removed,context='VERTS')
bm.to_mesh(model.data);bm.free()

def pbr(name, color, roughness, source='woven-wool', metallic=0):
    mat=bpy.data.materials[source].copy();mat.name=name
    p=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    for link in list(p.inputs['Base Color'].links):mat.node_tree.links.remove(link)
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Roughness'].default_value=roughness
    p.inputs['Metallic'].default_value=metallic
    return mat
wool=pbr('v2-charcoal-wool',(.022,.028,.034),.88)
mantle_mat=pbr('v2-weathered-indigo',(.038,.043,.061),.9)
lining=pbr('v2-hood-lining',(.012,.014,.018),.95)
trim=pbr('v2-muted-violet',(.07,.035,.095),.82)
stitch=pbr('v2-flax-thread',(.16,.115,.071),.85)
leather=pbr('v2-oiled-leather',(.032,.017,.009),.66,'worn-leather')

parts=[]
def garment(name, verts, faces, material, joint, thickness=.003, subdiv=1):
    data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
    ob=bpy.data.objects.new(name,data);scene.collection.objects.link(ob)
    data.materials.append(material)
    for p in data.polygons:p.use_smooth=True
    uv=data.uv_layers.new(name='UVMap')
    for p in data.polygons:
        for li in p.loop_indices:
            co=data.vertices[data.loops[li].vertex_index].co
            uv.data[li].uv=(co.x*2+.5,co.z)
    for v in data.vertices:
        if joint=='TORSO':
            z=v.co.z
            weights={'Hips':max(0,1-abs(z-1.03)/.18),'Spine':max(0,1-abs(z-1.19)/.18),'Spine1':max(0,1-abs(z-1.32)/.18),'Spine2':max(0,1-abs(z-1.46)/.18)}
        elif joint=='COAT':
            w=max(0,min(.55,(1.02-v.co.z)*1.2))
            weights={'Hips':1-w,('Left' if v.co.x<0 else 'Right')+'UpLeg':w}
        elif joint.endswith('SLEEVE'):
            side=joint.replace('SLEEVE','');w=max(0,min(1,(v.co.z-1.14)/.10))
            weights={side+'Arm':w,side+'ForeArm':1-w}
        else:weights={joint:1}
        total=sum(weights.values())
        for key,val in weights.items():
            if val:
                vg=ob.vertex_groups.get(key) or ob.vertex_groups.new(name=key)
                vg.add([v.index],val/total,'REPLACE')
    bpy.context.view_layer.objects.active=ob
    if subdiv:
        mod=ob.modifiers.new('soft tailored surface','SUBSURF');mod.levels=subdiv
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if thickness:
        mod=ob.modifiers.new('turned fabric edge','SOLIDIFY');mod.thickness=thickness
        bpy.ops.object.modifier_apply(modifier=mod.name)
    parts.append(ob)
    return ob

def grid_faces(rows,cols,wrap=False):
    return [(r*cols+c,r*cols+(c+1)%cols,(r+1)*cols+(c+1)%cols,(r+1)*cols+c)
            for r in range(rows-1) for c in range(cols if wrap else cols-1)]

def seam(name,points,radius,material,joint):
    cv=bpy.data.curves.new(name,'CURVE');cv.dimensions='3D';cv.bevel_depth=radius;cv.bevel_resolution=2
    sp=cv.splines.new('POLY');sp.points.add(len(points)-1)
    for p,co in zip(sp.points,points):p.co=(*co,1)
    ob=bpy.data.objects.new(name,cv);scene.collection.objects.link(ob)
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
    bpy.ops.object.convert(target='MESH');ob=bpy.context.object
    ob.data.materials.append(material)
    if joint=='COAT':
        for v in ob.data.vertices:
            w=max(0,min(.55,(1.02-v.co.z)*1.2))
            for key,val in {'Hips':1-w,('Left' if v.co.x<0 else 'Right')+'UpLeg':w}.items():
                vg=ob.vertex_groups.get(key) or ob.vertex_groups.new(name=key)
                vg.add([v.index],val,'REPLACE')
    else:
        vg=ob.vertex_groups.new(name=joint);vg.add(list(range(len(ob.data.vertices))),1,'REPLACE')
    parts.append(ob)

# Hood: rounded crown, broad face opening, inward-turned dark lining.
profiles=[(1.445,.12,.105),(1.49,.116,.108),(1.55,.106,.106),(1.63,.103,.10),
          (1.70,.101,.094),(1.75,.086,.084),(1.782,.060,.063),(1.80,.025,.038),(1.801,.003,.012)]
vs=[];cols=49
for r,(z,rx,ry) in enumerate(profiles):
    t=r/(len(profiles)-1)
    opening=.76*(1-t**6)
    for j in range(cols):
        a=-pi/2+opening+(2*pi-2*opening)*j/(cols-1)
        ripple=.0025*sin(a*7+z*12)*(1-t)
        vs.append(((rx+ripple)*cos(a),.022+(ry+ripple)*sin(a),z+.005*sin(a*3)*(1-t)))
hood=garment('rounded-lined-hood',vs,grid_faces(len(profiles),cols),mantle_mat,'Head',.005,2)
for col in [0,cols-1]:
    edge=[vs[r*cols+col] for r in range(len(profiles))]
    seam('hood-bound-edge',edge,.003,trim,'Head')
    inner=[(x*.985,y+.005,z) for x,y,z in edge]
    seam('hood-shadow-lip',inner,.006,lining,'Head')

# Shoulder cape shaped as fabric settling over the shoulders, with an asymmetric hem.
vs=[];rows=20;cols=80
for r in range(rows):
    t=r/(rows-1)
    rx=.063+.20*sin(t*pi/2);ry=.055+.083*sin(t*pi/2)
    for j in range(cols):
        a=j*2*pi/cols
        fold=(.003+.007*t)*sin(a*8+1.5*t)+.002*sin(a*13-t)
        z=1.495-.145*t-.050*t*t*(.5+.5*cos(a))+.014*sin(a*3+1)*t*t
        vs.append(((rx+fold)*cos(a),.008+(ry+fold)*sin(a),z))
garment('draped-shoulder-mantle',vs,grid_faces(rows,cols,True),mantle_mat,'Spine2',.004,1)
seam('mantle-hem',vs[-cols:]+[vs[-cols]],.002,trim,'Spine2')

# Fitted torso with tension folds radiating out from the belt.
vs=[];rows=28;cols=64
for r in range(rows):
    t=r/(rows-1);z=1.025+.412*t
    rx=.139+.051*t**.8;ry=.082+.023*sin(t*pi/2)
    neck=max(0,min(1,(z-1.35)/.10))
    rx*=1-.55*neck;ry*=1-.30*neck
    for j in range(cols):
        a=2*pi*j/cols
        fold=.0045*sin(a*9+t*3)*(1-t)+.003*sin(t*18+a*3)*sin(t*pi)
        vs.append(((rx+fold)*cos(a),(ry+fold)*sin(a),z))
garment('belt-gathered-tunic',vs,grid_faces(rows,cols,True),wool,'TORSO',.003,1)

# Continuous sleeves with elbow compression, rather than stacked rigid cylinders.
for sign,side in [(-1,'Left'),(1,'Right')]:
    vs=[];rows=38;cols=40
    for r in range(rows):
        t=r/(rows-1);z=1.455-.385*t;cx=sign*(.18+.113*t)
        radius=(.067-.022*t+.008*sin(t*pi))*sin(min(1,t/.14)*pi/2)
        for j in range(cols):
            a=2*pi*j/cols
            folds=.004*sin(a*5+t*5)*sin(t*pi)+.0045*sin(t*43+a*2)*math.exp(-((t-.71)/.21)**2)
            vs.append((cx+(radius+folds)*cos(a),-.008*t+(radius+folds)*sin(a),z+.008*cos(a)*sin(t*pi)))
    garment(side+'-tailored-sleeve',vs,grid_faces(rows,cols,True),wool,side+'SLEEVE',.003,1)

# Two overlapping coat tails replace the ten rigid skirt strips.
for side in [-1,1]:
    vs=[];rows=30;cols=45
    for r in range(rows):
        t=r/(rows-1)
        for j in range(cols):
            u=j/(cols-1);a=-pi/2+.10+u*(pi-.18)
            if side==-1:a+=pi
            fold=(.003+.012*t)*sin(u*pi*8+.7*t)+.003*t*sin(u*pi*15)
            rx=.157+.066*t;ry=.112+.053*t
            hem=.058*cos(u*pi*2)+.025*sin(u*pi)
            vs.append(((rx+fold)*cos(a),(ry+fold)*sin(a),1.04-.49*t+hem*t**4))
    garment('split-coat-tail',vs,grid_faces(rows,cols),wool,'COAT',.004,1)
    seam('coat-bound-hem',vs[-cols:],.002,trim,'COAT')
    for c in [0,cols-1]:seam('coat-front-binding',[vs[r*cols+c] for r in range(rows)],.002,trim,'COAT')

# Unify the old material palette with the new garments.
for i,mat in enumerate(model.data.materials):
    if mat.name=='woven-wool':model.data.materials[i]=wool
    elif mat.name=='worn-leather':model.data.materials[i]=leather
    elif mat.name=='accent':model.data.materials[i]=trim

# Bring face and exposed skin to a less chalky, more subdued complexion.
skinmat=bpy.data.materials['skin']
p=next(n for n in skinmat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
p.inputs['Roughness'].default_value=.64
ca=model.data.color_attributes.get('Color')
if ca:
    skinidx=list(model.data.materials).index(skinmat)
    for poly in model.data.polygons:
        if poly.material_index==skinidx:
            for li in poly.loop_indices:
                co=model.data.vertices[model.data.loops[li].vertex_index].co
                # Short stubble follows the actual facial surface, not separate blobs.
                beard=co.z>1.53 and co.z<1.614 and co.y<-.035
                if beard:
                    fade=min(1,max(0,(1.615-co.z)/.026))*.65
                    ca.data[li].color=(.32*(1-fade)+.045*fade,.177*(1-fade)+.034*fade,.108*(1-fade)+.027*fade,1)
                else:ca.data[li].color=(.32,.177,.108,1)

# Join added pieces to the original skinned mesh without renaming bones or clips.
bpy.ops.object.select_all(action='DESELECT');model.select_set(True)
for ob in parts:ob.select_set(True)
bpy.context.view_layer.objects.active=model;bpy.ops.object.join()
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.view_layer.update()
triangles=sum(len(p.vertices)-2 for p in model.data.polygons)
dec=model.modifiers.new('review-game-budget','DECIMATE');dec.ratio=min(1,80000/triangles)
bpy.ops.object.modifier_apply(modifier=dec.name)
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/mage-final-review.blend')
print('REVIEW BUILT',len(model.data.vertices),'vertices;',len(rig.data.bones),'bones;',len(bpy.data.actions),'actions')
