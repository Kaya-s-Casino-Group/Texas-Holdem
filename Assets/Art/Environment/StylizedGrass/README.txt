StylizedGrass — opaque grass and reference terrain

Scene: Assets/Art/Environment/StylizedGrass/Scenes/StylizedGrassTest.unity
Controls: StylizedGrass root > StylizedGrassManager
Tools: Tools > Scene Art > StylizedGrass
Local captures/reports: ArtSource/StylizedGrass

The original opaque grass scheme has been restored. Source/Models contains the
original grass FBX; normalized near/far meshes contain 144 / 48 triangles.
The dark red palette, wind, lodging, seed, spacing and height range are restored
from the values recorded before the rejected transparent Blender experiment.
That experiment's shaders, mask atlases, controls and setup tools were removed.
No package rocks or rendering configuration assets are used.

StylizedGrass/Opaque Grass is a handwritten instanced URP shader. GBuffer, Forward,
ShadowCaster, DepthOnly, DepthNormals and Meta passes share the original HLSL.
Wind and root-fixed lodging remain available from the manager. Ground material,
source textures and shared HLSL were retained without changes.

Main-camera LOD remains scene-local. Spatial cells choose the near/far mesh,
stable density thinning and distance culling from the configured LOD camera,
falling back to Camera.main. Other cameras only control their own frustum culling;
they cannot replace main-camera statistics or drive LOD. Each cell uses
Graphics.DrawMeshInstanced in batches of at most 1023, without per-tuft objects.
The restored opaque renderer needs no transparent sorting or shared-topology workaround.

The 22 x 24 m ground has been reshaped after the reference composition:
- Finite, oblique mounds overlap across the foreground, middle and background.
- There is no road, central channel or continuous bare-ground exclusion mask.
- Foreground grass has higher coverage and up to 1.3x tuft scale, with small isolated gaps.
  This emphasis uses fixed terrain coordinates; camera motion does not regenerate grass.

Runtime/StylizedGrassTerrainShape.cs defines the shared height and coverage.
Its composition axes are fixed to the existing view; moving a camera does not
move the terrain. Ground vertices, collider and grass placement use the same
height function. The ground mesh asset retains its GUID. Ground material and
its scene-local overrides remain unchanged. No package rocks are used.
The foreground now has a 0.28m lift tapering out between 2m and 10m from the fixed design camera.
Four independently editable painted background layers live under StylizedRuins Backdrop;
their textures, materials and shader are in Assets/Art/Environment/StylizedRuins.

Use Rebuild Reference Terrain after editing the height/coverage function.
Restore Opaque Grass and Shape Terrain restores the recorded pre-migration grass
values while retaining the current ground material overrides, LOD, camera,
lights and renderer configuration. The older Build in Current Scene and palette
tools rebuild the whole test scene; use the manager for ordinary adjustments.

Validation:
- Validate and Capture: actual Deferred mode, instancing, shader errors and all six passes.
- Validate Main Camera LOD: detail/density reduction, far mesh, secondary-camera
  independence, distance culling, manager controls and unchanged material assets.
- Validate Reference Terrain: shared height sampling, collider, overview capture.

Current rendering uses the existing MainURP / CharacterRenderer configuration,
including 480p/Point scaling and existing post effects. Validation measures
geometry submissions and rendered output, not GPU frame time.

Terrain editor:
- Open Tools > Scene Art > StylizedGrass > Terrain Editor, or the manager Inspector button.
- Current scene is connected to TerrainData/StylizedGrassTerrain.asset (129 x 129 samples).
- Enable Scene brush, then left-drag: Raise / Lower / Smooth / Flatten / Add Grass / Erase Grass / Restore.
- Shift reverses raise/lower and add/erase. Ctrl or Command + click samples the flatten height.
- Alt keeps Scene navigation; Escape disables the brush. Standard Undo/Redo updates mesh, collision and grass.
- Each stroke saves paint data and the derived mesh. Save Terrain and Scene also saves scene references.
- Restore locally removes brush changes, returning to the procedural reference surface and density.
- Height offsets and signed density influence are bilinearly sampled in the common ground/field local space.
- Ground vertex red and grass coverage share the painted density, including exact zero/full coverage.
- Mesh/collider refresh is throttled to 0.12s while dragging and finalized on release. Grass retains instancing and main-camera LOD.
- Current 22 x 24m mesh topology/materials, background cards, camera and renderer settings are not replaced.
- Rebuild Reference Terrain preserves the paint data. Do not use the legacy full-scene Build command to refresh paint.
- Validate Terrain Editor exercises brushes, undo/redo, save/reimport, collision, grass erase and soil blend on temporary data.
