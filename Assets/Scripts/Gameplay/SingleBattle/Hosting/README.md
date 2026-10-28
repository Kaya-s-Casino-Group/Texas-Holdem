# 共用战局接入（Hosting）

`BattleSceneHost : BaseBattleHost` 是通用的每局入口，通过 `IBattleSessionSource` 与 `IBattleSceneAdapter` 组合来源和场景适配；每个 Battle 一个实例。Palamedes 保留最小的 `XmlBattleSessionSource + BattleTimelineSceneAdapter + ScenePresentationDirector` 组合。旧 Scene3D、Minimal2D 场景和专用适配器已清理。

最小预制体与一键工具见 [入口配置教程](../../../../../Tools/BattleSceneHostSetup.md)。

## 多文件 XML 单局入口

`XmlBattleSessionSource` 同时接受原单文件和包含 `<Import path="..." />` 的入口。开发特定战斗表现时，在 `Content Xml` 绑定包内 `Content/SingleChapter/Battle1.xml`、`Battle2.xml`、`Gatekeeper.xml` 或 `Wrath.xml`，设置对应 Encounter ID，再点击“收集并校验 XML 依赖”。例如暴怒使用 `Wrath.xml` + `single-chapter-wrath`，可直接开局，省去前置冒险关卡。

编辑器会递归收集入口目录内的依赖并保存为 `Modules` 的相对路径与 TextAsset 引用；失败时保留已有绑定，支持 Undo。修改 Import 关系后需再次收集；修改已绑定文件内容无需重新收集。运行时按这些序列化引用装配，不使用 AssetDatabase 或磁盘读取。程序化装配可调用 `Configure(entry, encounterId, BattleXmlModule[])`；原两参数接口仍可绑定单文件，并清空旧依赖。

入口、模块缺失或引用错误会阻止创建新会话，诊断保留原 XML 文件和行号。`Build` 返回未 Start 会话，随后由 Host 控制开局。单局使用基础构筑；如需验证敌人中途阶段，应通过会话的测试/管理接口设置状态，或附加已准备好的会话。此装配层不自动推进敌人阶段。

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

## 场景站位 Layout

`SingleBattle` 已配置独立根节点 `BattleLayout`，通过 `Battle` 上 `BattleTimelineSceneAdapter.Scene Layout` 显式引用。Layout 与 BattleCamera、CameraPresent、动作节点、角色实例无父子依赖。

```text
BattleLayout (BattleSceneLayout)
├─ Players/P1, P2
└─ Enemies/E1, E2, E3, E4, E5
```

`BattleSceneLayout` 是场景数据：只声明 2 个玩家和 5 个独立敌人槽的稳定 ID、位置与朝向，不订阅对局、不生成角色、不更新 Transform。Scene 视图显示蓝色玩家锚点、橙色敌人锚点及朝向箭头；直接移动对应锚点即可编辑。E1–E3 初次配置时捕获当前三个螃蟹的世界机位，P1 捕获现有玩家机位，其他为可编辑预留位置。锚点不是模型容器，也不要把它作为时间线的动画目标。

菜单 `工具/卡牌战斗/Scene Layout/配置当前场景站位` 创建缺失配置并绑定桥接，支持 Undo；重复执行保留已有锚点和位姿。`Battle` 的桥接 Inspector 中点击“校验并预览当前对局站位”会创建未开局会话并校验映射，不安装到 Host、不运行规则开局。Play 时该 Inspector 显示实际绑定；当前 Battle1 编成为 `player → P1`、`enemy → E1`。场景中的额外模型仍是美术摆放，不代表规则中额外存在的敌人。

桥接提供 `LayoutBindings.TryGet(unitId, out placement)`，返回复制的世界基础位姿 `RootPosition / RootRotation`、`SlotId` 和个体／部位身份；不会向动作暴露可写的锚点 Transform。后续 Actor Present 从这些数据初始化基础站位，角色自身仍独立组织。初始化／重开重建映射，同局同步保留已有存活个体的站位并重读场景位姿；运行中编辑锚点后需显式同步，不会每帧跟随。

默认按显示单位编成顺序分配该阵营首个空槽，再按 UnitId 保持已有绑定。桥接的 `Layout Overrides` 可明确指定 `Unit Id + Slot Id`。死亡不会让幸存者补位，死亡个体保留末次槽位信息但不再占用活槽，新个体可复用；复活优先原槽，原槽已被占用则分配空槽，不能挤走存活个体。显式槽位冲突、重复 ID、跨阵营槽位、缺失锚点及超容量均明确报错，不截断单位列表。

部位在桥接配置 `Unit Id + Owner Unit Id + Part Key`，不另占独立槽，也不从召唤关系或单位名称猜测归属。例如暴怒本体 `enemy` 占 E1，`hand-1` 至 `hand-5` 显式指向 `enemy`，保留各自规则身份。部位取得的是本体基础位姿，**不是部位的实际受击坐标**；Part Key 留给角色资源中的部位锚点解析，当前尚未接入模型／部位资源装配。部位的本体必须存在、同阵营且为独立个体；禁止循环／多级部位归属和同一本体重复 Part Key。本体死亡但仍有存活部位时继续占用该槽。当前 Battle1 不配置部位覆盖。

Layout 映射随 DisplayStore 检查点更新，不提前按权威最终状态暴露新单位。播放前会对整批事实预检容量和绑定，失败不会先发布部分站位。纯卡牌前端可不绑定 Layout。此处只实现场景容量契约，没有更改引擎人数或召唤规则；采用该场景的编成与召唤内容需要满足 2／5 个独立个体容量，多部位要显式配置归属。

2026-10-29 验证：桥接回归 **25/25 通过**（含 16 项 Layout 测试）；当前场景完整开局、发牌、同步、重开、锚点无动画写入以及退出 Play Mode 检查通过。场景验证记录位于 `Library/BattleLayoutValidation/scene.xml`。

## 场景相机绑定

与 Layout 相同，`BattleCamera / CameraSceneConfiguration` 是场景数据，保存原 Camera 输出、Overview／Opening／Closeup 锚点、默认焦点、开场时长／曲线／放行比例及其他 Cue 参数。它不持有 Director、不订阅对局，也不执行动作。`Battle/CameraPresent` 保留执行器和动作节点，机位与参数不再序列化在执行组件上。

桥接的 `Camera Scene` 和 `Camera Present` 是显式配对引用；`ValidateSession` 在接入前检查同场景、执行器归属、Cue Key 和开场配置，`BeforeInitialize` 调用 `BindCameraPresentation` 后再发布初始化快照。重开／重新接入都会读取当前场景配置；普通同步复位到 Overview，取消播放但不重放运镜。没有相机功能的前端可同时不绑定两个引用；只绑定其中一个会报错。

Scene 配置不需要和执行器共用父节点。编辑器预览通过同一绑定方法工作，绑定本身不移动相机。当前开局 2.5 秒、OutSine、90% 发牌放行以及原 BattleStarted → NearlyArrived checkpoint 均保留。调整和预览方式见 [Camera Present](../../../Presentation/BattlePresentation/README.md#camera-present)。
