# Shared-platform model realism pass

This pass updates the actual game assets in `Assets/Models`: the mage, all eleven arena
models, and both stone PBR surface sets. The dark-fantasy designs, established dimensions,
walkable surfaces, skeleton, animation names and recolorable accents are preserved.

Editable source: `models.blend`. Each asset has its own collection, at the original game
pivot. The mage is visible by default; toggle the other asset collections to work on props.
All textures are packed. Original generated GLBs and replaced material textures are retained
in the ignored `before/` folder. The older mage and arena Blender sources are unchanged.

## Changes

- Stone: irregular bevels within collision bounds, mineral variation, pores, roughness and
  local crevice darkening. Floor/wall maps have worn mortar, sparse fractures and fine relief.
- Wood and metal: directional wood grain, less uniform color/roughness, metal pitting and
  small patina patches. The brazier bowl shades smoothly rather than as a faceted cone.
- Cloth and leather: baked material grain, roughness variation, small compression folds,
  and banner creasing. Banner and mage keep separate `accent` materials for runtime tinting.
- Mage: fused palm/finger-joint surfaces with transferred skin weights. The existing
  approximately 64,000-triangle model is reduced to **24,500 triangles**, with **52 bones**
  and **17 animation clips**. Its existing face, costume silhouette and procedural motions
  remain stylized; this is not a new realistic character sculpt or animation replacement.
- Exports: PBR detail is baked into embedded images rather than depending on procedural
  Blender nodes or vertex color streams. The mage uses two materials and shared skin vertex
  buffers for glTFast. Two degenerate tangent vectors were repaired before validation.
- Unity floor/wall shaders now use linear occlusion and packed smoothness maps rather
  than a constant smoothness value.

## Review and validation

`model-preview-sheet.jpg` contains actual Unity URP renders of the final exported assets.
The individual `*-unity.png` files are Unity renders; plain model-name PNGs are Blender
studio renders. `mage-team-cyan-unity.png` checks the same material-property tint path used
by combatants. The previews have different studio lighting from the original asset sheet.

- `gltf-validation.json`: all twelve GLBs, mesh/material budgets, bounds, embedded maps,
  no vertex colors, shared skin buffers and Khronos validation.
- `mage-import-check.json`: Blender re-import, normalized skin weights, rest dimensions,
  52 bones, 17 clips and 85 sampled poses without nonfinite/exploding geometry.
- `../arena/import-check.json`: all eleven prop imports; collision bounds, walkable crate
  and platform tops, stair heights and the brazier's flame attachment.
- `texture-validation.json`: 2K dimensions, required seamless edges and roughness-to-
  smoothness alpha packing for the stone surfaces.
- `unity-validation.txt`: actual Unity imports, shader availability, material/clip counts,
  nonblank renders, packed stone maps and team accent recoloring.

This is a common asset set designed around the original geometry budgets. It has not been
profiled on physical phones or Quest. A full match, device texture-memory measurements,
LOD tuning, close facial/hand sculpting and animation polish remain production work.

## Rebuild

Run `tools/refine_models_realism.py` in Blender, then `tools/finalize_realism_bounds.py`.
The former reads the preserved original Blender sources and writes the game GLBs; the
latter enforces the authored envelopes and organizes the combined editable source.
Then run, in order:

```powershell
node tools/glb-fix-skinned.cjs Assets/Models/characters/mage.glb Assets/Models/characters/mage.glb --strip-colors
node tools/fix_realism_gltf.cjs
node tools/validate_realism.cjs
```

Use Blender for `tools/check_realism_import.py` and `tools/check_arena_import.py`.
Run Unity with `-batchmode -projectPath <project> -executeMethod ModelRealismReview.Validate
-quit -logFile <log>` for the actual game import/material review. Generate the contact sheet
with `tools/make_realism_preview.py` using Python with Pillow. The texture check also needs
NumPy. The pinned glTF validator is installed from `output/mage/validation/package-lock.json`.
