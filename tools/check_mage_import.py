"""Blender glTF round-trip smoke check (run in background)."""
import bpy, os, json
root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=os.path.join(root,'Assets/Models/characters/mage.glb'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers)]
rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
assert len(meshes)==1 and len(rigs)==1
assert len(rigs[0].data.bones)==52
assert len(bpy.data.actions)==17
rig=rigs[0]
rig.animation_data_clear()
for pb in rig.pose.bones:
    pb.rotation_mode='QUATERNION';pb.rotation_quaternion=(1,0,0,0);pb.location=(0,0,0)
bpy.context.view_layer.update()
dg=bpy.context.evaluated_depsgraph_get()
ob=meshes[0].evaluated_get(dg)
points=[ob.matrix_world @ v.co for v in ob.data.vertices]
bounds=[[min(v[i] for v in points),max(v[i] for v in points)] for i in range(3)]
assert abs(bounds[2][1]-1.8)<.001, bounds
assert bounds[2][0]>=-.001, bounds
assert bounds[0][1]-bounds[0][0]<=.7, bounds
assert bounds[1][1]-bounds[1][0]<=.5, bounds
report={'roundtrip':'PASS','mesh_count':1,'bones':52,'clips':len(bpy.data.actions),
        'rest_bounds_blender':bounds,'materials':[m.name for m in meshes[0].data.materials]}
with open(os.path.join(root,'output/mage/import-check.json'),'w') as f:json.dump(report,f,indent=2)
print(json.dumps(report))
