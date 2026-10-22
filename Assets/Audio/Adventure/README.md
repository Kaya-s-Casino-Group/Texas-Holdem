# 单章冒险音频

交付清单：[飞书音频表](https://moonton.feishu.cn/sheets/LwXksHSTahwDmwtndilcrczLnPf?sheet=wBkXoU)。

本次仅接入 `Assets/Scenes/SingleChapterAdventure.unity`。音乐按阶段循环播放，切换时淡出、淡入；地图和商店复用主菜单音乐。

| 导入文件 | 使用位置 / 触发 |
| --- | --- |
| `Music/MainMenu.mp3` | 主菜单、地图、商店 |
| `Music/NormalBattle.mp3` | 普通战斗 |
| `Music/EliteBattle.mp3` | 精英战斗 |
| `Music/Boss.mp3` | Boss 战斗 |
| `Music/Rest.mp3` | 休息节点 |
| `SFX/Draw1.mp3` | 抽牌长变体 |
| `SFX/Draw2.mp3` | 单张抽牌短变体 |
| `SFX/EnemyAttack.mp3` | 敌方攻击；交付源文件名为 `受击.mp3`，按表中的触发用途绑定 |
| `SFX/EnemyHit.mp3` | 保留原始交付素材，当前未绑定；敌方受击与玩家共用 `multhit1__加倍率计分.ogg` |

原始音频不裁剪、不改写。`Draw1` 约 15.621 秒，音量设为 0.2；长音未结束前不重复选中，后续落牌使用约 0.287 秒的 `Draw2`（音量 0.55），避免快速发牌叠加多段长音。播放仍跟随真实落牌事件。

尚未交付的按钮、金币、胜负结算、警告、主角攻击/受击、死亡、格挡等音效，暂用项目已有 `Assets/Audio/Balatro/SFX` 素材；既有选牌、取消选牌、弃牌音效也继续使用。它们是临时补缺素材，并非本次飞书音频交付。

接入入口为 `工具/局外冒险/接入单章音乐与音效`，代码入口为 `AdventureAudioSetup.ApplyToScene(scene)`。设置页的音乐、音效音量分别由场景内 `AdventureAudioController.MusicVolume` 与 `EffectsVolume` 控制。
