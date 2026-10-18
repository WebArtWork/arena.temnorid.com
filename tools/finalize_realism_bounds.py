"""Clamp sub-centimeter weathering to the authored collision envelopes, then re-export."""
import bpy,json,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'output'/'realism'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'models.blend'))
scene=bpy.context.scene
def rest_pose():
    rig=bpy.data.objects['mage'];rig.animation_data.action=None
    for track in rig.animation_data.nla_tracks:track.mute=True
    for pb in rig.pose.bones:
        pb.location=(0,0,0);pb.rotation_euler=(0,0,0);pb.rotation_quaternion=(1,0,0,0);pb.scale=(1,1,1)
    scene.frame_set(1);bpy.context.view_layer.update()
rest_pose()
if '--replace-banner' in sys.argv:
    old=bpy.data.objects['arena-banner'];bpy.data.objects.remove(old,do_unlink=True)
    with bpy.data.libraries.load(str(OUT/'models-arena-banner.blend'),link=False) as (source,target):
        target.objects=['arena-banner']
    banner=target.objects[0];scene.collection.objects.link(banner);banner.name='arena-banner'
report=json.loads((OUT/'build-report.json').read_text())
for item in report:
    mage=item['name']=='mage'
    ob=bpy.data.objects['mage-mesh' if mage else item['name']]
    original=item['before_bounds']
    for v in ob.data.vertices:
        for axis in range(3):v.co[axis]=min(original[axis][1],max(original[axis][0],v.co[axis]))
    if mage:
        lo=min(v.co.z for v in ob.data.vertices);hi=max(v.co.z for v in ob.data.vertices)
        for v in ob.data.vertices:v.co.z=(v.co.z-lo)*1.8/(hi-lo)
    ob.data.update()
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
    if mage:
        rig=bpy.data.objects['mage'];rig.select_set(True);bpy.context.view_layer.objects.active=rig
    for child in ob.children:child.select_set(True)
    path=ROOT/'Assets'/'Models'/('characters' if mage else 'arena')/(item['name']+'.glb')
    bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,
        export_animations=mage,export_animation_mode='NLA_TRACKS',export_force_sampling=True,
        export_frame_range=False,export_yup=True,export_materials='EXPORT',export_skins=True,
        export_tangents=True,export_vertex_color='NONE',export_all_vertex_colors=False)
    item['bounds_blender']=[[min(v.co[a] for v in ob.data.vertices),max(v.co[a] for v in ob.data.vertices)] for a in range(3)]
    item['bytes']=path.stat().st_size
    print('FINALIZED',item['name'],item['bounds_blender'],flush=True)
rest_pose()
bpy.data.orphans_purge(do_recursive=True)
# Keep each model at its game pivot but make the source easy to navigate without overlapping assets.
for item in report:
    ob=bpy.data.objects['mage-mesh' if item['name']=='mage' else item['name']]
    collection=bpy.data.collections.new('Asset | '+item['name']);scene.collection.children.link(collection)
    members=[ob]+list(ob.children)
    if item['name']=='mage':members.append(bpy.data.objects['mage'])
    for member in members:
        for old in list(member.users_collection):old.objects.unlink(member)
        collection.objects.link(member)
    collection.hide_viewport=item['name']!='mage'
    collection.hide_render=item['name']!='mage'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'models.blend'))
(OUT/'build-report.json').write_text(json.dumps(report,indent=2))
