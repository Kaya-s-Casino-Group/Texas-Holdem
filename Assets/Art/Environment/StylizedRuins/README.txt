CURRENT SCENE: horizontal floor with baked perspective UVQ + separate foreground/midground/background cards.
No active grass/terrain controllers or custom renderer features. See Rendering/README.txt for the current workflow.
The sections below include earlier asset-creation history.

StylizedRuins — fixed-camera painted backdrops

Textures: separate original PNGs generated with built-in image_gen using the user's reference.
Sky is opaque. Mountains, Castle, RuinsFront and RuinsRear retain their generated alpha channels.
RuinsFront contains the foreground arches and rubble; RuinsRear contains distant silhouettes and fog.
The original combined Ruins texture is retained as source/reference, not used by the split scene.
Split prompts: ArtSource/StylizedGrass/RuinsSplitPrompts.txt
Source prompts: ArtSource/StylizedGrass/BackdropPrompts.txt

Scene root: StylizedRuins Backdrop
Default build layers: Sky (65m), Mountains (53m), Castle (43m), RuinsRear (36m), RuinsFront (31m), measured along main-camera forward.
In-place split preserves the existing front transform and adds the rear 4m farther from the camera, scaled to retain image registration.
Five two-triangle quads, world-space transforms, composed for 16:9 with side overscan.
No camera-follow script: these are fixed-view production cards, editable independently in the scene.

Shader: StylizedRuins/Backdrop, handwritten URP UniversalForwardOnly unlit alpha blend.
Premultiplies sampled RGB by alpha, preserves soft edges, depth-tests against existing 3D ground.
All materials inherit shader queue 3000 (no per-layer queue offsets), with default sorting layer/order/priority.
Castle, pillars and RuinsFront use alpha clipping and depth writes for solid per-pixel occlusion.
Sky, Mountains and RuinsRear retain soft alpha blending without depth writes and use camera-distance sorting.
Soft transparency sorts whole renderers; intersecting soft planes are not per-pixel order-independent transparency.
No shadows.
Instancing variants and material instancing are enabled. Five unique textures require five layer draws.
No project renderer, lighting, camera, or grass manager settings are installed by this asset.

Build/recompose: Tools > Scene Art > StylizedGrass > Build Reference Backdrop
Capture only: Tools > Scene Art > StylizedGrass > Capture Reference Backdrop
Rebuild repositions these five layers and restores their default materials; use capture only after manual composition edits.

Foreground lift: StylizedGrassTerrainShape.Height adds 0.28m near the fixed design camera,
smoothly tapering to zero between 2m and 10m. Terrain mesh, collider and instanced grass share the same height function.

Standalone stone pillar cards:
- Texture: Textures/T_StylizedRuins_Pillar.png, 1024x1536 RGBA, generated with built-in image_gen.
- Prompt: ArtSource/StylizedGrass/PillarPrompt.txt
- Shared material: Materials/M_StylizedRuins_Pillar.mat (instancing enabled, shader-default queue, solid cutout/depth writes).
- Reusable asset: Prefabs/PF_StylizedRuins_Pillar.prefab. Center pivot; manually place its foot into the ground.
- Current scene group: StylizedRuins Pillars, three independently editable world-space quads.
- Mid Left / Far Left / Mid Right use the existing camera at placement time and sample the painted terrain height.
- Add Stone Pillar Cards creates missing named slots only; existing placements/material adjustments are retained.
- These are visual occluders for the fixed camera, with alpha and depth testing, no physics collider or occlusion-culling volume.

Additional pillar variants (built-in image_gen):
- Slender: narrow, tall fractured column.
- Broad: wide stepped broken pier for broad occlusion.
- Leaning: diagonal fractured pillar.
- Overhang: attached projecting top slab for edge framing.
Each has its own T_ / M_ / PF_StylizedRuins_Pillar_<variant> asset.
The four prefab roots have a foot pivot; Painted Card child contains the shared two-triangle mesh.
At unit root scale the card is 6m tall, with its base slightly buried at root height.
Rotate each root to face the fixed camera, position its foot, then scale as needed.
All retain generated alpha and the original pillar's material grading and instancing setting.
Four distinct textures/materials are separate draw groups; repeated copies of the same variant can instance.
Import Stone Pillar Variants creates missing assets without changing current scene placement.
Prompts: ArtSource/StylizedGrass/PillarVariantPrompts.txt

In-place migration: Tools > Scene Art > StylizedGrass > Split Ruins and Use Depth
Validation: Tools > Scene Art > StylizedGrass > Validate Card Depth Sorting
The migration preserves terrain, grass, project rendering configuration and existing card placement.

Current painted-ground scene setup:
Rendering/README.txt describes the independent Forward renderer, projected ground painting,
surface-only depth/height atmosphere and soft terrain-to-card transition now used in StylizedGrassTest.
