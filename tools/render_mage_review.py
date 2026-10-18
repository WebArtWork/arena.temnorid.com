"""Finalize the live review scene: separate GLB export and neutral studio views."""
import bpy, os, json
from mathutils import Vector
OUT='C:/Users/Unity/work/temnorid-arena/output/mage/review-v2'
scene=bpy.context.scene;model=bpy.data.objects['mage-mesh'];rig=bpy.data.objects['mage']
rig.animation_data.action=None
for track in rig.animation_data.nla_tracks:track.mute=True
for pb in rig.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
scene.frame_set(1);bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);model.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.gltf(filepath=OUT+'/mage-review.glb',export_format='GLB',use_selection=True,
    export_animations=True,export_animation_mode='NLA_TRACKS',export_nla_strips=True,
    export_force_sampling=True,export_frame_range=False,export_yup=True,
    export_materials='EXPORT',export_skins=True,export_all_influences=False)
# glTF export evaluates every clip and can leave the rig on the final death pose.
rig.animation_data.action=None
for track in rig.animation_data.nla_tracks:track.mute=True
for pb in rig.pose.bones:pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
scene.frame_set(1);bpy.context.view_layer.update()
report={'triangles':sum(len(p.vertices)-2 for p in model.data.polygons),
        'bones':len(rig.data.bones),'clips':[a.name for a in bpy.data.actions],
        'unweighted_vertices':sum(not v.groups for v in model.data.vertices),
        'status':'Review candidate; walking pose inspected; Unity validation pending.'}
with open(OUT+'/review-report.json','w') as f:json.dump(report,f,indent=2)

def aim(ob,point):ob.rotation_euler=(Vector(point)-ob.location).to_track_quat('-Z','Y').to_euler()
def area(name,pos,power,size):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    ob=bpy.data.objects.new(name,data);scene.collection.objects.link(ob);ob.location=pos;aim(ob,(0,0,1))
area('studio-key',(-2,-3,4),450,3);area('studio-fill',(2,-2,2),220,2);area('studio-rim',(0,2,3),500,2)
scene.world.use_nodes=True
bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND')
bg.inputs[0].default_value=(.19,.17,.15,1);bg.inputs[1].default_value=.5
bpy.ops.mesh.primitive_plane_add(size=200)
floor=bpy.context.object;floor.name='review-studio-floor';floor.location.z=-.003
mat=bpy.data.materials.new('review-studio');mat.use_nodes=True
p=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
p.inputs['Base Color'].default_value=(.16,.14,.12,1);p.inputs['Roughness'].default_value=.85
floor.data.materials.append(mat)
data=bpy.data.cameras.new('review-camera');cam=bpy.data.objects.new('review-camera',data)
scene.collection.objects.link(cam);scene.camera=cam;data.type='ORTHO';data.ortho_scale=2.10
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=1000;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
for name,pos in [('three-quarter',(2.6,-4,1.65)),('front',(0,-4,1.15)),('back',(0,4,1.15))]:
    cam.location=pos;aim(cam,(0,0,.91));scene.render.filepath=OUT+'/mage-'+name+'.png'
    bpy.ops.render.render(write_still=True)
cam.location=(2.6,-4,1.65);aim(cam,(0,0,.91))
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/mage-review-studio.blend')
print('REVIEW READY',report)
