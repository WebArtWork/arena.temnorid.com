# Temnorid Arena: models to make

**Latest revision (2026-10-18):** a shared-platform realism pass updates all twelve
models (mage plus eleven props) and both stone material sets. Current editable source,
actual Unity previews, backups and validation are in `output/realism/`. The mage is
24,500 triangles with 52 bones and 17 clips; prop triangle budgets and collision
dimensions are retained. Original sources and earlier reviews remain available.

**Delivery status (2026-10-15):** all 14 entries now have a first-pass asset in
`Assets/Models/`: the rigged mage, eleven arena props, and both stone texture sets.
See `output/mage/README.md` and `output/arena/README.md` for previews, validation
reports, rebuild instructions, and remaining polish/integration work. These
files are generated prototypes; asset-catalog wiring and in-game Unity checks
remain to be done.

A work list for generating the game's 3D models. Each entry is self-contained: what it is, its exact
size, how it must be oriented, its budget, and a prompt to start from. Work top to bottom; the first
group replaces the grey shapes the game shows today.

The game: **Temnorid Arena**, a fast first/third-person arena game where mages fight with spells
instead of guns (fireball, frost nova, arcane shield, blink). The first arena is a ruined stone
courtyard, 40 × 40 m, with pillars, crates and a raised central platform.

## Rules for every model

These come from how the engine loads models. A model that breaks them will float, sink, face the
wrong way or be the wrong size.

| Rule | Value |
|---|---|
| Format | **GLB** (glTF 2.0 binary), textures embedded |
| Units | **meters**, real-world size, scale 1 (no scaling in the file) |
| Up | **+Y** |
| Front | **+Z** (glTF's front). A character looks along +Z; a prop's "front side" faces +Z |
| Origin (pivot) | **bottom centre**: the point the model stands on, at (0, 0, 0). Nothing below y = 0 |
| Transforms | applied (no leftover rotation or scale on the root) |
| Materials | PBR metallic-roughness (glTF standard). **One material per prop** where possible |
| Textures | power of two (512, 1024, 2048), PNG or JPG; base color in sRGB; normal map optional |
| Contents | the model only: no cameras, lights, ground planes or hidden helper objects |
| Vertex colors | **none** (no `COLOR_0` / `COLOR_1`; Blender's "Color Attributes" export off) |
| Skinned meshes | all material parts **share one vertex set**: one mesh, one set of vertices, one index list per material (Blender: export the character as a single joined mesh). Separate vertex data per material makes Unity's GLB importer fail |
| Names | lowercase with dashes, as given below (`arena-pillar.glb`) |

**Size must match the bounds below.** The game already places these objects using these exact
sizes (collision boxes and the grey placeholders use them), so a model bigger or smaller than its
bounds will look wrong against what players collide with.

**Style (all models):** stylized-realistic dark-fantasy ruins. Readable silhouettes from 20 m away,
chunky shapes, slightly exaggerated proportions, worn edges. Palette: warm grey-beige stone, dark
weathered wood, aged bronze/iron accents, small glowing arcane runes (cyan-violet) as the only
saturated color besides the spells. No text or logos. Avoid fine detail thinner than ~3 cm (it
shimmers at game distance).

**Where files go:** `Assets/Models/<group>/<file>.glb` (create the folders). After
delivery, Claude wires each file to its asset id, so the game swaps the placeholder for the model.

## Priority

| # | Asset id | File | Group | Replaces |
|---|---|---|---|---|
| 1 | `characters/mage` | `mage.glb` | characters | the capsule player and bots |
| 2 | `arena/pillar` | `arena-pillar.glb` | arena | grey pillar boxes |
| 3 | `arena/crate` | `arena-crate.glb` | arena | brown crate boxes |
| 4 | `arena/platform` | `arena-platform.glb` | arena | grey central block |
| 5 | material `arena/stone-floor` | `stone-floor.png` set | materials | the plain floor |
| 6 | material `arena/stone-wall` | `stone-wall.png` set | materials | the plain walls |
| 7 | `arena/wall-segment` | `arena-wall-segment.glb` | arena | (next arena: modular walls) |
| 8 | `arena/low-wall` | `arena-low-wall.glb` | arena | (more cover) |
| 9 | `arena/broken-pillar` | `arena-broken-pillar.glb` | arena | (variety) |
| 10 | `arena/rubble` | `arena-rubble.glb` | arena | (variety) |
| 11 | `arena/stairs` | `arena-stairs.glb` | arena | (height, onto the platform) |
| 12 | `arena/brazier` | `arena-brazier.glb` | arena | (light source, mood) |
| 13 | `arena/spawn-circle` | `arena-spawn-circle.glb` | arena | (marks spawn points) |
| 14 | `arena/banner` | `arena-banner.glb` | arena | (decoration on walls) |
| 15 | app icon | `AppIcon.png` | branding | the generated placeholder icon (needed for the App Store) |

---

## 1. Mage (player and bots) — `characters/mage`

**File:** `Assets/Models/characters/mage.glb`

**Used for:** every combatant. The player's own body (seen in third person and in shadows), other
players and the 5 bots. Bots and players are told apart by a color tint the game applies, so model
it in **neutral colors** (greys, browns) with one clearly separate **accent material** (sash, trim,
hood lining) that the game can recolor.

**Size:** height **1.80 m** to the top of the head (or hood), shoulders at most **0.70 m** wide, depth
at most 0.50 m. The engine's collision capsule is 1.8 m tall with a 0.35 m radius; the body must fit
inside it. **Eyes at 1.65 m** (the first-person camera sits there).

**Pivot:** between the feet, on the ground. **Front:** the face looks along +Z. T-pose or A-pose.

**Budget:** 15 000–25 000 triangles. Textures 2048 × 2048: base color, normal, metallic-roughness
(one set for the body, one small set for the accent part is fine). Two materials: `body`, `accent`.

**Rig:** humanoid skeleton compatible with Unity's Humanoid / Mixamo naming (hips, spine, chest,
neck, head, shoulders, upper/lower arms, hands with fingers, upper/lower legs, feet, toes). Skinned,
no stretching at shoulders and hips. **No root motion**: the game moves the character itself, so
animations stay in place.

**Animations** (separate clips in the same GLB, 30 fps, names exactly as listed):

| Clip | Loop | Length | Notes |
|---|---|---|---|
| `idle` | yes | 2–3 s | relaxed ready stance, staff or hands lowered |
| `walk` | yes | 1 s | 4 m/s forward |
| `run` | yes | 0.7 s | 6.5 m/s forward, leaning in |
| `strafe-left`, `strafe-right` | yes | 1 s | sideways at 4 m/s, facing forward |
| `walk-back` | yes | 1 s | backwards at 3 m/s |
| `crouch-idle`, `crouch-walk` | yes | 1 s | crouched height ≤ 1.10 m, walk at 2 m/s |
| `jump-start`, `fall`, `land` | no / yes / no | 0.2 / loop / 0.3 s | jump rises 1.2 m |
| `cast-projectile` | no | 0.4 s | one-hand thrust forward (fireball) |
| `cast-nova` | no | 0.6 s | both hands slam down / outward burst |
| `cast-shield` | no | 0.5 s | arms raise and cross, protective |
| `cast-blink` | no | 0.3 s | quick crouch-spring forward |
| `hit` | no | 0.3 s | flinch, upper body only is fine |
| `death` | no | 1.2 s | collapse, ends lying still |

**Details:** a hooded battle-mage: hood or cowl (face partly visible), layered robe ending above the
knees (no long trailing cloth: it would clip through legs while running), leather bracers, belt with
small pouches, soft boots. No weapon in hand (spells come from the hands); optional short staff on
the back. Glowing rune marks on bracers in the accent material.

**Prompt:**
> A stylized-realistic dark-fantasy battle mage, full body, standing in T-pose. Hooded cowl with the
> face partly visible, knee-length layered robe in weathered grey and brown fabric, leather bracers
> with faint glowing cyan-violet runes, leather belt with small pouches, soft leather boots. Chunky
> readable silhouette, worn edges, no weapon in hand. Neutral colors except a separate colored sash
> and hood lining. 1.8 m tall, game-ready, clean topology for rigging.

---

## 2. Pillar — `arena/pillar`

**File:** `Assets/Models/arena/arena-pillar.glb`

**Used for:** the 8 tall pillars, the main cover. Players hide behind them, so the silhouette must
fully fill the bounds (no see-through gaps).

**Size / bounds:** x −0.6 … 0.6, y 0 … 4.0, z −0.6 … 0.6 (1.2 × 4.0 × 1.2 m). The shaft can be
narrower than 1.2 m only if base and capital fill the full width. **Pivot:** bottom centre.

**Budget:** 1 000–3 000 triangles, one material, 1024 textures (base, normal, metallic-roughness).

**Details:** square-based ancient stone column: stepped base, slightly chipped shaft, simple capital,
cracks and missing corners, a faint carved rune band at eye height (1.5–1.8 m). Warm grey-beige
stone with darker grime in crevices.

**Prompt:**
> An ancient square stone column from a ruined arena, 4 m tall, 1.2 m wide base. Stepped plinth,
> chipped weathered shaft, simple carved capital, cracks and broken corners, a faint band of carved
> runes at head height. Warm grey-beige stone with dark grime in the cracks. Stylized-realistic,
> chunky, game-ready, single material.

---

## 3. Crate — `arena/crate`

**File:** `Assets/Models/arena/arena-crate.glb`

**Used for:** 6 crates, low cover (crouch behind it, jump onto it).

**Size / bounds:** 1.2 × 1.2 × 1.2 m (x −0.6 … 0.6, y 0 … 1.2, z −0.6 … 0.6). A full box shape:
the top is walkable and flat. **Pivot:** bottom centre.

**Budget:** 300–1 000 triangles, one material, 1024 textures.

**Details:** heavy wooden supply crate with iron-banded edges and corners, dark weathered planks, a
few nails, one side with a burnt arcane stamp (rune, not text). Top surface flat.

**Prompt:**
> A heavy wooden supply crate, 1.2 m cube, dark weathered planks with iron-banded edges and corner
> brackets, rivets, a faded burnt rune stamp on one side. Flat top. Stylized-realistic dark fantasy,
> game-ready, single material.

---

## 4. Central platform — `arena/platform`

**File:** `Assets/Models/arena/arena-platform.glb`

**Used for:** the raised block in the arena's centre, the high ground everyone fights over.

**Size / bounds:** 6.0 × 1.2 × 6.0 m (x −3 … 3, y 0 … 1.2, z −3 … 3). The top must be **flat and
walkable** across the whole 6 × 6 m. Sides may be slightly sloped inward but no overhang beyond the
bounds. **Pivot:** bottom centre.

**Budget:** 1 500–4 000 triangles, one material, 2048 textures.

**Details:** a square stone dais of large fitted blocks, worn edges, a circular inlaid rune circle on
the top surface (shallow, flat, glowing faintly cyan-violet), steps carved into one side are NOT part
of this model (stairs are item 11).

**Prompt:**
> A square ancient stone dais, 6 m wide and 1.2 m high, built from large fitted stone blocks with worn
> chipped edges. Flat walkable top with a shallow inlaid circular rune pattern that glows faintly
> cyan-violet. Warm grey-beige stone, moss-free, dusty. Stylized-realistic, game-ready, single material.

---

## 5. Stone floor material — `arena/stone-floor`

**Files:** `Assets/Models/materials/stone-floor_basecolor.png`, `_normal.png`, `_roughness.png`
(optional `_ao.png`), **2048 × 2048, seamless (tileable)**.

**Used for:** the 40 × 40 m arena floor; one texture tile covers **2 × 2 m**.

**Details:** worn flagstones of mixed sizes (40–80 cm), dark mortar gaps, scattered small cracks, dust
in the joints. No large unique features (they repeat visibly every 2 m).

**Prompt:**
> Seamless tileable texture of worn ancient flagstones, mixed sizes 40 to 80 cm, dark mortar joints,
> fine cracks and dust, warm grey-beige stone, top-down, even lighting, no shadows. PBR set: base
> color, normal, roughness.

## 6. Stone wall material — `arena/stone-wall`

**Files:** `Assets/Models/materials/stone-wall_basecolor.png`, `_normal.png`, `_roughness.png`,
**2048 × 2048, tileable horizontally** (vertically it spans the 4 m wall height once).

**Used for:** the four 40 m × 4 m arena walls; one tile covers 4 m wide × 4 m high.

**Details:** large ashlar blocks, a darker weathered band near the ground, a carved rune frieze at
about 3 m height, cracks and a few missing chips. Flat-on view.

**Prompt:**
> Tileable texture of a weathered ancient stone wall from large ashlar blocks, darker grime near the
> ground, a carved band of runes near the top, cracks and chipped edges, warm grey-beige stone, front
> view, even lighting. PBR set: base color, normal, roughness.

---

## 7. Wall segment — `arena/wall-segment`

**File:** `Assets/Models/arena/arena-wall-segment.glb`

**Used for:** building the next arenas' outer walls from pieces placed side by side.

**Size / bounds:** 4.0 m wide (x −2 … 2), 4.0 m high (y 0 … 4), 0.6 m thick (z −0.3 … 0.3).
Left and right ends must line up seamlessly with copies of itself. **Front** (+Z) is the side that
faces into the arena. **Pivot:** bottom centre.

**Budget:** 800–2 000 triangles, 2048 textures (can reuse the stone-wall material set).

**Prompt:**
> A modular ancient stone wall section, 4 m wide, 4 m high, 0.6 m thick, large ashlar blocks with a
> carved rune frieze near the top, weathered and chipped, ends that tile seamlessly with copies.
> Stylized-realistic dark fantasy, game-ready.

## 8. Low wall — `arena/low-wall`

**File:** `Assets/Models/arena/arena-low-wall.glb`

**Used for:** waist-high cover: crouch behind it to hide, stand to cast over it.

**Size / bounds:** 3.0 × 1.0 × 0.6 m (x −1.5 … 1.5, y 0 … 1.0, z −0.3 … 0.3). Solid, no holes.
**Pivot:** bottom centre. **Budget:** 500–1 500 triangles, 1024 textures.

**Prompt:**
> A low ruined stone wall, 3 m long, 1 m high, 0.6 m thick, fitted blocks with a broken uneven top
> edge, cracks, warm grey-beige weathered stone. Stylized-realistic, game-ready, single material.

## 9. Broken pillar — `arena/broken-pillar`

**File:** `Assets/Models/arena/arena-broken-pillar.glb`

**Used for:** a shorter variant of the pillar for variety and mid-height cover.

**Size / bounds:** 1.2 × 2.2 × 1.2 m (x −0.6 … 0.6, y 0 … 2.2, z −0.6 … 0.6). Same base as item 2,
snapped off with a jagged top. **Pivot:** bottom centre. **Budget:** 1 000–3 000 triangles, 1024.

**Prompt:**
> The lower half of an ancient square stone column, snapped off with a jagged broken top at about
> 2.2 m, stepped plinth, cracks, warm grey-beige weathered stone. Matches a 4 m column of the same
> style. Stylized-realistic, game-ready.

## 10. Rubble pile — `arena/rubble`

**File:** `Assets/Models/arena/arena-rubble.glb`

**Used for:** decoration at the base of walls and pillars; too low to block (players walk over or
around it).

**Size / bounds:** 2.0 × 0.5 × 1.5 m (x −1 … 1, y 0 … 0.5, z −0.75 … 0.75). **Pivot:** bottom
centre. **Budget:** 500–1 500 triangles, 1024.

**Prompt:**
> A low pile of broken stone blocks and fragments, 2 m wide, 0.5 m high, warm grey-beige weathered
> stone, dust. Stylized-realistic, game-ready, single material.

## 11. Stairs — `arena/stairs`

**File:** `Assets/Models/arena/arena-stairs.glb`

**Used for:** reaching the 1.2 m platform without jumping. The engine climbs steps up to **0.35 m**,
so **each step must be ≤ 0.30 m high**.

**Size / bounds:** 3.0 m wide (x −1.5 … 1.5), 1.2 m high (y 0 … 1.2), 2.4 m deep (z −1.2 … 1.2):
**4 steps of 0.30 m**, each 0.6 m deep. The low end faces **+Z**, the top step meets the platform
at −Z. **Pivot:** bottom centre. **Budget:** 500–1 500 triangles, 1024.

**Prompt:**
> Wide ancient stone stairs, 3 m wide, four steps each 0.3 m high and 0.6 m deep, rising to 1.2 m,
> worn chipped step edges, warm grey-beige stone. Stylized-realistic, game-ready, single material.

## 12. Brazier — `arena/brazier`

**File:** `Assets/Models/arena/arena-brazier.glb`

**Used for:** fire bowls along the walls; the game adds the flame and light at the top (leave the bowl
empty or with coals only, no modeled flames).

**Size / bounds:** 0.8 × 1.4 × 0.8 m (x −0.4 … 0.4, y 0 … 1.4, z −0.4 … 0.4). Put an empty child
node named **`flame`** at the centre of the bowl, where the fire will be attached. **Pivot:** bottom
centre. **Budget:** 800–2 000 triangles, 1024.

**Prompt:**
> A dark iron fire brazier on a three-legged stand, 1.4 m tall, wide shallow bowl filled with glowing
> coals, aged bronze trim with small rune engravings. Stylized-realistic dark fantasy, game-ready.

## 13. Spawn circle — `arena/spawn-circle`

**File:** `Assets/Models/arena/arena-spawn-circle.glb`

**Used for:** marking the 8 spawn points on the floor. Flat: it lies on the floor, nothing to trip on.

**Size / bounds:** 2.0 × 0.02 × 2.0 m (x −1 … 1, y 0 … 0.02, z −1 … 1). A single flat decal-like
mesh slightly above the floor. The rune pattern's "front" arrow points to **+Z** (the direction a
player faces after spawning). **Pivot:** centre on the floor. **Budget:** under 500 triangles,
1024 texture with **alpha** (transparent outside the circle), emissive cyan-violet glow.

**Prompt:**
> A flat circular arcane rune sigil, 2 m across, glowing cyan-violet lines on a transparent
> background, with one arrow-shaped rune pointing forward. Top-down, for a floor decal.

## 14. Banner — `arena/banner`

**File:** `Assets/Models/arena/arena-banner.glb`

**Used for:** hanging on the arena walls for color and to tell sides apart.

**Size / bounds:** 1.2 m wide (x −0.6 … 0.6), 3.0 m high (y 0 … 3.0), 0.3 m deep (z −0.15 … 0.15),
including the top pole. **Pivot:** bottom centre (the game hangs it on the wall). **Front:** +Z (the
side facing into the arena). One separate **accent** material on the cloth emblem so the game can
recolor it. **Budget:** 300–1 000 triangles, 1024.

**Prompt:**
> A tall hanging cloth banner on a horizontal wooden pole, 1.2 m wide, 3 m long, weathered dark fabric
> with frayed bottom edge and a simple abstract arcane emblem (no text). Stylized-realistic dark
> fantasy, game-ready.

---

## 15. App icon — `branding/app-icon`

**File:** `Assets/Branding/AppIcon.png` (replace the placeholder made by
`Tools~/make-placeholder-icon.cjs`; keep the file name, the project already uses it as the icon of
every platform).

**Used for:** the game's icon on iPhone, Android and Windows, and in the App Store / Google Play.

**Format:** 1024 × 1024 PNG, **RGB without transparency** (the App Store rejects icons with an
alpha channel), square corners (iOS and Android round them themselves), no text, readable at
60 × 60 pixels: one bold shape, strong contrast. Keep the important part inside the centre 80 %
(Android crops adaptive icons to a circle or squircle).

**Prompt:**
> A game app icon for a dark fantasy arena spell-fighting game: a glowing orange fireball held
> between two stylized mage hands, inside a thin golden rune circle, on a dark blue-grey stone
> background. Bold silhouette, high contrast, painterly but clean, no text, no border, square
> 1024×1024, readable at small sizes.

---

## 16. VR hands — `characters/vr-hands`

**Files:** `Assets/Models/characters/vr-hand-left.glb`, `vr-hand-right.glb`.

**Used for:** the player's own hands in VR (Meta Quest), shown where the controllers are, instead
of today's grey spheres. Only the local player sees them; other players see the mage.

**Size and orientation:** a real adult hand in a mage's leather glove (palm about 9 × 10 cm,
fingers 8 cm), **front +Z = where the controller points** (the fingers point along +Z when the hand
is open), +Y up (back of the hand), origin at the **centre of the palm** where a controller is
held. Left and right are mirror images.

**Budget:** 3,000 triangles each, one 1024 texture set shared by both. Rigged (wrist and three
bones per finger) with clips `Idle` (relaxed), `Grip` (closed around a controller), `Point` (index
out), `Cast` (open, fingers spread); no root motion.

**Prompt:**
> A pair of stylized fantasy mage hands in dark leather fingerless gloves with small glowing
> orange runes on the back, for a VR game, low poly but clean, PBR, rigged, matching a robed
> fire mage.

---

## Checklist per delivered model

- [ ] GLB, one file per asset, named as above, in `Assets/Models/<group>/`
- [ ] Real size matches the bounds (check the bounding box in a viewer)
- [ ] Origin at bottom centre, +Y up, front +Z, transforms applied
- [ ] Triangle count inside the budget; textures power of two and embedded
- [ ] Character: humanoid rig, all clips named exactly as listed, no root motion
- [ ] Character: no vertex colors, one shared vertex set across materials. If a delivered file breaks
      either rule, `node tools/glb-fix-skinned.cjs in.glb out.glb --strip-colors` (in the workspace
      root) fixes it without changing how it looks
- [ ] Tell Claude which files arrived; it connects each one to its asset id and checks it in the arena
