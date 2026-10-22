using System;
using System.Collections;
using System.Collections.Generic;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Keep the enemy action open through recovery, while damage still reveals at Impact.</summary>
    public sealed class BattleAttackRecoveryAction : PresentationActionBehaviour
    {
        private readonly List<PresentationOperation> pending = new List<PresentationOperation>();
        public override ActionDescriptor Descriptor => new ActionDescriptor("enemy.recovery", new[] { "Recovered" });
        public override IPresentationOperation Play(ActionContext context, IMarkerSink markers)
        {
            var target = context.Targets?.Resolve(context.Data.ActorId, context);
            var actor = target as BattleActor;
            if (actor == null && target is Component component) actor = component.GetComponentInParent<BattleActor>();
            if (actor == null && target is GameObject go) actor = go.GetComponentInParent<BattleActor>();
            if (actor == null || !actor.IsDisplayBusy) { markers.Emit("Recovered"); return PresentationOperation.Completed(); }
            Action stop = null;
            var operation = new PresentationOperation(() => stop?.Invoke());
            pending.Add(operation);
            context.Scope.Own(() => { operation.Cancel(); pending.Remove(operation); });
            stop = StartRoutine(Wait(actor, operation, markers));
            return operation;
        }
        private IEnumerator Wait(BattleActor actor, PresentationOperation operation, IMarkerSink markers)
        {
            while (actor != null && actor.IsDisplayBusy && operation.Status == PresentationStatus.Pending) yield return null;
            pending.Remove(operation);
            if (operation.Status != PresentationStatus.Pending) yield break;
            try { markers.Emit("Recovered"); operation.TryComplete(); }
            catch (Exception error) { operation.TryFail("EnemyRecoveryFailed", error.Message, error); }
        }
        private void Clear() { foreach (var operation in pending.ToArray()) operation.Cancel(); pending.Clear(); }
        public override void Synchronize(ActionSyncContext context) => Clear();
        private void OnDisable() => Clear();
    }
}
