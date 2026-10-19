# Battle2DValidation：XML 与公共表现时间线迁移

后续状态：本记录中的 5 项既有失败已完成排查修复，见[完整回归修复记录](2026-10-19-full-regression-fixes.md)。下方保留该阶段的原始结果。

日期：2026-10-19（Asia/Shanghai）；Unity 2022.3.62f3。

## 交付

场景 `Assets/Scenes/Battle2DValidation.unity` 已原位接入：

- `BattleSceneHost` 管理唯一生命周期；`XmlBattleSessionSource` 使用规则包中的 `SingleBattleDemoNew.xml / single-battle-new`。
- `Battle2DSceneAdapter` 通过共享 `BattlePresentationBridge` 投影规则结果、查询候选并提交输入，不创建另一份会话。
- `Battle2DView` 只接收中性数据，转接同一 `ScenePresentationDirector`，按其 DisplayStore 更新卡牌、HUD 和日志。
- `Battle2DCardTimelineAction` 和公共 `TransformTimelineAction` 均派生基础表现动作。顺序、marker、checkpoint、筛选、目标与动作参数保存在场景配置。

出牌默认 `cards.ReadyToAttack → attack → Impact`；弃牌等待准备动作完成。敌人矩形在 Impact 后继续收招，动作完成才进入下一事实，支持多个敌人共用最简矩形依次行动。未配置动作的其余规则节点继续通过显示检查点公开快照。

移除了旧 `Battle2DHost`、2D 的 SO 规则字段与程序集依赖、View 的 BuildPlan/内置动画协程、私有 Director，以及角色初始位置捕获和强制归位逻辑。原 Host 的脚本 GUID 迁给适配器，保留场景引用。卡牌美术和渲染器保留。此迁移阶段暂留的共用战斗 SO，已在后续[旧 SO 清理](2026-10-19-battle-so-cleanup.md)中迁移全部引用并删除。

场景原有 94 个序列化对象块全部保留，新增 9 个块（Host、XML 来源、Director、时间线子物体和四个动作）。原块只修改战斗根组件列表、移除旧规则引用、绑定 Director 和添加时间线子物体；没有重建 UI、相机或卡牌资源。

工具入口：`工具 > 卡牌战斗 > 2D验证 > 接入 XML 与可配置表现时间线`。重复执行保留已有非空配方和 XML 配置；“创建或更新场景”也使用同一组装入口。详细配置见 [Minimal2D README](../../Assets/Scripts/Gameplay/SingleBattle/Presentation/Minimal2D/README.md)。

## 验证

| 检查 | 结果 |
| --- | --- |
| Unity 编译 | 无错误、无警告 |
| 最终 2D EditMode 专项 | 18/18 通过 |
| 扩展 EditMode 回归 | 406/411 通过，5 项已知失败见下文 |
| 2D 与 3D 联合 PlayMode | 8/8 通过（2D 3 项、原有 5 项） |
| 实际 2D 射线交互验证 | 通过：2 次卡牌点击、4 次按钮点击、2 个完整回合 |
| 场景与元数据 | GUID 保留，新资产均有 meta；本次文件 diff whitespace 检查通过 |

新增覆盖 XML 与无界面规则执行一致性、无效编成保留已有会话、时间线依赖与生成幂等、卡牌动作取消后按最新 slot 恢复。实际 PlayMode 验证预选切换/清空、出牌、弃牌、敌人回合、View 与 Host 启停门禁及重开；把攻击依赖由 ReadyToAttack 改成 cards.Completed 后，实测攻击在卡牌完整移动 0.7 单位后启动。XML 增加第二名敌人的回归验证队列不会重入同一矩形动作，规则状态和显示快照一致。

单独 2D 射线验证确认牌区和双方矩形确实移动、取消恢复没有再次结算规则，画面检查确认手牌与中文 HUD 正常；运行与退出均无错误日志。相机继续使用 MainURP 的 Simple2DRenderer（槽 2），未修改公共管线与其他 Renderer。

本地证据（Library 不入库）：

- `Library/BattlePresentationValidation/battle2d-xml-editmode.xml`：扩展 EditMode。
- `Library/BattlePresentationValidation/battle2d-xml-focused-editmode.xml`：最终 2D EditMode 专项。
- `Library/BattlePresentationValidation/battle2d-xml-playmode.xml`：最终联合 PlayMode。
- `Library/Battle2DValidation/play.json` 与 `game-screen.png`：实际射线输入与画面。

## 扩展检查的已知问题

与上一轮 Host 拆分记录一致：CardHoverPresenter 的视口边界断言、两项 CardUICamera 分辨率断言（960×540 对 853×480）、UI Renderer 带有 CRT Feature 的断言，以及规则包内本地 `.DS_Store` 缺少 `.meta` 的打包检查，共 5 项失败。没有修改这些模块或删除本地文件以通过测试。

中间一次联合 PlayMode 退出时，3D 的 `SingleBattleHud.RefreshEnemyIntents` 曾出现已销毁 RectTransform 引用日志；后续联合重跑未复现，单独 2D 运行和退出也未出现。本次未修改该 3D HUD，不将其列为已修复问题。

本次未运行完整 Player/IL2CPP 构建，也未重复运行纯规则 .NET 测试。规则子模块与 XML 内容本身没有修改；场景默认规则由旧 Demo SO 切换为共用 XML 编成，因此采用 XML 的生命、伤害、弃牌预算及牌型效果。
