# BattleDemo 参考图布局与角色面片

2026-10-20 按用户提供的灰色区域布局图调整 BattleDemo。左上为回合/能量和玩家血条，敌人血条与意图放在右上方斜条，右侧竖栏保留可滚动的状态/遗物/结算内容；底部从画面约 28.4% 处开始放牌型预览、牌库、手牌和操作按钮。摸牌/强制弃牌时，右下操作区域切换为对应流程控件。

玩家和敌人的胶囊已替换为透明 Quad 面片，材质为官方 `Universal Render Pipeline/Unlit`，无光照、投影、法线或定制 Renderer Feature。保留逻辑 Actor 和原 Visual Transform 的场景引用，原攻击/受击/死亡表现仍作用于 Visual；视口适配只调整外层 Actor，不每帧追随相机震动。

资产：

- `Assets/Art/Characters/BattleBillboards/Player.png`：根据布局图中的白发角色制作。
- `Assets/Art/Characters/BattleBillboards/Enemy.png`：使用用户随后提供的独立敌人原图去灰底。
- 同目录的 Player / Enemy / HandBackdrop 材质。

贴图通过内置 `image_gen` 工具生成/去底，保留 RGBA 透明通道。完整输入说明和提示词见 `generation.json`；生成结果已复制进工程。

编辑器菜单 `Tools/BattleDemo/Apply Reference Layout` 可重新应用排布。场景保存后可直接 Play，不需要再次执行菜单。不要在运行模式保存场景。

检查记录位于 `Library/BattleDemoLayout/`，包含最终 `preview.png`、宽屏截图和运行验证结果。已实际点击验证跳过摸牌、选牌和出牌，扣除能量及敌人血量变化正常。EditMode 533 项、PlayMode 8 项全部通过；重新进入游戏后 Console 无错误。测试完成退出时曾出现 Unity Test Runner 1.3.3 的 `PlaymodeLauncher.BackgroundWatcher` 空引用，未发生在游戏代码中。
