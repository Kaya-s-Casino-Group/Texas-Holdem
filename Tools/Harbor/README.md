# Harbor material-group GI meshes

Editor menu: `Tools/Harbor/Material GI`.

1. `1 Export UV0 Groups` reads active Contribute GI renderers in the SingleBattle Harbor root. Exact material assets are grouping keys. Shader-deformed flags and reflective water remain separate. Renderer settings must agree; property blocks, mirrored transforms, multi-material meshes and unhandled behaviours stop the operation.
2. Run `pack_material_gi.py` with Python, numpy and `xatlas==0.0.11`. It preserves UV0 island shapes, isolates overlapping faces, reprojects collapsed UV faces, separates stacked instances, then packs UV1. Water/seabed with wholly collapsed UV0 use a planar projection. Original geometry and UV0 are untouched.
3. `2 Apply Validated UV1` checks input fingerprints and per-triangle attributes before changing the scene. A copy of the current scene, including unsaved edits, is saved under `output/harbor-material-gi`. New mesh assets go to `Assets/Art/Environment/Harbor/Meshes/MaterialGI`. Merged source renderers are disabled and lose ContributeGI, but their objects, colliders and original mesh assets remain. Single objects retain their transforms and components.
4. `3 Verify Applied Meshes` exports actual imported mesh data for comparison. The current run's `verified-result.json` confirms all 18 groups and 239799 triangles match in geometry, UV0, vertex-attribute hashes, materials and validated UV1.

The generator packs with 5-pixel chart padding and adds an explicit 4-pixel outside border in the packing canvas; canvas dimensions are recorded per group. These are normalized UV margins, **not a promise of 4 pixels in the eventual Unity lightmap allocation**. Lower lightmap resolution or smaller renderer allocation requires rechecking bake margins. Float32 UV overlap validation uses intersection area > 1e-10 UV units; shared edges and numerical slivers below this tolerance are ignored.

Existing baked lightmap bindings on changed renderers are invalidated. **Rebake lighting after applying**; this workflow does not start a bake or delete the old lightmap textures. The existing scene's unrelated edits are retained. Re-applying is blocked once GI_Merged exists; restore the saved scene copy (and use a fresh output asset directory) before rebuilding. The merge/apply step does not rewrite the source prefab. Use `4 Update Harbor Prefab` to apply the verified scene instance to the original prefab, preserving its GUID and first backing up the previous prefab. This step verifies hierarchy, transforms, mesh/material references, components and renderer settings, then saves the scene.

Diagnostics, cached inputs and the before/after UV image are under `output/harbor-material-gi`; they are not runtime dependencies. The Unity mesh assets contain the final UV1.
