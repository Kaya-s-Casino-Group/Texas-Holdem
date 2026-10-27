using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

namespace CardPresentation
{
    public partial class HandZoneView
    {
        [SerializeField] private Vector3 selectionActionOffset = new Vector3(0, 0, .035f);
        [SerializeField, Min(.01f)] private float selectionActionSeconds = .18f;
        [SerializeField, Range(0.01f, 1)] private float selectionActionReadyProgress = .7f;
        private SelectionAction selectionAction;

        private sealed class SelectionAction
        {
            public readonly object Owner = new object();
            public readonly object TweenId = new object();
            public CardInstanceView[] Views;
            public Vector3[] Starts;
            public Tween Tween;
            public CardActionViewOperation Operation;
            public bool Ready;
        }

        /// <summary>
        /// Prepare accepted cards without changing their displayed zone membership. Prepared is an actual
        /// tween milestone; the visual offset persists until the later CardsMoved fact or synchronization.
        /// </summary>
        public CardActionViewOperation PresentSelectionAction(IEnumerable<string> viewIds, Action<string> marker, bool animate = true)
        {
            ValidateOffset(selectionActionOffset);
            if (!CardZoneRowLayout.Finite(selectionActionSeconds) || selectionActionSeconds <= 0 ||
                !CardZoneRowLayout.Finite(selectionActionReadyProgress) || selectionActionReadyProgress <= 0 || selectionActionReadyProgress > 1)
                throw new InvalidOperationException("Selection action timing must be finite and positive.");
            var ids = (viewIds ?? Enumerable.Empty<string>()).ToArray();
            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length || ids.Any(id => !TryGetView(id, out _)))
                throw new ArgumentException("Selection action requires distinct card identities in the current hand.", nameof(viewIds));
            CancelSelectionAction();
            var batch = new SelectionAction { Views = ids.Select(id => { TryGetView(id, out var view); return view; }).ToArray() };
            batch.Starts = batch.Views.Select(view => view.ClaimSelectionAction(batch.Owner)).ToArray();
            batch.Operation = new CardActionViewOperation(() => ReleaseSelectionAction(batch));
            selectionAction = batch;
            if (!animate || !isActiveAndEnabled || ids.Length == 0)
            {
                try { marker?.Invoke("Prepared"); batch.Ready = true; batch.Operation.Complete(); }
                catch (Exception error) { ReleaseSelectionAction(batch); batch.Operation.Fail(error); }
                return batch.Operation;
            }
            float progress = 0;
            batch.Tween = DOTween.To(() => progress, value =>
                {
                    if (!ReferenceEquals(selectionAction, batch) || !batch.Operation.keepWaiting) return;
                    progress = value;
                    for (int i = 0; i < batch.Views.Length; ++i)
                    {
                        var view = batch.Views[i];
                        if (view == null || !view.isActiveAndEnabled) { batch.Operation.Cancel(); return; }
                        view.SetSelectionActionOffset(batch.Owner, Vector3.LerpUnclamped(batch.Starts[i], selectionActionOffset, progress));
                    }
                    if (!batch.Ready && progress >= selectionActionReadyProgress) EmitReady(batch, marker);
                }, 1, selectionActionSeconds)
                .SetEase(Ease.OutQuad).SetUpdate(UpdateType.Manual).SetId(batch.TweenId)
                .SetAutoKill(true).SetRecyclable(false)
                .OnComplete(() =>
                {
                    if (!ReferenceEquals(selectionAction, batch) || !batch.Operation.keepWaiting) return;
                    batch.Tween = null;
                    if (!batch.Ready) EmitReady(batch, marker);
                    batch.Operation.Complete();
                });
            batch.Tween.ForceInit();
            return batch.Operation;
        }

        private void EmitReady(SelectionAction batch, Action<string> marker)
        {
            batch.Ready = true;
            try { marker?.Invoke("Prepared"); }
            catch (Exception error) { ReleaseSelectionAction(batch); batch.Operation.Fail(error); }
        }

        public void CancelSelectionAction(CardActionViewOperation expected = null)
        {
            var batch = selectionAction;
            if (batch == null || (expected != null && !ReferenceEquals(expected, batch.Operation))) return;
            if (batch.Operation.keepWaiting) batch.Operation.Cancel();
            else ReleaseSelectionAction(batch);
        }

        private void ReleaseSelectionAction(SelectionAction batch)
        {
            if (ReferenceEquals(selectionAction, batch)) selectionAction = null;
            if (batch.Tween != null) DOTween.Kill(batch.TweenId);
            batch.Tween = null;
            foreach (var view in batch.Views) if (view != null) view.ClearSelectionActionOffset(batch.Owner);
        }

        private void AdvanceSelectionAction(float deltaTime)
        {
            var batch = selectionAction;
            if (batch?.Tween != null && batch.Tween.IsActive()) batch.Tween.ManualUpdate(deltaTime, deltaTime);
        }

        private void ReconcileSelectionAction()
        {
            var batch = selectionAction;
            if (batch == null || !batch.Views.Any(view => view == null || !TryGetView(view.ViewId, out var current) || current != view)) return;
            // After Prepared, the following fact can transfer/remove these instances while the lift settles.
            // That handoff completes preparation; it is not an interrupted or failed rule action.
            if (batch.Ready) batch.Operation.Complete(); else batch.Operation.Cancel();
            ReleaseSelectionAction(batch);
        }
    }
}
