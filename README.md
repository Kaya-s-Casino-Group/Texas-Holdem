# 扑克时光 / Pocket Poker

Unity 2022.3 卡牌游戏项目，面向肉鸽与 PvP 共用的纯 C# 对局引擎开发。当前包含共享 PokerEngine、卡牌资源工具与渲染场景。

## 开始

打开 `Assets/Scenes/MainScene.unity` 查看当前主场景；`Assets/Scenes/CardRenderingValidation.unity` 用于卡牌材质、光照和阴影验证。

早期单人原型的规则、AI 和自动启动 IMGUI 牌桌已移除。共享 HoldemMatchEngine 已可独立执行对局，场景交互与 AI 尚未接入；进入 Play 不会自动创建旧牌桌。

## 构建

Unity 菜单 `Poker > Build macOS Game` 构建当前 `MainScene`，输出 `Builds/Pocket Poker.app`。该入口不负责创建对局或游戏界面。

## 卡牌动效

已从 [DOTween 官方下载页](https://dotween.demigiant.com/download) 导入免费版 1.3.030，并通过 `Tools > Demigiant > DOTween Utility Panel > Setup DOTween` 完成设置。已启用 UI 和 Sprite 模块。脚本中添加 `using DG.Tweening;` 后，可以用 `RectTransform.DOAnchorPos`、`Transform.DOScale`、`CanvasGroup.DOFade` 和 `DOTween.Sequence()` 编排发牌、翻牌和收牌动效。

## Unity MCP

项目内嵌 [MCP Unity 1.5.0](https://github.com/CoderGamester/mcp-unity/tree/1.5.0)，支持当前 Unity 2022.3。Codex 的项目级配置在 `.codex/config.toml`，打开此项目时自动加载。需要 Node.js 18 或更新版本。

首次打开 Unity 时，等待包导入完成。桥接服务默认自动启动；也可在 `Tools > MCP Unity > Server Window` 查看状态或手动点击 **Start Server**。重新打开 Codex 任务，使新 MCP 配置生效，然后可让 Codex“读取当前 Unity 场景层级”验证连接。

如果桥接端尚未构建，在 `Packages/com.gamelovers.mcp-unity/Server~` 中运行 `npm ci && npm run build`。认证令牌由 Unity 写入 `Library/McpUnity/bridge-token`，不会提交到仓库。默认仅允许本机连接，也不允许 MCP 客户端安装 Unity 包。

## 单牌区展示

`Assets/CardPresentation` 提供无交互的 `CardZoneView`：接收可见牌区数据、复用卡牌视觉实例并将有序列表排为圆弧或直排。`CardInstanceView` 绑定展示身份及可选逻辑卡牌身份，内部实例化现有 CardArt Prefab，继续由 CardView 渲染牌面与颜色。支持未知牌背、增删重排、矩形边界、间距压缩和重叠。`HandZoneView : CardZoneView` 承接手牌的抽牌/弃牌动画、悬停缩放、选中上浮及三组音效；`PlayedZoneView : CardZoneView` 提供五个固定直排槽位，最多五张牌，无弧度和持续自转；验证窗口可直接创建出牌区。弃牌区只保留数据和虚拟终点，不创建 View。验证窗口按实际子类匹配配置，可跟随场景选择切换任意牌区，并通过“导出 View Prefab…”保存当前子类的展示设置，默认输出到 `Assets/CardPresentation/Prefabs`。接入说明见 [CardPresentation README](Assets/CardPresentation/README.md)。

新增牌会按列表顺序从可配置的右侧牌堆位置背面朝上依次飞入：可见牌在途中平滑翻正，不可见牌保持牌背；入列后过冲回摆，已有牌平滑让位。翻面只改变视觉旋转，牌面、属性和观察者可见性在生成时即已确定，动画不会修改这些数据。Present 比较完整快照，不要求调用方驱动单牌动画；牌堆可使用局部坐标或场景 Transform。提供立即恢复/完成动画接口。验证窗口的独立“动画实验”页读取并编辑绑定对局的抽牌堆，通过 Core.DrawTo 抽取 1/3/指定张数，展示抽牌前后列表、逐牌动画状态和实际/目标位置；可暂停、调速、单步推进及调整飞入/惯性参数。`Tools > Cards > Card Zone Animation Lab` 可直接打开此页。

抽牌、弃牌和选中默认使用随首包提供的 `CardDraw.wav` 占位音效，各自独立设置开关、音频和音量。“牌区样式 → 音效”提供三组配置，进入 Play 后试听；抽牌在新牌飞入结束、入列开始惯性回稳时播放，弃牌仍在开始飞出时播放，选中在卡牌由未选中变为选中时播放。连续操作允许尾音重叠，关闭某一种音效不影响其他音效。悬停、取消选中、重复快照、排序和静默恢复不播放选中音效。

默认惯性采用 0.65 秒柔和回稳、0.006 过冲和 3° 摆角；飞入和回稳的衔接速度平滑归零。“动画参数”中的“应用柔和惯性”按钮可将现有预览调整为这组参数，保留牌堆、手牌及其他配置。

牌列沿圆弧排列，每张牌的布局根节点 local +Z 指向同一圆心，牌面子节点补偿半圈旋转以保持正向，并通过 DOTween 给每张牌添加独立的 local Y 轻微摆动；默认弧高 0.008、最大摆角 1.5°、完整周期 3.5–6.5 秒。表现随机按 ViewId 稳定生成，刷新/重排不重启，不影响规则数据或对局随机。牌区可选 +Y 指向指定摄像机（未指定时使用 MainCamera），关闭后恢复原朝向。“布局与样式”页可调布局和相机朝向，“动画实验”页控制播放/暂停及摆动参数；`Tools > Cards > Preview Gentle Row Motion` 可快速应用预设。

`Tools > Cards > Card Zone Validator` 打开统一验证窗口，分为“手牌列表 / 抽牌实验 / 牌区样式 / 诊断”。手牌和测试牌堆均可任意插入、改牌面和属性、复制、移动、排序、删除；可指定任意牌面、数量和手牌位置直接抽取，也可抽出牌堆中的任意一张。测试牌堆按实际抽牌顺序从上往下显示，第一行标记为“牌顶（下一张）”；抽 1 张取第一行，抽 N 张依次取前 N 行，移动、复制和插入位置均对应显示序号。牌堆与弃牌堆由场景 CardMatchHost 的共享 CardTableSession 维护；验证窗口读取对局，Edit 配置 Match 启动快照、Play 调试运行会话。共享同一堆的牌区始终看到同一份数据，关闭窗口不影响对局；重开由 Match 统一恢复。编辑模式支持撤销；Play 内同样可操作运行时副本，保留鼠标交互，退出恢复初始数据。牌面、正反面、强化、塑封、失效、禁用、悬停、选中和乘色直接在每张牌所在行配置，行尾保留移动、复制、清除、抽出及删除；窄窗口可横向滚动。常用列表操作与动画/音效/外观设置、数据诊断分开。创建牌区仍用 `Create Card Zone In Current Scene`，工具不自动保存整个场景。

“卡牌工具”（Atlas Builder / Prefab Exporter）与“牌区验证”的最小尺寸为 360 × 260 UI 点。浮动时可拖动底栏移动窗口、拖动右下角三角手柄缩放，Esc 取消本次拖动；原生标题栏和边框也保留。拖动顶部标签页可移出或停靠，停靠后通过面板分隔线调整大小。牌区验证固定顶部导航、正文滚动，小窗口下仍可访问全部操作。

卡牌效果采用可组合的 `CardEffects`（强化、塑封、失效），禁用独立控制能否打出/丢弃。视图消费卡牌属性与本地 `CardInteractionState`；悬停以独立 DOTween 将面积扩大 20%（长宽等比缩放至 √1.2 ≈ 1.0954），不产生位移；选中（待提交）仍沿牌区局部 +Z 上浮，取消选中回到原位。两种效果可叠加；鼠标离开只恢复大小。放大/上浮默认 0.12 秒、缩小/回落 0.14 秒，都包含惯性回稳；窗口可调整时长和惯性强度。选中上浮使用更明显的惯性回弹（默认约越过本次位移的 25%），摆动角度随高度同步回稳归零，完成后保持静止；取消后从实际角度恢复摆动。失效牌保留牌型贡献，跳过自身整段逐牌计分。属性经统一外观解析器驱动 Shader：强化显示火漆、塑封开启镭射、禁用显示红叉、失效压暗并抑制特效发光；牌背隐藏全部正面特效。验证窗口提供“Shader 效果对照（8 张）”与样式参数，继续共享 Mesh/Material。

选中后可在验证窗口“手牌列表 / 抽牌实验”点击“弃掉选中”，或调用 `CardZonePlayHost.DiscardSelected()`。手牌列表立即更新，禁用牌阻止整批弃牌、失效牌允许弃牌；逻辑牌移入弃牌区，视觉从当前上浮位置按顺序一边翻到牌背一边飞向左侧虚拟弃牌堆，到达直接消失，其余牌自动补位。已隐藏牌保持牌背。弃牌堆位置/场景锚点、时长和逐张间隔在“牌区样式 → 动画”调整；原身份和效果保留，编辑模式支持撤销，Play 退出恢复初始快照。

单牌点数可通过 `CardScoreRules.GetBasePoints(rank/definition)` 或 `ScoringCard.BasePoints` 查询；`PointsPerTrigger` 为基础与额外点数之和，`CanScore` 单独报告失效状态。计分引擎和未来悬停 UI 共用规则，原始点数不因失效归零，实际得分仍由牌型与结算流程决定。接入示例见 [PokerEngine README](Assets/PokerEngine/README.md#单牌点数查询与悬停数据)。

卡牌信息提示使用 `Assets/CardPresentation/Prefabs/CardSuitChipsTooltip.prefab`，搭配用户提供的上下两栏背景与 Fusion Pixel 12px TMP 字体；上方显示原生花色符号、花色文字和点数（如 `♣ 梅花 J`），红桃/方块整行红色，下方显示蓝色“+点数”及“筹码”。场景 `CardHoverPresenter` 持有 Prefab，多个牌区共用一个提示实例，消费 `ICardHoverSource` 已解析的数据；当前验证宿主按卡牌定义查询基础点数。提示跟随卡牌上方并面向场景相机，牌背和飞入/离场不显示。固化手牌自动接入，也可执行 `Tools > Cards > Configure Card Hover Tooltip`；工具不自动保存场景。资源说明见 [卡牌提示 UI](Assets/CardPresentation/UI/README.md)，所有权和数据接入见 [交互 README](Assets/CardInteraction/README.md#场景共用的悬停提示)。

Game 中已接入出牌、弃牌、点数理牌、花色理牌四个按钮，默认缩小为左下角两行。`HandActionPanel` Prefab 消费按钮状态，场景 `HandActionPresenter` 将选中牌提交到既有出牌路径；不显示牌堆余量或“优先”状态行。右下角 `DrawPileIcon` 直接采样当前卡牌图集，以错位叠层展示局内抽牌堆并允许指定顶层牌面，`DrawPilePresenter` 通过牌区绑定读取 Match 的运行时 DeckCards；编辑器只配置启动快照，实际抽牌由玩法逻辑调用 DrawCards，图标悬停显示花色 × 点数剩余统计表，离开图标立即收起，点击打开并保持完整牌组页，返回关闭，右下角数字显示剩余牌数。理牌沿用落位后排序，UI 与验证窗口共用运行列表。当前占位美术使用 Fusion Pixel 字体；`Tools > Cards > Update Player Hand UI` 可升级已有布局，Configure Hand Action Buttons 接入手牌和牌堆图标。按钮与图标使用 Screen Space Camera Canvas，独立 UI Camera 通过精简 Forward Renderer 叠加到主相机 Stack，在场景低分辨率放大后按输出分辨率绘制；悬停提示共用该渲染相机。相机及分辨率说明见 [渲染模块](Assets/Rendering/README.md)，数据所有权见 [交互 README](Assets/CardInteraction/README.md#game-中的手牌操作按钮)。

## 鼠标选牌

独立 `Assets/CardInteraction` 模块提供 `CardZoneMouseInput`、`CardZonePointerController` 和卡牌数据接口。鼠标移入悬停，左键在同牌按下/松开切换选中；状态存于每张本地卡牌，重新投影驱动 View，控制器不另存选中集合。禁用牌可悬停但不能新增选中，失效牌仍可选中；选中表示待提交；已接入弃牌及手牌到出牌区的提交。`CardZonePlayHost.PlaySelected` 向共享 Match 提交命令，校验并一次更新两区列表，`CardZoneTransferPresenter` 接手原视觉实例，支持每批 1–5 张从左到右起飞、反向交叉落位，等本批到齐后整理回真实顺序；验证窗口“出牌路径”页可配置并测试，详见模块 README。

`Tools > Cards > Play Card Zone` 固化当前牌区并进入 Play，鼠标仅在 Game 视图操作；Scene 交互入口已移除。`Bake Card Zone For Play` 只固化，旧 Preview Mouse Interaction 菜单保留为该入口别名。默认沿局部 +Z 上浮 0.02。每次 Play 复制初始数据，运行时选择不覆盖编辑快照。命中使用实际卡牌变换后的 Mesh bounds 射线相交，不启用物理 Collider。运行时由宿主绑定卡牌数据和相机；接入与边界见 [CardInteraction README](Assets/CardInteraction/README.md)。

## 共享 PokerEngine

策划关卡流程编辑器入口为 `Poker > Run Flow Editor`，支持楼层排序、每层独立节点图、分支/汇合、战斗参数与奖励/商店池。配置保存为 `RunFlowAsset` `.asset`，开局编译为不可变 RunDefinition 并绑定成 RunContent；玩家进度独立保存为 Run v4 JSON 快照。已提供三层 + Boss 层资产示例，具体操作和消费代码见 [RunFlow README](Assets/RunFlow/README.md)。

共享引擎位于 `Assets/PokerEngine/Runtime`，已拆成首包内八个独立纯 C# 程序集：Core、Abilities、Scoring、Holdem、Draw、Runs、Progression、Composition。基础层提供卡牌/随机/牌型、能力执行和计分机制；德州与出牌计分分别拥有状态、命令与流程；Run 通过强类型适配器接入两种玩法。

`HoldemMatchEngine` 执行标准无限注德州及能力扩展；`DrawEngine` 执行抽牌、选牌、出牌/弃牌及积分目标。泛型 `RunEngine` 管理小丑构筑、持久牌组与楼层序列，每层有独立的 RunNodeTree；普通/精英/Boss 战斗、奖励、商店共用 RunNodeDefinition，层内支持分支与汇合，层终点完成后进入下一层，最后一层完成才通关。玩家选择合法后继，存档保存当前层和所有层的路径。旧线性/单树配置自动包装为一个楼层。Run 不引用任一具体玩法，示例组装位于 Composition；CreateBranching 提供单层分支示例，CreateFourFloors 提供三层 + Boss 层的德州/Draw 示例，数值待策划确定。

Core 的 `CardZoneOperations` 统一任意有序牌区的插入、移除、移动、交换、重排和跨区转移/抽牌；操作返回不可变结果，由玩法引擎验证并提交。能力、德州和 Draw 已共用此操作层。

`CardZone.SortBySuit(resolveDefinition)` / `SortByRank(resolveDefinition)` 提供花色优先和点数优先排序：花色按黑桃、红桃、梅花、方块，点数按 A、K、Q、J、10…2；同牌面稳定保序。验证窗口“卡牌属性”和“动画实验”页均可触发，经过卡牌定义 → Core Zone 列表 → View 重排动画，保留卡牌属性、选中状态和视觉实例。

验证手牌默认选择点数排序。抽牌先按提交顺序飞入，整批牌完成飞行和惯性回稳后，再按当前“点数排序 / 花色排序”选项更新 Card 层列表并播放重排动画；飞行期间切换选项采用最后一次选择，追加抽牌会延后整理。牌顶/批量/指定牌面/任意抽取以及 Edit/Play 一致；Play 的等待由运行时宿主持有，关闭窗口仍会整理。手动移动、反转和打乱保留到下一次抽牌；诊断中的“手牌之后”在整理时同步更新。

存档版本为 Match v5、Draw v2、Run v4，卡牌 Effects / IsDisabled 与 Run 楼层/节点进度为必填字段。旧 Match/Draw/Run 格式及含旧内层版本的 Run 存档暂不自动迁移。完整玩家视图、联网宿主、玩法场景接入、地图生成/选路界面与 AI 待后续接入。

完整 Run 之外的成长由 `PlayerProgressionState` 保存：内容解锁、局外货币、永久升级等级与已结算 Run 记录；统计从记录汇总。成长存档 schema v1 与活动 Run schema v4 分开，结算按 RunId 去重。规则、数据结构和宿主原子保存/开局接入说明见 [Progression README](Assets/PokerEngine/Runtime/Progression/README.md)。

模块依赖、接入代码和边界见 [PokerEngine README](Assets/PokerEngine/README.md)，项目变更见 [CHANGELOG](CHANGELOG.md)。Unity 测试位于 `Assets/PokerEngine/Tests/Editor`；`bash Tools/test-poker-engine.sh` 按实际依赖分别编译八个模块并执行回归测试。

局内数据归属为 `CardTableState / CardTableSession → CardMatchHost → CardZonePlayHost → CardZoneView`。牌区宿主不再保存独立手牌、牌堆、弃牌堆；初始化/存档恢复可调用 Player 可用的 StartMatch(state, observerCards)，重新绑定牌区不会重置对局。旧场景执行 `Tools > Cards > Migrate Scene To Card Match`，工具验证全局身份并保留现有卡牌，只标记 dirty。迁移、验证窗口和调试改牌 API 明确标记 UNITY_EDITOR，Player 仅保留正式运行功能。详见 [交互模块](Assets/CardInteraction/README.md) 与 [引擎](Assets/PokerEngine/README.md)。

原生 Canvas 牌组总览已提供：四行按花色展示完整成员，固定行宽、随牌数调整间距，共用手牌浅弧算法并保留重叠；已抽出牌半透明显示。左侧采用紧凑牌组信息板，显示各花色数字/JQK/A、分类总数和逐点数余量，配有手绘像素图标。卡牌共享原图集与 UI 材质、Point 采样，UI 按输出分辨率原生绘制，悬停放大和 idle 摆动均属于本页，数据从 Match 读取。`Tools > Cards > Show Canvas Deck Overview` 可在当前场景接入/打开，玩法可调用 Presenter.Open；牌堆图标悬停预览、点击保持，返回关闭，面板与共用提示支持 DOTween 惯性显隐。详见 [Canvas 牌组总览](Assets/CardPresentation/UI/README.md#原生-canvas-牌组总览)。
