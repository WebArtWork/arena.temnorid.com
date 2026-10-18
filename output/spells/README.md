# Elemental spell art

All four starter spells now share layered emission, a clear primary silhouette, secondary
wisps or particles, and a short fade rather than opaque placeholder geometry.

`elemental-spells.blend` contains separate FIRE, FROST, SHIELD and BLINK scenes. The fire
scene is appended from the accepted `fire/fire-study.blend`. Shield includes the existing
mage as a scale and transparency reference. The Blender bridge was disconnected during
this pass; the installed Blender 5.2 executable authored and rendered the saved studies.

- `fire/fireball-blender.png`: previously reviewed fire study.
- `frost-nova-blender.png`: a freezing wave with shards and low vapor.
- `arcane-shield-blender.png`: a transparent membrane with flowing violet arcs.
- `blink-blender.png`: tapering displacement wisps and suspended residual motes.

Rebuild the new Blender studies with `tools/build_elemental_studies.py`. These are editable
art studies, not exported Unity meshes. The actual gameplay effects use the matching
procedural meshes and particles in `ElementalSpellVisual.cs` and `FireSpellVisual.cs` in
the spells package. Blender and Unity shading are separate implementations; the images
ending in `-unity.png` are the actual Unity effect renders, not Blender renders.

Unity validation: run the installed editor with `-batchmode -projectPath <project>`
`-executeMethod ElementalSpellReview.Validate -quit -logFile <log>`. It checks all four
effects render nonblank, shaders compile, cosmetic effects have no colliders, transient
geometry fades and the shield follows its caster. `validation.txt` is written only on
success. Full match and target-device visual/performance review remain outstanding.
