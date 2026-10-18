# Mage — live Blender review

This candidate was edited through Blender MCP. The game's existing mage.glb is unchanged
by this second pass. Review the separate mage-review.glb before integrating it.

Changes: rounded open hood, asymmetric shoulder mantle, continuous folded sleeves,
belt-gathered tunic, two longer split coat tails, muted charcoal/indigo materials,
subdued skin and short stubble. The coat tails blend hips and upper-leg weights.

The source mesh was reduced to approximately 80,000 triangles. All vertices have
weights; the original 52 bones and 17 animation actions remain. Four walking frames
were visually inspected in Blender. The exported GLB also passed a Blender re-import
check for one skinned mesh, 52 bones, 17 clips and no unweighted vertices (import-check.log).
Full animation, Unity import, and performance
checks are still required. Face and hands retain stylized anatomy.

Files:
- mage-v2-final.blend: final candidate with camera and lighting for review.
- mage-final-review.blend: earlier checkpoint before the final neckline adjustment.
- mage-review.glb: separate animated candidate, excluding studio objects.
- mage-three-quarter.png, mage-front.png, mage-back.png: actual mesh renders.
- mage-before-live.blend: snapshot of the starting character.

Rebuild: load output/mage/mage.blend, then run tools/refine_mage_live.py in a
subsequent Blender invocation/MCP call, then run tools/render_mage_review.py. The
refinement script reduces the joined mesh to 80,000 triangles. Loading a file and
applying modifiers within the same MCP call can leave Blender's context stale.
