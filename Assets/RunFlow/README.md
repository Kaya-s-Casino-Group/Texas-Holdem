# 关卡流程（RunFlow）

本模块负责外层地图和楼层配置，不实现单场战斗。项目主线玩法为 [卡牌战斗（SingleBattle）](../SingleBattle/README.md)，[分数挑战（DrawBattle）](../DrawBattle/README.md) 单独保留。下文的纯 C# Run 接入示例使用 `DrawRunAdapter`，对应分数挑战；不能据此认为主线卡牌战斗场景已经接入完整 Run。Unity 运行时地图、选路与战斗节点串联仍需后续接入。

菜单统一位于 **工具 > 关卡流程**，提供“流程编辑器”和“创建四层示例”；窗口名为“关卡流程编辑器”。类名与资产格式保持不变。


策划在 Unity 编辑节点地图，保存为 `RunFlowAsset`（ScriptableObject `.asset`）。每份资产描述一次完整 Run：有序楼层列表，每层独立入口、节点和连接，整局构筑/金币/默认内容池。节点位置与显示名称也保存，便于继续编辑。

## 编辑

菜单 `工具 > 关卡流程 > 流程编辑器` 打开窗口；双击流程资产也可打开。已有示例为 `Configs/RunFlow_ThreeFloorsAndBoss.asset`，包含三棵普通分支树和一棵独立 Boss 树。窗口“三层 + Boss 示例”会创建新资产，不覆盖原资产。

- 左侧添加、删除、调整楼层顺序；“整局规则”配置开局金币、小丑、奖励池和商店池。
- 中间通过“+ 节点”添加普通战斗、精英、Boss、奖励、商店。拖动节点改变排布；右侧圆点连接到目标左侧圆点或节点框，支持分支及汇合。
- 右侧编辑楼层入口与类型、节点名称/类型，以及战斗目标、回合上限、通关金币、能量。奖励与商店可指定独立内容池；为空时使用整局默认池。内容池填的是 `RunOfferDefinition.Id`，开局小丑填 `JokerDefinition.Id`。
- 后继列表中移除连接；删除节点会清理指向它的连接。禁止重复连线、自连接和循环。中键平移、滚轮缩放，“自动排列”整理节点，“适配”显示整张图，Ctrl/Cmd+Z 撤销。
- “校验”检查图和数值；“保存”只保存当前流程资产。允许保存未完成草稿，但草稿无法初始化 Run。Play 期间也可以编辑资产，新配置在下一次编译初始化时生效。

层数范围 1–32，每层 1–500 个节点，最多 100 个战斗。楼层顺序决定推进顺序，不连接跨层节点。玩家只走所选路线，走到该层终点后进入下一层，最后一层完成后通关。Boss 层与 Boss 节点是标记，具体战斗机制仍由玩法适配器实现。

## 开局消费

```text
RunFlowAsset.asset
    → BuildDefinition(scoring) → 不可变 RunDefinition（有序 Floors / NodeTree）
    → BuildContent(...)       → RunContent（绑定牌、能力、小丑、商品及起始牌组）
    → RunEngine.Create(...)   → 本次 RunState
    → RunSnapshotCodec        → 本次进度 JSON
```

Unity 宿主通过序列化字段引用资产，或通过 Resources/Addressables 加载；只把配置放在 Configs 目录不会自动打进 Player。开局调用 `BuildContent`，它内部先构造并校验 `RunDefinition`，再校验内容池 ID。分数规则、具体内容目录与起始牌组由宿主传入，地图编辑器不复制这些目录。

以下是用现有示例内容绑定编辑器地图的完整初始化片段（`flow` 为宿主持有的 `RunFlowAsset`）：

```csharp
using System.Linq;
using Poker.RunFlow;
using PokerEngine;

var source = ExampleRunContent.Create(drawMode: true);
var offers = source.Definition.ShopPool.Select(source.Offer).ToArray();
var jokers = offers.Where(o => o.Kind == RunOfferKind.Joker)
    .Select(o => o.JokerId).Distinct().Select(source.Joker);
var content = flow.BuildContent(source.CardsAndAbilities, source.Definition.Scoring,
    jokers, offers, source.StartingDeckVersion, source.StartingDeck);
var adapter = new DrawRunAdapter(ExampleRunContent.Runtime(drawMode: true));
var engine = new RunEngine<DrawState, DrawCommand, DrawEvent>(content, adapter);
var state = engine.Create("run-001", new ParticipantId("player"),
    HmacDeckShuffler.FromSeed(seed)); // seed 由权威宿主提供
var started = engine.ExecuteSystem(state,
    new RunCommand<DrawCommand>("start", state.Revision, RunAction.StartRun));
state = started.State;
```

转换后所有规则数据复制为不可变定义；编辑资产不会影响已创建的 Run。规则改动应更新“整局规则版本”及相关“节点树版本”，读档须使用匹配的内容配置。节点名称和画布位置不参与树指纹；拓扑和战斗参数参与。当前编辑器校验使用默认分数配置，宿主绑定时使用实际分数配置再次校验。

## 保存格式与程序集

资产格式版本为 1，Unity 序列化 `.asset`（当前项目为文本 YAML），与进度快照版本分开。Run 进度继续使用 `RunSnapshotCodec` schema v4，包含当前层、所有层路径、构筑、随机流、积分和当前对局。资产是流程模板，不写入玩家进度；进度恢复需要同版本 `RunContent`。

`Poker.RunFlow.Runtime` 引用 UnityEngine，负责资产到纯 C# 定义的桥接，并有 link.xml；GameCore 的十个程序集继续无 Unity 依赖。`Poker.RunFlow.Editor` 和测试程序集仅进入 Editor。本轮提供固定地图编辑；随机地图生成、玩家地图/选路界面及场景 Run 宿主接入另行实现。跨 Run 成长由独立 PlayerProgressionState 持有，见 [Progression README](../PokerEngine/Runtime/Progression/README.md)。

测试入口 `工具 > 测试面板 > 编辑模式测试 > 更多模块 / 全量回归 > 关卡流程编辑器`：8 项同步测试，覆盖资产保存重导入、不可变转换、四层实际 Draw Run 与每步快照恢复、撤销/重做、图及内容 ID 校验、楼层排序、排布和 Player 程序集边界；结果位于 `Library/RunFlowEditorTests`。不保存或替换当前场景。
