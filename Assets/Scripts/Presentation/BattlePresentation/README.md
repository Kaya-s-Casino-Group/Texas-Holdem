# Battle Presentation

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
            new[] { ActionDependency.Marker("cards", "ReadyToAttack") })
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

## Unity adapters

`BattlePresentation.Unity` is an optional frontend with no game-rule references:

- `ScenePresentationDirector` holds target bindings and serializable fact-to-plan recipes. A recipe can augment or replace the frontend's default plan and choose an explicit checkpoint. Components implementing `IPresentationAction` can control cards, UI, characters, cameras or arbitrary objects.
- `TransformTimelineAction` provides actual segment completion markers. It rejects another managed action/feedback writing the same Transform. Use a dedicated child layer when the base layout can also move. Its default restores its contribution when complete; disabling that option holds the transient pose until the scope ends.
- `SceneCandidateFeedback` routes candidate state and pattern keys to arbitrary `IInteractionFeedback` components. Empty routes have no visual side effects.
- `CandidateTransformFeedback` acts on a dedicated neutral child layer, smoothly retargets from its current pose, and releases toward zero position, identity rotation and unit scale. A new candidate can explicitly take over a layer from an exiting candidate; it cannot steal a running battle action's channel. The base layout belongs on the layer's parent.

The existing `HandZoneView.PresentSelectionAction` is a card-library adapter. It emits `ReadyToAttack` during the real preparation tween without changing `Data.Cards`. The later `CardsMoved` fact performs actual displayed-zone changes. Cancel removes only its temporary offset and leaves current layout, ordering and selection intact.

For the existing Scene3D objects, add `BattleActorTimelineAction` or `HandPreparationTimelineAction` to a scene object and drag that component into a `ScenePresentationDirector` recipe node's `action` field. Configure the actor and action kind, or the hand source, in the Inspector or through `Configure`. An omitted actor resolves the bound visual child back to its actor, using the fact's actor/effect target identity; an omitted hand uses the `cards` binding. These adapters consume only projected DTOs, wait on actual operations, and emit `Impact`/`Revealed` or `ReadyToAttack`. Cancellation stops only the operation they started. All Unity timeline actions derive from `PresentationActionBehaviour`. The Scene3D host no longer constructs a default recipe: authored scene groups carry the complete sequence. `HandSettleTimelineAction` waits for the actual card motion, and `UiFeedbackTimelineAction` creates scoped UI feedback from neutral DTOs. The scene setup menu migrates legacy empty groups and serializes component references and dependencies.

## Verification

- `BattlePresentation.Tests.PresentationCoreTests`: cursor transactions, signal order, intermediate checkpoints, parallel/AllOf scheduling, nonblocking tails, fault recovery, epochs, candidate replacement and lease ownership.
- `BattlePresentation.Unity.Tests.SceneFeedbackTests`: actual Transform contribution release, cross-pattern handoff and action/feedback conflicts.
- `CardPresentation.Tests.CardZoneTransitionTests`: actual card preparation marker, preserved card list, current-layout cancellation and legal transfer after the intermediate marker.

No rules, random-number operations or command entrypoints live in this module.
