"""Rest pose, weights, bones and deformation smoke review of the FINAL game GLB."""
import bpy,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(ROOT/'Assets'/'Models'/'characters'/'mage.glb'))
rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers)]
assert len(rigs)==1 and len(meshes)==1
rig,model=rigs[0],meshes[0]
assert len(rig.data.bones)==52
assert len(bpy.data.actions)==17
assert all(v.groups and sum(g.weight for g in v.groups)>.99 for v in model.data.vertices),'Unweighted or unnormalized vertices'
assert len(model.data.color_attributes)==0
assert sum(len(p.vertices)-2 for p in model.data.polygons)<=25000
rig.animation_data.action=None
for track in rig.animation_data.nla_tracks:track.mute=True
for pb in rig.pose.bones:
    pb.rotation_mode='QUATERNION';pb.rotation_quaternion=(1,0,0,0);pb.location=(0,0,0)
bpy.context.view_layer.update()
dg=bpy.context.evaluated_depsgraph_get();evaluated=model.evaluated_get(dg)
points=[evaluated.matrix_world@v.co for v in evaluated.data.vertices]
bounds=[[min(v[i] for v in points),max(v[i] for v in points)] for i in range(3)]
assert abs(bounds[2][1]-1.8)<.001 and abs(bounds[2][0])<.001,bounds
assert bounds[0][1]-bounds[0][0]<=.701 and bounds[1][1]-bounds[1][0]<=.501,bounds
samples=[]
for action in bpy.data.actions:
    rig.animation_data.action=action
    if action.slots:rig.animation_data.action_slot=action.slots[0]
    lo,hi=action.frame_range
    for fraction in [0,.25,.5,.75,1]:
        bpy.context.scene.frame_set(round(lo+(hi-lo)*fraction))
        dg=bpy.context.evaluated_depsgraph_get();evaluated=model.evaluated_get(dg)
        coords=[evaluated.matrix_world@v.co for v in evaluated.data.vertices]
        assert all(math.isfinite(c) for v in coords for c in v),(action.name,fraction)
        assert max(v.length for v in coords)<4,(action.name,'exploding skin')
    samples.append(action.name)
report={'status':'PASS','bones':52,'clips':samples,'sampled_poses':len(samples)*5,'unweighted_vertices':0,'rest_bounds_blender':bounds,'triangles':sum(len(p.vertices)-2 for p in model.data.polygons)}
(ROOT/'output'/'realism'/'mage-import-check.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report),flush=True)
