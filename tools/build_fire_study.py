"""Editable Blender fireball shape study. Run in Blender; Unity uses its own particle effect."""
import bpy
import math
import random
from pathlib import Path
from mathutils import Vector

OUT = Path('C:/Users/Unity/work/temnorid-arena/output/spells/fire')
OUT.mkdir(parents=True, exist_ok=True)
random.seed(24)
scene = bpy.context.scene
# Keep the user's starting scene available in its original collection.
for collection in scene.collection.children:
    collection.hide_render = True
    collection.hide_viewport = True
study = bpy.data.collections.new('FIRE | combustion study')
scene.collection.children.link(study)

def put(obj):
    for collection in list(obj.users_collection): collection.objects.unlink(obj)
    study.objects.link(obj)
    return obj

def fire_material(name, strength, hot=False):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    links = mat.node_tree.links
    out = nodes.new('ShaderNodeOutputMaterial')
    emission = nodes.new('ShaderNodeEmission')
    emission.inputs['Strength'].default_value = strength
    noise = nodes.new('ShaderNodeTexNoise')
    noise.noise_dimensions = '4D'
    noise.inputs['Scale'].default_value = 5.8
    noise.inputs['Detail'].default_value = 3.2
    noise.inputs['Roughness'].default_value = .7
    noise.inputs['W'].driver_add('default_value').driver.expression = 'frame / 13.0'
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = .23
    ramp.color_ramp.elements[0].color = (.65, .018, .001, 1)
    ramp.color_ramp.elements[1].position = .76
    ramp.color_ramp.elements[1].color = (1, .72 if hot else .34, .15 if hot else .008, 1)
    ramp.color_ramp.elements.new(.49).color = (1, .19, .006, 1)
    links.new(noise.outputs['Fac'], ramp.inputs[0])
    links.new(ramp.outputs[0], emission.inputs['Color'])
    density = nodes.new('ShaderNodeMapRange')
    density.inputs['From Min'].default_value = .37
    density.inputs['From Max'].default_value = .72
    density.inputs['To Max'].default_value = .45
    links.new(noise.outputs['Fac'], density.inputs['Value'])
    transparent = nodes.new('ShaderNodeBsdfTransparent')
    mix = nodes.new('ShaderNodeMixShader')
    facing = nodes.new('ShaderNodeLayerWeight')
    inverse = nodes.new('ShaderNodeMath')
    inverse.operation = 'SUBTRACT'
    inverse.inputs[0].default_value = 1
    links.new(facing.outputs['Fresnel'], inverse.inputs[1])
    soften = nodes.new('ShaderNodeMath')
    soften.operation = 'MULTIPLY'
    links.new(inverse.outputs[0], soften.inputs[0])
    links.new(density.outputs[0], soften.inputs[1])
    links.new(soften.outputs[0], mix.inputs[0])
    coords = nodes.new('ShaderNodeTexCoord')
    xyz = nodes.new('ShaderNodeSeparateXYZ')
    links.new(coords.outputs['Generated'], xyz.inputs[0])
    heat = nodes.new('ShaderNodeValToRGB')
    heat.color_ramp.elements[0].color = (.4,.004,.0001,1)
    heat.color_ramp.elements[1].color = (1,.62,.065,1)
    heat.color_ramp.elements.new(.6).color = (1,.07,.001,1)
    links.new(xyz.outputs['X'], heat.inputs[0])
    multiply = nodes.new('ShaderNodeMixRGB')
    multiply.blend_type = 'MULTIPLY'
    multiply.inputs[0].default_value = 1
    links.new(heat.outputs[0], multiply.inputs[1])
    links.new(ramp.outputs[0], multiply.inputs[2])
    links.new(multiply.outputs[0], emission.inputs['Color'])
    links.new(transparent.outputs[0], mix.inputs[1])
    links.new(emission.outputs[0], mix.inputs[2])
    links.new(mix.outputs[0], out.inputs['Surface'])
    return mat

outer = fire_material('01 | turbulent orange flame', 1.4)
inner = fire_material('02 | hot yellow combustion', 2.2, True)

def tongue(index, length, width, y, z, material):
    verts, faces = [], []
    phase = random.uniform(0, 6.28)
    for j in range(33):
        t = j / 32
        x = .22 - length * t
        taper = math.sin(math.pi * t) ** .7
        radius = max(.001, width * taper * (1 - .55 * t))
        cy = y * (.35 + t) + math.sin(t * 11 + phase) * .13 * t
        cz = z * (.35 + t) + .22 * t*t + math.cos(t * 14 + phase) * .1 * t
        for k in range(10):
            a = k * math.tau / 10
            verts.append((x, cy + math.cos(a)*radius, cz + math.sin(a)*radius))
        if j:
            for k in range(10):
                a = (j-1)*10+k
                b = (j-1)*10+(k+1)%10
                faces.append((a,b,b+10,a+10))
    mesh = bpy.data.meshes.new('Sculpted turbulent flame')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(f'Flame tongue {index:02}', mesh)
    study.objects.link(obj)
    mesh.materials.append(material)
    for p in mesh.polygons: p.use_smooth = True
    for frame, factor in [(1,1),(9,.88),(18,1.08),(29,.94),(40,1.04),(49,1)]:
        obj.scale = (factor,1,1)
        obj.keyframe_insert(data_path='scale', frame=frame)
    return obj

for i in range(26):
    angle = random.uniform(0, math.tau)
    radius = random.uniform(.03,.24)
    tongue(i, random.uniform(.65,2.45), random.uniform(.055,.15), math.cos(angle)*radius,
           math.sin(angle)*radius, inner if i < 9 else outer)

ember_mat = fire_material('03 | hot drifting sparks', 4, True)
for i in range(36):
    x = random.uniform(-3,.1)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=random.uniform(.006,.017),
        location=(x, random.gauss(0,.24), random.gauss(.08,.23)))
    obj = put(bpy.context.object)
    obj.name = 'Ember'
    obj.scale.x = random.uniform(2,5)
    obj.data.materials.append(ember_mat)

bpy.ops.mesh.primitive_plane_add(size=200, location=(0,0,-.68))
floor = put(bpy.context.object)
floor.name = 'Studio | charcoal ground'
mat = bpy.data.materials.new('Charcoal')
mat.use_nodes = True
bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
bsdf.inputs['Base Color'].default_value = (.025,.03,.04,1)
bsdf.inputs['Roughness'].default_value = .85
floor.data.materials.append(mat)

for position, energy, color, radius in [((-.2,0,.1),180,(1,.22,.025),.7),((-1.4,0,.2),60,(1,.08,.01),1)]:
    data = bpy.data.lights.new('Combustion glow', 'POINT')
    data.energy, data.color, data.shadow_soft_size = energy,color,radius
    obj = bpy.data.objects.new('Combustion glow',data)
    study.objects.link(obj)
    obj.location = position

data = bpy.data.cameras.new('Fire study camera')
camera = bpy.data.objects.new('Fire study camera',data)
study.objects.link(camera)
camera.location = (-.8,-6,2.4)
camera.rotation_euler = (Vector((-1.1,0,.03))-camera.location).to_track_quat('-Z','Y').to_euler()
data.type = 'ORTHO'
data.ortho_scale = 4.4
scene.camera = camera
scene.world.color = (.015,.015,.015)
scene.render.resolution_x,scene.render.resolution_y,scene.render.resolution_percentage = 1400,800,100
scene.render.image_settings.file_format = 'PNG'
scene.frame_start,scene.frame_end = 1,48
scene.render.fps = 24
scene.frame_set(18)
scene.use_nodes = True
# Blender 5 uses a compositor node group.
tree = bpy.data.node_groups.new('Fire study glow','CompositorNodeTree')
tree.interface.new_socket(name='Image',in_out='OUTPUT',socket_type='NodeSocketColor')
scene.compositing_node_group = tree
rl = tree.nodes.new('CompositorNodeRLayers')
glare = tree.nodes.new('CompositorNodeGlare')
glare.inputs['Type'].default_value = 'Fog Glow'
glare.inputs['Strength'].default_value = .45
output = tree.nodes.new('NodeGroupOutput')
tree.links.new(rl.outputs['Image'],glare.inputs['Image'])
tree.links.new(glare.outputs['Image'],output.inputs['Image'])
for area in bpy.context.screen.areas:
    if area.type == 'VIEW_3D':
        area.spaces.active.region_3d.view_perspective = 'CAMERA'
scene.render.filepath = str(OUT / 'fireball-blender.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'fire-study.blend'))
print('Fire study saved:', len(study.objects), 'objects; animated frames 1-48')
