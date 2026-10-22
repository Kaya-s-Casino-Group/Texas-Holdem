using System;
using global::BattlePresentation;
using System.Collections;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>One Actor entry point. Body and shields share an operation; partial blocks never create a second serial hit.</summary>
    public sealed class BattleActorPresentationCoordinator : MonoBehaviour, IBattleActorPresentation, IBattleActorDisplayPresentation, IShieldPresentation
    {
        [SerializeField] private MonoBehaviour body;
        [SerializeField] private DiscardShieldPresenter shields;
        public IBattleActorPresentation Body => body as IBattleActorPresentation;
        public DiscardShieldPresenter Shields => shields;
        private Coroutine playback;
        private BattlePresentationOperation active;
        private ShieldSnapshot current = ShieldSnapshot.Empty;
        public void Configure(MonoBehaviour modelPresentation, DiscardShieldPresenter shieldPresentation)
        {
            if (modelPresentation == this || !(modelPresentation is IBattleActorPresentation) || shieldPresentation == null)
                throw new ArgumentException("Bind a body presentation and shield container.");
            CancelPresentation(); body = modelPresentation; shields = shieldPresentation;
        }
        public void Synchronize(CombatantState unit) => Body?.Synchronize(unit);
        public void SynchronizeDisplay(UnitDisplayData unit)
        {
            if (Body is IBattleActorDisplayPresentation display) display.SynchronizeDisplay(unit);
        }
        public void SynchronizeShields(ShieldSnapshot snapshot) { current = snapshot ?? ShieldSnapshot.Empty; shields?.Snap(current); }
        public BattlePresentationOperation Play(BattlePresentationCue cue)
        {
            if (active != null && active.keepWaiting) throw new InvalidOperationException("Wait for the current actor presentation.");
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                Snap(cue);
                if (cue.Kind == BattlePresentationKind.Attack || cue.Kind == BattlePresentationKind.Support) cue.Markers?.Emit("Impact");
                return BattlePresentationOperation.Completed();
            }
            active = new BattlePresentationOperation(); var pending = active;
            try
            {
                var shieldWork = ApplyShields(cue, true);
                var bodyWork = Body?.Play(cue) ?? BattlePresentationOperation.Completed();
                if (Body == null && (cue.Kind == BattlePresentationKind.Attack || cue.Kind == BattlePresentationKind.Support)) cue.Markers?.Emit("Impact");
                playback = StartCoroutine(Wait(pending, bodyWork, shieldWork));
                if (!pending.keepWaiting) playback = null;
            }
            catch (Exception error) { pending.Fail(error); shields?.CancelPresentation(); Body?.CancelPresentation(); }
            return pending;
        }
        private BattlePresentationOperation ApplyShields(BattlePresentationCue cue, bool animate)
        {
            if (cue.Kind == BattlePresentationKind.Death) current = ShieldSnapshot.Empty;
            else if (cue.Shields != null) current = cue.Shields;
            else return BattlePresentationOperation.Completed();
            return shields != null ? shields.Present(current, animate, cue.Kind == BattlePresentationKind.BlockHit) : BattlePresentationOperation.Completed();
        }
        private IEnumerator Wait(BattlePresentationOperation parent, BattlePresentationOperation bodyWork, BattlePresentationOperation shieldWork)
        {
            while (parent.keepWaiting && (bodyWork.keepWaiting || shieldWork.keepWaiting))
            {
                if (Failed(bodyWork) || Failed(shieldWork)) break;
                yield return null;
            }
            if (parent.keepWaiting)
            {
                if (Failed(bodyWork) || Failed(shieldWork))
                { parent.Fail(bodyWork.Error ?? shieldWork.Error ?? new InvalidOperationException("Actor presentation interrupted.")); Body?.CancelPresentation(); shields?.CancelPresentation(); }
                else parent.Complete();
            }
            playback = null;
        }
        private static bool Failed(BattlePresentationOperation operation) => operation.Status == BattlePresentationStatus.Cancelled || operation.Status == BattlePresentationStatus.Failed;
        public void Snap(BattlePresentationCue cue) { CancelPresentation(); ApplyShields(cue, false); Body?.Snap(cue); }
        public void CancelPresentation()
        {
            active?.Cancel(); active = null;
            if (playback != null) StopCoroutine(playback); playback = null;
            Body?.CancelPresentation(); shields?.CancelPresentation();
        }
        public void ResetPresentation() { CancelPresentation(); Body?.ResetPresentation(); shields?.Snap(current); }
        private void OnDisable() => CancelPresentation();
    }
}
