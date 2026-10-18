# Temnorid Arena assets: first production pass

**Current game revision:** the shared-platform realism pass is documented in
`../realism/README.md`. Its editable source is `../realism/models.blend`, with current
Unity previews and validation reports alongside it. This folder's original source and
studio renders remain the earlier checkpoint described below.

All 13 remaining entries from `models-to-do.md` are supplied: eleven GLB models
and two stone PBR texture sets. These are procedural stylized assets matching
the approved mage prototype's level of detail. They have not yet been connected
to the engine's asset catalog or tested inside Unity.

The complete contact sheet is `asset-preview-sheet.jpg`. Individual PNGs render
the actual exported geometry and shaders. `arena-assets.blend` is editable source;
it contains the separate model objects at their original dimensions, without
the studio's cameras, lights or ground.

| Asset ID | Delivery | Triangles |
|---|---|---:|
| `arena/pillar` | `Assets/Models/arena/arena-pillar.glb` | 1796 |
| `arena/crate` | `Assets/Models/arena/arena-crate.glb` | 550 |
| `arena/platform` | `Assets/Models/arena/arena-platform.glb` | 2502 |
| `arena/wall-segment` | `Assets/Models/arena/arena-wall-segment.glb` | 1536 |
| `arena/low-wall` | `Assets/Models/arena/arena-low-wall.glb` | 604 |
| `arena/broken-pillar` | `Assets/Models/arena/arena-broken-pillar.glb` | 1162 |
| `arena/rubble` | `Assets/Models/arena/arena-rubble.glb` | 836 |
| `arena/stairs` | `Assets/Models/arena/arena-stairs.glb` | 1120 |
| `arena/brazier` | `Assets/Models/arena/arena-brazier.glb` | 1456 |
| `arena/spawn-circle` | `Assets/Models/arena/arena-spawn-circle.glb` | 12 |
| `arena/banner` | `Assets/Models/arena/arena-banner.glb` | 548 |
| `arena/stone-floor` | `Assets/Models/materials/stone-floor_{basecolor,normal,roughness}.png` | — |
| `arena/stone-wall` | `Assets/Models/materials/stone-wall_{basecolor,normal,roughness}.png` | — |

## Conventions and checks

- GLB/glTF 2.0, metres, identity object transforms, +Y up and +Z front.
- Every model has a bottom-centre pivot and dimensions matching the brief.
- Props use one PBR material and embedded basecolor, normal, metallic-roughness
  and emissive textures. Banner also has a separate material named `accent`.
- Normal-map tangents are exported explicitly.
- Crate and platform have continuous flat tops. The platform rune pattern is flat.
- Stairs have four 0.30 m rises and 0.60 m treads; the low end faces +Z.
- Brazier has a child named `flame` at y = 1.25 m, with coals but no modeled fire.
- Spawn circle is a 2 cm-thick decal mesh with embedded 1024 alpha texture,
  emission and a +Z arrow. Transparent regions are preserved in the GLB.
- Floor and wall maps are 2048 square. Floor repeats every 2 × 2 m and tiles in
  both directions; wall repeats every 4 m horizontally, with one tile over its
  4 m height. Wall basecolor has bottom grime and a rune frieze near 3 m.
- Khronos validation, dimensions, budgets, embedded images, root transforms,
  helper parenting, recolor material and alpha checks: `gltf-validation.json`.
- PNG size and pixel-edge checks: `texture-check.json`.
- Re-imported geometry, walkable top surfaces, all step heights and the flame
  attachment: `import-check.json`.

## Import and remaining integration

Import GLBs with the project's glTFast package and map them to the IDs above in
the asset catalog. Let Unity generate GLB importer metadata on first import;
the package cache is not present in this checkout, so importer script GUIDs
have not been guessed. Texture import metadata sets the normal maps to Normal
Map and disables sRGB for roughness maps.

Use basecolor in sRGB; normal and roughness are linear. These are separate
roughness images, not Unity smoothness images: convert roughness to 1 − roughness
when assigning to a shader that expects smoothness. Wall UVs should place v=0
at the ground and v=1 at the top.

Game collision boxes remain authoritative. Confirm glTFast/URP vertex colors,
transparent decal rendering, banner recoloring, model orientation and shader
emission in an actual arena session before treating the set as gameplay-tested.
This pass has simpler weathering and ornamentation than the mage concept image.

## Rebuild

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --python tools/build_arena_assets.py
```

Add `-- --no-render` to skip previews. Render contact sheet and check texture
edges with `tools/make_asset_preview.py` using Python with Pillow.
Install validator dependencies with `npm ci --prefix output/mage/validation`,
then run `node tools/validate_arena_assets.cjs` and `node tools/validate_mage.cjs`.
