# Draw 战局与现有卡牌 UI

`Poker.DrawBattle.Runtime` 是 Unity 场景桥接层。依赖既有 Draw/Core/Abilities/Scoring、CardInteraction 和 CardPresentation，GameCore 仍是纯 C#。一场战局只有 `DrawState` 持有权威玩法状态；`CardMatchHost` 在接入时成为本地展示镜像，允许动画帧临时保留出牌，不参与玩法结算和存档。

## 当前场景接入

在包含一个已配置手牌操作面板的场景执行 **Tools > Cards > Connect Draw Battle In Current Scene**。工具在对应 Card Match 上添加/配置 `DrawBattleHost`，复用手牌、出牌区、出牌路径、按钮、抽牌堆图标和完整牌库总览；支持 Undo，不自动保存场景。当前 CardRenderingValidation 已配置，进入 Play 即可开始一场 Draw。关闭宿主的 Start On Enable 可由正式 Run 宿主显式初始化。

默认参数在宿主 Inspector：8 张手牌、4 次出牌、3 次弃牌、目标 200，属于可玩验证参数。

## Run 牌组与单次战局牌组

- 新 Run 的基础持有牌组是标准 52 张牌：四种花色各 A–K 共 13 张，不含大小王。RunState.Deck 持有持久卡牌 ID、牌面和强化，随后可因奖励/商店发生复制、移除、转换和强化；52 是初始数量。
- 每场战局从当前 Run.Deck 创建独立 DrawCard/CardInstance。DrawRunAdapter.Create 已按这个规则组装，OriginId 指回对应 RunCard.Id；下一场继承改变后的数量、牌面和强化，不重置为 52 张。战局的手牌、抽牌顺序、出牌区和弃牌区按新战局初始化。
- 例如：开局 52 张 → 复制一张后 53 张 → 下一战手牌 8、抽牌堆 45；之后移除两张变为 51 张 → 再下一战手牌 8、抽牌堆 43。
- DrawState.Cards 表示本战局牌组，World.Zones 保存当前位置。常规出/弃/补牌只移动原身份，所有牌区（含移除区）总量等于本战局牌组数量；弃牌并不等于从 Run.Deck 永久删牌。永久构筑变更必须通过 Run 层明确提交。
- StartBattle 仅供独立战局验证，固定创建标准 52 张和新的随机种子，场景预览只提供美术映射。已绑定外部命令提交委托时拒绝调用此入口，避免 Run 牌组被意外重置。正式 Run 进入下一场或读档使用 StartEncounter 绑定该 Run 已创建/恢复的状态。

Run 中的 Joker 构筑由 RunState.Jokers 单独持有，不混入这套扑克牌。完整 Run 场景宿主仍待接入；以上牌组传递已由纯 C# Run/DrawRunAdapter 支持。

## 数据及按钮流

- 出牌/弃牌按钮与 `PlaySelected` / `DiscardSelected` 提交真实 DrawCommand。引擎检查身份、Revision、阶段、每批 1–5 张、禁用、出牌/弃牌次数。动画、待能力选择、胜负后禁用行动。
- 出牌先复用原卡牌实例飞入 PlayedZoneView，展示 Scored 事件，停留默认 0.6 秒，再投影引擎最终弃牌区。若仍是 Scored，提交一次 Continue 后补齐手牌。Won/Lost 不再补牌。
- 弃牌沿用既有飞往虚拟弃牌堆的动画，待离场完成后展示引擎已经补齐的手牌。Draw 不将弃牌自动洗回抽牌堆。
- 抽牌堆计数、悬停统计、点击完整牌库、单牌花色/点数提示继续复用现有 Presenter。弃牌堆是完整逻辑牌区及手牌已有离场锚点，目前没有独立弃牌浏览面板。
- 点数/花色排序只改本地镜像与显示顺序，不改变 DrawState/随机/Revision。补牌沿用当前理牌方式，等入场落位后整理。
- 镜像的 PrepareDraw/Play/Discard/Reorder、RestartMatch 和编辑器 DebugEdit 在外部权威模式拒绝；不能绕过 Draw 的次数和阶段。镜像 Revision 表示本地投影更新次数，不等于权威 Draw Revision，不可用作玩法 ExpectedRevision。

## 正式宿主入口与恢复

```csharp
// rules 可由 DrawRunAdapter.Engine(context) 创建；state 是当前 Run.Encounter。
// 默认不传 submit 时直接执行 DrawEngine，适合独立战局。
battle.StartEncounter(rules, state, card => artwork.Resolve(card.DefinitionId));

// Run 拥有战局时必须经 RunEngine 提交，不能执行独立 DrawEngine 再修改 Run。
// Run 宿主负责正确路由 Continue 系统命令，并从 Run 结果提取本次 Draw 事件。
battle.StartEncounter(rules, run.Encounter, resolveArt, command => {
    var result = SubmitThroughRunEngine(command);
    return new DrawBattleTransition(result.DrawState, result.DrawEvents, result.DrawError);
});
```

上面的 `SubmitThroughRunEngine` 是宿主适配伪代码；本模块提供委托入口，没有实现全 Run 场景管理、奖励/商店/选路 UI 或存档 I/O。换战局/读档使用 `StartEncounter`，停止旧动画、重新绑定新 ZoneId、更新显式 roster。继续使用既有 `DrawSnapshotCodec` / `RunSnapshotCodec` 保存权威状态，不保存 CardMatchHost 镜像或动画帧。

`State` 随成功提交更新；`StateChanged` 在显示投影完成时发布，后续得分/次数/胜负 UI 可读取它；`EventPresented` 在视觉到达后发布本次 Draw 事件，Scored 使用事件的 ScoreResult。`Submit` 提供显式 ActivateAbility/SelectAbilityTargets/Continue 入口；能力选择 UI 尚未配置，不自动跳过或超时选择。`SnapToState` 中断动画并投影最新权威状态，不重新执行原命令。

## 验证入口

- **Poker > Tests > Run Draw Battle In Current Scene**：同步 NUnit 回归，使用独立 PreviewScene，不替换当前场景。
- Play 中 **测试面板 > 实战验证 > 分数目标战局**：调用真实按钮和指针事件，等待真实飞行/补牌动画，检查重复提交锁定、计分、次数、四区、牌堆数字、悬停统计、点击牌库与排序。完成恢复原战局；报告 `Library/DrawBattleTests/play.json`。不模拟系统鼠标。
- 既有 **Run Card Presentation In Current Scene** 保留原验证/预览模式回归。
