// Finish generated GLBs: valid tangents for degenerate UV corners and explicit default team accents.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const reportPath = path.join(root, 'output/realism/build-report.json');
const report = JSON.parse(fs.readFileSync(reportPath));
for (const item of report) {
  const file = path.join(root, 'Assets/Models', item.name === 'mage' ? 'characters' : 'arena', item.name + '.glb');
  const input = fs.readFileSync(file);
  let j, bin;
  for (let off = 12; off < input.length;) {
    const n = input.readUInt32LE(off), kind = input.readUInt32LE(off + 4);
    const data = input.subarray(off + 8, off + 8 + n);
    if (kind === 0x4e4f534a) j = JSON.parse(data);
    if (kind === 0x004e4942) bin = Buffer.from(data);
    off += 8 + n;
  }
  let repaired = 0;
  const seen = new Set();
  for (const mesh of j.meshes) for (const primitive of mesh.primitives) {
    const index = primitive.attributes.TANGENT;
    if (index === undefined || seen.has(index)) continue;
    seen.add(index);
    const a = j.accessors[index], av = j.bufferViews[a.bufferView];
    const n = j.accessors[primitive.attributes.NORMAL], nv = j.bufferViews[n.bufferView];
    if (a.componentType !== 5126 || n.componentType !== 5126) throw new Error('Expected float tangents/normals');
    for (let i=0;i<a.count;i++) {
      const pos = (av.byteOffset || 0) + (a.byteOffset || 0) + i * (av.byteStride || 16);
      const t = [0,1,2].map(k => bin.readFloatLE(pos + k * 4));
      if (Math.hypot(...t) > 0.5) continue;
      const normalPos = (nv.byteOffset || 0) + (n.byteOffset || 0) + i * (nv.byteStride || 12);
      const normal = [0,1,2].map(k => bin.readFloatLE(normalPos + k * 4));
      const tangent = Math.abs(normal[1]) < .9 ? [normal[2],0,-normal[0]] : [0,-normal[2],normal[1]];
      const length = Math.hypot(...tangent);
      if (length < .0001) throw new Error('Invalid normal at degenerate tangent');
      for (let k=0;k<3;k++) bin.writeFloatLE(tangent[k]/length,pos+k*4);
      bin.writeFloatLE(1,pos+12);repaired++;
    }
  }
  let changedAccent = false;
  for (const material of j.materials) if (material.name.startsWith('accent')) {
    const factor = item.name === 'mage' ? [.045,.025,.095,1] : [.12,.075,.25,1];
    changedAccent ||= material.name !== 'accent' || JSON.stringify(material.pbrMetallicRoughness.baseColorFactor) !== JSON.stringify(factor);
    material.name = 'accent';
    material.pbrMetallicRoughness.baseColorFactor = factor;
  }
  const json = Buffer.from(JSON.stringify(j));
  const jsonPadded = Buffer.concat([json,Buffer.alloc((4-json.length%4)%4,0x20)]);
  const binPadded = Buffer.concat([bin,Buffer.alloc((4-bin.length%4)%4)]);
  const header = Buffer.alloc(12);header.writeUInt32LE(0x46546c67,0);header.writeUInt32LE(2,4);
  header.writeUInt32LE(12+8+jsonPadded.length+8+binPadded.length,8);
  const chunk = (type, bytes) => {const h=Buffer.alloc(8);h.writeUInt32LE(bytes.length,0);h.writeUInt32LE(type,4);return Buffer.concat([h,bytes]);};
  if (repaired || changedAccent) fs.writeFileSync(file,Buffer.concat([header,chunk(0x4e4f534a,jsonPadded),chunk(0x004e4942,binPadded)]));
  item.bytes = fs.statSync(file).size;item.repaired_degenerate_tangents = repaired;
  item.materials = j.materials.map(m=>m.name);
  console.log(item.name + ': repaired ' + repaired + ' degenerate tangents; ' + item.materials.join(', '));
}
fs.writeFileSync(reportPath,JSON.stringify(report,null,2));
