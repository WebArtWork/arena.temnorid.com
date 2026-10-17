# IT Kamianets 3D Engine workspace

The engine packages (`3d-*`) and the game built on them, **Temnorid Arena** (`temnorid-arena*`).
The root folder is the **Temnorid Arena Unity project** (`WebArtWork/temnorid-arena`). Every
package, the website and the constructor starters are their own git repos cloned **inside** it and
ignored by its `.gitignore`. The root also holds the list of repos (`workspace.json`), the tools that
clone them (`tools/`), and the guide for working on them ([CLAUDE.md](CLAUDE.md)). The game itself
is described in [README.md](README.md).

## New machine

### 1. Install

| What | Why |
|---|---|
| Git | cloning the repos |
| Node.js 18+ | `tools/setup.mjs`, `tools/new-package.mjs`, model tools |
| Unity Hub + Unity **6000.6.5f1** | the Editor the projects use |
| Unity modules (Hub → Installs → 6000.6.5f1 → Add modules) | **Windows Build Support** is included on Windows; add **iOS Build Support** (Mac, plus Xcode) or **Android Build Support** (with OpenJDK and SDK & NDK) only for those builds |
| Firebase CLI (optional) | `npm i -g firebase-tools`, only to deploy `firestore.rules` |
| Unity CLI (optional) | live control of the Editor from Claude Code (`unity pipeline install` in the project) |

The Android SDK, NDK and the like are installed into the Unity Editor folder, never into a repo;
each machine installs them once per Unity version.

### 2. Clone

```bash
git clone https://github.com/WebArtWork/temnorid-arena.git 3d
cd 3d
node tools/setup.mjs temnorid-arena
```

`setup.mjs` clones every package the game needs (game packages, engine packages, schemas) into
`Packages/`, where Unity loads them as embedded packages, and the website into the root.
It reads the dependencies from the repos themselves, so nothing has to be listed by hand.

```bash
node tools/setup.mjs --list                 # every repo, cloned or not, and what it is
node tools/setup.mjs temnorid-arena --pull  # later: update everything already cloned
node tools/setup.mjs --all                  # every repo, including unused engine packages and the website
node tools/setup.mjs temnorid-arena --dry   # show what would be cloned
```

Over SSH instead of HTTPS: `TEMNORID_GIT_BASE=git@github.com:{org}/{name}.git node tools/setup.mjs temnorid-arena`.

### 3. Open the game

Unity Hub → Add → `3d` → open with 6000.6.5f1. The first import takes a while
(packages, glTFast models, shaders). Then open `Assets/Scenes/Arena.unity` and press Play.

Everything the game needs is in the repos: scenes, prefabs, models, the Firebase client config
(`Assets/Firebase/`). If the scene or prefabs ever look out of date, **Temnorid → Build Arena
Scene** rebuilds them from the packages.

### 4. Build

| Target | How |
|---|---|
| Windows | **Temnorid → Build Windows Release** → `Builds/TemnoridArena-<version>-windows.zip` |
| Android | **Temnorid → Build Android APK** → `Builds/Android/TemnoridArena.apk` (needs the Android module; restart the Editor after installing it) |
| Meta Quest 3 | **Temnorid → Build Quest APK** → `Builds/Quest/TemnoridArena.apk` (needs the Android module; turns OpenXR on only for this build), then `adb install -r` (see the game's README, "Meta Quest 3") |
| iOS | **Temnorid → Build iOS Xcode Project** → `Builds/iOS` (needs the iOS module; exports on Windows too), then on a Mac: Xcode, team, run (see the game's README, "Phones") |

`Builds/` and `Library/` are git-ignored in every project.

## Working on it

Read [CLAUDE.md](CLAUDE.md) first: the package map, the layers and their rules, conventions,
how to add a package (`node tools/new-package.mjs`), and how to verify changes. Each repo is
committed and pushed on its own.
