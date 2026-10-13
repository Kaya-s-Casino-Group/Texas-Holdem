# 卡牌 UI 资源

可复用的提示 Prefab 由场景 `CardHoverPresenter` 持有，多个牌区共用一份运行时提示实例；数据通过 `ICardHoverSource` 提供。所有权、权限与运行时接入见 [交互模块](../../CardInteraction/README.md#场景共用的悬停提示)。

## 资源

| 资源 | 路径 | 配置 |
| --- | --- | --- |
| 背景 | `Textures/CardSuitChipsBackground.png` | 1484 × 1060，RGBA；Sprite/Single/FullRect，无压缩、无 mipmap、Bilinear/Clamp |
| 字体 | `Assets/Fonts/FusionPixelFont/TMP/FusionPixel12pxProportional.asset` | Fusion Pixel 12px 比例简体中文，12px RASTER_HINTED 位图，Point/Clamp，动态字符图集 |
| 提示 Prefab | `Assets/CardPresentation/Prefabs/CardSuitChipsTooltip.prefab` | World Space Canvas，上方花色/点数、下方筹码 |

背景完整复制自 `ArtDirection/UI/card-suit-chips-background-v1.png`，保留原始透明边缘、上下两块底板及比例。纹理不做九宫格拉伸；背景 Image 使用 Simple、Preserve Aspect。缩放根节点可以整体调整尺寸。

字体 Asset 保存自身材质与 1024 × 1024 位图图集，直接引用原始 `fusion-pixel-12px-proportional-zh_hans.ttf`，预填 ASCII、四种花色名称、“筹码”和原生花色字形 `♠ ♥ ♣ ♦`（U+2660/U+2665/U+2663/U+2666）。花色字形不用 Sprite 或其他字体替代。动态模式可补充后续中文；原字体许可证沿用 FusionPixelFont 目录中的 OFL 与来源说明。标准 TMP Essential Resources 位于 `Assets/TextMesh Pro`，提示的两行文字都显式使用 Fusion Pixel，不修改全局默认字体。

## Prefab 结构

```text
CardSuitChipsTooltip         RectTransform + World Space Canvas + CanvasGroup + CardTooltipView
├── Background              Image
├── SuitRank                TextMeshProUGUI：♣ 梅花 J
└── Chips                   TextMeshProUGUI：<color=#009AFF>+10</color>筹码
```

画布为 448 × 320，根节点等比缩放 0.0004，文字字号分别为 48 与 60（12px 的整数倍）。上栏按“花色符号 + 花色文字 + 点数”排列：黑桃/梅花整行深棕色，红桃/方块整行红色（默认 #C73532），包含符号、文字和点数。下栏数字始终蓝色，筹码文字深棕色。文字居中、不换行，超长内容采用省略；两栏的位置和尺寸均可在 Prefab 内调整。Prefab 的“+10”是预览数值；运行时由宿主提供基础/名义筹码数据，不代表最终结算。

`CardTooltipView` 保存当前展示内容及三种颜色配置，可在 Prefab Inspector 修改 Suit、Rank、Chip Points 预览不同卡牌；内容变化会重新填写两个 TMP 文本节点。调用方也可使用：

```csharp
tooltip.SetContent(Suit.Hearts, Rank.Jack, 10); // ♥ 红桃 J，整个上栏红色；+10筹码
tooltip.SetContent(Suit.Clubs, Rank.Jack, 10);  // ♣ 梅花 J，整个上栏恢复深色
```

View 接收已经解析、允许当前观察者读取的花色/点数和筹码，不查询规则引擎，不更改卡牌属性，也不负责鼠标或显隐。Presenter 消费来源提供的数据并调用 SetContent。负数筹码只显示负号，零及正数显示加号。

根节点与子节点使用 UI Layer，Prefab 不保存场景相机引用。Presenter 按场景相机计算卡牌上方的位置、朝向和视口边界；尺寸和偏移由场景 Presenter 配置。独立 UI Camera 绘制提示并保持场景相机的投影，Canvas.worldCamera 绑定 UI Camera；没有独立相机时回退到场景相机，实际绘制相机的 Culling Mask 必须包含 UI。无 GraphicRaycaster，CanvasGroup 的 Interactable/Blocks Raycasts 与各 Graphic 的 Raycast Target 均关闭，提示不会抢占卡牌悬停。

## 编辑器工具

- `Tools > Cards > Prepare Suit And Chips Tooltip Prefab`：准备缺失的背景、字体和 Prefab；补充四种花色字形并为旧模板绑定 CardTooltipView，重复执行保留 GUID、布局和颜色设置。不在场景安装对象或逻辑。
- `Tools > Cards > Capture Suit And Chips Tooltip Prefab`：在独立 Preview Scene 中渲染，输出 `Library/CardTooltipValidation/tooltip.png` 及四种花色的 `tooltip_clubs/diamonds/hearts/spades.png`；检查上栏实际使用 Fusion Pixel 字形，关闭临时场景并释放资源，不修改或保存用户场景。
- `Tools > Cards > Configure Card Hover Tooltip`：将选中的固化手牌（或当前场景的全部固化手牌）注册到场景提示 Presenter；固化手牌时也自动接入。进入 Play 后在 Game 悬停查看。工具不自动保存场景。

生成工具位于 `Editor/CardTooltipPrefabBuilder.cs`，展示组件位于 `Runtime/CardTooltipView.cs`，各有独立 .meta。CardPresentation.Runtime 新增 UGUI/TMP 引用；离屏渲染依赖仍只在 Editor，PokerEngine 的纯 C# 模块不引用 UI。Prefab 及引用的 Sprite、TMP 字体/材质属于普通 Assets，可随引用它们的游戏场景进入首包，展示组件沿用现有 link.xml 的保留规则。

## 手牌操作按钮

`Assets/CardPresentation/Prefabs/HandActionPanel.prefab` 使用同一 Fusion Pixel TMP 字体，包含 Actions 背景、Play / Discard / RankSort / SuitSort 四个 Button，不再保留 Status 节点。Screen Space Camera Canvas 排序 100、Plane Distance 1，场景实例绑定独立 UI Camera；CanvasScaler 以 1280 × 720 缩放。Actions 锚定左下角，边距 (16, 16)，基准大小 316 × 124、局部缩放 0.75；第一行出牌/弃牌，第二行点数/花色理牌，没有牌堆数字和“优先”状态行。四按钮的尺寸/位置/色块可以在 Prefab 修改，当前是交互验证用占位美术。

Prefab 只持有 `HandActionPanelView` 和 UI 引用；场景 `HandActionPresenter` 引用实际手牌宿主和出牌路径，提交出/弃/理牌命令。所有文字显式使用 Fusion Pixel，只有按钮 Image 开启 Raycast Target，其余 Graphic 关闭。准备/配置菜单位于 `Editor/HandActionPanelSetup.cs`，固化手牌时自动安装；场景同时需要 EventSystem。玩法数据和按钮权限见 [交互模块](../../CardInteraction/README.md#game-中的手牌操作按钮)。

`Tools > Cards > Use Compact Hand Action UI Camera` 将已有 Prefab 更新为上述紧凑布局并连接当前场景，保留 GUID 和美术颜色。独立 `UIRenderer.asset` 使用 Forward，不添加自定义 Feature/后处理；UI Camera 为主相机的 Overlay，关闭阴影及深度/颜色纹理请求。Stack 先将场景 Point 放大，再由 UI Camera 按输出分辨率绘制，UI 不降采样。详见 [渲染模块](../../Rendering/README.md)。

## 右下角抽牌堆

`Assets/CardPresentation/Prefabs/DrawPileIcon.prefab` 是独立 Screen Space Camera Canvas，共用 UI Camera 和 1280 × 720 参考分辨率，排序 300，使牌组总览打开后仍可点击牌堆。Icon 锚定右下角，保留边距 (20, 20)、大小 64 × 96。Deck 子节点使用最多八层 CardUIGraphic 采样当前手牌 CardView.Atlas 的 card_back，错位、逐层乘色与阴影组成有厚度的牌堆；不再使用独立牌背 PNG / RawImage。所有层共享原生 Card Canvas 材质和图集，余量控制厚度，空堆保留暗色单层。右下角 Remaining/Count 以 Fusion Pixel 字体显示当前剩余牌数（含 0），带深色底、随 Deck 悬停缩放，不接收射线；可在 Prefab 调整位置/大小/配色，Prepare 只补缺失节点并保留现有封面与布局。

顶层由 DrawPileView 的 Cover Card Id 指定，默认 card_back；SetCoverCard("hearts_q") 等可换为当前图集内的指定牌面，非法 id 在改变已有图标前拒绝。这是牌组外观配置，不读取隐藏的牌顶卡牌。DrawPilePresenter 从 CardMatchHost 的 DeckCards 读取余量，Refresh 跟随当前手牌图集；不保存另一份牌堆、不发起抽牌命令。

原生 GraphicRaycaster 只命中 Icon 的固定透明 Image；真正的图集卡牌/阴影不接收射线。DrawPileView 输出 HoverChanged / Clicked，DOTween 只缩放 Deck 子节点至 √1.2（约 1.095），0.14 秒 OutBack 惯性放大/回落，固定命中区域不跟随动画变动。DrawPilePresenter 绑定同一 Match 的 DeckOverviewPresenter：悬停打开独立 DeckSummary 紧凑统计表，鼠标离开图标立即开始惯性收起，移入面板不续留。左键点击打开并固定完整 DeckOverview 四行卡牌页，再点击取消固定并回到悬停统计表；返回关闭并取消固定，仍停在图标上时不会立即重开。

Prepare Draw Pile Icon Prefab 迁移旧模板，保留 GUID 与 Icon 位置/尺寸；Configure Deck Icon And Overview 一次连接当前场景图标、总览与共用悬停提示。固化手牌/按钮接入也自动连接。资源沿普通场景/Prefab 引用进入首包，工具只标记场景 dirty。

## 原生 Canvas 牌组总览

`DeckOverview.prefab` 是 Screen Space Camera 页面，使用既有 UI Camera/Forward Renderer 和 Fusion Pixel 字体。面板缩至 1040 × 632（原 1200 × 642），右侧 ♠ / ♥ / ♣ / ♦ 四行按 A、K、Q、J、10…2 排列，每个实例独立展示，同牌面多份保留原稳定次序。行宽由共同父级决定，卡牌基准高度 120，浅弧高度 5、边距 2（以行高 132 为参考）；按牌数填充固定行宽，使用与手牌相同的 CardZoneRowLayout 圆弧/边界算法，映射 XZ 到 Canvas XY，卡牌轻微倾斜并从左向右重叠。单牌居中，空行保留槽位；SetRowLayout 或 Inspector 可调卡牌高度/弧高/边距，不改变固定行宽。

四行的共同父级为 `DeckOverview/Frame/Panel/SuitRows`（生成基准位置 164, -8，大小 680 × 528；已有 Prefab 调整保留）。直接修改父级 RectTransform 的位置、Width/Height 或 Scale 即可整体调整牌区。四行等高，宽度撑满父级；`Vertical Overlap` 默认 0.12、范围 0–0.3，通过 Inspector 或 `SetVerticalOverlap` 调整。行高为区域高度 / (4 − 3 × overlap)，中心间距为行高 × (1 − overlap)，四行始终填满区域；0 恢复四分之一高度。渲染前检测尺寸变化并复用现有卡牌重排，Edit/Play 都生效。卡牌高度、弧高及边距以默认行高 132 为基准按区域高度同比变化，极窄区域会额外缩小卡牌以容纳完整牌面；左侧统计、点数列和标题不属于该父级。`SuitRowsRoot` 提供运行时引用，`RefreshLayout` 可显式刷新。

左侧参考小丑牌的独立牌组信息板，保留每花色数字牌（2–10）、J/Q/K、A 与合计，并增加 A/人头牌/数字牌的总数及旁侧 A…2 余量。三枚分类图标见 [Icons](Icons/README.md)，使用内置 image_gen 生成的简单手绘像素 PNG；花色符号仍来自现有字体。已抽出卡仍占原位置，Opacity 0.28、去饱和 0.65，悬停提高至 0.48。隐藏或非标准牌不推断花色点数，仅显示汇总数量。

`CardUIItem.prefab` 使用 `CardUIGraphic : MaskableGraphic` 与共享 `FourBeastsCanvas.mat`，直接采样 `FourBeastsAtlas.asset`，没有 MeshRenderer、预览相机或 RenderTexture。图集坐标与手牌使用同一 CardAtlasSampleUV，完整区域映射、仅边界半像素钳制，不缩放整幅牌面；共用原纹理的 Point/无 mipmap/无压缩设置。Canvas Pixel Perfect；1920 × 1080 输出时，场景手牌仍为 960 × 540 再 Point 放大，UI 卡牌直接绘制到 1920 × 1080，不再降分辨率。

Tint、HDR 镭射颜色/强度、火漆颜色/贡献、红叉、去色通过顶点颜色与 UV0–3 传递；透明度使用原生顶点 Alpha，不更改 Card Tint。UV3 打包标志和去色，并保留完整图集区域尺寸及 Tint 曝光。图集数据不存法线，防止 Canvas 合批变换/idle 旋转改变采样区域；Normal 只表示几何方向。所有牌共享材质。Canvas 启用额外 UV/Normal 通道，Shader 支持透明混合、CanvasGroup Alpha、Stencil 和 RectMask2D；采用原生 Canvas 批处理，不使用 GPU instancing。

`CardCanvas.shader` 不受场景灯光、阴影或 GBuffer 影响；火漆采用固定装饰光照。图集采样、镭射光谱和火漆坐标计算与场景卡牌共用 `CardArt/Shaders/CardSurfaceCommon.hlsl`，纹理直接复用已有资源。场景 Deferred Shader 保持原渲染路径。

`CardUIItemView` 持有本页逐牌悬停投影，仅查看、无待提交选中。悬停通过 DeckOverviewPresenter 的 ICardHoverSource 接口复用场景 CardHoverPresenter / CardSuitChipsTooltip，显示在卡牌左侧；上栏花色/文字/点数、下栏基础筹码，红色花色整行红色。已抽出牌也可查看；隐藏/未知牌不显示。总览打开时独占提示来源，避免背后手牌悬停串入；移出隐藏，关闭恢复手牌。Inspector 的 Canvas Size Multiplier / Canvas Gap 可调尺寸和左侧间距，使用页面 CanvasScaler 与原生 UI Camera，提示不拦截射线。DOTween 使用独立时间做约 1.095 倍放大/回落，idle 使用原牌区的逐牌随机幅度/周期/相位，轻微绕 local Y 摆动，并附加 0.55 倍幅度的 local Z 平面摆角，让无光照 Canvas 上的摆动也可见；每张牌独立、悬停时继续。原位槽位不动，悬停牌在本行及四行之间置顶，退出恢复花色顺序。关闭页面清除悬停与自身 Tween，不修改手牌交互或 Match；重新刷新复用视觉身份，编辑器重载清理未被映射的临时卡牌。

`DeckOverviewPresenter.Configure(match, view, deckZoneId, rosterIds)` 从明确的牌组成员集合与 Match 抽牌区构建只读投影，剩余状态来自 DeckZone 成员关系，不写入 CardEffects。`SetRoster` 供正式构筑流程更新成员；新加入绑定抽牌堆的实例也会纳入，未属于该牌组的其他玩家卡牌不纳入。统计按 DefinitionId 解析规则花色/点数，禁用及失效牌仍计入余量；不从美术文件名推断。启动工具只从手牌/绑定抽牌堆/弃牌堆/同 Match 出牌区的启动数据引导初始成员。

- `Tools > Cards > Prepare Canvas Deck Overview Prefab`：准备共享材质与两个 Prefab，已有美术设置保留。
- `Tools > Cards > Update Canvas Deck Overview Layout`：明确升级为当前紧凑布局，保留 Prefab GUID、图集与共享材质，重新绑定当前场景；会更新总览布局，日常 Prepare 不覆盖已有新版布局。
- `Tools > Cards > Show Canvas Deck Overview`：注册当前场景的局内对局与 UI Camera 并打开页面；Play 时打开已有实例。
- `Tools > Cards > Capture Canvas Deck Overview Prefab`：独立 Preview Scene 验证 52 张样例及效果，输出 `Library/DeckOverviewValidation/overview.png`；离屏验证只在编辑器使用，不属于运行方案。
- `Tools > 测试面板 > 截图 > 牌库与单牌提示`：捕获当前 Game 中卡牌左侧的共用信息提示，写入 `Library/DeckOverviewValidation/hover.png`；捕获后恢复原悬停和页面状态，不修改对局。
- 玩法通过 Presenter.Open / Close 控制页面，图标由 DrawPilePresenter 管理预览/固定显示，“返回”关闭并取消固定。
- Tools > 测试面板 > 实战验证 > 牌堆与总览动画 检查指针事件、快速反向、上移回弹、两种提示惯性与固定显示，结果写入 Library/DeckOverviewValidation/motion.json。

新增运行脚本、Shader、材质和 Prefab 带独立 .meta，沿现有 Runtime asmdef/link.xml 和场景资源引用进入首包；Editor 工具和 Play 验证菜单整文件用 UNITY_EDITOR 限定。工具只标记场景 dirty，不自动保存。

总览 Frame 从下方 90 Canvas 单位以 0.22 秒 OutBack 上移回稳，CanvasGroup 同步出现；关闭以 0.14 秒向下退场，立即释放输入/提示独占，动画结束后停用。入口距离/时长可在 DeckOverviewView Inspector 调整，重复目标不重播，中途反向从当前姿态继续。Card Hover UI 的 Show Seconds 默认 0.16、Hide Seconds 默认 0.12，手牌与总览提示共用惯性放大/缩小；定位按最终尺寸计算，不随动画抖动。所有 Tween 使用不受暂停影响的时间，各组件只回收自己持有的 Tween。

## 牌堆悬停统计表

`Prefabs/DeckSummary.prefab` 是独立原生 Canvas 片段（RectTransform 896 × 272），由 DrawPilePresenter 持有 Prefab 引用并按需实例化为牌堆 Canvas 的子节点。默认在 icon 上方 14 Canvas 单位、右侧对齐，沿用 UI Camera 原生输出与 Fusion Pixel 字体；Summary Gap 在 Presenter Inspector 调整，整体尺寸及排版可修改 Prefab。悬停表无 Raycaster，所有 Graphic 和 CanvasGroup 都不接收输入，离开 icon 即收起；点击切换为既有完整卡牌总览，返回关闭且不会因仍悬停而重开。

参考紧凑牌组表排版：上方 A、K、Q、J、10…2 十三列，每列白色点数牌签及剩余合计；下方 ♠、♥、♣、♦ 四条蓝/红/绿/橙色行，52 个格子显示对应花色与点数余量；左侧花色标记与该花色剩余总数，0 降低透明度。统计按获准查看的 Definition 计数，多份同牌分别计入，已抽出不计入；禁用/失效仍属于剩余牌。隐藏或未知牌只显示“其他 / 不可见”汇总，不猜测分布。

DeckSummaryView 只消费 DeckOverviewData；DeckOverviewPresenter.ReadData 与完整页共用缓存投影，刷新完整页不会因统计表先读取而跳过更新。数据层增加 Remaining(rank) / Remaining(suit, rank) 查询，不改变 Match、Zone 或卡牌属性。PixelPanelGraphic 以原生 Canvas 顶点生成像素阶梯边框和底板，共享默认 UI 材质，不新增位图或动态材质。

显隐使用本组件拥有的 DOTween，默认从下方 28 单位快速上移并由 0.94 倍回稳到 1 倍（0.18 秒 OutBack），0.12 秒缩小/下移渐隐；快速反向保留当前姿态，统计刷新不重播动画。停用和销毁只清理自己的 Tween。Prepare Deck Hover Summary Prefab 只准备缺失资源/渲染组件，保留 GUID 和布局；Configure Deck Icon And Overview 自动绑定。Editor-only 的 Capture Deck Hover Summary In Play 捕获真实 Game 画面到 Library/DeckOverviewValidation/hover-summary.png 并恢复交互状态。
