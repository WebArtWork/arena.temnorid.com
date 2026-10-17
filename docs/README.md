# Temnorid Arena website

The game's public site: what the game is, downloads, and every rule of the game (`guide.html`).
Plain static HTML, no build step. GitHub Pages serves it from this folder
(repo Settings → Pages → Deploy from a branch → `master` / `/docs`).

* `index.html`: intro, spells, downloads, controls.
* `guide.html`: the full game logic, with the numbers the code uses.
* `404.html`: served by GitHub Pages for any missing path (self-contained, works at any depth).
* `logo.png`, `favicon.ico` / `favicon.png`, `seo.png` (1200 × 630 share image; `og:image` uses the absolute URL `https://webartwork.github.io/temnorid-arena/seo.png`, change it with the domain).
* `assets/`: `style.css`, a mage render (`output/mage/`), the arena prop sheet (`output/arena/`).
* `.nojekyll`: serve the files as they are.

### Downloads

The download buttons point at a GitHub release tagged `v0.1.0` with these assets:

| File | Made by |
|---|---|
| `TemnoridArena-0.1.0-windows.zip` | Temnorid → Build Windows Release (`Builds/`) |
| `TemnoridArena-0.1.0-android.apk` | Temnorid → Build Android APK (`Builds/Android/TemnoridArena.apk`, renamed) |
| `TemnoridArena-0.1.0-quest.apk` | Temnorid → Build Quest APK (`Builds/Quest/TemnoridArena.apk`, renamed) |

For a new version: raise Player Settings → Version, build, upload the files to a release
`v<version>`, then replace `0.1.0` in `index.html` (links, sizes, "Version 0.1") and `guide.html`.

### Keep in sync

`guide.html` mirrors the game's rules. When a package changes behaviour, update it too:
spells and bots (`temnorid-arena-spells`: `SpellBook`, `SpellCaster`, `SpellBot`), the match
(`temnorid-arena-core`: `Combatant`, `ArenaMatch`), the arena (`temnorid-arena-maps`), the HUD
and menus (`temnorid-arena-hud`, `-menus`), movement (`3d-unity-character`: `CharacterSettings`)
and bindings (`3d-unity-input`: `EngineInputActions`).
