# 普通 UI 字体

`dev` 使用 **Noto Sans CJK SC Regular** 显示中文、英文、数字与扑克牌花色符号。字体来自 [Noto 官方仓库](https://github.com/notofonts/noto-cjk/tree/main/Sans/OTF/SimplifiedChinese)，按 [SIL Open Font License 1.1](OFL.txt) 随工程分发；下载地址和 SHA-256 见 [SOURCE.json](SOURCE.json)。

`TMP/NotoSansCJKSCRegular.asset` 使用 TextMeshPro Mobile Distance Field 材质、64px SDFAA 采样、8px padding、2048 × 2048 动态图集和 Bilinear/Clamp 过滤，支持运行时补充中文字符与多图集。字号由各 UI 控件设置，无须对齐像素字体的整数倍。

此资源也是 TMP 全局默认字体。迁移保留原字体资源 GUID、材质和首张图集的本地 ID，场景与五个 UI 预制体继续使用原有引用。卡牌提示的文本框高度按新字体行高调整，避免 Ellipsis 将整行裁掉。`Tools/BattleDemo/Use Regular UI Font` 可应用默认字体并检查配置，`Validate Regular UI Font` 检查场景和 UI 预制体绑定。

重新生成卡牌提示、牌库、手牌操作面板或对局界面的编辑器工具均使用该路径。卡牌图集中的手绘牌面文字属于贴图，不由 TMP 字体控制。

2026-10-20 已验证 220 处场景/预制体文字绑定、四种花色提示截图及对局运行画面；`CardPresentation.Tests` 的 261 项 EditMode 回归全部通过，无缺字警告。结果与预览位于 `Library/RegularUIFont/`。
