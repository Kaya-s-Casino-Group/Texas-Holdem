using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Keep the settled enemy phase visible until its existing warning clock expires.</summary>
    public sealed class WrathWarningBoundaryAction : PresentationActionBehaviour
    {
        private SingleBattleSceneAdapter host;
        private SingleBattleHud hud;
        private bool searchedForHud;
        private readonly List<Run> pending = new List<Run>();
        private sealed class Run
        {
            internal PresentationOperation Operation;
            internal Action Stop;
            internal bool Released;
        }
        public override ActionDescriptor Descriptor => new ActionDescriptor("wrath.warning-boundary");
        public void Configure(SingleBattleSceneAdapter owner, SingleBattleHud warningHud = null)
        { host = owner; hud = warningHud; searchedForHud = warningHud != null; }

        public override IPresentationOperation Play(ActionContext context, IMarkerSink markers)
        {
            var before = context.Before;
            if (host == null || !host.UsesExternalCommands || context.Data.SemanticKey != "PhaseEnded" ||
                !context.Data.Values.TryGetValue("action", out var action) || action != "EnemyAction" ||
                before == null || !before.HasStarted || before.IsFinished ||
                !before.Labels.TryGetValue("wrath.body", out var body) || body != context.Data.ActorId ||
                before.Unit(body)?.IsAlive != true || before.Resource(body, "wrath.warning")?.Value != 1 ||
                before.CompletedRounds + 1 >= (before.Resource(body, "wrath.releaseRound")?.Value ?? -1))
                return PresentationOperation.Completed();
            if (!searchedForHud)
            {
                searchedForHud = true;
                hud = host.gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SingleBattleHud>(true)).FirstOrDefault();
            }
            if (Remaining <= 0) return PresentationOperation.Completed();
            var run = new Run();
            run.Operation = new PresentationOperation(() => Release(run));
            pending.Add(run);
            context.Scope.Own(() => run.Operation.Cancel());
            try
            {
                var stop = StartRoutine(Wait(run));
                if (run.Released) stop(); else run.Stop = stop;
            }
            catch (Exception error)
            { Release(run); run.Operation.TryFail("WrathWarningBoundaryFailed", error.Message, error); }
            return run.Operation;
        }

        private float Remaining => host != null && host.UsesExternalCommands && hud != null
            ? hud.WrathUltimateWarningRemaining : 0;
        private IEnumerator Wait(Run run)
        {
            while (!run.Released && Remaining > 0) yield return null;
            if (run.Released) yield break;
            run.Stop = null;
            Release(run);
            run.Operation.TryComplete();
        }
        private void Release(Run run)
        {
            if (run.Released) return;
            run.Released = true;
            var stop = run.Stop; run.Stop = null;
            stop?.Invoke(); pending.Remove(run);
        }
        private void Clear() { foreach (var run in pending.ToArray()) run.Operation.Cancel(); pending.Clear(); }
        public override void Synchronize(ActionSyncContext context) => Clear();
        private void OnDisable() => Clear();
    }
}
