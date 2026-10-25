# 共用战局接入（Hosting）

`BattleSceneHost : BaseBattleHost` 是通用的每局入口，通过 `IBattleSessionSource` 与 `IBattleSceneAdapter` 组合来源和场景适配；每个 Battle 一个实例。Palamedes 保留最小的 `XmlBattleSessionSource + BattleTimelineSceneAdapter + ScenePresentationDirector` 组合。旧 Scene3D、Minimal2D 场景和专用适配器已清理。

最小预制体与一键工具见 [入口配置教程](../../../../../Tools/BattleSceneHostSetup.md)。

## 责任分配

- `BattleHost`（`PokerEngine.Composition`）：持有 `Session / Playback / AuthorityState`；接收创建未开局会话的委托；统一 `StartBattle / RestartBattle / AttachSession / Synchronize`。
- `BaseBattleHost`（`Poker.BattleHosting.Runtime`）：提供场景公共入口 `StartBattle / RestartBattle / AttachSession / SynchronizeBattle`，暴露共用会话与命令端口；不引用配置、Actor、牌区或具体表现。
- `BattleSceneHost`：通过组件组合选择 `IBattleSessionSource` 与 `IBattleSceneAdapter`，统一开局门禁、自动开局及启停同步。不同关卡复用此类，每个 Battle 单独实例化。
- `IBattleSessionSource`：创建未开局会话；`IBattleSceneAdapter`：校验会话、绑定输入并实现表现初始化、播放和同步。具体场景适配器不持有另一份生命周期。
- `XmlBattleSessionSource`：使用 `BattleContentCompiler / BattleContentFactory` 校验 XML 并返回未开局会话。旧 SO 配置工厂已删除。
- `BattleSessionPlayback`：统一提交命令和派发表现；`BattleSession`：原子推进规则到下一决策点或终局。

## 生命周期约定

创建 / 重开都会重新调用工厂。工厂失败或返回已开局会话时保留当前会话；接入已有会话不会调用工厂或自动执行开局。

绑定顺序为：安装新 `Session`、暂时清空 `Playback`、调用表现 `Initialize`、安装新命令端口、发出 `SessionAttached`。创建新局再执行一次开局命令。这样初始化期间不会读到上一局的命令状态，按钮绑定后可立即读取新局。

复用表现端时由 `Initialize` 清理旧显示；更换表现端时先同步旧表现。初始化失败保留新会话但不暴露命令端口，修复绑定后通过 `AttachSession` 恢复。生命周期回调中禁止嵌套创建、接入或同步，避免覆盖正在安装的会话。

同步只取消显示播放并投影当前权威状态；不会重放规则、洗牌或推进回合。动画内部的逐步投影仍由各表现实现负责。

`BattleTimelineSceneAdapter.SubmitAction` 将动作、卡牌、目标、目标槽绑定和摸牌数量原样交给 `BattleSessionPlayback`；合法性和结算由规则层决定。当前示例 XML 使用手动摸牌，开局后等待 Draw 命令，不由 Unity 自动发牌。`PlaySelected / DiscardSelected / EndTurn` 是便捷转发入口。新前端仍需按所采用规则提供自己的交互和显示绑定。

本程序集只直接引用规则程序集和 Unity；不同风格实现互不引用。纯 C# 调用方可直接使用 `BattleHost`，无需创建场景组件。
