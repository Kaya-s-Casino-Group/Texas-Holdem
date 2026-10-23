# 七站牌型倍率与暴怒预警调整

日期：2026-10-23。基于主仓 `193cdb2`、BattleToolkit `b788c09` 及保留的本地未提交改动。

## 实现范围

- 仅七站战斗配置：两对基础倍率 4→3、顺子基础倍率 2→3、同花额外倍率 2→5。普通同花顺基础总倍率为 3×5=15。
- 一对保持×2；葫芦保持三条、对子独立评分后各乘2，1级无加成时实际各×4。顺子每张计分牌单独造成伤害、每段附加1层中毒。
- 同花治疗仍为同花计分范围的牌面点数和×50%，心连心仍增加30个百分点；牌型成长、道具程序及皇家同花顺强制消灭均未改。
- 七站暴怒 `initialWarningRound` 5→1，`cooldownRounds` 仍为5。保留本体普通行动及即时反应完成后预警／追加大招的原顺序。
- 五手齐全且本体能正常行动时，R1预警/R2释放、R7预警/R8释放、R13预警/R14释放。R2为33普攻＋99大招；R8先蓄力，再释放159大招。
- 断手取消、控制打断、8回合再生、两次免伤及其他战斗规则未改。旧标准配置、独立Demo时序及旧三章暴食节点未改。
- 开场字号最高24→36、黑底高度约1.5倍、内边距18×8；大招复用同位置同样式黑底红字。文案为“警告：Boss有五只手臂，将于下一回合释放大招！！”。开场仍展示4秒，随后显示仍有效的大招预警；释放、打断、战斗结束即隐藏。提示不拦截点击，现有红光跟随规则状态。
- 更新相关规则测试、HUD测试以及七站实际Play测试中过期的生命、意图、再生节点和布局假设；没有为通过测试改变当前美术、布局或战斗表现。

## 实际验证

Unity使用 **2022.3.62f3 / macOS / Metal**。在当前工作区的APFS隔离副本上导入、编译及运行，避免关闭用户原编辑器或写入其动态字体。全部12个本次修改的C#/XML文件与测试副本逐文件哈希一致，见 `tested-source-match.json`。Play测试从实际七站场景复制隔离场景并关闭自动存档；未读取、覆盖玩家旅程存档。

证据目录：`Library/WrathBalance-20261023-173055/`。

上述验证使用保留原有未提交改动的工作区。此次提交排除了原有的普通战出牌／弃牌飞行观察修订；3/3 Play结果对应包含该修订的工作区，未对排除该修订后的完整提交树重复执行普通战用例。本次Boss生产代码及Boss回归与已验证版本一致。

| 验证 | 结果 | 证据 |
|---|---|---|
| 规则TDD RED，旧生产配置 | 66例中27个预期失败 | `engine-red.trx` |
| 更新配置后的计分／敌规 | 66/66通过 | `engine-green.trx` |
| 七站计分、敌规、牌型说明 | 81/81通过 | `engine-full-final.trx`、`engine-validation-summary.json` |
| 完整.NET规则套件 | 1796例，1783通过，13个既有失败 | `engine-full-final.trx` |
| 隔离HEAD规则基线 | 1791例，1778通过，同样13个失败 | `engine-baseline-isolated.trx` |
| Unity提示RED | 29例中6个缺功能失败、2个射线夹具失败 | `unity-ui-red.xml` |
| 射线夹具修正后在旧生产基线复验 | 2/2通过 | `unity-raycast-baseline.xml` |
| Unity最终EditMode相关回归 | 197例，196通过，1个既有失败 | `unity-related-editmode-final.xml` |
| 普通战斗实际PlayMode | 出牌／弃牌分流、道具、灰烬和奖励流程通过 | `unity-related-playmode.xml` 中普通战用例 |
| Boss图形PlayMode | 2/2通过：五手断裂／恢复；提示及R1/R2、R7/R8时序 | `unity-boss-playmode-graphical.xml` |
| Unity编译 | 0个C#编译错误；保留既有CS0618警告 | `unity-related-editmode.log`、`unity-boss-playmode-graphical.log` |

编译警告是 `Assets/Scripts/Tools/Editor/ConceptBattleSetup.cs(52,21)` 使用过时的 `TextureImporter.spritesheet`，未在本次范围内更改。

第一次批处理Play运行中，普通战通过，两个Boss用例均停在“未生成截图”的断言；未把这两项算作通过。改为带图形窗口的Unity进程后，两项均完整通过，耗时137秒，退出码0。最终实际Play合计 **3/3通过**。Boss最长实际跑到第9轮验证再生；20轮上限仅由规则测试验证，没有进行20轮实机跑测。

实际截图在证据目录的 `screenshots/`：

- `boss-opening-warning.png`：放大的黑底红字开场。
- `boss-round-1-warning.png`、`boss-round-7-warning.png`：普通行动完成后的持续预警。
- `boss-round-2-released.png`、`boss-round-8-released.png`：大招结算后提示消失。
- `boss-severed.png`、`boss-regenerated.png`：五手消失、倒计时及原对象原位置再生。

预警截图在该轮敌方行动结束后捕获，顶部回合计数已进入下一轮玩家阶段；这是原有回合显示行为。

## 已有失败与验收边界

最终.NET套件的13个失败与隔离HEAD基线的完整测试名和断言消息完全相同，没有新增失败。完整名称和比较结果记录在 `engine-validation.md`、`engine-validation-summary.json`。

Unity EditMode剩余失败是 `WrathBossRulesTests.RoyalPlayPreviewIsPureAndEliminationStillCompletesTheAttachedFlushHealing`，期望生命99912、实际99909；它读取旧标准配置，并在修改前的.NET规则基线中以相同断言失败。本次没有改动该旧皇家同花顺／治疗逻辑，不能宣称扩展套件全绿。

新增加或更新的回归覆盖：顺子新倍率和分段、同花顺×15、葫芦分项、同花治疗与心连心独立、道具加成、手雷及顺子消耗两层免伤、普通行动后预警、首轮预警被断手取消、首次释放受控取消及冷却继续，以及真实提示可读性／切换／点击穿透。

配置指纹变化后，旧七站旅程存档会被现有恢复校验拒绝；原文件保留，本次没有自动迁移或清除存档。新规则应通过新旅程验证。

## 工作区保留

- 原字体资源和三份未跟踪验证文档与操作前哈希完全一致。
- 原 `SingleChapterAdventureSceneTests.cs` 的出牌／弃牌飞行观察修订保留，在其后追加本次回归及相关旧断言修正。
- 验证阶段未重置、提交或推送；随后按用户要求单独提交、推送本次调整，并保留上述原有未提交改动。工作区原始副本保存在证据目录 `original/`。
