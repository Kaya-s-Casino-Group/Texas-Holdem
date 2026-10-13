# Card Art 约定

## 可被现有工具扫描的原始图

| 项目 | 约定 |
| --- | --- |
| 原图 | 每牌一张 1024×1536 PNG，2:3，RGB/RGBA，完整不透明纸牌 |
| 方向 | 正视竖牌，正常朝上；没有透视、场景背景或相机光照 |
| 整套 | 55 张：52 张 A–K、`joker_big`、`joker_small`、`card_back` |
| 文件名 / ID | 小写，花色 + 下划线 + 牌值；文件名去 `.png` 即图集 ID |
| 花色 | `spades`、`hearts`、`clubs`、`diamonds` |
| 牌值 | `a`、`02` … `09`、`10`、`j`、`q`、`k` |
| 示例 | `spades_a.png`、`hearts_02.png`、`clubs_10.png`、`diamonds_k.png` |
| 特殊牌 | `card_back.png`、`joker_big.png`、`joker_small.png` |
| 角标 | 左上正常朝上，右下旋转 180°；牌值和花色必须与文件名相符 |
| J/Q/K | 默认逐像素满足 180° 对称；拼接构图随本次风格设计 |
| 留边 | 完整画布就是牌面；图集 extrude 留边由工具生成，原图不添加 |

工具还接受 JPG、别名和变体后缀，但本 skill 统一输出上表的标准 PNG；同一 ID 只留一个文件。数字 2–10 的中央大花色数与牌值相符，角标中的小花色不计入中央数量。别把数字牌的布局变成无法辨认数量的装饰插画。

Atlas Builder 自动从左到右、从上到下排列：卡背 → 黑桃 A–K → 红桃 A–K → 梅花 A–K → 方块 A–K → 大王 → 小王。部分牌组只排列存在且勾选的牌，不预留缺牌空位。skill 不计算索引、牌位矩形和 UV，也不需要按空间位置预排列原图。

## 三种尺寸与采样的区别

1. **源文件**：1024×1536。校验与图集构建读取原始文件。
2. **单图 Unity 导入纹理**：可被 importer 或平台设置缩小；这是导入结果，不是原图规范。不得用导入尺寸替代源文件尺寸。
3. **atlas 牌位**：工具默认逻辑单牌 128×192、扩展留边 4px；整个网格 Fit 后实际牌位像素数改变。列数与布局由工具配置决定，不影响本 skill 的原图尺寸。

交接给现有工具的像素采样要求：Nearest 重采样、Point 纹理采样、Clamp、sRGB=true、mipmaps=false、Uncompressed、anisoLevel=1、NPOT Scale=None。Nearest 是图集生成算法，Point 是运行时采样，不要混为一项。

Atlas UV 原点在左下；`xy=offset, zw=scale`，不含扩展留边。Shader 将采样边界向内限制半个 texel。生成原图时保持视觉朝上即可；不要为 UV 约定预先上下翻转图。PNG 本身不能设置 Unity Filter/Wrap 等导入属性，配置文本只是交接说明，后续由工具应用。

## 每次请求的视觉基准

风格、主题、角色、底色、纹理、笔触、边框和装饰由本次用户要求、会话中明确选定的风格或用户指定参考决定。仅在用户要求延续已有牌组时读取该牌组的美术参考；不自动把项目中的牌组当作新风格的基准。

同套保持角标尺度、色盘和视觉语言一致，大小 Joker 能清晰区分，卡背与牌面协调。J/Q/K 的旋转对称约定不指定水平或斜线接缝，也不限定插画媒介。Nearest/Point 是采样要求，不意味着原图必须是像素画。

运行时镭射/火漆/红叉/tint 属于独立渲染参数与贴图，不加入基础牌面。

## 配置文本

`cards.json` 是本 skill 的 **source manifest**：包含 sourceSize、aspectRatio、sampleContract、原图相对路径、ID、SHA-256、对称检查结果和 style 文本。它没有 atlas 索引或 UV，不是 `CardAtlasRecipe.asset` 或 `CardAtlasManifest`；现有工具扫描 `originals/` 即可，配置供人工/后续 agent 读取。

## 项目依据与更新位置

在目标项目存在时检查以下文件；用户明确要求或实现有变化时更新约定，不把旧导出布局硬编码成原图生成规则。

- `Tools/CardAtlas.md`：扫描、命名、原图校验和导出规则。
- `Assets/CardArt/Editor/CardAtlasRecipe.cs`：图集默认设置。
- `Assets/CardArt/Editor/CardImageValidator.cs`：原始文件 1024×1536、比例容差默认 0.5%。生成结果使用精确尺寸，不以容差掩盖错误。
- `Assets/CardArt/Editor/CardAtlasBuilder.cs`：一次原图重采样、整组变换及扩展留边。
- `Assets/Editor/CardTextureImporter.cs`：单图导入设置与作用路径；不作为美术风格依据。
- `Assets/Art/CardAtlases/` 中对应 atlas 的 JSON：需要核对现有图集时查看实际配置。
- `Assets/CardArt/Shaders/CardSurfaceCommon.hlsl`：图集采样边界和 UV 语义。
