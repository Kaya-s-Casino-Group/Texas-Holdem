# dev 与旧 3D 场景分支拆分清单

检查日期：2026-10-20。当前 dev / origin/dev 均为 `dcf0863`；本清单只梳理迁移，没有切换分支、删除资源、提交或推送。

## 目标边界

建议将“dev 只保留代码”落实为：**开发代码 + 卡牌运行必需资源 + 最小验证入口**。Card Prefab 本身就是资源，若保留它，就必须保留相应 Mesh、材质、图集、Shader 和配置。旧环境、角色、怪物、灯光、后处理演示场景留在新分支。

分支名建议：`codex/legacy-3d-scenes`。先完整保存现状，再从 dev 做减法。旧分支保留原资源路径与 GUID，迁移无需在目录间搬动旧场景。

后续场景统一使用 **官方 URP + 标准 Universal Renderer，Rendering Path = Forward**。定制 URP、Simple2DRenderer、低分辨率渲染与专用后处理均随旧 3D 分支归档。通用代码保留，引用定制接口的代码必须改为官方接口或随旧分支归档；不为了保留旧代码继续携带定制管线。

## 当前工作区必须先保存

- 已跟踪改动 9 个文件，混合了 `SceneDemo 1V2.unity`、CardZoneView、编辑器窗口、Renderer、MainURP、TagManager 和说明文档。
- 未跟踪文件 212 个，包括 `Assets/Art/Environment/`、`ViewportOverlayCamera.cs` 及 meta、`CardForwardRenderer.asset` 及 meta。
- 只从当前 HEAD 建一个新分支并不能保存这些未提交内容；必须在归档分支提交实际改动，包含未跟踪资源与 meta。
- 当前子模块指针为 `External/BattleToolkit @ 2a82ecd6a67126f714f7e3a73676ec064becc42c`，归档与精简分支应显式保留所采用的指针；提交前另检查子模块内部是否有改动。
- CardZoneView 等通用改动与 3D 资源改动分开提交，方便把通用改动带回 dev。MainURP、TagManager 和相机代码需要逐项判断，不能直接整份带回。

## dev 保留资源

| 范围 | 保留原因 |
| --- | --- |
| `Assets/Scripts` 的规则、Hosting、CardMatch、BattlePresentation、CardArt、CardPresentation、CardInteraction、UIFoundation、通用工具和对应测试 | 继续开发的代码基础；专用 3D 工具另行收口 |
| `External/BattleToolkit`、Packages、必要 ProjectSettings | 规则包、Unity 编译与项目运行基础 |
| `Assets/GameAssets/CardArt` | 三个卡牌 Prefab、Profile、效果纹理及 Shader/HLSL |
| `Assets/Art/Models/Cards/Card_Rounded.fbx` | 三个世界卡牌以及最简 2D 卡牌均使用；模型目录不能整体删除 |
| `Assets/Art/CardAtlases` | Classic / FourBeasts 的 asset 与 PNG；JSON 为构建配套文件，第一阶段一起保留 |
| `Assets/Art/Materials/Cards/FourBeastsCards.mat`、`FourBeastsCanvas.mat` | 世界卡牌和 Canvas 卡牌材质 |
| `Assets/GameAssets/CardPresentation` | UI Prefab、Canvas Shader、图标、状态映射和 Resources 音频 |
| `Assets/GameAssets/SingleBattle/Hosting`、`Presentation/Minimal2D` | 最小宿主和验证表现 |
| `Assets/GameAssets/PokerScoring`、`RunFlow`、仍被使用的内容配置 | 运行配置，与环境模型无关 |
| 字体、TMP、DOTween、必要输入/UI 依赖 | 卡牌 UI、文字和动画使用；按依赖保留 |
| 官方 URP 包、标准 Forward Renderer 与一个最小验证场景 | 不保留嵌入式定制管线，也不保留自研 Simple2DRenderer |

### Card Prefab 的实际依赖

以下来自当前工作区 YAML GUID 递归检查；三个世界卡牌均未追踪到环境、角色或怪物模型。

| Prefab | 牌面 | 材质 | 结论 |
| --- | --- | --- | --- |
| `GameAssets/CardArt/Prefabs/Card.prefab` | FourBeastsAtlas + StandardCardArt | `Art/Materials/Cards/FourBeastsCards.mat` | 保留；与 FourBeastsCard 使用不同材质资产，暂不合并 |
| `GameAssets/CardArt/Prefabs/FourBeastsCard.prefab` | FourBeastsAtlas + StandardCardArt | 同目录 `FourBeastsCard.mat` | 保留 |
| `GameAssets/CardArt/Prefabs/ClassicCard.prefab` | Classic 图集 + Classic Profile | 同目录 `ClassicCard.mat` | 保留 |
| `GameAssets/CardPresentation/Prefabs/CardUIItem.prefab` | FourBeastsAtlas | FourBeastsCanvas | 保留；没有卡牌 FBX 依赖 |
| 同目录 DeckOverview / DrawPileIcon / DeckSummary / HandActionPanel / CardSuitChipsTooltip | 卡牌 UI / TMP / 字体等 | Canvas / TMP 材质 | 保留 |
| `GameAssets/SingleBattle/Presentation/Minimal2D/Art/Battle2DCard.prefab` | Classic 图集 | CardValidationUnlit | 保留；也依赖 Card_Rounded.fbx |

表内相对路径均以 `Assets/` 为根。

世界卡牌共同保留 `CardDeferred.shader`、`CardDeferredInput.hlsl`、`CardDeferredPasses.hlsl`、`CardSurfaceCommon.hlsl` 和 FoilPattern / RedCross / WaxSeal_Paw / WaxSeal_Paw_Normal 四张纹理。Canvas Shader 与验证 Unlit Shader 也 include `CardSurfaceCommon.hlsl`。这些 include 不会出现在纯 GUID 依赖扫描中，必须补充保留。

`CardDeferred` 名字带 Deferred，但已有 `UniversalForward` / `CardForward` Pass；保留卡牌自定义 Shader 不等于保留定制管线。迁移后用官方 Forward 实测牌面、镭射、火漆、阴影与 Canvas 显示。若有定制接口依赖就适配 Shader；暂不为改名批量更换材质或 GUID。

### 原始牌面与运行图集分开处理

`Assets/Art/Cards` 本地约 154 MB；本次 Prefab 依赖检查引用的是输出图集，没有追踪到原始单牌 PNG。若 dev 仍要编辑/重建牌面，保留该目录；若以代码开发和现成 Prefab 使用为主，可把原始 PNG 留在旧分支，dev 保留输出图集与构建工具。

后者需要调整 CardAtlasRecipe 默认源路径、CardTextureImporter 和依赖原始目录的测试/提示，避免工具默认假定源图存在。不能将“运行时不用”直接等同于“开发工具不用”。

## 旧 3D 分支保存范围

| 范围 | 内容 |
| --- | --- |
| `Assets/Art/Models/Environment` | 桌子、ForestRuins、GothicChapel 等 |
| `Assets/Art/Models/Characters`、`Creatures` | 角色、石像、怪物及 VAT 配套资源 |
| `Assets/Art/Environment` | 本地新增 StylizedGrass / StylizedRuins，包含资源和自身脚本 |
| `Assets/HandSkinValidation`、`Assets/SceneBlockouts` | 角色/手部验证及场景灰盒 |
| `Assets/Art/VFX` 中仅服务旧战斗的部分 | 命中、护盾等；先确认保留前端是否需要 |
| `Assets/GameAssets/SingleBattle/Presentation/Scene3D` | 3D 专用盾牌和 Shader |
| `Assets/GameAssets/Rendering` 的旧环境效果 | 后处理、雾、体积光、萤火虫等，先排除保留卡牌的真实依赖 |
| 旧 Renderer、Lighting、RenderTexture | 清理全局引用后迁走 |
| `Packages/com.unity.render-pipelines.universal` | 完整保存旧版定制 URP；dev 改从官方 registry 安装 |
| Simple2DRenderer、低分辨率及原生分辨率 Overlay 扩展、配套 Feature / 工具 / 测试 | dev 采用标准 Forward，不再维护这些渲染路径 |
| Blender / ForestRuins 工具、导入器和专项验证 | 与对应模型一起保存在旧分支；dev 中入口应同步移除或改为明确的可选工具 |

明确归档的场景：`SceneDemo 1V2`、`ForestRuins`、`CharacterValidation`、`SingleBattleDemo`、`SingleBattleDemoNew`、`TeamBattleDemo`、`HandSkinHumanoidTest`，以及新增草地演示场景。

`CardRenderingValidation` 实际包含环境/怪物引用，也应归档，另建精简卡牌验证入口。`CardFoilValidation` 可以归档后由工具重新生成精简版本。`MainScene`、`PreservedScene`、`SampleScene` 不能只凭名称判断，替换入口时再确认是否需要保留。

建议改造 `Battle2DValidation` 或重新建立最小卡牌/战斗验证场景，使用标准 Forward Renderer；Minimal2D 是表现前端，不要求继续使用原自研 Renderer。

## 删除前必须解除的耦合

1. **全局管线仍引用旧 Renderer。** 当前 MainURP 的 Renderer 列表为 MainDeferredRenderer、UIRenderer、Battle2DRenderer、CharacterRenderer、CardForwardRenderer、StylizedRuinsForwardRenderer，默认 index 0。不能直接删后四项或重排列表：新建标准 URP Asset 和一个 UniversalRendererData（Forward、默认无自定义 Feature），修改 Graphics / Quality 引用与所有保留相机的 Renderer index，再删除旧管线资产。
2. **2D 验证依然依赖现有设置约定。** Battle2DValidation 的 Editor/Play 测试断言 MainURP、MainDeferredRenderer、UIRenderer 以及 Renderer index。精简时需一并调整工具和断言，不能只留场景就算完成。
3. **卡牌编辑器依赖旧渲染程序集。** CardArt.Editor 引用 RetroRendering.Editor / Runtime；CardPresentation.Editor 和测试也引用 RetroRendering.Runtime。移除对 RetroRendering 的依赖；Simple2DRenderer 一并归档。必要 UI Camera 功能改用官方 Base / Overlay Stack，Screen Space Overlay Canvas 保持普通 UI 路径。
4. **旧 3D 前端仍引用渲染代码。** Scene3D 的 `Poker.SingleBattle.Runtime` 引用 RetroRendering.Runtime。纯业务/表现代码可保留，但 VAT/渲染专用部分需拆分或归档；同步检查其他 asmdef、公共测试面板与类型调用，不能留下程序集缺失。
5. **构建入口写死。** EditorBuildSettings 仅启用 MainScene，PokerBuild 也硬编码 `Assets/Scenes/MainScene.unity`。替换开发入口时两处一起调整。
6. **Resources / 动态加载。** 保留 `StandardCardStateArt`、CardPresentation 音频和必要 DOTween 配置；GUID 扫描不能覆盖 Resources.Load、Shader.Find 和 AssetDatabase 路径常量。
7. **嵌入式 URP 已定制。** 本地 `Packages/com.unity.render-pipelines.universal` 约 124 MB，含低分辨率与原生 UI Overlay 支持。本次明确移出 dev，并在 manifest 显式声明官方 URP。当前 Unity 为 2022.3.62f3、嵌入包基线为 14.0.12，优先恢复官方同版本，避免把引擎/包升级混入迁移；由 Unity Package Manager 重新解析 lock 文件，确认 universal 来源为 registry，core / shadergraph 版本匹配。嵌入目录不移除时会继续覆盖 registry 包，不能只改 manifest。MCP 包单独保留。

## 官方 Forward 替换的具体工作

- **包与资产**：旧分支保存整个嵌入式 URP 后，dev 移除该目录，manifest 添加 `com.unity.render-pipelines.universal: 14.0.12`，重新解析依赖。新建标准管线与 Forward Renderer，Render Scale 设为 1，先采用单一默认 Renderer，无自研 Renderer Feature。
- **相机与 UI**：清除自定义 `renderOverlayAtNativeResolution` 用法；UICameraStack / ViewportOverlayCamera 仅保留业务确需的投影、视口与标准堆叠功能。CardUICameraSetup 不再复制 MainDeferredRenderer 或生成旧渲染配置。普通屏幕 UI 优先沿用 Screen Space Overlay，确需相机 UI 时使用官方 Forward 的 Base / Overlay Stack。
- **编译依赖**：已找到 `lowResolution`、`LowResolutionSettings`、`RenderPassEvent.BeforeUpscaling`、`renderOverlayAtNativeResolution`、`lastCameraRenderTargetInfo`、`CameraRenderTargetInfo` 等定制符号调用。清理范围包括运行时代码、编辑器设置/截图工具及测试，不能仅删除包目录。
- **编辑器与测试**：移除“主场景 Deferred、540 行、原生 UI Overlay、自研 2D Renderer index”等旧约定。保留抽牌、选牌、布局、Canvas、卡牌效果与实际输出检查；旧管线专项验证随旧分支归档。CardArt 的预览/导出工具改为使用标准 Forward。
- **保留卡牌外观**：卡牌 Mesh / 材质 / 图集 / 自定义 Shader 继续保留。标准 Forward 下分别验证三种世界卡牌、Unlit 验证卡牌和 Canvas 卡牌；默认 URP 不意味着必须把卡牌材质全部换成 URP/Lit。

## 执行批次

1. **保存完整现状。** 检查主仓库与子模块状态；建立旧 3D 分支，在该分支把通用代码改动和场景/资源改动分开提交，保证新增草地、遗迹、相机和 Renderer 全部进入快照。确认快照可重建后再精简 dev。
2. **回收通用代码。** 将通用 CardZoneView / 编辑器等改动按提交或 hunk 带回 dev；保留必要配置。此时建立精简前基线，运行既有关键回归。
3. **切换官方 Forward 并建立开发入口。** 先适配/归档定制接口调用，再替换包、管线与 Renderer；准备最小验证场景和构建入口，修改卡牌编辑器、2D 前端与相关测试。此批次不升级 Unity。
4. **按依赖删除旧资源。** 以保留 Prefab / 场景 / 配置为根，在 Unity 用 AssetDatabase.GetDependencies 复核，再补 HLSL include、Resources、动态路径和 Packages 依赖。资源与 meta 一起删除；保留资产原 GUID。原始单牌 PNG 作为独立清理批次。
5. **收口工具与测试。** 将只验证旧场景的入口/测试随旧分支保存，公共代码与测试仍留 dev；处理硬编码资源路径，更新目录说明。
6. **验收后提交。** 建议分为“脱离定制渲染接口”“恢复官方 URP 与 Forward 入口”“清理旧 3D 资源”“工具与文档收口”几个可回退提交。此后从旧分支回迁通用改动使用选择性 cherry-pick；不要整体 merge 旧分支回 dev，以免重新引入旧资产或删除冲突。

## 验收与回退

- dev 使用 registry 来源的官方 URP；无嵌入式 URP、自研 Simple2DRenderer、定制接口调用残留。Graphics / Quality 实际使用标准 Forward，Render Scale 为 1。
- dev 打开后无脚本编译、Shader include、Missing Script 和丢失的序列化引用错误。
- 三个世界卡牌及 CardUIItem / DeckOverview / DrawPileIcon 可实例化并正确显示；抽牌、选牌、出/弃牌与总览正常。
- 最小战斗 XML 可启动，运行规则/Hosting、CardArt、CardPresentation、CardMatch、Minimal2D 相关 EditMode / PlayMode 检查，并实际构建精简入口。
- Graphics / Quality / URP Renderer 列表、相机 index、BuildSettings 不引用已移除资产；公共菜单不再尝试加载旧环境。
- 旧分支完整保留原场景、材质、模型、贴图、Shader、meta 与依赖代码；从干净检出打开至少一个主要旧场景验证。
- 验收失败则回退相应清理提交；避免使用会覆盖当前工作的 reset --hard 或清理未跟踪文件。

本地目录大小仅供判断收益：环境模型约 141 MB，角色约 5.3 MB，怪物约 11 MB，新增环境目录约 30 MB；非 Git 压缩体积，也不代表全部可删。删除当前分支文件不会移除 Git 历史中的大对象，因此不会同等减少完整 clone 体积；历史瘦身不纳入本次迁移。

## 本次检查的边界

已检查：工作区/分支/子模块指针、资源目录、本地大小、Prefab 与场景的静态 GUID 引用、Shader include、程序集引用、资源加载与构建路径。本轮没有运行 Unity 的完整依赖解析、编译或回归；以上是迁移设计与静态清单，不是迁移完成报告。
