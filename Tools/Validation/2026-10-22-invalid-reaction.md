# 出牌／弃牌 InvalidReaction 与意图不可用排查

日期：2026-10-22。Unity：2022.3.62f3，macOS / Metal。

本文保留既有出牌／道具／替死修复的历史验收。后续用户要求将这些修复及回归提交到两个远端，同时将七站普通 A 牌面点数由 11 改为 1；按要求本轮仅编译，不重复运行此前通过的测试。

## 现场与原因

七站暴怒 Boss 第 19 轮，玩家 15/144 生命、剩余 1 行动，Boss 本体 157 生命。
成功存档 revision 94，装备包含 10001、15001、11003 和仅剩一次的替死鬼 13004。
原存档先完整复制到 `Library/InvalidReaction-20261022/user-run-before.json`，诊断仅操作副本。

此前出牌已让道具 10001 产生 `program/previous-straight` 的 Round 临时计数。
结束回合后敌方造成致死伤害，替死鬼复活并消耗自身。
`BattleRelicLoadout.RebindItems` 重建剩余道具时，将完整战斗 Memory 交给只允许
Encounter 持久记忆的 `BattleRelicSetup`，抛出 `ArgumentException`。
规则层将其转换成 `InvalidReaction`，整次事务回滚。

最后一次出牌或弃牌会自动结束玩家回合，因此这两个按钮也会被同一失败阻断。
敌人意图预览同样模拟结束回合和敌方行动，所以返回 rejected 并显示“意图不可用”。
普通药水消耗、骰子替换以及管理员编辑既有道具也存在相同的记忆边界问题，
分别可能返回 `InvalidSelection` 或 `InvalidAdminPatch`。并非 Boss 的轮数上限或牌型伤害错误。

## 修复范围

- `BattleRelics.cs` 的删除／替换路径：仅在构造持久 Setup 时复制 Encounter 记忆，
  然后通过原有 `WithRuntime` 恢复幸存道具完整战斗记忆、剩余次数、本轮使用次数和冷却。
- `BattleAdminCommands.cs` 的道具编辑路径采用同样处理，保留完整当前战斗状态。
- 不放宽持久存档校验，不清空临时计数，不改牌型、道具效果、敌人数值或表现层。
- 存档结构及规则指纹未改变，原第 94 步存档无需迁移。

## 规则复验

原始结果、堆栈、TRX 和 Unity 结果统一位于 `Library/InvalidReaction-20261022/`。

| 验证 | 实际结果 | 证据 |
| --- | --- | --- |
| 新增道具专项先复现失败 | 前四项 0/4，末次弃牌 0/1，均按预期复现同源错误 | item-memory-red.trx、item-memory-discard-red.trx |
| 修复后新增道具专项 | 5/5 通过 | item-memory-green.trx |
| 管理员道具编辑先复现失败 | 1 失败、2 通过；失败为 InvalidAdminPatch | admin-memory-red.trx |
| 管理员／道具／遗物联合专项 | 71/71 通过 | admin-memory-green.trx |
| 全量 .NET | 1745 项，1733 通过、12 失败；失败名称与既有基线完全相同，无新增失败 | admin-memory-full.trx、baseline-comparison.json |

新增测试除真实出牌、药水／骰子／替死鬼顺序外，还检查非零充能、本轮使用次数、冷却、
Encounter 与 Round 双作用域记忆保留，以及存档导出仅过滤 Round 记忆。

## 精确现场副本

修复前 `PreviewEnemyAction` 和结束回合均返回 `InvalidReaction`。
修复后三个独立分支均成功，且原存档不变：

| 操作 | 玩家生命 | Boss 生命 | 下一回合行动 | 替死鬼 |
| --- | --- | --- | --- | --- |
| 结束回合 | 15 → 86 | 157 → 124 | 3 | 正常移除 |
| 出梅花 9 | 15 → 86 | 157 → 115 | 3 | 正常移除 |
| 弃梅花 9 | 15 → 86 | 157 → 124 | 3 | 正常移除 |

三个分支均 revision 94 → 95、完成轮数 18 → 19；各自 Export → Restore 完全一致，
恢复后再次出牌成功，revision 95 → 96、行动 3 → 2。
只读意图预览 accepted，未消耗原状态的道具。证据：`reproduce-after.log`。

## Unity 验证

首次重新导入编译无脚本错误、无编译警告；Unity EditMode 已实际执行道具专项 5、
七站牌型 38、敌人规则 9、内容 6，共 58/58。

独立 Unity 进程首轮实际 PlayMode 为 4/6 通过：原有四项七站流程、普通战斗、流血精英、
Boss 断手／再生均通过；新增两项濒死复活用例捕获 `ActionCancelled: effect`，未按通过记录。
证据：`unity-adventure-and-revival-playmode.xml`，进程退出码 2。

进一步发现玩家专用 `PortraitBattleActorPresenter.SynchronizeDisplay` 在每次收到 0 血快照时，
直接调用完整 `CancelPresentation`，连带取消基类正在播放的致死 Hit。
Hit 发出 Revealed 标记后，检查点同步重复死亡快照便会触发该问题，早于后续 Revive 的恢复保护。
修复已将其限定为仅停止立绘自身的换图／滑移，让时间线正常完成受击、死亡及复活；
显式取消、重置和禁用时仍执行原有完整清理。
另增加真实场景中的 Hit／Death 快测，先确认底层 operation 正在 Pending，
重复同步死亡快照后仍 Pending，随后自然 Completed，避免使用 EditMode 的即时完成路径空测。

修复立绘后第二轮 5/7：Hit／Death 快测和原有四项场景回归通过，
两个复活用例已通过 86 生命、3 行动、移除替死鬼及玩家可交互的断言，
但选牌后同帧检查按钮 `interactable` 失败。按钮原本在 `HandActionPresenter.LateUpdate`
刷新；原 ClickButton helper 会先等一帧，这两处新增直接断言遗漏了等待。
测试补上等待一帧，保留两个按钮状态和随后真实点击断言，没有调整运行时按钮逻辑。
第二轮证据：`unity-adventure-and-revival-playmode-final.xml`，退出码 2，未记为整轮通过。

最终新增三项 PlayMode 全部通过，进程退出码 0：

1. 末次弃牌触发复活并恢复实际按钮输入：24.59 秒。
2. 末次出牌触发复活并恢复实际按钮输入：23.85 秒。
3. 重复死亡快照不中断真实 Hit／Death 动画：2.03 秒。

证据：`unity-item-revival-final.xml`。前两项还实际点击复活后下一张出牌，确认行动 3 → 2。
与第二轮已通过的原四项场景回归合计，当前最终验证的七个 PlayMode 用例均通过；
这七项来自两次独立运行，不把第二轮 5/7 写成整轮 7/7。

最终 Unity EditMode 66/66 通过、0 失败、0 跳过，进程退出码 0。
范围为新增道具 5、管理员／内存边界 3、七站牌型 38、敌人规则 9、内容 6、立绘 5。
证据：`unity-editmode-final.xml`。Unity 编译、EditMode 与真实渲染 PlayMode 分开记录，
没有将纯 C# 结果用作实机通过证据。

正式场景重新打开后再次执行脚本重编译，成功且 0 编译警告（`unity-recompile-final.json`）；
当前 Console Error 为 0（`restored-console-errors.json`）。编辑器保持 PlayMode 的冒险菜单，
“继续旅程”可用；原第 19 轮 revision 94 存档字节保持不变，未替玩家提交战斗动作。

## 单 A 点数后续调整

上述历史排查时，七站高牌额外基础分为 0、倍率为 1，Ace 点数为 11，所以单 A 伤害为 11。
本轮按用户决定，将 `SingleChapterBattle.xml` 的 Ace 普通牌面点数改为 **1**，内容版本由
`single-chapter-battle.2026-10-22.5` 升为 `.6`。一级无加成单 A 为 `1 × 1 = 1`，AA 为 `2 × 2 = 4`；
A 参与其他普通计分和同花治疗时也按 1 点，牌型排序保持。顺子仍独立按 A2345 的 1 或
10JQKA 的 14 逐段计分。同花倍率、各牌型倍率和升级增长、道具效果、旧三章及全局默认没有改。
当前标准单局配置原本已为 Ace=1，无需修改。

`SingleChapterScoringTests` 的单 A 预览／实际伤害预期同步为 1，并补充 AA=4、三级高牌 A=3 的预期。
这些 A 新规则用例本轮仅编译，**未执行**；不得沿用此前 A=11 时的通过记录声称新数值已完成运行验收。

## 保留与边界

未实际运行满 20 轮，未验证独立 Player 打包。
Unity PlayMode 使用真实七站场景的隔离副本，并关闭自动存档，不覆盖用户旅程。
两个仓库原有未提交内容在操作前按完整字节备份；本轮将出牌／弃牌／意图／道具／替死修复及对应回归纳入提交。字体缓存、旧飞牌采样修复及其他无关历史记录保留未提交。
验证生成的 TMP 动态字体缓存另存 `font-after-validation.asset`，再按本轮开始前的完整字节
恢复原字体文件，保留其已有未提交内容。原有其他脏文件 SHA256 均未改变；
见 `preserve-after-validation.json`；重新打开正式场景后再次核对，
`preserve-final.json` 仍全部为 true，包括原存档。


## 本轮提交范围与验证边界

- BattleToolkit：`BattleRelics.cs`、`BattleAdminCommands.cs` 的记忆边界修复；两个新增规则／管理员回归及 `.meta`；七站 A 点数、牌型测试预期及内容说明。
- 主仓：`PortraitBattleActorPresenter.cs` 的死亡快照播放修复；`AdventureItemRoundMemoryTests.cs` 及 `.meta`；本文和子模块引用。
- 字体缓存、`SingleChapterAdventureSceneTests.cs` 的飞牌采样修改、其他三个历史排查文档保持未提交。

上文的 Unity 66项 EditMode、新增3项 PlayMode及规则回归为**历史实测**；本轮不重复运行。
与精英提交时相比，先前保留在本地、导致远端替死播放风险的立绘修复此次一并纳入。
A 点数是新的配置变化，本轮验证限于编译，未跑完整测试或新的 Unity 实际出牌验收。

内存与立绘修复本身不改变存档结构；本次 A 点数改变了内容版本和指纹，需重新读取配置并开启新旅程。
旧内容指纹存档沿用既有恢复限制，本轮不迁移或覆盖用户存档。

本轮编译与文件保护证据：`Library/GameplayFixPush-20261022-195907`。


本轮实际编译结果：

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| `.runtime/dotnet/dotnet build Tests/BattleToolkit.Tests.csproj --configuration Release --nologo` | 成功，退出码0，0警告、0错误；包含运行时及回归测试程序集 | `dotnet-build.log` |
| Unity 2022.3.62f3 `recompile_scripts` | 全脚本重编译成功，0警告 | `unity-compile.json` |
| Unity Console Error | 0条 | `unity-console.json` |

本轮未运行 `.NET test`、Unity EditMode／PlayMode 或完整回归，旧通过结果不重复计数。
