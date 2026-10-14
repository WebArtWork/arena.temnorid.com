const fs=require('node:fs');
const path=require('node:path');
const assert=require('node:assert/strict');
const root=path.resolve(__dirname,'..');
const validator=require(path.join(root,'output/mage/validation/node_modules/gltf-validator'));
const specs=JSON.parse(fs.readFileSync(path.join(root,'output/arena/build-report.json')));
async function main(){
  const reports=[];
  for(const spec of specs){
    const file=path.join(root,'Assets/Models/arena',spec.name+'.glb');
    const bytes=fs.readFileSync(file);
    const json=JSON.parse(bytes.subarray(20,20+bytes.readUInt32LE(12)).toString());
    const validation=await validator.validateBytes(new Uint8Array(bytes),{uri:spec.name+'.glb',maxIssues:100});
    assert.equal(validation.issues.numErrors,0,spec.name+' validation errors');
    assert.equal(validation.issues.numWarnings,0,spec.name+' validation warnings');
    assert.equal(json.cameras?.length??0,0);
    assert(!json.extensions?.KHR_lights_punctual);
    let triangles=0;const lower=[Infinity,Infinity,Infinity],upper=[-Infinity,-Infinity,-Infinity];
    for(const m of json.meshes)for(const primitive of m.primitives){
      const acc=json.accessors[primitive.attributes.POSITION];
      for(let i=0;i<3;i++){lower[i]=Math.min(lower[i],acc.min[i]);upper[i]=Math.max(upper[i],acc.max[i]);}
      triangles+=(primitive.indices===undefined?acc.count:json.accessors[primitive.indices].count)/3;
    }
    assert(triangles>=spec.budget[0]&&triangles<=spec.budget[1],spec.name+' budget');
    for(let i=0;i<3;i++)assert(Math.abs(upper[i]-lower[i]-spec.size_xyz_gltf[i])<.0001,spec.name+' dimensions '+i);
    assert(Math.abs(lower[1])<.0001,spec.name+' ground pivot');
    assert(Math.abs(upper[0]+lower[0])<.0001&&Math.abs(upper[2]+lower[2])<.0001,spec.name+' centered pivot');
    for(const img of json.images??[])assert(img.bufferView!==undefined&&!img.uri,'embedded image');
    for(const idx of json.scenes[json.scene??0].nodes){
      const node=json.nodes[idx];
      assert(!node.matrix&&!node.rotation&&!node.scale&&!node.translation,spec.name+' applied root transform');
    }
    if(spec.name==='arena-brazier'){
      const index=json.nodes.findIndex(n=>n.name==='flame');assert(index>=0,'flame node missing');
      assert(json.nodes.some(n=>n.children?.includes(index)),'flame must be a child');
      assert(Math.abs(json.nodes[index].translation[1]-1.25)<.001,'flame height');
    }
    if(spec.name==='arena-banner')assert(json.materials.some(m=>m.name==='accent'));
    if(spec.name==='arena-spawn-circle')assert(json.materials[0].alphaMode==='BLEND'||json.materials[0].alphaMode==='MASK');
    reports.push({name:spec.name,triangles,bounds_gltf:[lower,upper],bytes:bytes.length,issues:validation.issues});
  }
  fs.writeFileSync(path.join(root,'output/arena/gltf-validation.json'),JSON.stringify(reports,null,2));
  console.log(JSON.stringify(reports.map(x=>({name:x.name,triangles:x.triangles,errors:x.issues.numErrors,warnings:x.issues.numWarnings})),null,2));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
