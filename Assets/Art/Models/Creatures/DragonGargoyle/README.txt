Dragon Gargoyle

Source: dragon+gargoyle+3d+model.glb (Tripo)
Converted through Blender MCP, 2026-10-05.

DragonGargoyle.fbx
  2,807 triangles, 1 mesh, 1 material, UVs, imported normals and tangents.
  Original size preserved: Unity X/Y/Z = 0.701172 / 0.998047 / 0.634766 units.
  Ground-center pivot, Y=0. Root scale 1 and rotation 0 after Unity import.
  Drag the FBX model directly from the Project window into a scene.

DragonGargoyle_Material.mat
  Rendering/Gargoyle Cracks Deferred; assigned to the FBX through material remapping.
  Original glTF double-sided setting preserved.

Textures (all 1024 x 1024)
  DragonGargoyle_BaseColor.jpg: original embedded color, sRGB.
  DragonGargoyle_Normal.jpg: original embedded tangent-space normal, Normal Map import.
  DragonGargoyle_MetallicRoughness.png: original glTF texture, G=roughness, B=metallic.
  DragonGargoyle_MetallicSmoothness.png: Unity texture, R=metallic, A=1-roughness.
  Both packed textures use linear sampling. URP smoothness multiplier is 1.

Editable Blender source and conversion/validation reports:
  Blender/DragonGargoyle/ (project root, outside Assets)

Asset setup command:
  Tools > Art Assets > Configure Dragon Gargoyle
This command configures only this asset folder and does not place objects in the current scene.

Crack preview:
  DragonGargoyle_Cracks.png: 128 x 128, tileable linear data with mipmaps.
  R=crack proximity, G=reveal order. Original UV0 is used only for base material.
  Cracks use the original object-space position and normal with triplanar projection.
  _CrackProgress: 0=intact, 1=fully cracked; saved at 0.55 for this preview.
  _CrackScale / _CrackWidth / _CrackDepth / _CrackColor control appearance.
  Tools > Art Assets > Dragon Gargoyle Cracks > Intact / Partial / Full previews.
  Texture generator: Tools/Art/generate_gargoyle_cracks.py (numpy and Pillow).
  This shader has GBuffer, ShadowCaster, DepthOnly and DepthNormals passes.

Rigid VAT collapse (2026-10-07):
  VAT/DragonGargoyle_Collapse_Unity.asset is the prepared render mesh.
  36 pieces, 7,812 triangles; Unity splits corner tangents to 15,564 render vertices.
  UV0, imported normals and original surface shape are retained. COLOR.r selects exterior (0) or interior (1); one material and one submesh.
  The FBX node's centimeter/root-axis transform is baked into the prepared mesh.
  Use the prepared mesh with Collapse_Surface material; Interior Color/Metallic/Smoothness configure the caps.
  Raw Collapse.fbx is the source asset, not a VAT-ready scene object.
  VAT textures are 64 x 128 RGBAHalf EXR, linear, point, clamp, no mipmaps/compression.
  UV1=lookup; UV2=Blender pivot XY; UV3=Blender pivot Z / piece ID.
  Shader basis: Blender XYZ -> Unity XZY, quaternion XYZ -> -XZY, W unchanged.
  GBuffer, ShadowCaster, DepthOnly and DepthNormals share RigidVAT.hlsl deformation.
  Crack projection uses the bind position/normal captured before deformation.
  Mesh.bounds includes all 110 frames and a half-float error margin.

Scene control:
  CardRenderingValidation / TabletopCombat_Blockout / DragonGargoyle
  RigidVATPlayer Progress: 0=rest, 1=collapsed. One renderer timeline drives all pieces.
  Duration: 109/30 = 3.633333 seconds; Play On Start is disabled; holds the last pose.
  During Play Mode, invoke Play() (or component context menu Play Collapse).
  Pause(), ResetPose() and SetProgress(0..1) provide explicit control.
  Battle/death event wiring is left to the gameplay caller.

VAT menus: Tools > Art Assets > Dragon Gargoyle VAT
  Configure Assets: import/bake/update assets only.
  Apply To Current Scene: validate GPU, replace the named statue mesh/materials, add player, save scene.
  Preview > Rest / Mid Collapse / Collapsed: synchronized previews; Rest restores pose.
  Validate GPU Decode: compile all four passes and compare actual GPU readback at integer,
    fractional and clamped frames, including rotated normals and tangents.
  Validate Scene And Capture: original/rest/mid/collapsed captures without saving preview changes.
  Reports and PNG captures: Library/GargoyleVATValidation/.
  Blender bind JSON is an optional extra reference; core import/GPU checks run without it.
  Crack preview menus update both the source material and VAT combined surface material.

Blender addon / export workflow:
  Source: Tools/Blender/statue_fracture/ (single authoritative package).
  Build: python3 Tools/Blender/build_addon.py -> Tools/Blender/dist/statue_fracture.zip.
  Installed addon: Object > Statue Fracture; 3D View > N > Destruction.
  Authoring file: Blender/DragonGargoyle/Fracture/DragonGargoyle_Fracture_VAT.blend.
  Blender working output: //VAT/DragonGargoyle_Collapse/ relative to that file.
  Unity output: Assets/Art/Models/Creatures/DragonGargoyle/VAT/ (flat).
  Export + Validate publishes only FBX, two EXRs and VAT manifest, with SHA256 hashes.
  Unity automatically prepares the mesh/material when the manifest changes.
  Existing GUIDs, crack tuning, player progress and unsaved scene state are retained.
  Auto-import toggle / manual Configure Assets: Tools > Art Assets > Dragon Gargoyle VAT.
  Large bind JSON, validation reports and .blend files remain outside Assets.
