# 通用数字 UI

`UIFoundation.Runtime` 仅引用 Unity UI / TextMeshPro 和 DOTween，不引用 PokerEngine、战局、卡牌或计分模块。

将 `RollingNumberText` 挂到 TextMeshProUGUI 上，调用 `SetTarget(decimal number)` 更新目标。整数、负数和小数均可传入；`SnapTo(decimal number)` 立即显示。`Configure(format, seconds)` 配置数值格式与动画时长，默认 `N0`、0.25 秒。格式化使用 invariant culture。

```csharp
number.SetTarget(120);
number.SetTarget(80); // 正在滚动时，从当前值继续减到 80
number.SetTarget(1.25m);
```

数字沿独立 OutCubic 曲线增减，重复提交相同 target 不重启动画。数值由 decimal 保存/插值，避免大整数先转 float/double 后丢失精度；结束时精确设置为输入目标。UI 使用 unscaled time，禁用时结束到最新 target 并清理自身 Tween。`Advance(seconds)` 是单组件时钟入口，不驱动全局 DOTween。

`RollingNumberValue` 只负责数字状态、重新定向和归一化进度；无 Unity 或时钟 API。游戏负责产生要显示的数值，组件不读状态、不判牌型、不计算筹码/倍率/得分，也不提交任何命令。

## 屏幕安全区

`PokerDisplaySettings` 在独立桌面 Player 首次启动时使用 2340×1080 窗口；游戏设置提供 2340×1080、1920×1080、1560×720、1280×720 四档，应用后保存并在下次启动恢复。窗口禁用拖拽缩放和全屏切换，通过设置修改分辨率。Editor 不强制分辨率，方便预览不同设备。iOS / Android 保持原生分辨率，只允许左右横屏，继续使用 CanvasScaler 与下述安全区适配，不执行桌面的 SetResolution。

`ScreenSafeArea.ScreenRect(Canvas)` 与 `CameraRect(Camera)` 将 `Screen.safeArea` 按系统屏幕尺寸归一化，再换算到实际显示尺寸并与当前 viewport 相交。Device Simulator、Editor 预览相机和不同朝向的离屏测试可能返回不同的屏幕与输出尺寸，不能直接混用像素坐标。分屏相机由 `pixelRect / rect` 得到完整显示尺寸；RenderTexture 相机使用自己的 viewport，不继承设备刘海区域。

`CalculateDisplaySafeRect` 提供相同换算的纯计算入口。卡牌世界空间 tooltip 使用 `CameraRect` 限制位置，Canvas UI 使用 `ScreenRect`；移动端额外边距和 tooltip 自身留白仍各自生效。

保存过布局的战斗 UI 通过 `ScreenSafeArea.SetReferenceSize` 在安全区中居中、等比适配设计画布，锚点和固定像素偏移使用同一坐标系。手牌与人物立绘采用相同参考宽高比，背景继续铺满屏幕。敌人工作台布局则以各条目的 `referenceAspect` 同时换算位置和大小，HUD 以 `referenceHudHeight` 换算偏移与尺寸，确保 Boss 本体和各只手在切换宽高比时仍保持组合关系。
