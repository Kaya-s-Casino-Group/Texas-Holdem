# 牌型指南

已通过 `SingleBattleHud.HandGuide.cs` 接入战斗 HUD。`SingleChapterAdventure.unity` 已保存问号按钮和点击绑定，位于 `Gameplay/UI/DrawPileIcon/Hand Guide`，HUD 的 `guideEntry` 引用该按钮。其他未绑定场景保留运行时创建入口的兼容路径。二级面板点击时创建，使用矩形、细边框和现有字体，不使用生成的面板图片。

## 入口与布局

- 问号跟随牌堆图标的尺寸与位置，间距 14 个 Canvas 单位；战斗未就绪、播放表现或战斗结束时不可打开。
- 同时支持编辑状态的 `Icon` 和运行时安全区域调整后的 `Safe Area/Icon`，避免启动后找不到牌堆节点。编辑菜单 `Tools/BattleDemo/Install Hand Guide Entry` 可重新安装固定入口。
- 面板位于屏幕上部中央，对应需求图中的绿色区域：归一化左上角 `(0.24, 0.055)`，尺寸 `(0.56, 0.57)`，保留底部手牌可见。
- 左侧牌型单选、独立同花筛选；右侧依次显示牌型、方案序号、补齐状态、效果标签、候选卡牌、补齐说明和可滚动公式。
- 已持有牌使用方案色外框；缺牌通过 `+` 分隔，使用真实牌库/弃牌实体对应的卡面与效果。面板复用手牌图集、ArtProfile 和 `CardUIGraphic` 采样。
- 每 2 秒轮换一个方案，手牌呼吸外框、面板方案颜色、卡牌和公式同步更新。查询与公式按快照/候选缓存，不逐帧重新计算。
- 展开时保留真实选牌，禁用世界卡牌鼠标输入；遮罩拦截背景 UI，同时锁定战斗命令、目标选择、道具和玩家切换。关闭按钮或 Escape 退出，恢复原输入组件状态。管理员面板不能同时打开。
- 关闭、禁用或销毁时释放高亮；销毁 HUD 时一起移除运行时入口和面板。

资源：`BattleConcept/Icons/icon_hand_guide` 与 `BattleConcept/Materials/hand_guide_outline` 位于 `Assets/Art/UI/BattleConcept/Resources/`，保证运行时与构建都能加载。

## 规则与查询

`CardDuelEngine.QueryHandGuide(state, actor, new HandGuideRequest(type, requireFlush))`

- `type` 单选；同花是独立筛选。关闭筛选仍保留候选实际拥有的同花加成。
- 顺子 + 同花是普通同花顺；皇家同花顺保留独立入口与消灭效果。
- 按实际最高结算牌型匹配；如葫芦需要其三条部分，也可另查三条得到一个独立合法组合。
- 优先返回手牌已成立的组合，否则返回缺牌最少的可成立组合。同缺牌数按实体 ID 稳定枚举，不承诺收益排序。
- 只统计该玩家的手牌、牌库、可洗回弃牌；排除不可打出、禁用、已移除及其他玩家的牌。每张实体牌只能用一次。
- 返回最小有效组合，不用无关踢脚牌填满。实际规则提高最少出牌数时遵守该限制。
- 已凑齐显示 `已凑齐 · 100%`；尚有必要牌显示 `可补齐 · 还差 N 张`；完整搜索无解显示 `无法补齐`。
- 这是当前牌组的组合可达性，不是抽牌概率，也不保证剩余行动点或回合数足够。
- 默认最多 24 个候选、200000 个搜索节点。`Truncated` 表示结果不完整；无候选且搜索未完成必须显示“组合较多，尚未完成查询”，不能显示无法补齐。可调预算重新查询。
- 示例牌来自真实实体，保留卡面、效果、来源区域；不读取抽牌顺序、不消耗随机数。

查询结果按不可变战斗快照缓存，禁止逐帧搜索。卡牌、遗物、等级或战斗状态变化后重新查询。

## 预览与文案

`engine.PreviewHandGuide(state, candidate, settle: false)` 返回与真实打牌共用的计分、效果配置和遗物技能修正。

- `Score` / `Score.Steps` / `Score.Components`：计分依据及分项；计分不等于总伤害，尤其是葫芦、连击和皇家同花顺。
- `Hand.SubmittedCards / ScoringCards / FlushCards`：参与组合、计分、同花治疗的卡分别保留。纯同花可能只有最高单牌计分、五张牌参与治疗。
- `RuleEffects`：原始配置效果，保留治疗小数；`BaseEffects`：应用遗物技能后的效果，尚未经过目标减免和生命上限。
- `settle: true` 可指定目标和命名目标槽，复用真实 `PreviewPlay`，返回实际伤害、扣血、护盾、治疗与溢出。行动点不足或阶段不符单独返回 `SettlementError`，不影响组合可补齐状态。
- 缺牌时只在私有副本中补入候选牌，用于假设预览，不触发抽牌、不提交状态，也不暴露可误提交的模拟状态。它不模拟弃牌成本或未来回合。
- 必须传入生成候选的同一快照，否则返回 `StaleRevision`。注入的规则回调与普通预览一样必须为纯函数。

`new BattleHandGuideText(preview, statusCatalog)` 提供标题、效果标签、公式行、结算行、手牌高亮 ID 和 `SampleCards`。
`SampleCards` 使用现有 `CardDisplayData` 的 `ArtKey`、`effects` 协议，交给与手牌相同的美术投影和 `CardUIGraphic` 采样路径；面板只在前面排版 `+`，不要把示例创建成真实手牌。

示例文案：

```text
高牌 · 同花                    已凑齐 · 100%
[单体伤害] [治疗]
计分：⌊13 × 2⌋ = 26
同花治疗基值：35 × 0.5 = 17.5
基础治疗：17（自身）
治疗 17 · 实际恢复 0 · 溢出 17
```

有治疗遗物时，基础治疗与实际恢复独立变化；不把已取整数值反推为配置倍率。缺牌情况必须同时显示“补齐示例 · 使用牌库或可洗回的弃牌，不代表下次抽牌”。

## 外框

共享材质：`Assets/GameAssets/CardArt/Prefabs/CardBreathingOutline.mat`。
Shader：`CardArt/BreathingOutline`，URP 透明通道、深度测试、无深度写入。独立放大的圆角矩形面片在卡边外渲染描边与柔光，卡面中心透明。

`CardBreathingOutline.Bind(surface, material, size, center, rotation)` 的矩形坐标为本地 XY、法线 +Z。传入实际卡牌美术层 Transform 及该网格的面矩形；不要使用世界 AABB 代替旋转牌面的尺寸。它跟随表面的选牌位移、扇形旋转和翻转，不接触原卡的材质、属性块或碰撞器。

`CardOutlineCycle` 每组配置一组外框和一种颜色。由调用者使用从打开指南开始累计的非缩放时间调用 `Tick`，默认每组 2 秒、正弦平方呼吸。`ActiveIndex` 同时选择对应的文案与补牌示例；颜色仅区分方案，面板另显示方案序号。
同一牌属于多组时依次显示当前组颜色；离开组、关闭、禁用或销毁时清理外框。面板释放时调用 `Dispose`。共享材质不被修改。组件禁用时释放自有面片与 Mesh（包括编辑器重载）；重新启用或对象池复用后调用 `Bind`，单纯关闭指南可使用 `Clear` 而保留绑定。

## UI 接入约束

- 中央面板：左侧牌型单选 + 同花筛选，右侧效果标签、当前候选、公式和补齐状态。
- 每次方案轮换，外框 / `CandidateId` / `Revision` / 公式 / 示例牌一起切换，不能各自计时。
- 面板内允许切换筛选和关闭；外部手牌、出牌、弃牌、回合、道具和快捷键统一锁定。仅 Canvas 遮罩不足以拦截现有世界卡牌鼠标输入。
- 打开/关闭指南保留真实选牌，不把高亮 ID 写入选牌模型。

## 验证

接入阶段按用户要求不运行测试，仅进行资源导入与脚本编译。以下为基建阶段已有的验证入口：

- 引擎：`bash Tools/test-poker-engine.sh HandGuideTests SingleChapterScoringTests SingleChapterContentTests SingleChapterEnemyRulesTests AdventureEconomyTests`
- Unity EditMode：`Poker.BattlePresentation.Tests.BattleHandGuideTextTests` 与 `CardArt.Tests.CardBreathingOutlineTests`。
- Shader 测试实际绘制到 RenderTexture，检查外边缘可见、中心透明、零脉冲消失；同时检查共享材质/原卡属性块未被修改和资源释放。

## 问号图标生成记录

- 使用内置 `image_gen` 编辑模式，透明背景；保留输出 Alpha。
- 最终文件：`Assets/Art/UI/BattleConcept/Resources/BattleConcept/Icons/icon_hand_guide.png`。
- 风格参考：同目录的 `icon_draw_pile.png`、`icon_coin.png`、`icon_discard_trash.png`。前三版中的最终版采用现有图标的粗轮廓、红金色和手绘块面。
- 输入 1 为上一版问号（编辑目标）；输入 2–4 仅为风格参考，不修改这些现有资源。

最终提示词：

```text
Edit image 1, the question-mark icon, to match the exact existing game HUD icon family in reference images 2, 3 and 4. Images 2/3/4 are STYLE REFERENCES ONLY, not edit targets: keep their bold black contour, angular hand-painted facets, warm muted gold and ivory highlights, deep brown-black shadows, and restrained printed brush texture. The user specifically wants the SAME style as these other icons. Match their Hades-inspired 2D illustrated treatment: graphic hard-edged shading, modest painted wear, absolutely not realistic sculpted 3D metal or stone. A single large easy-to-read ivory/gold '?' on a small dark red-brown medallion with a simple gold rim. Simplify the excessive ornamental compass points from image 1, no extra symbols, no orange gem decorations. Balanced compact icon, frontal view, transparent outside silhouette, readable at 48px next to the book deck icon in image 2. No words, no other objects, no UI panel, no realistic grain, no fine surface crackle, no checkerboard. Deliver only the final isolated question-mark icon.
```
