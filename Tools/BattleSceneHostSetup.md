# 单个 Battle 的入口与最小预制体

> **阅读范围（2026-10-28）**：通用 Host 与最小入口说明仍供参考；“现有 3D 关卡如何迁移”和“验证”记录旧版集成。现行表现接入统一见 [CardPresentation README](../Assets/Scripts/Presentation/CardPresentation/README.md)，会话职责以 [Hosting README](../Assets/Scripts/Gameplay/SingleBattle/Hosting/README.md) 为准。

一个独立 Battle 对应一个 `BattleSceneHost` 实例。同一个 Unity Scene 可以容纳多个 Battle，各自持有会话、观察者、显示状态、预选和时间线。场景变化不要求新增 Host 子类。

## 当前结构

```text
BattleSceneHost : BaseBattleHost
├─ BattleHost → BattleSession / BattleSessionPlayback（运行时创建）
├─ Session Source : IBattleSessionSource
└─ Scene Adapter : IBattleSceneAdapter
   └─ 规则投影 → 中性数据 → 表现前端 / 时间线
```

Host 只管理创建、接入、开局、重开和同步，不引用牌区、Actor、Canvas、相机或具体表现模块。

- `IBattleSessionSource.CreateSession()` 返回未开局的会话，不提前执行 Start。
- `IBattleSceneAdapter.BindHost()` 绑定唯一宿主；`ValidateSession()` 检查适配能力；`SessionAttached()` 更新输入。表现初始化、播放和同步沿用 `IBattleSessionPresentation`。
- 服务组件必须位于所属 Host 自身或子层级，不能借用另一 Battle 的来源或适配器。
- 初始化先安装 Session，再初始化表现，安装命令端口，通知适配器，最后提交开局。`AttachSession` 不自动开局，`SynchronizeBattle` 不执行规则。

## 最小启动预制体

资源：`Assets/GameAssets/SingleBattle/Hosting/BattleSceneHostMinimal.prefab`

仅有一个根对象和四个组件，无子物体：

| 组件 | 职责 |
| --- | --- |
| `BattleSceneHost` | 生命周期与自动开局开关 |
| `XmlBattleSessionSource` | XML 内容包与 encounterId，创建新会话 |
| `BattleTimelineSceneAdapter` | Bridge、观察者、预选和命令入口；不依赖卡牌／角色对象 |
| `ScenePresentationDirector` | 中性表现调度和 DisplayStore |

预制体已绑定包中的 `SingleBattleDemoNew.xml`、`single-battle-new`，默认 Auto Start 开启。空场景也能启动到玩家决策点。

这是一份最小运行入口，不会生成可视化牌桌、角色或 HUD。空 Timeline 立即发布规则快照；可以逐步向 Director 添加任意动作和对象绑定。现有完整关卡继续使用自己的场景适配器。

## 一键加入当前场景

菜单：`工具 > 卡牌战斗 > Battle入口 > 在当前场景添加 BattleSceneHost`。

也可使用：`GameObject > 战斗 > BattleSceneHost（最小入口）`。

- 空场景：实例化最小预制体，不删除或调整已有相机、灯光与物体。
- 已有一个 Host：定位该入口，重复执行不再创建、不覆盖配置。
- 选中了某个 Host 的层级：定位该 Battle。
- 有多个 Battle 但未选中：明确提示选择，不随意接管某一局。
- 确实需要再开一局：使用 `新增独立 Battle（允许同场景多局）`。
- 旧场景接入：使用 `迁移当前场景已有 Battle 入口`，为实现服务接口的既有适配器补齐 Host 绑定。

添加支持 Unity Undo，只标记当前场景待保存；不会自动覆盖场景文件。`定位或生成最小入口预制体` 在资产存在时只定位，不覆盖已经编辑的预制体。

选中入口，在 Inspector 配置 Session Source、Scene Adapter、Auto Start、Animate Opening。编辑模式有“校验会话与适配器（不启动）”；运行时提供开始／重开／同步及最小适配器的结束回合验证按钮。

## 接入自己的表现

1. 使用 XML source 或实现自己的 `IBattleSessionSource`。
2. 使用 `BattleTimelineSceneAdapter` 消费规则投影，并向它的 Director 配置时间线。默认 observer 为空表示第一名玩家，也可指定 XML 中的玩家 ID。
3. 视图读取适配器的 `Display`、`Candidate` 或 `Director.Display.Changed`，不读取已经推进到结算末尾的权威状态。
4. 输入调用 `SelectCards(cardIds)`、`PlaySelected()`、`DiscardSelected()`、`EndTurn()`。候选查询、提交冻结和关闭作用域均在适配器处理。
5. 若需要特殊牌区投影或角色绑定，实现另一个 `IBattleSceneAdapter`，无需改变通用 Host。

每个 Battle 使用自己的 Director 和动作实例；不要让两局共用可变的输入、显示对象或动作资源。纯 XML TextAsset 可以复用。

## 现有 3D 关卡如何迁移

原 `SingleBattleHost` 已更名为 `SingleBattleSceneAdapter`，脚本 `.meta` GUID 保留，现有 Actor、HUD 和场景序列化引用继续指向同一适配器组件。

`SingleBattleDemoNew` 的 `Battle/Card Match` 现在同时持有：

- `BattleSceneHost`：唯一战局生命周期所有者。
- `SingleBattleSceneAdapter`：现有牌区、角色、盾牌、输入、HUD 的绑定与投影。
- 原有 `ScenePresentationDirector` 和候选反馈组件：保留时间线及美术配置。

这个具体适配器同时实现了会话来源接口，负责 XML 编译组装与阵容校验；旧 SO 来源已删除。Host 的 Source 和 Adapter 均指向它；它自己不再继承 BaseBattleHost，也不再持有独立 BattleHost。其 `StartBattle / RestartBattle / AttachSession / SynchronizeBattle` 暂保留为兼容转发，既有按钮无需重新绑线。新代码直接调用 `adapter.SceneHost`。

Minimal2D 已迁到相同入口：`BattleSceneHost + XmlBattleSessionSource + Battle2DSceneAdapter + ScenePresentationDirector`。旧 `Battle2DHost` 和 Demo SO 规则绑定已清理，卡牌与矩形动作由场景时间线配置。最小预制体仍独立于具体 2D/3D 模块。

## 验证

使用 `工具 > 卡牌战斗 > 表现时间线 > 验证 EditMode / 验证 PlayMode`。

新增检查覆盖：最小预制体无场景对象启动、双 Battle 会话与选择隔离、接入不重复开局、同步不执行规则、配置失败保留已有会话、跨 Battle 绑定拒绝、停用门禁、一键工具幂等与显式新增；真实场景回归验证出牌、弃牌、取消恢复、重开和重新启用。
