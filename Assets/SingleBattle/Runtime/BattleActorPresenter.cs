using System;
using System.Collections;
using DG.Tweening;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Replaceable demo presentation. Owns model transforms and timings, not unit identity, HP or turns.</summary>
    [DisallowMultipleComponent]
    public class BattleActorPresenter : MonoBehaviour, IBattleActorPresentation
    {
        [SerializeField] private Transform model;
        [SerializeField] private MonoBehaviour audioPresentation;
        [SerializeField, Min(0)] private float prepareSeconds = .8f, attackSeconds = .2f, hitSeconds = .2f, deathSeconds = .35f;
        [SerializeField] private Vector3 attackOffset = new Vector3(.06f, 0, 0), hitPunch = new Vector3(.035f, 0, 0);
        [SerializeField, Min(1)] private int hitVibrato = 6;
        private Vector3 restPosition, restScale;
        private Quaternion restRotation;
        private bool hasRestPose;
        private Coroutine playback;
        private BattlePresentationOperation active;
        private Tween motion;
        public Transform Model => model;
        public IBattlePresentationAudio Audio => audioPresentation != null ? audioPresentation as IBattlePresentationAudio : null;
        public void ConfigureAudio(MonoBehaviour implementation)
        {
            if (implementation != null && (!(implementation is IBattlePresentationAudio) ||
                implementation.transform != transform && !implementation.transform.IsChildOf(transform)))
                throw new ArgumentException("Bind an audio implementation in this presentation subtree.");
            StopSounds(); audioPresentation = implementation;
        }
        public virtual bool UsesDeathTimeline => false;
        public void ConfigureAttackOffset(Vector3 offset) => attackOffset = offset;
        public virtual void Synchronize(CombatantState unit) { }

        public void Configure(Transform target)
        {
            if (target == null || target != transform && !target.IsChildOf(transform)) throw new ArgumentException("Bind a model in this presentation subtree.");
            ResetPresentation(); model = target; hasRestPose = false; CapturePose();
        }
        private void CapturePose()
        {
            if (hasRestPose || model == null) return;
            restPosition = model.localPosition; restRotation = model.localRotation; restScale = model.localScale; hasRestPose = true;
        }
        protected void RestorePose()
        {
            if (model == null || !hasRestPose) return;
            model.localPosition = restPosition; model.localRotation = restRotation; model.localScale = restScale;
        }
        public BattlePresentationOperation Play(BattlePresentationCue cue)
        {
            if (cue == null) throw new ArgumentNullException(nameof(cue));
            if (active != null && active.keepWaiting) throw new InvalidOperationException("Presentation operations must be sequenced.");
            CapturePose(); active = new BattlePresentationOperation();
            if (!Application.isPlaying || !isActiveAndEnabled || model == null) { Snap(cue); return BattlePresentationOperation.Completed(); }
            var operation = active;
            try { if (cue.Kind != BattlePresentationKind.PrepareAttack) Audio?.Play(cue.Kind); }
            catch (Exception error) { Debug.LogWarning("Battle sound skipped: " + error.Message, this); }
            playback = StartCoroutine(Run(cue, operation));
            if (!operation.keepWaiting) playback = null;
            return operation;
        }
        private IEnumerator Run(BattlePresentationCue cue, BattlePresentationOperation operation)
        {
            var sequence = cue.Kind == BattlePresentationKind.Death ? PlayDeath() : cue.Kind == BattlePresentationKind.PrepareAttack
                ? PrepareAttack() : Pulse(cue.Kind == BattlePresentationKind.Hit);
            try
            {
                while (operation.keepWaiting)
                {
                    bool next = false; object current = null; Exception failure = null;
                    try { next = sequence.MoveNext(); if (next) current = sequence.Current; }
                    catch (Exception error) { failure = error; }
                    if (failure != null) { operation.Fail(failure); yield break; }
                    if (!next) { operation.Complete(); yield break; }
                    yield return current;
                }
            }
            finally { (sequence as IDisposable)?.Dispose(); operation.Cancel(); playback = null; }
        }
        protected virtual IEnumerator PrepareAttack()
        { if (prepareSeconds > 0) yield return new WaitForSecondsRealtime(prepareSeconds); }
        private IEnumerator Pulse(bool hit)
        {
            float seconds = hit ? hitSeconds : attackSeconds;
            if (seconds <= 0) { RestorePose(); yield break; }
            bool completed = false;
            if (hit) motion = model.DOPunchPosition(hitPunch, seconds, hitVibrato, .5f);
            else
            {
                motion = DOTween.Sequence()
                    .Append(model.DOLocalMove(restPosition + attackOffset, seconds * .4f).SetEase(Ease.OutQuad))
                    .Append(model.DOLocalMove(restPosition, seconds * .6f).SetEase(Ease.InOutQuad));
            }
            motion.SetAutoKill(true).SetUpdate(true).OnComplete(() => completed = true);
            while (!completed && motion != null && motion.IsActive()) yield return null;
            motion = null;
            if (!completed) throw new InvalidOperationException("The DOTween motion was interrupted.");
            RestorePose();
        }
        protected virtual IEnumerator PlayDeath()
        {
            for (float elapsed = 0; elapsed < deathSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float progress = elapsed / deathSeconds;
                model.localRotation = restRotation * Quaternion.Euler(0, 0, 25 * progress);
                model.localScale = restScale * Mathf.Lerp(1, .75f, progress);
                yield return null;
            }
            SnapDeath();
        }
        protected virtual void SnapDeath()
        { model.localRotation = restRotation * Quaternion.Euler(0, 0, 25); model.localScale = restScale * .75f; }
        public void Snap(BattlePresentationCue cue)
        {
            CancelPresentation(); CapturePose(); RestorePose();
            if (model != null && cue.Kind == BattlePresentationKind.Death) SnapDeath();
        }
        public virtual void CancelPresentation()
        {
            active?.Cancel();
            StopSounds();
            motion?.Kill(); motion = null;
            if (playback != null) { StopCoroutine(playback); playback = null; }
            active = null;
        }
        public virtual void ResetPresentation() { CancelPresentation(); RestorePose(); }
        private void StopSounds()
        {
            try { Audio?.StopSounds(); }
            catch (Exception error) { Debug.LogWarning("Battle sound cleanup failed: " + error.Message, this); }
        }
        protected virtual void OnDisable() => ResetPresentation();
    }
}
