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
