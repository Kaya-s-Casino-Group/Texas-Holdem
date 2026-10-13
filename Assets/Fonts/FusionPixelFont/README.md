# 缝合像素字体 / Fusion Pixel Font

项目使用 [TakWolf/fusion-pixel-font](https://github.com/TakWolf/fusion-pixel-font) 的 [2026.09.25 稳定版](https://github.com/TakWolf/fusion-pixel-font/releases/tag/2026.09.25)。本目录提供 Unity 可导入的 TrueType 字体，采用简体中文（`zh-Hans`）字形版本，同时包含拉丁字母、数字和其他受支持字符。

| 像素规格 | 等宽模式 | 比例模式 |
| --- | --- | --- |
| 8 px | [fusion-pixel-8px-monospaced-zh_hans.ttf](fusion-pixel-8px-monospaced-zh_hans.ttf) | [fusion-pixel-8px-proportional-zh_hans.ttf](fusion-pixel-8px-proportional-zh_hans.ttf) |
| 10 px | [fusion-pixel-10px-monospaced-zh_hans.ttf](fusion-pixel-10px-monospaced-zh_hans.ttf) | [fusion-pixel-10px-proportional-zh_hans.ttf](fusion-pixel-10px-proportional-zh_hans.ttf) |
| 12 px | [fusion-pixel-12px-monospaced-zh_hans.ttf](fusion-pixel-12px-monospaced-zh_hans.ttf) | [fusion-pixel-12px-proportional-zh_hans.ttf](fusion-pixel-12px-proportional-zh_hans.ttf) |

## 使用

普通界面文本可先使用 `fusion-pixel-12px-proportional-zh_hans.ttf`；需要对齐数字或列时可选择等宽版本。Unity 的 Font 字段可直接引用这些 TTF。使用 TextMeshPro 时，先以选定 TTF 创建 TMP Font Asset，再赋给文本组件。

卡牌悬停信息 Prefab 使用 `TMP/FusionPixel12pxProportional.asset`：12px RASTER_HINTED 位图、Point 采样、动态字符图集，显式引用比例模式的 12px TTF。图集已预填 ASCII、四种花色名称、“筹码”与原生 `♠ ♥ ♣ ♦` 字形，支持继续补充文字；材质与纹理作为字体 Asset 子资源保存。接入说明见 [卡牌提示资源](../../CardPresentation/UI/README.md)。尚未修改全局默认字体或场景。像素文字的清晰程度还取决于字体图集、采样模式、Canvas 缩放及最终屏幕像素对齐，应在实际游戏分辨率下检查。

## 来源与更新

上游 Git 仓库已浅克隆到项目的 `.cache/upstream/fusion-pixel-font/`，固定在标签 `2026.09.25`，提交 `6c88c8ec0f16f05e06663890a043ecbc81d448ae`。原始下载包和解压依赖位于 `.cache/`，由项目 `.gitignore` 排除。

字体取自同版本官方 `ttf.woff2` 发布包，验证官方 SHA-256 后使用 FontTools 还原为 TTF；保留字体名称、字形、字符映射和字宽。[SOURCE.json](SOURCE.json) 记录下载地址、包和字体校验值、工具版本与验证结果。更新时应同时替换字体、来源记录和对应许可证，并保留现有 `.meta` 的 GUID。

## 许可证

字体采用 **SIL Open Font License 1.1**。官方发布包的 [OFL.txt](OFL.txt) 和 [LICENSES/](LICENSES/) 中各上游字体的版权与授权说明已完整保留。随游戏分发字体时，应一并附带这些授权说明；具体条款以原文为准。
