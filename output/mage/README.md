# Mage: realism review pass

**Current game revision:** see `../realism/README.md`, `../realism/models.blend` and
`../realism/mage-unity.png`. The shared-platform mesh is 24,500 triangles with baked PBR
maps, two materials, no vertex colors, 52 bones and 17 retained clips. This folder's source
and renders remain an earlier checkpoint, described below.

Game asset: `Assets/Models/characters/mage.glb`.
Editable Blender source: `output/mage/mage.blend`.
Actual mesh renders: `mage-front.png`, `mage-three-quarter.png`, `mage-back.png`.

This is a locally authored procedural prototype inspired by the concept image,
not an image-to-3D reconstruction. The face, cloth and surface detail are substantially
simpler than the concept. The asset is referenced by ArenaPlayer and ArenaBot;
this revision has not been visually tested in Unity.

- 1.80 m rest height, 0.69 m total rest width, approximately 0.346 m depth.
- Bottom-centre pivot; glTF +Y up and +Z front; identity root transforms.
- Approximately 64,000 triangles; 52 bones using Mixamo-style names.
- Seven materials: wool, leather, skin, bronze, hair, eyes and recolorable `accent`.
- Embedded 2048-square microfabric basecolor and surface normal textures with vertex-color palette.
- All 17 requested named animation clips, sampled at 30 fps.

## Review changes

Continuous remeshed facial anatomy replaces the visibly intersecting cheeks, jaw and nose.
Smaller eyes, eyelids and swept hair reduce the exaggerated facial proportions. Garments
have more vertical geometry for rounded compression folds. Separate PBR materials give
bronze, leather, wool and skin different roughness and surface relief. Accent cloth no
longer emits light. `mage-before.png` preserves the previous three-quarter render.

The Blender GLB round-trip check passes for mesh, bones, clips and dimensions. Unity
appearance and animation deformation still need an in-editor review. This pass raises
the triangle count and embedded texture size; optimize after the art direction is accepted.
The existing Khronos validation report predates this pass and is not a validation of it.

## Remaining production work

- Replace the simplified face and hair with a sculpted character likeness.
- Sculpt believable cloth folds, worn leather and seam details, then bake a full
  character texture atlas with normal and metallic-roughness maps. Current vertex-color
  rendering should also be confirmed with the project's glTFast/URP configuration.
- Polish skinning and cloth panel weights; examine shoulders, elbows, hips and fingers
  through the full range of motion. Some parts intentionally overlap in this prototype.
- Replace procedural motions with authored or retargeted animations; locomotion timing
  and pose quality are not calibrated to the specified game movement speeds.
- Configure looping and Unity Humanoid mapping in the importing pipeline. glTF does not
  encode the Unity clip loop flag. Crouch height, foot contact, jump and death poses need
  gameplay verification. Locomotion has no horizontal root travel; crouch and death
  intentionally animate the hips vertically as pose changes.

## Rebuild

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --python tools/build_mage.py
```

Add `-- --no-render` to export and save without studio rendering.
The studio camera, lights and floor are added after export and do not enter the GLB.
`build-report.json` records dimensions, clip names, triangle count and known limitations.
`gltf-validation.json` contains the Khronos glTF validator output.
