# Battle Presentation

Palamedes retains this generic framework. Use the [CardPresentation README](../CardPresentation/README.md) as the current presentation integration entrypoint and historical-document index. World-card motion and pointer interaction have been restored; Camera Present infrastructure is configured in SingleBattle. Old Scene3D actors, HUD and complete battle timelines remain archived; references to those below describe the archived integration. New frontends provide their own implementations of the retained contracts.

`BattlePresentation.Runtime` is a pure C# assembly with `noEngineReferences: true`. It references neither Unity nor the battle rules. A rule bridge supplies immutable projected facts; frontends supply object bindings and actions.

## Data ownership

- `BattleDisplaySnapshot` contains public units, card zones, resources and labels. Its lists and dictionaries are defensively copied. `CardDisplayData` distinguishes visual identity from publicly visible card identity.
- `BattleDisplayChange` describes one fact with `Before`, `After`, semantic key, actor/targets/cards and optional effect data. It contains no executable rule command.
- `DisplayStore` alone publishes persistent display state. `ExpectedCursor` establishes fact order. `DisplayVersion` counts publications, so applying three facts in one transaction advances the cursor three facts and the version once.
- `PresentationBatch.Initial` must match the current battle, observer and cursor. A rejected or stale batch never starts animation. `Instant` validates the entire batch and publishes its final snapshot once without invoking ordinary action `Play` or synthesizing markers.

The bridge must filter hidden information before constructing any DTO, including card art keys, extra properties and action IDs. Redacting only `CardId` does not make an otherwise identifying payload private.

## Signal plans

The frontend creates a `PresentationDirector(store, targets, planFactory)` and calls `Tick()` on its update thread. The factory receives a fixed `ActionContext` for each ordered fact and returns an `ActionPlan`.

```csharp
new ActionPlan(
    new[] {
        new ActionNode("cards", prepareCards),
        new ActionNode("attack", attack,
            new[] { ActionDependency.Marker("cards", "Prepared") })
    },
    new[] { ActionDependency.Marker("attack", "Impact") });
```

Dependencies are AllOf and can reference `Started`, a declared marker, or `Completed`. Array order breaks ties between simultaneously eligible actions. A null checkpoint waits for all blocking nodes; an empty checkpoint applies the fact immediately. An explicit marker checkpoint can apply the current fact and create the following fact's plan while the previous attack continues recovering. The batch remains pending until all facts and blocking actions finish.

All dependencies exist before `Play` begins. Synchronous marker emission is buffered until `Play` returns; immediately completed operations remain valid. Required markers missing at completion, cycles, unknown nodes, exceptions and null operations fail the batch. Late callbacks cannot cross the playback epoch. Nonblocking tails can keep scheduling other nonblocking nodes after the input gate opens and are cancelled on the next command, synchronization or frontend disable.

Each producer owns a `PresentationOperation`; ordinary consumers receive `IPresentationOperation`. Complete, cancel and fail are mutually exclusive terminal outcomes. Marker names identify a point within one action instance; use distinct markers for multiple hits. No global marker bus or guessed timeout is involved.

## Cancellation and recovery

`Synchronize` invalidates the old epoch, disposes scopes, cancels operations, replaces the display snapshot and synchronizes known action adapters. Cancelling or failing an animated batch recovers to that already committed batch's final snapshot. It never replays rules or signals. `IsRecoveryBlocked` keeps the input gate closed if recovery itself fails until a later successful synchronization.

Scopes own cleanup. `PresentationChannels.Acquire` explicitly transfers an exclusive channel; stale leases cannot write or restore over the new owner. `TryAcquire` instead rejects a conflicting owner. Release callbacks must remove their own contribution or read the current base layout, not restore an obsolete gameplay snapshot.

## Candidate interaction

`InteractionSession` is separate from blocking playback. Synchronous adapters use `Reconcile`; asynchronous previews use `BeginRequest` and `Apply`, which check battle, observer, rule revision, selection version and selected IDs. Same-state/same-pattern updates reuse feedback scopes; changed patterns replace scopes. `BeginCommit` returns a fixed intent candidate before publishing `Committing`; the rule adapter still decides whether submission is accepted. `Close` and `Clear` invalidate pending preview results.

`IInteractionFeedback.Reconcile` targets the newest state rather than queuing every historical enter/exit. On release, scope callbacks are invalidated before the feedback adapter is notified. Candidate animation never changes `DisplayStore` or globally blocks play.

### Player card gesture contract

Player pointer operations on cards (short press, long press and drag) are valid only during that player's turn while the gameplay bridge accepts input. The single-battle frontend uses `BattleTimelineSceneAdapter.CanAct`, which includes authority turn ownership and pending playback; visual card presence alone never grants input permission. Check the gate at press, hold/update and release. When it closes, immediately cancel the held gesture and restore any uncommitted drag order. A late release must not select a card or commit a reorder, including after the gate reopens; require a new press. Candidate selection still obeys the narrower action/forced-discard window and card eligibility. Programmatic local arrangement remains a separate view capability.

## Unity adapters

`BattlePresentation.Unity` is an optional frontend with no game-rule references:

- `ScenePresentationDirector` holds target bindings and serializable fact-to-plan recipes. A recipe can augment or replace the frontend's default plan and choose an explicit checkpoint. Components implementing `IPresentationAction` can control cards, UI, characters, cameras or arbitrary objects.
- `TransformTimelineAction` provides actual segment completion markers. It rejects another managed action/feedback writing the same Transform. Use a dedicated child layer when the base layout can also move. Its default restores its contribution when complete; disabling that option holds the transient pose until the scope ends.
- `SceneCandidateFeedback` routes candidate state and pattern keys to arbitrary `IInteractionFeedback` components. Empty routes have no visual side effects.
- `CandidateTransformFeedback` acts on a dedicated neutral child layer, smoothly retargets from its current pose, and releases toward zero position, identity rotation and unit scale. A new candidate can explicitly take over a layer from an exiting candidate; it cannot steal a running battle action's channel. The base layout belongs on the layer's parent.

The existing `HandZoneView.PresentSelectionAction` is a card-library adapter. It emits `Prepared` during the real preparation tween without changing `Data.Cards`. The later `CardsMoved` fact performs actual displayed-zone changes. Cancel removes only its temporary offset and leaves current layout, ordering and selection intact.

For the existing Scene3D objects, add `BattleActorTimelineAction` or `CardZoneTimelineAction` to a scene object and drag that component into a `ScenePresentationDirector` recipe node's `action` field. Configure the actor and action kind, or the hand source, in the Inspector or through `Configure`. An omitted actor resolves the bound visual child back to its actor, using the fact's actor/effect target identity; an omitted hand uses the `cards` binding. These adapters consume only projected DTOs, wait on actual operations, and emit `Impact`/`Revealed` or `Prepared`. Cancellation stops only the operation they started. All Unity timeline actions derive from `PresentationActionBehaviour`. The Scene3D host no longer constructs a default recipe: authored scene groups carry the complete sequence. `CardZoneTimelineAction` waits for the actual card motion, and `UiFeedbackTimelineAction` creates scoped UI feedback from neutral DTOs. Scene configuration serializes component references and dependencies. Card actions bind an `ICardZonePresentation` provider and an action key; use `cards.settle` with Observe Projection to wait for checkpoint-driven projection.

## Camera Present

当前 `SingleBattle` 将静态场景配置与执行分开。`BattleCamera` 保存 Camera、机位和参数；`Battle/CameraPresent` 只执行动作、合成构图与震动。Battle 桥接显式绑定两者，运行时不按物体名搜索配置，也不要求相机位于执行器下面。

```text
BattleCamera                         CameraSceneConfiguration（静态场景数据）
  Main Camera                        原有相机输出及渲染配置，可直接编辑
  Shots/Overview                      默认构图 CameraShot
  Shots/Closeup                       可编辑的近景镜头点 CameraShot
  Shots/Opening                       开局远景锚点 CameraShot
  FocusPoint                          聚焦目标占位锚点
Battle/CameraPresent                 CameraPresentation（执行器）
  Actions/Shot                        CameraTimelineAction，Cue Key = shot
  Actions/Focus
  Actions/Return
  Actions/Shake
  Actions/Opening                     DOTween 开局动作 CameraOpeningTimelineAction
```

- 调整基础构图：编辑 `BattleCamera/Shots/Overview` 的世界位置、旋转、Field Of View（透视）或 Orthographic Size（正交），在 BattleCamera Inspector 点击“回到基础构图”；也可先直接调整 Main Camera，再点击“将当前 Main Camera 机位保存为基础构图”。镜头动作不切换投影模式，不改渲染器、裁剪层、相机堆叠或输入相机引用。Main Camera 不要求本地位姿为零。
- 调整其他镜头：编辑 `BattleCamera/Shots/Closeup`，或复制 CameraShot 锚点并绑定到场景配置的 Cues / Shot。动作执行节点只保存 Source 和 Cue Key，时长、偏移和震动等参数统一在场景配置中编辑。
- 预览：选中 `Actions` 下的动作，在 Inspector 点击“预览镜头动作（自动回位）”；可随时停止。预览使用同一动作实现及独立编辑器时钟，离开 Inspector、进入 Play Mode 或脚本重载时清理预览。
- 新场景装配：菜单 `工具/卡牌战斗/Camera Present/准备当前场景`，要求恰好一个 Battle 桥接和一个活动 MainCamera。使用 Undo，重复执行保留已有配置；`校验当前场景` 检查配置和显式绑定。当前场景迁移保留了所有相机、锚点和动作的对象引用、世界机位以及原时间线。

`BattleTimelineSceneAdapter` 的 Camera Scene 引用场景配置，Camera Present 引用本 Battle 的执行器。每次 Initialize（包括重开／重新接入）在发布初始显示快照前校验并调用 BindScene；单独绑定不修改相机位姿，初始化的同步快照才恢复 Overview。普通动作只读取已绑定的参数，不持有第二份序列化配置。编辑器预览也显式通过同一个桥接绑定入口，脚本重载后不依赖残留字段；重新开局或重新预览会重读场景参数。

### 时间线接入与信号

将相应 `CameraTimelineAction` 拖入已有 `ScenePresentationDirector` 的节点 Action。Source 指向 CameraPresent 执行器，Cue Key 对应场景配置中的唯一键；Director 的 `camera` 目标仍指向执行器，既有 `camera.focus` 引用仍指向场景 FocusPoint。当前六个卡牌配方保留原样，未自动增加攻击、受击或终局的镜头效果。

| Kind | 参数与行为 | 信号 |
| --- | --- | --- |
| Shot | Duration 内到达 Shot 的世界位姿与镜头值，保持 Hold Duration，再以 Return Duration 回到 Overview | 到达发 Arrived，回位发 Settled，然后 Completed |
| Focus | 保持当前位置和镜头值，转向 Focus Target + 世界空间 Focus Offset；保持后回位 | Arrived、Settled、Completed |
| Return | Duration 内回到 Overview | Arrived、Settled、Completed |
| Shake | Duration 内按 Shake Position / Euler / Frequency 施加衰减震动；末尾归零 | Settled、Completed |

Focus Target 留空时，通过节点的 Target Key 解析 Transform、GameObject 或 Component；未提供目标时使用场景 Default Focus。当前可填 `camera.focus`，后续可绑定角色的 AimAnchor。聚焦在动作开始时采样目标，不持续追踪移动目标。镜头动作没有自动从 ActorId 解析角色的注册表。

需要在构图到达后启动其他动作，可依赖 `Arrived`；需要镜头完全回位后继续，可依赖 `Completed`。Shot / Focus 的保持时长由配置决定；当前不支持跨任意时长演出持有镜头。震动可设为 nonblocking 尾部，业务事实的 checkpoint 由配方决定，不由镜头自行发布。

### 开局镜头与发牌衔接

当前 `SingleBattle` 的开局使用 `Actions/Opening` 上的 `CameraOpeningTimelineAction`。起点从 `Shots/Opening` 读取完整世界位姿和镜头值，终点从 `Shots/Overview` 读取；实际数值以场景中的锚点 Inspector 为准。两端均可独立编辑，过渡不依赖 Main Camera 的父级零位姿。

DOTween 同步插值位置、Quaternion 旋转、FOV 和正交尺寸。时长、曲线和 `Deal Start Progress` 以 `BattleCamera → Opening` 为准；本次拆分保留 2.5 秒、OutSine、0.9 的现有配置。后者按实际 Tween 推进的时间比例触发 `NearlyArrived`，对应动作时间为 Duration × Deal Start Progress（当前约 2.25 秒），空间进度还受曲线影响。动作完成时发 `Arrived` 并保持 Overview，不返回开场远景。每个实例只推进自己的 Manual Tween，不更新全局 DOTween 时钟。

`Battle → ScenePresentationDirector → Timeline → BattleStarted` 的 `opening-camera` 节点是 blocking，显式 checkpoint 仅依赖该节点的 `NearlyArrived`。收到信号后发布 BattleStarted 并继续后续事实，原 CardsDealt 配方再发布手牌并等待真实发牌动画。镜头剩余段和发牌可以并行；整个批次完成后才开放玩家操作。不要把 BattleStarted 的 checkpoint 改为空，否则发牌会提前；也不用另加计时器或延迟规则开局。

查看完整开局：进入 Play，或在 BattleSceneHost 点击“重新开始 Battle”。只预览镜头：选中 `BattleCamera` 或开场执行节点，点击“预览开场镜头”；停止预览会回位。编辑起点用 `BattleCamera/Shots/Opening`，编辑终点用 `BattleCamera/Shots/Overview`，时长、曲线和发牌放行比例在 `BattleCamera → Opening` 调节。菜单“以当前相机配置开场”用于首次捕获当前相机并接入配方；重复执行保留已有配置。

如果直接修改了 Main Camera 的位置、旋转或 FOV，在编辑模式下使用菜单“将当前相机更新为开场”，或在 Actions/Opening Inspector 点击“将当前 Main Camera 机位保存为开场”。此操作记录相机完整世界位姿与镜头参数，再回到 Overview 供继续编辑，支持 Undo。直接编辑相机不会导致 Rig 校验失败；没有捕获到锚点的修改不会替换已配置的开场起点。

### 所有权与恢复

Pose（包含镜头值）与 Shake 是独立的数据通道，可并行，不对应场景中的 Transform 节点。唯一合成出口按 `position = basePosition + baseRotation * shakeOffset`、`rotation = baseRotation * shakeRotation` 写入 Main Camera，并应用构图的 FOV／正交尺寸；动作更新时立即合成，在 LateUpdate 再提交当前结果。更新顺序不改变合成关系。

同一通道的两个动作返回 TargetBusy。只要任意相机通道仍在运行，合成器就持有 Main Camera Transform 的独占租约，与 TransformTimelineAction / CandidateTransformFeedback 共用输出通道池；其他 Rig 也不能同时控制同一相机。回位也是 Pose 动作，不能强行抢占正在运行的 Shot；中断时先取消所属操作，或调用 Rig 的 ResetPresentation。

取消／Scope 释放仅移除该操作的贡献：Pose 恢复最新 Overview 并保留正在播放的震动；Shake 清零偏移，保留镜头过渡当前位置。单独震动从当前相机机位采样基础构图，结束后保留该机位。运行中的 Rig 停用、显示快照 Synchronize（包括重开）会取消镜头操作并复位。普通显示检查点不打断镜头。Instant 不播放镜头动作或制造 Marker。运行时协程异常转成 Failed 操作，避免 Director 永久等待。空闲时不持续写相机，组件启停也不覆盖设计者的机位编辑。

Rig 仅管理战斗镜头。独立手牌 Overlay 相机继续由 ViewportOverlayCamera 管理，UI 相机继续由 UICameraStack 管理；如果手牌仍由 Main Camera 渲染，则会自然随主镜头移动。

## Verification

- `BattlePresentation.Tests.PresentationCoreTests`: cursor transactions, signal order, intermediate checkpoints, parallel/AllOf scheduling, nonblocking tails, fault recovery, epochs, candidate replacement and lease ownership.
- `BattlePresentation.Unity.Tests.SceneFeedbackTests`: actual Transform contribution release, cross-pattern handoff and action/feedback conflicts.
- `BattlePresentation.Unity.Tests.CameraPresentationTests`: lens/pose arrival and return, independent shake, ownership conflicts, cancellation, stale scopes, focus targets, instant mode and checkpoints; real Play Mode coroutines, disable and synchronization recovery.
- `CardPresentation.Tests.WorldCardZoneContractTests`: real motion markers, preserved preparation membership, projection ordering/state, cancellation, instant playback and director checkpoint binding.

2026-10-29 Camera Present 接入验证：当前 Unity 工程的通用表现、Unity 动作及卡牌契约回归 **67/67 通过**，包含真实 Play Mode 镜头协程及同步／停用恢复；本地结果在 `Library/CameraPresentationValidation/results.xml`。当前场景配置校验和重复装配检查通过，安装前后的原 Camera、URP 附加配置及 AudioListener 序列化数据一致。

同日开局镜头接入后回归 **72/72 通过**，新增检查点延迟发牌、镜头尾段并行、Tween 时钟隔离、取消重播和零时长／末端阈值测试（`Library/CameraPresentationValidation/opening-results.xml`）。当前场景 Play Mode 实测首次开局在约 1.45 秒、重开在约 1.44 秒开始发出 8 张手牌，剩余镜头位移均小于 1%；两次完整落定及中途同步恢复通过（`Library/CameraPresentationValidation/opening-scene.xml`）。

同日改为直接相机输出后回归 **76/76 通过**，新增非零局部位姿、空闲编辑保留、不同更新顺序的合成、取消单一贡献和多个 Rig 的输出互斥检查（`Library/CameraPresentationValidation/composition-results.xml`）。当前场景完整开局及重开通过：按现有 2.5 秒、90% 配置，Tween 推进约 2.25 秒后发牌，剩余位移约 1.2%；开场期间叠加并取消震动不打断构图，最终回到 Overview（`Library/CameraPresentationValidation/composition-scene.xml`）。

同日拆分场景相机配置后，表现／卡牌回归 **76/76**、桥接回归 **32/32** 全部通过；新增 7 项场景相机绑定测试覆盖参数不再序列化到执行器、显式绑定不移动相机、缺失配置拒绝、OutSine／90% 信号时刻和重新接入读取新配置。当前场景首次开局及重开均在 Tween 推进约 2.25 秒后发出 8 张手牌，剩余位移约 1.2%；中途同步取消运镜并恢复 Overview。记录在 `Library/CameraSceneValidation/regression.xml` 和 `scene.xml`。原 Camera、URP 附加数据、AudioListener 和 CameraShot 参数序列化内容保持一致。

No rules, random-number operations or command entrypoints live in this module.
