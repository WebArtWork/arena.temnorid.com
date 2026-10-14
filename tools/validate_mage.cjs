const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const validator = require(path.join(root, 'output/mage/validation/node_modules/gltf-validator'));
const file = path.join(root, 'Assets/Models/characters/mage.glb');
const bytes = fs.readFileSync(file);
validator.validateBytes(new Uint8Array(bytes), {uri: 'mage.glb', maxIssues: 100}).then(report => {
  fs.writeFileSync(path.join(root,'output/mage/gltf-validation.json'), JSON.stringify(report,null,2));
  console.log(JSON.stringify(report.issues,null,2));
  if (report.issues.numErrors) process.exitCode = 1;
});
