# FourBeasts / URP Deferred 卡牌渲染

Unity 2022.3 / 项目内嵌 URP 14。验证场景：`Assets/Scenes/CardRenderingValidation.unity`。

## 资源与实例

- 几何：`Assets/Art/Models/Cards/Card_Rounded.fbx`，64×96×0.35 mm，圆角 0.8 mm，160 三角形，一个材质槽。保留导入法线和 UV1；禁止 Generate Lightmap UVs。
- Shader：`Assets/CardArt/Shaders/CardDeferred.shader` / `CardArt/Card Deferred`。
- 皮肤共享材质：`Assets/Art/Materials/Cards/FourBeastsCards.mat`。
- 通用 Prefab：`Assets/CardArt/Prefabs/Card.prefab`。根节点负责布局/翻转，Surface 上的 CardView 负责牌面绑定。Collider 厚度 1 mm 便于交互，与视觉厚度分离。
- 所有 CardView 使用 MeshFilter.sharedMesh / Renderer.sharedMaterial，避免通过 mesh / material getter 产生副本。CardInstanceId 等玩法标识留在玩法层；CardView 只接受图集中的牌面 ID。

```csharp
// 每个皮肤配置一次共享材质。
CardShaderProperties.ConfigureSkin(sharedMaterial, atlas);
// 创建 Prefab 实例后，使用组件切换牌面。
var view = instance.GetComponentInChildren<CardView>();
view.SetCard("hearts_q");
// 仅给选中的这一张牌乘色，不修改共享材质。
view.SetTint(new Color(1f, 0.8f, 0.35f, 1f));
// 取消选中，恢复皮肤原色。
view.SetTint(Color.white);
```

CardView 在 OnEnable 恢复 MaterialPropertyBlock，适用于编辑器预览、Prefab 实例及运行时实例。Bind 接受显式 Mesh/材质/图集/牌面 ID。缺失或被图集 Fill 裁切的牌面不能绑定为完整卡牌，SetCard 验证失败时保留原牌面。组件的 Color Tint 默认白色、可在 Inspector 设置，通过 `Tint` 读取；SetTint 可在 Bind 前设置，Bind / SetCard / 重新启用组件均保留颜色。对象池归还时由交互层显式 SetTint(Color.white) 重置选中状态。

## Shader 入口约定

| 属性 | 设置位置 | 含义 |
| --- | --- | --- |
| `_BaseMap` | 共享材质 | 牌面和卡背的同一图集 |
| `_FrontUVRect` | 每张牌的 MaterialPropertyBlock，GPU instanced | 当前正面的图集区域 |
| `_ColorTint` | 每张牌的 MaterialPropertyBlock，GPU instanced | 单牌 RGBA 乘色，默认白色；用于选中高亮 |
| `_BackUVRect` | 共享材质 | `card_back` 区域 |
| `_BaseColor` | 共享材质 | 全局乘色，默认白色 |
| `_EdgeColor` | 共享材质 | 实体侧边纸色 |
| `_Cutoff` | 共享材质 | 不透明渲染中的 Alpha Clip 阈值，默认 0.01 |

Rect 统一为 **xy=offset、zw=scale**，坐标原点左下；这是图集导出的 `region.uv`，不是 Unity `_BaseMap_ST` 的排列。计算：`atlasUV = localUV * rect.zw + rect.xy`，边界向内限制半个纹素，避免采到相邻区域。采样由 Texture / RenderTexture 的 Filter Mode 决定。

- UV0 是通用 0–1 局部 UV。
- UV1.x=1 表示背面；UV1.y=1 表示侧边。正面 `(0,0)`、背面 `(1,0)`、侧边 `(0,1)`。
- FBX 背面的 UV 已补偿镜像；Shader 不再次翻转。
- 这是封闭实体，使用 Cull Back，正反面分别有几何和朝外法线。

## 延迟渲染与合批

Shader 的 UniversalGBuffer Pass 使用 URP 内嵌的 SurfaceDataToGbuffer，材质类型 SimpleLit，粗糙纸面使用低高光。Deferred 中使用 GBuffer 光照，同时提供 UniversalForward、ShadowCaster、DepthOnly、DepthNormals；各 Pass 使用相同的实例牌面、卡背、侧边和 Alpha Clip 逻辑。

卡牌使用环境光/Light Probe 与实时灯光，不支持用 UV1 烘焙 Lightmap，因为 UV1 已作为表面标记。此版没有法线贴图、透明混合、Decal/DBuffer 和烘焙 Meta Pass。

共享材质开启 GPU Instancing，`_FrontUVRect` 和 `_ColorTint` 写入每实例 PropertyBlock，均通过 `UNITY_DEFINE_INSTANCED_PROP` 声明和 `UNITY_ACCESS_INSTANCED_PROP` 读取。颜色计算为「图集采样（或侧边纸色）× 全局 _BaseColor × 单牌 _ColorTint」，覆盖正面、背面和侧边。Tint 的 Alpha 参与所有 Pass 的 Alpha Clip，不提供半透明混合。

CardView 管理这些实例参数，不创建材质副本；交互层通过 SetTint 改色，避免修改共享材质或往 PropertyBlock 添加非实例属性。MaterialPropertyBlock 对象不走 SRP Batcher；项目全局 SRP Batcher 保持现有设置。是否合成一次实例 Draw 取决于 Pass、可见性、阴影、平台和 Unity 的批次拆分；没有将共享材质等同于保证单次 Draw Call。

## 验证场景与像素输出

场景有三种 JQK、方块 A、Joker、卡背和倾斜卡牌，共 7 张牌；同一 Mesh 和卡牌材质。早期 IMGUI 牌桌及其全局自动启动已移除，验证场景独立运行。方向光和局部点光检查延迟光照，倾斜卡牌检查侧边和阴影。

Main Camera 使用项目默认 Renderer 0，直接输出到 Game View。Renderer 的 `Low Resolution Rendering` 配置把实际颜色、深度与 GBuffer 的高度限制为可配置值（默认 540），宽度按原始相机输出比例取整；低于 540 高的窗口保持原尺寸。独立的 `RetroPostProcessFeature` 在 Renderer 提供的分辨率执行后处理，URP FinalBlit 使用 Point 放大到原生输出，Overlay UI 保持原生分辨率。已移除固定 960×540 RenderTexture、额外输出相机与 RawImage；旧 RenderTexture 资产保留但不再被此场景引用。设置与验证细节见 [RetroRendering.md](RetroRendering.md)。

菜单：

- `工具/卡牌美术/渲染验证/创建验证场景`：重建验证场景、共享材质与 Prefab。会提示保存其他场景的未保存修改；重建会覆盖此生成场景。
- `工具/卡牌美术/渲染验证/捕获验证画面`：保存 960×540、Point 放大到 1920×1080、倾斜侧边与圆角特写。使用 URP Render Request 实际渲染。
- `工具/卡牌美术/渲染验证/捕获游戏输出`：Play 模式下保存最终 Game View 输出，包含实时后处理与 Point 放大。

截图和报告放在 `ArtSource/Cards/RenderingValidation/`，不导入为游戏纹理。`validation.json` 检查实际 renderer 模式、Shader 错误、资源共享、网格尺寸和采样设置；这不是 GPU Draw Call 数量报告。

EditMode 测试类 `CardArt.Tests.CardRenderingTests` 检查物理尺寸、160 三角形、正反面法线与 UV 镜像约定、UV1 三类标记、共享资源、54 张牌的独立 UV/tint 绑定、切牌与重新启用时的颜色保留、恢复白色、无效牌面处理和 Shader Pass/Point 导入配置。Prefab 导出测试同时拒绝缺少 `_ColorTint` 或仅使用共享 tint uniform 的自定义 Shader。

## Prefab 导出页签

打开 `工具/卡牌美术/图集构建` 后选择 **Prefab 导出**，或直接使用 `工具/卡牌美术/预制体导出`。同一个窗口保留卡牌列表和实时线框图集预览页签，窄窗口中的图集配置通过“配置 / 替换”切换，Prefab 页签独立滚动。

选择 CardAtlasData、Shader、Mesh，设置默认正面 ID、是否添加 BoxCollider、Assets 内目录和文件名。默认牌面与卡背可以直接预览，校验未通过时禁用导出。

输出 `<名称>.prefab` 和 `<名称>.mat`。Prefab 根节点负责位姿和可选碰撞体，Surface 子节点负责渲染与 CardView；原始 Mesh 保持共享，通过子节点旋转和偏移把正面统一到 +Y、牌面上方向统一到 +Z，并居中。Collider 最小厚度 1 mm。材质配置图集、卡背和 GPU Instancing，默认正面通过 CardView 的实例参数绑定。不会把 54 张牌分别导出为 54 种材质。

同名导出会询问是否更新生成内容及共享材质；更新保留资产 GUID。工具在临时 Preview Scene 中构建 Prefab，不向当前编辑场景添加卡牌。输入校验失败时不会产生 Prefab 或材质。图集的 Point / Clamp / Mipmap 等导入配置保持原值。

### Shader 接口校验

支持符合约定的自定义 Shader，不按 Shader 名称限制。当前接受 ShaderLab `.shader` 文件及递归引用的 HLSL/CGInclude（支持项目资源、内嵌包及 PackageCache）。要求：

- `_BaseMap` 为 Texture2D；`_FrontUVRect`、`_BackUVRect` 为 Vector；`_BaseColor`、`_ColorTint`、`_EdgeColor` 为 Color。
- `RenderPipeline=UniversalPipeline`，使用不透明或 AlphaTest 队列。
- 受光 Shader 声明 `UniversalMaterialType=Lit` 或 `SimpleLit`，必须有 `UniversalGBuffer`、`UniversalForward`、`ShadowCaster`、`DepthOnly`、`DepthNormals` LightMode Pass；原有校验保持不变。
- 无光照 Shader 显式声明 `UniversalMaterialType=Unlit`，必须有 `UniversalForwardOnly` 和 `DepthOnly` Pass；如需用于 URP 2D Renderer，另提供 `Universal2D` Pass。每个必需 Pass 以及提供的 `Universal2D` Pass 都须声明 `multi_compile_instancing`。
- `_FrontUVRect` 与 `_ColorTint` 均包含对应的 `UNITY_DEFINE_INSTANCED_PROP(float4, ...)` 与 `UNITY_ACCESS_INSTANCED_PROP(..., ...)`。自定义 Shader 也需要接入单牌 Tint。
- 用 SubShader Tags 声明坐标/表面语义：`"CardUVRectContract"="OffsetXYScaleZW"` 和 `"CardSurfaceContract"="UV1BackXEdgeY"`。已随内置卡牌 Shader 加入。
- Shader 支持当前设备，并且没有已报告的编译错误。

这些校验检查属性、接口声明和导入 Pass；自定义 Shader 的具体采样逻辑按上述 Tags 所声明的语义实现。Shader Graph 和宏生成的 ShaderLab Pass 不在本版源码声明校验范围内。

图集检查空/重复 ID、完整 `card_back`、默认正面、有效且位于图集内的 UV Rect。允许选择含卡背的部分牌面图集；被裁切的其他区域会提示并排除在默认牌面列表外。Mesh 检查单一三角形子网格、有效法线、二维 UV0/UV1、正反面与侧边标识、每个三角形表面标识一致，以及 Mesh 宽高与图集逻辑单牌比例（1% 容差）。通过 Editor MeshUtility 读取数据，未开启 Read/Write 的导入模型同样可以校验，无需修改其导入设置。

### 2D 验证用无光照 Shader

`Assets/CardArt/Shaders/CardValidationUnlit.shader`（`CardArt/Validation Unlit`）用于正交相机下的平面卡牌验证。它复用 `CardView`、共享 Mesh、图集与每实例 `_FrontUVRect` / `_ColorTint`。当前验证相机使用固定在 `Assets/Settings/Battle2DRenderer.asset` 的 `Simple2DRenderer`，优先绘制 `UniversalForwardOnly` Pass；该 Pass 也支持常规 URP 和 CardArt 编辑器预览。保留的 `Universal2D` Pass 可供官方 URP 2D Renderer 使用。HUD 独立使用 Canvas；卡牌 Shader 的入口是 MeshRenderer，不是 UGUI Graphic。

基础属性和两项坐标契约与现有卡牌一致。正反面按 UV1.x 选择图集区域，UV1.y 标记纸边，圆角沿用 `Card_Rounded` 网格，图集 Alpha 和 `_Cutoff` 决定裁切。颜色直接输出「图集或纸边 × `_BaseColor` × `_ColorTint`」，不受灯光影响；没有 GBuffer、阴影、镭射、火漆或红叉扩展。`DepthOnly` 使用相同的 Alpha 裁切。

在美术配置中为它建立独立的参数组合，只使用 `_ColorTint` 等实际支持的实例属性，例如白色基础组合、暖黄色选中组合和灰色禁用组合；复用现有 `CardAtlasData` 即可。`StandardCardArt` 的基础组合含镭射和火漆参数，不能直接套给此 Shader，导出校验仍会拒绝不支持的组合。无需修改已有 3D 卡牌 Prefab、材质和 Shader。


## 动态镭射牌

CardView Inspector 的「镭射（仅正面）」提供开启、HDR 颜色和强度（0–5）三个入口，默认关闭。材质的 Foil Color 同样支持 HDR。白色保留完整彩虹，其他颜色对彩虹进行染色；颜色 Alpha 不参与效果。RGB 可超过 1，SetFoilColor 与实例属性传递保留 HDR 数值，用于提高镭射亮度和 Emission。强度 0 与关闭效果一致。

```csharp
view.SetFoilEnabled(true);
view.SetFoilColor(new Color(0.3f, 0.8f, 1f));
view.SetFoilStrength(1.5f); // 限制到 0–5，拒绝 NaN/Infinity
view.SetFoilEnabled(false); // 保留颜色和强度，方便再次开启
```

通过 `FoilEnabled`、`FoilColor`、`FoilStrength` 读取状态，`SupportsFoil` 检查当前材质能力。可在 Bind 前设置，切换牌面、重新绑定、重新启用组件均保留参数；对象池归还时显式关闭镭射。

Shader 实例入口为 `_FoilEnabled`、`_FoilColor`、`_FoilStrength`，共享材质入口为 `_FoilMap`（灰度纹理）及 `_FoilSpeed`（动画速度，0 停止时间动画，仍响应视角）。可选契约 Tag 为 `CardFoilContract=ColorStrengthV1`，声明者必须将三个属性实现为 GPU 实例属性；旧版自定义卡牌 Shader 可继续绑定，不会收到这些额外属性。

默认纹理 `Assets/CardArt/Textures/FoilPattern.png` 是 64×64 的菱形切面与斜向刻纹，Linear、Point、Repeat、无 mipmap。可在共享材质中替换；纹理固定在局部 UV 上，时间驱动彩虹亮带扫过切面，同时叠加视角变化。动画使用 Unity Shader 时间。只影响正面，保护深色油墨，背面、纸边、深度和阴影轮廓不变。GBuffer 中写入调整后的颜色、高光、光滑度及附加亮度；Forward 使用相同表面计算。不会新增 Pass、GBuffer 附件或逐牌材质。

`工具/卡牌美术/渲染验证/预览闪卡` 打开独立的 `Assets/Scenes/CardFoilValidation.unity`：左侧普通牌、中间彩虹镭射、右侧粉色镭射。以附加方式打开，保留原场景的未保存修改。`Validate Foil Rendering` 检查颜色、强度、零强度、关闭恢复、纹理及动态相位；`Capture Foil Animation` 固定相机捕获 12 帧，验证时间动画。`Validate Card API Without Scene Reload` 执行四组同步测试断言，无需关闭当前场景。结果在 `ArtSource/Cards/RenderingValidation/Foil/`。


## 火漆印章与法线

卡牌正面支持火漆覆盖层，默认中心 `(0.30, 0.62)`，UV 尺寸 `(0.45254834, 0.3016989)`（较原版宽高各乘 √2，面积两倍），顺时针旋转 12°，在 2:3 卡牌上保持圆形，位于中心偏左上。共享材质 `_SealRect` 的语义为中心 XY / 尺寸 ZW，与牌面图集 Rect 的偏移语义不同。

```csharp
view.SetSealEnabled(true);
view.SetSealColor(new Color(0.65f, 0.08f, 0.04f));
view.SetSealEmissionStrength(0.3f); // 0–1，默认 0，拒绝 NaN/Infinity
// 可以读取 SealEnabled、SealColor、SealEmissionStrength、SupportsSealEmission；切牌与重新启用保留状态。
```

`_SealEnabled`、`_SealColor`、`_SealEmissionStrength` 是 GPU 实例属性；`_SealMap`、`_SealNormalMap`、`_SealNormalScale`、`_SealRect`、`_SealRotation` 是共享材质参数。火漆颜色 Alpha 忽略；颜色贴图 Alpha 决定覆盖范围。未绑定颜色贴图的 CardView 不提交启用状态。旧自定义 Shader 不声明可选 Tag `CardSealContract=ColorNormalV1` 时继续支持普通卡牌，导出火漆配置则会提示不兼容。

`_SealRotation` 单位为度，正值逆时针；颜色与法线在同一局部坐标旋转。法线使用 Unity `UnpackNormalScale` 解码，按旋转后的火漆 UV 导数建立切线空间，导入 Mesh 无需新增 tangent。Deferred GBuffer、Forward 光照及 DepthNormals 都使用火漆法线；火漆覆盖镭射的颜色、高光、光滑度和 Emission，背面与侧边保持原样。法线只模拟凹凸光照，不改变实体厚度或投影轮廓。默认贴图为 `WaxSeal_Paw.png` 与 `WaxSeal_Paw_Normal.png`；法线强度 0 关闭凹凸，不移除火漆图案。

火漆自发光是附加贡献：`纹理 RGB × 火漆颜色 × 纹理 Alpha × 强度`，保留原有法线、漫反射与高光；不依赖灯光，不影响背面与纸边。0 关闭贡献，1 添加完整颜色亮度。仍然遮住下方镭射的 Emission。旧火漆 Shader 没有 `_SealEmissionStrength` 时可继续使用普通火漆，但导出非零自发光会报错；提供该属性时必须为 Float/Range 并声明和读取 GPU 实例属性。

Prefab 导出页新增「火漆印章（可选）」：颜色贴图、法线贴图、内置素材按钮、默认开启、颜色、自发光强度、法线强度、中心、尺寸和旋转，以及「恢复默认火漆布局」按钮。选择的纹理及法线强度写入导出的共享材质；开关、颜色和自发光强度写入 Prefab 的 CardView。法线必须以 Normal Map 导入；颜色图必须保留 Alpha，两张图的比例必须一致。工具校验扩展属性类型和实例属性声明，不会自行修改所选纹理的导入设置。

`工具/卡牌美术/素材准备/蜡封材质` 为现有内置卡牌材质配置缺失的火漆纹理，并保留其他特效默认纹理。`工具/卡牌美术/渲染验证/验证蜡封导出与渲染` 验证导出/覆盖更新、法线导入错误拦截、实例隔离、实际法线光照和卡背排除。临时渲染场景在验证结束时关闭，原场景及未保存修改保留；截图与报告位于 `ArtSource/Cards/RenderingValidation/Seal/`。

## 手绘红叉

`CardView` Inspector 的「红叉（仅正面）」提供「显示红叉」，默认关闭：

```csharp
card.SetRedCrossEnabled(true);
card.SetRedCrossEnabled(false);
bool enabled = card.RedCrossEnabled;
bool supported = card.SupportsRedCross;
```

`_RedCrossEnabled` 为 GPU 实例属性，`_RedCrossMap` 为共享材质贴图，可选契约为 `CardRedCrossContract=OverlayV1`。切换牌面、Bind、组件重新启用均保留开关；共用 Mesh 与材质，不新增 Pass。缺失贴图时不提交启用状态，旧自定义 Shader 不声明扩展仍可渲染普通卡牌。

红叉使用局部 UV0 覆盖整个正面，透明区域显示原牌面；叠加顺序为牌面 → 镭射 → 火漆 → 红叉。红色油漆覆盖下层颜色、高光和 Emission，保留底层法线受光；不修改背面、侧边、卡牌 Alpha、深度或投影轮廓。Forward 与 Deferred 使用相同表面函数。

内置 `Assets/CardArt/Textures/RedCross.png` 为 **128×192 RGBA**，从 `ArtSource/Cards/RedCross/RedCross_Portrait.png` 用最近邻缩小，保留原始 Alpha。导入使用 Point、Clamp、无 mipmap、无压缩、sRGB，NPOT Scale=None 保持 2:3。`工具/卡牌美术/素材准备/红叉贴图与材质` 可配置内置贴图与现有卡牌材质，保留已有自定义红叉贴图和每张牌的开关。

Prefab 导出页的「手绘红叉（可选）」支持贴图选择、内置素材按钮和默认开关。校验 Shader 属性/实例声明、颜色图与 Alpha、Point/Clamp/无 mipmap、与逻辑单牌一致的宽高比。导出贴图至共享材质，开关保存至 CardView；未选贴图时保持空引用。

`工具/卡牌美术/渲染验证/验证蜡封导出与渲染` 同时验证红叉导出、实例隔离、切牌/重绑、关闭恢复、无贴图保护、与其他特效共存及背面排除。结果位于 `ArtSource/Cards/RenderingValidation/Seal/`，红叉预览为 `red_cross.png`。验证结束自动关闭临时场景，保留原场景的未保存状态。
