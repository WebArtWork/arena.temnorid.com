"""Round-trip GLB importer and walkable-surface smoke checks."""
import bpy, os, json
from mathutils import Vector
root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
specs=json.load(open(os.path.join(root,'output/arena/build-report.json')))
results=[]
for spec in specs:
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=os.path.join(root,'Assets/Models/arena',spec['name']+'.glb'))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert len(meshes)==1,(spec['name'],len(meshes))
    ob=meshes[0]
    coords=[ob.matrix_world @ v.co for v in ob.data.vertices]
    bounds=[[min(v[i] for v in coords),max(v[i] for v in coords)] for i in range(3)]
    w,h,d=spec['size_xyz_gltf']
    for i,size in enumerate([w,d,h]):assert abs(bounds[i][1]-bounds[i][0]-size)<.0001,(spec['name'],bounds)
    assert abs(bounds[2][0])<.0001
    surface=[]
    def height(x,y):
        result=ob.ray_cast(Vector((x,y,10)),Vector((0,0,-1)))
        assert result[0],(spec['name'],'missing walkable surface',x,y)
        return result[1].z
    if spec['name'] in ['arena-crate','arena-platform']:
        for x in [-w*.45,0,w*.45]:
            for y in [-d*.45,0,d*.45]:
                z=height(x,y);assert abs(z-h)<.002,(spec['name'],z);surface.append(z)
    if spec['name']=='arena-stairs':
        for step in range(4):
            z=height(0,-.9+step*.6);assert abs(z-(step+1)*.3)<.0001,z;surface.append(z)
    if spec['name']=='arena-brazier':
        flame=next(o for o in bpy.context.scene.objects if o.name.startswith('flame'))
        assert flame.parent and abs(flame.matrix_world.translation.z-1.25)<.0001
    results.append({'name':spec['name'],'roundtrip':'PASS','bounds_blender':bounds,'surface_heights':surface})
with open(os.path.join(root,'output/arena/import-check.json'),'w') as f:json.dump(results,f,indent=2)
print('All eleven GLBs re-imported; bounds, walkable tops, step heights and flame attachment passed.')
