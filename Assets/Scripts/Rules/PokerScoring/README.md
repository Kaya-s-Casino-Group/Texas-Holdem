# 牌型筹码与倍率

入口：`工具/共享规则/牌型计分配置`。默认资产为 `Assets/GameAssets/PokerScoring/Profiles/DefaultPokerScore.asset`，也可通过 `Assets/Create/玩法/牌型计分配置` 新建独立资产。

中文 Inspector 提供三组配置：各牌型基础筹码/倍率及每级成长、13 种点数的单牌筹码、指定花色+点数的覆盖值。例如 K 默认 10 筹码，但可单独让黑桃 K 为 20。负数、缺失点数或重复覆盖会报错，不会静默选择某一条。

## 结算约定

无额外效果时：`最终得分 = floor((牌型筹码 + 参与计分牌的筹码之和) × 牌型倍率)`。

- 继续使用现有德州识别方式；只计入构成当前牌型的牌，高牌只计最高点数那张。筹码配置不改变牌型大小或选牌结果。
- 例如 K♠、K♥、A♣ 识别一对，A 不计分，默认结果 `(10 + 10 + 10) × 2 = 60`。
- 每张计分牌按配置基础筹码 + 牌实例 BonusPoints 计算；保留现有逐牌倍率、重触发和有序 Joker 扩展。失效牌仍组成牌型但不触发逐牌贡献。
- 最终得分由独立 ScoredHandDamageProfile 构建的 IScoredHandDamageRule 用于攻击；默认 EqualScoreDamageRule 完整保留得分作为基础伤害。攻击强度与目标承伤分别由战斗侧 IAttackDamageModifier / IIncomingDamageModifier 扩展，不反向修改筹码倍率。这里的“筹码”是计分值，不进入角色资源或德州下注钱包。

## 默认值

默认参考 [Balatro 牌型数值表](https://balatrohq.com/poker-hands/) 和[单牌筹码说明](https://steamcommunity.com/sharedfiles/filedetails/?id=3166946815&l=english)（2026-10-13 核对）。采用数值基线，不引入完整的小丑牌玩法。

| 牌型 | 基础筹码 | 基础倍率 | 每级筹码 | 每级倍率 |
| --- | ---: | ---: | ---: | ---: |
| 高牌 | 5 | 1 | 10 | 1 |
| 一对 | 10 | 2 | 15 | 1 |
| 两对 | 20 | 2 | 20 | 1 |
| 三条 | 30 | 3 | 20 | 2 |
| 顺子 | 30 | 4 | 30 | 3 |
| 同花 | 35 | 4 | 15 | 2 |
| 葫芦 | 40 | 4 | 25 | 2 |
| 四条 | 60 | 7 | 30 | 3 |
| 同花顺 | 100 | 8 | 40 | 4 |
| 五条（保留已有扩展） | 120 | 12 | 35 | 3 |

等级从 1 起算，皇家同花顺归入同花顺。标准 52 张牌无法组成五条；为兼容已有重复牌实例保留此项。不新增同花葫芦、同花五条。单牌默认：2～10 按点数；J/Q/K = 10；A = 11。

## 边界与接入

- `PokerEngine.Core` 的 PokerHandClassifier 只识别牌型。
- `PokerEngine.Scoring` 的 ScoreProfile / CardPointProfile 是不可变纯 C# 配置；ScoreEngine 读取配置并输出带步骤的 ScoreResult。
- `Poker.Scoring.Runtime` 的 PokerScoreProfile 是 Unity 编辑资产。`Build()` 创建独立快照，无场景/角色状态依赖。
- `BattleRulesProfile` 引用该资产；SingleBattleHost 创建 CardDuelEngine 时注入快照，双方 Demo 共用 DemoBattleRules。Host 不存牌型数值。修改配置只影响下一场战局。
- Run 可将同一资产的 `Build()` 结果传给现有 `RunFlowAsset.BuildDefinition(scoreProfile)` / RunDefinition；Run 继续持有自己的 HandLevels。Run 编辑器与等级自动带入单局的适配尚未扩展。
- 单局通过 PlayerBattleProfile 配置每个牌型等级，开战后由该玩家的 BattlePlayerRules.HandLevels 持有快照；候选预览与正式计分读取同一玩家等级。此公共资产仅保存基础值与每级增长公式。Joker 列表仍为空，旧 Draw/Run 链路保持各自等级模型。
- ICardScoreSource 提供只读基础筹码查询，PlayerBattleActor 读取战局快照。手牌和牌库提示优先消费该来源，不在 CardZone 或美术里维护计分配置。不支持该协议的旧宿主沿用默认查询。
- ScoringCard.BasePoints / PointsPerTrigger 是默认表的便捷查询；使用自定义配置时必须调用 GetBasePoints(profile.CardPoints) / GetPointsPerTrigger(profile.CardPoints)。正式结算已使用后者。

默认计分版本改为 `score.balatro.1`，不应当作旧 `score.example.1` 存档的同版本替换。发布自定义数值更新时更换资产规则版本。配置资产不保存玩家等级，也不在战局中被直接修改。

验证入口：`工具/测试面板` →「牌型筹码与倍率」；资产接入、悬停及快照隔离在「角色与表现协议」中验证。测试只存在于 Editor。
