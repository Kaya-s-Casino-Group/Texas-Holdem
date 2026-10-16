using System;
using UnityEngine;

namespace CardPresentation
{
    public enum CardActionViewStatus { Pending, Completed, Cancelled, Failed }

    /// <summary>Completion for one committed card action. The producing View owns the terminal state.</summary>
    public sealed class CardActionViewOperation : CustomYieldInstruction
    {
        public CardActionViewStatus Status { get; private set; }
        public Exception Error { get; private set; }
        public override bool keepWaiting => Status == CardActionViewStatus.Pending;
        public bool Complete() => Finish(CardActionViewStatus.Completed, null);
        public bool Cancel() => Finish(CardActionViewStatus.Cancelled, null);
        public bool Fail(Exception error) => Finish(CardActionViewStatus.Failed, error ?? new InvalidOperationException("Card action presentation failed."));
        private bool Finish(CardActionViewStatus status, Exception error)
        {
            if (!keepWaiting) return false;
            Error = error; Status = status; return true;
        }
    }
}
