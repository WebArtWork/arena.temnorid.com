const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const validator = require(path.join(root, 'output/mage/validation/node_modules/gltf-validator'));
const specs = JSON.parse(fs.readFileSync(path.join(root, 'output/arena/build-report.json')));
const results = [];
async function main() {
  for (const name of [...specs.map(s => s.name), 'mage']) {
    const mage = name === 'mage';
    const bytes = fs.readFileSync(path.join(root, 'Assets/Models', mage ? 'characters' : 'arena', name + '.glb'));
    const j = JSON.parse(bytes.subarray(20, 20 + bytes.readUInt32LE(12)));
    const validation = await validator.validateBytes(new Uint8Array(bytes), {uri:name+'.glb', maxIssues:100});
    assert.equal(validation.issues.numErrors, 0, name + ': glTF errors');
    assert.equal(validation.issues.numWarnings, 0, name + ': glTF warnings');
    assert.equal(j.cameras?.length ?? 0, 0, name + ': exported cameras');
    assert(!j.extensions?.KHR_lights_punctual, name + ': exported lights');
    const lower = [Infinity,Infinity,Infinity], upper = [-Infinity,-Infinity,-Infinity];
    let triangles = 0;
    for (const m of j.meshes) for (const p of m.primitives) {
      assert(!Object.keys(p.attributes).some(k => k.startsWith('COLOR_')), name + ': vertex colors');
      const a = j.accessors[p.attributes.POSITION];
      for (let i=0;i<3;i++) {lower[i]=Math.min(lower[i],a.min[i]);upper[i]=Math.max(upper[i],a.max[i]);}
      triangles += j.accessors[p.indices].count / 3;
    }
    if (mage) {
      assert(triangles <= 25000, 'mage triangle budget');
      assert.equal(j.skins.length, 1);assert.equal(j.skins[0].joints.length, 52);
      assert.equal(j.animations.length, 17);
      assert(j.materials.some(m => m.name.startsWith('accent')), 'mage accent');
      assert.equal(j.materials.length, 2, 'mage material budget');
      for (const m of j.meshes) {
        const first = m.primitives[0].attributes;
        assert(m.primitives.every(p => Object.entries(first).every(([k,v]) => p.attributes[k] === v)), 'shared skin vertex buffers');
      }
      assert(Math.abs(upper[1] - lower[1] - 1.8) < .001, 'mage height');
      assert(upper[0] - lower[0] <= .701 && upper[2] - lower[2] <= .501, 'mage envelope');
    } else {
      const spec = specs.find(s => s.name === name);
      assert(triangles >= spec.budget[0] && triangles <= spec.budget[1], name + ': triangle budget');
      for (let i=0;i<3;i++) assert(Math.abs(upper[i]-lower[i]-spec.size_xyz_gltf[i])<.0001, name + ': dimensions '+i);
      assert(j.materials.length <= (name === 'arena-banner' ? 2 : 1), name + ': material budget');
      if (name === 'arena-banner') assert(j.materials.some(m => m.name.startsWith('accent')), 'banner accent');
    }
    assert(Math.abs(lower[1])<.0001, name + ': ground pivot');
    for (const image of j.images ?? []) assert(image.bufferView !== undefined && !image.uri, name + ': embedded textures');
    results.push({name,triangles,bytes:bytes.length,materials:j.materials.length,bounds:[lower,upper],issues:validation.issues});
  }
  fs.writeFileSync(path.join(root,'output/realism/gltf-validation.json'),JSON.stringify(results,null,2));
  console.log(JSON.stringify(results.map(({name,triangles,materials,issues})=>({name,triangles,materials,errors:issues.numErrors,warnings:issues.numWarnings})),null,2));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
