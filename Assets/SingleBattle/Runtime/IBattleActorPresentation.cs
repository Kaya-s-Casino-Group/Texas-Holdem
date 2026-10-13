using System;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    public enum BattlePresentationKind { PrepareAttack, Attack, Hit, Death }
    public enum BattlePresentationStatus { Pending, Completed, Cancelled, Failed }

    /// <summary>Immutable, already-resolved presentation input. It cannot submit gameplay commands.</summary>
    public sealed class BattlePresentationCue
    {
        public BattlePresentationKind Kind { get; }
        public ParticipantId ActorId { get; }
        public CombatEvent Effect { get; }
        public BattlePresentationCue(BattlePresentationKind kind, ParticipantId actorId, CombatEvent effect = null)
        {
            if (!Enum.IsDefined(typeof(BattlePresentationKind), kind) || !actorId.IsValid) throw new ArgumentException("Invalid presentation cue.");
            Kind = kind; ActorId = actorId; Effect = effect;
        }
    }

    /// <summary>Main-thread operation. The producer finishes it exactly once; late callbacks after cancellation are ignored.</summary>
    public sealed class BattlePresentationOperation : CustomYieldInstruction
    {
        public BattlePresentationStatus Status { get; private set; }
        public Exception Error { get; private set; }
        public override bool keepWaiting => Status == BattlePresentationStatus.Pending;
        public bool Complete() => Finish(BattlePresentationStatus.Completed, null);
        public bool Cancel() => Finish(BattlePresentationStatus.Cancelled, null);
        public bool Fail(Exception error) => Finish(BattlePresentationStatus.Failed, error ?? new InvalidOperationException("Presentation failed."));
        private bool Finish(BattlePresentationStatus status, Exception error)
        {
            if (!keepWaiting) return false;
            Error = error; Status = status; return true;
        }
        public static BattlePresentationOperation Completed()
        { var operation = new BattlePresentationOperation(); operation.Complete(); return operation; }
    }

    /// <summary>Play returns an operation completed by the actual animation/timeline, never an estimated host delay.
    /// Synchronize updates health visuals without playing an action. Snap is synchronous.
    /// Cancel stops all work/callbacks. Reset restores the idle pose for a fresh binding.
    /// Implementations must not advance turns or modify authoritative state.</summary>
    public interface IBattleActorPresentation
    {
        BattlePresentationOperation Play(BattlePresentationCue cue);
        void Synchronize(CombatantState unit);
        void Snap(BattlePresentationCue cue);
        void CancelPresentation();
        void ResetPresentation();
    }
}
