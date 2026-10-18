import bpy,importlib.util
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
spec=importlib.util.spec_from_file_location('realism_helpers',ROOT/'tools'/'refine_models_realism.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
model=bpy.data.objects['mage-mesh'];rig=bpy.data.objects['mage']
rig.animation_data.action=None
for track in rig.animation_data.nla_tracks:track.mute=True
for pb in rig.pose.bones:
    pb.location=(0,0,0);pb.rotation_euler=(0,0,0);pb.rotation_quaternion=(1,0,0,0);pb.scale=(1,1,1)
bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
module.render(model,'mage',rig)
