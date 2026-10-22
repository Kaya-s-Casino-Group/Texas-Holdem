using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Each configured actor action consumes display data and owns only its own animation operation.</summary>
    [AddComponentMenu("Battle Presentation/Actor Timeline Action")]
    public sealed class BattleActorTimelineAction : PresentationActionBehaviour
    {
        [SerializeField] private BattleActor target;
        [SerializeField] private BattlePresentationKind kind = BattlePresentationKind.Attack;
        private sealed class Run
        {
            internal BattleActor Actor;
            internal ActionContext Context;
            internal IMarkerSink Markers;
            internal BattlePresentationOperation Source;
            internal PresentationOperation Operation;
            internal Action Stop;
            internal bool Started;
            internal long LifeVersion;
        }
        private sealed class GuardedMarkers : IMarkerSink
        {
            private readonly Run run;
            internal GuardedMarkers(Run run) { this.run = run; }
            public bool Emit(string marker) => run.Context.Scope.IsActive && run.Operation.Status == PresentationStatus.Pending && run.Markers.Emit(marker);
        }
        private readonly List<Run> pending = new List<Run>();
        private readonly HashSet<BattleActor> resolvedActors = new HashSet<BattleActor>();
        public override ActionDescriptor Descriptor => new ActionDescriptor("actor." + kind,
            kind == BattlePresentationKind.Attack ? new[] { "Impact" } : new[] { "Revealed" }, new[] { "actor" });
        public void Configure(BattleActor actor, BattlePresentationKind actionKind)
        { CancelAll(); target = actor; kind = actionKind; }

        public override IPresentationOperation Play(ActionContext context, IMarkerSink markers)
        {
            if (!isActiveAndEnabled) return PresentationOperation.Failed("ActionDisabled", name);
            var actor = Resolve(context);
            if (actor == null) return PresentationOperation.Failed("MissingActor", context.TargetKey ?? context.Data.ActorId ?? name);
            var run = new Run { Actor = actor, Context = context, Markers = markers, LifeVersion = actor.DisplayLifeVersion };
            run.Operation = new PresentationOperation(() => Release(run)); pending.Add(run); resolvedActors.Add(actor);
            context.Scope.Own(() => { if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); });
            Tick(run);
            if (run.Operation.Status == PresentationStatus.Pending && (Application.isPlaying || StartPresentationRoutine != null))
                run.Stop = StartRoutine(Observe(run));
            return run.Operation;
        }
        private IEnumerator Observe(Run run)
        {
            while (run.Operation.Status == PresentationStatus.Pending) { yield return null; Tick(run); }
            run.Stop = null;
        }
        public void Tick() { foreach (var run in pending.ToArray()) Tick(run); }
        private void Tick(Run run)
        {
            if (run.Operation.Status != PresentationStatus.Pending) return;
            if (!run.Context.Scope.IsActive) { run.Operation.Cancel(); return; }
            if (run.Actor == null || !run.Actor.isActiveAndEnabled) { Fail(run, "ActorDisabled", "The actor was disabled or removed."); return; }
            try
            {
                if (!run.Started)
                {
                    if (kind != BattlePresentationKind.Attack && kind != BattlePresentationKind.PrepareAttack &&
                        kind != BattlePresentationKind.Revive &&
                        (run.LifeVersion != run.Actor.DisplayLifeVersion || run.Actor.IsSupersededDeath(run.Context.Data.Effect)))
                    {
                        // A newer revive fact may overtake a queued death in an older scene recipe.
                        // Satisfy its marker without applying the obsolete dead snapshot or pose.
                        new GuardedMarkers(run).Emit("Revealed");
                        run.Operation.TryComplete();
                        return;
                    }
                    if (run.Actor.IsDisplayBusy) return;
                    var effect = run.Context.Data.Effect;
                    var motion = kind;
                    if (run.Context.Data.Values.TryGetValue("enemyMotion", out var enemyMotion) && enemyMotion == "Support")
                        motion = kind == BattlePresentationKind.PrepareAttack ? BattlePresentationKind.PrepareSupport :
                            kind == BattlePresentationKind.Attack ? BattlePresentationKind.Support : kind;
                    run.Source = run.Actor.PresentDisplay(motion, effect, new GuardedMarkers(run), ShieldFrame(run, effect));
                    run.Started = true;
                    if (kind != BattlePresentationKind.Attack) new GuardedMarkers(run).Emit("Revealed");
                }
                if (run.Source.keepWaiting) return;
                if (run.Source.Status == BattlePresentationStatus.Completed) { run.Operation.TryComplete(); }
                else if (run.Source.Status == BattlePresentationStatus.Cancelled) run.Operation.Cancel();
                else Fail(run, "ActorActionFailed", run.Source.Error?.Message ?? "Actor presentation failed.", run.Source.Error);
            }
            catch (Exception error) { Fail(run, "ActorActionFailed", error.Message, error); }
        }
        private static ShieldSnapshot ShieldFrame(Run run, EffectDisplayData effect)
        {
            if (effect?.Before == null || effect.After == null || effect.TargetId != run.Actor.DisplayId || effect.Before.Block == effect.After.Block) return null;
            var fact = run.Context.Data;
            string key = fact.Values.TryGetValue("effectKey", out var supplied) ? supplied : run.Context.BatchId + "/" + fact.Sequence;
            ShieldCard[] cards = null;
            if (effect.Kind == "GainBlock" && fact.Values.TryGetValue("action", out var action) && action == "Discard")
                cards = fact.CardIds.Select(id => run.Context.Before.Zones.SelectMany(zone => zone.Cards)
                    .FirstOrDefault(card => card.CardId == id || card.ViewId == id)).Where(card => card != null && !card.Hidden && !string.IsNullOrEmpty(card.ArtKey))
                    .Select(card => new ShieldCard(card.CardId ?? card.ViewId, card.ArtKey)).ToArray();
            return run.Actor.RecordDisplayBlock(key, effect, cards);
        }
        private BattleActor Resolve(ActionContext context)
        {
            if (target != null) return target;
            var bound = ResolveObject(context.Target); if (bound != null) return bound;
            string id = kind == BattlePresentationKind.Attack || kind == BattlePresentationKind.PrepareAttack
                ? context.Data.ActorId : context.Data.Effect?.TargetId ?? context.Data.TargetIds.FirstOrDefault() ?? context.Data.ActorId;
            return ResolveObject(context.Targets?.Resolve(id, context));
        }
        private static BattleActor ResolveObject(object value)
        {
            if (value is BattleActor actor) return actor;
            if (value is GameObject gameObject) return gameObject.GetComponentInParent<BattleActor>();
            return value is Component component ? component.GetComponentInParent<BattleActor>() : null;
        }
        private void Fail(Run run, string code, string message, Exception error = null)
        { Release(run); run.Operation.TryFail(code, message, error); }
        private void Release(Run run)
        {
            var stop = run.Stop; run.Stop = null; stop?.Invoke();
            if (run.Actor != null) run.Actor.CancelDisplayOperation(run.Source);
            pending.Remove(run);
        }
        private void CancelAll()
        { foreach (var run in pending.ToArray()) { if (run.Operation.Status == PresentationStatus.Pending) run.Operation.Cancel(); else Release(run); } }
        public override void Synchronize(ActionSyncContext context)
        {
            CancelAll(); if (target != null) resolvedActors.Add(target);
            foreach (var actor in resolvedActors.Where(actor => actor != null))
            {
                var unit = context.Snapshot.Unit(actor.DisplayId);
                if (unit == null) continue;
                if (actor.IsDisplayBusy) throw new InvalidOperationException("Another action still owns this actor during synchronization.");
                actor.SynchronizeDisplay(unit, true);
            }
        }
        private void Update() { if (StartPresentationRoutine == null && !Application.isPlaying) Tick(); }
        private void OnDisable() => CancelAll();
    }
}
