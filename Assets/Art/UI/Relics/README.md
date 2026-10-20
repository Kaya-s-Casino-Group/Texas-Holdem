# 遗物图标

`Resources/BattleRelics.png` 是 768 × 576 的透明图集，4 列 × 3 行。每格由 `BattleRelicArt.Get(definitionId)` 读取；规则 XML 的 ID、中文名、持有次数和冷却仍是独立数据。未知遗物保留文字显示。

从左到右、从上到下：10001 同花倍率、10002 急救药水、11002 心连心、11003 存折、11004 一串手雷、12001 反甲、13001 债券、13002 致命一击、13003 牌型倍率、13004 替死鬼、14001 六面骰、15001 俄罗斯转盘。

制作方式：内置 imagegen 生成与风格调整，sips 缩小整张图集。原始大图不入库。

最终生成提示词：

> Redraw the complete 12-relic sprite sheet in the graphic hand painted visual style of Hades game by Supergiant: forceful thick black ink silhouette outlines, angular expressive shapes, large clean hard-edged shadow planes, sharp controlled rim highlights, confident sparse brush strokes, deep near-black shadows, vivid crimson and antique gold with small cyan accents. Less shiny photoreal texture, more high contrast comic-like heroic underworld icon art. Preserve the twelve subjects, their order, the 4 columns by 3 rows layout, and transparent background. Each icon fits inside its equal square grid cell with clear padding, no overlaps. Row 1: cards, potion, two hearts, passbook; row 2: grenades, thorn armor, bond, critical dagger; row 3: crowned chips, revival doll, die, roulette cylinder. No text, labels, numbers, borders or UI panels.

切片留白调整：

> Technical sprite-sheet spacing edit: keep the exact twelve objects, style, palette and order. Make every icon fit in the central 65% of its equal cell on a precise 4 × 3 transparent grid. The card fan, crown and dagger must not cross cell boundaries. Center each object with transparent gutters on every side. No grid lines, labels, frames or separators.
