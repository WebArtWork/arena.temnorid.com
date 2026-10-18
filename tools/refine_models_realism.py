"""Shared-platform realism pass. Preserve bounds/rigs, bake PBR into GLB, retain original sources.
Run with Blender --background --python this.py [-- --only arena-crate] [--no-render].
"""
import bpy, bmesh, numpy as np, math, json, random, sys, shutil
from pathlib import Path
from mathutils import Vector, kdtree

ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'output'/'realism'
OUT.mkdir(parents=True,exist_ok=True)
random.seed(125)
ONLY=sys.argv[sys.argv.index('--only')+1] if '--only' in sys.argv else None
REPORT=[]
if ONLY and (OUT/'build-report.json').exists():
    REPORT=[r for r in json.loads((OUT/'build-report.json').read_text()) if r['name']!=ONLY]

def backup(path):
    path=Path(path)
    dest=OUT/'before'/path.relative_to(ROOT)
    if path.exists() and not dest.exists():
        dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,dest)

def bsdf(mat):return next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
def node(mat,kind):return mat.node_tree.nodes.new(kind)
def link(mat,a,b):mat.node_tree.links.new(a,b)
def mathnode(mat,op,a,b):
    n=node(mat,'ShaderNodeMath');n.operation=op
    for value,socket in [(a,n.inputs[0]),(b,n.inputs[1])]:
        if isinstance(value,(int,float)):socket.default_value=value
        else:link(mat,value,socket)
    return n.outputs[0]

def source_material(name,color,roughness,metallic=0,kind='stone',vertex_tint=False):
    mat=bpy.data.materials.new('source | '+name);mat.use_nodes=True
    p=bsdf(mat);p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metallic
    tex=node(mat,'ShaderNodeTexCoord')
    noise=node(mat,'ShaderNodeTexNoise');noise.inputs['Scale'].default_value=18 if kind=='stone' else 95
    noise.inputs['Detail'].default_value=4;noise.inputs['Roughness'].default_value=.72
    link(mat,tex.outputs['Object'],noise.inputs['Vector'])
    fine=node(mat,'ShaderNodeTexNoise');fine.inputs['Scale'].default_value=280 if kind!='skin' else 450
    fine.inputs['Detail'].default_value=2
    link(mat,tex.outputs['Object'],fine.inputs['Vector'])
    if kind in ['wood','cloth']:
        mapping=node(mat,'ShaderNodeVectorMath');mapping.operation='MULTIPLY'
        mapping.inputs[1].default_value=(1,1,.035) if kind=='wood' else (1,1,1)
        link(mat,tex.outputs['Object'],mapping.inputs[0]);link(mat,mapping.outputs[0],noise.inputs['Vector'])
        if kind=='wood':noise.inputs['Scale'].default_value=130
        if kind=='wood':
            top=node(mat,'ShaderNodeVectorMath');top.operation='MULTIPLY';top.inputs[1].default_value=(1,.035,1)
            link(mat,tex.outputs['Object'],top.inputs[0])
            geometry=node(mat,'ShaderNodeNewGeometry');separate=node(mat,'ShaderNodeSeparateXYZ')
            link(mat,geometry.outputs['Normal'],separate.inputs[0])
            topmask=mathnode(mat,'GREATER_THAN',mathnode(mat,'ABSOLUTE',separate.outputs['Z'],0),.7)
            choose=node(mat,'ShaderNodeMixRGB')
            link(mat,topmask,choose.inputs[0]);link(mat,mapping.outputs[0],choose.inputs[1]);link(mat,top.outputs[0],choose.inputs[2])
            link(mat,choose.outputs[0],noise.inputs['Vector'])
    ramp=node(mat,'ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position=.22
    ramp.color_ramp.elements[1].position=.8
    ramp.color_ramp.elements[0].color=tuple(c*.55 for c in color)+(1,)
    ramp.color_ramp.elements[1].color=tuple(min(1,c*1.35) for c in color)+(1,)
    link(mat,noise.outputs['Fac'],ramp.inputs[0]);base=ramp.outputs[0]
    if kind=='iron':
        rust=mathnode(mat,'MAXIMUM',0,mathnode(mat,'MULTIPLY',mathnode(mat,'SUBTRACT',noise.outputs['Fac'],.64),2.5))
        patina=node(mat,'ShaderNodeMixRGB');patina.inputs[2].default_value=(.11,.035,.01,1)
        link(mat,rust,patina.inputs[0]);link(mat,base,patina.inputs[1]);base=patina.outputs[0]
        link(mat,mathnode(mat,'MULTIPLY',metallic,mathnode(mat,'SUBTRACT',1,rust)),p.inputs['Metallic'])
    if vertex_tint:
        attr=node(mat,'ShaderNodeVertexColor');attr.layer_name='Color'
        mult=node(mat,'ShaderNodeMixRGB');mult.blend_type='MULTIPLY';mult.inputs[0].default_value=1
        link(mat,base,mult.inputs[1]);link(mat,attr.outputs['Color'],mult.inputs[2]);base=mult.outputs[0]
    # Bake local contact shading into the color, without lighting or cast shadows.
    ao=node(mat,'ShaderNodeAmbientOcclusion');ao.samples=8;ao.only_local=True
    ao.inputs['Distance'].default_value=.14 if kind=='stone' else .035
    mix=node(mat,'ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.28
    link(mat,base,mix.inputs[1]);link(mat,ao.outputs['Color'],mix.inputs[2])
    link(mat,mix.outputs[0],p.inputs['Base Color'])
    r=mathnode(mat,'ADD',roughness-.08,mathnode(mat,'MULTIPLY',noise.outputs['Fac'],.16))
    link(mat,r,p.inputs['Roughness'])
    bump=node(mat,'ShaderNodeBump');bump.inputs['Strength'].default_value=.3
    bump.inputs['Distance'].default_value={'stone':.009,'wood':.003,'iron':.0012,'cloth':.0006,'skin':.0002,'hair':.0008,'rune':.0002}.get(kind,.001)
    link(mat,fine.outputs['Fac'],bump.inputs['Height']);link(mat,bump.outputs[0],p.inputs['Normal'])
    if kind=='wood':link(mat,noise.outputs['Fac'],bump.inputs['Height'])
    if kind=='cloth':
        wave=node(mat,'ShaderNodeTexWave');wave.inputs['Scale'].default_value=270
        wave.inputs['Distortion'].default_value=.6
        link(mat,tex.outputs['Object'],wave.inputs['Vector']);link(mat,wave.outputs['Color'],bump.inputs['Height'])
    if kind=='skin':p.inputs['Subsurface Weight'].default_value=.06
    if kind=='rune':
        p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=.4
    return mat

def triangles(ob):return sum(len(p.vertices)-2 for p in ob.data.polygons)
def bounds(ob):return [[min(v.co[i] for v in ob.data.vertices),max(v.co[i] for v in ob.data.vertices)] for i in range(3)]
def select(ob):
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob

def irregular_stone(ob):
    """Disturb bevels and broken faces, retaining collision boundaries and flat walkable tops."""
    protected={'arena-platform','arena-crate','arena-stairs','arena-wall-segment'}
    old=bounds(ob);verts=set()
    for p in ob.data.polygons:
        if 'source | stone' in ob.data.materials[p.material_index].name:verts.update(p.vertices)
    for i in verts:
        v=ob.data.vertices[i];x,y,z=v.co
        # Boundary planes stay exactly where the engine expects them.
        if any(abs(v.co[a]-old[a][s])<.0001 for a in range(3) for s in range(2)):continue
        if ob.name in protected:
            if ob.name=='arena-stairs' and any(abs(z-h)<.005 for h in [.3,.6,.9,1.2]):continue
            if ob.name=='arena-platform' and z>1.16:continue
        amount=.006 if ob.name!='arena-rubble' else .014
        v.co.x+=amount*math.sin(x*41+y*17+z*21)
        v.co.y+=amount*math.sin(x*29-y*33+z*13)
        if ob.name not in protected:v.co.z+=amount*.5*math.sin(x*37+y*29+z*17)
        for a in range(3):v.co[a]=min(old[a][1],max(old[a][0],v.co[a]))
    ob.data.update()

def atlas_sources(ob):
    old=list(ob.data.materials);uv=ob.data.uv_layers.active
    labels=[]
    for p in ob.data.polygons:
        if old[p.material_index].name.startswith('accent'):labels.append('accent');continue
        coords=[uv.data[i].uv for i in p.loop_indices]
        u=sum(c.x for c in coords)/len(coords);v=sum(c.y for c in coords)/len(coords)
        labels.append('rune' if v>=.9 else ('stone' if u<.5 else 'wood') if v<.5 else 'iron' if u<.5 else 'cloth')
    mats={
        'stone':source_material('stone',(.21,.185,.15),.84,kind='stone',vertex_tint=True),
        'wood':source_material('wood',(.085,.036,.012),.72,kind='wood',vertex_tint=True),
        'iron':source_material('iron',(.045,.042,.035),.54,.78,kind='iron',vertex_tint=True),
        'cloth':source_material('cloth',(.027,.023,.032),.9,kind='cloth',vertex_tint=True),
        'rune':source_material('rune',(.08,.13,.31),.72,kind='rune'),
        'accent':source_material('accent',(.8,.8,.8),.86,kind='cloth')}
    ob.data.materials.clear()
    keys=list(mats)
    for m in mats.values():ob.data.materials.append(m)
    for p,label in zip(ob.data.polygons,labels):p.material_index=keys.index(label)
    irregular_stone(ob)
    if ob.name=='arena-brazier':
        for p in ob.data.polygons:p.use_smooth=True
    if ob.name=='arena-banner':
        # The embroidered crest follows the same hanging cloth instead of floating in front of it.
        for v in ob.data.vertices:
            if v.co.z<2.83:
                t=(2.88-v.co.z)/2.85
                v.co.y+=.013*math.sin(v.co.x*15+t*2)*math.sin(math.pi*t)
        for p in ob.data.polygons:
            if labels[p.index] in ['cloth','accent']:p.use_smooth=True
    return mats

def soften_hands(model):
    skin_indices={i for i,m in enumerate(model.data.materials) if m.name=='skin'}
    for side in [-1,1]:
        faces=[p for p in model.data.polygons if p.material_index in skin_indices and all(side*model.data.vertices[i].co.x>.255 and .75<model.data.vertices[i].co.z<.97 for i in p.vertices)]
        if not faces:continue
        ids=sorted({i for p in faces for i in p.vertices});remap={v:i for i,v in enumerate(ids)}
        kd=kdtree.KDTree(len(ids))
        weights=[]
        for j,i in enumerate(ids):
            kd.insert(model.data.vertices[i].co,j)
            weights.append([(g.group,g.weight) for g in model.data.vertices[i].groups])
        kd.balance()
        mesh=bpy.data.meshes.new('Continuous hand anatomy')
        mesh.from_pydata([model.data.vertices[i].co for i in ids],[],[[remap[i] for i in p.vertices] for p in faces]);mesh.update()
        hand=bpy.data.objects.new('Continuous hand anatomy',mesh);bpy.context.scene.collection.objects.link(hand)
        select(hand)
        rem=hand.modifiers.new('Fuse palm and finger joints','REMESH');rem.mode='VOXEL';rem.voxel_size=.0025
        bpy.ops.object.modifier_apply(modifier=rem.name)
        smooth=hand.modifiers.new('Soft tissue','SMOOTH');smooth.factor=.4;smooth.iterations=3
        bpy.ops.object.modifier_apply(modifier=smooth.name)
        hand.data.materials.append(model.data.materials[next(iter(skin_indices))])
        for group in model.vertex_groups:hand.vertex_groups.new(name=group.name)
        color=hand.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
        for c in color.data:c.color=(.49,.29,.18,1)
        for v in hand.data.vertices:
            neighbors=kd.find_n(v.co,3);total=sum(1/max(d,.0001)**2 for _,_,d in neighbors);combined={}
            for _,index,d in neighbors:
                factor=(1/max(d,.0001)**2)/total
                for g,w in weights[index]:combined[g]=combined.get(g,0)+w*factor
            for g,w in combined.items():hand.vertex_groups[g].add([v.index],w,'REPLACE')
        for p in hand.data.polygons:p.use_smooth=True
        bm=bmesh.new();bm.from_mesh(model.data);bm.faces.ensure_lookup_table()
        remove=[bm.faces[p.index] for p in faces]
        bmesh.ops.delete(bm,geom=remove,context='FACES');bm.to_mesh(model.data);bm.free()
        select(model);hand.select_set(True);bpy.ops.object.join()

def mage_sources(model):
    soften_hands(model)
    # Keep garment silhouettes; add small compression folds at the waist and elbows.
    wool={i for i,m in enumerate(model.data.materials) if m.name=='woven-wool'}
    vertices={i for p in model.data.polygons if p.material_index in wool for i in p.vertices}
    for i in vertices:
        v=model.data.vertices[i];x,y,z=v.co
        if .97<z<1.22 and abs(x)<.2:
            pressure=math.exp(-((z-1.07)/.11)**2)
            v.co.y+=.0035*pressure*math.sin(z*140+x*27)*math.cos(x*22)
    # Decimation preserves the existing vertex groups and rig; most reduction comes from the remesh.
    select(model);before=triangles(model)
    mod=model.modifiers.new('Shared-platform mesh budget','DECIMATE');mod.ratio=min(1,24500/before)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    old=list(model.data.materials)
    mats={}
    for m in old:
        name=m.name
        if name=='accent':mats[name]=source_material(name,(.8,.8,.8),.84,kind='cloth')
        else:
            kind='cloth' if name=='woven-wool' else 'iron' if name=='aged-bronze' else 'skin' if name in ['skin','eyes'] else 'hair' if name=='hair' else 'wood'
            rough={'woven-wool':.9,'worn-leather':.61,'aged-bronze':.4,'skin':.5,'eyes':.19,'hair':.82}.get(name,.75)
            mats[name]=source_material(name,(1,1,1),rough,.76 if name=='aged-bronze' else 0,kind,True)
            if name=='worn-leather':
                # Leather grain is isotropic, unlike wood; replace the stretched mapping.
                for n in mats[name].node_tree.nodes:
                    if n.type=='VECT_MATH':n.inputs[1].default_value=(1,1,1)
            if name=='eyes':bsdf(mats[name]).inputs['Subsurface Weight'].default_value=0
    ob_indices=[p.material_index for p in model.data.polygons]
    model.data.materials.clear()
    for m in old:model.data.materials.append(mats[m.name])
    for p,i in zip(model.data.polygons,ob_indices):p.material_index=i
    # Normalize the rest height only if remeshing introduced numerical drift.
    return mats

def new_image(name,n,noncolor=False):
    im=bpy.data.images.new(name,width=n,height=n,alpha=True)
    if noncolor:im.colorspace_settings.name='Non-Color'
    return im

def save_image(im,path):
    im.filepath_raw=str(path);im.file_format='PNG';im.save();im.pack()

def bake(ob,size,mage=False):
    scene=bpy.context.scene
    for other in scene.objects:
        if other.type=='MESH':other.hide_render=other!=ob
    select(ob)
    # UVs used by all source graphs are Object-space; the new atlas is free of the old atlas packing.
    for layer in list(ob.data.uv_layers):ob.data.uv_layers.remove(layer)
    ob.data.uv_layers.new(name='SurfaceUV')
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.012)
    bpy.ops.object.mode_set(mode='OBJECT')
    sources=list(ob.data.materials)
    # A unique copy per material avoids cross-object target-node state during baking.
    for i,m in enumerate(sources):ob.data.materials[i]=m.copy()
    sources=list(ob.data.materials)
    outputs=[]
    bake_folder=OUT/'textures';bake_folder.mkdir(exist_ok=True)
    for mode in ['basecolor','normal','orm','emissive']:
        im=new_image(ob.name+'-'+mode,size,mode in ['normal','orm'])
        saved=[]
        for mat in sources:
            p=bsdf(mat);nodes=mat.node_tree.nodes;links=mat.node_tree.links
            out=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
            target=nodes.new('ShaderNodeTexImage');target.image=im;nodes.active=target;target.select=True
            if mode!='normal':
                emit=nodes.new('ShaderNodeEmission');emit.inputs['Strength'].default_value=1
                if mode in ['basecolor','emissive']:
                    inp=p.inputs['Base Color' if mode=='basecolor' else 'Emission Color']
                    if inp.is_linked:links.new(inp.links[0].from_socket,emit.inputs['Color'])
                    else:emit.inputs['Color'].default_value=inp.default_value
                    if mode=='emissive':emit.inputs['Strength'].default_value=p.inputs['Emission Strength'].default_value
                else:
                    combine=nodes.new('ShaderNodeCombineColor');combine.inputs[0].default_value=1
                    for source,socket in [(p.inputs['Roughness'],combine.inputs[1]),(p.inputs['Metallic'],combine.inputs[2])]:
                        if source.is_linked:links.new(source.links[0].from_socket,socket)
                        else:socket.default_value=source.default_value
                    links.new(combine.outputs[0],emit.inputs['Color'])
                links.new(emit.outputs[0],out.inputs['Surface'])
                saved.append((mat,out,p,emit,target))
            else:saved.append((mat,out,p,None,target))
        scene.render.bake.margin=8 if size<=1024 else 12
        bpy.ops.object.bake(type='NORMAL' if mode=='normal' else 'EMIT',use_clear=True)
        save_image(im,bake_folder/(ob.name+'-'+mode+'.png'))
        outputs.append(im)
        for mat,out,p,emit,target in saved:
            mat.node_tree.links.new(p.outputs[0],out.inputs['Surface'])
            mat.node_tree.nodes.remove(target)
            if emit:mat.node_tree.nodes.remove(emit)
        print('BAKED',ob.name,mode,flush=True)
    # Store team-color regions as a separate material using the SAME maps as the body.
    accent_faces={p.index for p in ob.data.polygons if 'accent' in ob.data.materials[p.material_index].name}
    # For accent material, neutralize the original purple in the baked image so runtime tints work.
    # Keeping its baked purple as the default material color preserves the established art direction.
    final=pbr_material(ob.name+'-body' if mage else ob.name+'-surface',outputs)
    accent=None
    if accent_faces:
        accent=final.copy();accent.name='accent'
        p=bsdf(accent);source=p.inputs['Base Color'].links[0].from_socket
        multiply=node(accent,'ShaderNodeMixRGB');multiply.blend_type='MULTIPLY';multiply.inputs[0].default_value=1
        multiply.inputs[2].default_value=(.045,.025,.095,1) if mage else (.12,.075,.25,1)
        link(accent,source,multiply.inputs[1]);link(accent,multiply.outputs[0],p.inputs['Base Color'])
    for ca in list(ob.data.color_attributes):ob.data.color_attributes.remove(ca)
    ob.data.materials.clear();ob.data.materials.append(final)
    if accent:ob.data.materials.append(accent)
    for p in ob.data.polygons:p.material_index=1 if p.index in accent_faces else 0
    return outputs

def pbr_material(name,images):
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    p=bsdf(mat);nodes,links=mat.node_tree.nodes,mat.node_tree.links
    textures=[]
    for im in images:
        tx=nodes.new('ShaderNodeTexImage');tx.image=im;textures.append(tx)
    links.new(textures[0].outputs['Color'],p.inputs['Base Color'])
    normal=nodes.new('ShaderNodeNormalMap');normal.uv_map='SurfaceUV'
    links.new(textures[1].outputs['Color'],normal.inputs['Color']);links.new(normal.outputs[0],p.inputs['Normal'])
    split=nodes.new('ShaderNodeSeparateColor');links.new(textures[2].outputs['Color'],split.inputs[0])
    links.new(split.outputs['Green'],p.inputs['Roughness']);links.new(split.outputs['Blue'],p.inputs['Metallic'])
    links.new(textures[3].outputs['Color'],p.inputs['Emission Color']);p.inputs['Emission Strength'].default_value=1
    return mat

def smooth_noise(n,gx,gy,seed):
    rng=np.random.default_rng(seed);grid=rng.random((gy,gx),dtype=np.float32)
    x=np.arange(n,dtype=np.float32)*gx/n;y=np.arange(n,dtype=np.float32)*gy/n
    ix=x.astype(int);iy=y.astype(int);fx=x-ix;fy=y-iy
    fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy)
    return ((1-fy[:,None])*((1-fx[None,:])*grid[iy[:,None]%gy,ix[None,:]%gx]+fx[None,:]*grid[iy[:,None]%gy,(ix[None,:]+1)%gx])+
            fy[:,None]*((1-fx[None,:])*grid[(iy[:,None]+1)%gy,ix[None,:]%gx]+fx[None,:]*grid[(iy[:,None]+1)%gy,(ix[None,:]+1)%gx]))

def texture_image(name,pixels,linear=False):
    im=new_image(name,pixels.shape[0],linear)
    if pixels.shape[-1]==3:pixels=np.concatenate([pixels,np.ones((*pixels.shape[:2],1),dtype=np.float32)],axis=-1)
    im.pixels.foreach_set(pixels.astype(np.float32).ravel())
    path=ROOT/'Assets'/'Models'/'materials'/(name+'.png');backup(path);save_image(im,path)
    return im

def stone_surfaces():
    n=2048;y,x=np.mgrid[:n,:n].astype(np.float32)/n
    cloud=smooth_noise(n,7,7,8)-.5;grain=smooth_noise(n,180,180,17)-.5
    rng=np.random.default_rng(35);micro=rng.random((n,n),dtype=np.float32)-.5
    for wall,name in [(False,'stone-floor'),(True,'stone-wall')]:
        rows=8 if wall else 4;cols=5 if wall else 4
        row=np.floor(y*rows).astype(int)
        offset=(row%2)*.5
        xx=x*cols+offset;cell=np.floor(xx)
        px=xx-cell;py=y*rows-row
        edge=np.minimum(np.minimum(px,1-px),np.minimum(py,1-py))
        jitter=(grain*.01+cloud*.018)
        stone=np.clip((edge-jitter-.007)/.035,0,1)
        block=np.sin(cell*37.5+row*19.7)*.04
        pores=(micro>.485).astype(np.float32)
        color=np.stack([.19+block+cloud*.055+grain*.018,.17+block*.85+cloud*.05+grain*.016,.137+block*.7+cloud*.04+grain*.013],axis=-1)
        color*= (.31+.69*stone)[...,None]
        color*= (1-pores*.22)[...,None]
        # Sparse, branching mineral fractures; periodic, with mortar masking.
        path=px-(.35+.13*np.sin(py*9+cell*3+row)+.035*np.sin(py*33))
        cracks=(np.abs(path)<.0035)&(stone>.7)&(np.sin(cell*23+row*11)>.38)&(py>.2)&(py<.84)
        color[cracks]*=.45
        height=.25+stone*.24+cloud*.012+grain*.012+micro*.003-pores*.006-cracks*.025
        rough=np.clip(.79+cloud*.12+grain*.08+(1-stone)*.1,0,1)
        ao=np.clip(.55+stone*.45,0,1)
        if wall:
            color*=(.65+.35*np.clip(y/.22,0,1))[...,None]
            frieze=(y>.72)&(y<.78)
            rune=(np.abs(np.sin(x*math.tau*10))<.1)&frieze
            color[rune]*=.45;height[rune]-=.035
        arrays=[color,height,rough,ao]
        for a in arrays:
            a[:,-1]=a[:,0]
            if not wall:a[-1]=a[0]
        dx=(np.roll(height,-1,1)-np.roll(height,1,1))*2.5
        dy=(np.roll(height,-1,0)-np.roll(height,1,0))*2.5
        normal=np.stack([-dx,-dy,np.ones_like(dx)],axis=-1);normal/=np.linalg.norm(normal,axis=-1)[...,None]
        normal=normal*.5+.5;normal[:,-1]=normal[:,0]
        if not wall:normal[-1]=normal[0]
        texture_image(name+'_basecolor',np.clip(color,0,1))
        texture_image(name+'_normal',normal,True)
        texture_image(name+'_roughness',np.repeat(rough[...,None],3,axis=-1),True)
        texture_image(name+'_ao',np.repeat(ao[...,None],3,axis=-1),True)
        packed=np.zeros((n,n,4),dtype=np.float32);packed[...,3]=1-rough
        texture_image(name+'_metallic-smoothness',packed,True)
        print('TEXTURES',name,flush=True)

def render(ob,filename,rig=None):
    scene=bpy.context.scene
    for other in scene.objects:
        if other.type=='MESH':other.hide_render=other!=ob
    ground=bpy.data.objects.get('Review ground')
    if ground is None:
        bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.005));ground=bpy.context.object;ground.name='Review ground'
        mat=bpy.data.materials.new('Review ground');mat.use_nodes=True
        bsdf(mat).inputs['Base Color'].default_value=(.055,.06,.07,1);bsdf(mat).inputs['Roughness'].default_value=.82
        ground.data.materials.append(mat)
    ground.hide_render=False
    box=bounds(ob);h=box[2][1];w=box[0][1]-box[0][0];d=box[1][1]-box[1][0]
    span=max(h,w,d);target=Vector((0,0,h*.48))
    if not scene.camera:
        data=bpy.data.cameras.new('Review camera');camera=bpy.data.objects.new('Review camera',data);scene.collection.objects.link(camera);scene.camera=camera
    camera=scene.camera;camera.data.type='ORTHO';camera.data.ortho_scale=max(h*1.2,(w+d*.6)*1.13)
    camera.location=(span*1.4,-span*2.5,h*.65+span*1.1)
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    for light in list(scene.objects):
        if light.type=='LIGHT':bpy.data.objects.remove(light,do_unlink=True)
    for pos,power in [((-span,-span,span*1.7),95),((span,-span*.3,span),45),((0,span,span*1.7),80)]:
        data=bpy.data.lights.new('Review softbox','AREA');data.energy=power*max(span,1)**2;data.size=max(span*.8,1)
        light=bpy.data.objects.new('Review softbox',data);scene.collection.objects.link(light);light.location=pos
        light.rotation_euler=(target-light.location).to_track_quat('-Z','Y').to_euler()
    scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100
    scene.cycles.samples=24;scene.cycles.use_denoising=True
    scene.render.filepath=str(OUT/(filename+'.png'));bpy.ops.render.render(write_still=True)
    print('RENDERED',filename,flush=True)

def main():
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/'output'/'mage'/'mage.blend'))
    scene=bpy.context.scene
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=8
    scene.render.bake.use_selected_to_active=False
    scene.world.use_nodes=True
    bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND')
    bg.inputs['Color'].default_value=(.08,.09,.11,1);bg.inputs['Strength'].default_value=.35
    model=bpy.data.objects['mage-mesh'];rig=bpy.data.objects['mage']
    rig.animation_data.action=None
    for track in rig.animation_data.nla_tracks:track.mute=True
    for pb in rig.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
    scene.frame_set(1);bpy.context.view_layer.update()
    with bpy.data.libraries.load(str(ROOT/'output'/'arena'/'arena-assets.blend'),link=False) as (src,dst):
        dst.objects=[n for n in src.objects if n.startswith('arena-') or n=='flame']
    for ob in dst.objects:
        if ob and ob.name not in scene.objects:scene.collection.objects.link(ob)
    props=[o for o in scene.objects if o.type=='MESH' and o.name.startswith('arena-')]
    source_specs={s['name']:s for s in json.loads((ROOT/'output'/'arena'/'build-report.json').read_text())}
    queue=props+[model]
    for ob in queue:
        label='mage' if ob==model else ob.name
        if ONLY and ONLY!=label:continue
        path=ROOT/'Assets'/'Models'/('characters' if ob==model else 'arena')/(label+'.glb')
        backup(path)
        original=triangles(ob);oldbounds=bounds(ob)
        if label=='arena-spawn-circle':
            # Keep the readable directional sigil and its transparency; soften overbright emission.
            p=bsdf(ob.data.materials[0]);p.inputs['Emission Strength'].default_value=.65
            for ca in list(ob.data.color_attributes):ob.data.color_attributes.remove(ca)
            size=1024
        else:
            if ob==model:mage_sources(ob)
            else:atlas_sources(ob)
            size=2048 if ob==model else 1024 if label in ['arena-crate','arena-pillar','arena-platform','arena-wall-segment','arena-banner'] else 512
            bake(ob,size,ob==model)
        select(ob)
        if ob==model:rig.select_set(True);bpy.context.view_layer.objects.active=rig
        for child in ob.children:child.select_set(True)
        bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,
            export_animations=ob==model,export_animation_mode='NLA_TRACKS',export_force_sampling=True,
            export_frame_range=False,export_yup=True,export_materials='EXPORT',export_skins=True,
            export_tangents=True,export_vertex_color='NONE',export_all_vertex_colors=False)
        result={'name':label,'before_triangles':original,'triangles':triangles(ob),'texture_size':size,
            'bounds_blender':bounds(ob),'before_bounds':oldbounds,'materials':[m.name for m in ob.data.materials],
            'bytes':path.stat().st_size,'source':'output/mage/mage.blend' if ob==model else 'output/arena/arena-assets.blend'}
        REPORT.append(result)
        if ob==model:
            rig.animation_data.action=None
            for track in rig.animation_data.nla_tracks:track.mute=True
            for pb in rig.pose.bones:
                pb.location=(0,0,0);pb.rotation_euler=(0,0,0);pb.rotation_quaternion=(1,0,0,0);pb.scale=(1,1,1)
            scene.frame_set(1);bpy.context.view_layer.update()
        if '--no-render' not in sys.argv:render(ob,label,rig if ob==model else None)
        scene.cycles.samples=8
        print('ASSET_COMPLETE',json.dumps(result),flush=True)
        (OUT/'build-report.json').write_text(json.dumps(REPORT,indent=2))
    if not ONLY:stone_surfaces()
    # Store only editable model/rig objects, keeping the original sources untouched.
    for ob in list(scene.objects):
        if ob.type in ['CAMERA','LIGHT'] or ob.name=='Review ground':bpy.data.objects.remove(ob,do_unlink=True)
        else:ob.hide_render=False
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/('models-'+ONLY+'.blend' if ONLY else 'models.blend')))
    print('REALISM_PASS_COMPLETE',flush=True)

if __name__=='__main__':main()
