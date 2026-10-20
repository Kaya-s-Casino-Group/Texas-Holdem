# 卡牌渲染

当前主 Shader 为 `Assets/GameAssets/CardArt/Shaders/CardUnlit.shader`，名称 `CardArt/Card Unlit Forward`。从旧 Shader 重命名时保留 GUID，现有 Card / FourBeastsCard / ClassicCard Prefab 和材质引用继续有效。

只有 `UniversalForwardOnly`（ForwardUnlit）与 `DepthOnly` 两个 Pass；无 GBuffer、光照、阴影、法线贴图、DepthNormals。UV0 表示局部牌面，UV1 区分正面、背面和厚度。正背图集裁剪、边缘色、透明裁切、每实例颜色及 GPU instancing 保留。

正面效果顺序为：镭射 → 火漆 → 红叉。镭射使用 UV 与时间驱动的彩色闪动；火漆保留颜色、位置、旋转、透明度与独立自发光；红叉在最上层。背面和厚度不受这些正面效果影响。Canvas 卡牌使用同样的无光照效果约定，保留 UI 遮罩与裁剪。

保留 FoilPattern、WaxSeal_Paw、RedCross 三张效果图；WaxSeal_Paw_Normal 已归档。导出器接受 `CardSealContract=ColorOverlayV1`，不要求法线输入。旧 normal 参数仅保留为序列化/API 兼容字段，不参与当前导出或渲染。

验证入口：Unity Test Runner 的 CardArt.Tests，以及“工具 / 卡牌美术 / 渲染验证 / 验证蜡封导出与渲染”。后者实测效果启停、旋转、光照不变性、背面隔离及场景恢复。
