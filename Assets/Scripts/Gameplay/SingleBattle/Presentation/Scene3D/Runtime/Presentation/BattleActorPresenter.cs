using System;
using global::BattlePresentation;
using System.Collections;
using DG.Tweening;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Replaceable demo presentation. Owns model transforms and timings, not unit identity, HP or turns.</summary>
    [DisallowMultipleComponent]
    public class BattleActorPresenter : MonoBehaviour, IBattleActorPresentation, IBattleActorDisplayPresentation
    {
        [SerializeField] private Transform model;
        [SerializeField] private MonoBehaviour audioPresentation;
        [SerializeField, Min(0)] private float prepareSeconds = .8f, attackSeconds = .2f, hitSeconds = .2f, deathSeconds = .35f;
        [SerializeField] private Vector3 attackOffset = new Vector3(.06f, 0, 0), hitPunch = new Vector3(.035f, 0, 0);
        [SerializeField, Min(1)] private int hitVibrato = 6;
        [Header("Enemy attack")]
        [SerializeField, Min(0)] private float enemyPrepareSeconds = .65f, enemyAttackSeconds = .85f;
        [SerializeField, Min(0)] private float enemyRetreatDistance = .08f, enemyAttackDistance = .22f;
        [Header("Enemy support action")]
        [SerializeField, Min(0)] private float supportPrepareSeconds = .25f, supportSeconds = .75f;
        private bool IsEnemy => GetComponentInParent<EnemyBattleActor>() != null;
        private Vector3 EnemyDirection => attackOffset.sqrMagnitude > .000001f ? attackOffset.normalized : Vector3.left;
        private Vector3 restPosition, restScale;
        private Quaternion restRotation;
        private bool hasRestPose;
        private bool restoreAttackOnCancel;
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
        public virtual void SynchronizeDisplay(UnitDisplayData unit) { }

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
            if (cue.Kind == BattlePresentationKind.Revive)
            {
                ResetPresentation();
                if (cue.DisplayEffect?.After != null) SynchronizeDisplay(cue.DisplayEffect.After);
                else if (cue.Effect?.After != null) Synchronize(cue.Effect.After);
                return BattlePresentationOperation.Completed();
            }
            if (active != null && active.keepWaiting) throw new InvalidOperationException("Presentation operations must be sequenced.");
            if (cue.Kind == BattlePresentationKind.GainBlock || cue.Kind == BattlePresentationKind.ClearBlock ||
                cue.Kind == BattlePresentationKind.BlockHit && cue.HealthLost == 0)
                return BattlePresentationOperation.Completed();
            if (cue.Kind == BattlePresentationKind.BlockHit) cue = cue.WithKind(BattlePresentationKind.Hit);
            CapturePose(); active = new BattlePresentationOperation();
            restoreAttackOnCancel = cue.Kind == BattlePresentationKind.Attack || cue.Kind == BattlePresentationKind.PrepareAttack ||
                cue.Kind == BattlePresentationKind.Support || cue.Kind == BattlePresentationKind.PrepareSupport;
            if (!Application.isPlaying || !isActiveAndEnabled || model == null)
            {
                Snap(cue);
                if (cue.Kind == BattlePresentationKind.Attack || cue.Kind == BattlePresentationKind.Support) cue.Markers?.Emit("Impact");
                return BattlePresentationOperation.Completed();
            }
            var operation = active;
            try { if (cue.Kind != BattlePresentationKind.PrepareAttack && cue.Kind != BattlePresentationKind.PrepareSupport &&
                cue.Kind != BattlePresentationKind.Support) Audio?.Play(cue.Kind); }
            catch (Exception error) { Debug.LogWarning("Battle sound skipped: " + error.Message, this); }
            playback = StartCoroutine(Run(cue, operation));
            if (!operation.keepWaiting) playback = null;
            return operation;
        }
        private IEnumerator Run(BattlePresentationCue cue, BattlePresentationOperation operation)
        {
            var sequence = cue.Kind == BattlePresentationKind.Death ? PlayDeath() : cue.Kind == BattlePresentationKind.PrepareAttack
                ? PrepareAttack() : cue.Kind == BattlePresentationKind.PrepareSupport ? PrepareSupport() : Pulse(cue, operation);
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
        {
            if (!IsEnemy) { if (prepareSeconds > 0) yield return new WaitForSecondsRealtime(prepareSeconds); yield break; }
            // Leave the charged pose in place for the following Attack operation.
            float duration = Mathf.Max(.001f, enemyPrepareSeconds);
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / (duration * .7f)));
                model.localPosition = restPosition - EnemyDirection * (enemyRetreatDistance * t);
                model.localScale = Vector3.Scale(restScale, new Vector3(1 - .05f * t, 1 + .04f * t, 1));
                yield return null;
            }
            model.localPosition = restPosition - EnemyDirection * enemyRetreatDistance;
        }
        private IEnumerator Pulse(BattlePresentationCue cue, BattlePresentationOperation operation)
        {
            bool hit = cue.Kind == BattlePresentationKind.Hit;
            bool support = cue.Kind == BattlePresentationKind.Support;
            bool enemyAttack = !hit && IsEnemy;
            float seconds = hit ? hitSeconds : support ? supportSeconds : enemyAttack ? enemyAttackSeconds : attackSeconds;
            if (seconds <= 0)
            {
                if (!hit) EmitImpact(cue, operation);
                RestorePose(); yield break;
            }
            bool completed = false;
            if (hit) motion = model.DOPunchPosition(hitPunch, seconds, hitVibrato, .5f);
            else if (support)
            {
                motion = DOTween.Sequence()
                    .Append(model.DOLocalRotateQuaternion(restRotation * Quaternion.Euler(0, 0, 4), seconds * .4f).SetEase(Ease.InOutSine))
                    .Join(model.DOScale(restScale * 1.06f, seconds * .4f).SetEase(Ease.OutSine))
                    .AppendCallback(() => EmitImpact(cue, operation))
                    .Append(model.DOLocalRotateQuaternion(restRotation * Quaternion.Euler(0, 0, -2), seconds * .25f).SetEase(Ease.InOutSine))
                    .Join(model.DOScale(restScale * .985f, seconds * .25f).SetEase(Ease.InOutSine))
                    .Append(model.DOLocalRotateQuaternion(restRotation, seconds * .35f).SetEase(Ease.OutSine))
                    .Join(model.DOScale(restScale, seconds * .35f).SetEase(Ease.OutSine));
            }
            else if (enemyAttack)
            {
                motion = DOTween.Sequence()
                    .Append(model.DOLocalMove(restPosition + EnemyDirection * enemyAttackDistance, seconds * .19f).SetEase(Ease.InCubic))
                    .Join(model.DOScale(Vector3.Scale(restScale, new Vector3(1.07f, .94f, 1)), seconds * .19f))
                    .AppendCallback(() => EmitImpact(cue, operation))
                    .AppendInterval(seconds * .12f)
                    .Append(model.DOLocalMove(restPosition, seconds * .69f).SetEase(Ease.OutCubic))
                    .Join(model.DOScale(restScale, seconds * .69f).SetEase(Ease.OutCubic));
            }
            else
            {
                motion = DOTween.Sequence()
                    .Append(model.DOLocalMove(restPosition + attackOffset, seconds * .4f).SetEase(Ease.OutQuad))
                    .AppendCallback(() => EmitImpact(cue, operation))
                    .Append(model.DOLocalMove(restPosition, seconds * .6f).SetEase(Ease.InOutQuad));
            }
            motion.SetAutoKill(true).SetUpdate(true).OnComplete(() => completed = true);
            while (!completed && motion != null && motion.IsActive()) yield return null;
            motion = null;
            if (!completed) throw new InvalidOperationException("The DOTween motion was interrupted.");
            RestorePose();
        }
        private void EmitImpact(BattlePresentationCue cue, BattlePresentationOperation operation)
        {
            if (!operation.keepWaiting) return;
            try { cue.Markers?.Emit("Impact"); }
            catch (Exception error) { operation.Fail(error); motion?.Kill(); }
        }
        private IEnumerator PrepareSupport()
        {
            float duration = Mathf.Max(.001f, supportPrepareSeconds);
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                model.localRotation = restRotation * Quaternion.Euler(0, 0, -3 * t);
                model.localScale = restScale * Mathf.Lerp(1, .97f, t);
                yield return null;
            }
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
            if (cue.Kind == BattlePresentationKind.Revive)
            {
                ResetPresentation();
                if (cue.DisplayEffect?.After != null) SynchronizeDisplay(cue.DisplayEffect.After);
                else if (cue.Effect?.After != null) Synchronize(cue.Effect.After);
                return;
            }
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
            if (restoreAttackOnCancel) RestorePose();
            restoreAttackOnCancel = false;
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
