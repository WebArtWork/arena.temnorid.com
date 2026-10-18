"""Blender studies for the other three starter spells; CPU renders avoid the live viewport stall."""
import bpy
import math
import random
from pathlib import Path
from mathutils import Vector

OUT = Path('C:/Users/Unity/work/temnorid-arena/output/spells')
random.seed(73)

def material(name, color, strength=1.5, membrane=False, ice=False):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    nodes.clear()
    out = nodes.new('ShaderNodeOutputMaterial')
    emission = nodes.new('ShaderNodeEmission')
    emission.inputs['Color'].default_value = (*color,1)
    emission.inputs['Strength'].default_value = strength
    transparent = nodes.new('ShaderNodeBsdfTransparent')
    noise = nodes.new('ShaderNodeTexNoise')
    noise.noise_dimensions = '4D'
    noise.inputs['Scale'].default_value = 6
    noise.inputs['Detail'].default_value = 3
    noise.inputs['W'].driver_add('default_value').driver.expression = 'frame/18'
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = .26
    ramp.color_ramp.elements[0].color = (.03,.03,.03,1)
    ramp.color_ramp.elements[1].position = .76
    ramp.color_ramp.elements[1].color = (.7,.7,.7,1)
    links.new(noise.outputs['Fac'],ramp.inputs[0])
    factor = ramp.outputs[0]
    if membrane:
        facing = nodes.new('ShaderNodeLayerWeight')
        fade = nodes.new('ShaderNodeMapRange')
        fade.inputs['To Min'].default_value = .015
        fade.inputs['To Max'].default_value = .7
        links.new(facing.outputs['Fresnel'],fade.inputs[0])
        factor = fade.outputs[0]
    mix = nodes.new('ShaderNodeMixShader')
    links.new(factor,mix.inputs[0])
    links.new(transparent.outputs[0],mix.inputs[1])
    if ice:
        bsdf = nodes.new('ShaderNodeBsdfPrincipled')
        bsdf.inputs['Base Color'].default_value = (*color,1)
        bsdf.inputs['Metallic'].default_value = .15
        bsdf.inputs['Roughness'].default_value = .15
        bsdf.inputs['Transmission Weight'].default_value = .25
        bsdf.inputs['Emission Color'].default_value = (*color,1)
        bsdf.inputs['Emission Strength'].default_value = .25
        links.new(bsdf.outputs[0],mix.inputs[2])
    else: links.new(emission.outputs[0],mix.inputs[2])
    links.new(mix.outputs[0],out.inputs['Surface'])
    return mat

def mesh_obj(scene, name, verts, faces, mat):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    obj = bpy.data.objects.new(name,mesh)
    scene.collection.objects.link(obj)
    mesh.materials.append(mat)
    return obj

def ribbon(scene,name,points,width,mat):
    verts,faces=[],[]
    for i,p in enumerate(points):
        tangent=Vector(points[min(i+1,len(points)-1)])-Vector(points[max(i-1,0)])
        side=tangent.cross(Vector((0,0,1))).normalized()
        if side.length < .01: side=Vector((1,0,0))
        w=width*max(.03,math.sin(math.pi*i/(len(points)-1)))
        verts.extend([Vector(p)-side*w,Vector(p)+side*w])
        if i: faces.append((2*i-2,2*i,2*i+1,2*i-1))
    return mesh_obj(scene,name,verts,faces,mat)

def ring(scene,name,radius,height,width,mat,wave=.015):
    verts,faces=[],[]
    for i in range(160):
        a=i*math.tau/160
        r=radius+math.sin(a*13)*wave+math.sin(a*23)*wave*.5
        for w in [-width,width]: verts.append(((r+w)*math.cos(a),(r+w)*math.sin(a),height+math.sin(a*17)*wave*.3))
        j=(i+1)%160
        faces.append((i*2,j*2,j*2+1,i*2+1))
    return mesh_obj(scene,name,verts,faces,mat)

def studio(name, camera_pos, target, size):
    scene=bpy.data.scenes.new(name)
    bpy.context.window.scene=scene
    try: scene.render.engine='CYCLES'
    except TypeError as e: raise RuntimeError(str(e))
    scene.cycles.device='CPU'
    scene.cycles.samples=24
    scene.render.resolution_x,scene.render.resolution_y,scene.render.resolution_percentage=1050,800,100
    scene.render.image_settings.file_format='PNG'
    scene.frame_start,scene.frame_end,scene.render.fps=1,48,24
    world=bpy.data.worlds.new(name+' | World')
    world.use_nodes=True
    background=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND')
    background.inputs['Color'].default_value=(.015,.02,.03,1)
    background.inputs['Strength'].default_value=.3
    scene.world=world
    bpy.ops.mesh.primitive_plane_add(size=200)
    floor=bpy.context.object
    floor.name='Studio | dark ground'
    mat=bpy.data.materials.new(name+' | charcoal')
    mat.use_nodes=True
    bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value=(.02,.025,.035,1)
    bsdf.inputs['Roughness'].default_value=.8
    floor.data.materials.append(mat)
    data=bpy.data.cameras.new(name+' camera')
    camera=bpy.data.objects.new(name+' camera',data)
    scene.collection.objects.link(camera)
    camera.location=camera_pos
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    data.type='ORTHO'; data.ortho_scale=size
    scene.camera=camera
    for pos,energy,color in [((2,-3,5),500,(.45,.65,1)),((-3,2,3),300,(.65,.4,1))]:
        light=bpy.data.lights.new('Studio softbox','AREA')
        light.energy=energy; light.shape='DISK'; light.size=4; light.color=color
        obj=bpy.data.objects.new('Studio softbox',light)
        scene.collection.objects.link(obj); obj.location=pos
        obj.rotation_euler=(-obj.location).to_track_quat('-Z','Y').to_euler()
    tree=bpy.data.node_groups.new(name+' | glow','CompositorNodeTree')
    tree.interface.new_socket(name='Image',in_out='OUTPUT',socket_type='NodeSocketColor')
    scene.compositing_node_group=tree
    layers=tree.nodes.new('CompositorNodeRLayers')
    glare=tree.nodes.new('CompositorNodeGlare')
    # Blender 5.2 exposes glare choices as socket enum values.
    glare.inputs['Type'].default_value='Fog Glow'
    glare.inputs['Strength'].default_value=.3
    output=tree.nodes.new('NodeGroupOutput')
    tree.links.new(layers.outputs['Image'],glare.inputs['Image'])
    tree.links.new(glare.outputs['Image'],output.inputs['Image'])
    return scene

frost=studio('FROST | radial freezing front',(8,-10,9),(0,0,.2),14.6)
ice=material('Frost | blue crystalline facets',(.25,.65,.9),ice=True)
edge=material('Frost | luminous freezing edge',(.2,.7,1),2)
mist=material('Frost | fading mist',(.15,.35,.5),.5)
for i,r in enumerate([5.8,5.4,4.8]):
    obj=ring(frost,'Freezing wave',r,.08+i*.018,.04 if i else .11,edge,.035)
    for frame,scale in [(1,.08),(18,1),(36,1.03),(48,1.04)]:
        obj.scale=(scale,scale,1);obj.keyframe_insert(data_path='scale',frame=frame)
for i in range(64):
    a=i*math.tau/64+random.uniform(-.03,.03)
    r=random.uniform(4.4,5.9); w=random.uniform(.04,.12); h=random.uniform(.15,.65)
    x,y=r*math.cos(a),r*math.sin(a)
    mesh_obj(frost,'Faceted ice splinter',[(x-w,y-w,.02),(x+w,y-w,.02),(x+w,y+w,.02),(x-w,y+w,.02),(x+w*.6,y,h)],[(0,4,1),(1,4,2),(2,4,3),(3,4,0),(0,1,2,3)],ice)
for i in range(22):
    a=random.uniform(0,math.tau);r=random.uniform(3.8,5.8)
    points=[]
    for j in range(45):
        t=j/44;angle=a+t*.35
        points.append((r*math.cos(angle),r*math.sin(angle),.13+math.sin(t*math.pi)*random.uniform(.1,.3)))
    ribbon(frost,'Low cold vapor',points,.15,mist)

# A shallow noise-filled volume gives the freezing front a soft vapor layer.
bpy.ops.mesh.primitive_torus_add(major_segments=96,minor_segments=12,major_radius=5.35,minor_radius=.38,location=(0,0,.16))
fog=bpy.context.object;fog.name='Cold vapor volume';fog.scale.z=.35
fogmat=bpy.data.materials.new('Frost | suspended cold vapor')
fogmat.use_nodes=True
nodes,links=fogmat.node_tree.nodes,fogmat.node_tree.links
nodes.clear()
out=nodes.new('ShaderNodeOutputMaterial')
volume=nodes.new('ShaderNodeVolumePrincipled')
volume.inputs['Color'].default_value=(.25,.55,.8,1)
volume.inputs['Emission Color'].default_value=(.08,.2,.3,1)
volume.inputs['Emission Strength'].default_value=.08
noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=5
density=nodes.new('ShaderNodeMath');density.operation='MULTIPLY';density.inputs[1].default_value=.7
links.new(noise.outputs['Fac'],density.inputs[0]);links.new(density.outputs[0],volume.inputs['Density'])
links.new(volume.outputs['Volume'],out.inputs['Volume'])
fog.data.materials.append(fogmat)

shield=studio('SHIELD | transparent arcane field',(3,-5,2.8),(0,0,1),3.8)
field=material('Shield | transparent refractive membrane',(.32,.09,.65),1.2,membrane=True)
arcs=material('Shield | flowing violet arcs',(.5,.18,1),2.4)
bpy.ops.mesh.primitive_uv_sphere_add(segments=64,ring_count=32,location=(0,0,1.05))
shell=bpy.context.object;shell.name='Transparent protective membrane';shell.scale=(.78,.78,1.05)
shell.data.materials.append(field)
for p in shell.data.polygons:p.use_smooth=True
for i in range(5):
    phi=i*math.pi/5;points=[]
    for j in range(100):
        a=j*math.tau/99
        points.append((math.cos(a)*.79*math.cos(phi),math.cos(a)*.79*math.sin(phi),1.05+math.sin(a)*1.06))
    obj=ribbon(shield,'Streaming field arc',points,.009,arcs)
    obj.rotation_euler.z=0;obj.keyframe_insert(data_path='rotation_euler',frame=1)
    obj.rotation_euler.z=.4;obj.keyframe_insert(data_path='rotation_euler',frame=48)
# A subdued mage inside the field shows transparency and protective scale.
before=set(shield.objects)
bpy.ops.import_scene.gltf(filepath='C:/Users/Unity/work/temnorid-arena/Assets/Models/characters/mage.glb')
for obj in set(shield.objects)-before:
    if obj.animation_data: obj.animation_data_clear()

blink=studio('BLINK | displacement remnants',(3,-6,3),(0,0,1),4.1)
violet=material('Blink | violet departure wisps',(.35,.12,.8),2.1)
cyan=material('Blink | cool arrival wisps',(.12,.6,1),2)
for i in range(9):
    points=[]
    for j in range(80):
        t=j/79;a=t*math.pi*2.2+i*math.tau/9;r=.14+math.sin(t*math.pi)*(.5+.04*math.sin(t*23+i*2.1))
        points.append((math.cos(a)*r,math.sin(a)*r,t*2.1+.02))
    obj=ribbon(blink,'Displacement filament',points,.018,cyan if i%3==0 else violet)
    for frame,w in [(1,1.1),(18,.8),(36,.3),(48,.08)]:
        obj.scale=(w,w,1);obj.keyframe_insert(data_path='scale',frame=frame)
ring(blink,'Ground remnant',.64,.035,.015,cyan)
for i in range(45):
    a=random.uniform(0,math.tau);r=random.uniform(.2,.7);z=random.uniform(.1,2)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=random.uniform(.003,.013),location=(r*math.cos(a),r*math.sin(a),z))
    bpy.context.object.name='Residual mote';bpy.context.object.data.materials.append(cyan)

# Keep the accepted fire study alongside the three new scenes in one editable file.
fire_path=OUT/'fire'/'fire-study.blend'
if fire_path.exists():
    with bpy.data.libraries.load(str(fire_path),link=False) as (source,target):
        target.scenes=source.scenes[:1]
    for scene in target.scenes:
        if scene:scene.name='FIRE | combustion study'

for scene in [frost,shield,blink]:scene.frame_set(18)
bpy.context.window.scene=shield
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'elemental-spells.blend'))
for scene,name in [(frost,'frost-nova'),(shield,'arcane-shield'),(blink,'blink')]:
    scene.render.filepath=str(OUT/(name+'-blender.png'))
    bpy.context.window.scene=scene
    bpy.ops.render.render(write_still=True)
    print('RENDERED',name,flush=True)
print('All three spell studies rendered.',flush=True)
