# Fire spell review

`fire-study.blend` is an editable Blender shape study: layered, animated flame tongues,
noise-driven emission and drifting embers. The original Blender scene is preserved in a hidden
collection. Rebuild with `tools/build_fire_study.py` in Blender. This is an art study, not a
fluid simulation or a GLB intended to replace fire with a solid mesh.

The game uses `Packages/temnorid-arena-spells/Runtime/FireSpellVisual.cs` and the
Resources shader `TemnoridFireParticle.shader`: particle core, world-space flame wake,
embers, flickering light and a surface impact burst with cooling smoke. The Blender
study and Unity effect are separate implementations; the Blender material is not exported.

Unity review: run the installed Unity editor with `-batchmode -projectPath <project>`
`-executeMethod FireSpellReview.Validate -quit -logFile <log>`.
This compiles the project and renders `fireball-unity.png` and `impact-unity.png`.
Gameplay damage, speed, range, mana and collision radius are unchanged.
Full match, phone and headset performance checks are still required.
