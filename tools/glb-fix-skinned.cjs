// Makes every primitive of each mesh share ONE set of vertex attributes (one vertex buffer), with
// a separate index list per primitive/material. Lossless. Works around glTFast's design-time import
// of skinned meshes whose primitives have separate vertex data (bone weight jobs race on one buffer).
// Usage: node tools/glb-fix-skinned.cjs in.glb out.glb [--strip-colors]
// --strip-colors drops COLOR_n vertex attributes (only when they change nothing: COLOR_0 all white,
// COLOR_1+ are never read by glTF materials). Extra color streams break Unity's skinned-mesh layout.
const fs = require('fs');
const [input, output] = process.argv.slice(2);
const stripColors = process.argv.includes('--strip-colors');

const buf = fs.readFileSync(input);
let offset = 12, json, bin;
while (offset < buf.length) {
	const len = buf.readUInt32LE(offset), type = buf.readUInt32LE(offset + 4);
	const data = buf.subarray(offset + 8, offset + 8 + len);
	if (type === 0x4e4f534a) json = JSON.parse(data.toString('utf8'));
	else if (type === 0x004e4942) bin = Buffer.from(data);
	offset += 8 + len;
}
const g = json;
const COMPONENTS = { SCALAR: 1, VEC2: 2, VEC3: 3, VEC4: 4, MAT4: 16 };
const BYTES = { 5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4 };
const chunks = [bin];
let binLength = bin.length;

const elementSize = (a) => COMPONENTS[a.type] * BYTES[a.componentType];

// Tightly packed bytes of an accessor (handles byteStride).
function bytesOf(index) {
	const a = g.accessors[index];
	const view = g.bufferViews[a.bufferView];
	const size = elementSize(a);
	const stride = view.byteStride || size;
	const start = (view.byteOffset || 0) + (a.byteOffset || 0);
	if (a.sparse) throw new Error('sparse accessors not supported');
	const out = Buffer.alloc(a.count * size);
	for (let i = 0; i < a.count; i++) bin.copy(out, i * size, start + i * stride, start + i * stride + size);
	return out;
}

function append(data, target) {
	const pad = (4 - (binLength % 4)) % 4;
	if (pad) { chunks.push(Buffer.alloc(pad)); binLength += pad; }
	const view = { buffer: 0, byteOffset: binLength, byteLength: data.length };
	if (target) view.target = target;
	chunks.push(data);
	binLength += data.length;
	g.bufferViews.push(view);
	return g.bufferViews.length - 1;
}

if (stripColors) {
	for (const mesh of g.meshes) for (const p of mesh.primitives) for (const k of Object.keys(p.attributes)) if (/^COLOR_\d+$/.test(k)) delete p.attributes[k];
}

let merged = 0;
for (const mesh of g.meshes) {
	const prims = mesh.primitives;
	if (prims.length < 2) continue;
	const names = Object.keys(prims[0].attributes).sort();
	const same = prims.every((p) => JSON.stringify(Object.keys(p.attributes).sort()) === JSON.stringify(names));
	if (!same) throw new Error(`mesh ${mesh.name}: primitives have different attribute sets`);
	if (prims.every((p) => names.every((n) => p.attributes[n] === prims[0].attributes[n]))) continue; // already shared

	const counts = prims.map((p) => g.accessors[p.attributes.POSITION].count);
	const total = counts.reduce((a, b) => a + b, 0);

	const newAttributes = {};
	for (const name of names) {
		const first = g.accessors[prims[0].attributes[name]];
		for (const p of prims) {
			const a = g.accessors[p.attributes[name]];
			if (a.type !== first.type || a.componentType !== first.componentType || !!a.normalized !== !!first.normalized) {
				throw new Error(`attribute ${name}: different formats across primitives`);
			}
		}
		const data = Buffer.concat(prims.map((p) => bytesOf(p.attributes[name])));
		const accessor = { bufferView: append(data, 34962), componentType: first.componentType, count: total, type: first.type };
		if (first.normalized) accessor.normalized = true;
		if (name === 'POSITION') {
			accessor.min = [0, 1, 2].map((k) => Math.min(...prims.map((p) => g.accessors[p.attributes[name]].min[k])));
			accessor.max = [0, 1, 2].map((k) => Math.max(...prims.map((p) => g.accessors[p.attributes[name]].max[k])));
		}
		g.accessors.push(accessor);
		newAttributes[name] = g.accessors.length - 1;
	}

	// Indices: offset each primitive by the vertices before it; 32-bit when needed.
	let base = 0;
	prims.forEach((p, i) => {
		const a = g.accessors[p.indices];
		const raw = bytesOf(p.indices);
		const read = a.componentType === 5125 ? (j) => raw.readUInt32LE(j * 4) : a.componentType === 5123 ? (j) => raw.readUInt16LE(j * 2) : (j) => raw[j];
		const wide = total > 65535;
		const out = Buffer.alloc(a.count * (wide ? 4 : 2));
		for (let j = 0; j < a.count; j++) {
			const v = read(j) + base;
			if (wide) out.writeUInt32LE(v, j * 4); else out.writeUInt16LE(v, j * 2);
		}
		g.accessors.push({ bufferView: append(out, 34963), componentType: wide ? 5125 : 5123, count: a.count, type: 'SCALAR' });
		p.indices = g.accessors.length - 1;
		p.attributes = { ...newAttributes };
		base += counts[i];
	});
	merged++;
}

// Morph targets would need the same treatment; refuse rather than break them.
if (g.meshes.some((m) => m.primitives.some((p) => p.targets))) throw new Error('morph targets not supported');

// Compact: keep only accessors and buffer views something still uses.
{
	const used = new Set();
	const mark = (i) => { if (i !== undefined) used.add(i); };
	for (const m of g.meshes) for (const p of m.primitives) { Object.values(p.attributes).forEach(mark); mark(p.indices); }
	for (const s of g.skins || []) mark(s.inverseBindMatrices);
	for (const a of g.animations || []) for (const s of a.samplers) { mark(s.input); mark(s.output); }
	const accessorMap = new Map();
	const accessors = [];
	g.accessors.forEach((a, i) => { if (used.has(i)) { accessorMap.set(i, accessors.length); accessors.push(a); } });

	const usedViews = new Set(accessors.map((a) => a.bufferView));
	for (const img of g.images || []) if (img.bufferView !== undefined) usedViews.add(img.bufferView);
	const full = Buffer.concat(chunks);
	const viewMap = new Map();
	const views = [];
	const parts = [];
	let length = 0;
	g.bufferViews.forEach((v, i) => {
		if (!usedViews.has(i)) return;
		const pad = (4 - (length % 4)) % 4;
		if (pad) { parts.push(Buffer.alloc(pad)); length += pad; }
		parts.push(full.subarray(v.byteOffset || 0, (v.byteOffset || 0) + v.byteLength));
		viewMap.set(i, views.length);
		views.push({ ...v, byteOffset: length });
		length += v.byteLength;
	});

	accessors.forEach((a) => { a.bufferView = viewMap.get(a.bufferView); });
	for (const img of g.images || []) if (img.bufferView !== undefined) img.bufferView = viewMap.get(img.bufferView);
	const remap = (i) => (i === undefined ? undefined : accessorMap.get(i));
	for (const m of g.meshes) for (const p of m.primitives) {
		for (const k of Object.keys(p.attributes)) p.attributes[k] = remap(p.attributes[k]);
		if (p.indices !== undefined) p.indices = remap(p.indices);
	}
	for (const s of g.skins || []) if (s.inverseBindMatrices !== undefined) s.inverseBindMatrices = remap(s.inverseBindMatrices);
	for (const a of g.animations || []) for (const s of a.samplers) { s.input = remap(s.input); s.output = remap(s.output); }
	g.accessors = accessors;
	g.bufferViews = views;
	chunks.length = 0;
	chunks.push(...parts);
}

const newBin = Buffer.concat(chunks);
const padBin = Buffer.concat([newBin, Buffer.alloc((4 - (newBin.length % 4)) % 4)]);
g.buffers[0].byteLength = padBin.length;
const text = Buffer.from(JSON.stringify(g));
const padJson = Buffer.concat([text, Buffer.alloc((4 - (text.length % 4)) % 4, 0x20)]);
const header = Buffer.alloc(12);
header.writeUInt32LE(0x46546c67, 0);
header.writeUInt32LE(2, 4);
header.writeUInt32LE(12 + 8 + padJson.length + 8 + padBin.length, 8);
const chunk = (type, data) => { const h = Buffer.alloc(8); h.writeUInt32LE(data.length, 0); h.writeUInt32LE(type, 4); return Buffer.concat([h, data]); };
fs.writeFileSync(output, Buffer.concat([header, chunk(0x4e4f534a, padJson), chunk(0x004e4942, padBin)]));
console.log(`merged ${merged} mesh(es); ${output}: ${fs.statSync(output).size} bytes`);
