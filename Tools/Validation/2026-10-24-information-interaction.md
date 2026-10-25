# 七站冒险：信息与交互优化验收（2026-10-24）

## 范围

- 弃牌预览明确显示“格挡+X”，使用既有实际弃牌预览数据。
- 暴怒本体显示“免伤剩余N次”，耗尽后短暂显示0；说明每段正数伤害消耗一次、只保护本体。
- 现有底栏显示参与计分的点数、倍率及核心效果；葫芦保留三条/对子分项。完整算式和其他效果仍在原有详情中。
- 飘字改为简短状态文本并保留详细日志；逐段伤害保留，末段增加实际扣血汇总。顺子转火可汇总，反应/道具追加伤害不混入牌型主段汇总。
- 选定单体金色轮廓、群攻实际命中目标浅蓝轮廓；当前目标名称附在既有血量栏。保留暴怒大招独立红光。
- 商店货架/服务/购买详情明确金币不足及差额；不足时仍可点图标看详情，确认购买沿用权威校验。

不修改 BattleToolkit、战斗/牌型/道具数值、精英/Boss规则、七站或旧三章节点、原有场景和美术布局。不提交或推送。

## 环境及方法

- Unity 2022.3.62f3（96770f904ca7），macOS Metal。
- 主仓库基线 `310f0f091c6e5a63c0d368dd8eac3a7a5f0d5fb6`；BattleToolkit基线 `f3f353a5f520c1bc662680296973ab82d956c310`。
- 使用已有隔离验证副本 `/Users/mt/code-yang/Texas-Holdem-RoundLimitValidation-20261023-221206`，同步本次脚本及当前引擎；Packages与关键ProjectSettings逐字节一致。
- PlayMode实际加载七站冒险场景的临时副本、使用正常渲染及UI射线点击。只通过管理接口准备卡牌/血量/金币等夹具和跳过无关战斗，不写用户存档。
- EditMode/纯引擎回归与实际有渲染PlayMode分开记录；未重跑20回合长测。

## 验收记录

| 验证层次 | 实际结果 | 证据 |
| --- | --- | --- |
| Unity EditMode相关回归 | 86/86通过，0失败 | `Library/InteractionPolish-20261024/editmode-final2.xml` |
| 引擎相关回归 | 122/122通过，0失败 | `Library/InteractionPolish-20261024/engine-related.log` |
| Unity有渲染PlayMode | 7/7通过，0失败 | `Library/InteractionPolish-20261024/playmode-final.xml` |

最终有渲染PlayMode七项：普通战斗飞牌/道具/击杀收尾；新增计分/弃牌/连击；Boss目标切换/两次免伤/连击；金币不足图标查看；可支付购买；指南缓存；切换目标后的指南预览缓存。所有测试清理时均断言无运行错误。最终日志 `playmode-final.log` 无 `error CS`，Unity正常以code 0结束。

重点观测：

- 选一张7弃牌，界面显示“格挡+7”，真实获得7格挡。
- 普通混色23456顺子真实5段扣血合计60，叠5层中毒；汇总按实际主段扣血计算，不把护盾吸收、道具反应算入。
- 暴怒两次免伤下A2345：实际逐段扣血 `[0,0,9,12,15]`，合计36。两次独立高牌攻击时，HUD明确显示2→1→0；0的短暂提示会自动退场。
- 原宽高内检查低牌葫芦、AAAKK+13003和受控×204倍率的真实字形宽度；完整分项式/效果保留在详情中。
- 顺子每击击杀一只手后自动转火：真实桥接结果仅在末段汇总一次，五段实际扣血5；单次三目标群攻不误报3段连击，葫芦群攻+追击则标为“4次命中”。这三项为引擎/展示桥接回归，区别于实际有渲染Play验收。
- 飘字去重跨fact scope不取消旧operation/checkpoint；旧scope释放不影响新文字。上方空间不足时转向下方行，保持原模板坐标；新命令/同步/禁用清理尾部。
- 金币不足仍可通过图标查看详情，禁购按钮提示所需价格和差额；查看不改变金币或revision，真实购买仍以原权威逻辑只扣一次。

本轮实际验证发现并修复的实现问题：原预览栏被皮肤压缩至约224×23，双行/过长分项式会溢出；切换战斗时重配展示配方发布旧快照造成身份同步错误；延长飘字寿命后重复状态刷新挤占回合栏。均补充了针对性回归。实际截图先等淡入至可读透明度，并检查所有可见字形在屏幕内，未以“存在TMP文本对象”代替可见性验收。

引擎命令：

```sh
bash Tools/test-poker-engine.sh SingleChapterScoringTests SingleChapterEnemyRulesTests CombatWrathImmunityTests AdventureEconomyTests RelicNumericBattleTests PokerHandEffectIntegrationTests
```

Unity两类验证均通过`-runTests`运行，EditMode使用`-batchmode -nographics`；有渲染PlayMode使用正常Metal渲染、1280×720，不带这两个参数。未运行全项目所有测试或重新测试20回合流程；这不在本批改动范围内。

## 原有改动保留

任务开始保存了状态、完整补丁及原有脏文件副本于 `Library/InteractionPolish-20261024/`。
两个字体缓存及三个既有未跟踪调查文档与开始时SHA256一致，记录见 `Library/InteractionPolish-20261024/preservation-after.json`。最终验证副本与工作区本次29个修改/新增脚本及meta逐字节一致。
既有冒险PlayMode测试中的飞行观察修复完整保留；只另将旧“无倍率符号”断言更新为本次要求的“显示倍率符号”。
BattleToolkit保持原始HEAD、工作区无变更。


## 实际截图

- [连击汇总与状态飘字](/Users/mt/code-yang/Texas-Holdem/Library/InteractionPolish-20261024/screenshots/information-combo.png)
- [Boss目标轮廓与免伤剩余次数](/Users/mt/code-yang/Texas-Holdem/Library/InteractionPolish-20261024/screenshots/information-boss-target-immunity.png)
- [葫芦加倍率道具预览](/Users/mt/code-yang/Texas-Holdem/Library/InteractionPolish-20261024/screenshots/information-fullhouse-relic.png)
- [货架金币不足](/Users/mt/code-yang/Texas-Holdem/Library/InteractionPolish-20261024/screenshots/information-shop-insufficient.png)
- [详情禁购与差额](/Users/mt/code-yang/Texas-Holdem/Library/InteractionPolish-20261024/screenshots/information-shop-disabled-purchase.png)

本批六项信息/交互改动与相关回归均完成，无遗留阻塞项；没有提交或推送。
