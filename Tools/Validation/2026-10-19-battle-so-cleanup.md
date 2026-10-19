# 战斗 SO 依赖清理与 XML 迁移验证

## 结果

删除 `Assets/Scripts/Gameplay/SingleBattle/Configuration`（17 个 Profile、工厂、2 个专用编辑器、旧工厂测试及 3 个程序集）和 `Assets/GameAssets/SingleBattle/Configuration`（19 个战斗 SO）。对应目录及资源的 `.meta` 同步删除。扫描 Assets 下所有 scene / prefab / asset / asmdef，对已删除 GUID 的引用为 0。

`SingleBattleSceneAdapter` 仅保留 XML 来源；删除配置来源枚举、Legacy 分支、RulesProfile 字段和工厂调用。玩家/敌人 Actor 不再持有 Profile 或创建规则 Setup，未绑定时不从资产推算规则。XML 编成仍严格校验数量和参与者顺序；编译或组装失败保留原有 Session。

数值配置窗口保留原菜单，改为定位 XML 与编成、校验场景阵容。单人/多人生成器均绑定 XML。移除配置程序集和计分 SO 程序集的直接引用。现有 UI 运行验证改为持有临时 TextAsset 副本并在结束时恢复原 XML，不修改作者资产。

## 场景迁移

新增 `Assets/GameAssets/SingleBattle/Content/SceneBattles.xml`，保留旧场景数值，不套用新策划包的牌型技能：

| 场景/用途 | 编成 | 保留内容 |
| --- | --- | --- |
| SingleBattleDemo、ForestRuins | single-battle | 800/1200 HP、240 敌伤、20 回合、无限弃牌、每牌 8 盾、敌阶段末清盾、出弃立即补牌、最低生命攻击目标 |
| 已保存 TeamBattleDemo | demo-battle | 原场景实际为 1v1；40/75 HP、7 敌伤、445 回合、整场 3 次弃牌、无收益、出牌下轮补牌 |
| 新建 2v3 示例工具 | team-battle | 同上 demo 数值；两个独立牌组、三个敌人 |
| SingleBattleDemoNew、Battle2DValidation | single-battle-new | 继续使用规则包中的策划 XML，数值与表现时间线未改变 |

三个旧场景只替换规则引用并移除 Actor Profile 字段；不重建模型、UI、牌区或时间线对象。SingleBattleDemoNew 移除无效的旧配置字段。

通用 `PokerScoreProfile`、CardArt、渲染、RunFlow SO 不属于这次旧战斗配置层，仍有独立模块消费者，予以保留。现有未提交的环境美术、其他场景等修改不作为本次清理目标。

## 验证

- Unity 脚本编译通过，0 warning。
- 3D 专项 EditMode：97/97。包括三个迁移场景无 Missing Script、XML 只构建未开局会话、原数值/弃牌/完整敌方回合、多参与者独立牌组、快照隔离、无效 XML 不替换会话、预选和动作取消、护盾归属等。
- 完整表现 EditMode：410/415；5 项失败与清理前一致，见下方。
- 2D/3D 实际场景 PlayMode：8/8，包含开局、预选切换与清空、信号依赖、出牌、弃牌、镜头/UI、敌方队列、同步取消与重开。最终 Console 无错误。
- 已删除 GUID 引用扫描：0；旧 Profile 类型/工厂/模式枚举的生产代码引用：0；配置程序集依赖：0。

本地结果：`Library/BattlePresentationValidation/so-cleanup-scene3d-editmode.xml`、`so-cleanup-editmode.xml`、`so-cleanup-playmode.xml`。

完整回归的既有失败：

1. CardHoverPresenterTests.TooltipFacesTheSharedCameraAndClampsWhileFollowingCardScale：tooltip viewport x 为 -0.00866752863。
2. CardUICameraTests.NativeUIStackDrawsSinglePixelStripesAfterSceneResolveAndDisabledUILeavesScene：期望 960×540，实际 853×480。
3. CardUICameraTests.OverlaysAppendedAfterUIStayBeforeNativeUIOnTheirFirstRender：同上。
4. CardUICameraTests.RendererIsForwardUIOnlyAndDoesNotInheritRetroOrLowResolutionFeatures：UI renderer 含 CRTFeature。
5. PackagingTests.EngineAssetsHaveImportedUniqueMetadata：规则子模块内 `.DS_Store` 没有 meta。

本次清理未修改规则子模块，未执行 Player/IL2CPP 构建。
