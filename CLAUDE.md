# IT Kamianets 3D Engine — workspace guide

This folder holds the engine ecosystem and the game built on it, **Temnorid Arena** (round-based
team matches like CS2, with spells instead of guns). **The root folder is the game itself:** the
`temnorid-arena` Unity project and git repo (`WebArtWork/temnorid-arena`, branch `master`), which also
holds this file, `WORKSPACE.md` (new-machine setup), `workspace.json` and `tools/`. Every package,
the website and the constructor starters are their **own git repos cloned inside it** (GitHub org
`IT-Kamianets`; the `temnorid-arena-*` repos are in `WebArtWork`, set by `org` in `workspace.json`),
and the root `.gitignore` ignores those folders (`/3d-*/`, `/temnorid-arena-*/`, ...). The game's
manifest loads them as `file:../<repo>`. (Until 2026-10-15 the root was a separate
`temnorid-arena-workspace` repo with the game in a `temnorid-arena/` subfolder; that repo is retired.)

**Which repos exist, and getting them:**

- `workspace.json` lists every repo with its kind (schema, engine, constructor, template, game,
  game-package, website) and a one-line description. It is the answer to "what exists?".
- `node tools/setup.mjs <repo>` clones a repo and everything it needs into the root, by reading
  `package.json` / `Packages/manifest.json` (plus `requires` in `workspace.json`). For a game:
  `node tools/setup.mjs temnorid-arena`. `--list` shows what is cloned, `--pull` updates, `--dry`
  only plans. Run it before working on a repo that is missing here.
- `https://3d.itkamianets.com/packages.md` (generated from the website catalog) documents every
  public package: what it does, how to install it, what it depends on. Read it to choose packages
  for a new constructor or game.
- `constructors/3d-constructor-<style>/` are the six starter repos (character-view, top-down,
  isometric, side-view, fixed-view, free-view). They are **not part of this workspace**: not in
  `workspace.json`, not touched by `tools/`. A developer runs
  `git clone https://github.com/IT-Kamianets/3d-constructor-<style> <game-name>`, and that clone is
  the game's own workspace: the Unity project at its root, engine repos cloned inside it and
  git-ignored (`/3d-*/`), changes to them sent as a branch or fork PR to `master`. Each starter holds
  only `AGENTS.md`, `CLAUDE.md` (→ AGENTS.md), `README.md`, `.gitignore`, `.gitattributes`,
  `LICENSE`. Edit them in their own folders; this workspace only keeps them on disk.

The plan (which packages exist and which the game uses) is the pitch page on the website:
`3d.itkamianets.com/src/app/pages/pitch/pitch.component.ts` (`packageColumns`, `exampleUses`).
Keep folders, this file and that page in sync.

**Before coding any package, read this file and the READMEs of the packages it depends on and is
depended on by.** Every package is one piece of a larger graph; changes must fit that graph.

Never commit or push: the user commits everything themselves.

## Layers

```text
schemas (JSON, engine-independent)     3d-scene-schema, 3d-asset-schema, 3d-network-schema
        ↓
Unity engine packages (reusable)       3d-unity-*
        ↓
constructors (one per camera style)    3d-constructor-<style>-*
        ↓
game packages + game project          temnorid-arena-*, temnorid-arena
```

Website and catalog: `3d.itkamianets.com` (Angular 22, prerendered; has its own `AGENTS.md`).

## Package map

| Folder | Package id / assembly | Depends on | State |
|---|---|---|---|
| `3d-scene-schema` | JSON Schema v1 (1.1.0) | — | 1.1.0: scans + authored levels (`asset`, `GROUP`/`MARKER`/`PROP`, roles in `metadata.tags`); forward and surfaces face local −Z (+Z in Unity) |
| `3d-asset-schema` | JSON Schema v1 | — | v1: asset library + asset (model/material/texture/audio, source files, PBR material, bounds, placeholder); example library with generated GLB/PNG/WAV |
| `3d-network-schema` | JSON Schema v1 | — | v1 draft: session, message, result, replay; examples validate |
| `3d-unity-core` | `com.itkamianets.3d-unity-core` / `ITKamianets.Engine.Core` | — | v0.1: `EngineLog`, `EngineStorage` (PlayerPrefs default), `EngineEvents`, `EngineServices`; log through `EngineLog`, save player settings through `EngineStorage` |
| `3d-unity-scene` | `…3d-unity-scene` / `ITKamianets.Engine.Scene` | core, Newtonsoft.Json, Physics | v0.1: model (1.1.0) + JSON, `SceneLoader` → `LoadedScene`/`SceneObject` (tags, surfaces, colliders, `ISceneAssetResolver`), `SceneSaver` |
| `3d-unity-assets` | `…3d-unity-assets` / `ITKamianets.Engine.Assets` | core, scene, Newtonsoft, glTFast (optional) | v0.1: `EngineAssets` (catalog → file → placeholder → missing stand-in, cache, unload), `ISceneAssetResolver` |
| `3d-unity-input` | `…3d-unity-input` / `ITKamianets.Engine.Input` | core, Input System, uGUI, XR module | v0.1: `EngineInput.Default` — Player/UI/XR maps, scheme detection, `ReadLookDegrees`, game maps, rebinding, bindings saved to `EngineStorage`; touch via `EngineTouchControls` (on-screen device) + `TouchLookArea`; XR: `TryReadHead`/`TryReadHandAim`, `Turn`, `NextSlot`, `DominantHand` (saved), `Haptic`, controllers as UI pointers |
| `3d-unity-interaction` | `…3d-unity-interaction` / `ITKamianets.Engine.Interaction` | core, scene, input, Physics | v0.1: `SceneEditor` (select/pick, move/rotate/scale/duplicate/delete, snapping, drag, undo/redo), `SceneEditorInput`, `SelectionHighlight` |
| `3d-unity-character` | `…3d-unity-character` / `ITKamianets.Engine.Character` | core, Physics | v0.1: `CharacterMotor` driven by `CharacterCommand` (no input, no camera); events, eye position, state for network; `CharacterAnimator` (movement → Animator parameters); `IAimSource` (an aim ray, e.g. a VR hand) |
| `3d-unity-audio` | `…3d-unity-audio` / `ITKamianets.Engine.Audio` | core, scene, assets, character | v0.1: `EngineAudio` (sound events by name, voice pool, volume groups saved, music crossfades), `SoundBank`, `FootstepPlayer`, `AudioSceneResolver` |
| `3d-unity-xr` | `…3d-unity-xr` / `ITKamianets.Engine.XR` | core, input, character, Input System, uGUI (TextMeshPro), XR module | v0.1: `XRCharacterRig` (headset camera + hands on a `CharacterMotor`, room-scale steps, eye height, snap/smooth turn, body hidden, view smoothing), `XRComfort` (saved), `ComfortVignette`, `XRModeSwitch` (VR or flat components), `XRHeadDriver` (menu camera), `XRStatus.IsActive`, `XRPrimaryButtonAction`, `StatusHud`; no XR plug-in inside (the project picks OpenXR) |
| `3d-unity-spatial` | `…3d-unity-spatial` / `ITKamianets.Engine.Spatial` | core, scene, MRUK (optional) | v0.1: `RoomScanner` (MRUK → SceneData, never run on a device), `RoomMetrics`, `RoomLoader` |
| `3d-unity-firebase` | `…3d-unity-firebase` / `ITKamianets.Engine.Firebase` | core, scene, Newtonsoft | v0.1: REST (no SDK): anonymous auth kept per device, Firestore JSON, profile/progress/scene storage; rules in the game project |
| `3d-unity-network` | `…3d-unity-network` / `ITKamianets.Engine.Network` | core, character, Netcode for GameObjects 2.13.3 | v0.1: `NetworkSession` (host/server/client, CLI flags, extra network prefabs), `NetworkCharacter` (prediction, reconciliation, interpolation, owner-only components), `SessionChannel` (3d-network-schema session, messages, results, replays) |
| `3d-constructor-character-view-camera` | `…3d-constructor-character-view-camera` / `ITKamianets.Engine.CharacterView.Camera` | core, input, character, Physics | v0.1: `CharacterViewCamera` — owns Yaw/Pitch, 1st/3rd person blend, collision, body hiding, cursor lock |
| `3d-constructor-character-view-controls` | `…3d-constructor-character-view-controls` / `ITKamianets.Engine.CharacterView.Controls` | core, input, character, character-view-camera | v0.1: `CharacterViewControls` — input → `CharacterCommand`, 1st/3rd-person facing, aim, sprint/crouch toggles |
| `3d-constructor-vr-controls` | `…3d-constructor-vr-controls` / `ITKamianets.Engine.VR.Controls` (namespace `ITKamianets.Engine.VR`) | core, input, character, xr | v0.1: `VRControls` — XR input → `CharacterCommand` (head-relative walking, body follows room steps, real crouching, toggles), `HandAim` (dominant hand as `IAimSource`) |
| `3d-constructor-character-view-template` | Unity project (starter for 1st/3rd-person games) | pulls packages from GitHub | v0.1: Playground scene + Player prefab (copied from `temnorid-arena`, keep in sync), no project-wide input asset, setup guide; can't open until packages are pushed |
| `temnorid-arena-core` | `com.itkamianets.temnorid-arena-core` / `Temnorid.Arena.Core` | core, character, network | v0.1: `Combatant`, `ArenaMatch` (bots, respawns, scoreboard, defeat messages), `ArenaMatchSpawner`, `IArenaSpawns` |
| `temnorid-arena-spells` | `…temnorid-arena-spells` / `Temnorid.Arena.Spells` | core, input, character, audio, network, arena-core | v0.1: `SpellBook` (fireball, frost nova, arcane shield, blink), `SpellCaster` (server-authoritative; aims from an `IAimSource` in VR, `NextSlot`, haptics), `SpellBot`, `ArenaCharacterVisuals` (cast/hit/death animations, accent color), `SpellAimVisual` (VR casting hand) |
| `temnorid-arena-maps` | `…temnorid-arena-maps` / `Temnorid.Arena.Maps` | core, scene, assets, arena-core | v0.1: `ArenaMap` (catalogs, floor/wall materials), first arena (Scene JSON, generator in `Tools~`) + placeholder prop library |
| `temnorid-arena-hud` | `…temnorid-arena-hud` / `Temnorid.Arena.Hud` | core, input, xr, network, arena-core, arena-spells, uGUI | v0.1: `ArenaHud` (bars, spell bar, kill feed, scoreboard, respawn, touch controls, safe area; VR: wrist panel + floating panel), `ArenaUi` builder, `ArenaVRCanvas` (canvases become world-space panels in VR) |
| `temnorid-arena-menus` | `…temnorid-arena-menus` / `Temnorid.Arena.Menus` | core, input, xr, audio, network, firebase, arena-core, arena-hud, uGUI | v0.1: `ArenaMenus` (main, pause, settings, Firebase sign-in and stats; VR settings: turning, vignette, casting hand) |
| `temnorid-arena` (the workspace root) | Unity project of the game | used packages via `file:` | **Playable v0.1**: `Arena` scene (first build scene) built by Temnorid → Build Arena Scene; prefabs `ArenaPlayer`, `ArenaBot`, `ArenaMatch`; test scenes `Playground`, `NetworkPlayground`; dev bot `-bot`; Firebase config + rules; app id `arena.temnorid.waw`; glTFast, TMP essentials; models in `Assets/Models` (mage + arena props, Temnorid → Build Arena Art); Temnorid → Build Windows Release (zip) / Build Android APK / Build iOS Xcode Project / Build Quest APK; placeholder app icon; phone settings (landscape, IL2CPP, `Assets/Build/link.xml`, iOS Info.plist post-process); **VR (Meta Quest 3)**: OpenXR + Meta OpenXR, XRI (Editor simulator: Temnorid → VR Simulator in Play Mode), XR off on every target except while Build Quest APK runs, `Quest` quality level, `ArenaPlayer` with both control sets behind `XRModeSwitch` |

**Not used by Temnorid Arena** (still built, as engine packages): `3d-unity-interaction`,
`3d-unity-spatial`. They are not in the game project, so nothing
compiles them until a project that uses them exists.

`vroom-scanner` / `vroom-sync` (mentioned in some READMEs) are a separate product built on these
packages; they are not in this workspace. Engine packages must stay free of VRoom/game logic.

## Rules

- **Unity only for now.** No web or other-engine runtimes in this workspace; schemas stay
  engine-independent so those can come later.

- **Dependencies point one way**, down the layers above. A package may only use what its asmdef
  references; never add a reference that creates a cycle or points "up" (e.g. core → scene).
- **Shared before specific.** If more than one constructor could use it, it belongs in a
  `3d-unity-*` package (e.g. the character lives in `3d-unity-character`; each constructor only
  adds its camera/style).
- **Optional SDKs** (Meta MRUK, Firebase, ...) are not hard dependencies. Reference them in the
  asmdef plus a `versionDefines` entry (`MRUK_PRESENT`, `FIREBASE_PRESENT`, ...) and guard code
  with `#if`, so the package still compiles without them. See `3d-unity-spatial`.
- **Schemas are the source of truth.** C# models (and later TypeScript) mirror the JSON Schema
  field names exactly (`[JsonProperty]`, camelCase). Change the schema first, then the models.
- **Input only through `3d-unity-input`.** Other packages never read devices directly.
- **No product or game specifics** in `3d-unity-*` or constructor packages. Game rules live only
  in `temnorid-arena-*`; game events use the `temnorid-arena.` namespace in 3d-network-schema.

## Package conventions

- Folder `3d-<name>` → package id `com.itkamianets.3d-<name>`, version starts at `0.1.0`,
  `"unity": "6000.6"`.
- Assemblies `ITKamianets.Engine.<Name>` (Runtime), `.Editor`, `.Tests`, `.Editor.Tests`;
  `rootNamespace` equals the assembly name. Constructors use assemblies `ITKamianets.Engine.<Style>.<Part>`
  but one shared namespace `ITKamianets.Engine.<Style>` (a namespace ending in `.Camera` would hide
  `UnityEngine.Camera`).
  Game packages: folder `temnorid-arena-<part>`, id `com.itkamianets.temnorid-arena-<part>`,
  assembly `Temnorid.Arena.<Part>`, display name `Temnorid Arena: <Part>`.
- Layout: `package.json`, `Runtime/`, `Editor/`, `Tests/Runtime/`, `Tests/Editor/`,
  `README.md`, `CHANGELOG.md`, `LICENSE`, Unity `.gitignore`.
- README sections, in order: intro, `### Logic`, `### Status`, `### Roadmap`. The website catalog
  (`3d.itkamianets.com/src/data/catalog/*.json`) is written from these, so keep Status current
  and update the website when a package changes state.
- `.meta` files are generated by Unity when the game project imports a package; commit them with the
  package. Don't hand-write them.
- Tests are optional — the user prefers not to write them by default. Add one only where it
  clearly pays off (e.g. schema ↔ JSON round-trip) or when asked.
- Unity 6: TextMeshPro is part of `com.unity.ugui` (2.6.0); do not depend on `com.unity.textmeshpro`.
- No project-wide `.inputactions` asset in any project: all input is `3d-unity-input`'s `EngineInput`.

## Adding a package

```bash
node tools/new-package.mjs 3d-unity-foo --asm ITKamianets.Engine.Foo --display "Unity Foo" \
  --description "..." --deps 3d-unity-core,3d-unity-scene \
  [--unity-deps com.unity.inputsystem@1.20.0] [--asm-refs Unity.InputSystem] [--project temnorid-arena]
```

It creates the layout above (never overwrites existing files) and runs `git init`. With
`--project temnorid-arena` it also adds the package to that project's `Packages/manifest.json`;
leave it out for packages the game does not use. Then fill in the README and add the package to
this file's map (and to the website plan if it is new).

## Developing and verifying

- Open the workspace root (the game project) in Unity 6000.6.5f1. It loads every package the game uses
  from its folder inside the root, so edits recompile immediately and break-other-package errors show up right away.
- Changed files reach the Editor only after an asset refresh (normally on window focus): run
  `AssetDatabase.Refresh()` (via `unity command eval`) before `recompile`, and recompile before a build, or
  the build fails with "script class layout is incompatible between the editor and the player".
- Play mode only advances frames while the Unity window is in front: call `unity command editor_focus`
  before Play-mode tests driven from the CLI (otherwise `Time.frameCount` stays put).
- Live control from Claude Code needs that Editor open plus `unity pipeline install` in the
  game project; then `unity status` / `unity command`. Without an open Editor, compile/tests can
  run headless with `unity test temnorid-arena --mode EditMode`.
- Hidden runtime helpers (`[EngineAudio]`, `[EngineAssets]`) use `HideInHierarchy` + `DontDestroyOnLoad`, never
  `HideAndDontSave` (that survives the end of Play mode in the Editor and leaks one per session).
  `FindObjectsByType` does not see hidden objects; use `Resources.FindObjectsOfTypeAll` in tests.
- `Shader.Find` only finds shaders that a material in the build uses. URP Lit always ships; anything
  else (e.g. URP Unlit) needs a fallback to Lit or a place in Always Included Shaders, or it is null in builds.
- GLB models: glTFast (6.14) fails to import a skinned mesh whose materials have separate vertex data
  ("SortAndNormalizeBoneWeightsJob ... JobHandle.Complete"), and extra vertex colors break the skinned
  layout. `node tools/glb-fix-skinned.cjs in.glb out.glb --strip-colors` fixes both without changing
  the look. Model rules for artists/AI: `models-to-do.md`.
- Network test: build `NetworkPlayground` for Windows into `Builds/` (git-ignored), host in the
  Editor, run `TemnoridArena.exe -client 127.0.0.1 -bot -batchmode -nographics -logFile <file>`.
- Installed editor has only the Web module; Quest builds need the Android module
  (`unity install-modules 6000.6.5f1 --module android`).
- Games consume packages by git URL (`https://github.com/IT-Kamianets/<repo>.git#v0.1.0`). UPM
  does not resolve transitive git dependencies, so a game/template manifest lists every
  `com.itkamianets.*` package it needs, including indirect ones.

## Decisions (2026-10-14)

Agreed with the user before working through the remaining packages without stopping:

- **Firebase:** REST API (Firebase Auth + Firestore over HTTPS), not the Firebase Unity SDK: works on
  PC, servers and mobile alike. Project `temnorid-arena`. Config files and CLI files live in
  `temnorid-arena` only (`Assets/Firebase/`, `firebase.json`, `.firebaserc`, `firestore.rules`),
  never in engine packages. Deploy rules with the Firebase CLI from Git Bash:
  `firebase deploy --only firestore:rules` (allowed). Sign-in v0.1: anonymous; Google later (needs a
  Desktop OAuth client the user creates).
- **App identifier:** `arena.temnorid.waw` (Standalone, Android, iOS), matching the Firebase apps.
- **Unused engine packages** (interaction, spatial, xr): compiled and tested by adding them to
  `temnorid-arena`'s manifest temporarily, then removing them. No Meta SDKs or Android module:
  Quest-only code stays behind `#if` and compiles without them.
- **Temnorid Arena v0.1:** free-for-all **deathmatch against AI bots**, endless (a time limit later),
  respawns. Runs as a network host so real players can join later. Spells chosen by Claude (starter set:
  fireball, frost nova, arcane shield, blink), easy to change. HUD and menus: uGUI + TextMeshPro.

## Decisions (2026-10-15)

- **Platforms:** one project for Windows, Android, iOS and Meta Quest; per-platform differences are settings
  (quality levels, build profiles), input schemes and small `#if`s. Builds through the Temnorid menu.
- **Meta Quest 3:** a **VR arena** (immersive, the same arenas and matches; mixed reality in your room maybe
  later through `3d-unity-spatial`). XR stack: **OpenXR + XR Interaction Toolkit** (vendor-neutral; Meta-only
  features such as passthrough or MRUK as optional extras). **Cross-play**: a Quest player is just another
  combatant in the same matches as PC and phone players. Full plan and progress: `quest-plan.md`.
- **Quest vs phones (both Android):** XR stays off for every build target in the project; Temnorid → Build
  Quest APK assigns the OpenXR loader, Quest quality, Vulkan and API 32 only while it builds, then restores the
  phone settings. Game code never touches the XR SDK: it asks `XRStatus.IsActive` and reads poses through
  `EngineInput`. The XR Interaction Toolkit is used for its Editor simulator; the runtime uses the Input
  System's own XR UI support (tracked-device raycaster), not XRI interactors.

## Work order

Done (2026-10-14): every `3d-*` engine package at v0.1 (unused ones compiled and tested by adding them
to `temnorid-arena` temporarily), and Temnorid Arena v0.1 playable: deathmatch against bots, single
player and over the network (a built client joining the Editor host).

Next, one slice at a time, keeping the whole graph in view:

1. **Game feel:** real models and sounds (asset libraries with GLB, sound banks), spell aim feedback,
   hit markers, a smarter bot (cover, line of sight while moving).
2. **Match:** time or kill limit with results (`session.ended`), teams, the round/economy mode.
3. **Network:** lag compensation for hits, unreliable command stream, local-player smoothing,
   results and replays written by a trusted server to Firebase.
4. **Engine:** Google sign-in on PC, Quest builds (Android module, Meta XR SDK) for spatial/xr,
   further constructors (top-down, isometric, side view, fixed view, free view).
