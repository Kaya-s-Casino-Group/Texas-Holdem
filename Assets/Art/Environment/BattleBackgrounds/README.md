# BattleDemo 背景

只用于 `Assets/Scenes/BattleDemo.unity`，同一对局内切换三个环境：

| 选项 | 内容 | 分层 |
| --- | --- | --- |
| 精英 | 原有 StylizedRuins | 保留原有全部面片和投影地面 |
| 小怪 | 红月荒原 | `Minion_Back` / `Minion_Mid` / `Minion_Front` |
| Boss | 城堡入口 | `Boss_Back` / `Boss_Mid` / `Boss_Front` |

画面上的背景选择按钮已移除。编辑时可在 `Battle Backgrounds` 的
`BattleBackgroundSwitcher` 选择背景，或使用 `Tools/BattleDemo/Backgrounds` 菜单。
默认保留精英背景；切换只控制环境对象，不重开战斗、不改变敌人或规则状态。

两张原图复制在 `Sources`；篝火未导入、未处理。使用内置 `image_gen` 从原图提取透明
中景、前景，并补全远景遮挡区域；完整提示词保存在 `Generation.json`。生成图为约
1886 × 834，保留透明 alpha，按原图 2442:1080 比例对齐。它们是绘制补全的分层素材，
不是逐像素复原原图的裁片。

`PaintedBattleBackdrop` 将远、中、前景放在距参考相机 200、50、18 单位的面片上。
地面为 Y=0 的水平网格，使用与远景相同的补全底图：从参考相机将底图下部反投影到
平面，存储 `(u × depth, v × depth, depth)` UV，片元阶段除以 depth，防止地板拉伸。
视口改变时按统一 cover 裁切重建投影；运行时保留标定相机位置，支持相机运动产生层间视差。

沿用现有 `StylizedRuins/Backdrop` 的 Unlit Forward 材质，不增加 Renderer Feature。
背景绘制在角色、手牌及 UI 之前。每套新环境只有三张分层纹理，地面复用 Back，
没有额外的场景文件或整套游戏逻辑副本。

`Tools/BattleDemo/Backgrounds/Set Up Background Choices` 可重新建立当前 BattleDemo 的
绑定（会保存场景并恢复默认精英选项）。`Capture Background Plates` 将三套环境的纯背景
预览输出到 `Library/BattleBackgrounds/`。材质、纹理与脚本均在工程中，运行时不依赖下载目录。
