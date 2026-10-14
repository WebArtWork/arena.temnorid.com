# Temnorid Arena

**Temnorid Arena** is the sample game of the **IT-Kamianets 3D Engine**: round-based team matches, like CS2, with spells instead of guns.

This folder is the game's Unity project (Unity 6000.6, URP).

### Logic

The game starts from the Character View constructor (`3d-constructor-character-view-template`): first- and third-person camera and controls, on the shared engine packages.

`Packages/manifest.json` references each package from its sibling folder (`file:../../3d-unity-core`, ...), so editing a package recompiles it here immediately. It lists only what the game uses:

* Engine: `3d-unity-core`, `-scene`, `-input`, `-character`, `-assets`, `-audio`, `-firebase`, `-network`
* Constructor: `3d-constructor-character-view-camera`, `-controls`
* Game: `temnorid-arena-core`, `-spells`, `-maps`, `-hud`, `-menus`

Not used by this game: `3d-unity-interaction`, `3d-unity-spatial`, `3d-unity-xr`.

Scenes, prefabs wiring the packages together, and build settings live here; reusable code lives in the packages.

### Setup

1. Clone this repo next to the package repos, so the folders are siblings (`3d/temnorid-arena`, `3d/3d-unity-core`, ...).
2. Open `temnorid-arena` in Unity 6000.6 (Unity Hub → Add → this folder).
3. Optional, for live editing from Claude Code: `unity pipeline install` in this folder.

### Status

**Playable v0.1: an endless free-for-all deathmatch against bots.** Open `Assets/Scenes/Arena.unity` (the first build scene) and press Play:

* Main menu: type your name, **Play vs bots** (you host; 5 bots join), or **Join** another player's game by address. Settings: volumes, mouse sensitivity, invert look.
* In the match: WASD / left stick move, mouse / right stick look, Space jump, Shift sprint, Ctrl crouch, V first/third person; **1-4** pick a spell, **left mouse / right trigger** casts it; **Tab** scoreboard; **Esc** pause (Leave match).
* Spells: 1 Fireball, 2 Frost nova, 3 Arcane shield, 4 Blink.
* With Firebase (`Assets/Firebase/google-services.json`) you are signed in anonymously and your kills and deaths are added up across matches.

Contents:

* `Assets/Scenes/Arena.unity`: NetworkSession (player `ArenaPlayer`, network prefabs `ArenaBot`, `ArenaMatch`), ArenaMap (first arena), ArenaMatchSpawner, HUD, Menus, a camera with `CharacterViewCamera`.
* `Assets/Prefabs/`: `Player` (offline), `NetworkPlayer` (variant with `NetworkCharacter`), `ArenaPlayer` (variant with `Combatant`, `SpellCaster`, footsteps), `ArenaBot`, `ArenaMatch`.
* `Assets/Dev/Editor/ArenaSceneBuilder.cs`: **Temnorid → Build Arena Scene** rebuilds the prefabs and the scene from the packages.
* `Assets/Dev/NetworkTestBot.cs`: started with `-bot`, a client walks, jumps and casts by itself (network tests).
* `Assets/Scenes/Playground.unity` and `NetworkPlayground.unity`: test levels for the character and the network.
* Firebase: `Assets/Firebase/` (config), `firebase.json`, `.firebaserc`, `firestore.rules` (deploy with `firebase deploy --only firestore:rules` from Git Bash).
* App identifier `arena.temnorid.waw`; TextMeshPro Essential Resources imported; glTFast for GLB models; Run In Background on.
