using System;
using global::BattlePresentation;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Identity and current state access. Calls the presentation protocol without owning transforms or timing.</summary>
    [DisallowMultipleComponent]
    public abstract class BattleActor : MonoBehaviour
    {
        [SerializeField] private SingleBattleSceneAdapter battle;
        [SerializeField] private Transform visual;
        [SerializeField] private MonoBehaviour presentation;
        private ParticipantId participant;
        private string shieldSession;
        private readonly ShieldLedger shieldLedger = new ShieldLedger();
        public ShieldSnapshot Shields => shieldLedger.Snapshot;
        internal ShieldSnapshot RecordBlockEffect(CombatEvent effect, ShieldCard[] cards) =>
            shieldLedger.Apply(effect.Revision + "/" + effect.Index, effect.Before.Block, effect.After.Block, cards);
        private BattlePresentationOperation pending;
        private bool startingPresentation;
        private UnitDisplayData displayUnit;
        public UnitDisplayData DisplayUnit => displayUnit;
        public string DisplayId => displayUnit?.Id ?? (participant.IsValid ? participant.Value : null);
        public bool IsDisplayBusy => startingPresentation || pending != null && pending.keepWaiting;

        public ShieldSnapshot RecordDisplayBlock(string effectKey, EffectDisplayData effect, ShieldCard[] cards)
        {
            if (string.IsNullOrEmpty(effectKey) || effect?.Before == null || effect.After == null)
                throw new ArgumentException("A display shield fact needs an identity and before/after units.");
            return shieldLedger.Apply(effectKey, checked((int)effect.Before.Block), checked((int)effect.After.Block), cards);
        }
        /// <summary>Neutral scene adapter. It consumes already-projected facts and never reads the rule session.</summary>
        public BattlePresentationOperation PresentDisplay(BattlePresentationKind kind, EffectDisplayData effect,
            IMarkerSink markers, ShieldSnapshot shields = null)
        {
            if (string.IsNullOrEmpty(DisplayId)) throw new InvalidOperationException("Synchronize the display actor before playing actions.");
            if (kind != BattlePresentationKind.Attack && kind != BattlePresentationKind.PrepareAttack &&
                effect != null && effect.TargetId != DisplayId) return BattlePresentationOperation.Completed();
            if (effect?.After != null && effect.TargetId == DisplayId)
            {
                displayUnit = effect.After;
                (Presentation as IBattleActorDisplayPresentation)?.SynchronizeDisplay(displayUnit);
            }
            if (kind == BattlePresentationKind.Attack) ActionPresented?.Invoke();
            return Play(new BattlePresentationCue(kind, DisplayId, effect, markers, shields), true);
        }
        public void SynchronizeDisplay(UnitDisplayData unit, bool reset = false)
        {
            if (unit == null || string.IsNullOrEmpty(unit.Id)) throw new ArgumentException("A display unit needs stable identity.");
            bool first = displayUnit == null;
            bool identityChanged = displayUnit != null && displayUnit.Id != unit.Id;
            bool blockChanged = Shields.TotalBlock != unit.Block;
            if (reset || identityChanged) ResetPresentation();
            displayUnit = unit;
            if (identityChanged) shieldLedger.Reset(checked((int)unit.Block));
            else shieldLedger.Reconcile(checked((int)unit.Block));
            (Presentation as IBattleActorDisplayPresentation)?.SynchronizeDisplay(unit);
            if (reset || first || identityChanged || blockChanged)
                (Presentation as IShieldPresentation)?.SynchronizeShields(Shields);
            if (reset && !unit.IsAlive) Presentation?.Snap(new BattlePresentationCue(BattlePresentationKind.Death, unit.Id));
        }
        public SingleBattleSceneAdapter Battle => battle;
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

        public void Configure(SingleBattleSceneAdapter owner, Transform model = null)
        {
            if (owner == null || owner.gameObject.scene != gameObject.scene ||
                model != null && (model == transform || !model.IsChildOf(transform)))
                throw new ArgumentException("Bind an owner in this scene and an optional visual beneath this actor.");
            ResetPresentation(); battle = owner; visual = model; participant = default; displayUnit = null;
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
            ResetPresentation(); participant = id; displayUnit = null;
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
            if (IsDisplayBusy) throw new InvalidOperationException("Wait for the previous actor presentation first.");
            if (Presentation == null)
            {
                if (animate && cue.Kind == BattlePresentationKind.Attack) cue.Markers?.Emit("Impact");
                return BattlePresentationOperation.Completed();
            }
            if (!animate) { Presentation.Snap(cue); return BattlePresentationOperation.Completed(); }
            if (!presentation.isActiveAndEnabled) throw new InvalidOperationException("The actor presentation is disabled.");
            startingPresentation = true;
            try
            {
                pending = Presentation.Play(cue) ?? throw new InvalidOperationException("A presentation must return a completion operation.");
                return pending;
            }
            finally { startingPresentation = false; }
        }
        internal void CancelPresentation()
        {
            pending?.Cancel(); pending = null;
            Presentation?.CancelPresentation();
        }
        /// <summary>Adapters may release only their own operation, never a later action on this actor.</summary>
        internal bool CancelDisplayOperation(BattlePresentationOperation expected)
        {
            if (expected == null || !ReferenceEquals(pending, expected)) return false;
            CancelPresentation(); return true;
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
