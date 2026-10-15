#!/usr/bin/env node
// Clones a repo of the workspace and every repo it needs into this folder. The workspace root is the
// Temnorid Arena Unity project itself (`path: "."` in workspace.json); every other repo is a folder
// inside it (git-ignored by the game), so `file:../<repo>` paths in its manifest resolve. Dependencies are read from the repos themselves
// (package.json and Packages/manifest.json), plus `requires` in workspace.json, so nothing is
// listed twice. Repos already on disk are left alone unless --pull is given.
//
// Usage:
//   node tools/setup.mjs temnorid-arena        a game: its project, game packages, engine, schemas
//   node tools/setup.mjs 3d-unity-network      one package and what it needs
//   node tools/setup.mjs --all                 every repo in workspace.json
//   node tools/setup.mjs --list                every repo, whether it is cloned, what it is
//   node tools/setup.mjs temnorid-arena --pull also fast-forward repos already on disk
//   node tools/setup.mjs temnorid-arena --dry  show the plan, change nothing
//
// Clones over HTTPS from each repo's GitHub org (`org` in workspace.json, else the top-level one).
// TEMNORID_GIT_BASE changes where from: a template with {org} and {name}, e.g.
// git@github.com:{org}/{name}.git for SSH or /path/to/other/workspace/{name} for local copies.

import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const registry = JSON.parse(readFileSync(join(ROOT, 'workspace.json'), 'utf8'));
const byName = new Map(registry.repos.map((r) => [r.name, r]));
const gitBase = process.env.TEMNORID_GIT_BASE ?? 'https://github.com/{org}/{name}.git';
const orgOf = (name) => byName.get(name)?.org ?? registry.org;
const cloneUrl = (name) => {
	const url = gitBase.replaceAll('{org}', orgOf(name));
	return url.includes('{name}') ? url.replaceAll('{name}', name) : `${url}${name}.git`;
};

const args = process.argv.slice(2);
const flag = (name) => args.includes(`--${name}`);
const targets = args.filter((a) => !a.startsWith('--'));

/** Where a repo lives: its `path` in workspace.json, else a folder named after it. */
const dirOf = (name) => join(ROOT, byName.get(name)?.path ?? name);
const isCloned = (name) => existsSync(join(dirOf(name), '.git'));
const readJson = (path) => (existsSync(path) ? JSON.parse(readFileSync(path, 'utf8')) : null);

/** Repos a cloned repo needs: its Unity dependencies with our package prefix, plus `requires`. */
function needsOf(name) {
	const ids = [
		...Object.keys(readJson(join(dirOf(name), 'package.json'))?.dependencies ?? {}),
		...Object.keys(readJson(join(dirOf(name), 'Packages', 'manifest.json'))?.dependencies ?? {}),
	];
	const repos = ids
		.filter((id) => id.startsWith(registry.packageIdPrefix))
		.map((id) => id.slice(registry.packageIdPrefix.length));
	return [...new Set([...repos, ...(byName.get(name)?.requires ?? [])])];
}

function git(cwd, ...gitArgs) {
	const result = spawnSync('git', gitArgs, { cwd, stdio: 'inherit' });
	return result.status === 0;
}

if (flag('list')) {
	const onDisk = readdirSync(ROOT).filter((f) => statSync(join(ROOT, f)).isDirectory() && existsSync(join(ROOT, f, '.git')));
	const width = Math.max(...registry.repos.map((r) => r.name.length));
	let kind = '';
	for (const r of registry.repos) {
		if (r.kind !== kind) console.log(`\n${(kind = r.kind)}`);
		console.log(`  ${isCloned(r.name) ? '✓' : '·'} ${r.name.padEnd(width)}  ${r.description}`);
	}
	const unlisted = onDisk.filter((f) => !byName.has(f));
	if (unlisted.length) console.log(`\nOn disk but missing from workspace.json: ${unlisted.join(', ')}`);
	console.log('\n✓ cloned   · not cloned');
	process.exit(0);
}

const wanted = flag('all') ? registry.repos.map((r) => r.name) : targets;
if (!wanted.length) {
	console.error('Usage: node tools/setup.mjs <repo...> | --all | --list   [--pull] [--dry]');
	process.exit(2);
}

// Walk the dependency graph: clone a repo, read what it needs, queue that. Breadth-first, each once.
const queue = [...wanted];
const seen = new Set();
const report = { cloned: [], pulled: [], present: [], failed: [], unknown: [] };

while (queue.length) {
	const name = queue.shift();
	if (seen.has(name)) continue;
	seen.add(name);

	if (!byName.has(name)) report.unknown.push(name);

	if (isCloned(name)) {
		if (flag('pull') && !flag('dry')) {
			(git(dirOf(name), 'pull', '--ff-only', '--quiet') ? report.pulled : report.failed).push(name);
		} else {
			report.present.push(name);
		}
	} else if (flag('dry')) {
		report.cloned.push(`${name} (would clone)`);
		continue; // its dependencies are unknown until it is cloned
	} else if (git(ROOT, 'clone', '--quiet', cloneUrl(name), dirOf(name))) {
		report.cloned.push(name);
	} else {
		report.failed.push(name);
		continue;
	}

	queue.push(...needsOf(name));
}

const line = (label, items) => items.length && console.log(`${label} (${items.length}): ${items.join(', ')}`);
line('Cloned', report.cloned);
line('Pulled', report.pulled);
line('Already here', report.present);
line('Failed (not pushed yet, no access, or local changes)', report.failed);
line('Not in workspace.json: add them', report.unknown);
process.exit(report.failed.length ? 1 : 0);
