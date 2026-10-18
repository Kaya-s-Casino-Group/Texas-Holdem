# 战斗共享配置

此模块把 Unity 中的作者配置构建成不可变的 PokerEngine 规则快照，供 Scene3D 与 Minimal2D 共用。它不持有战局当前状态，不绑定场景对象，不播放动画。

这里的 SO 配置为兼容入口。`SingleBattleDemoNew` 已改用 `External/BattleToolkit/Engine/Content/SingleBattleDemoNew.xml`；3D Host 直接调用纯规则 XML 编译器和内容工厂，旧配置资产继续供其他场景使用。XML 通过外部 HTML 工具配置；详见 [场景接入说明](../Presentation/Scene3D/README.md)。

- `Runtime`：共用 `BattleSessionFactory` 与 17 个 Profile。公共规则与牌型配置、玩家和敌人初始数据、攻击目标与友方策略、得分伤害、弃牌收益、攻击和承伤修正。
- `Editor`：出牌伤害和无弃牌收益的 CustomEditor，以及对应默认配置菜单。依赖当前 3D Host 的数值窗口与审计仍在 Scene3D/Editor。
- `Profiles`：已有 Demo 与 SingleBattle 配置资产，移动后保留原 GUID 和数值。

`Poker.BattleConfiguration.Runtime` 仅引用 PokerEngine 的 Core、Scoring、Combat、Composition，以及 `Poker.Scoring.Runtime`。`.Editor` 仅在 Editor 编译。两种前端必须直接引用配置程序集，不能通过另一种前端间接获取 Profile。

配置类沿用 `Poker.SingleBattle` 命名空间；跨程序集迁移补充原 `Poker.SingleBattle.Runtime` 的 `MovedFrom` 信息，并保留脚本 GUID。这个兼容名字不表示配置依赖 3D 实现。

扩展规则时在 PokerEngine 实现纯规则，在本模块添加构建该规则的 Profile；具体风格的材质、布局、音效与动画配置应留在各自表现目录。局内当前 HP、格挡、能量、手牌和随机状态只由 BattleSession 持有。

## 共用战局组装

`BattleSessionFactory.Create(rules, players, enemies, battleId, random)` 统一构建公共规则与评分快照、CardDuelEngine、初始 CardDuelState 和 BattleSession。players/enemies 是显式 Setup，保留身份、顺序、独立牌堆、初始生命与角色策略；缺省战局 ID 和随机状态也在这里生成。

工厂返回尚未发开局手牌的会话，不绑定表现，不执行 Start。两种 Host 先接入自己的表现，再调用共用 `BattleSessionPlayback.Start`，因此动画选择不影响组装规则。配置层测试位于 `Tests/Editor`，使用独立且仅供 Editor 的测试程序集。
