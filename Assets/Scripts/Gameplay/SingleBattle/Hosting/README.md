# 共用战局接入（Hosting）

`BattleSceneHost : BaseBattleHost` 是通用的每局入口，通过 `IBattleSessionSource` 与 `IBattleSceneAdapter` 组合来源和场景适配；每个 Battle 一个实例。原 `SingleBattleHost` 已更名为 `SingleBattleSceneAdapter`，只负责当前卡牌场景的输入和显示投影。旧 `Battle2DHost` 作为独立验证实现继续继承基类。

最小预制体与一键工具见 [入口配置教程](../../../../../Tools/BattleSceneHostSetup.md)。

## 责任分配

- `BattleHost`（`PokerEngine.Composition`）：持有 `Session / Playback / AuthorityState`；接收创建未开局会话的委托；统一 `StartBattle / RestartBattle / AttachSession / Synchronize`。
- `BaseBattleHost`（`Poker.BattleHosting.Runtime`）：提供场景公共入口 `StartBattle / RestartBattle / AttachSession / SynchronizeBattle`，暴露共用会话与命令端口；不引用配置、Actor、牌区或具体表现。
- `BattleSceneHost`：通过组件组合选择 `IBattleSessionSource` 与 `IBattleSceneAdapter`，统一开局门禁、自动开局及启停同步。不同关卡复用此类，每个 Battle 单独实例化。
- `IBattleSessionSource`：创建未开局会话；`IBattleSceneAdapter`：校验阵容、绑定输入并实现表现初始化、播放和同步。具体场景适配器不持有另一份生命周期。旧 Minimal2D 的派生宿主继续保留作验证。
- `BattleSessionFactory`：将配置与阵容 Setup 转成纯规则快照，返回未开局会话。
- `BattleSessionPlayback`：统一提交命令和派发表现；`BattleSession`：原子推进规则到下一决策点或终局。

## 生命周期约定

创建 / 重开都会重新调用工厂。工厂失败或返回已开局会话时保留当前会话；接入已有会话不会调用工厂或自动执行开局。

绑定顺序为：安装新 `Session`、暂时清空 `Playback`、调用表现 `Initialize`、安装新命令端口、发出 `SessionAttached`。创建新局再执行一次开局命令。这样初始化期间不会读到上一局的命令状态，按钮绑定后可立即读取新局。

复用表现端时由 `Initialize` 清理旧显示；更换表现端时先同步旧表现。初始化失败保留新会话但不暴露命令端口，修复绑定后通过 `AttachSession` 恢复。生命周期回调中禁止嵌套创建、接入或同步，避免覆盖正在安装的会话。

同步只取消显示播放并投影当前权威状态；不会重放规则、洗牌或推进回合。3D 的 `SnapToState` 是此入口的兼容转发。动画内部的逐步投影仍由各表现实现负责。

本程序集只直接引用规则程序集和 Unity；不同风格实现互不引用。纯 C# 调用方可直接使用 `BattleHost`，无需创建场景组件。
