# Statue Fracture — Blender 4.5 / Unity

正式插件源码只有一份：`statue_fracture/`。`__init__.py` 注册插件，
`fracture.py` 负责切割与刚体，`rigid_vat.py` 负责纹理、网格和验证，
`unity_export.py` 负责发布 Unity 资源。

## 安装

运行 `python3 Tools/Blender/build_addon.py`，生成 `Tools/Blender/dist/statue_fracture.zip`。
Blender 的 Preferences → Add-ons → Install from Disk 选择这个 ZIP 并启用
**Object: Statue Fracture**。3D 视图按 **N → Destruction**，无需每次运行 Text 脚本。
插件首选项中的 **Unity 项目** 选择项目根目录，**Unity 资源目录** 是 Assets 内的相对路径。
本机已经安装、启用并保存了这些首选项。重启 Blender 后仍可使用。

## 目录和导出

当前工程：`Blender/DragonGargoyle/Fracture/DragonGargoyle_Fracture_VAT.blend`。
在 `DragonGargoyle_Fracture_Preview` 场景编辑和导出；纹理预览场景可用
**返回破碎模拟场景** 切回。旧的 voxel / OriginalSurface 示例只是早期参考。

| 用途 | 项目根目录下的路径 |
| --- | --- |
| 插件源代码 | `Tools/Blender/statue_fracture/` |
| 安装包（可重新生成） | `Tools/Blender/dist/statue_fracture.zip` |
| Blender 工程 | `Blender/DragonGargoyle/Fracture/DragonGargoyle_Fracture_VAT.blend` |
| 完整 VAT 工作包与检查报告 | `Blender/DragonGargoyle/Fracture/VAT/DragonGargoyle_Collapse/` |
| Unity 输入与生成资源 | `Assets/Art/Models/Creatures/DragonGargoyle/VAT/` |

Blender 工作目录使用 `//VAT/`，相对于当前 `.blend`。勾选 **同时导出到 Unity**，
点击 **导出刚体 VAT + 验证**：完整工作包验证通过后，只发布 `.fbx`、
`_position.exr`、`_rotation.exr`、`_vat.json` 四个文件到 Unity 目录。
VAT 名称按资产区分，例如 **DragonGargoyle_Collapse** / **FoxStatue_Collapse**。
Unity 自动按 `statue-rigid-vat-v1` manifest 识别，不再绑定某个雕像名称。
**Unity 资源目录覆盖** 留空使用插件首选项；移动项目后只需更改首选项的项目路径。
工作包必须放在 Assets 外；大体积 bind JSON、报告和 `.blend` 不会进入 Unity。

Unity 检测最后发布的 `_vat.json` 后自动执行资源配置，校验三个输入文件的 SHA256，
更新 `_Unity.asset`、`_Surface.mat` 并生成带 `RigidVATPlayer` 的 `_VAT.prefab`，保持 GUID 和已有材质参数。
同内容重复导出不会改写文件。更新不调用场景替换菜单，不保存场景或重置播放进度。
自动导入可在 `Tools → Art Assets → Statue VAT → Auto Import Blender Exports`
开关；关闭 Unity 或 Auto Refresh 时，在打开项目 / Refresh 后导入。
手动补导入可选中 `_vat.json`，使用 `Tools → Art Assets → Statue VAT → Configure Selected Manifest`；
同处的 **Validate Selected GPU Decode** 验证纹理、所有顶点通道、四个 pass 和 GPU 读回。
**Unity 源材质** 可填写 Assets 内原表面 `.mat` 路径；留空时会在 VAT 相邻的资产目录按
Blender 原材质名称查找，必须唯一。表面纹理及参数从该材质继承。

已有雕像引用准备好的 `_Unity.asset` 和 `_Surface.mat`，后续导出会就地更新。
首次接入其他场景才需要 **Apply To Current Scene**，这个菜单会保存场景。
原始 FBX 含节点的单位/轴变换，不能直接替代准备好的 Unity mesh。

## Workflow

1. Select the source statue and use **检查模型**. The example is Z-up, about one
   metre tall, with its ground-centred pivot at the origin.
2. Keep **保留原外表面（仅切割裂缝）** enabled, choose fragment count and seed,
   then **生成预破碎网格**. The source mesh is never modified. Render fragments
   directly clip original triangles against Voronoi planes. Uncrossed triangles
   keep their original shape; crossed triangles gain intersection vertices and
   are triangulated. UV seams, all UV layers, and corner normals are interpolated
   from the original triangle using barycentric coordinates, without UV reprojection.
   New cut faces use `SF_StoneInterior`. Each shared cut section is constrained by
   original triangle intersections, triangulated once, and reused by both pieces.
   Generalized winding classifies interior triangles across the source's intersecting
   wing shells and small holes. Original nonmanifold features are retained; render
   fragments are not promised to become watertight by changing exterior triangles.
   A welded/voxel-repaired copy is used only for seeds and reference mass/volume.
   In preservation mode this reference is voxelized even for watertight sources:
   long thin triangles otherwise make successive BMesh cuts numerically fragile.
   Hidden convex collision hulls enclose each actual render fragment, including wing
   tips. The voxel resolution affects this reference copy, not the visible surface.
   Disabling the preservation option explicitly selects the older remeshed prototype.
3. **设置 / 重置模拟** creates a simple tabletop collider and active convex-hull
   rigid bodies on the hidden collision hulls, sets mass from reference volume,
   and staggers release from top to bottom. Render pieces follow their matching
   hulls as children.
   Defaults: 36 cells, release frames 24–36, 30 fps, end frame 110. The collider
   is a preview plane at Z=0, not a copy of the ornate Unity table. Change
   settings and reset before baking again. Regeneration uses the remembered
   source even when a generated fragment is selected.
4. **烘焙刚体动画**, then play the timeline to inspect the collapse.
5. **导出碎块运动数据** writes stable IDs, rest matrices and per-frame position /
   quaternion values. This optional debug/interchange export is **not VAT
   textures**. Use the separate rigid VAT export below for textures and a bind mesh. Coordinates remain Blender
   world Z-up; quaternions are WXYZ. Each 4×4 rest matrix is flattened row-major.

Generated objects live in owned `SF_Pieces_*`, `SF_Work_*`, `SF_Stage_*`
and `SF_Collision_*` collections. Rebuilding replaces only tool-owned pieces,
collision hulls and working copies.
The example preserves the original asset in `DragonGargoyle_Asset` and a hidden
source in `DragonGargoyle_Fracture_Preview`. Frame 1 reassembles the original
exterior surface with cuts; this preserves shape and attributes, not the total
original vertex/triangle count at fracture boundaries.

## Current outputs and verification

- `original_surface_preview.gif`: animation of the preserved exterior.
- `original_surface_intact.png`, `original_surface_collapsed.png`: rendered endpoints.
- `original_surface_reference.png`: the untouched original under the same camera/lights.
- `original_surface_motion.json`: 36 pieces × 110 frames of rigid transforms.
- `original_surface_validation.json`: surface, collision, cache and motion checks.

The source has 2,807 triangles. 1,899 remain whole; cutting the other 908 yields
4,974 exterior triangles in total. The new interiors contain 2,838 triangles.
Every source triangle is accounted for by its fragments; maximum per-triangle
area error is about 0.0011% and total area error is below 0.000001%.
UV and free corner normals match their original-triangle interpolation.
The source's 5 boundary / 72 nonmanifold edges are never repaired on the display mesh.

Render faces record `sf_source_triangle` (-1 for interiors) and `sf_barycentric`
corner attributes for preservation validation and future exports. Blender 4.5
free corner normals (`custom_normal`, FLOAT_VECTOR, CORNER) avoid the old tangent
normal encoding errors on this asset. This geometry is rigid, so these normals
need only rotate with the pieces. A future VAT/mesh exporter must preserve these
normals, UV seams and piece IDs; generic exporters need separate verification.
The procedural crack texture remains independent of the cutting boundaries.

## Rigid VAT export (v1.5)

The tool now exports textures and a bind mesh, in addition to the optional motion
JSON. The installed addon loads both modules from its package.
`Statue_Fracture_Tool.py` in the prepared project is only a launcher for the installed addon;
embedded historical script copies are no longer the editing/export source.
The prepared VAT project is
`Blender/DragonGargoyle/Fracture/DragonGargoyle_Fracture_VAT.blend`.

After physics baking, use **导出刚体 VAT + 验证** in the Destruction sidebar.
Choose output directory, name, EXR16/EXR32 precision and the encoding range.
The first encoded frame must be intact; the saved rest pose remains the
reference regardless of the current timeline frame. By default export opens
`DragonGargoyle_VAT_Preview`; play its timeline. This separate scene has no rigid
bodies and its Geometry Nodes decoder samples the exported, reloaded EXRs.
**返回破碎模拟场景** returns to the editable simulation. The preview works after
reopening the blend without script execution; the installed addon registers its
editing/export sidebar automatically.

### Reference and format choice

Design reference: [OpenVAT core](https://github.com/sharpen3d/openvat/blob/43b519620c710c9081a72eba253222d6fe6f931f/Development/openvat/core.py)
and [metadata / range utilities](https://github.com/sharpen3d/openvat/blob/43b519620c710c9081a72eba253222d6fe6f931f/Development/openvat/utils.py),
reviewed at commit `43b519620c710c9081a72eba253222d6fe6f931f`.
We use the same reference-pose, texel-center UV, range-remapping, sidecar and
texture-decoder-preview workflow. The exporter and generated node graph here
are independent implementations; they do not require the OpenVAT extension.

Our format is **rigid piece VAT**, with one pixel per piece per frame. OpenVAT's
standard format stores per-vertex position/normal data. Our two textures store
piece pivot positions and rotation deltas, so rigidly transformed original
normals and tangents are preserved without separate per-vertex normal textures.
The package has its own `statue-rigid-vat-v1` schema; it cannot be decoded directly
by the OpenVAT standard shader. Unity shader/importer integration is provided by `Assets/Editor/RigidVATAssetImport.cs`; the existing Gargoyle menus delegate to this shared importer.

### Package and decode contract

For `DragonGargoyle_Collapse` the output directory contains:

- `DragonGargoyle_Collapse.fbx`: one static bind mesh, one material slot and binary `VAT_SURFACE` vertex colors.
- `DragonGargoyle_Collapse_position.exr`: RGBA, RGB normalized pivot position,
  alpha 1 for used piece columns.
- `DragonGargoyle_Collapse_rotation.exr`: RGBA = XYZW quaternion × 0.5 + 0.5;
  alpha contains quaternion W and is an independent data channel.
- `DragonGargoyle_Collapse_vat.json`: versioned layout, remapping, clips,
  coordinates, IDs, reference matrices, files and full animation bounds.
- `DragonGargoyle_Collapse_bind_mesh.json`: exact reference positions, normals,
  tangents, UVs, triangles, material IDs, surface masks and piece IDs for importer verification.
- `DragonGargoyle_Collapse_validation.json`: decoded file and preview checks.

All original UV layers remain first, in their original order. Three new layers
follow them (indices are recorded in JSON):

| Layer | Meaning |
| --- | --- |
| `VAT_UV` | Texel center at column `piece_id`, top row / first encoded frame |
| `VAT_PIVOT_XY` | Rest pivot X and Y, mesh local |
| `VAT_PIVOT_Z_ID` | Rest pivot Z and piece ID |

The mesh contains 13,390 export vertices and 7,812 triangles in the current
example. UV/normal seams split export vertices without changing triangle shapes
or interpolated original surface attributes. Surface-type boundaries are also included
in the export vertex seam key: `COLOR.r=0` identifies the original exterior and
`COLOR.r=1` identifies new interior faces. G/B are 0 and alpha is 1; opacity and
VAT quaternion W remain independent. Every triangle has a constant binary type,
so the marker never blends across a fracture boundary. Original simulation pieces
keep their materials; only the exported bind mesh is combined. Blender's one-slot
preview material uses the same vertex-color selector to mix the original surface
shader and rough interior stone. More than one original surface material is rejected
rather than silently losing its textures. No physics rebake is required.

Unity imports `COLOR.r`, validates every triangle, and uses one material / one
submesh for GBuffer, shadow, depth and normals. The material's Interior Color,
Metallic and Smoothness parameters replace the previous separate cap material;
interior pixels skip original texture/normal/crack sampling. Legacy source meshes
leave `_UseSurfaceMask=0`. Source materials, mask encoding and single-slot layout
are explicitly recorded in the manifest.

FBX verification reads the binary
payload and checks connectivity, vertex order, normals, every UV layer, exact vertex colors, one material slot and constant surface type per triangle.
This does not assume that a later engine importer retains vertex order: texture
lookup uses the exported UV, not SV_VertexID.

For frame offset `t = clamp(frame - frame_start, 0, frame_count - 1)`, sample
adjacent rows at `floor(t)` and `min(floor(t)+1, frame_count-1)`. Images use a
bottom-left row origin; row index is `height - 1 - offset`. `VAT_UV.y` is the
first-row texel center, and the sampled UV.y subtracts `offset / height`.
Use linear data, point filtering, clamp addressing and no mipmaps. Frame
interpolation is explicit: interpolate pivots linearly and quaternions with
shortest-path normalized linear interpolation. At the end, hold the last pose.

```text
pivot_current = position_min + position_rgb * position_max_minus_min
q_delta = normalize(rotation_rgba * 2 - 1)  // XYZW
P = pivot_current + rotate(q_delta, P_rest - pivot_rest)
N = rotate(q_delta, N_rest)
T.xyz = rotate(q_delta, T_rest.xyz); T.w = T_rest.w
```

Zero-range position channels decode to their constant minimum. Unused time rows
repeat the final pose; unused piece columns are not sampled. The bind and VAT
vectors use Blender right-handed mesh-local XYZ / Z-up. FBX is exported with
`-Z Forward, Y Up` and no mesh-space bake; a future importer must keep mesh-local
axes, or apply the same basis conversion to mesh positions, pivot UV values,
texture vectors, quaternion rotations, normals and tangents. Reference JSON
makes this conversion explicit rather than relying on importer defaults.

### Validation and limits

Every export reads the saved EXRs back, reconstructs every vertex at every encoded
frame, and compares against evaluated simulation matrices. It also checks normal
and tangent rotations, first-frame assembly, interpolation and the end clamp.
The separate Geometry Nodes preview is checked against the decoded file values,
including fractional frames. Output is staged and the manifest is published only
after these checks pass; a failed bake preserves earlier exported files.

Current example: 36 pieces × 110 frames, two 64×128 EXR16 textures, **128 KiB**
uncompressed data in total (about 29 KiB ZIP-compressed on disk). Maximum
position error is **0.55 mm** at one Blender unit per metre; maximum rotation
error is **0.10°**. The animation bounds cover the full collapse, with padding
for later runtime culling. EXR32 is available when more precision is needed.

Rigid VAT rejects unbaked/range-invalid simulations, changing scale/shear,
mirrored or nonuniform reference scale, deformation modifiers and shape keys.
It supports the current static fractured topology. Deforming or changing-topology
VAT is outside this tool's scope. Source render meshes and physics caches are
preserved during export. Unity integration and GPU validation are available under
`Tools → Art Assets → Dragon Gargoyle VAT`. Crack rendering validation is under
`Tools → Art Assets → Dragon Gargoyle Cracks → Validate Rendering`.

## FoxStatue 闭环验证（v1.5）

工程：`Blender/FoxStatue/Fracture/FoxStatue_Fracture_VAT.blend`；当前 Blender 已打开 VAT 预览。
源 FBX / 原贴图：`Assets/Art/Models/Creatures/FoxStatue/`。
在新资产上先命名模型、设置 VAT 名称和独立 Unity 资源目录，配置原表面 `.mat`，再导出。
狐狸保存的设置是：

- VAT 名称：`FoxStatue_Collapse`
- 工作目录：`//VAT/`
- Unity 资源目录覆盖：`//../../../Assets/Art/Models/Creatures/FoxStatue/VAT/`
- Unity 源材质：`Assets/Art/Models/Creatures/FoxStatue/FoxStatue_Material.mat`

`FoxStatue_Collapse_VAT.prefab` 可直接拖入场景，绑定单个 mesh / 材质 / 播放器。
调节材质的 **裂痕程度**，在播放器 **Progress** 中预览崩塌；Play Mode 调用 `Play()`。
当前保留原始 2,976 个三角面的表面归属，切割后 5,304 个外表面三角形 + 3,428 个断面；
UV 和角法线插值误差为 0，36 块 / 110 帧 / 30 fps，两张 64×128 EXR16。
Blender 量化后最大位置误差约 0.463 mm，Unity GPU / CPU 解码差约 1.24e-7 m。

Unity 菜单 `Tools → Art Assets → Fox Statue` 提供源资产配置、VAT 导入、GPU 和渲染验证。
验证在独立临时预览场景运行，不需要把测试对象放入游戏场景。
`Reexport Probe → Begin` 保持一个已加载 mesh 的实例，并临时将材质裂痕设为 .37、实例进度设为 .35；
随后在 Blender 返回模拟场景导出，再用 `Finish` 验证 GUID、实例进度、材质调节值和实际 GPU 顶点流。
Finish 恢复裂痕原值并关闭临时预览场景。这个步骤验证同一资源多次更新后的 GPU 状态。

检查报告及画面对照在 `Library/FoxStatueValidation/`：`blender.json`、`GPU/report.json`、
`rendering.json`、`reexport.json`、`reexport-unity.json` 和 `comparison.png`。
原 DragonGargoyle 在共享导入器上的 GPU 回归同样通过。
