# Changelog

项目变更按日期记录；未发布变更保留在 Unreleased。每次修改 PokerEngine 的公共模型或快照契约，同时更新本文件及模块 README。

## [Unreleased]

### 2026-10-17 — 自研脚本与模块资源统一目录

- 自研 C# 集中到 `Assets/Scripts`，按 Rules / Gameplay / Presentation / Infrastructure / Tools 分组，模块内部保留 Runtime / Editor / Tests。
- 模块预制件、配置、Shader、贴图和 Resources 归入 `Assets/GameAssets`；通用美术、音频、场景、管线设置和第三方插件保留原归属。
- 706 个迁移项保留 `.meta` GUID，程序集、类名和命名空间不变；同步工具路径、Shader include、离线测试入口与文档链接。
- Unity 编译通过（0 警告），73 项针对性 EditMode 测试通过；离线规则全量 436 通过、1 项既有计分期望失败。详细限制及分批说明见 [目录迁移验证记录](Tools/Validation/2026-10-17-script-layout.md)。

### 2026-10-17 — 场景、音频及项目设置归档

- 归档现有森林场景、光照贴图、材质与主 Renderer 参数调整，以及战斗示例和卡牌渲染验证场景更新。
- 归档已导入的 Balatro 音效及用途索引、FourBeasts 图集更新和 RunFlow 配置变更。
- 归档 Input System 及 Unity Services 设置的已有变更；AudioReference 原始参考文件和本地产物不纳入版本控制。

### 2026-10-17 — 中文工具菜单与模块说明

- 工具菜单按卡牌战斗、分数挑战、美术与渲染等用途整理为中文入口。
- 同步 CardArt、关卡流程、渲染、模型导入与 iOS 工具说明，明确 SingleBattle 与 DrawBattle 定位。

### 2026-10-17 — 最简 2D 战斗验证与固定前向渲染器

- 新增 Minimal2D 场景，复用 BattleSession、共用 Host 和表现协议，覆盖出牌、弃牌、敌方回合与胜负。
- 新增简单矩形角色与卡牌表现，使用 StandardClassic 图集及符合 CardArt 导出校验的 Unlit Shader。
- 新增独立 Forward2D 程序集与固定 Renderer 资产，MainURP 保留原主 Renderer 并追加 2D 槽位。
- 验证：目录迁移前相关 128 项 EditMode 测试通过，Unity 编译无警告。

### 2026-10-17 — 场景接入与卡牌交互职责分离

- SingleBattle 共享配置、Hosting 与 Scene3D 表现分模块组织，场景 Host 继承 BaseBattleHost。
- CardInteraction 以纯身份数据提交选牌命令，CardMatchHost 保留牌桌镜像与本地交互；DrawBattle 接入同一命令契约。
- CardPresentation 按协议、World、Canvas、美术和反馈组织，补齐安全区适配与交互回归。

### 2026-10-17 — 纯 C# 战斗会话与生命周期

- 新增 BattleSession，原子结算玩家命令及后续自动步骤；规则推进独立于动画。
- 新增 BattleHost、BattleSessionPlayback 和 IBattleSessionPresentation，统一会话生命周期、命令提交与表现同步。
- 个人牌型等级纳入玩家规则快照；新增生命周期、会话和表现边界测试。

### 2026-10-16 — 落地特效与卡牌动作统一交接

- 接入 Sword Slash 14 Hit Flashes 及完整渲染依赖，落地实例按卡牌高度 × 0.2 等比缩放，在牌底单次播放整组粒子，使用实际时间回收。
- 新增 ICardActionView / CardActionViewOperation，出牌与弃牌共用完成、取消及失败协议；View 不引用 Actor。
- 恢复弃牌飞出到指定区域，待本批全部离场后才启动 Actor 的盾牌飞入；剩余手牌理牌不参与动作完成判定。当前出牌直接移除并立即完成，后续动画沿用接口。
- 覆盖错峰批次完成、取消与恢复、即时出弃牌、Actor 交接，以及实际特效底部位置、缩放和单次播放的专项验证。

### 2026-10-16 — 弃牌盾牌与卡牌反馈表现

- Actor 总表现入口组合模型、盾牌与反馈，新增获得格挡、格挡受击和清空格挡协议；部分格挡与角色受击并行等待。
- 弃牌按批次保存实际牌面与格挡，DOTween 错峰飞入角色前方圆弧，按累计格挡消耗向上取整逐张完整溶解。
- 共享图集与 Instancing 的 URP 延迟渲染盾牌 Shader，Perlin + 高度渐变 alpha clip 和自发光边缘。
- 圆弧起点、半径、朝向和角度可调；卡牌高度锁定图集宽高比，开放三轴随机角度范围和朝外概率，落地特效锚定卡牌底部。
- 移除 SingleBattle 的 PlayedZone 表现与飞行路由，保留规则结算；补齐理牌音效、Input System 手机/手柄短震动，按每次实际发声触发一次。
- 更新 SingleBattleDemoNew 场景、相关资源、配置说明与专项测试；设备实际触感仍需真机调校。

### 2026-10-16 — 单局数值中文集中配置窗口

- 新增 `Tools/Poker/战局/单局数值配置`，从当前活动场景的实际战局与角色绑定读取配置，按总览、公共规则、玩家、敌人分区。
- 集中编辑公共计分基础、个人牌型等级、角色预算及独立伤害/收益/目标策略资产；显示资产路径、共享影响与下次开战生效提示。
- 支持撤销、配置构建校验和仅保存当前战局关联的配置资产；运行期间只读，不修改场景阵容或执行对局。
- 简单静态检查与 Unity 脚本编译通过（0 警告），菜单窗口已打开、控制台无错误；未进入 Play 或运行动态测试。

### 2026-10-15 — 个人牌型等级归属玩家

- PlayerBattleProfile 新增逐牌型等级表，BuildRules 生成不可变 BattleHandLevels 并由玩家 BattlePlayerRules 持有；校验类别、重复和正整数等级，缺省牌型兼容为 1 级。
- CardDuelEngine.PreviewHand 按判型结果读取动作玩家自己的等级，候选 UI、伤害预览与正式 Play 共用；移除单局计分中固定等级 1 的参数。
- 公共 ScoreProfile 保留基础筹码/倍率、每级增长与单牌计分规则；SingleBattlePlayer / DemoPlayer 显式配置各牌型默认 1 级，既有平衡不变。
- 仅做静态检查与脚本编译，未进入 Play 或运行动态测试；战斗内升级及 Run 等级自动带入尚未接入。

### 2026-10-15 — 首次开局等待牌区启用

- 将 SingleBattleHost 的首次自动开局从 OnEnable 移到 Start，避免较早的 Host 初始化遇到尚未激活的 HandZoneView，导致开局发牌走立即摆放分支。
- 保留空手牌绑定后提交 Start、按玩家配置发牌与落位后自动理牌；已有对局重新启用仍恢复状态，不重复开局。
- 仅做静态检查和脚本编译，未进入 Play 验证视觉效果。

### 2026-10-15 — 森林遗迹场景与光照资源归档

- 纳入森林遗迹环境、手绘 Deferred 材质、Meta Pass、植被预制件和优化后的 Lightmap UV1 缓存；原贴图 UV 与雕像 VAT 保持不变。
- ForestRuins 与 SingleBattleDemo 的 Lighting Settings、LightingData 和 lightmap 分别归档到同名场景目录；清理未引用的默认设置，森林场景独立重烘焙。
- 光照图集从 1024² 收紧到 512²，有效 UV 覆盖约 41.5%；缓存生成脚本纳入 Tools/ForestRuins，场景烘焙工具按当前场景选择独立设置。

### 2026-10-15 — 高度雾衰减曲线

- 新增 Height Falloff 选项：线性、平滑、贴地浓雾、高处缓降及自定义 AnimationCurve；默认线性保持原画面。归一化高度控制密度倍率，限制 0–1 并固定上下边界。
- 非线性曲线按需缓存密度及匹配的累计积分，纯高度雾无需新增 ray march；动态 3D 噪声与体积光共用同一高度分布，曲线修改时刷新缓存，释放 Feature 时回收。
- 渲染模块 33/33 测试通过；GPU 检查覆盖五种分布的密度/积分、噪声叠加和共享透射率，自定义曲线下正交视角与地面裁切通过。

### 2026-10-15 — 高度雾与体积光共享动态 3D 密度纹理

- 高度雾新增可选 Texture3D、强度、世界平铺尺寸、偏移、移动速度和共享步数，默认接入现有 32³ R8 周期噪声；空纹理、关闭或零强度保留解析路径。
- 世界空间连续平移，独立 Repeat/Linear sampler、显式 LOD 0、double 时间偏移周期回绕；固定中点采样，不逐帧生成/上传纹理。
- 纹理模式一次 ray march 同时积分透射率和散射，复用体积光 RT 的 alpha 存储 T，再统一合成。双边滤波仅影响 RGB；无参与灯光时自动回退独立纹理雾。
- 渲染模块 27/27 测试通过；新增 GPU 检查通过三轴周期、接缝、平移、常量纹理、回退、共享积分和正交/地面裁切，原高度雾及主光/点光/聚光阴影 GPU 回归通过。

### 2026-10-15 — 场景萤火虫区域

- 新增 `FireflyVolume`、创建菜单及 Prefab：局部 Box 内随机游动的不透明 HDR 发光球，每只附带点光源和 `VolumetricLight`，区域统一控制外观、灯光、散射与运动参数，支持编辑器预览。
- 同场景 Mesh Renderer 包围盒避障，无 Collider 也生效，检测整个移动线段；尊重球体半径、旋转和缩放后的区域边界，空间不足不会强行生成。生成子物体不序列化，禁用后清理。
- 4 项测试通过，含实际 GPU HDR 保留、前后景深度遮挡检查；默认使用现有 Deferred / 高度雾 / 体积光 / Bloom 流程，不新增 Renderer Feature。

### 2026-10-15 — 离线 32³ 雾噪声生成工具

- 新增 Editor-only 3D 噪声生成窗口，支持种子、频率、层数、强度递减、输出路径及 Z 切片预览；周期性三维 Value Noise 多层离线合成，限制最高频率以避免欠采样。
- 生成默认 `FogNoise32.asset`：32³ R8 线性、三轴 Repeat、无 mip，体素数据 32 KiB。重烘焙保持 GUID/file ID，参数随 `.meta` 保存；本轮不改变运行时高度雾和体积光。
- 4 项 EditMode 测试覆盖确定性、周期性、频率约束及 Texture3D 持久化；默认八切片预览位于 `Library/VolumeNoiseValidation/FogNoise32Slices.png`。

### 2026-10-15 — 体积光命名整理与独立 Bloom

- 将体积光 Feature、灯光组件和 Shader 改为 `VolumetricLightFeature`、`VolumetricLight`、`VolumetricLight.shader`，保留 GUID、Renderer 子资产和场景灯光引用，补充 MovedFrom；主光与额外灯光的启用规则不变。
- 新增独立 BloomFeature：柔和阈值、高光提取、多级降采样/Tent 升采样、HDR 上限、扩散、强度和颜色；RGB 加法保留 alpha，强度为零时跳过，缓冲随尺寸/层数变化回收。
- 接入定制主 Renderer：SSAO → 高度雾 → 体积光 → Bloom → 复古处理 → Point 放大 → UI；不修改嵌入式 URP 和 UI Renderer，不重置已有灯光与效果参数。安装菜单维持推荐顺序。
- 渲染模块 EditMode 17/17 通过；Bloom GPU 验证覆盖 HDR 光晕、阈值、颜色、零强度、alpha、单层、1×1、奇数尺寸、横/竖屏、原生尺寸及 Point 放大。重命名后高度雾/体积光 GPU 回归通过，无 Shader 错误；未执行 Player 构建。

### 2026-10-15 — 高度雾与依赖高度雾的体积光

- 拉取 LifeSugar/RayMarchingGodRay（`9bcc726`）至 `.cache/RayMarchingGodRay`，保留 MIT 授权，将原单一效果拆为 `HeightFogFeature` 与显式依赖它的 `VolumetricLightFeature`。
- 高度雾独立控制基础高度、最大高度、地面裁切、密度、消光距离与雾色；修复基础高度无效和关闭地面后密度消失。共用解析高度积分和透视/正交深度重建；雾只施加一次消光，体积光仅添加散射。
- 迁移主光/点光/聚光、阴影、Cookie 和 Cornette–Shanks 相位函数，主光由 Feature 控制，额外灯光必须挂载并启用 `VolumetricLight` 才参与散射。使用固定采样噪声、内部尺寸 RGBA16F 缓冲与深度引导滤波。
- 安装到定制 MainDeferredRenderer：SSAO → 高度雾 → 体积光 → 复古处理 → Point 放大 → 原生 UI。保留嵌入式 URP 与已有渲染参数；跳过 Overlay / XR / Preview / Reflection，禁用高度雾时体积光自动停用。
- 新增安装、GPU 验证及当前场景对照捕获菜单，更新 Rendering README。EditMode 渲染模块 14/14 通过；Metal GPU 验证覆盖横屏/竖屏/正交、540 行及原生尺寸、Point 像素块、alpha、依赖开关、零密度、三类灯光与阴影。对照图与报告位于 `Library/HeightFogValidation`；未执行 Player 构建。

### 2026-10-15 — 目标选择策略归属角色

- 新增 BattleActorTargetRules，攻击及友方策略随角色 Setup 进入战局，玩家扣费、结束回合和恢复能量均保留快照；移除 Host / CardDuelEngine / TeamBattleEngine 全局策略入口。
- BattleActorProfile 提供独立攻击目标资产槽，PlayerBattleProfile 提供友方目标资产槽。内置首个、最低生命、最高生命、全部存活对手及显式目标开关；同生命按阵容顺序选择，默认保持旧行为。
- SingleBattle / Demo 的玩家、敌人分别绑定默认策略资产；攻击目标选择读取行动来源角色配置，意图预览和正式执行继续共用同一条规则路径。
- 公共目标校验同时检查请求与输出的阵营、存活、数量和重复，校验行动来源，非法请求无法被忽略请求的策略绕过。更新既有调用点与模块文档。
- 仅做静态检查与脚本编译，不运行动态测试或进入 Play。

### 2026-10-15 — 敌人意图读取规则层行动预览

- 新增 TeamBattleEngine.PreviewEnemyAction 与 EnemyActionPreview，以不可变状态复用正式 Execute，逐项包含目标选择、攻击修正、承伤修正、格挡和生命结算。
- 玩家阶段按立即结束回合预演，敌人阶段按剩余队列预演；前序敌人的格挡消耗、击杀和目标变化影响后续意图，已行动、死亡和终局隐藏。
- EnemyBattleActor 只包装规则预览；Billboard 读取逐目标 SettledDamage，显示两层修正后的格挡前攻击值，多人时标明目标并分行，面板与运行时 Gizmo 同步尺寸。失败显示“意图不可用”，不回退基础伤害。
- 更新原有调用点与文档；仅做静态检查与脚本编译，不运行动态测试或进入 Play。

### 2026-10-14 — 出牌伤害归属玩家与公共规则清理

- 将 ScoredHandDamageProfile 槽位从 BattleRulesProfile 移至 PlayerBattleProfile，原 SingleBattleRules / DemoBattleRules 的资产引用迁入对应玩家配置；默认配置打开入口改为读取单局玩家实际绑定的资产。
- BattlePlayerRules.DamagePolicy 保存开战快照，TeamBattle 按出牌者构建基础伤害；预览与正式动作共用，怪物仍直接提供基础攻击。Unity 玩家配置必须绑定资产；纯 C# 默认等分数伤害。
- BattleRules 移除出牌伤害与旧全局攻击/承伤回退，仅保留版本、回合上限和阵营格挡清除时机；角色修正一律来自各自 Setup。旧 ScoredDuelDefinition 适配显式转入默认玩家规则，旧 standalone 快照不变。
- 更新既有测试调用点和模块文档，记录目标策略、敌人意图预览、个人计分成长及 HUD 职责的后续边界。
- 仅做静态检查与脚本编译，不运行动态测试或进入 Play。

### 2026-10-14 — 弃牌收益移至玩家配置

- 将 DiscardRewardProfile 槽位从 BattleRulesProfile 移至 PlayerBattleProfile；公共 BattleRules 同步移除收益字段与构造参数，由每位玩家的 BattlePlayerRules 持有不可变收益规则，空槽位为无收益。
- TeamBattle 按弃牌者读取收益规则，预览和正式执行共用；选择队友作为目标不会改用队友的规则。已有玩家规则快照随扣费、结束回合与能量恢复保留。
- 单局每张弃牌 8 格挡、多人无收益的原资产引用分别迁至 SingleBattlePlayer / DemoPlayer；更新默认资产打开入口、文档及既有测试调用点。
- 仅做静态引用检查与脚本编译，不运行动态测试或进入 Play。

### 2026-10-14 — 角色独立攻击与承伤修正配置

- BattleActorProfile 新增两个独立资产槽位，玩家与怪物均可绑定 AttackDamageModifierProfile / IncomingDamageModifierProfile；配置归属角色，不加入公共战局配置。
- 具体倍率/固定增减值资产生成不可变 ScaledAttackDamageModifier / ScaledIncomingDamageModifier；输出为 floor(max(0, 输入 × 倍率 + 固定值))，默认 1 倍、0 增减，空槽位原值通过。
- BattlePlayerSetup / BattleEnemySetup 携带 BattleActorDamageRules 快照；攻击阶段读取来源角色，承伤阶段读取实际目标角色。快照随扣费、结束回合及恢复能量保留，预览复用同一规则链。
- SingleBattlePlayer / SingleBattleEnemy 分别绑定四份独立中性资产；多人旧配置空槽位保持原行为。BattleRules 旧代码注入仅保留为未提供角色规则时的兼容回退，不叠加角色修正；Combat/旧 ScoredDuel 存档格式不变。
- 仅做静态检查与脚本编译，不进入 Play 或运行动态测试。

### 2026-10-14 — 左侧候选牌型与通用数字滚动 UI

- 左侧候选面板显示当前观察玩家的选牌牌型、选中/计分牌数、整数筹码 × 整数倍率，选牌变化时更新 target。
- CardDuelEngine.PreviewHand 与正式 Play 共用判型计分，不进入费用、目标、伤害或发牌策略，不写状态；快照格式不变。
- 筹码与倍率各自使用白色矩形背景、整数格式和居中对齐，不显示结果行。
- 独立 UIFoundation.Runtime 只接收数字，支持增减、途中重新定向、重复 target 保持动画、decimal 精度及最终值准确落位；不引用玩法模块。
- 按用户要求仅执行静态检查与脚本编译，不运行动态测试或进入 Play。

### 2026-10-14 — 手牌选中上限与统一拒绝反馈

- HandZoneView 新增最多选中卡牌数，默认 5；鼠标及 CardZonePlayHost 的新增选择共用上限，取消选择仍可执行。
- 超限、卡牌禁用、权威阶段锁定及可见飞行牌共用小幅上升/回弹反馈；默认独立空音效槽位，空值静默。反馈不写选中状态，重复点击不累加高度，刷新不中断，成功选择/收牌/禁用清理。
- Inspector、实战牌区工具与 View Prefab 导出支持新配置。
- 验证：42 项专项测试通过；实战鼠标路径验证 5 张上限、第 6 张回弹/静默、取消选择释放名额及演出阶段拒绝通过，编译无警告/错误。

### 2026-10-13 — 世界空间敌人意图 Billboard

- 每个怪物的意图改用独立世界空间 Canvas，挂在怪物根节点下、跟随模型底部，并朝向摄像机；固定世界尺寸，保留小型只读攻击标签。
- HUD 提供世界尺寸和偏移配置，死亡/终局/禁用隐藏，HUD 销毁时清理独立标签；移除屏幕坐标投影定位。
- Scene Gizmo 增加面板范围、模型底部锚点/偏移连线与朝向箭头，Edit/Play 共用实际标签的尺寸、位置和 Billboard 计算，仅在 Editor 编译。
- 验证：Play 定向检查世界 Canvas、怪物归属、相机移动后的世界锚点/尺寸与 Billboard 朝向、禁用/恢复复用及权威状态不变通过；实际截图检查通过。

### 2026-10-13 — 右侧紧凑战局信息层与怪物意图

- 单局/多人 Demo 的战局 HUD 缩为右侧窄信息栏，只显示状态、资源和结算信息；移除顶部/中间的大面板、操作提示及内部计分流程说明，控制按钮和终局信息同步缩小。
- 每个怪物独立显示模型下方的攻击意图，随模型和相机投影更新，死亡/终局/镜头外隐藏；继续只读观察权威状态。
- 增加仅整理现有 HUD 的 Tools 入口，新建 Demo 沿用相同布局。
- 验证：当前场景真实按钮/动画完整流程通过，覆盖回合、能量、牌库、胜负及重开；Game View 已检查右侧布局和模型下方意图，编译与 Console 无错误。

### 2026-10-13 — 发牌后自动理牌

- 保留默认点数理牌，修复战局提前绑定并发牌后，手牌 OnEnable 重复初始化清除动画与待理牌请求的问题。
- 切换观察玩家时自动按当前方式理牌；后续补牌继续沿用点数/花色选择，仅调整显示顺序。
- 验证：36 项角色/卡牌表现测试通过；当前场景 Play 验证首次发牌、重复启用回调、弃牌补牌及下一轮补牌的自动点数排序通过，切换玩家由双玩家编辑模式回归覆盖。

### 2026-10-13 — 独立弃牌收益接口与验算边界

- 新增 IDiscardRewardRule / DiscardRewardContext / DiscardRewardPlan，默认 NoDiscardRewardRule 返回空计划，不计分、不产生额外收益、不改变原有弃牌能耗/次数/补牌。
- 独立 DiscardRewardProfile 配置在开局创建快照，公共规则引用 DemoDiscardReward，缺失引用默认无收益。收益只能作用于已验证的存活友方；当前承接已有格挡/治疗效果，未添加具体生产规则。
- PreviewDiscard 复用正式入口，Host 记录最近实际弃牌；中文「弃牌收益验算」面板读取相同结果，展示卡牌快照、友方目标、费用和收益说明。
- 补充默认无事件、敌方拒绝、非法策略与溢出回滚、预览/提交一致、循环洗牌随机隔离、配置快照和缓存生命周期测试。

### 2026-10-13 — 预留友方目标边界

- 新增独立 IBattleAllyTargetPolicy 与默认 SelfOrRequestedAllyPolicy，Host / CardDuel / TeamBattle 支持开战前注入；目标限定存活己方单位，并验证请求及策略输出。
- 弃牌收益尚未定案，暂不启用牌型换格挡，也不改变现有过牌、能耗和次数逻辑。

### 2026-10-13 — 攻击来源快照与触发依据

- 正式出牌生成不可变 HandAttackInfo / AttackCardSnapshot，明确提交牌、牌型组成牌、实际计分牌三个范围，失效牌不参与逐牌计分但仍可组成牌型。
- AttackContext 由权威流程附加，贯穿攻击修正、目标承伤和 CombatEvent；各阶段共用同一份来源，保留初始伤害，怪物普通攻击不携带出牌快照。Combat 不新增 Scoring/Unity 依赖。
- DamageModification 可携带 DamageTriggerReason（来源、原因、检查范围、关联牌），错误范围引用原子拒绝；中文只读验算面板使用原始快照与实际触发记录，不查当前牌区或另行判型。未实现具体花色/牌型能力。
- 增加来源一致性、失效/杂牌边界、多目标、预览/提交一致性、移牌后快照、记录不可变性与非法引用的定向测试及中文测试入口。

### 2026-10-13 — 得分等额攻击与分阶段伤害修正

- 默认 ScoreDamageProfile 改为 EqualScoreDamageRule，移除除数配置，保留最终得分作为基础伤害；原有 ScoreDamagePolicy 仅保留显式兼容调用。
- BattleRules 可注入 IAttackDamageModifier 与 IIncomingDamageModifier，默认原值通过，玩家与怪物共用。前者在攻击构建后执行，后者在 CombatEngine 内格挡前执行；筹码倍率仍独立属于计分层。
- CombatEvent 携带各阶段真实修正记录，中文只读验算面板展示基础伤害、攻击修正、承伤修正、格挡前伤害及最终生命损失。继续复用正式入口，保留异常/溢出原子拒绝。
- 同步默认资产、Inspector、HUD 提示和定向测试；怪物生命数值未在本次接口调整中重新平衡。

### 2026-10-13 — 出牌伤害接口、独立配置与只读验算

- 新增 IScoredHandDamageRule / ScoredHandDamageContext / DamagePlan，目标策略先给出合法目标，转换仅生成伤害请求；玩家和怪物统一经 CombatEngine 扣格挡与生命。
- 公共战局规则引用独立 ScoredHandDamageProfile，默认 DemoScoreDamage 迁移原有换算参数；开战时生成不可变策略。Host/HUD 不再读取具体转换参数。
- CardDuelTransition 携带正式牌型、计分、伤害转换及 CombatEvent。中文「出牌伤害验算」窗口直接复用 Execute 的纯返回结果，或读取最近真实结算记录，不维护另一套计算公式、不写回战局。
- 定向覆盖多目标转换、异常请求原子拒绝、预览/提交一致性、补牌洗牌随机状态不变及配置快照隔离。

### 2026-10-13 — 独立牌型筹码与倍率配置

- 保留现有牌型识别，将计分配置从 ScoreEngine 拆为纯 C# ScoreProfile / CardPointProfile，默认采用小丑牌基础筹码、倍率与每级成长，计分版本更新为 score.balatro.1。
- 独立 PokerScoring 模块提供中文配置 Inspector 与 Tools 入口，可设置各点数单牌筹码及指定花色/点数覆盖；不把计分筹码放进单位资源或卡牌美术。
- 战局公共规则引用计分资产，开战时冻结配置；实际出牌及手牌/牌库悬停共用权威配置。保留失效牌、杂牌不计分、逐牌加成和重触发语义。
- 保留现有五条兼容项，不新增特殊牌型；新战局等级/Joker 接入范围不变。新增默认表、配置覆盖、溢出、快照隔离与 UI 接入定向测试。

### 多单位战局与配置边界

- 单场战斗支持多玩家对多怪物：共享玩家阶段、个人结束标记、按序怪物队列和整队胜负。
- 每位玩家独立牌组/四区、能量/费用、弃牌次数、手牌上限与补牌策略；目标策略预留多目标接口。
- 单位数值移入玩家/怪物 Profile，公共回合/计分规则独立配置；Host 维护阵容与流程，不集中保存角色数值。
- 新增 TeamBattleDemo 2v3，支持本地切换玩家观察手牌；角色表现仍逐个等待实际完成。


### 2026-10-13 — 项目工具统一到 Tools 菜单

- 将 Poker、卡牌美术、玩法配置及测试面板入口统一移到 Unity 顶栏 Tools 下；测试面板直接从 Tools 打开，已有 Tools 内分组保持原位。
- 同步当前文档入口路径，不改工具逻辑、运行时玩法或场景资源。


### 2026-10-13 — 自由 Shader 组合与玩法状态映射

- CardArtProfile 只保存图集、基础组合及命名的通用 Shader 参数组合；支持数值、HDR 颜色、向量及覆盖/乘算。美术输入中移除游戏状态，保留原 Atlas/Instancing 最小约定；组合与导出检查参数类型及实例声明/读取，自定义参数名可用。
- 玩法侧 CardStateArtBindings 配置效果/禁用条件、组合 ID 与叠加顺序。CardMatchHost 绑定战局映射，Run/战局投影、交互与跨区保留该配置；隐藏牌只用基础组合。现有火漆、镭射、失效与红叉迁移为默认资源，保持原画面与 HDR 参数。
- 场景单牌消费共享材质和独立 MPB，每次从基础组合重新合成；Canvas 保持现有顶点通道适配，对未支持的参数明确报错。Zone 不持有 Shader 样式配置。
- 中文美术工具编辑、校验和预览组合 ID，独立相机渲染两张真实实例，支持可控主光/补光/环境亮度。玩法映射有独立 Inspector 入口及状态检查；测试面板提供对应专项。
- Run DeckEntry 的效果、禁用和来源保留到 CardDuel，禁用牌拒绝出/弃；Core 存档无需新增字段。可挂载测试辅助组件以 UNITY_EDITOR 编译约束隔离，Player 排除测试代码。

### 2026-10-13 — 中文测试面板与 Editor 隔离

- 项目全部测试程序集显式限定 Editor 并关闭自动引用，补齐 CardPresentation.Tests.Support 的平台边界；发布边界测试检查 Player 不包含项目测试程序集。
- 新增统一中文入口“测试面板”，移除原分散的 Poker / Tests 菜单。按编辑模式专项、实战验证与截图分类，显示真实用例数量；全量模块和旧场景验证默认折叠，旧本地测试禁止在实战权威存在时运行。
- 面板提供进度、通过/失败、模式条件提示、停止编辑模式测试及报告定位。进入 Play 或编译时取消队列，不把未执行项记为通过，并释放重载锁；实战报告只接受本次新增结果，避免误用旧报告。
- 顶栏一级菜单直接命名为“测试面板”，仅保留“打开面板”入口。实际点验发牌规则 17 项、Editor 隔离 4 项与实战发牌专项通过，编译零警告/错误，不运行全量回归。

### 2026-10-13 — 牌区验证工具简化为实战观察

- 移除窗口持有的卡牌模型、测试牌堆与抽牌记录、编辑器造牌/改牌/固化/预览命令及牌区创建菜单；不再从启动快照或旧窗口缓存恢复牌区。
- 改为实战卡牌、表现参数、视觉诊断、出牌路径四页。卡牌直接读取 Play 中外部玩法权威绑定的宿主，只读；Edit 仅调整现有 View，Play 在可交互且动画落位时调整表现参数。
- 移除编辑器动画时钟、强制完成过渡与窗口驱动排序。生命周期、切换目标和撤销不清空视觉、不提交卡牌数据；保留仅导出表现设置的 View Prefab 工具。
- HandActionPanel 配置不再调用牌区固化入口，使用现有场景绑定；旧快照 API 保留给资源迁移和隔离测试。
- 针对性验证：窗口/绑定/派生 View 共 18 项通过；真实 Play 检查确认读到当前牌局、表现参数修改不改变牌区状态、切换及关闭不替换视觉，窗口打开时实战发牌动画专项通过。编译零警告/错误。

### 2026-10-13 — 空手初始化、发牌策略与实战牌区数据接管

- CardDuel 创建时 52 张牌全部位于抽牌堆、手牌为 0；新增一次性 Start 和 HasStarted，进入首个玩家回合才自动发 8 张。首回合前拒绝操作，重复发牌拒绝，发牌不消耗能量或弃牌次数。
- SingleBattleHost 先绑定空区，再投影首回合发牌；保留现有牌型/能量和表现等待。
- 新增只读发牌策略接口 ICardDuelDealPolicy，开局、出牌后、弃牌后、玩家回合开始共用安全发牌路径。Hand Size 默认 8，出牌与弃牌分别选择立刻补满或下个玩家回合补满，默认出牌延后、弃牌立刻；固定抽 N 张通过策略注入验证，终局不补牌。
- Demo CardMatchHost 改为 Runtime Managed，清除编辑器测试牌、初始四区、绑定 ID 和牌库 roster；保留 View、输入、相机、锚点和 UI，运行时由战局重新绑定。牌区验证器对这些目标只读，独立验证场景仍可编辑。新增 Prepare Runtime Card Zones 清理菜单。
- 针对性验证：CardDuel 17/17、角色与表现 14/14、旧验证器缓存恢复 1/1 通过，真实 UI 与发牌专项动画均通过；新增 Validate Single Battle Dealing In Play 可仅检查发牌流程。编译零警告/错误，场景保留原有布局与配置。

### 2026-10-13 — 独立动作能耗与能量超上限

- 按用户澄清，调整的是出牌与弃牌，不新增主动抽牌。PlayEnergyCost / DiscardEnergyCost 独立配置、默认各 1；弃牌整次动作计费一次，自动发牌/补牌免费，能量不足时保留卡牌、次数、随机状态和能量。
- MaxEnergy / InitialEnergy 独立配置，PlayerTurnState 允许当前能量超过名义上限而不截断；后续回合恢复到 MaxEnergy。耗尽到 0 自动结束，剩余能量不足出牌但能支付弃牌时保留行动机会，仍可手动结束。
- ScoredDuel 记录付费弃牌并按账本校验能量，定义指纹包含最大/初始能量与两种消耗；规则与快照更新为 scored-duel.4 / schema v4，拒绝旧 v1–v3 快照，Combat 仍为 combat.2，其他模式契约保持。
- Demo Inspector 拆出四项设置，HUD 显示当前/上限及两种消耗，保留旧 energyPerTurn 场景字段到 MaxEnergy 的迁移。已有模型、表现协议和音效空槽位保持。
- 验证：离线 286/286、Unity 引擎 289/289、角色/表现 12/12 通过。真实按钮/动画检查覆盖 5/3 超上限余额、不同动作费用、余额不足的权限、弃牌耗尽后单次敌人行动、回合恢复、胜负与重开；HUD 截图检查通过，编译零警告/错误，退出 Play 无异常。

### 2026-10-13 — 角色音效空槽位与表现层解耦

- 新增可选 IBattlePresentationAudio 与独立 BattleActorAudio，表现组件在攻击/受击/死亡开始时调用；角色逻辑和纯规则层不持有音频资源或 AudioSource。空 Clip 不加载默认音频、不播放、不阻塞动作完成。
- Demo 双方 Model/Audio 各留 Attack、Hit、Death 三个独立声源，Clip 全为空。每类保留音量、音高、延迟、2D/3D 混合、最小/最大距离参数，声源子物体可调整发声位置，保留可选混音器输出；默认不自动播放、不循环。
- 音效与动作时长分离，音效错误不改变战局流程；取消/重开/禁用停止声音及延迟播放，Snap/复位静默。独立声源允许不同动作尾音重叠，重复编辑器连接保留已有音效绑定与配置。
- 验证：基础角色/协议/裂纹 11/11 回归通过；真实对局验证通过，检查双方六个空 Clip、声源无自动播放/循环且全程静默，动作和回合正常完成。编译零警告/错误，退出 Play 无异常，重复连接后场景仍为五组。

### 2026-10-13 — DOTween 攻击/受击与生命裂纹分档

- 当前石像表现固化为 DOTween 前冲/回位攻击、Punch 受击、完整 VAT 死亡。双方在各自 Model 上独立配置方向、时长和振幅；动作的真实完成仍通过表现协议控制流程，取消/重开清理旧 Tween。
- 新增 HealthCrackPresenter，按只读单位生命比例瞬间切换四档：>75% 无裂纹，≤75% / ≤50% / ≤25% 分别使用 0.35 / 0.7 / 1 的裂纹显现和原材质凹陷深度倍数。阈值、显现和深度倍数可在 Inspector 调整；治疗可降档，格挡不改变生命档位。
- IBattleActorPresentation 增加同步快照投影 Synchronize；绑定、恢复、重新启用与效果检查点更新裂纹。Renderer 级 MaterialPropertyBlock 保留 VAT 与其他属性，不修改共享材质，沿用 Fox / Dragon 各自的最大裂纹深度。
- 验证：角色/协议/裂纹 11/11 通过，含阈值边界、恢复、共享材质不变、裂纹与 VAT 属性互不覆盖；真实场景按钮/动画回归通过，覆盖 DOTween、生命分档、死亡 VAT 完成及重开复位。编译零警告/错误，纯规则与存档契约保持。

### 2026-10-13 — 可等待的角色表现协议

- 新增 IBattleActorPresentation（Play / Snap / CancelPresentation / ResetPresentation）、只读 BattlePresentationCue 与一次终态的 BattlePresentationOperation。表现组件自行报告真实完成、取消或失败；迟到回调不能再次完成旧句柄，表现不持有战局命令权限。
- BattleActor 移除模型变换、协程、计时和提前死亡反馈，保留角色身份/状态与协议桥接。独立 BattleActorPresenter 负责预备、攻击、受击和倒伏；VatBattleActorPresenter 按各自 RigidVATPlayer 实际结束状态完成死亡，重开/禁用取消并复位。
- SingleBattleHost 逐阶段等待卡牌落位、攻击、受击、死亡及敌人预备操作；完成后才推进敌人调度、收牌/补牌、输入释放和终局展示，移除固定 scoreHoldSeconds / enemyTellSeconds。规则仍同步提交确定性结果，表现耗时只控制 Unity 流程检查点。
- Demo 双方 Model 挂独立 VAT Presenter，VAT 不再开局自动崩塌。编辑器连接默认实现时保留已选择的其他协议组件。Snap/失败恢复同步到当前权威状态，不重复提交已结算伤害；支持取消旧操作和重开复位。
- 验证：角色/协议 9/9 回归通过；真实场景按钮/动画回归通过，包含能量、连续出牌、自动/主动结束、胜负、VAT 完成及重开进度复位；Unity 编译零警告/错误，退出 Play 无异常。尚未实现 Animator/Timeline 适配器、命中时刻标记或多人并发演出。

### 2026-10-13 — Fox 英雄与 VAT prefab / Demo 层级整理

- 英雄 Model 接入已摆放的 FoxStatue_Collapse_VAT prefab；DragonGargoyle 从 FBX 实例转为独立 DragonGargoyle_Collapse_VAT prefab 实例，保留原有网格、材质、VAT 参数、场景覆盖和对象引用。美术 prefab 不持有战局宿主，逻辑组件仍在 Player / Enemy 根节点。
- 场景整理为 Battle、Card Table、Environment、Cameras、UI 五个根分组；手牌与出牌区重命名为 Hand Zone / Played Zone，旧空敌人定位点移入 Environment / Markers。整理校验原有世界姿态，保存时保留 UI 和相机绑定。
- 新增可重复执行的 Organize Demo Scene 菜单；角色连接与 HUD 配置支持分组后的场景。修复 DrawPilePresenter 在宿主先配置、随后 OnEnable 时重复订阅点击事件的问题，牌库按钮不再因初始化顺序而切换两次。
- SingleBattleHost 停止/复位时显式检查 Unity 对象有效性，退出 Play 或卸载场景时不再访问已销毁的手牌组件。
- 验证：Unity 编译零警告/错误；场景整理与角色连接重复执行后仍为五个根分组，原有世界姿态检查通过。完整按钮/动画回归通过，覆盖出牌、能量、主动/自动结束回合、牌库、胜负及重开；控制台无错误。攻击/受击/死亡反馈目前仍在 BattleActor 内，VAT 未接入战斗事件驱动。

### 2026-10-13 — 玩家/怪物派生组件与模型子物体

- 新增抽象 BattleActor 场景组件，负责 ParticipantId 绑定、读取宿主最新 CombatantState、行动/受击/死亡表现事件及姿态复位。玩家与怪物分别派生 PlayerBattleActor / EnemyBattleActor；共用战斗数据不派生，也不在角色组件中复制生命/格挡。
- PlayerBattleActor 独立接收 ICardMatchCommands、选牌/能量许可与主动结束回合请求；EnemyBattleActor 独立生成攻击意图、检查本方行动许可并向宿主提交固定攻击。SingleBattleHost 保留战局创建、统一命令校验/结算、卡牌镜像与表现顺序，HUD 从绑定角色读取单位状态。
- Demo 创建 Battle Actors/Player 和 Enemy 逻辑根节点；模型放 Model 子树，当前可见的 DragonGargoyle 保留 MeshFilter/MeshRenderer/RigidVATPlayer，并保持重挂载前的世界姿态。角色根不放渲染/VAT组件，基础表现只作用于子物体；玩家 Model 是第一人称视角下的预留人物模型挂点。保留当前场景中用户已调整的布局与美术。
- 新增 Connect Battle Actors 编辑器菜单，连接角色时保留 HUD；按实际可见 Renderer 选取模型，避免将空定位点或隐藏的旧占位模型作为怪物表现。角色槽位、跨场景绑定和根物体作为模型均拒绝，重开重新绑定怪物身份并恢复模型姿态。
- 验证：角色专用 5/5 回归通过（命令所有权、最新快照、目标反馈、单次行动、错误槽位/禁用输入、死亡及重开复位、逻辑根姿态不变）。完整场景按钮/动画验证通过，含能量、连续出牌、自动/主动/空过回合、牌库/排序、胜负与重开；Unity 编译零警告，Game View 已检查。纯引擎规则与快照格式保持，未执行完整 Player/IL2CPP 构建。

### 2026-10-13 — 玩家能量与主动结束回合

- ScoredDuelState 新增独立 PlayerTurnState（MaxEnergy / Energy），定义配置每回合能量和出牌消耗；玩家/怪物共用 CombatantState 保持无筹码/能量。Demo 默认每回合 3 点能量、每次出牌消耗 1 点，弃牌仍使用整场次数预算并不消耗能量。
- 玩家可以同回合连续出牌，能量不足以继续出牌自动进入怪物阶段；EndPlayerTurn 可主动放弃剩余能量，支持零次出牌空过。怪物阶段只攻击一次，战斗继续时恢复全部能量；击杀优先于回合结束，不再反击。
- 完成回合数独立于出牌次数；新增出牌/主动结束/怪物行动的 Actions 账本，ScoredDuel.Revision 独立于 Combat.Revision，恢复时重放核对生命、能量、回合、阶段和版本。ScoredDuelSnapshot 升级 schema v3 / scored-duel.3，v1/v2 明确拒绝；Combat 仍为 combat.2，既有 Draw/Run 格式保持。
- CardDuel 协调器增加 EndTurn 与 CollectPlayed 检查点：每次计分动画结束后清理出牌区再开放下一手牌，同回合不自动补牌；怪物行动后补满手牌，未打出的牌保留。手牌打空仍可结束回合，动画/计分检查点、怪物阶段及终局拒绝重复输入。
- SingleBattleDemo 已保存能量显示和“结束回合”按钮；SingleBattleHost 串联多次出牌与手动/自动结束的动画，按钮在开局发牌/本地理牌落位后刷新可用性。玩家能量上限和单次出牌消耗可在 Inspector 配置。
- 验证：新增 10 项规则回归；离线 282/282、Unity Editor 285/285 通过，Unity Test Runner 玩家回合专项 7/7 通过，编译零警告。真实场景按钮/动画覆盖开局空过、能量扣除、同回合连续出牌、自动/主动/零出牌回合结束、单次反击、能量恢复、补牌、重复点击、牌库/排序、胜负和重开；Game View 已检查，控制台无错误。未执行完整 Player/IL2CPP 构建。

### 2026-10-13 — 独立单场生命战 Demo

- 将当前 CardRenderingValidation 场景（含当时未保存布置）另存为 Assets/Scenes/SingleBattleDemo.unity，原场景文件未覆盖。专用场景移除 DrawBattleHost，使用独立 SingleBattleHost，保留原有桌面、相机、四牌区交互和美术；不修改 Build Settings。
- Composition 新增 CardDuelEngine/CardDuelState，统一拥有 CardTableState、ScoredDuelState、洗牌状态和弃牌预算。默认独立 52 张、8 张手牌；选牌由权威协调器校验和计分，一次出牌后怪物反击，清理出牌区再补牌；弃牌不消耗回合，抽牌堆耗尽时确定性洗回弃牌堆。可显式传入当前 DeckEntry 构筑并保留 OriginId。
- 新增 SingleBattle 独立 Runtime/Editor 模块，复用原有选择、出牌/弃牌按钮、卡牌飞行、理牌、牌堆统计、单牌提示和牌库总览；生命战 HUD 显示双方生命/格挡、怪物意图、回合、得分伤害、弃牌预算、胜负与重开。动画期间拦截重复提交，击杀跳过反击，失败或胜利后锁定行动。
- 初始数值：玩家 40 生命、守卫 75 生命、敌人攻击 7、每 5 分转 1 伤害、8 回合上限、整场 3 次弃牌。规则可在 SingleBattleHost Inspector 调整，未进行数值平衡。新增结构说明、配置与 Game View 截图菜单。
- CardDuel 组合状态目前仅在内存运行，不提供完整战局快照、Run 结果账本/奖励/跨节点生命写回。独立 ScoredDuel 快照不能用于恢复本 Demo 的卡牌状态；现有 Draw/Run 玩法与格式保持。
- 验证：新增 8 项协调器规则回归；离线引擎 272/272、Unity Editor 275/275 通过，Unity 编译零警告。真实场景按钮/动画验证通过：弃牌补牌、分数扣血、怪物反击、输入锁、牌库、排序、胜负和重新开始；控制台无错误。已检查 Game View 并调整重开按钮避让抽牌堆；未执行完整 Player/IL2CPP 构建。

### 2026-10-12 — 生命战单位拆离通用资源包

- CombatantState 直接持有必填 Health、MaxHealth、Block，移除 ResourceState；玩家和怪物均不再携带未使用的筹码/能量，公共结算和死亡判定沿用同一份生命值。玩家能量归未来玩家行动状态，本轮不引入没有消费规则的空结构。
- EncounterParticipantDefinition 新增仅声明槽位/阵营的构造方式，InitialResources 可缺省；ScoredDuel 描述不再构造零筹码/零能量包。既有 Draw/Holdem 的显式资源描述及玩法行为保持不变，原 ResourceState 保留给其能力流程。
- ScoredDuel 快照升级 schema v2，单位序列化直接保存 Health；combat.2 / scored-duel.2 同步冻结新指纹，v1 旧档明确拒绝，不自动迁移。补充无筹码/能量存档往返、恢复后双方伤害、必填生命/越界生命/旧嵌套形状拒绝回归，并更新模块边界文档。
- 验证：离线十程序集编译与测试 264/264、Unity Editor 测试 267/267 通过；Unity 编译零警告，控制台无错误。未执行完整 Player/IL2CPP 构建，未保存或替换场景。

### 2026-10-12 — 共用战斗单位与牌型伤害基建

- 新增纯 C# PokerEngine.Combat，仅依赖 Core/Abilities。玩家与怪物共用 CombatantDefinition/CombatantState，生命沿用唯一的 ResourceState.Health；死亡派生，格挡独立，状态效果复用 StatusInstance。基础单位不持有牌组、AI、回合、奖励或 Run。
- CombatEngine 提供 Damage/Heal/GainBlock/ClearBlock 的不可变原子批量结算；格挡优先、生命上下限、吸收/实际损失/溢出伤害/死亡事件、Revision 拒重及失败回滚。状态数据集中保存并校验，触发/过期策略留在模式层。
- Composition 新增 ScoredDuelEngine 最小一对一流程：宿主权威牌型分数经可配置整数比例转伤害，存活敌人固定攻击，击杀跳过反击，死亡/回合预算产生通用 EncounterProgress/Result。分数、伤害与胜负各自有明确责任；示例暂拒绝尚未执行的状态效果。
- ScoredDuelSnapshotCodec schema v1 冻结规则指纹，保存初始生命、单位、阶段与积分账本；恢复重放验证生命/格挡/阶段/Revision 一致性，支持等待怪物行动处恢复。未连接磁盘 I/O。
- 新模块加入 asmdef、linker、首包依赖和离线编译检查，补齐元数据与边界文档。现有 Draw 场景和 Run 仍按分数目标运行；后续生命战接入需要 Run 结果账本及快照升级，本轮不修改 Run v4/Draw v2/Match v5。
- 验证：新增 18 项回归，十程序集独立 warnings-as-errors 编译及离线测试 262/262 通过；Unity Editor 同步测试 265/265 通过（含 Player 编译列表/linker/元数据检查），编译 0 警告，控制台无错误。未执行完整 Player/IL2CPP 构建，未保存或替换场景。

### 2026-10-12 — Run 持有牌组，战局使用当前构筑

- 明确标准 52 张是新 Run 的基础牌组；Run.Deck 的复制、删牌、转换和强化经既有 DrawRunAdapter.Create 传到下一场，战局实例牌使用独立 ID 并以 OriginId 关联持久 RunCard。常规弃牌只移动区域，不永久删牌。
- StartBattle 限于独立验证；已绑定外部提交委托时拒绝直接重置为 52 张。正式 Run 的换战局和恢复继续通过 StartEncounter。补充 53/51 张显式构筑、强化/OriginId 保留和误重置拒绝的桥接回归；更新接入说明。未扩展 Run 场景管理。
- 验证：Draw 桥接测试 13/13 通过，Unity 编译 0 警告。

### 2026-10-12 — 单次 Draw 战局的标准牌组

- StartBattle 固定创建一套独立标准 52 张牌（四种花色各 13 张，不含大小王）；场景预览仅提供美术映射，不再决定玩法牌组的数量、牌面、效果或禁用状态。每场卡牌 ID 归属于该战局，出牌/弃牌/补牌保持身份与唯一牌区位置。
- 默认开局为手牌 8 张、抽牌堆 44 张、出牌区和弃牌区为空；已有 StartEncounter 显式构筑/恢复入口继续支持 Run 的牌组。新增回归覆盖残缺/重复/禁用/大小王预览不污染基础牌组，以及区间移动守恒和重开牌组独立性。
- 验证：Draw 桥接测试 12/12 通过，Unity 编译 0 警告，控制台无错误。

### 2026-10-10 — Draw 战局接入卡牌区和现有 UI

- 新增独立 Unity 桥接模块 DrawBattle：DrawBattleHost 以 DrawState 为权威，将四牌区及移除区投影到既有 CardMatchHost；StartEncounter 支持独立 Draw、快照恢复、新战局重绑定和 Run 提交委托。核心快照格式保持。
- 既有出牌/弃牌按钮提交真实 DrawCommand，检查阶段、上限和剩余次数；复用出牌手递交、弃牌飞行与补牌动画，出牌计分后清理 Played，未结束自动 Continue。动画期间阻止重复提交，胜负后停止补牌；理牌只改本地顺序，沿用当前方式在补牌落位后整理。
- CardMatchHost 增加 ICardMatchCommands 外部权威模式，旧的自由移动/调试编辑/重启命令拒绝越过玩法。继续复用牌堆余量、悬停统计、牌库总览与单牌提示。Tools > Cards > Connect Draw Battle In Current Scene 配置当前验证场景，支持 Undo，不自动保存。
- 全 Run 场景管理、奖励/商店/地图界面、能力选择 UI 及存档 I/O 尚待接入；提供强类型命令委托、状态及计分事件，不额外新增相关 UI。
- 验证：新增 Draw 桥接回归 10/10、既有卡牌/UI 185/185 通过；当前场景 Play 真实按钮与指针事件验证通过，动画期间重复出牌拒绝、补牌/计分/次数与四区一致、悬停统计及牌库总览同步。Unity 编译 0 警告，控制台无错误；未保存或替换当前场景，未执行完整 Player/IL2CPP 构建。

### 2026-10-09 — 通用战局数据结构

- 新增独立纯 C# PokerEngine.Encounters，仅引用 Core。EncounterDefinition 保存版本、玩法、目标、回合预算、参与者模板与特殊规则 ID；EncounterContext 精确绑定参与者槽位和实际身份，支持独立战局或可选 Run/楼层/节点来源。EncounterSetup<TSettings> 保持模式配置强类型。
- EncounterProgress 为粗粒度进度投影，检查行动者、待选择、检查点及终局摘要一致性；EncounterResult 按参与者保存胜/负/平局/中止、结束原因、回合与最终 Revision。复用 Core.ResourceState；卡牌、牌区、效果、随机和具体阶段仍由既有玩法状态持有，不额外序列化通用投影。
- DrawRunAdapter / HoldemRunAdapter 新增 Describe 入口，从真实 Run 输入生成通用描述及 DrawDefinition / MatchDefinition。现有战斗与 Run 推进规则保持，Run v4 / Draw v2 / Match v5 无 schema 升级；通用进度/结果自动生成、生命战/AI/特殊规则执行及场景宿主后续接入。
- 九程序集依赖、Player 编译列表、linker 保留与离线编译脚本同步更新；补齐 .meta 和模块/根/RunFlow 文档。9 项数据回归覆盖单人/对抗/合作、集合隔离、目标/参与者约束、阶段/结果一致性及两种玩法实际描述与旧快照恢复。未保存或替换场景，未执行完整 Player/IL2CPP 构建。
- 验证：九程序集独立 warnings-as-errors 编译及离线测试 244/244 通过；Unity 当前场景同步测试 247/247 通过，编译 0 警告，控制台无错误。

### 2026-10-07 — 跨 Run 玩家成长结构与结算

- 新增纯 C# PokerEngine.Progression，仅引用 Core。PlayerProgressionState 保存玩家身份/Revision、成长规则版本及指纹、局外货币、类型化解锁、永久升级等级和已结算 Run 记录；全局/分玩法统计由记录汇总。本局卡牌、小丑实例及地图继续由 Run/策划资产持有。
- ProgressionDefinition 独立配置货币、解锁目录/默认解锁、升级费用/等级上限/解锁前置。ProgressionEngine 提供不可变创建、结算和购买升级；结算按 RunId 去重，同一结果重试不重复发奖励，冲突记录拒绝。身份、Revision、奖励 ID、等级、余额及溢出均校验，失败不修改输入状态。
- Composition 新增 RunProgressionSettlement，从校验后的终局 Run 提取胜/败摘要，或显式记录放弃；奖励由宿主解析，不自动转换 Run 金币或构筑。宿主负责结算/清除活动 Run 的原子持久化、内容过滤及永久升级的下一局效果，未自动接入场景或 I/O。
- 独立 ProgressionSnapshotCodec schema v1 JSON，读档核对规则指纹、结算唯一性、解锁、升级支出、余额和 Revision；Run v4 / Draw v2 / Match v5 不变。未提供规则迁移、结算归档或签名。新增 asmdef/linker 保留及 .meta，八程序集依赖测试、首包检查和离线编译脚本同步更新。
- 验证：11 项新增回归；离线八程序集 warnings-as-errors 编译及测试 235/235 通过，Unity 当前场景同步测试 238/238 通过，编译 0 警告。更新根、引擎、RunFlow 及成长模块说明。未保存/替换场景，未执行完整 Player/IL2CPP 构建。

### 2026-10-07 — 策划 Run 关卡流程编辑器

- 独立 Assets/RunFlow 模块，RunFlowAsset 将整局规则、有序楼层、各层入口/节点/连接和画布位置保存为 Unity .asset（格式 v1）。BuildDefinition 复制为不可变 GameCore 模型，BuildContent 绑定真实内容目录及起始牌组；既有纯 C# 引擎不引入 Unity 依赖，Run 进度仍使用 schema v4 JSON。
- Poker > Run Flow Editor 提供楼层增删/排序、五种节点、端口连接、节点拖动、参数面板、分支/汇合、自动排布、缩放/平移、撤销/重做与图校验。可保存未完成草稿，初始化时拒绝无效流程；资产编辑不会修改已编译的 Run。保存只写当前资产。
- 提供三层 + Boss 层实际流程资产；新增 Runtime/Editor/Tests asmdef、link.xml 及资源 .meta。地图资产需由宿主引用或显式加载，玩家地图/选路 UI 与场景 Run 宿主尚待接入。
- 8 项 Editor 回归覆盖资产往返、不可变编译、真实四层 Draw Run/逐步快照恢复、连线校验和撤销、楼层重排、布局及 Player 程序集边界。实际编辑窗口确认楼层切换、参数面板、自动排列、端口连线与撤销；未保存或替换场景，未执行完整 Player/IL2CPP 构建。
- 验证：RunFlow 8/8、既有 GameCore 227/227 通过；Unity 编译 0 警告，控制台无错误。

### 2026-10-07 — 完整肉鸽由多楼层节点树组成

- RunDefinition 新增有序 Floors，每个 RunFloorDefinition 持有独立 NodeTree 和 Normal/Boss 标记；支持 1–32 层。完成当前树所选路线的终点后自动进入下一层入口，仅最后一层终点完成才结束整局。层内分支/汇合保持既有规则，跨层不接受节点连接或跳层命令。
- RunState 保存 FloorIndex 与所有 RunFloorState（FloorId、Kind、Nodes、IsCompleted），Nodes 便捷属性读取当前层；保留已完成楼层的路径，未来楼层维持未访问状态并提前固定图身份。金币、牌组、小丑、等级、成长及历史积分贯穿整局；层间非战斗节点保留上一场冻结对局。
- 战斗索引按楼层顺序拼接，层内允许重复使用其他层的节点 Id/Stage.Id，实例与积分保持跨层唯一；RunEncounterContext.Node/Floor/FloorIndex 指向当前或最近对局所属节点与楼层。旧 stages/单树构造包装为一个默认楼层，单树兼容 NodeTree 属性仅接受单层配置。
- RunSnapshotCodec 升级 schema v4，新增楼层及每层进度为必填字段；读档校验楼层身份/顺序、完成前缀、当前层、未来未访问状态、所有树指纹和跨层账本。旧 Run v1–v3 明确拒绝，无自动迁移；Match v5 / Draw v2 保持不变。
- Composition 新增 CreateFourFloors，提供三棵普通分支树 + 独立 Boss 层的德州/Draw 示例。新增楼层回归覆盖四层通关、跨层构筑/金币/成长、重复局部节点名、跨层事件、非战斗楼层、失败阻止继续、伪造/跳层/重排存档和未来图变更。更新根与引擎 README；地图生成及选路 UI 尚未接入。
- 验证：离线七程序集独立编译及测试 224/224 通过；Unity 当前场景同步测试 227/227 通过，包含 10 项节点、9 项楼层和 3 项首包检查；编译 0 警告。未执行完整 Player/IL2CPP 构建，未保存或替换当前场景。

### 2026-10-07 — 肉鸽统一节点与分支路线

- 新增 RunNodeDefinition 共同基类与 RunNodeTree：普通/精英/Boss 使用战斗节点，奖励和商店使用各自节点；支持多后继分支与路径汇合，构造时拒绝循环、缺失连接、重复身份和不可达节点。每个战斗可配置目标、回合预算、能量和通关金币，奖励/商店可配置独立报价池。
- RunEngine 按节点连接推进：单后继自动进入，多后继进入 NodeChoice 等待玩家 ChooseNextNode，无后继成功完成即结束 Run；StartRun 由系统启动，CompleteNode 由玩家离开商店。校验认证身份、Revision、阶段及后继连接，失败保留原状态和随机数；旧 stages 构造自动生成线性节点图，保留 StartEncounter 的原有线性接入。
- RunState.Nodes 保存图版本/指纹、当前节点、实际进入路径和已完成前缀；积分账本改为按实际访问的战斗校验，允许绕过未选路线并汇合到较小 StageIndex。尚未进入战斗时 StageIndex 为 -1。每次对局转换及构筑同步均先校验模式状态，再允许自动进入后继；跨战斗命令的事件按各自来源状态过滤。
- RunSnapshotCodec 升级 schema v3，读档同时验证图身份、路径、完成顺序、账本和模式状态；旧 Run v1/v2 明确拒绝，无自动迁移。Match v5、Draw v2 保持不变。公开 RunState 构造新增必填 RunNodeState；内容拓扑或战斗配置变化会使指纹变化。
- Composition 新增 ExampleRunContent.CreateBranching，提供德州/Draw 共用的普通/精英分支、额外奖励、商店汇合及最终 Boss 示例。精英/Boss 类型通过 RunEncounterContext.Node 传给适配器；特殊 Boss 行为、地图生成与选路 UI 后续接入。更新根与引擎 README。
- 新增节点回归，覆盖两路通关/汇合、独立报价池、读档后选路与构筑传递、逆序战斗索引、非法/越权/过期路线命令、伪造路径与未访问分数、图配置变更、无战斗节点、连续战斗事件和失败阻止推进。同步修复已有 CardScoreQueryTests 的 C# 7.2 局部变量重名编译错误。
- 验证：离线七程序集独立编译及测试 215/215 通过；Unity 当前场景同步测试 218/218 通过（含 10 项节点测试和 3 项首包检查），编译 0 警告、控制台无错误。未执行完整 Player/IL2CPP 构建；未保存或替换当前场景。

### 2026-10-07 — 牌堆悬停改为花色点数统计表

- 新增 DeckSummary 原生 Canvas Prefab、DeckSummaryView、PixelPanelGraphic 与 Editor-only DeckSummarySetup。按参考排版：上方 A…2 十三列点数及剩余总数，下方四条花色颜色行显示 52 个交叉余量，左侧花色总数；0 压暗。使用 Fusion Pixel 字体与原生顶点阶梯边框，不新增纹理、相机或逐实例材质。初次准备生成独立 .meta，后续 Prepare 保留 GUID/布局，仅修复缺失 CanvasRenderer。
- DrawPilePresenter 持有统计表 Prefab 及单个临时实例，在 icon 上方右侧对齐，Summary Gap 默认 14。悬停仅显示统计表、离开即收起；左键切换并保持原完整 DeckOverview 四行卡牌页，再点解除固定，返回关闭且不立即重开。统计表完全不接收射线，不创建第二份逻辑牌堆或写入卡牌悬停。
- DeckOverviewData 增加 Remaining(rank) / Remaining(suit, rank)。DeckOverviewPresenter.ReadData 提供两处共用的只读观察者投影及缓存，完整页关闭时也可读取，完整页刷新比较实际投影而非仅 State，避免统计表先读取导致漏刷新。重复牌分别统计、抽出牌不计余量、禁用/失效仍计入，隐藏/未知只显示其他汇总。
- 统计表沿用 DOTween：0.18 秒由下方 28 单位上移、0.94 倍放大回稳，0.12 秒缩小下移消失；中途移出/重入从当前姿态接续，数据刷新不重播。各组件仅拥有与清理自身 Tween，停用和销毁清理临时实例。原 UI Camera 分辨率、完整页与逐牌提示保持既有路径。
- 新增统计回归并更新入口与 Play 动画验证，提供 Capture Deck Hover Summary In Play 捕获真实 Game 画面后恢复交互。项目/展示/交互/UI 文档同步，现有 Runtime/link.xml 与场景 Prefab 引用将新运行逻辑和资源纳入首包，无新存档字段。
- 验证：展示/交互 185/185 通过，Unity 编译 0 警告；Play 统计表与完整页切换、离开收起、快速反向、完整页及单牌提示事件验证通过，实际 Game 截图确认彩色底板、阶梯边框、十三列/四行数字正常显示，控制台无错误/警告。两份运行程序集脱离 UNITY_EDITOR 编译通过；未注入系统鼠标、未执行完整 Player 构建，当前场景仅标记 dirty，未自动保存。

### 2026-10-07 — 牌堆预览离开关闭与剩余数字

- 悬停牌组预览只由牌堆 Icon 驱动：鼠标离开图标立即调用 Close 并开始既有 DOTween 收起，移入面板不续留；移除面板范围检测与延迟关闭 Tween。点击保持显示仍可在离开图标后继续查看，返回关闭。
- DrawPileView 新增 RemainingCount 引用与 BindRemainingCount，消费既有 Match 抽牌堆余量，在图标 Deck/Remaining/Count 右下角用 Fusion Pixel 显示数字，含空堆 0。深色底与文字随牌堆缩放、不拦截射线；负数在更新文字/叠层前拒绝，不保存第二份牌堆。
- Prepare 只补缺失数字节点与引用，保留现有图集、指定封面、厚度、位置、样式和 Prefab GUID。更新项目、展示、交互和 UI 文档，改写既有回归与 Play 事件验证，验证抽牌后数字/空堆/重启、离开即时收起与点击保持。沿用现有资源进入首包，未新增资产文件或存档字段。
- 验证：展示/交互 184/184 通过，Unity 编译 0 警告，两份运行程序集脱离 UNITY_EDITOR 编译通过；Play 牌堆/UI 动画事件验证通过，真实 Game 画面确认右下角剩余 32 清晰显示，控制台无错误/警告。未注入系统鼠标、未执行完整 Player 构建，未自动保存当前场景。

### 2026-10-07 — 图集牌堆入口与 DOTween 惯性显隐

- DrawPileIcon 改为八层原生 CardUIGraphic，直接采样当前手牌 CardView.Atlas 的 card_back，以错位、逐层乘色和阴影形成厚度；余量控制叠层，空堆保留暗色单层。DrawPileView 提供 CoverCardId / SetCoverCard 指定顶层图集牌面，默认牌背；仅装饰封面，不读取隐藏的逻辑牌堆顶牌。图集与封面切换先验证后更新，继续共享 Canvas 材质。
- 图标 Canvas 排序 300，并添加 GraphicRaycaster；固定 Icon 射线区域与 Deck 缩放节点分开，避免放大造成移入/移出闪烁。悬停 0.14 秒 OutBack 惯性放大至约 1.095 倍并预览牌组；鼠标移入面板保持预览，离开两处后延迟 0.12 秒关闭。点击保持打开，再次点击解除保持，返回关闭并避免仍悬停时立即重开；不发抽牌命令、不改变 Match 或存档。
- DeckOverviewView 面板从下方 90 Canvas 单位用 0.22 秒 OutBack 上移回稳并渐显，退出 0.14 秒下移渐隐；关闭起点即释放射线与悬停作用域，动画结束才停用。快速关闭/重开从当前姿态接续。
- 共用 CardHoverPresenter 为手牌 World Space 与牌组 Canvas 的同一花色/点数/筹码提示接入 DOTween：0.16 秒惯性放大出现、0.12 秒缩小消失。逐帧内容/位置刷新不重播动画，快速移入移出从当前倍率反向，保留既有字号、尺寸、红黑配色与左侧布局。各组件仅清理自己持有的 Tween，使用不受 TimeScale 影响的更新。
- Prepare 迁移现有 DrawPileIcon 与 DeckOverview Prefab，保留 GUID、图集/材质及用户 SuitRows/统计板布局；Configure Deck Icon And Overview 接入同场景入口，编辑器辅助代码仍限定 UNITY_EDITOR。更新根、展示、交互与 UI README，运行逻辑沿现有程序集与资源引用进入首包，无新美术资源或存档字段。
- 验证：展示/交互 184/184 通过，Unity 编译 0 警告；新增回归覆盖图集封面原子校验、叠层数量、稳定射线区域、动画反向/不重复播放、预览移入面板、点击保持、返回关闭与重复绑定。Play 的牌堆/UI 动画及牌组总览事件验证通过，真实 Game 捕获确认 UI 为 1920×1080 原生绘制、场景仍 960×540，控制台无错误/警告。两份运行程序集脱离 UNITY_EDITOR 编译通过；未注入系统鼠标、未执行完整 Player 构建，当前场景仅标记 dirty，未自动保存。

### 2026-10-07 — 四行牌组总览复用左侧悬停信息

- CardHoverTarget 扩展为场景 CardInstanceView / 原生 Canvas CardUIItemView 两种锚点；DeckOverviewPresenter 实现 ICardHoverSource，从只读投影提供规则花色、点数和 CardScoreRules 基础筹码。已抽出、禁用、失效牌仍可查看，隐藏/未知定义不提供信息；不解析美术 id、不写入 Match 或手牌交互。
- 继续由场景 CardHoverPresenter 持有既有 CardSuitChipsTooltip Prefab 和唯一提示实例。Canvas 悬停将同一实例挂到页面 Canvas，依据实际牌面四角放在左侧、垂直居中，跟随缩放/摆动、边界限制及 UI Camera；Canvas Size Multiplier 默认 0.4、Canvas Gap 默认 8，均可在 Inspector 调整。切回手牌恢复原 World Space 父级、尺寸与排序，保留原红黑花色和筹码配色。
- 新增可选 ICardHoverScope：打开总览时独占提示来源，防止背景手牌悬停串入；鼠标移出隐藏，关闭总览恢复普通来源。提示不接收射线。注册工具接收通用来源，Show Canvas Deck Overview 自动接入同场景共用提示，保留既有设置及 Prefab/字体/背景 GUID。
- 展示/交互 181/181 通过，Unity 编译 0 警告；新增回归覆盖单实例跨 Canvas/World Space 复用、左侧位置/页面排序、红黑内容切换、背景来源隔离、隐藏牌/停用 Graphic 过滤和边界限制。Play 验证扩展至实际指针事件、数据内容及父级，另提供 Capture Canvas Deck Hover In Play 捕获真实画面并恢复测试悬停；均仅 Editor，不注入系统鼠标。
- 更新交互/展示/UI 文档，CardInteraction 运行程序集脱离 UNITY_EDITOR 编译通过；复用现有资源与首包引用，无新存档字段或美术资源。未执行完整 Player 构建，未自动保存当前场景。

### 2026-10-06 — Suit Row 纵向重叠、逐牌摆动与原生 UI 分辨率

- DeckOverviewView 增加 VerticalOverlap / SetVerticalOverlap，默认 0.12、范围 0–0.3；四行在 SuitRows 区域内等高分布并轻微纵向重叠，0 恢复无重叠。保留父级大小/位置与现有美术调整，Prepare 只迁移行 anchors，不重建统计板或覆盖用户布局。悬停牌及其所在行置顶，移出后恢复花色顺序。
- CardUIItemView 每张牌继续使用独立幅度/周期/相位的 local Y 摆动，并附加幅度 0.55 倍的轻微 local Z 摆角，使原生无光照 Canvas 上的自转可见；悬停仍持续摆动，不改变卡牌属性、Match 或逻辑 Zone。
- 嵌入式 URP 增加 renderOverlayAtNativeResolution，UICameraStack 自动启用并保持 UI Camera 在 Stack 末尾。场景和普通 Overlay 先完成 540p 渲染、复古处理及 Point 放大，尾部 UI Overlay 直接加载输出颜色并按输出尺寸绘制，独立清除深度，不借用/改尺寸低分辨率附件。普通相机默认行为不变，XR 沿原有路径。
- 实际 Play 捕获：1920 × 1080 输出下场景/复古输入保持 960 × 540，UI 颜色/深度目标为 1920 × 1080，UI 不再降分辨率或二次放大。回归检查 1280×720、1920×1080（含普通 Overlay）、800×450 的实际单像素条纹、背景保留和 UI 停用；另覆盖跨行悬停层级、父级缩放与逐牌摆动。
- Unity 编译 0 警告，展示/交互 179/179 通过，Play 牌组总览验证通过；Rendering、CardPresentation、CardInteraction 三份运行程序集脱离 UNITY_EDITOR 编译通过。更新渲染/展示/UI 与项目文档，沿现有程序集/Prefab/URP 引用进入首包；未执行完整 Player 构建，未自动保存当前场景。

### 2026-10-06 — SuitRows 共同布局父级

- DeckOverview 的四个 Suit Row 移到 Frame/Panel/SuitRows 下，共同父级默认 680 × 528、位置 (164, -8)；四行横向拉伸、各占四分之一高度，原默认排列位置保持一致。修改父级 RectTransform 的位置、宽高或缩放即可整体调整牌区，统计板/点数列/标题仍独立。
- DeckOverviewView 暴露 SuitRowsRoot / ConfigureRowsRoot / RefreshLayout，Editor 与 Play 在 Canvas 渲染前检测尺寸变化并自动重排；卡牌高度、弧高、边距随区域高度同比适配，窄区域额外容纳完整牌面。重排保留实例、材质、本页悬停及对局投影，不生成抽牌动画或提交 Match 命令；SetRowLayout 先验证适配后的完整布局再改变设置。
- Prepare Canvas Deck Overview Prefab 自动迁移旧层级，保留已有行节点、Prefab GUID、美术与左侧布局，重复运行不覆盖用户调整；新模板直接生成共同父级。运行逻辑继续沿现有 Runtime/link.xml 进入首包，迁移代码仅 Editor，无新资产或存档字段。
- 更新表现/UI 文档；Unity 编译 0 警告，展示/交互 177/177 通过。新增检查实际父级缩小/放大后四行尺寸、自动重排、实例/悬停保留、左侧独立及缩放后的布局校验；非 UNITY_EDITOR 运行程序集编译通过。未执行完整 Player 构建，未自动保存场景。

### 2026-10-06 — 牌组总览固定弧形行与紧凑像素统计板

- 总览四行固定 680 × 132，卡牌高度 120、弧高 5、边距 2；间距随牌数填满行宽，按原顺序重叠，空行/单牌保持边界。复用手牌 CardZoneRowLayout 的圆弧与完整旋转足迹约束，将 XZ 位置转为 Canvas XY，并修正正立牌面旋转；保留逐牌悬停置顶/放大和 idle local Y 摆动。暴露 Inspector 与 SetRowLayout 参数，布局错误在改变已有实例前拒绝。
- 面板由 1200 × 642 缩至 1040 × 632，左侧参考小丑牌信息板，保留每花色数字/JQK/A/合计，新增 A/人头/数字总数及 A…2 余量。统计只读取剩余实例；禁用、失效不从牌组余量中扣除，隐藏/未知牌仅汇总。更新小字号和原生返回按钮射线设置。
- 以内置 image_gen 生成三枚简单手绘像素分类图标，原始透明 PNG 导入 CardPresentation/UI/Icons，Sprite/Point、无 mipmap/无压缩；维护资源 .meta 及包含完整提示词的 README，不覆盖既有卡牌图集。
- UI 图集映射改为与手牌相同的完整区域采样和边界半像素钳制，避免把整幅牌面压缩到内缩坐标。图集区域/效果参数全部保留于 UV 通道，几何法线只参与装饰方向，避免 Canvas 合批或旋转改变图集坐标；透明度沿用原生顶点 Alpha、保留 HDR Tint 和效果参数。开启 Canvas Pixel Perfect，继续共用手牌内部目标和 Point 放大。
- 新增 Update Canvas Deck Overview Layout 明确升级菜单；Prepare 只迁移旧布局，重复准备保留新版布局与 Prefab GUID/共享材质。编辑器捕获先解除相机/活动纹理再释放预览资源。更新根、表现及 UI 文档，沿原 Runtime 程序集/link.xml 和 Prefab 资源链进入首包，未变更 Core/存档/对局命令。
- 验证：Unity 编译 0 警告，展示/交互 176/176 通过，新增两项覆盖不同牌数的固定行宽、浅弧/重叠/正立与边界、原子布局校验、分类/点数统计及图标导入。Play 抽牌统计、复用、悬停事件和关闭重开通过并恢复原对局；实际 1920 × 1080 Game 捕获确认主相机和 UI 均为 960 × 540，Point 输出。运行程序集脱离 UNITY_EDITOR 编译通过；未执行完整 Player 构建，未自动保存当前场景。

### 2026-10-06 — 原生 Canvas 牌组总览与图集卡牌

- 新增 CardUIGraphic / CardUIItemView、DeckOverviewData / DeckOverviewView 及 DeckOverviewPresenter，原生 MaskableGraphic 直接采样现有 FourBeasts 图集，沿用 UI Camera Stack、Fusion Pixel 字体及 Canvas 批处理。四行按花色分组、点数降序，每个实例独立保留；左侧统计剩余数字牌、J/Q/K、A 与合计。
- 新增 UI/Shaders/CardCanvas.shader、Art/Materials/Cards/FourBeastsCanvas.mat、CardUIItem.prefab 和 DeckOverview.prefab。逐牌 Tint、HDR 镭射、火漆、红叉及透明度通过顶点颜色/UV 通道传递，共享纹理与材质；支持 UI 透明混合、CanvasGroup、Stencil 与 RectMask2D，不使用 MeshRenderer 或运行时 RenderTexture。火漆采用固定装饰光照。
- CardArt 新增 CardSurfaceCommon.hlsl，共享图集边界采样、镭射光谱与火漆坐标计算；Deferred Input 仅改为调用共享函数，保留场景卡牌渲染路径。图集 UV 半像素内缩防串牌。
- 已抽出卡原位透明并去色，悬停用 DOTween 放大/回落、提高透明度并在本行置顶，idle 轻微绕 local Y 摆动。本页悬停投影独立于手牌交互，不提供选中/提交；关闭清理自身 Tween，刷新复用视觉身份，编辑器重载清理临时孤立实例。
- Presenter 从明确牌组成员和绑定 DeckZone 构建只读投影，统计按 DefinitionId 解析，不从美术名推断、不改 Match 或 CardEffects；排除无关玩家卡牌，隐藏/非标准牌仅汇总。提供 Configure / SetRoster / Open / Close；新增准备、打开、样例捕获菜单，牌堆图标点击入口仍留待后续。
- 新增文件、目录及资源维护独立 .meta，运行代码沿既有 Runtime asmdef/link.xml 和资源引用进入首包，编辑器工具/验证整文件限定 UNITY_EDITOR。更新根、表现、交互及 UI README；工具不自动保存场景。
- 验证：Unity 编译 0 警告，展示/交互 174/174 通过；运行程序集移除 UNITY_EDITOR 定义及 UnityEditor 引用后编译通过。Play 验证悬停事件、抽牌统计、实例/材质复用和关闭重开，通过后恢复原对局；Game 画面已核验，控制台无错误/警告。鼠标验证使用 Unity Pointer 事件，未核验物理鼠标；未执行完整 Player 构建，当前场景未自动保存。

### 2026-10-06 — 局内共享卡牌对局与编辑器宏隔离

- Core 新增纯 C# CardTableState / CardTableSession / CardTableChange：全局实例注册表、有序牌区与 Revision，保证任一牌只有一个位置；抽牌、出牌、弃牌、重排使用 Prepare/Commit，校验 Zone Kind、权限、容量及身份，并拒绝过期/跨会话命令。能力/计分/德州/出牌玩法仍解耦，仅共享此归属基础。
- 新增 CardTableSnapshotCodec schema 1，16 MiB 严格 UTF-8，上下文完整验证后恢复会话；不包含美术、观察者可见性或本地交互，不变更既有 Holdem/Draw/Run 存档。完整隐藏信息快照用于可信玩法存档，网络需另外构建授权投影。
- 新增 CardInteraction/CardMatchHost，集中持有启动快照、运行会话与本地观察者投影；提供 Player 可用的 StartMatch(state, observerCards) 初始化/恢复，规则属性以 Core 为准。CardZonePlayHost 改为 Match/ZoneId 绑定及命令适配器，不再独立维护手牌/牌堆/弃牌堆。多个区域可共享同一个抽牌堆/弃牌堆，重复 BeginSession 或停用牌区不重置数据；RestartMatch 才统一重开。
- View 增加提交回调，在准备/验证视觉后提交完整 Match，再通知表现；出牌继续使用原实例的交叉转移，抽/弃保留翻面、回稳和独立音效，排序保持落位后提交。坏牌面不消耗共享牌堆。
- 验证窗口每次刷新/提交先读取绑定 Match，Edit 配置启动快照、Play 调试当前会话；撤销记录 Match，切换目标重新读对应牌堆，移除第二份 Play 卡牌缓存及进入 Play 前由旧缓存自动重写的行为。实验数据只作为缓冲/诊断。
- CardPresentation/Editor 所有工具整文件标记 UNITY_EDITOR；运行目录的 CardMatchHost.Editor / CardZonePlayHost.Editor 整文件限定，Capture、ReplaceCardsAndDeck、ConfigureInitial、Debug 编辑、预览出牌与旧列表迁移不进入 Player。场景迁移菜单验证所有身份后统一创建 Match、清空旧 Host 列表，支持 Undo，只标记场景 dirty。
- 所有新增脚本及测试带独立 .meta，运行类型沿已有 Runtime asmdef/link.xml 进入首包。更新根、引擎、交互、表现及 UI 文档。Core 207/207、展示/交互 166/166 通过；两个运行程序集移除 UNITY_EDITOR 定义与 UnityEditor 引用后编译通过，Unity 编译 0 警告，Play 四按钮事件验证通过、控制台无错误/警告。未执行完整 Player 构建，未自动保存场景。

### 2026-10-06 — 玩家出牌按钮与右下角局内抽牌堆图标

- 手牌面板改为出牌、弃牌、点数理牌、花色理牌；移除牌堆余量和“优先/落位后理牌”状态行，面板高度从 164 缩至 124，仍为左下角两行、0.75 缩放。原 Draw 节点改为 Play，使用 FormerlySerializedAs 迁移按钮/文字引用，保留 Prefab GUID、字体及颜色。
- HandActionPresenter 去掉玩家主动抽牌入口和 Cards Per Draw/Hand Limit 配置，绑定现有 CardZoneTransferPresenter 路径，将选中牌经 PlaySelected 移入出牌区，继续复用交叉飞行、视觉身份和落位整理。出牌检查两区会话、路径、选择、禁用、可见视觉、飞行状态及剩余五张容量；无路径/无选择/超容量和过期按钮调用不提交，失效牌仍可出/弃。
- 新增 Runtime/DrawPileView、CardInteraction/Runtime/DrawPilePresenter、Editor/DrawPileSetup 及 DrawPileIcon.prefab，全部带独立 .meta。右下角图标大小 64 × 96、边距 20，三层 RawImage 直接复用 FourBeasts 牌背，不复制资源或改动 CardArt 导入；有牌显示叠层，耗尽保留暗色单层，无 Button/GraphicRaycaster/文本，全部关闭 Raycast Target，点击逻辑留待后续。
- 图标读取宿主的局内 DeckCards 副本，编辑器仅维护启动快照；实际 DrawCards 由玩法流程调用并消耗 Core DeckZone，不从 UI 发起。关闭验证窗口不影响数据和图标刷新。手牌固化/配置工具自动接入图标、同一 UI Camera 和既有出牌路径；新增 Update Player Hand UI 升级菜单，重复注册复用场景实例，未自动保存用户场景。新运行脚本沿现有程序集/link.xml 和场景/Prefab 资源引用进入首包。
- 更新根、展示、交互及 UI 文档；调整原有按钮回归，新增 4 项测试覆盖出牌容量/失效、图标资源/输入边界、运行时消耗/重启与空堆表现。Unity 编译 0 警告，展示/交互 162/162 通过，Play 四按钮事件检查通过，Game 画面已核验左下角按钮与右下角图标，控制台无错误/警告；未执行完整 Player 构建。

### 2026-10-06 — 左下角紧凑按钮与独立 UI Camera Stack

- 手牌按钮 Prefab 改为 Screen Space Camera，左下角两行排列、边距 16、缩放 0.75；基准大小 316 × 164，保留 Fusion Pixel 字体、四按钮事件及原配色。新增 Use Compact Hand Action UI Camera 菜单，更新已有布局并接入当前场景，保留 Prefab GUID。
- 新增 Rendering/Runtime/UICameraStack、CardPresentation/Editor/CardUICameraSetup 及独立 .meta。主相机排除 UI，子对象 UI Camera 只绘制 UI，并追加到现有 Base Camera Stack；注册复用、不清空其他 Overlay。编辑器和 Play 同步投影与视口，销毁移除自己的 Stack 引用。
- 新增 Settings/UIRenderer.asset 及 .meta，独立 Forward Renderer，无自定义 Feature/PostProcessData、无不透明层绘制、关闭自身降采样及深度预处理；UI Camera 关闭阴影、后处理、额外深度/颜色纹理请求、HDR/MSAA、XR、动态分辨率与 Volume 自动更新。MainURP 追加 Renderer index 1，默认仍为主 Deferred Renderer。
- CardHoverPresenter 增加 RenderingCamera / SetRenderingCamera：UI Camera 绘制 World Space 提示，场景相机继续负责几何投影和位置；编辑器注册自动匹配已有 Stack。主场景 RetroPostProcessFeature 改为 Base 执行、Overlay 跳过，避免主相机非最终解析者时丢失复古效果。
- UI Stack 沿用主相机当前的 540p 渲染目标，最终共同 Point 放大；独立 Renderer 不额外降采样，UI 不经过场景复古调色。新增 Game 帧捕获菜单，实测 1920 × 1080 输出时场景/UI 均为 960 × 540，复古输入尺寸相同，左下角布局与场景效果已检查。
- 更新根、展示、交互与 UI 文档，新增 Rendering README/.meta。新增 5 项回归；Unity 编译 0 警告，展示/交互 158/158 通过，Play 四按钮事件检查通过，实际 Stack 渲染无错误/警告。当前场景已接入并保留 dirty，未自动保存；未执行完整 Player 构建。

### 2026-10-06 — Game 手牌抽牌、弃牌及两种理牌按钮

- 新增 `CardPresentation/Prefabs/HandActionPanel.prefab` 与 `Runtime/HandActionPanelView`，四按钮为抽牌（默认每次 1 张）、弃牌（当前选中数）、点数理牌、花色理牌；当前使用 Fusion Pixel 字体和占位色块，显示余牌和当前排序选择。Overlay UI 默认位于当前手牌上方，仅四个按钮 Image 拦截鼠标，背景/文字不阻挡牌区。
- 新增 `CardInteraction/Runtime/HandActionPresenter` 与 `CardZonePlayHost.Draw`。Presenter 持有手牌宿主/UI 引用和本地抽牌数/容量配置，按钮调用 Card 数据命令；牌堆不足/满手牌/无选中/禁用选择时阻止无效操作，失效牌仍可弃。抽牌使用 Core DrawTo 消耗牌顶原身份并原样进入手牌，View 消费变化播放既有飞入/翻面/惯性/音效；两种理牌保留最后选择、等待过渡结束后更新列表，不直接改 CardView 或 Transform。
- 将验证窗口配置牌堆固化到场景启动快照，提供 InitialDeckCards / DeckCards / DeckZone 和 ReplaceCardsAndDeck；三组身份不允许重叠，坏牌面/重复身份/非法数量在消耗前拒绝。关闭窗口仍能抽牌，不自动补牌或回收弃牌；每次 Play 从初始数据复制。验证窗口在 Play 同步宿主牌堆，与 Game 按钮共用列表并一起提交；通用/出牌区不注入测试牌堆。Core、正式 Match 存档契约及 View Prefab 导出边界保持原状。
- 新增 `Editor/HandActionPanelSetup`，Prepare Hand Action Panel Prefab 保留已有美术设置，Configure Hand Action Buttons 及固化手牌自动接入按钮；缺少活动 EventSystem 才创建 StandaloneInputModule。7 组新增脚本/Prefab 各带独立 .meta，沿现有运行程序集/link.xml 和普通场景资源引用进入首包。更新根、展示、交互及 UI 文档；当前场景已接入、只标记 dirty，未自动保存用户场景。
- 新增 6 项回归，覆盖按钮事件到 Card 列表/增减动画、抽牌落位后整理、序列化/耗尽/重启、原子拒绝、禁用/失效、重复绑定/停用/容量与 Prefab 输入边界。新增测试菜单 Validate Hand Action Buttons In Play，检查真实 Play 反序列化后的四个按钮事件绑定，结束恢复启动数据。Unity 编译 0 警告，展示/交互 153/153 通过，Play 四按钮事件检查通过；布局与字体已在 Game 检查。自动鼠标输入未被 Game 接收，真实鼠标点击尚未完成核验；未执行完整 Player 构建。

### 2026-10-06 — 抽牌落位后按当前选项整理手牌

- 手牌默认选择点数排序；成功抽牌后先保留提交顺序，整批飞行和惯性回稳结束，再调用现有 Card 层 SortByRank / SortBySuit 并播放重排。两个原有按钮设置当前排序选择，飞行中点击只排队，采用最后一次选择；追加抽牌延后整理。牌顶/批量/指定牌面/任意抽取以及 Edit/Play 一致，手动移动、反转和打乱仍可使用。
- 新增 CardInteraction/Runtime/CardZonePlayHost.Sort.cs 及独立 .meta；RequestSortAfterLanding / TryCompletePendingSort 由 Play LateUpdate 驱动，关闭验证窗口仍会完成，停止/停用/重启清理请求。运行时沿用首包程序集/link.xml，排序偏好随场景快照固化，不改 Core 排序/转移契约或权威存档。
- 编辑器在预览过渡完成后更新卡牌列表，支持 Undo；诊断“手牌之后”同步重排顺序，Drawn 保留抽取顺序。排序保留身份、属性、可见性与交互，不重新飞入或补播抽牌提示；牌堆/出牌区不自动整理。更新根/展示/交互文档，新增 5 项时序回归覆盖两种排序的 Edit/运行时落位、飞入结束后的惯性等待、追加抽牌、复用、最新选择及生命周期清理。
- 验证：Unity 编译 0 警告，展示/交互 147/147 通过；未改写或自动保存场景。

### 2026-10-06 — 场景共用的卡牌悬停提示 Presenter

- 新增 CardInteraction/Runtime 的 `CardHoverPresenter`、`ICardHoverSource` / `CardHoverTarget`、`CardZonePlayHost.Hover` 及独立 .meta。场景 Presenter 持有提示 Prefab 和一个复用实例，多个来源按配置顺序提供观察者可读的花色、点数、筹码和视觉锚点；CardZoneView/CardView 不持有 UI 引用，提示不修改卡牌或计算最终得分。
- 验证宿主从运行副本的逐牌悬停状态及 DefinitionId 查询标准牌定义和 CardScoreRules 基础点数，不从美术 ID 推断；禁用/失效牌保留印刷点数，未知定义不提供提示。输入模块增加纯 C# Scoring 引用；正式玩法可通过接口提供能力解析后的牌面及名义筹码，观察者授权由来源处理。
- Play 的 LateUpdate 将 World Space 提示放在实际卡牌上边缘，朝向共用场景相机；支持尺寸、间距、相机平面偏移、向相机偏移和视口边界。牌背、飞入/离场、无悬停或停用来源隐藏；UI 不拦截射线，停用/销毁释放临时实例，编辑模式不自动处理鼠标。
- 新增 Editor/CardHoverPresenterSetup 及 .meta；固化手牌自动连接同场景/相机的 `Card Hover UI`，菜单 Configure Card Hover Tooltip 可接入已有固化手牌。工具支持 Undo，保留既有样式，只标记场景 dirty；当前验证场景已连接，未自动保存场景。运行时沿用首包程序集/link.xml，保存后的场景引用带入提示及其背景/字体资源。
- 新增 6 项回归，覆盖鼠标控制器 → 卡牌状态 → UI、复用/排序、基础点数和红黑颜色恢复、隐藏牌/弃牌/飞入过滤、定义与美术解耦、禁用/失效、相机跟随/视口、多来源以及停用/重启清理。Unity 编译 0 警告，展示/交互 142/142 通过。已进行 Play 检查；原生鼠标触发提示未完成核验，未执行完整 Player 构建。更新根、展示、交互和 UI 文档。

### 2026-10-06 — 提示上栏花色符号、文字和点数

- 上栏统一显示“花色符号 + 花色文字 + 点数”，如 `♣ 梅花 J` / `♥ 红桃 J`；Fusion Pixel TMP 图集预填原生 `♠ ♥ ♣ ♦`，不使用花色 Sprite 或字体回退。
- 新增 Runtime/CardTooltipView 及 .meta，绑定到现有提示 Prefab；`SetContent(suit, rank, chips)` 和 Inspector 预览共用刷新，红桃/方块整个上栏为红色，切回黑桃/梅花恢复深色，下栏蓝色数字及深色筹码保持独立。组件只消费调用方已解析的展示数据，实际悬停绑定留待后续 Presenter。
- CardPresentation.Runtime 新增 UGUI/TMP 引用，沿用首包程序集和 link.xml；PokerEngine 不增加 UI 依赖。更新生成工具，在保留 Prefab GUID、布局与颜色设置的同时补充字形及展示组件；更新根/展示/UI/字体文档。
- 验证：Unity 编译 0 警告，四种花色离屏渲染成功，检查上栏实际字形均来自 Fusion Pixel，已核验整行染红及红→黑颜色恢复；未修改或保存用户场景。

### 2026-10-06 — 花色与筹码提示 World Space Prefab

- 完整导入用户提供的 `card-suit-chips-background-v1.png` 至 `CardPresentation/UI/Textures/CardSuitChipsBackground.png`：RGBA、Sprite/FullRect、无压缩和 mipmap；保留透明边缘、上下底板及原比例。
- 新建 `CardPresentation/Prefabs/CardSuitChipsTooltip.prefab`，World Space Canvas 使用 448 × 320 画布；上栏“梅花 J”，下栏蓝色“+10”与深色“筹码”，独立 TMP 文本节点。关闭所有鼠标射线拦截，未接入实际悬停、跟随或规则数据绑定。
- 从 Fusion Pixel 12px 比例简体中文 TTF 生成 `Fonts/FusionPixelFont/TMP/FusionPixel12pxProportional.asset`，12px 位图/Point 采样，预填花色、筹码与 ASCII，动态补充字符；导入标准 TMP Essential Resources，提示显式引用像素字体。新增资源均保留 .meta，沿普通场景/Prefab 引用进入首包。
- 新增 Editor/CardTooltipPrefabBuilder 及准备/离屏预览菜单；仅 Editor 程序集增加 UGUI/TMP 与渲染依赖，更新根/展示/字体 README 并新增 UI 资源说明。重复准备保留已有字体/Prefab 调整；预览使用独立 Preview Scene，不保存当前场景。
- 验证：背景源文件与导入副本 SHA-256 相同；Unity 编译与 Prefab 结构检查通过，实际离屏渲染已检查两栏文字、颜色及对齐。未执行完整 Player 构建。

### 2026-10-06 — 单牌基础点数与悬停查询接口

- 新增纯 C# Scoring/CardScoreRules.GetBasePoints(Rank / CardDefinition)，统一当前 2–10、J/Q/K=10、A=11 的规则，并拒绝非法点数/空定义；ScoringCard 新增只读 BasePoints、PointsPerTrigger，复用既有 BonusPoints、倍率、重触发和 CanScore。
- ScoreEngine 的逐牌点数改为消费同一个 PointsPerTrigger；查询保留失效牌原始数值，结算仍跳过其全部逐牌贡献。单次名义点数不等同最终得分，不包含倍率、小丑、牌型或重复触发；有效牌面由玩法宿主解析。checked 溢出策略与原有失效过滤顺序保持一致。
- 新增 Runtime/Scoring/CardScoreRules.cs、Tests/Editor/CardScoreQueryTests.cs 及 .meta，沿用首包程序集和依赖边界；无 Core、存档 schema 或场景数据变更。更新根/引擎 README 的查询与未来悬停接入说明，本次未添加 UI 面板。
- 新增 5 项回归覆盖全牌组查询、能力修改后的牌面与实际计分一致性、失效数值保留、非计分踢脚牌以及溢出处理。
- 验证：Unity 编译 0 警告，PokerEngine 全模块 200/200 测试通过，控制台无错误；未执行完整 Player 构建。

### 2026-10-06 — 悬停改为面积放大惯性动画

- HandZoneView 悬停不再平移，改用独立 DOTween OutBack 将卡牌等比缩放至 √1.2（约 1.0954），目标面积增加 20%；移出鼠标惯性缩回。选中/取消选中仍沿原选中偏移上浮/回落，选中摆角同步归零，两种效果独立叠加、互不重播。
- 新增 Runtime/CardInstanceView.Hover.cs 及 .meta，沿用首包程序集；Face 下增加 Hover 视觉节点，独立推进、反向、静默恢复及清理。普通牌区不应用悬停缩放；弃牌保留实际起飞大小，跨区转移继续捕获完整姿态。射线使用实际缩放几何。
- 保留旧悬停偏移的序列化字段与接口兼容，旧值不再产生位移，HoveredOffset 返回零。验证工具移除悬停位移配置，显示面积 +20% 说明，共用现有时长和惯性设置；更新根、展示、交互 README。
- 更新原有交互回归，新增旧偏移/牌背/重排/普通牌区隔离与放大边缘命中测试。Unity 编译 0 警告，展示/交互 136/136 通过，控制台无错误；未保存场景或改写美术 Prefab。

### 2026-10-05 — 验证窗口逐行编辑卡牌属性

- 手牌与测试牌堆的每行直接提供牌面、正面、强化、塑封、失效、禁用、悬停、选中及乘色；移除下方单牌属性面板。移动到指定序号、上下移动、复制、清除效果、抽出和删除集中在行尾，序号悬停保留实例 ID。
- 表头与逐行控件共用列坐标，长列表纵向滚动、窄窗口横向滚动。沿用 Card 层属性校验、禁用牌选中限制、Undo/Redo、Play 副本及牌顶显示顺序。
- 更新根/模块 README，沿用 Editor/CardZoneValidationWindow.Lists.cs 及原 .meta，无新增资产。Unity 编译 0 警告，展示/交互 134/134 测试通过，控制台无错误；实际窗口验证首行强化勾选与撤销恢复，测试修改已撤销，未保存场景。

### 2026-10-05 — 测试牌堆首行对齐下一张抽牌

- 修复验证窗口按存储顺序显示、实际从列表末尾抽牌造成的方向不一致。测试牌堆改为牌顶在前，首行标记“牌顶（下一张）”；单抽取首行，批量依次取前 N 行，诊断中的牌堆前后对照采用相同方向。
- 新增 Editor/CardZonePreviewList 及 .meta，将移动、指定序号、复制、插入、删除与排序映射到显示顺序；相同牌面和无点数图案保持稳定排序。手牌保持自然顺序，Core 的末尾为牌顶约定及现有序列化数据不变。
- 更新根/模块 README，新增 3 项回归覆盖 Edit/Play 副本的单抽与批量顺序、属性/身份保留及列表编辑映射。展示/交互 134/134 测试通过；实际 Edit/Play 窗口已确认首行牌顶标记，原生按钮点击核验因自动化窗口定位错误未完成。未自动保存场景。

### 2026-10-05 — 测试牌堆延续到 Play 抽牌实验

- 新增 Editor/CardZoneDrawExperimentStore，按牌区保存编辑牌堆及独立 Play 副本。进入 Play 深复制牌序、身份、牌面和全部逐牌属性；抽牌消耗对应副本，切换牌区不再重建牌堆，退出后再进入恢复编辑配置。抽空或主动清空保持空堆，不生成默认牌补位。
- 两份状态和当前牌区标识均支持窗口序列化，解决旧的 Play 副本未序列化、目标切换直接重置，以及恢复过程中场景引用短暂缺失造成的上下文丢失。旧窗口现有牌堆迁移保留。
- 新窗口和动画实验菜单默认打开“从测试牌堆抽取”，界面明确当前来源与 Play 副本的消耗规则；“指定牌面”仍为显式生成新卡。测试状态留在 Editor 程序集，沿用 Core 转移和宿主 ReplaceCards，不改变运行时数据契约。
- 新增对应 .meta，更新根/模块 README；新增回归覆盖属性和身份保留、运行时消耗与编辑配置隔离、序列化/重复会话、空堆和切换目标恢复。
- Unity 编译 0 警告，展示/交互 131/131 测试通过；未自动保存场景。
- 补充实际 Play 验证：当前 52 张乱序测试牌堆经菜单及窗口按钮两次进入 Play 均保持 52 张，前部顺序与第 4 张梅花 8 的牌背属性保留；点击“牌顶抽 10 张”后手牌 10、牌堆 42，退出恢复手牌 0、牌堆 52，再进入仍为 52。此路径未复现牌堆丢失；未改写牌堆配置或保存场景。

### 2026-10-05 — 强化选中上浮的惯性回稳

- CardInstanceView 的选中上浮在公共惯性强度上乘 1.35（上限 3），默认最大越过量约 25%；从悬停进入选中时，明显越过选中高度再回稳固定。沿用 0.12 秒总时长，高度与自身摆角仍共用进度并同时回稳，基础强度为 0 时关闭全部回弹。
- 验证窗口补充选中增强说明；新增鼠标控制器点击链路回归，验证第二段上浮越界、鼠标离开/重复投影不中断，以及终点固定。同步更新摆角回归、README；沿用文件与 .meta，无新增资产。
- 当前手牌区已应用；Unity 编译 0 警告，展示/交互 128/128 测试通过。

### 2026-10-05 — 增强悬停与回落惯性

- 上浮/回落 Overshoot 默认从 1.2 提高到 2.2，越过目标的最大幅度约从本次位移的 5% 增至 15%；悬停、选中和回落共用新强度，仍在 0.12/0.14 秒内完成回稳。
- 同步更新快速惯性预设、模块 README 与现有双向回弹断言；沿用原文件及 .meta，无新增资产。
- 当前手牌区已应用新参数；Unity 编译 0 警告，展示/交互 127/127 测试通过。

### 2026-10-05 — 出牌先交叉落位，再整理排列

- 手牌按从左到右顺序依次起飞，本批 N 张暂时落到反向对应的目标空槽，形成交叉；早到卡牌等待本批最后一张到齐，再一起整理回原手牌顺序。支持 1–5 张，单张直接落位；保留五个固定槽位及已有出牌，数据列表从提交时起始终维持真实顺序。
- CardInstanceView 的跨区 Tween 使用统一时间轴覆盖飞行、等候与整理；新增 LandingPause（默认 0.08 秒）和 ReorderSeconds（默认 0.3 秒，旧场景零值兼容）。整理时对换牌有轻微错层抬高，奇数批次中间牌保持原位。连续批次独立等待，重复投影不重播，布局调整同时更新交叉与最终落点；立即完成和清空仍清理整段动画。
- 验证窗口“出牌路径”增加全员落位停顿、整理时长；Gizmo 区分绿色交叉飞行与黄色整理方向。沿用 Runtime/Editor/Tests 文件与 .meta，更新根与模块 README，无新增资产；未改写或保存场景文件。
- 验证：Unity 编译 0 警告，展示/交互 127/127 通过。新增 4 项回归覆盖 1–5 张的实际逆序落点与同步整理、已有牌和连续批次隔离、布局/中断，以及旧配置兼容和非法时长拒绝；未执行完整 Player 构建。

### 2026-10-05 — 加快悬停回落并加入双向惯性

- HandZoneView 上浮/回落默认总时长改为 0.12/0.14 秒，使用 DOTween OutBack：悬停、选中及两档切换均轻微越过目标后回稳。选中时自身摆角沿相同进度收回零，回稳后保持静止。
- CardZoneLiftSettings 新增 Overshoot（0–3，默认 1.2），支持关闭回弹并拒绝非有限或越界参数。反向从实际姿态接续，重复目标不重播，静默恢复与停用仍精确落位；不改变卡牌数据或上浮高度。
- 验证窗口“牌区样式 → 动画”增加惯性强度和快速预设，当前手牌区已应用；保留旧场景自定义值，不自动保存场景。更新 README，沿用原脚本与 .meta，无新增资产。
- 扩展回归验证双向越界、精确终点、回弹期间反向和选中摆角同步；Unity 编译 0 警告，展示/交互测试 123/123 通过。

### 2026-10-05 — 手牌到出牌区的数据转移与连续飞行

- 新增 CardZonePlayBatch / CardZonePlayHost.PlaySelected，按手牌顺序验证选中牌 CanPlay、目标五张容量和身份唯一性，一次提交两区新列表；清除移入牌的悬停/选中，保留原卡牌属性、可见性和来源。禁用牌整批拒绝，失效牌可打出；Play 初始快照独立。
- 新增 CardZoneTransferPresenter 与独立出牌路径设置。两区展示均预校验后转交相同 CardInstanceView，从实际世界位置、上浮、翻面和摆动姿态连续飞至目标槽；剩余手牌自然让位，避免误触发抽牌/弃牌音效。默认 DOTween 飞行 0.4 秒、间隔 0.06 秒、目标 +Y 弧高 0.02，可选 Transform 曲线控制点。
- CardZoneView 将快照同步提取为可预校验的 Prepared 更新；CardInstanceView 新增独立 Transfer 偏移和时钟。目标管理飞行实例，重复快照/连续批次不重启动画，布局和目标移动更新终点。路径停用/销毁立即落位，目标 Clear 释放视觉；动画中断不撤销数据归属。等比缩放与相同卡牌 Prefab 为跨区复用契约。
- 验证窗口独立“出牌路径”页支持选区、新建空出牌区并连接、配置曲线、提交、暂停/单步、立即完成和 Gizmo；Play 在 Game 选牌，编辑模式通过逐牌状态选择并可一起撤销两区数据。新增 Runtime/Editor/Tests 文件及 .meta，更新根/模块 README，运行时继续进入首包；不自动保存场景。

- 验证：Unity 编译 0 警告，展示/交互 123/123 测试通过，新增 8 项跨区转移回归；控制台无错误。修复预览撤销过程中 Transform 禁写问题。当前场景新增空的 PlayedZoneView Transfer Preview 并连接测试手牌，未自动保存场景；未执行完整 Player 构建。

### 2026-10-05 — 抽牌音效改为新牌入列触发

- HandZoneView 的抽牌音效从起飞调整为飞入结束、进入排列并开始惯性回稳时播放，每张一次；默认在该牌起飞 0.45 秒后触发，无须等待回稳结束。新增 CardEnteredZone 通知，CardDealStarted 保留起飞通知但不再播放音效。
- 飞行/回稳中重排、布局调整和重复快照不重复触发；大帧跨过完整动画仍保留一次提示。入列前移除或弃掉、静默恢复、立即完成和停用不补播；关闭列表动画的即时新增仍触发。
- 弃牌、选中音效的触发时机与三组独立配置保持不变。更新验证窗口说明、README 和现有时序/运行时/弃牌回归，沿用脚本和资产 .meta。
- 验证：Unity 编译 0 警告，展示/交互 114/114 测试通过，控制台无错误。

### 2026-10-05 — PlayedZoneView 五槽直排出牌区

- 新增 PlayedZoneView，直接派生 CardZoneView，默认 Kind 为 played。按五个固定槽位从左到右展示 0–5 张真实卡牌，空位只保留位置；提供 GetSlotPoses 读取全部槽位。间距可压缩，宽度不足五张完整牌时拒绝配置，卡牌不重叠。
- 基类增加布局计算扩展点与 CardCapacity / SupportsArc / SupportsSway 能力声明。出牌区的弧度、持续摆动始终关闭，所有设置入口与序列化恢复遵循约束；卡牌属性/可见性继续驱动外观。当前更新直接落位，不自动绑定手牌输入或音效，尚未接入跨区出牌转移动画。
- Present 在修改数据/视觉前拒绝第六张；CardZonePlayHost 的 Capture/BeginSession 也在替换快照或停止旧会话前验证容量，失败保留原运行状态。Core CardZone 数据契约不变。
- 验证窗口新增创建出牌区按钮和菜单，生成五张基础示例并自动匹配 played 类型。列表新增/复制及预设遵循五张容量，隐藏弧度/摆动选项，Scene Gizmo 展示五个固定槽位；沿用现有 Prefab 配置导出。
- 新增 Runtime/PlayedZoneView.cs、Editor/CardZoneValidationWindow.Played.cs、测试及 .meta，更新根与模块 README；运行时继续归入首包程序集。未改写既有场景文件。
- 验证：Unity 编译 0 警告，展示/交互 114/114 测试通过，新增 6 项出牌区、容量、工具与导出回归。未执行完整 Player 构建。

### 2026-10-05 — 牌区 View 配置导出为 Prefab

- 验证窗口顶部新增“导出 View Prefab…”与“定位 Prefab”，默认输出到 Assets/CardPresentation/Prefabs。保存 CardZoneView / HandZoneView 及进一步派生类的实际类型和序列化展示设置，可捕获当前编辑或 Play 中调好的参数。
- 导出保留卡牌 Prefab 和共享音频资源、布局/动画/外观配置；排除验证宿主、鼠标组件、卡牌数据、临时视觉、音源与其他场景子对象。抽牌/弃牌锚点转换为局部位置，相机引用清空并沿用 MainCamera；根位置归零，保留原始本地旋转与缩放，清理相机跟随缓存。
- 在隔离 Preview Scene 中复制并校验，不改动原牌区或进行中动画，不保存/替换场景实例。子类自身引用重映射，未知场景依赖或临时资源写入前拒绝。
- 同名资源需明确覆盖，仅接受相同具体子类的普通牌区 Prefab，保留 GUID；拒绝覆盖单张卡牌、异类资源或越出 Assets 的路径。新增 Editor 导出器/窗口 partial、Prefab 目录及对应 .meta，更新根与模块 README。
- 验证：Unity 编译 0 警告，展示/交互 108/108 测试通过；新增 5 项导出/实例化/子类与引用/覆盖保护回归。未执行完整 Player 构建。

### 2026-10-05 — 通用牌区与 HandZoneView 派生

- 抽出通用 CardZoneView：复用有序视觉实例、同步牌面/属性与可见性、布局/相机朝向/摆动及生命周期；新增 HandZoneView 派生类承接抽牌/翻面/惯性、重排、悬停/选中上浮、弃牌飞出和三组音效。基类引用仍动态使用具体子类效果，原功能回归保持通过。
- 弃牌区保留 Core 有序数据与虚拟终点，无独立弃牌 View。CardZonePlayHost 仅为 HandZoneView 及其子类自动绑定鼠标，通用牌区不自动引入手牌输入；通用牌区调用 DiscardSelected 在更改数据前拒绝。
- 验证窗口增加目标牌区选择、实际类型显示和跟随场景选择，匹配基类及任意子类，包括非激活/嵌套牌区。手牌专用配置按能力显示；切换目标读取各自卡牌源与配置，保留 ZoneId/Kind/ControllerId，避免跨区数据覆盖；未知真实卡牌源的外部投影只读。
- 默认生成 HandZoneView；原脚本 GUID 随主脚本及 .meta 迁移至 HandZoneView，已有场景组件与参数无需重建。HandZoneView.Audio/Discard partial 归入运行时目录，共用基类使用新 GUID；运行时继续进入首包，测试派生辅助程序集仅用于测试。
- 更新根/模块 README、资源结构与 .meta。新增 7 项继承/工具隔离/迁移测试；Unity 编译 0 警告，展示/交互 103/103 通过。当前场景确认组件为 HandZoneView，保留 8 张牌、原锚点、两档上浮与音量配置；未执行完整 Player 构建。

### 2026-10-05 — 选中音效与独立配置

- CardZoneView 比较同牌区、同卡牌身份的前后快照，仅在 Interaction.IsSelected 从 false 变为 true 时发出 CardSelected 并播放一次；悬停、取消选中、排序、重复刷新、初始选中及静默恢复不播放，关闭视觉动画仍可触发。
- 新增 SetSelectionSound 与对应只读属性，独立配置开关、音量及替换音频，默认使用现有 CardDraw.wav。选中拥有独立 2D AudioSource，与抽牌/弃牌互不静音或截断尾音；清空、停用与销毁统一清理三路音源。
- 验证窗口“牌区样式 → 音效”增加选中音效区块，支持读取、应用及固化；继续由逐牌数据驱动表现，不直接在鼠标控制器播放，也不新增 Core/存档字段。
- 新增 3 项选中音效回归，覆盖实际鼠标控制器、快照边沿/静默恢复、独立配置/序列化/非法值；更新 README，沿用现有脚本、音频及 .meta。
- 验证：Unity 编译 0 警告，展示/交互 96/96 测试通过，控制台无错误。

### 2026-10-05 — 抽牌与弃牌音效独立配置

- 抽牌继续使用 SetDrawSound，新增 SetDiscardSound 及对应只读属性；两组分别保存启用状态、替换音频与音量，留空均回退到现有 CardDraw.wav 占位资源。
- 各自按需创建 2D AudioSource，独立播放/静音；关闭或归零其中一组不会截断另一组的尾音。牌区清空、停用和销毁统一清理两路，逐张触发时序保持不变。
- 验证窗口“牌区样式 → 音效”拆成抽牌音效和弃牌音效两个独立区块，分别读取、应用与固化配置；更新 README，沿用现有资产与 .meta。
- 新增独立配置、序列化恢复及非法音量拒绝回归。
- 验证：Unity 编译 0 警告，展示/交互 93/93 测试通过，控制台无错误。

### 2026-10-05 — 弃牌复用默认占位音效

- 每张弃牌实际开始飞出时触发一次 CardDiscardStarted，并复用抽牌的默认 CardDraw.wav、音量/开关和同一个 2D AudioSource.PlayOneShot；无需复制音频资源，连续抽牌/弃牌保留尾音。
- 音效提示在新手牌列表提交后、离场实例销毁前消费；隐藏牌同样触发，长帧跨过起飞和到达不会漏播。重复刷新不重播，静默恢复、起飞前取消和立即完成不补播；仅关闭列表动画时，即时弃牌仍逐张提示。
- 验证窗口音效页改为“抽牌 / 弃牌音效”，明确配置共用；更新模块及根 README，沿用既有脚本、音频及 .meta。
- 新增 2 项弃牌音效回归，并补验发牌途中弃牌的独立提示次数。
- 验证：Unity 编译 0 警告，展示/交互 92/92 测试通过，控制台无错误。

### 2026-10-05 — 选中弃牌与翻面飞出

- 新增本地 `CardZoneDiscardBatch` 与 `CardZonePlayHost.DiscardSelected`：读取逐牌选中状态，按原手牌顺序通过 Core.TransferRangeTo 转移到逻辑弃牌区。禁用牌整批拒绝，失效牌可弃；清除弃牌交互，保留身份、来源、效果、可见性和颜色，初始快照独立。
- CardZoneView 消费新列表与本次弃牌身份，立即更新 Data/Views/鼠标命中范围，其余牌自然补位；离场实例由独立集合维护。普通编辑删除仍立即释放，避免把其他移除原因当作弃牌。
- DOTween 从当前上浮、摆动及翻面姿态接续飞出，可见牌途中翻到牌背，隐藏牌保持牌背；默认 0.45 秒、逐张间隔 0.08 秒，到达直接销毁，无落地回弹、不触发抽牌音效。虚拟弃牌堆默认在左侧，可指定局部坐标或场景锚点，使用粉红色 Gizmo。
- 覆盖连续弃牌、整手弃空、入列途中弃牌、未起飞牌取消、重复刷新、同身份恢复与停用/清空等生命周期；仅清理自身 Tween。
- 验证窗口增加弃掉选中按钮、弃牌数量/身份诊断、独立动画参数；编辑操作支持 Undo/Redo，Play 操作独立于初始数据，手牌为空时仍推进离场动画。新增源码/测试 .meta，运行时继续纳入首包，更新模块与根 README。
- 验证：Unity 编译 0 警告，展示/交互 90/90 测试通过（新增 9 项弃牌回归）。

### 2026-10-04 — 牌区验证窗口分类与任意列表编辑 / 抽牌

- 窗口重组为手牌列表、抽牌实验、牌区样式、诊断四页；固定状态与导航，长列表使用紧凑行和单牌编辑面板，样式另分布局、动画、音效、外观，预设及详细记录移入对应区域。
- 手牌和测试牌堆支持任意位置插入、修改牌面及属性、复制、移动到指定序号、上下移动、排序、反转、打乱、删除和清空。复制产生新身份并保留规则/外观数据，清空瞬时交互。
- 可指定任意牌面、数量、可见性和入列位置直接抽牌，测试牌堆为空仍可操作；也能从牌堆任意一张抽出原实例。全部沿用 Core 列表转移和既有 View 动画/音效，历史记录区分两种来源。
- Play 中开放同样的列表/样式操作，手牌通过运行时宿主更新，牌堆使用独立副本；鼠标交互继续消费新列表，退出恢复编辑配置。编辑模式按操作统一合并 Undo/Redo 与固化，并在撤销恢复时避免新增撤销记录；补齐来源信息与实时悬停保留。
- 新增列表编辑 UI partial 及 .meta，维护模块/根 README；新增 6 项列表与任意抽牌回归。
- 验证：Unity 编译 0 警告，展示/交互 81/81 测试通过；实际窗口检查分类与列表编辑，Play 抽牌确认运行时列表和视觉同步增加，初始快照保持不变。

### 2026-10-04 — 抽牌逐张触发音效

- 将用户指定的 Epic Stock Media 卡牌音效按字节复制并重命名为 `Assets/CardPresentation/Resources/CardPresentation/Audio/CardDraw.wav`，随首包提供；导入配置为 48 kHz PCM、Decompress On Load 和预加载，保留原 WAV。
- 每张牌实际起飞后仅发出一次 CardDealStarted，并由牌区自有的 2D AudioSource.PlayOneShot 播放；连续抽牌允许尾音重叠。隐藏牌同样触发，刷新、重排和飞行中调整不重播；静默恢复和起飞前取消不补播，关闭动画的新增牌仍逐张触发。
- CardZoneView 增加音效开关、替换 Clip 和音量接口；动画实验页可配置并在 Play 中试听。清空/停用停止音源，销毁时清理；无需修改 Card/Core/存档契约。
- 验证窗口增加 Play 抽牌试听入口，独立复制测试牌堆，经 CardZonePlayHost.ReplaceCards 更新运行时手牌并保留鼠标交互；进入/退出 Play 重新绑定场景实例，退出恢复初始数据。
- 增加逐张时序、防重复、取消/恢复、音频配置及运行时抽牌集成共 4 项回归；维护新资源、脚本的 .meta 和模块/根 README。
- 验证：Unity 编译 0 警告，展示/交互 75/75 测试通过；Play 中实际重建测试牌堆并连续抽 3 张，确认运行时创建唯一的 2D、非循环、非自动播放音源。

### 2026-10-04 — 发牌飞入时由牌背翻到正面

- 新增运行时 DealFlip 视觉节点，新增牌从发牌区背面朝上飞出；观察者可见的牌在 FlightSeconds 内由附加 Z 旋转 180° 平滑转到 0°，到达时翻正后进入惯性回稳。不可见牌维持牌背，不执行翻正。
- 从生成时即绑定最终卡牌投影、牌面和 Shader 参数；飞入翻面只写视觉 Transform，不改卡牌效果、禁用、可见性或交互属性。隐藏牌继续使用过滤后的投影。
- 翻面沿用现有飞入 DOTween 时间轴；重复快照不重播，飞行中重排/追加/换布局保留实际角度并在剩余飞行时间内完成。立即恢复、完成动画、停用/销毁复用现有清理流程。
- 动画实验页增加翻面角度/数据可见性对照和说明；增加实际朝向、数据/Shader 不变及动画接续回归。更新 README，沿用现有源码及 .meta；无新的 Core 或存档字段。
- 验证：Unity 编译 0 警告，展示/交互 71/71 测试通过，控制台无错误；生命周期回归同时确认翻面偏移清理。

### 2026-10-04 — 选中第二段上浮同步将自身摆动归零

- CardInstanceView 收到选中投影时停止持续摆动，并从实际角度向零过渡。与悬停 → 选中的第二段上浮共用同一个 DOTween OutCubic 进度，高度到达 0.02 时摆角同时归零，随后保持静止；悬停高度仍为 0.01。
- 重复快照或选中期间鼠标进出不重播；取消选中从实际角度恢复持续摆动，再次选中从当前高度/角度接续。首次恢复、关闭动画或停用时直接落位归零。
- 仅归零附加摆动，圆弧布局朝向、正反面保持独立。新增同进度插值及中途反向回归，调整旧测试预期并更新模块/根 README；沿用现有脚本及 .meta，数据层和存档契约不变。
- 验证：Unity 编译 0 警告，展示/交互 69/69 测试通过，控制台无错误。

### 2026-10-04 — 悬停高度微调为 0.01

- 默认及当前验证牌区的悬停偏移改为局部 +Z 0.01，选中保持 +Z 0.02。同步编辑器默认值、方向按钮兜底值、既有测试预期和 README；选中暂停摆动及两档 DOTween 过渡保持不变。
- 验证：Unity 编译 0 警告，展示/交互 68/68 测试通过；已检查当前牌区两档偏移为 0.01 / 0.02。

### 2026-10-04 — 区分悬停与选中上浮高度

#### Changed

- 修正上一轮对“悬浮矮一点”的理解：默认悬停保持局部 +Z 0.015，选中恢复为 +Z 0.02；两者同时存在以选中为准，不叠加。选中暂停自身摆动的行为保留。
- CardZoneView 增加 HoveredOffset / SelectedOffset 和 SetInteractionOffsets；验证窗口“布局与样式”分开编辑两档，并随牌区配置固化到 Play。旧 RaisedOffset 对应选中偏移，SetRaisedOffset 仍可将两档同时设为同值。
- DOTween 从当前偏移接续目标，悬停转选中继续上浮，取消选中但仍悬停时按回落时长退到低档；选中期间鼠标进出不重播。扩展已有测试验证两档优先级、连续过渡、鼠标状态及非法配置原子拒绝。
- 更新 README 与 changelog，沿用既有脚本和 .meta；Core、卡牌状态及权威存档契约不变。

#### Validation

- Unity 编译 0 警告，展示/交互 68/68 同步 NUnit 方法通过，控制台无错误。当前验证牌区已配置悬停 `(0, 0, 0.015)`、选中 `(0, 0, 0.02)`。

### 2026-10-04 — 降低上浮高度，选中暂停自身摆动

#### Changed

- 默认悬停/选中共享上浮偏移从局部 +Z 0.02 降为 0.015，降低 25%；同步调整验证窗口默认值、Z 方向按钮的兜底值，并应用到当前验证牌区。
- CardInstanceView 根据卡牌投影中的 IsSelected 暂停自身 local Y 摆动，保留当前角度及 Tween 相位；取消选中继续同一 Tween，初始选中保持中立角度。上浮/回落与入列/让位动画独立推进，悬停仍可摆动。
- 扩展既有动效回归，验证选中暂停、其他牌继续摆动、刷新不重播和取消后续播；更新模块及根 README。沿用既有资源与 .meta，Core/存档契约不变。

#### Validation

- Unity 编译 0 警告，展示/交互 68/68 同步 NUnit 方法通过；当前验证牌区偏移已确认是 `(0, 0, 0.015)`。

### 2026-10-04 — 上浮 DOTween 与固化牌区 Play 验证（首包）

#### Added

- 新增 CardZoneLiftSettings，默认使用 DOTween OutCubic 上浮 0.18 秒、回落 0.22 秒，可独立启停及配置时长；单牌提供实际 InteractionOffset / IsLifting，方向保持牌区局部 +Z。
- 新增运行时 CardZoneCardState 与 CardZonePlayHost，序列化完整有序卡牌启动快照。悬停/选中仍由逐牌数据持有；Play 复制初始数据、重建牌面并自动绑定鼠标，关闭验证窗口也能运行。宿主及模型留在 CardInteraction.Runtime，随原 asmdef/link.xml 纳入首包。
- 验证窗口新增“固化当前牌区”“固化并进入 Play”、上浮/回落时长，菜单新增 Bake Card Zone For Play 与 Play Card Zone。旧 Preview Mouse Interaction 保留为固化菜单别名。

#### Changed

- 移除 Scene 鼠标事件注册和点击处理，编辑器鼠标输入仅在 Play 的 Game 视图生效。牌区根对象由 EditorOnly 改为普通 Untagged 对象；卡牌数据与配置存于场景组件，临时卡面继续使用 DontSave 并由运行时重建。
- 悬停转选中共用动画目标、不叠加或重播；中途反向从实际偏移接续。上浮与入列/让位/摆动分别持有 Tween，沿用牌区手动时钟；停用、清理和销毁回收所属动画，animate: false 立即恢复。
- 稳定悬停命中范围使用实际动画偏移；弧形重排、翻面与缩放不改变上浮轴向。固化清除瞬时悬停并保留初始选中，每次 Play 独立复制，运行时选择不覆盖编辑快照；重新打开验证窗口从宿主恢复身份/顺序/属性。
- 新脚本维护 .meta，更新展示模块、交互模块及根 README。Core 和权威存档契约不变；工具不自动保存整个场景。

#### Validation

- Unity 编译 0 警告，展示/交互 **68/68 同步 NUnit 方法通过**。6 项新增测试覆盖 Tween 连续性、状态切换、局部 Z、立即恢复与生命周期隔离、快照序列化/运行时副本、重复启动、非法输入原子拒绝及编辑器恢复。
- 已进入实际 Play 验证当前 12 张卡牌由宿主重建、运行时输入自动绑定，控制台无错误；未执行完整 Player/IL2CPP 构建。

### 2026-10-04 — 卡牌鼠标悬停/选中与局部 Z 上浮（首包）

#### Added

- 新增独立 `Assets/CardInteraction/Runtime` 输入模块及 asmdef/link.xml。`CardZoneMouseInput` 读取鼠标左键，`CardZonePointerController` 处理悬停与同牌按下/松开，`ICardInteractionSource` 将状态写回每张本地卡牌；宿主在 StateChanged 后投影给 CardZoneView。展示模块不反向依赖输入模块，控制器不另存选中集合。
- `CardZoneRaycaster` 以世界射线测试实际卡牌变换后的 Mesh bounds，处理重叠距离/顺序、上浮后的稳定悬停、旋转缩放和牌背；飞入/回稳中、隐藏渲染器或停用区域不参与命中。继续禁用美术实例的物理 Collider。
- 支持移出/失焦/UI 遮挡/解绑时取消悬停和未完成点击，保留卡牌选中。验证模型禁止新增选中禁用牌，允许取消已选中的禁用牌；失效牌仍可选中。GetSelectedViewIds 按当前 Zone 顺序读取待提交身份，不直接提交出牌或移动卡牌。
- 验证窗口增加 Scene 鼠标开关、待提交数量和 +Z 偏移按钮；`Tools > Cards > Preview Mouse Interaction` 启用编辑模式验证，左键选中支持 Undo/Redo，Alt/中右键保留 Scene 导航。运行时组件由正式宿主绑定。

#### Changed

- 悬停/选中默认偏移改为牌区局部 `(0, 0, 0.02)`，两者成立时仍只上浮一次；圆弧朝向、翻面与摆动不改变偏移方向。当前验证牌区通过菜单切换到 +Z，已有序列化配置不批量覆盖。
- PreviewCard 保留悬停/选中字段并提供 Interaction 读写；CardZonePreviewModel 实现交互数据接口，继续经 Card → Zone 投影 → View/Shader 驱动显示。Core 规则卡牌及存档契约不变。
- 新模块、编辑器适配和测试脚本维护完整 .meta；新增交互 README，更新展示模块和根 README。

#### Validation

- Unity 编译 0 警告；展示/交互 **62/62 同步 NUnit 方法通过**。7 项新测试覆盖卡牌数据写回、Z 位移、悬停稳定、重叠优先级、禁用/失效、点击取消、排序/移除、变换/牌背、入列屏蔽和运行时解绑。
- 原有动效测试调整为在牌区坐标验证上浮，并扣除交互偏移验证基础布局边界；首包测试覆盖交互程序集、link.xml 和两个模块的元数据。
- 未保存或替换当前场景；未执行完整 Player/IL2CPP 构建。本次完成待提交选牌，出牌目的地与提交流程后续接入。

### 2026-10-04 — CardZone 花色优先与点数优先排列（首包）

#### Added

- Core CardZoneOperations 新增 `SortBySuit(resolveDefinition)` 和 `SortByRank(resolveDefinition)`，返回新的有序 Zone。花色次序为黑桃 → 红桃 → 梅花 → 方块，点数为 A、K、Q、J、10…2；两个接口互换主/次排序键，同牌面稳定保序。
- 排序通过调用者提供的定义解析器读取固有花色/点数，每个实例牌解析一次；显式 null 表示无标准排序数据，稳定放在末尾。解析失败不提交部分结果，原快照、Zone 元数据、卡牌身份/属性和随机状态保持不变。
- 验证模型接入同名排序方法，“卡牌属性”和“动画实验”页均暴露两个按钮，支持 Undo/Redo。非标准王牌美术样例归尾；选中、悬停、可见性和效果跟随原卡牌身份保留。
- 排序后的 Core 列表通过 Present 触发 View 重排，复用已有视觉实例；重排中再次排序从当前姿态接续，不重新从牌堆发牌。接口为显式单次操作，后续抽牌不会自动重排。
- 更新引擎/展示模块及根 README；修改既有脚本并保留 .meta，无新增资源或存档字段。

#### Validation

- Unity 编译 0 警告；引擎 **195/195**、展示模块 **55/55** 同步 NUnit 方法通过，控制台无错误。
- 新增 4 项测试覆盖完整 52 张标准牌的双排序、重复牌面及无标准数据的稳定性、失败原子性、属性/序列化保留，以及动画中连续排序的实例复用和姿态衔接。
- 已检查动画实验页的两种排序按钮；未保存或替换当前场景，未执行完整 Player/IL2CPP 构建。

### 2026-10-04 — 放缓并柔化卡牌落位惯性

#### Changed

- 默认回稳从 0.32 秒延长到 0.65 秒，过冲距离从 0.008 降至 0.006，摆角从 5° 降至 3°。回摆从一圈半改为一圈衰减，呈现一次主要回摆和微弱余摆。
- 飞行抬高改为正弦平方，回稳包络首尾斜率归零，使飞入到回稳的位移/转动速度平滑衔接，消除突然制动与反向。
- 动画实验参数区新增“应用柔和惯性”按钮和 `Tools > Cards > Preview Soft Landing` 菜单，仅调整上述三个参数并支持 Undo。已应用到当前验证牌区，保留 15 张手牌、牌堆及其他配置；既有序列化配置不自动迁移。
- 更新模块和根 README；无新增资源文件，Core/存档契约不变。

#### Validation

- Unity 编译 0 警告，展示模块 **53/53 同步 NUnit 方法通过**，控制台无错误。新增回归按帧采样飞入/回稳交界及结束时的位移与转动速度，验证没有突跳并最终准确落位。
- 未保存或替换当前场景，未执行完整 Player/IL2CPP 构建。

### 2026-10-04 — 牌区验证独立动画实验页

#### Added

- Card Zone Validator 新增第四页“动画实验”，以及 `Tools > Cards > Card Zone Animation Lab` 直达入口。支持抽 1/3/指定张数、牌堆剩余与后续顺序、重建 12 张测试牌堆、清空目标 Zone。
- 新增 Editor-only CardZoneDrawExperiment；通过 CardZonePreviewModel.DrawFromDeck 调用 Core.DrawTo，将牌堆末尾的原卡牌按顺序移入目标 Zone，保留身份与属性。完整验证后同步提交两个列表，记录抽牌前后的列表和抽取顺序，再由 Present 驱动 View。
- 动画时钟提供播放/暂停、0.1–2 倍速、暂停并推进 0.05 秒、立即完成入列/让位；可对照每张牌的等待/飞入回稳/让位/落位状态与实际/目标位置。暂停或飞行中均可继续抽牌，观察数据与画面的不同步阶段。

#### Changed

- 抽牌、牌堆位置、过渡/惯性及持续摆动参数集中到动画实验页；样例生成/重排按钮移至卡牌属性页。布局与样式页保留区域、相机朝向和 Shader 配置。
- 牌堆、手牌及最近抽牌记录共同序列化，Undo/Redo 同步恢复；切页和脚本重载保留现有手牌，抽空后不自动补满。补 3 张菜单改为真实牌堆转移，数量或容量不足时拒绝整次操作。
- 新增 Editor/测试脚本及对应 .meta，更新模块和根 README；运行时引擎与存档契约不变。

#### Validation

- Unity 编译 0 警告；展示模块 **52/52 同步 NUnit 方法通过**。新增 4 项测试覆盖抽牌顺序、身份/属性保留、序列化恢复、非法操作原子拒绝及列表变化到 View 单步动画的完整链路。
- 已检查并打开独立动画实验页，保留当前 10 张样例和已有配置；未保存或替换当前场景。未执行完整 Player/IL2CPP 构建。

### 2026-10-04 — CardZone 从牌堆补牌、入列惯性与让位（首包）

#### Added

- CardZoneView 消费完整列表快照，按同一 ZoneId 下的稳定 ViewId 识别新增/移除/重排。新增牌从右侧牌堆位置按顺序错开发射，抬高减速飞入、轻微过冲并衰减回摆；已有牌向新槽位平滑让位。规则增删仍使用 Core CardZoneOperations，不由视图生成或修改实例牌。
- 新增 CardZoneTransitionSettings：默认飞入 0.45 秒、逐张间隔 0.12 秒、回稳 0.32 秒、让位 0.25 秒，支持飞行抬高、过冲距离和落位摆角调参。与原有持续摆动分别启停。
- SetDeckSource 支持牌区局部位置与可选场景 Transform 锚点，DeckWorldPosition 返回当前来源，选中牌区可见橙色牌堆 Gizmo。默认局部位置为 (0.38, 0.035, 0)；锚点在接收新增快照时采样。
- Present / SetLayout 增加 animate 参数；false 直接恢复目标布局。CompleteTransitions 立即完成当前过渡，牌区/单牌可查询 IsTransitioning，单牌可查询 IsEntering 与实际 DisplayPose。
- 验证窗口增加从牌堆补 1/3 张、立即完成动画、牌堆来源与飞入/惯性/让位配置；新增 `Tools > Cards > Deal Three Cards To Zone`。补牌先改 Card/Zone 样例再提交 Present；恢复/重载/Undo 使用立即同步。

#### Changed

- CardInstanceView 增加独立 Transition 子节点，目标圆弧、临时过渡、正反面/上浮和持续摆动分层。飞行使用正向美术姿态插值，保留圆心轴向与既有牌面方向补偿。
- 重复列表及属性刷新不重播动画；飞行中目标变化从当前显示姿态接续，等待中的牌保留发射时刻，追加批次沿用逐张间隔。移除先立即清理对应实例并让其余牌补位；离场目的地动画尚未接入。
- 使用私有 DOTween ID 和现有牌区手动时钟，停用时落稳、移除/销毁/Clear 时清理所属 Tween。异常重建已有身份的视觉实例不会误触发补牌，隐藏牌继续仅消费过滤后的投影。
- 新运行时/测试脚本及 .meta 已纳入原有程序集与资源目录；更新模块及根 README，Core/存档契约不变。

#### Validation

- Unity 编译 0 警告；展示模块 **48/48 同步 NUnit 方法通过**，报告位于 `Library/CardPresentationTests/results.xml`，控制台无错误。
- 8 项新增测试覆盖逐张时序、过冲/回稳、重入快照、飞行中追加/删除/重排、已有牌让位、场景锚点换算、Core 列表消费、隐藏牌、立即恢复、非法输入原子拒绝及 Tween 生命周期/隔离。原有布局测试显式关闭过渡以继续校验目标布局契约。
- 已检查验证窗口补牌按钮与调参控件；保留当前 10 张样例及用户参数，未保存或替换当前场景。未执行完整 Player/IL2CPP 构建。

### 2026-10-04 — 修复圆弧牌面上下颠倒

#### Fixed

- 正弧高的布局根节点为朝向圆心会旋转约 Y 180°，此前牌面直接继承该旋转而倒置。CardInstanceView 现在在 Face 子节点补偿半圈，使牌面与直排保持相同阅读方向，布局根节点 +Z 仍指向圆心。
- Present 与 SetLayout 统一通过 ApplyLayoutPose 更新布局与牌面朝向；补偿与正反面状态组合，独立 Sway 节点继续保留相位，悬停/选中仍使用牌区坐标。
- 更新验证窗口说明及模块/根 README，无新增资源或存档字段。

#### Validation

- Unity 编译 0 警告，展示模块 **40/40 同步 NUnit 方法通过**；新增回归覆盖正负弧高/零弧高、单张/多张、翻面及原实例复用，补充相机朝向下牌面阅读方向检查。
- 已检查当前 Game 视图，文字及图案恢复正向。保留用户的牌区参数与样例，未保存或替换场景；未执行 Player/IL2CPP 构建。

### 2026-10-04 — CardZone 圆心朝向与可选相机朝向（首包）

#### Changed

- 将浅抛物线改为局部 XZ 圆弧：卡牌等角排列，每张牌的基础 local +Z 指向同一圆心。新增 CardZoneCardPose / CalculatePoses，原 Calculate 保留位置输出；Present 与重排同时应用位置和基础旋转。
- 保留弧高正负约定：正值中间凸向 +Z，圆心位于 −Z；负值反向。布局为扇形旋转与持续摆动预留完整投影空间，空间不足时减小弧度；单张/零弧高退化为直排，两张牌也可各自朝向圆心。
- 悬停/选中偏移继续使用牌区局部坐标，不随逐牌基础旋转改变方向。独立 Sway 节点保留摆动相位，翻面与 Shader 属性照常消费投影。

#### Added

- CardZoneView 增加 FaceCamera / FacingCamera、SetCameraFacing 与 RefreshCameraFacing。选项默认关闭；开启后整区 +Y 朝向相机位置，+Z 沿相机上方向，未指定时使用 Camera.main。运行时 LateUpdate 与编辑器预览持续跟随，独立于牌数和摆动开关。
- 关闭选项或停用组件/对象后恢复原局部旋转；相机缺失/重合时保留当前朝向，相机 up 与观察方向平行时使用 forward 参考。
- 牌区验证“布局与样式”页增加朝向开关、Camera 选择及弧高方向说明，设置支持 Undo/Redo。新增相机测试脚本及 .meta，同步更新模块与根 README；不改 Core 或存档契约。

#### Validation

- Unity 编译 0 警告，展示模块 **39/39 同步 NUnit 方法通过**，报告位于 `Library/CardPresentationTests/results.xml`。
- 覆盖同圆等半径/+Z 指向圆心、正负弧高与旋转后边界、自定义上浮偏移、空牌区/关闭摆动时相机跟随、父级旋转缩放、退化方向、禁用后恢复，以及实例/牌面/属性/Tween 相位保持。
- 已检查验证窗口新增控件；保留当前样例与用户参数，未保存或替换当前场景。未执行完整 Player/IL2CPP 构建。

### 2026-10-04 — 自定义编辑器窗口拖动与缩放

#### Changed

- 卡牌工具（含图集预览与 Prefab 导出）及牌区验证共用 Editor-only `ToolWindowChrome`，增加始终可见的底部移动条和右下角缩放手柄，作为 macOS 原生窄边框的备用操作入口。停靠或最大化时停用这两个手柄，继续使用 Unity 标签页与分隔线。
- 统一最小尺寸为 360 × 260 UI 点，打开和脚本重载时重新应用尺寸范围。牌区验证由内容区滚动改为整页滚动，标签宽度随窗口调整，避免固定工具栏挤掉短窗口的全部内容。
- 拖动按屏幕坐标计算，缩放保留窗口原点；松开、失焦和禁用时释放鼠标捕获，Esc 取消并恢复本次拖动前的位置及尺寸。

#### Validation

- Unity 编译通过。4 项 EditorWindow 事件测试通过，覆盖双轴缩放与上下限、连续移动与 Esc 恢复、鼠标释放/失焦清理，以及内容区拖动隔离；报告位于 `Library/EditorWindowChecks/results.txt`。
- 为保留当前未保存场景，取消标准 Test Runner 的保存提示，使用临时编辑器调度器逐项执行测试，完成后移除调度器。已检查两个工具窗口的底栏显示；未确认 macOS 原生边框拖动问题的系统层原因。

### 2026-10-04 — CardZone 轻弧布局与 DOTween 摆动（首包）

#### Added

- CardZoneLayoutSettings 增加 ArcHeight，牌列中心沿局部 XZ 浅抛物线排布。默认弧高 0.008，支持正负方向与 0 直排；按可用深度限制弧度，空牌区及一/两张牌保持直排。
- CardZoneMotionSettings 配置 local Y 摆动，默认最大角度 1.5°、完整周期 3.5–6.5 秒。按 ViewId 与表现种子分别生成幅度、周期、相位，不消耗引擎或 Unity 全局随机数。
- CardInstanceView 增加独立 Sway 子节点，使用已有 DOTween 1.3.030 的 InOutSine / Yoyo 持续摆动；与 Face 的翻面/上浮及根节点布局分开。牌区 Update 推进自有 Tween，编辑器通过同一入口按自己的时钟预览。
- 验证窗口“布局与样式”页增加弧高、摆动开关/角度/周期/种子、播放预览、轻弧预设和恢复静态直排；新增 `Tools > Cards > Preview Gentle Row Motion`。预设保留现有卡牌与其他布局参数。

#### Changed

- 布局计算预留最大摆角的保守旋转空间，避免持续摆动导致牌面越出基础矩形。新增 SetMotionSettings、AdvanceMotion 和同时更新布局/动效的 SetLayout 重载，完整校验后应用。
- 相同身份/配置的 Present、重排、换牌面和布局刷新保留 Tween 相位；改变配置、停用、移除、销毁、Clear 与预览重载清理所属 Tween。使用私有 Tween ID 兼容编辑模式初始化前的清理，不调用全局 ManualUpdate/KillAll。
- Runtime asmdef 显式引用 DOTween.dll，测试程序集增加对应引用；新增脚本 `.meta` 及模块/根 README，纯 C# Core 和快照契约保持不变。

#### Validation

- Unity 编译 0 警告；展示模块 **33/33 同步 NUnit 方法通过**，控制台无错误/警告，报告位于 `Library/CardPresentationTests/results.xml`。
- 新增 5 项测试覆盖弧形边界、独立稳定相位、刷新/重排/翻面/选中组合、真实 Mesh 顶点在最大旋转下的边界、非法输入、生命周期清理，以及外部 Tween 与随机数隔离。
- 未保存或替换当前场景，未修改 Shader、共享材质和美术 Prefab；布局/翻面/上浮过渡仍为立即应用。未执行完整 Player/IL2CPP 构建。

### 2026-10-04 — 验证窗口逐牌属性页面

#### Added

- Card Zone Validator 新增“卡牌属性”页，按顺序为每张牌提供独立面板，编辑卡牌定义、强化/塑封/失效和禁用；显示实例牌 ID、定义 ID 与卡牌层计算的操作/计分权限。支持单牌清除规则状态、重排、删除和进入观察页。
- 编辑器模型提供按 CardInstanceId 的 GetCard / SetCardProperties。属性编辑先经过 Core CardInstance 校验与 WithEffects / WithDisabled，再保存供序列化/Undo 使用的样例数据；经原有牌区及 FromCard 投影刷新表现，不直接调用 CardView Setter。
- Editor-only CardZonePreviewCatalog 将 52 张标准定义映射到现有图集，选择器显示中文花色与点数；王等标记为美术样例，使用独立 editor-art 预览定义，不增加规则支持。
- 新增 3 项测试，覆盖标准定义/图集映射、非法卡牌属性拒绝与观察状态保留，以及编辑器属性经重排/隐藏/揭示/清除后到实际 Shader 参数的整条链路。

#### Changed

- 将单牌可见性、悬停/选中、预览乘色移入“观察与交互”页，布局和 Shader 调参移入“布局与样式”页。观察页按稳定身份跟随同一张牌，提供源卡牌 → 实际投影 → Shader 开关/Tint 的只读对照。
- 保留既有样例的序列化字段、Undo/Redo 与脚本重载恢复；标准牌从旧美术占位定义改用 Core 标准定义，原卡牌身份/属性/顺序保持不变。

#### Validation

- Unity 编译 0 警告，展示模块 **28/28 同步 NUnit 方法通过**，报告位于 `Library/CardPresentationTests/results.xml`；已检查卡牌属性页界面。
- 新脚本 `.meta`、模块及根 README 已更新。本次仅修改编辑器工具与测试，不改运行时规则、Shader 或存档契约；未保存当前场景，未执行 Player/IL2CPP 构建。

### 2026-10-04 — 卡牌属性驱动 Shader 表现（首包）

#### Added

- CardPresentation 新增 `CardAppearanceSettings`、`CardAppearanceResolver` 与不可变 `CardShaderAppearance`，统一把逐牌属性解析为完整 Shader 输入。强化对应火漆，塑封对应镭射，禁用对应红叉；失效压暗牌面、关闭火漆发光并减弱镭射，允许全部叠加。
- `CardZoneView.SetAppearanceSettings` 按当前卡牌属性立即刷新美术样式；支持 HDR 镭射颜色、火漆颜色/发光以及失效亮度/镭射系数。美术参数仅在 Unity 展示层，不进入 Core 或对局存档。
- 编辑器验证窗口增加“Shader 效果对照（8 张）”、中文效果多选及样式配置。对照按钮使用同一牌面的八种状态组合，独立 CardInstance 身份，经统一属性投影渲染；只有点击按钮才替换当前样例，支持 Undo/Redo。
- 新增 5 项测试；补齐新增脚本 `.meta`，更新模块、引擎及根 README。

#### Changed

- CardInstanceView 通过既有 CardView 接口完整提交 Tint、镭射、火漆与红叉参数，不从旧视图状态推导属性。移除效果、隐藏/揭示、对象复用及重复刷新会清除过期参数，不累积压暗；隐藏牌统一为普通牌背。
- 在修改任何视觉实例前，校验整组卡牌所需的 Shader 能力和火漆/红叉贴图；缺失时整次提交被拒绝，原显示保留。继续共享 Mesh/Material，仅写入逐牌 MaterialPropertyBlock。

#### Validation

- Unity 脚本编译 0 警告；展示模块 **25/25 同步 NUnit 方法通过**，控制台无错误/警告，结果位于 `Library/CardPresentationTests/results.xml`。
- 使用真实美术 Prefab，读取 MaterialPropertyBlock 验证全部 16 种效果/禁用组合、HDR 参数、样式更新、完整重置与失败前置校验；共享材质和 Prefab 序列化内容保持不变。
- 已检查验证窗口的新按钮和 Shader 样式字段。CardPresentation 无缺失 `.meta`，Assets 无重复 GUID。未执行完整 Player/IL2CPP 构建。

#### Scope

- 本次消费美术已有接口，不修改 Shader、共享材质、贴图或 Prefab；火漆位置/尺寸/旋转/法线和镭射速度仍由共享材质配置。失效使用乘色压暗，不新增去色 Shader。
- 未改 Core 规则或存档版本，未新增鼠标交互、动画与提交控制器；本次未保存或替换当前场景。

### 2026-10-04 — 卡牌效果、禁用与本地展示状态（首包）

#### Added

- Core `CardEffects` 使用稳定的位标记：Enhanced=1、Laminated=2、Ineffective=4。强化、塑封、失效可以组合，重复添加同一标记不增加层数；未知位在构造及存档校验时拒绝。
- CardInstance 增加 Effects / IsDisabled、CanPlay / CanDiscard / CanScore，以及不可变 WithEffects / AddEffects / RemoveEffects / WithDisabled 操作。定义、实例牌身份与 OriginId 保持不变；禁用与失效相互独立。
- 本地 `CardInteractionState` 保存悬停、选中（即待提交），不进入 Core 或权威存档。CardVisualData.FromCard 将规则属性、观察者可见性和本地状态投影给有序牌区及单牌视图；隐藏时不解析牌面，移除真实身份与规则属性。
- 单牌视图消费悬停/选中，任一成立都只应用一次位置偏移；CardZoneView 提供 SetRaisedOffset，默认局部 +Y 0.02。偏移位于视觉子节点，布局重排、翻面和反复刷新不会累积位移或改写牌区顺序。
- 编辑器窗口新增效果组合、禁用、模拟悬停、选中开关与三轴上浮配置；预览数据先构造 CardInstance，再走统一投影，并显示卡牌层操作/计分权限。
- 7 项引擎回归测试和 4 项展示测试，覆盖效果组合、独立限制、非法位、序列化、禁用操作原子拒绝、失效判型/计分、洗牌身份重分配及属性投影/上浮生命周期。新增脚本 `.meta` 齐全，更新模块及根 README。

#### Changed

- Draw 打出和丢弃在共用选择校验处检查卡牌权限，禁用牌在牌区转移前使整个请求被拒绝，不消耗次数、不产生部分事件。通用 CardZone 操作继续仅管理成员/顺序，不承担玩家动作权限。
- ScoringCard 携带 Effects；失效牌保留牌型参与及整手基础分，跳过自身逐牌点数、数值强化、重触发和逐牌小丑效果。Draw 和 Holdem Run 计分适配器传递卡牌效果。
- DeckEntry 支持初始化效果和禁用，Holdem 发牌/洗牌后分配新 ID 时保留属性；Draw 校验两份卡牌引用中的效果、禁用与身份一致。
- **Match schema v5 / Draw schema v2**：新增 Effects / IsDisabled 必填字段，旧 Match v1–v4、Draw v1 拒绝读取，暂不迁移。Run 外层仍为 v2，包含旧内层 schema 的 Run 存档也会被拒绝。

#### Validation

- 七个独立 DLL 以 Mono C# 7.2 编译（警告视为错误），**190/190 测试通过**。
- Unity 脚本编译 0 警告；当前场景轻量运行器完成引擎 **193/193**、展示 **20/20** 同步 NUnit 方法，报告分别位于 `Library/PokerEngineTests` 和 `Library/CardPresentationTests`。控制台无错误/警告。
- 已检查验证窗口的新属性开关；两个模块没有缺失 `.meta`，Assets 内无重复 GUID。未执行完整 Player/IL2CPP 构建。

#### Scope

- 强化、塑封仅保留效果语义，具体数值和外观待设计；禁用/失效不自动灰显。不增加 Run 商店、跨关状态同步或能力施加这些标记的命令。
- 可见性决定正反面；本地状态驱动位置，不新增拖拽或另一套待提交状态。尚未接鼠标射线、点击、提交控制器或动画，禁用牌能否被选中的策略仍待确定。
- 所有新增运行时类型位于既有首包程序集及 linker 保留范围；Editor 代码不进入 Player。未保存或替换当前场景，未改写卡牌 Prefab 和美术资源。

### 2026-10-03 — CardZone 编辑器验证窗口

#### Added

- 独立 Editor-only 程序集 `CardPresentation.Editor`，提供 `Tools > Cards > Card Zone Validator` 和 `Create Card Zone In Current Scene` 菜单。在当前场景创建/复用 EditorOnly 测试牌区，默认 5 张牌并定位 Scene 视图。
- 验证窗口支持增删、左右移动、反转、打乱、清空、牌面/牌背/颜色、矩形布局和 12 张重叠示例；显示数据/视觉数量、ViewId 与 GameObject ID，并可检查刷新是否复用实例。支持创建及数据/布局修改的 Undo/Redo。
- 可序列化的编辑器样例模型，经通用 Core 牌区操作及可见性投影驱动 CardZoneView；隐藏投影不带真实逻辑身份或牌面。脚本重载后恢复窗口数据，关闭窗口清理临时卡牌，保留测试根对象。
- 3 项样例模型测试；现有构建归属检查补充验证 Editor 程序集不进入 Player。补齐新资源 `.meta`、模块及根 README。

#### Fixed

- CardView 的编辑器 OnValidate 改为标记待刷新，在后续 Update 应用渲染数据，避免 Undo/对象校验期间设置 MeshFilter 引发 `OnMeshFilterChanged` 警告；Player 不包含这段编辑器刷新逻辑。

#### Validation

- 已在当前 `CardRenderingValidation` 场景创建 `CardZoneView Preview`。场景保持未保存，原有相机、灯光与美术资源不改写；示例牌为临时展示，不进入引擎存档或 Player。
- Unity 脚本编译 0 警告；本模块 **16/16 同步 NUnit 方法通过**，覆盖新增编辑器模型及 Player 排除/资源元数据检查。控制台无错误或警告，结果见 `Library/CardPresentationTests/results.xml`。
- 已检查编辑器窗口、场景内 12 张重叠显示及脚本重载后的样例恢复。未执行完整 Player/IL2CPP 构建。

### 2026-10-03 — 单牌区 CardZoneView（首包，无交互）

#### Added

- 独立 `CardPresentation.Runtime` 程序集，仅引用 Core 和 CardArt。CardZoneView 维护有序 CardInstanceView 与按 ViewId 查询的映射，复用已有卡牌 Prefab/共享渲染资源，同一展示身份刷新时保留 GameObject。
- CardInstanceView 区分展示身份、可选逻辑 CardInstanceId 和美术图集 ID；未知牌背不要求真实实例牌身份。揭示/隐藏时复用对象，隐藏时同时清除旧正面的图集区域和旧 Tint。
- CardVisualData / CardZoneViewData 输入，以及 `Present(CardZone, project)` 本地投影入口；宿主负责图集映射与观察者权限。支持添加、移除、重排、查询和清理，所有更新立即生效，不修改引擎状态。
- 局部 XZ 矩形横排：尺寸、留白、左/中/右对齐、期望间距；按真实 Prefab 尺寸排布，空间不足时压缩间距并重叠，厚度偏移避免交叠。提供 Gizmo 和显式 SetLayout/RefreshLayout。
- 13 项 Editor 测试，使用隔离 Preview Scene 和真实卡牌资产，覆盖数据边界、身份复用、共享资源、牌背清理、布局、生命周期和首包元数据。增加 `Poker > Tests > Run Card Presentation In Current Scene`，复用现有同步测试运行器。
- 模块 README、运行时/测试 asmdef、link.xml 和全部资源元数据；更新根 README。

#### Validation

- Unity 脚本编译 0 警告，本模块 **13/13 同步 NUnit 方法通过**，控制台无错误；包含 Player 归属、模块依赖与导入元数据检查。结果位于 `Library/CardPresentationTests/results.xml`。
- 复用测试运行器后，Core 入口回归 **186/186 通过**；新模块资源元数据齐全，Assets 内未发现重复 GUID。
- 未执行完整 Player/IL2CPP 构建；未修改或保存现有场景，未改写既有美术 Prefab、材质及引擎存档格式。

#### Scope

- 本版仅实现单牌区静态展示，不含交互、动画、跨区对象移交或移除对象的缓存池。Prefab 克隆中的 Collider 关闭；玩法绑定、可见性授权和网络状态顺序由宿主处理。

### 2026-10-03 — 通用有序牌区操作（首包）

#### Added

- `Core/Cards/CardZoneOperations.cs`：为任意 CardZone 提供不可变 Insert/InsertRange、Remove/RemoveRange/RemoveAt、Move、Swap、Reorder、TransferTo/TransferRangeTo 和 DrawTo 扩展方法。
- `CardZoneTransfer` 同时返回 Source、Target 及转移顺序 Cards；批量转移保持传入顺序，抽牌从来源末尾依次取出，目标支持指定插入位置。Move 使用移动后最终索引。
- 统一校验索引、有效/唯一 ID、源区成员、完整排列、不同源目标及两区不重叠；空批次/零张抽牌支持无操作，失败不修改输入或返回部分结果。操作保留区域 ID/Kind/ControllerId，调用者负责一起提交结果和校验完整玩法状态。
- 21 项测试：18 项基础操作边界、序列化、不可变性和连续转移守恒检查，以及能力效果链、德州轮流发牌/烧牌、Draw 弃牌/补牌顺序的 3 项集成检查。

#### Changed

- 能力执行的移牌/抽牌/重排、德州底牌/烧牌/公共牌发放、Draw 出牌/弃牌/补牌共用 Core 操作层，移除各自重复的列表编辑逻辑；玩法权限与规则校验保留在原模块。
- CardZone 构造函数拒绝本区重复或无效 CardInstanceId；操作层也验证反序列化后的输入。相同牌面定义的不同实例牌不受影响。
- 新文件及测试位于既有 Core/Editor 程序集内，补齐 `.meta`；更新根与模块 README。既有资产 GUID、依赖方向及快照字段保持不变，Match v4 / Draw v1 / Run v2 无 schema 升级。

#### Validation

- 七个独立 DLL 以 Mono C# 7.2 编译（警告视为错误），**183/183 测试通过**。
- Unity 脚本编译 0 警告，当前场景轻量运行器 **186/186 同步 NUnit 方法通过**，含七个模块的 Player 编译归属、linker 保留与元数据检查；控制台无错误。结果见 `Library/PokerEngineTests/results.xml`，未保存或替换当前场景。
- 本轮未执行完整 Player/IL2CPP 构建。

### 2026-10-03 — 模块解耦与独立出牌计分模式（首包）

#### Added

- 七个独立纯 C# 程序集：`Core`、`Abilities`、`Scoring`、`Holdem`、`Draw`、`Runs`、`Composition`。每个模块有独立 asmdef，离线脚本按同一依赖图逐个编译 DLL。
- `Core` 统一卡牌定义/实例/牌区、资源、随机、标准牌组与牌型分类。`PokerHandClassifier` 识别 1–5 张牌并区分提交牌与牌型组成牌；德州模块自行从 5–7 张中选最佳五张。
- 模式无关的 `AbilityWorld`、`AbilityExecutor` 与 `AbilityLifecycle`：共用费用、效果、选择暂停/续执行、触发排序、知识、记忆、期限和执行预算。德州通过 `HoldemAbilityAdapter` 投影状态，Draw 直接使用同一能力世界。
- 独立 `DrawEngine`：抽牌、选牌、出牌/弃牌、出牌次数、积分目标、计分前后能力窗口、选择超时与存档恢复。计分结果在完整检查点冻结，Run 采集后再继续补牌。
- 强类型 `IRunEncounter<TState,TCommand,TEvent>` 与泛型 Run 状态/命令/存档；`HoldemRunAdapter` 和 `DrawRunAdapter` 仅在 Composition 组装。Run 不引用任何具体玩法，也不保存任意 object/JSON 代替玩法状态。
- 42 项新增回归测试：通用能力 7 项、独立 Run 10 项、Draw 13 项、跨玩法集成 9 项、真实程序集依赖边界 3 项。覆盖同一构筑接入两种玩法、选择中断恢复、计分只采集一次、成长/商店变更不重算历史分数及跨玩法快照拒绝。

#### Changed

- 德州入口从 `MatchEngine` 改为 `HoldemMatchEngine`；原单体 `PokerEngine.Runtime` 拆除，公共命名空间继续为 `PokerEngine`。Holdem 不引用 Draw/Scoring/Runs，Draw 不引用 Holdem/Runs，基础模块不反向引用上层。
- 卡牌区 Kind、能力时机/动作、状态 ScopeId/PhaseKey 改为稳定字符串键；德州专属牌区及动作留在 Holdem。能力记忆、次数与冷却使用 Round/Encounter，移除共享层对下注街及 MatchState 的依赖。
- 计分接受明确的 0–512 张计分牌子集。Draw 默认仅对牌型组成牌计分，德州默认仍对最佳五张计分；获奖者筛选和弃牌获胜分数移到 `HoldemEncounterSettings`，从 `ScoreProfile` 移除。
- Run 的起始牌组及版本显式注入，最低牌数由玩法适配器决定（默认德州 12、Draw 8）；盲注/下注筹码不再属于共享关卡定义。构筑等级和成长由 Run 保持唯一事实，适配器同步到能力，避免重复成长或迁移旧实体引用。
- **Match schema 升级至 v4、Run schema 升级至 v2；新增 Draw schema v1**。快照固定内容、规则、计分、起始牌组及具体玩法版本；旧 Match v1–v3 和 Run v1 明确拒绝，目前无迁移器。能力德州规则升级为 `holdem.abilities` v2。
- 运行时文件随 `.meta` 移动，保留已有资产文件 GUID，补齐新增文件/目录元数据并清理旧空目录及其元数据；`link.xml` 保留七个模块。更新模块/根 README、接入示例、首包检查与独立测试脚本。

#### Validation

- 独立 Mono C# 7.2 编译（警告视为错误）及测试 **162/162 通过**；七个 DLL 分别编译，验证依赖边界没有被单体测试程序集掩盖。
- Unity 脚本编译通过，0 警告；当前场景轻量运行器在 Editor 中执行 **165/165 同步 NUnit 方法通过**，包含全部七个模块的 Player 编译归属、linker 保留及资源 GUID 检查。结果见 `Library/PokerEngineTests/results.xml`，未保存或替换当前场景。
- 本轮未执行完整 Player 或 IL2CPP 构建；目标平台仍须验证泛型 DataContract 序列化与 AOT 行为。

#### Scope

- Draw 为可独立运行的出牌计分基础模式，当前不自动回收弃牌；手牌和牌库都耗尽时失败。Run 仍为线性关卡，尚无分叉地图、Boss 策略、消耗品背包或账户永久解锁。
- 现有卡牌场景与渲染资源保持原状；完整玩家视图、HTTP/WebSocket、文件/数据库 I/O、持久化请求收据、AI、场景交互与计分动画由后续宿主/表现模块接入。

### 2026-10-02 — 能力执行、Run 构筑与计分（首包）

#### Added

- `Runtime/Execution/AbilityRuntime.cs`：注册主动/触发/续执行处理器，接入费用与有效牌面修正、能力效果替换和摊牌结果修正；原子执行费用、次数、冷却、资源、牌区、状态、计数及知识授权。
- 选牌/选项暂停与恢复：保存原下注决策、已处理触发器、执行步骤和窗口预算；读档和超时继续执行，不重复支付费用或跳过后续触发器。每批最多 64 条提案，每窗口最多 256 个效果步骤。
- `Runtime/Runs`：版本化内容、线性关卡、持久牌组、小丑槽位/副本限制/升级/成长、奖励、商店买卖与刷新；复制牌保持强化并生成独立身份，支持删牌、改牌及牌型升级。
- `Runtime/Scoring`：牌型等级基础分、逐牌强化/重触发、小丑加分/加倍率/乘倍率；按阶段及槽位顺序处理，保存逐步明细。long 分数和 decimal 倍率，最终向下取整，检查溢出并限制展开步数。
- 可执行示例内容：跟注回能、换牌、窥牌、加倍率、乘倍率、成长和花色计分类小丑；用于验证接入，数值不是最终平衡方案。
- `RunSnapshotCodec` schema v1：一次保存 Run、当前 Match、构筑、报价、随机流和计分账本，锁定内容/Run/计分及内层 Match schema 版本。
- 26 项能力执行/构筑/计分测试；Editor 菜单 `Poker > Tests > Run Core In Current Scene` 逐项执行同步 NUnit 方法并将整组结果写入 Library，保留当前未保存场景，避免 MCP 单次请求超时影响结果收集。

#### Changed

- 独立 `holdem.abilities` v1 复用标准德州流程并启用已注册能力；`holdem.standard` 继续限制为标准无能力牌局。
- 默认 Run 保留德州下注与底池胜负，额外累积肉鸽积分；下注筹码、商店金币和积分独立。关卡达标进入奖励/商店，超出手数或牌局结束但未达标则失败。
- 默认摊牌获奖者使用最佳五张计分；弃牌获胜固定积分，不触发逐牌计分/成长。可配置落败摊牌也计分；每手结算窗口关闭后只计一次。
- **Match schema 升级至 v3**：新增持久牌 OriginId、能力槽位 Order、结算有效牌面和执行窗口进度。v1/v2 快照明确拒绝，目前无迁移器。
- 支持自定义/重复牌面牌组及五条求值；两张底牌、五张公共牌约束保持，能力不得耗尽后续公共牌/烧牌所需牌库。Run 牌组范围 12–512 张。
- 街状态按对应街清理；次数状态显式消费；新手清理旧实例牌能力及依赖。Run 的等级/成长计数在计分后同步到能力，下一手即可使用。
- 更新模块与根 README、独立测试脚本；全部新运行时代码仍在 `PokerEngine.Runtime` 首包中，维护文件/目录 `.meta` 与已有 GUID。

#### Validation

- 独立 Mono C# 7.2 编译（警告视为错误）及测试 **120/120 通过**，含 26 项新增测试和原有 94 项回归。
- Unity 脚本编译 0 警告；当前场景轻量运行器在 Editor 中执行 **123/123 同步 NUnit 方法通过**，包含 3 项 Player 编译归属、linker 保留及导入资源元数据检查。结果见 `Library/PokerEngineTests/results.xml`。
- 完整 Unity Test Runner 首次停在未保存场景确认，已取消；随后用上述运行器完成验证，没有保存或替换当前场景。轻量运行器不支持生命周期钩子/协程测试；本模块当前测试均为无钩子的同步方法。
- 本轮未执行完整 Player 或 IL2CPP 构建。

#### Scope

- 当前 Run 为单玩家对单对手的线性关卡，关卡固定 10/20 盲注；未实现分叉地图、Boss 行为、消耗品背包、账户永久解锁或自由选牌出牌模式。
- 替换链处理能力产生的效果，基础发牌仍走固定流程。筹码能力限手间使用；手期限状态保留至下次发牌以支撑历史结算引用，结算后处理器须避免再次应用临时修正。
- 提供 C# 命令及完整权威快照；场景/UI/动画、AI、完整 PlayerView、HTTP/WebSocket、文件/数据库 I/O 与持久化请求收据仍由后续宿主层实现。

### 2026-10-02 — 卡牌实例 Color Tint

#### Added

- 卡牌 Shader 新增默认白色的 `_ColorTint`，标记为 PerRendererData，并与 `_FrontUVRect` 一起纳入 GPU instancing buffer；正面、背面、侧边及各 Pass 的 Alpha Clip 使用同一实例颜色。
- CardView 新增可序列化的 Color Tint、只读 Tint 与 SetTint(Color)；通过 MaterialPropertyBlock 单独改色，保留共享 Mesh/Material，切换牌面及重新启用后保留 tint。

#### Changed

- 共享 `_BaseColor` 继续作为皮肤全局乘色，界面标记为 Global Tint；单牌选中效果使用 `_ColorTint`，恢复白色即可取消乘色。
- Prefab 导出器与运行时 Shader 契约增加 `_ColorTint` 检查，要求自定义 Shader 具备 Color 属性及实例声明/读取。
- 更新卡牌渲染文档与调用示例；既有材质和 Prefab 使用默认白色，无需复制材质或重建资源。

#### Validation

- Unity 脚本编译通过，无警告；12 项相关 EditMode 检查通过，涵盖 Shader Pass、单牌 UV/tint 隔离、切牌/重新启用/恢复白色、共享材质保持、Prefab 导出及自定义 Shader 的实例颜色契约。
- 导出器拒绝缺少 `_ColorTint` 或仅将其声明为共享 uniform 的 Shader；测试临时资产在结束后清理。未测量实际 GPU Draw Call 数量。

### 2026-10-02 — 清理早期单人原型

#### Removed

- 删除 `Assets/Scripts/PokerGame.cs` 中的旧双人规则、PokerHands 牌型计算与抽样 AI。
- 删除仅依赖旧规则的 `Assets/Scripts/PokerTable.cs`，移除全局自动启动及 IMGUI 牌桌。
- 删除旧独立测试 `Tests/PokerRulesTests.cs`；同步清理脚本 `.meta`、空 `Assets/Scripts` 目录及目录 `.meta`、空根级 `Tests` 目录。

#### Changed

- 共享 `Assets/PokerEngine` / MatchEngine 成为唯一对局引擎，首包程序集、能力基建及现有测试保留。
- 更新根 README、引擎 README 和卡牌渲染说明，明确当前场景尚未接入新对局交互；进入 Play 不再自动创建早期牌桌。
- 已检查全部资源中的旧脚本 GUID 引用，无需修改场景或 Prefab；保留主场景、渲染验证场景、卡牌资源及现有构建入口。

#### Validation

- 删除后 Unity 脚本编译通过，无警告；独立 C# 测试 94/94 通过。
- Unity 测试接口整组运行超时，改为分组/单项验证后确认 96 项通过，包含全部首包检查。`RandomLegalHandsTerminateAndConserveEveryChip` 单项仍遇到接口请求超时，未取得其 Unity 结果；同项已在独立 C# 测试中通过，不计入本次 Unity 通过数。
- 清理后无旧类型或旧脚本 GUID 的可执行/序列化引用；对比清理前文件哈希，确认新引擎运行时及测试、卡牌和渲染模块均未改动。
- 验证期间发现 `Assets/Scenes/CardRenderingValidation.unity` 文件发生变化；本次清理没有直接编辑场景，保留编辑器当前场景内容，没有覆盖该变化。

### 2026-10-02 — MatchEngine 标准对局执行（首包）

#### Added

- `Runtime/Execution`：无 Unity 依赖的 MatchEngine、玩家下注命令和权威系统命令、错误码、转换结果、合法行动与按玩家筛选的事件。
- `Runtime/Rules`：固定版本标准 52 张牌目录、2–10 人无限注德州规则入口及 5–7 张牌最佳五张求值；涵盖九类牌型、A2345、踢脚与稳定同值选牌。
- 开手、大小盲、洗牌与私牌、四轮下注及烧牌、全下发完公共牌、摊牌、逐池获奖、未跟注退款、零头分配、换庄及淘汰闭环。
- 最小加注、短全下不重开与累计短全下重开、大盲行动权、盲注不足和仅剩全下对手时的合法行动处理。
- 可注入的纯洗牌接口，默认采用版本化 HMAC-SHA256 计数器、拒绝采样和 Fisher–Yates；随机种子仅存于权威状态，固定向量覆盖字节序和消费顺序。
- 31 项 MatchEngine 测试，包含 250 组随机合法牌局、确定边池归属/淘汰、每个检查点恢复后连续运行三手、事件隔离与非法命令状态不变。

#### Changed

- 能力窗口成为真实的对局暂停位置；通过 WindowId 推进系统阶段，通过 DecisionId 消费玩家下注决策。超时由宿主提交命令，默认可过牌则过牌，否则弃牌。
- 命令校验 Revision 与认证身份，所有变化在临时状态完成并验证后返回；失败不扣款、不消耗随机序列、不返回半途事件。
- 延续 schema v2，无持久化字段变更；结算支付、随机状态和下一手换庄信息从现有状态恢复。
- 更新模块/根 README、调用示例、服务端职责与离线测试入口；所有运行时代码继续进入 `PokerEngine.Runtime` 首包，维护新增目录及文件的 `.meta`。

#### Validation

- Mono C# 7.2 独立编译，警告视为错误；94/94 测试通过。
- Unity 脚本编译无警告；EditMode 97/97 通过，包含 3 项 Player 编译归属、linker 保留与资源元数据检查。
- 未执行完整 Player 或 IL2CPP 构建。

#### Scope

- 本轮是标准无能力规则执行路径；加载了能力、状态、知识授权或待执行效果时返回 `UnsupportedExtensions`。具体效果原子落地、替换/修正链接入与肉鸽胜负规则留待后续。
- 事件具备可见性筛选，完整 PlayerView、网络 DTO/服务器、持久化收据去重、文件/数据库 I/O、时钟与 AI 不在本轮实现范围。
- 保持现有 `Assets/Scripts/PokerGame.cs` 与演示场景独立。

### 2026-10-02 — 能力扩展基建（首包）

#### Added

- 能力定义、Solo/Pvp 范围、固定优先级、权威使用窗口、行动触发条件及可扩展条件接口。
- 主动/触发能力规划接口、受控效果提案、类型化规则查询、规则/结果修正与 Keep/Cancel/Replace 替换链；固定排序、来源检查和提案展开上限。
- 通用 SelectionSpec，支持选牌、选玩家、选项、排序、跳过、超时默认项；分别验证能力持有者、实际选择者、决策 ID 和候选项。
- Knowledge 目录及私人/公开牌面授权：一次性历史窥视、绑定牌区的实例牌跟踪、街/手结束后的观察过滤、权威有效牌面解析接口。
- 版本化 AbilityMemory：不可变的计数与实体标记、Hand/Match 作用域及显式重置辅助方法。
- 34 项能力数据/接口测试，包含五类测试能力；全部新增文件及目录均维护 Unity `.meta`。

#### Changed

- 快照升级为 **schema v2**，保存对局模式、能力记忆、能力窗口、知识状态及通用选择；v1 与未知版本明确拒绝，当前没有迁移器。
- 允许结算后和下一手开始前的能力处理；有未完成选择或待处理效果时阻止新主动能力。
- 保留原有数据构造器和基础选择工厂；只注册能力 ID 仍可加载结构，但不能参与行为规划。
- 更新模块目录/协议说明和离线测试入口，运行时仍属于 `PokerEngine.Runtime` 首包程序集，测试仅在 Editor。

#### Validation

- Mono C# 7.2 独立编译及数据测试：63/63 通过。
- Unity 脚本编译通过，无警告；EditMode 66/66 通过，包含 Player 程序集归属、linker 保留和资源 GUID 检查。
- 本轮提供数据、资格检查、选择解析、知识投影和效果提案组合；尚未实现对局命令执行、效果原子落地、资源支付/计数消耗、完整 PlayerView 或 IL2CPP 构建验证。

### 2026-09-30 — PokerEngine 数据基建（首包）

#### Added

- 新增 `Assets/PokerEngine/Runtime` 及 `PokerEngine.Runtime` 独立程序集，纯 C#、无 UnityEngine 依赖，所有 Player 平台可用。
- 强类型实体 ID、非负且检查溢出的整数筹码、不可变状态与防御性集合复制。
- 固定版本的规则定义与内容目录；卡牌定义、实例、有效牌面和唯一牌区归属。
- 多人座位、整场/本手参战状态、下注轮与加注重开信息；累计投入推导主池、边池与未跟注退款。
- 能力/状态实例、实体引用、明确的持续时间、决策窗口及可保存的效果续执行队列。
- 按底池记录的结算、牌型结果数据、随机提供者状态，以及跨实体引用与金额等不变量校验。
- schema v1 权威 JSON 快照，恢复前检查结构、规则/内容版本和格式大小；不接受未知版本的静默迁移。
- Editor NUnit 测试及 `Tools/test-poker-engine.sh` 离线验证入口，覆盖快照、错误状态和底池守恒。
- `link.xml` 保留首包运行时模型及序列化成员，完整 Unity `.meta` 文件和模块目录说明。

#### Scope

- 本轮实现数据基建，不包含对局命令执行、洗牌算法、牌型求值、能力执行器、玩家视图、网络服务或存档文件 I/O。
- 现有 `Assets/Scripts/PokerGame.cs` 和牌桌演示保持独立，尚未迁移到新引擎。
- 测试程序集仅限 Editor；运行时数据结构归属首包，不通过热更新或远程资源下载交付。

#### Validation

- Unity 2022.3.62f3 脚本编译通过，无编译警告。
- Unity EditMode：32/32 通过（29 项数据测试、3 项首包/资源结构检查）。
- 脱离 Unity 的 Mono C# 7.2 编译及同一组数据测试：29/29 通过。
- 验证 Player 编译列表包含 `PokerEngine.Runtime`，排除测试程序集；验证 linker 保留配置和全部新增资源的元数据。
- 尚未执行完整 Player 构建或 IL2CPP 目标平台验证。
