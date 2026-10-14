# Mage: procedural first pass

Game asset: `Assets/Models/characters/mage.glb`.
Editable Blender source: `output/mage/mage.blend`.
Actual mesh renders: `mage-front.png`, `mage-three-quarter.png`, `mage-back.png`.

This is a locally authored procedural prototype inspired by the concept image,
not an image-to-3D reconstruction. The face, cloth and surface detail are substantially
simpler than the concept. It is suitable for evaluating the asset pipeline and silhouette;
it has not been wired into the game or tested in Unity.

- 1.80 m rest height, 0.69 m total rest width, approximately 0.346 m depth.
- Bottom-centre pivot; glTF +Y up and +Z front; identity root transforms.
- Approximately 23,500 triangles; 52 bones using Mixamo-style names.
- Two materials: `body` and recolorable `accent`.
- Embedded 2048-square microfabric basecolor texture with vertex-color palette.
- All 17 requested named animation clips, sampled at 30 fps.

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
