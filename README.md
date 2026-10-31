# Temnorid Arena

**Step into the arena. Win your class’s crown.**

Heroic duels, glory and trophies. A [Temnorid](https://temnorid.com) game for **Windows**, **Android** and **iOS**, built on the [IT Kamianets 3D Engine](https://3d.itkamianets.com) with Unity 6000.6.

**Status:** version 0.3.0, in development. Website: https://arena.temnorid.com · Download: [Temnorid-Arena-Setup.exe](https://github.com/WebArtWork/arena.temnorid.com/releases/latest/download/Temnorid-Arena-Setup.exe)

## What it is

Step into the arena, face other heroes, and fight with weapons and elemental magic. Win, raise your reputation and earn unique rewards.

## The first playable

The first playable is a free-for-all in a stone courtyard: you and five bots, two fighters of each class, Warrior, Archer and Mage. Every period the best fighter of each class becomes that class’s Hero.

### How a session goes

1. Choose your class on the start screen: **Warrior**, **Archer** or **Mage**.
2. Fight everyone in the courtyard. Bots of all three classes hunt you and each other.
3. Defeat a fighter and score a kill. If you fall, you are back after three seconds at the spawn circle furthest from the fighting.
4. When the period ends (90 seconds), the fighters of each class are ranked against their own class: most kills first, then fewest deaths.
5. The best fighter of each class becomes that class’s **Hero** and wears a gold HERO badge through the next period: up to three Heroes, one per class.
6. Start the next period and try to take a Hero title, or defend yours.

### Controls

| Key | Action |
| --- | --- |
| W A S D | Move |
| Mouse | Look and aim |
| Shift | Run |
| Space | Jump |
| Left mouse (hold) | Attack: sword swing, arrow or fireball |
| Tab (hold) | Scoreboard |
| V | Switch between third and first person |
| Esc | Pause menu (resume, main menu); also frees the mouse so you can click the top bar tabs |

### Win and lose

- **Win:** Score the most kills in your class during a period to become its Hero.
- **Lose:** There is no losing: if you fall, you come back after three seconds. Your best period and how often you were Hero are saved.

### The scene

One stone courtyard, 40 by 40 metres: walls, pillars, crates, low walls, a raised platform with stairs, braziers and eight spawn circles. The props and the mage are the delivered models; the Warrior and the Archer wear the same body with a class sash and a simple weapon until their own models exist.

## Playing with others

Up to four people fight in one courtyard, free for all (**Play with others** on the main menu: one hosts, the others join by address or code). Everybody picks a class; bots take the remaining fighters, and a bot hands its place to anyone who joins later. The host runs the bots, the periods, the score and the Heroes and checks every hit; if the host leaves, the match ends for everybody. Windows, Android and iOS players can share a match.

## Updates

The Windows game updates itself from the newest GitHub release (`WebArtWork/arena.temnorid.com`) when it starts. **Profile** can turn that off. The installer starts the game again after a silent self-update.

## Phones

Android (minimum API 26) and iOS builds come from the same project through Unity Build Profiles (`ShellSceneKit` sets the application id `com.itkamianets.arena`, landscape and IL2CPP). Controls: a stick on the left to move, drag the right side to look, **Attack**, **Jump** and **Run** buttons, a **Pause** button, and the bottom tabs for the Scoreboard and Profile. The class screen and every menu work by tap.

## How it connects to Lords

Arena results, reputation and trophies are meant to carry into Lords, where heroes are raised. Today Arena does not talk to Lords.

This is for a later phase: [Lords](https://lords.temnorid.com) is the central strategy game and every Temnorid game will be connected to it. **This version is fully offline and does not talk to Lords**; progress is saved locally on the player's PC. Keep identifiers and save data simple and stable so the connection is cheap to add later.

## Scope of this version

- One arena, three classes, six fighters, timed periods and a Hero for each class.
- Up to four players online (free for all, bots fill the rest) and phones; no online ranking or VR. The earlier multiplayer Arena is kept on the `legacy` branch and is not used: the new online code is `ArenaNet`, on `3d-unity-net`.

Everything is drawn with Unity primitives (blocks, capsules, spheres), simple colours and text. No models, no paid assets. Every visible object has a stable asset id (for example `resources/wood-node`) so real models can replace the placeholders without touching gameplay.

## For developers and agents

- **Unity:** 6000.6, Universal 3D (URP). Open the folder in Unity Hub. `Library/` is rebuilt locally and not committed.
- **Platform:** Windows (self-updating), Android and iOS. Keyboard and mouse or touch. One arena, local save, optional online play.
- **The old Arena:** the earlier multiplayer game (Netcode, Firebase, phones, Meta Quest) is on the `legacy` branch. This project starts again from the Character View starter: nothing from that code is used, and the models are kept.
- **Starts from:** the IT Kamianets 3D Engine Character View starter. [AGENTS.md](AGENTS.md) is still the starter's generic guide; this README is the game's own.
- **Engine pieces this game uses:** the Character View set (`3d-unity-character`, the camera and controls), `3d-unity-core`, `3d-unity-input` (all input, through `EngineInput`, including the Tab scoreboard added as a game action), `3d-unity-save`, and the shared `3d-unity-combat` (health, teams, melee cone, **projectiles** for arrows and fireballs), `3d-unity-ai` (bots; **ranged attackers** that keep their distance), `3d-unity-feedback` (effects, health bars above heads), `3d-unity-ui` (the screens) and `3d-unity-graybox` (asset ids and builder helpers). The classes, the scoring and Heroes, the match and the screens are written in this game (`Assets/Scripts`).
- **Packages while developing:** the packages sit in the shared workspace folder next to the games (`../3d-unity-*`, `../3d-constructor-character-view-*`) and `Packages/manifest.json` points at them with `file:../../<package>`. When they get their own repositories, only these paths change.
- **Models:** `Assets/Models` keeps the delivered art as GLB files: the rigged mage with its animator (`characters/Mage.controller` and clips), eleven courtyard props and the stone floor and wall materials. `glTFast` imports them. Placeholder rule: the Warrior and the Archer use the mage's body with a class sash and a primitive weapon. Every placed object carries an asset id (`arena/pillar`, `characters/warrior`) so a real model can replace it later. The editable Blender sources and previews are in `output/`, and the brief for new models is `models-to-do.md`.
- **Level building:** menu **Temnorid > Arena > Build Scene** (or `-executeMethod Temnorid.Arena.EditorTools.ArenaSceneBuilder.Build`) rebuilds `Assets/Scenes/Main.unity` from scratch: floor, walls, props, stairs, braziers, banners, spawn circles, the six fighters, camera and light. Do not edit that scene by hand. The NavMesh is baked at game start from box colliders, and the fighters are switched on after it exists.
- **Shell and online (0.3.0):** scenes are `Boot` (splash, profile, update check), `Menu` (landing, onboarding, lobby, profile) and `Main` (the arena), built by the scene builder through `ShellSceneKit` (`ArenaShell.cs` is the config: game id `arena`, 4 players, places Arena and Scoreboard). `ArenaNet.cs` is all the online code. Authority rules: the host (where `NetSession.IsAuthority`) runs the bots, periods, respawn timing, score and Heroes; a client plays its own fighter (published as a pose) and shows the host's bots (entities) and score (`ar.state`). A client's hits are `ar.hit` requests (victim, kind) that the host checks for reach, rate and class and applies with the class's own damage; damage to a person comes back with `ar.dmg` to its owner. Wire events are prefixed `ar.`. Online time never stops (pause is a local menu; bots wait only before the match and between periods; the host starts the next period itself after 15 seconds). No catch-up against people.
- **Ports:** direct play uses 7777; the online test uses 7806.
- **Tests (0.3.0):** `OnlineArenaTests` runs the real scene as host with a detached guest session in the same process; `ArenaNetCodecTests` covers the wire formats.
- **Code map (`Assets/Scripts`):**
  - `ArenaScoring` (kills, deaths, the ranking inside each class, Heroes) and `ArenaSession` with `ArenaSaveStore` (the player's record): no Unity in them. `Classes` holds the numbers of the three classes: this is the whole balance.
  - `ArenaGame` (the match: periods, respawns, pause, saving), `Fighter` (one fighter, bot or player, and what its attack does), `PlayerControl` (aims and fires), `FighterAnimation`, `HeroBadge`, `ArenaHud` (all screens, plain uGUI).
- **Tests:** `Assets/Tests/EditMode` covers the scoring (rankings, ties, Heroes, titles moving between periods) and the player's record; `Assets/Tests/PlayMode` loads the scene, checks six fighters stand on the NavMesh, starts matches, plays a whole period with bots and checks the Heroes and respawns. Run them from Window > General > Test Runner, or with `Unity.exe -batchmode -nographics -runTests -testPlatform EditMode` (or `PlayMode`).
- **Save file:** `<persistent data path>/arena/default.json` (on Windows `%USERPROFILE%\AppData\LocalLow\Temnorid\Temnorid Arenarena\default.json`): periods played, best period, times Hero and the latest Heroes.
- **Windows build:** **Temnorid > Arena > Build Windows Player** (or `-executeMethod Temnorid.Arena.EditorTools.ArenaReleaseBuilder.BuildWindows`) writes `Builds/Windows/TemnoridArena` and a portable zip. `installer/Arena.iss` turns the folder into `Builds/Installer/Temnorid-Arena-Setup.exe` with Inno Setup.
- **Do not add:** Firebase, accounts, or any link to Lords in this version. Networking goes only through `3d-unity-net`.

## Website and downloads

The `docs/` folder is this game's public website (`docs/index.html`, served at https://arena.temnorid.com through GitHub Pages; `docs/CNAME` holds the domain). It repeats the information in this README: what the game is, how to play, controls and the download button.

The download button points to the latest GitHub Release asset:

`https://github.com/WebArtWork/arena.temnorid.com/releases/latest/download/Temnorid-Arena-Setup.exe`

Release steps (when the first playable is ready):

1. Build the Windows player from Unity (File > Build Profiles > Windows).
2. Wrap the build in a Windows installer named `Temnorid-Arena-Setup.exe`.
3. Create a GitHub Release (tag `v0.1.0`) and attach that file.
4. Open the website, click the button, install, play, save, restart, uninstall.

When you change the game's description, controls or rules, update this README and `docs/index.html` together.

## License

See [LICENSE](LICENSE).
