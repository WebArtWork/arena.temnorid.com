# Temnorid Arena

**Temnorid Arena** is the sample game of the **IT-Kamianets 3D Engine**: round-based team matches, like CS2, with spells instead of guns.

This folder is the game's Unity project (Unity 6000.6, URP).

### Logic

The game starts from the Character View constructor (`3d-constructor-character-view-template`): first- and third-person camera and controls, on the shared engine packages.

`Packages/manifest.json` references each package from its folder inside this one (`file:../3d-unity-core`, ...), so editing a package recompiles it here immediately. It lists only what the game uses:

* Engine: `3d-unity-core`, `-scene`, `-input`, `-character`, `-assets`, `-audio`, `-firebase`, `-network`
* Constructor: `3d-constructor-character-view-camera`, `-controls`
* Game: `temnorid-arena-core`, `-spells`, `-maps`, `-hud`, `-menus`

Not used by this game: `3d-unity-interaction`, `3d-unity-spatial`, `3d-unity-xr`.

Scenes, prefabs wiring the packages together, and build settings live here; reusable code lives in the packages.

### Generated models

`Assets/Models/` contains a first-pass rigged mage, eleven arena props, and floor/wall
PBR texture sets from `models-to-do.md`. Actual mesh previews, editable Blender
sources, validation reports, and import notes are in `output/mage/` and `output/arena/`;
the complete prop contact sheet is `output/arena/asset-preview-sheet.jpg`.
**Temnorid → Build Arena Art** (also run by Build Arena Scene) turns them into what the game uses:
the catalog `Assets/Models/ArenaModels.asset` (asset ids `arena/*`, `characters/mage`), the
`StoneFloor`/`StoneWall` URP materials, and `Mage.controller` (movement blend tree, jump, crouch,
death; casts and hit on an upper-body layer). Player and bots use the mage, recolored per
combatant (you cyan, other players gold, bots red). Skinned GLBs must keep one vertex set per
mesh and no vertex colors: fix a delivered one with `node ../tools/glb-fix-skinned.cjs in.glb out.glb --strip-colors`.
Not used yet: the stairs model, the brazier flame (no fire or light).

### Setup

1. Clone this repo (it is the workspace root), then `node tools/setup.mjs temnorid-arena` clones the package repos into it (`3d/3d-unity-core`, ...; git-ignored here). See [WORKSPACE.md](WORKSPACE.md).
2. Open this folder in Unity 6000.6 (Unity Hub → Add → this folder).
3. Optional, for live editing from Claude Code: `unity pipeline install` in this folder.

### Phones (iOS and Android)

The game runs on phones with touch controls (move stick, drag to look, Cast/Jump/Crouch/Sprint,
tappable spell bar), landscape only, inside the screen's safe area. Already set in this project:
app id `arena.temnorid.waw`, IL2CPP (Android ARM64), minimal code stripping plus
`Assets/Build/link.xml` (keeps the engine and game assemblies that JSON and Firebase read by
reflection), `Assets/Build/Editor/IosInfoPlist.cs` (Local Network prompt text, export
compliance "no non-exempt encryption"), iOS automatic signing, build number 1, iOS 15+, and a
placeholder app icon (`Assets/Branding/AppIcon.png`, all platforms; real art is item 15 of
`models-to-do.md`). Raise Player Settings → Version for each release and the iOS build number
for each upload.

**iOS (on a Mac):**

1. Unity Hub: install 6000.6.5f1 with **iOS Build Support**; install Xcode.
2. Clone this repo and the package repos into it, as here (`node tools/setup.mjs temnorid-arena`).
3. Open the project, **Temnorid → Build iOS Xcode Project** (exports `Builds/iOS`, then returns to the previous platform).
4. Xcode → Settings → Accounts: sign in with the Apple developer account.
5. Open `Builds/iOS/Unity-iPhone.xcodeproj` → target Unity-iPhone → Signing & Capabilities: tick "Automatically manage signing", pick your team (bundle id `arena.temnorid.waw`).
6. Plug in the iPhone (first time: trust the computer, turn on Developer Mode in Settings → Privacy & Security), choose it as the run destination, Run. The first launch asks to allow local network access: needed to host or join games on Wi-Fi.
7. Release: App Store Connect → My Apps → new app with bundle id `arena.temnorid.waw`; in Xcode pick "Any iOS Device", Product → Archive → Distribute App → TestFlight / App Store Connect.

The Xcode project can also be exported on Windows (with the iOS module) and copied to a Mac,
but building it there from the Unity project is simpler: rebuild and run in one place.

**Android (Windows or Mac):** install the Android module (`unity install-modules 6000.6.5f1 --module android`), then **Temnorid → Build Android APK** makes a development APK at `Builds/Android/TemnoridArena.apk` (debug-signed; the Editor returns to its platform afterwards). Install it with `adb install -r Builds/Android/TemnoridArena.apk` (USB debugging on), or send the file to the phone and open it. No Android Studio needed.

Testing touch in the Editor: Window → General → Device Simulator, pick a phone; the touch controls and safe area show there. Touch controls appear whenever the game is in the Touch scheme (phones always start in it).

### Meta Quest 3 (VR)

The same project is also a VR game for Meta Quest 3: you stand in the arena as the mage, walk with
the left stick, turn with the right one (snap 30° or smooth), and cast with the trigger along your
hand; health, mana and the spell bar are on your other wrist. A Quest player joins the same matches
as PC and phone players. Plan and progress: `quest-plan.md`.

How it fits together: XR is **off for every platform in the project**, so Windows, phone and iOS
builds never start it. **Temnorid → Build Quest APK** turns OpenXR on for Android only while it
builds (OpenXR with Meta Quest Support, Oculus Touch and Touch Plus controller profiles, single-pass
multiview, foveated rendering, the system keyboard), uses the `Quest` quality level
(`Assets/Settings/Quest_RPAsset.asset`: MSAA 4x, no HDR, 20 m shadows), Vulkan, Android 12L
(API 32) as minimum, landscape, writes `Builds/Quest/TemnoridArena.apk` (development, debug key) and
puts the phone settings back. **Temnorid → Set Up Quest XR** creates those settings without
building; **Build Android APK** always takes OpenXR off first.

The player prefab carries both control sets: `XRModeSwitch` turns on the XR rig, `VRControls` and
`HandAim` in a headset and `CharacterViewControls` otherwise, on the owner's copy only. The HUD and
menus become world-space panels in VR (`ArenaVRCanvas`); the menu camera follows the headset
(`XRHeadDriver`).

**On the headset:**

1. Quest 3 in Developer Mode (Meta Horizon app → your headset → Developer Mode), connected by USB-C, "Allow USB debugging" accepted in the headset.
2. **Temnorid → Build Quest APK**, then `adb install -r Builds/Quest/TemnoridArena.apk`.
3. On the headset: Library → Unknown Sources → Temnorid Arena. Logs: `adb logcat -s Unity`.

Controls: left stick move, right stick turn (click: next spell), trigger of your casting hand cast
(pick a spell on the wrist by pointing at it), A jump, B crouch (or crouch for real), left stick
click sprint, Y scoreboard, ☰ pause. Settings (in VR): snap or smooth turning, comfort vignette,
casting hand.

**Without a headset:** **Temnorid → VR Simulator in Play Mode** (imports the XR Interaction
Toolkit's simulator sample the first time) starts Play mode with a simulated headset and
controllers driven by keyboard and mouse; the game takes its VR layout. Turn it off for flat play.

### Status

**Playable v0.1: an endless free-for-all deathmatch against bots.** Open `Assets/Scenes/Arena.unity` (the first build scene) and press Play:

* Main menu: type your name, **Play vs bots** (you host; 5 bots join), or **Join** another player's game by address. Settings: volumes, mouse sensitivity, invert look.
* In the match: WASD / left stick move, mouse / right stick look, Space jump, Shift sprint, Ctrl crouch, V first/third person; **1-4** pick a spell, **left mouse / right trigger** casts it; **Tab** scoreboard; **Esc** pause (Leave match).
* Spells: 1 Fireball, 2 Frost nova, 3 Arcane shield, 4 Blink.
* With Firebase (`Assets/Firebase/google-services.json`) you are signed in anonymously and your kills and deaths are added up across matches.

Contents:

* `Assets/Scenes/Arena.unity`: NetworkSession (player `ArenaPlayer`, network prefabs `ArenaBot`, `ArenaMatch`), ArenaMap (first arena), ArenaMatchSpawner, HUD, Menus, a camera with `CharacterViewCamera`.
* `Assets/Prefabs/`: `Player` (offline), `NetworkPlayer` (variant with `NetworkCharacter`), `ArenaPlayer` (variant with `Combatant`, `SpellCaster`, footsteps), `ArenaBot`, `ArenaMatch`.
* `Assets/Dev/Editor/ArenaSceneBuilder.cs`: **Temnorid → Build Arena Scene** rebuilds the prefabs and the scene from the packages; `ArenaArtBuilder.cs`: **Temnorid → Build Arena Art** (catalog, materials, mage controller).
* `Assets/Dev/Editor/ArenaReleaseBuilder.cs`: **Temnorid → Build Windows Release** makes a release build in `Builds/Release/TemnoridArena` and zips it as `Builds/TemnoridArena-<version>-windows.zip` (version from Player Settings; folders Unity marks "don't ship" are left out). Players unzip it and run `TemnoridArena.exe`.
* `Assets/Models/`: mage and arena models (GLB), stone textures, the generated catalog, materials and controller.
* `Assets/Dev/NetworkTestBot.cs`: started with `-bot`, a client walks, jumps and casts by itself (network tests).
* `Assets/Scenes/Playground.unity` and `NetworkPlayground.unity`: test levels for the character and the network.
* Firebase: `Assets/Firebase/` (config), `firebase.json`, `.firebaserc`, `firestore.rules` (deploy with `firebase deploy --only firestore:rules` from Git Bash).
* App identifier `arena.temnorid.waw`; TextMeshPro Essential Resources imported; glTFast for GLB models; Run In Background on.
* VR: OpenXR 1.18 (with Meta OpenXR 2.6.1), XR Interaction Toolkit 3.6.1 (Editor simulator), XR Plug-in Management 4.7; `Assets/Dev/Editor/ArenaQuestBuilder.cs` (Set Up Quest XR, Build Quest APK), `ArenaVRSimulator.cs` (VR Simulator in Play Mode); `Assets/XR/` (XR and OpenXR settings), `Assets/Settings/Quest_RPAsset.asset`. Checked with simulated headset and controllers: a full round (menu, Play vs bots, cast, wrist spell pick, pause, settings, resume, leave).
