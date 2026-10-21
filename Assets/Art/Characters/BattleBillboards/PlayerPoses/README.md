# 玩家阶段立绘

当前素材于 2026-10-21 从用户提供的 `Downloads/untitled folder` 原样接入，保留 PNG 透明通道与现有资源 GUID。四张人物原图均为 3850 × 4320 RGBA；小怪 `../Enemy.png` 为 2442 × 2349 RGBA。Unity 导入上限为 2048，按原始宽高比显示。`Generation.json` 仅记录先前版本的生成提示词，不代表当前素材来源。

源文件对应：`005927` → 默认持牌，`005918` → 弃牌思考，`005924` → 出牌动作，`005921` → 结束防御，`005930` → 小怪（文件名前缀均为 `20261021-`）。

| 规则显示阶段 | 立绘 | 姿态 |
| --- | --- | --- |
| Draw / ForcedDiscard | Player-Discard.png | 合掌思考、选牌 |
| Action | Player-Action.png | 转身挥手、出牌 |
| Ending / 敌人行动 | Player-Ending.png | 抬掌警戒、防御 |
| 未开局 / 战斗结束 | ../Player.png | 持牌待机 |

`BattleDisplayProjector` 将当前显示快照的阶段投影成 `UnitDisplayData.Labels["portraitPose"]`。现有 Actor 同步链将其送到 `PortraitBattleActorPresenter`，无需表现组件访问规则会话。中间伤害事实不携带阶段标记，不会因为受击切回旧姿态。

过渡默认 0.12 秒淡出并向右移动立绘宽度的 5.5%，换图后 0.18 秒从左侧小偏移淡入归位。使用非缩放时间；重复快照不重播，连续切换从当前透明度接续，取消/重新绑定恢复可见状态。只改变 Artwork 子节点与 MaterialPropertyBlock，父节点继续使用原有攻击、受击、死亡和护盾表现协议，共享材质不变。

人物接入 BattleDemo 的玩家 Actor，小怪接入现有敌人 Artwork 材质。编辑器菜单：`Tools/BattleDemo/Portraits/Set Up Phase Portraits`。在 Inspector 的玩家 Portrait Presentation 上可更换贴图、调整淡入淡出时长和右移距离。

验证：5 项阶段立绘 EditMode 测试、3 项既有 Actor 表现测试全部通过；真实 Play 流程已观察到 discard → action → guard → discard，并确认中间透明度和向右位移。验证菜单会重开当前测试对局，报告与截图输出到 `Library/PlayerPortraits/`。
