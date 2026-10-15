// Draws Temnorid Arena's placeholder app icon: a glowing fireball inside a rune ring on a dark
// stone background. 1024x1024 RGB PNG without transparency (the App Store rejects alpha).
// Replace with real art when it exists (see models-to-do.md).
// Usage: node Tools~/make-placeholder-icon.cjs Assets/Branding/AppIcon.png
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

const out = process.argv[2];
const size = 1024;
const c = size / 2;

const mix = (a, b, t) => a + (b - a) * t;
const clamp = (v) => Math.max(0, Math.min(1, v));
const smooth = (e0, e1, x) => { const t = clamp((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); };

const rows = [];
for (let y = 0; y < size; y++) {
  const row = Buffer.alloc(1 + size * 3);
  row[0] = 0; // no filter
  for (let x = 0; x < size; x++) {
    const dx = x - c, dy = y - c;
    const r = Math.sqrt(dx * dx + dy * dy) / c; // 0 centre .. ~1.41 corner
    const angle = Math.atan2(dy, dx);

    // Background: dark blue-grey stone, lighter towards the centre.
    let R = mix(0.16, 0.05, clamp(r / 1.2)), G = mix(0.17, 0.06, clamp(r / 1.2)), B = mix(0.24, 0.1, clamp(r / 1.2));

    // Rune ring: a thin band with twelve notches.
    const ring = smooth(0.035, 0.0, Math.abs(r - 0.72));
    const notches = 0.5 + 0.5 * Math.cos(angle * 12);
    const ringGlow = ring * mix(0.55, 1, notches);
    R = mix(R, 1.0, ringGlow); G = mix(G, 0.62, ringGlow); B = mix(B, 0.2, ringGlow);

    // Fireball: hot core, orange body, soft glow.
    const glow = Math.exp(-Math.pow(r / 0.42, 2)) * 0.6;
    const body = smooth(0.42, 0.36, r);
    const core = smooth(0.2, 0.05, r);
    R = mix(R, 1.0, clamp(glow + body)); G = mix(G, 0.45, clamp(glow + body)); B = mix(B, 0.1, clamp(glow + body));
    R = mix(R, 1.0, core); G = mix(G, 0.92, core); B = mix(B, 0.7, core);

    const i = 1 + x * 3;
    row[i] = Math.round(clamp(R) * 255);
    row[i + 1] = Math.round(clamp(G) * 255);
    row[i + 2] = Math.round(clamp(B) * 255);
  }
  rows.push(row);
}

const crcTable = new Int32Array(256).map((_, n) => { let k = n; for (let i = 0; i < 8; i++) k = k & 1 ? 0xedb88320 ^ (k >>> 1) : k >>> 1; return k; });
const crc32 = (buf) => { let k = -1; for (const b of buf) k = crcTable[(k ^ b) & 0xff] ^ (k >>> 8); return (k ^ -1) >>> 0; };
const chunk = (type, data) => {
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
  const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(body));
  return Buffer.concat([len, body, crc]);
};
const header = Buffer.alloc(13);
header.writeUInt32BE(size, 0); header.writeUInt32BE(size, 4);
header[8] = 8; header[9] = 2; // 8-bit RGB
const png = Buffer.concat([
  Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
  chunk('IHDR', header),
  chunk('IDAT', zlib.deflateSync(Buffer.concat(rows), { level: 9 })),
  chunk('IEND', Buffer.alloc(0)),
]);
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, png);
console.log(out, png.length, 'bytes');
