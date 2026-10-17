using System;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Identity and current state access. Calls the presentation protocol without owning transforms or timing.</summary>
    [DisallowMultipleComponent]
    public abstract class BattleActor : MonoBehaviour
    {
        [SerializeField] private SingleBattleHost battle;
        [SerializeField] private Transform visual;
        [SerializeField] private MonoBehaviour presentation;
        private ParticipantId participant;
        private string shieldSession;
        private readonly ShieldLedger shieldLedger = new ShieldLedger();
        public ShieldSnapshot Shields => shieldLedger.Snapshot;
        internal ShieldSnapshot RecordBlockEffect(CombatEvent effect, ShieldCard[] cards) =>
            shieldLedger.Apply(effect.Revision + "/" + effect.Index, effect.Before.Block, effect.After.Block, cards);
        private BattlePresentationOperation pending;
        public SingleBattleHost Battle => battle;
        public Transform Visual => visual;
        public IBattleActorPresentation Presentation => presentation != null ? presentation as IBattleActorPresentation : null;
        public bool IsBound => battle != null && battle.PresentedState != null && participant.IsValid && OwnsParticipant(battle.PresentedState, participant);
        public ParticipantId ParticipantId => participant;
        public CombatantState Unit => IsBound ? battle.PresentedState.Duel.Combat.Get(participant) : null;
        public bool IsAlive => Unit != null && Unit.IsAlive;
        public event Action ActionPresented;
        public event Action<CombatEvent> DamagePresented;
        public event Action<CombatEvent> DeathPresented;
        protected abstract bool OwnsParticipant(CardDuelState state, ParticipantId id);

        public void Configure(SingleBattleHost owner, Transform model = null)
        {
            if (owner == null || owner.gameObject.scene != gameObject.scene ||
                model != null && (model == transform || !model.IsChildOf(transform)))
                throw new ArgumentException("Bind an owner in this scene and an optional visual beneath this actor.");
            ResetPresentation(); battle = owner; visual = model; participant = default;
        }
        public void ConfigurePresentation(MonoBehaviour implementation)
        {
            if (implementation != null && (!(implementation is IBattleActorPresentation) || implementation.gameObject.scene != gameObject.scene ||
                implementation.transform == transform || !implementation.transform.IsChildOf(transform)))
                throw new ArgumentException("Bind a presentation protocol implementation beneath this actor.");
            ResetPresentation(); presentation = implementation;
            if (IsBound) SnapToCurrentState();
        }
        public void Bind(ParticipantId id)
        {
            if (battle == null || battle.PresentedState == null || !id.IsValid || !OwnsParticipant(battle.PresentedState, id))
                throw new ArgumentException("Actor type and participant slot must agree.");
            if (visual != null && (visual == transform || !visual.IsChildOf(transform)))
                throw new ArgumentException("The visual must be beneath the logical actor, not its root.");
            ResetPresentation(); participant = id;
            string session = battle.PresentedState.Table.MatchId.ToString() + "/" + id;
            if (shieldSession != session) { shieldSession = session; shieldLedger.Reset(Unit.Block); }
            else shieldLedger.Reconcile(Unit.Block);
            Presentation?.Synchronize(Unit);
            (Presentation as IShieldPresentation)?.SynchronizeShields(Shields);
            if (!IsAlive) SnapToCurrentState();
        }
        internal BattlePresentationOperation PresentPreparation(bool animate) =>
            Play(new BattlePresentationCue(BattlePresentationKind.PrepareAttack, participant), animate);
        internal BattlePresentationOperation PresentAction(CombatEvent effect, bool animate)
        {
            if (!IsBound || !isActiveAndEnabled) return BattlePresentationOperation.Completed();
            ActionPresented?.Invoke();
            return Play(new BattlePresentationCue(BattlePresentationKind.Attack, participant, effect), animate);
        }
        internal BattlePresentationOperation PresentHit(CombatEvent effect, bool animate) => PresentEffect(effect, animate, null);
        internal BattlePresentationOperation PresentEffect(CombatEvent effect, bool animate, ShieldSnapshot shields)
        {
            if (!IsBound || effect.Effect.Target != participant) return BattlePresentationOperation.Completed();
            Presentation?.Synchronize(effect.After);
            BattlePresentationKind kind;
            if (effect.Effect.Kind == CombatEffectKind.GainBlock && effect.After.Block > effect.Before.Block) kind = BattlePresentationKind.GainBlock;
            else if (effect.Effect.Kind == CombatEffectKind.ClearBlock) kind = BattlePresentationKind.ClearBlock;
            else if (effect.BlockAbsorbed > 0) kind = BattlePresentationKind.BlockHit;
            else if (effect.HealthLost > 0) kind = BattlePresentationKind.Hit;
            else return BattlePresentationOperation.Completed();
            if (effect.HealthLost > 0) DamagePresented?.Invoke(effect);
            return Play(new BattlePresentationCue(kind, participant, effect, shields), animate);
        }
        internal BattlePresentationOperation PresentDeath(CombatEvent effect, bool animate)
        {
            if (!IsBound || effect.Effect.Target != participant || !effect.Died) return BattlePresentationOperation.Completed();
            DeathPresented?.Invoke(effect);
            return Play(new BattlePresentationCue(BattlePresentationKind.Death, participant, effect), animate);
        }
        private BattlePresentationOperation Play(BattlePresentationCue cue, bool animate)
        {
            if (pending != null && pending.keepWaiting) throw new InvalidOperationException("Wait for the previous actor presentation first.");
            if (Presentation == null) return BattlePresentationOperation.Completed();
            if (!animate) { Presentation.Snap(cue); return BattlePresentationOperation.Completed(); }
            if (!presentation.isActiveAndEnabled) throw new InvalidOperationException("The actor presentation is disabled.");
            pending = Presentation.Play(cue) ?? throw new InvalidOperationException("A presentation must return a completion operation.");
            return pending;
        }
        internal void CancelPresentation()
        {
            pending?.Cancel(); pending = null;
            Presentation?.CancelPresentation();
        }
        private void ResetPresentation() { CancelPresentation(); Presentation?.ResetPresentation(); }
        internal void SnapToCurrentState()
        {
            ResetPresentation();
            if (IsBound)
            {
                shieldLedger.Reconcile(Unit.Block);
                Presentation?.Synchronize(Unit);
                (Presentation as IShieldPresentation)?.SynchronizeShields(Shields);
            }
            if (IsBound && !IsAlive) Presentation?.Snap(new BattlePresentationCue(BattlePresentationKind.Death, participant));
        }
        protected virtual void OnDisable() => ResetPresentation();
        protected virtual void OnEnable() { if (IsBound) SnapToCurrentState(); }
    }
}
