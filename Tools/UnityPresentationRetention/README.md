# Unity 表现层保留清单

收集日期：2026-10-24。来源：`legacy-3d-scenes`，主仓库提交 `72a1553da8a16815a4eaf63cc4208987853af5bc`。

最初收集自研渲染管线、Card Instancing、Shader 和基础桥接层，原始文件路径、程序集引用和资源 GUID 依赖保存在 [inventory.json](inventory.json)。随后按用户要求建立 `Palamedes` 分支，并清理四类模块之外的 Unity 表现代码和资源；实际保留、补充依赖及删除路径见 [cleanup-plan.json](cleanup-plan.json)。原始 inventory 是收集时的快照，不应作为后续自动删除白名单。

清理后没有默认游戏场景，Build Settings 场景列表为空。保留模块的工具和测试继续存在。规则子仓库保持清理前工作区版本，具体变更和验证见本目录的清理报告。

## 1. 渲染管线与配套资源

| 保留位置 | 内容 |
| --- | --- |
| `Packages/com.unity.render-pipelines.universal/` | 内嵌 URP 14.0.12，包含项目定制修改；整包作为保留基线 |
| `Assets/Scripts/Infrastructure/Rendering/Runtime/Features/` | 复古后处理、SSAO、高度雾、体积光、Bloom、CRT、深度法线描边、角色描边 |
| `Assets/Scripts/Infrastructure/Rendering/Runtime/Forward2D/` | 独立 Simple2DRenderer / Simple2DRendererData |
| `Assets/Scripts/Infrastructure/Rendering/Runtime/Cameras/` | UICameraStack、ViewportOverlayCamera |
| `Assets/Scripts/Infrastructure/Rendering/Runtime/VAT/` | RigidVATPlayer 通用顶点动画播放 |
| `Assets/Scripts/Infrastructure/Rendering/Runtime/Environment/` | 体积灯、萤火虫和火焰灯表现组件 |
| `Assets/Scripts/Infrastructure/Rendering/Runtime/Infrastructure/` | 帧率配置与采样 |
| `Assets/GameAssets/Rendering/` | Shader/HLSL、噪声纹理、萤火虫材质和预制体 |
| `Assets/Settings/` 下列配置 | MainURP、MainDeferredRenderer、UIRenderer、CardForwardRenderer、CharacterRenderer、Battle2DRenderer |

`MainURP` 还引用 `Assets/Art/Environment/StylizedRuins/Rendering/StylizedRuinsForwardRenderer.asset`，已由资源依赖扫描补入。不能仅因为它位于环境目录而遗漏。

管线定制涉及低分辨率渲染、Point 输出、原生分辨率 Overlay UI 和 `BeforeUpscaling` 时机。相关代码分布在内嵌 URP 的 `LowResolutionSettings`、`UniversalRenderer`、`UniversalRendererData`、`UniversalAdditionalCameraData`、`UniversalRenderPipeline` 等文件；这不是仅复制 Renderer Features 就能完整保留的功能。旧工程版本为 Unity 2022.3.62f3。

## 2. Card Instancing

保留 `Assets/Scripts/Presentation/CardArt/Runtime/` 全模块：

- `CardView`：共享 Mesh / Material，使用 MaterialPropertyBlock 写入单牌参数。
- `CardAtlasData`：图集与正背面区域。
- `CardArtProfile` / `CardAppearance`：外观组合和实例参数。
- `CardShaderProperties`：Shader 属性与接口校验。

配套保留：

- `Assets/GameAssets/CardArt/`：Card、ClassicCard、FourBeastsCard 预制体、材质、Profile、Shader 与效果纹理。
- `Assets/Art/CardAtlases/`：Classic / FourBeasts 图集、映射数据。
- `Assets/Art/Models/Cards/`：卡牌网格及导入信息。
- 资源依赖扫描补入的 `Assets/Art/Materials/Cards/FourBeastsCards.mat`。

当前实现采用 MeshRenderer + 共享资源 + MPB + Shader instanced properties。CardDeferred 的各绘制 Pass 声明实例化变体，ClassicCard / FourBeastsCard 材质启用了 GPU Instancing。实例参数覆盖牌面 UV、Tint、镭射、火漆与红叉。此次确认了源码和资产配置，未通过 Frame Debugger 或 GPU 抓帧确认实际合批。

`CardArt.Runtime` 无项目程序集依赖，可以脱离旧 CardInteraction、CardPresentation 和战斗玩法 UI 保留。旧手牌布局、拖拽、悬停和按钮不属于本次 Card Instancing 保留范围。

## 3. 全部 Shader 源码

Assets 下共 40 个 `.shader`、22 个 `.hlsl`、4 个 `.cginc`，全部列入清单；内嵌 URP 自带 Shader 随管线包保留，不计入上述数量。

| 位置 | 源码文件数 | 内容 |
| --- | ---: | --- |
| `Assets/GameAssets/Rendering/Shaders/` | 20 | 后处理、雾、体积光、角色、桌面、石像鬼、VAT |
| `Assets/GameAssets/CardArt/Shaders/` | 5 | CardDeferred、验证 Unlit 与共享卡面计算 |
| `Assets/GameAssets/CardPresentation/UI/Shaders/` | 1 | CardCanvas；依赖 CardArt 的 CardSurfaceCommon.hlsl |
| `Assets/GameAssets/SingleBattle/Presentation/Scene3D/Shaders/` | 2 | ShieldCard 与配套 HLSL |
| `Assets/Art/Environment/StylizedGrass/Shaders/` | 4 | 草与地面 |
| `Assets/Art/Environment/StylizedRuins/Shaders/` | 2 | 废墟背景与绘画地面 |
| `Assets/Art/Models/Environment/ForestRuins/Shaders/` | 3 | 森林废墟 Deferred |
| `Assets/Art/Models/Environment/GothicChapel/Shaders/` | 6 | 教堂、旗帜、火焰 |
| `Assets/Art/VFX/HitFlashes/Shaders/` | 3 | 闪光、溶解与共享粒子代码 |
| `Assets/GameAssets/Editor/Shaders/` | 2 | VAT、裂纹验证 |
| `Assets/HandSkinValidation/HandReview.shader` | 1 | 手部验证 |
| `Assets/TextMesh Pro/Shaders/` | 17 | TMP 配套 Shader 与 Include |

Shader 连同 `.meta` 和 Include 文件保留；保留 Shader 不代表同时选用整套角色、环境模型或旧场景。除已收集的卡牌和渲染资源外，具体效果的材质、纹理和驱动组件，在接入该效果时再选取。Shader 和第三方来源说明应一并保留。

## 4. 基础桥接与通用表现框架

| 保留模块 | 职责 |
| --- | --- |
| `Gameplay/SingleBattle/Hosting/Runtime` | BaseBattleHost、BattleSceneHost、IBattleSessionSource / IBattleSceneAdapter、XmlBattleSessionSource |
| `Gameplay/SingleBattle/Presentation/Shared/Runtime` | BattlePresentationBridge、BattleDisplayProjector、BattleInteractionQueries、显示文案投影 |
| `Gameplay/SingleBattle/Presentation/Shared/Unity` | BattleTimelineSceneAdapter：通用会话、输入与时间线接入 |
| `Presentation/BattlePresentation/Runtime` | 显示/动作/交互协议、DisplayStore、PresentationDirector、InteractionSession、PresentationScope |
| `Presentation/BattlePresentation/Unity` | ScenePresentationDirector、时间线筛选、目标绑定及通用动作组件 |
| `Assets/GameAssets/SingleBattle/Hosting/` | BattleSceneHostMinimal 最小入口预制体 |

上表代码均位于 `Assets/Scripts/` 下。最小链路为：

```text
Unity 输入 → BattleTimelineSceneAdapter → BattleSessionPlayback → C# BattleSession
                                                                    ↓
Unity 视图 ← ScenePresentationDirector / DisplayStore ← Bridge ← Result / Timeline
```

规则和玩法继续属于 `External/BattleToolkit/Engine`。Hosting 不拥有伤害、回合或胜负算法；Shared 负责查询与投影；通用表现框架消费已结算的事实。

旧 `SingleBattleSceneAdapter`、Actor、HUD 和具体战斗动作不是最小桥接的必需依赖，暂不纳入。旧 CardMatch / CardInteraction / CardPresentation 同样不必为了保留 CardArt 和基本桥接而整体带入。

**版本适配：** 清理时新增了 `BattleTimelineSceneAdapter.SubmitAction` 通用转发入口，可传入动作、选牌、目标、目标槽绑定和摸牌数量；现有出牌、弃牌、结束回合入口复用它。当前规则的手动摸牌已通过最小宿主集成测试。新前端的完整玩法 UI 和新增事实显示仍待后续重构，不由这个基础适配器实现。

## 5. 工具、依赖与验证范围

随功能收集渲染、CardArt、Hosting、Bridge、BattlePresentation 的现有编辑器工具和测试。CardArt.Editor 引用 RetroRendering.Editor 与 Poker.EditorTools，因此公共 EditorTools 也被纳入。清理后 Unity EditMode 174 项测试全部通过，结果见 [清理报告](cleanup-report.md)。

配套依赖包括规则 UPM 包、Core RP、ShaderGraph、UGUI、TextMeshPro 和 Unity Test Framework。`Packages/manifest.json` / `packages-lock.json` 以及 GraphicsSettings、QualitySettings、TagManager 需要在后续工程整理中按引用迁移。文件清单会保留所选资产和父目录的 `.meta`，避免丢失 GUID。

静态检查结果：

- 对所选运行时资产及其递归 YAML 引用，记录 195 条 GUID 引用，指向 73 个不同文件；在当前 Assets、Packages 和规则包中均找到对应 GUID。
- 所收集程序集没有引用被排除的本地项目程序集；规则程序集和 Unity/包依赖按外部前置条件保留。
- 这不覆盖动态字符串加载、所有 Shader 编译变体、GPU 合批或当前规则版本的接口兼容性，也没有验证旧完整场景可以运行。

其余场景、角色/环境资产库、旧战斗 UI、旧交互实现、DrawBattle / RunFlow / Minimal2D 前端已在 Palamedes 清理；依赖扫描发现的必要文件除外。旧版本仍可在 legacy-3d-scenes 查看。
