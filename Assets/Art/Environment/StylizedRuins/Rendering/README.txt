StylizedRuins current scene: horizontal painted floor + standing image cards

StylizedRuinsForwardRenderer.asset is a separate plain Forward renderer, with no Renderer Features.
The active scene has no grass manager, grass field, terrain mesh or runtime projection controller.
Existing reusable grass/terrain project assets are retained, but are not instantiated in this scene.
User-added test actors are retained.

StylizedRuins Ground Card is a flat horizontal two-triangle mesh at world Y=0.
Its UVQ coordinates contain the inverse perspective mapping from the fixed design camera.
The backdrop shader divides UV.xy by Q before sampling, compensating for floor foreshortening.
It is not a camera-facing ground billboard and does not squash the image vertically.
GroundPerspective_Before/After and flat-ground-reprojection.json verify the isolated floor image before/after conversion.
Changing the camera or moving this ground requires deliberate re-authoring of the fixed-camera mapping.

The separate foreground group contains left broken arch/sword, right wall/chain, and low rubble cards.
StylizedRuins Midground contains three staggered low rubble/short wall groups, leaving open sight lines.
Distant backdrop layers are moved farther along the camera rays and scaled proportionally, retaining their projected framing.
The camera far clip includes these layers. No per-card queue offsets are used.
RuinsFront uses a world-height alpha blend near the floor, with stationary uneven edges. Its redundant painted soil base fades out.
The horizontal floor, soft contact region and middle rubble together bridge the foreground to distant ruins.
This is local alpha blending, not background blur or full-screen fog.

Tools > Scene Art > StylizedRuins:
Capture Cards Only: current scene preview.
Validate Cards Only / Validate Flat Ground: verify scene cards and horizontal ground.
Blend Ruins Into Flat Ground: set up the world-height seam blend and missing midground occluders.
Lay Ground Flat Preserving Picture: one-time conversion from a vertical ground card with image comparison.
Old Painted Ground Transition / Fix Ground Aspect setup commands belong to the superseded terrain workflow.

New assets generated using built-in image_gen:
Textures/T_StylizedRuins_ForegroundLeft.png
Textures/T_StylizedRuins_ForegroundRight.png
Textures/T_StylizedRuins_ForegroundRubble.png
Textures/T_StylizedRuins_GroundCard.png
Textures/T_StylizedRuins_MidgroundRubble.png
Prompts: ArtSource/StylizedGrass/{ForegroundPrompts,GroundCardPrompt,MidgroundPrompt}.txt
