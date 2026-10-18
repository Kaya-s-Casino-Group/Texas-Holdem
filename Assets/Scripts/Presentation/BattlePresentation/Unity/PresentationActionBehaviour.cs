using System;
using System.Collections;
using UnityEngine;

namespace BattlePresentation.Unity
{
    /// <summary>
    /// Common scene-action contract. Implementations receive only display data and emit presentation markers.
    /// A host may inject a routine runner and clock; neither requires a rule engine or a Unity Update loop.
    /// </summary>
    public abstract class PresentationActionBehaviour : MonoBehaviour, IPresentationAction
    {
        public Func<IEnumerator, Action> StartPresentationRoutine { get; set; }
        public Func<float> DeltaTimeProvider { get; set; }
        protected float DeltaTime => DeltaTimeProvider?.Invoke() ?? Time.unscaledDeltaTime;

        public abstract ActionDescriptor Descriptor { get; }
        public abstract IPresentationOperation Play(ActionContext context, IMarkerSink markers);
        public abstract void Synchronize(ActionSyncContext context);

        /// <summary>Returns an idempotent cancellation callback for the scheduled routine.</summary>
        protected Action StartRoutine(IEnumerator routine)
        {
            if (routine == null) throw new ArgumentNullException(nameof(routine));
            Action cancel;
            if (StartPresentationRoutine != null) cancel = StartPresentationRoutine(routine);
            else
            {
                var coroutine = StartCoroutine(routine);
                cancel = () => { if (this != null && coroutine != null) StopCoroutine(coroutine); };
            }
            bool cancelled = false;
            return () => { if (cancelled) return; cancelled = true; cancel?.Invoke(); };
        }
    }
}
