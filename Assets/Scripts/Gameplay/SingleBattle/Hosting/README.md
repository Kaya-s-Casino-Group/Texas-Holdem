# 共用战局接入（Hosting）

`BaseBattleHost : MonoBehaviour` 是场景组件基类，`SingleBattleHost` 和 `Battle2DHost` 均直接继承它。它持有普通 C# 的 `PokerEngine.BattleHost`，通过组合连接 Unity 生命周期与不依赖 Unity 的战局生命周期。

## 责任分配

- `BattleHost`（`PokerEngine.Composition`）：持有 `Session / Playback / AuthorityState`；接收创建未开局会话的委托；统一 `StartBattle / RestartBattle / AttachSession / Synchronize`。
- `BaseBattleHost`（`Poker.BattleHosting.Runtime`）：提供场景公共入口 `StartBattle / RestartBattle / AttachSession / SynchronizeBattle`，暴露共用会话与命令端口；不引用配置、Actor、牌区或具体表现。
- 场景派生类：实现 `CreateSession` 收集本场景配置，提供 `BattlePresentation`，在 `ValidateSession` 校验场景可接入的阵容，在 `OnSessionAttached` 刷新输入绑定。`CanStartBattle` 与 `AnimateOpening` 保留各自交互门禁和开局表现策略。
- `BattleSessionFactory`：将配置与阵容 Setup 转成纯规则快照，返回未开局会话。
- `BattleSessionPlayback`：统一提交命令和派发表现；`BattleSession`：原子推进规则到下一决策点或终局。

## 生命周期约定

创建 / 重开都会重新调用工厂。工厂失败或返回已开局会话时保留当前会话；接入已有会话不会调用工厂或自动执行开局。

绑定顺序为：安装新 `Session`、暂时清空 `Playback`、调用表现 `Initialize`、安装新命令端口、发出 `SessionAttached`。创建新局再执行一次开局命令。这样初始化期间不会读到上一局的命令状态，按钮绑定后可立即读取新局。

复用表现端时由 `Initialize` 清理旧显示；更换表现端时先同步旧表现。初始化失败保留新会话但不暴露命令端口，修复绑定后通过 `AttachSession` 恢复。生命周期回调中禁止嵌套创建、接入或同步，避免覆盖正在安装的会话。

同步只取消显示播放并投影当前权威状态；不会重放规则、洗牌或推进回合。3D 的 `SnapToState` 是此入口的兼容转发。动画内部的逐步投影仍由各表现实现负责。

本程序集只直接引用 `PokerEngine.Core / Composition` 和 Unity；不同风格实现互不引用。纯 C# 调用方可直接使用 `BattleHost`，无需创建场景组件。
