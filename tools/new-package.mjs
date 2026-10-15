#!/usr/bin/env node
// Scaffolds (or completes) a Unity package in this workspace and, with --project, registers it in
// that Unity project's Packages/manifest.json (e.g. --project temnorid-arena).
// Safe to re-run: existing files are never overwritten.
//
// Usage:
//   node tools/new-package.mjs <folder> --asm <AsmBaseName> --display "<Display Name>"
//     [--description "<text>"] [--deps 3d-unity-core,3d-unity-scene]
//     [--unity-deps com.unity.inputsystem@1.20.0] [--asm-refs Unity.InputSystem]
//     [--project temnorid-arena] [--package-id com.itkamianets.<folder>]
//     [--kind engine|constructor|game-package]   (workspace.json group; guessed from the name)
//
// Example:
//   node tools/new-package.mjs 3d-unity-input --asm ITKamianets.Engine.Input \
//     --display "Unity Input" --deps 3d-unity-core \
//     --unity-deps com.unity.inputsystem@1.20.0 --asm-refs Unity.InputSystem

import { existsSync, mkdirSync, readFileSync, writeFileSync, copyFileSync, readdirSync } from 'node:fs';
import { join, dirname, resolve, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execSync } from 'node:child_process';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const REFERENCE_PACKAGE = join(ROOT, '3d-unity-core'); // LICENSE and .gitignore are copied from here
const UNITY_VERSION = '6000.6';
const PACKAGE_PREFIX = 'com.itkamianets.';

const args = process.argv.slice(2);
const folder = args[0];
const opt = (name, fallback = '') => {
	const i = args.indexOf(`--${name}`);
	return i > 0 ? args[i + 1] : fallback;
};
const list = (name) => opt(name).split(',').map((s) => s.trim()).filter(Boolean);

if (!folder || folder.startsWith('--') || !opt('asm') || !opt('display')) {
	console.error('Usage: node tools/new-package.mjs <folder> --asm <AsmBaseName> --display "<Display Name>" [--description ..] [--deps ..] [--unity-deps ..] [--asm-refs ..] [--project ..]');
	process.exit(2);
}

const asm = opt('asm');
const dir = join(ROOT, folder);
const created = [];

const writeIfMissing = (path, content) => {
	if (existsSync(path)) return;
	mkdirSync(dirname(path), { recursive: true });
	writeFileSync(path, typeof content === 'string' ? content : JSON.stringify(content, null, 4) + '\n');
	created.push(path.slice(ROOT.length + 1));
};

// Resolve workspace dependencies: their package id, version and Runtime assembly name.
const deps = list('deps').map((depFolder) => {
	const pkg = JSON.parse(readFileSync(join(ROOT, depFolder, 'package.json'), 'utf8'));
	const runtimeAsm = readdirSync(join(ROOT, depFolder, 'Runtime')).find((f) => f.endsWith('.asmdef'));
	if (!runtimeAsm) throw new Error(`${depFolder} has no Runtime asmdef`);
	return { id: pkg.name, version: pkg.version, asm: runtimeAsm.replace(/\.asmdef$/, '') };
});

const dependencies = {};
for (const d of deps) dependencies[d.id] = d.version;
for (const u of list('unity-deps')) {
	const [id, version] = u.split('@');
	dependencies[id] = version;
}

const packageId = opt('package-id', PACKAGE_PREFIX + folder);

writeIfMissing(join(dir, 'package.json'), JSON.stringify({
	name: packageId,
	version: '0.1.0',
	displayName: `IT-Kamianets 3D Engine: ${opt('display')}`,
	description: opt('description', `${opt('display')} package of the IT-Kamianets 3D Engine.`),
	unity: UNITY_VERSION,
	license: 'MIT',
	author: { name: 'IT Kamianets' },
	keywords: ['3d-engine', 'it-kamianets'],
	dependencies,
}, null, 2) + '\n');

const asmdef = (name, references, extra = {}) => ({
	name,
	rootNamespace: name,
	references,
	includePlatforms: [],
	excludePlatforms: [],
	allowUnsafeCode: false,
	overrideReferences: false,
	precompiledReferences: [],
	autoReferenced: true,
	defineConstraints: [],
	versionDefines: [],
	noEngineReferences: false,
	...extra,
});
const testExtra = (platforms) => ({
	includePlatforms: platforms,
	overrideReferences: true,
	precompiledReferences: ['nunit.framework.dll'],
	defineConstraints: ['UNITY_INCLUDE_TESTS'],
});
const runtimeRefs = [...deps.map((d) => d.asm), ...list('asm-refs')];

writeIfMissing(join(dir, 'Runtime', `${asm}.asmdef`), asmdef(asm, runtimeRefs));
writeIfMissing(join(dir, 'Editor', `${asm}.Editor.asmdef`), asmdef(`${asm}.Editor`, [asm], { includePlatforms: ['Editor'] }));
writeIfMissing(join(dir, 'Tests', 'Runtime', `${asm}.Tests.asmdef`),
	asmdef(`${asm}.Tests`, [asm, 'UnityEngine.TestRunner', 'UnityEditor.TestRunner'], testExtra([])));
writeIfMissing(join(dir, 'Tests', 'Editor', `${asm}.Editor.Tests.asmdef`),
	asmdef(`${asm}.Editor.Tests`, [asm, `${asm}.Editor`, 'UnityEngine.TestRunner', 'UnityEditor.TestRunner'], testExtra(['Editor'])));

writeIfMissing(join(dir, 'CHANGELOG.md'), `# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial Unity package structure (\`Runtime\`, \`Editor\`, \`Tests\` assemblies).
`);

writeIfMissing(join(dir, 'README.md'), `# IT-Kamianets 3D Engine: ${opt('display')}

\`${folder}\` ...

### Logic

### Status

Package skeleton only.

### Roadmap
`);

for (const file of ['LICENSE', '.gitignore']) {
	if (!existsSync(join(dir, file))) {
		copyFileSync(join(REFERENCE_PACKAGE, file), join(dir, file));
		created.push(`${folder}/${file}`);
	}
}

if (!existsSync(join(dir, '.git'))) {
	execSync('git init -q -b main', { cwd: dir });
	created.push(`${folder}/.git`);
}

// Register in workspace.json, so tools/setup.mjs and agents know the repo exists.
const registryPath = join(ROOT, 'workspace.json');
if (existsSync(registryPath)) {
	const registry = JSON.parse(readFileSync(registryPath, 'utf8'));
	if (!registry.repos.some((r) => r.name === folder)) {
		const kind = opt('kind') ||
			(folder.startsWith('3d-unity-') ? 'engine'
				: folder.startsWith('3d-constructor-') ? 'constructor'
					: opt('project') ? 'game-package' : 'engine');
		// Insert after the last repo of the same kind, so the list stays grouped.
		const last = registry.repos.map((r) => r.kind).lastIndexOf(kind);
		const entry = { name: folder, kind, description: opt('description', opt('display')) };
		registry.repos.splice(last === -1 ? registry.repos.length : last + 1, 0, entry);
		writeFileSync(registryPath, JSON.stringify(registry, null, '\t') + '\n');
		created.push(`workspace.json entry ${folder} (${kind})`);
	}
}

// Register in the Unity project that uses it, so it compiles there from its own folder.
if (opt('project')) {
	// The project's folder comes from workspace.json (`path`; the game is the workspace root, ".").
	const projectEntry = JSON.parse(readFileSync(join(ROOT, 'workspace.json'), 'utf8')).repos.find((r) => r.name === opt('project'));
	const projectDir = join(ROOT, projectEntry?.path ?? opt('project'));
	const manifestPath = join(projectDir, 'Packages', 'manifest.json');
	const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
	if (!manifest.dependencies[packageId]) {
		manifest.dependencies[packageId] = `file:${relative(join(projectDir, 'Packages'), join(ROOT, folder)).replaceAll('\\', '/')}`;
		manifest.dependencies = Object.fromEntries(Object.entries(manifest.dependencies).sort(([a], [b]) => a.localeCompare(b)));
		writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n');
		created.push(`${opt('project')} manifest entry ${packageId}`);
	}
}

console.log(created.length ? `Created:\n  ${created.join('\n  ')}` : `${folder}: nothing to do, already complete.`);
