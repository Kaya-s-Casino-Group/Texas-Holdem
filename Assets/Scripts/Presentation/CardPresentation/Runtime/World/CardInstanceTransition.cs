using DG.Tweening;
using UnityEngine;

namespace CardPresentation
{
    /// <summary>One owned, manually clocked transition; retargets from its displayed pose, never from stale layout.</summary>
    internal sealed class CardInstanceTransition
    {
        private readonly CardInstanceView view;
        private readonly object tweenId = new object();
        private Tween tween;
        private CardZoneCardPose target;
        private CardZoneCardPose start;
        private CardZoneTransitionSettings settings;
        private float elapsed, delay, flight, startFlipAngle;
        private bool hasStarted, pendingStart, pendingDiscardStart;
        private bool hasArrived, pendingArrival;
        private CardZoneDiscardSettings discardSettings;
        private Vector3 exitOffset;
        private float exitSway;
        public bool IsAnimating => tween != null && tween.IsActive();
        public bool IsEntering { get; private set; }
        public bool IsExiting { get; private set; }
        public float ExitSecondsRemaining => IsExiting && IsAnimating ? Mathf.Max(0, delay + flight - elapsed) : 0;
        public float SecondsUntilExitTravelProgress(float progress) => IsExiting && IsAnimating
            ? Mathf.Max(0, delay + flight * Mathf.Pow(Mathf.Clamp01(progress), 1f / 3f) - elapsed) : 0;

        public CardInstanceTransition(CardInstanceView view) { this.view = view; }

        public void SetImmediate(CardZoneCardPose pose)
        {
            Kill(); target = pose; view.ApplyLayoutPose(pose); view.ResetTransition();
        }

        public void Enter(CardZoneCardPose pose, Vector3 source, float wait, CardZoneTransitionSettings tuning)
        {
            Kill(); target = pose; start = new CardZoneCardPose(source, Quaternion.identity);
            settings = tuning; delay = wait; flight = settings.FlightSeconds; IsEntering = true;
            hasStarted = false; hasArrived = false;
            startFlipAngle = view.Data.FaceUp ? 180 : 0;
            view.ApplyLayoutPose(pose); StartClock(delay + flight + settings.SettleSeconds);
        }

        public void MoveTo(CardZoneCardPose pose, CardZoneTransitionSettings tuning)
        {
            if (pose.Position == target.Position && pose.Rotation == target.Rotation) return;
            var displayed = view.DisplayPose;
            float displayedFlip = view.DealFlipAngle;
            bool entering = IsEntering && IsAnimating;
            float waiting = Mathf.Max(0, delay - elapsed);
            float remainingFlight = Mathf.Max(0, delay + flight - elapsed);
            var previousStart = start;
            bool startedPending = pendingStart, arrivalPending = pendingArrival;
            Kill(); target = pose; settings = tuning; view.ApplyLayoutPose(pose);
            // Retargeting the same arrival must not lose an already reached cue before its owner consumes it.
            pendingStart = startedPending; pendingArrival = arrivalPending;
            if (entering && remainingFlight > 0)
            {
                // Preserve the scheduled start for queued cards, and the remaining flight for cards already moving.
                IsEntering = true; start = waiting > 0 ? previousStart : displayed;
                startFlipAngle = displayedFlip;
                delay = waiting; flight = Mathf.Max(0.01f, remainingFlight - waiting);
                StartClock(delay + flight + settings.SettleSeconds);
            }
            else
            {
                start = displayed; delay = 0; flight = settings.ReflowSeconds;
                StartReflow();
            }
        }

        public void Exit(Vector3 destination, float wait, CardZoneDiscardSettings tuning, float sway)
        {
            var displayed = view.DisplayPose;
            float flip = view.DealFlipAngle;
            Kill(); IsExiting = true; start = displayed;
            hasStarted = false;
            target = new CardZoneCardPose(destination, Quaternion.identity);
            discardSettings = tuning; delay = wait; flight = tuning.FlightSeconds;
            startFlipAngle = flip; exitOffset = view.InteractionOffset / view.PresentationScale; exitSway = sway;
            StartClock(delay + flight);
        }

        private void StartClock(float duration)
        {
            elapsed = 0;
            OwnTween(DOTween.To(() => elapsed, value => { elapsed = value; ApplyFrame(); }, duration, duration).SetEase(Ease.Linear));
            tween.ForceInit(); ApplyFrame();
        }

        private void StartReflow()
        {
            float progress = 0;
            var destination = new CardZoneCardPose(target.Position, CardInstanceView.UprightRotation(target.Rotation));
            OwnTween(DOTween.To(() => progress, value => {
                progress = value;
                view.ApplyDisplayPose(new CardZoneCardPose(Vector3.LerpUnclamped(start.Position, destination.Position, value),
                    Quaternion.SlerpUnclamped(start.Rotation, destination.Rotation, value)));
            }, 1, settings.ReflowSeconds).SetEase(Ease.OutBack, settings.ReflowOvershoot));
            view.SetDealFlipAngle(0); view.SetTransitionVisible(true);
            tween.ForceInit(); view.ApplyDisplayPose(start);
        }

        private void OwnTween(Tween animation)
        {
            tween = animation.SetUpdate(UpdateType.Manual).SetAutoKill(true).SetRecyclable(false)
                .SetTarget(view.TransitionRoot).SetId(tweenId)
                .OnComplete(() => {
                    tween = null; IsEntering = false;
                    // The owner releases departures at the destination, without a reset/settle frame.
                    if (!IsExiting) view.ResetTransition();
                });
        }

        private void ApplyFrame()
        {
            if (IsExiting)
            {
                if (elapsed >= delay && !hasStarted) { hasStarted = true; pendingDiscardStart = true; }
                float progress = Mathf.Clamp01((elapsed - delay) / flight);
                // Reverse of the arrival's OutCubic: accelerate away, with no landing rebound.
                float exitTravel = progress * progress * progress;
                float flip = Mathf.SmoothStep(0, 1, progress);
                var position = Vector3.Lerp(start.Position, target.Position, exitTravel);
                float arc = Mathf.Sin(progress * Mathf.PI);
                position.y += arc * arc * discardSettings.FlightLift * view.PresentationScale;
                view.SetDiscardPose(exitOffset * (1 - exitTravel), exitSway * (1 - flip));
                view.SetDealFlipAngle(Mathf.Lerp(startFlipAngle, 180, flip));
                view.ApplyDisplayPose(new CardZoneCardPose(position, Quaternion.Slerp(start.Rotation, target.Rotation, flip)));
                return;
            }
            view.SetTransitionVisible(!IsEntering || elapsed >= delay);
            var destination = new CardZoneCardPose(target.Position, CardInstanceView.UprightRotation(target.Rotation));
            if (IsEntering && elapsed < delay)
            { view.SetDealFlipAngle(startFlipAngle); view.ApplyDisplayPose(start); return; }
            if (IsEntering && !hasStarted) { hasStarted = true; pendingStart = true; }
            if (IsEntering && elapsed >= delay + flight && !hasArrived) { hasArrived = true; pendingArrival = true; }
            float t = Mathf.Clamp01((elapsed - delay) / flight);
            float eased = 1 - Mathf.Pow(1 - t, 3);
            // Flip only during flight, with zero angular speed at launch/landing. Hidden faces need no extra turn.
            view.SetDealFlipAngle(IsEntering ? Mathf.Lerp(startFlipAngle, 0, Mathf.SmoothStep(0, 1, t)) : 0);
            var travel = destination.Position - start.Position; travel.y = 0;
            var overshoot = travel.normalized * settings.OvershootDistance * view.PresentationScale;
            float yaw = (travel.x <= 0 ? -1 : 1) * settings.LandingYaw;
            var landingRotation = destination.Rotation * Quaternion.Euler(0, yaw, 0);
            if (elapsed < delay + flight)
            {
                var position = Vector3.LerpUnclamped(start.Position, destination.Position + overshoot, eased);
                // A squared arc reaches the landing with zero vertical velocity, matching the settle phase.
                float lift = Mathf.Sin(t * Mathf.PI);
                position.y += lift * lift * settings.FlightLift * view.PresentationScale;
                view.ApplyDisplayPose(new CardZoneCardPose(position, Quaternion.SlerpUnclamped(start.Rotation, landingRotation, eased)));
            }
            else
            {
                float settle = Mathf.Clamp01((elapsed - delay - flight) / settings.SettleSeconds);
                // One broad return with a small residual sway; both ends have zero velocity.
                float remaining = 1 - settle;
                float decay = remaining * remaining * (1 + 2 * settle);
                float spring = decay * Mathf.Cos(settle * 2 * Mathf.PI);
                view.ApplyDisplayPose(new CardZoneCardPose(destination.Position + overshoot * spring,
                    destination.Rotation * Quaternion.Euler(0, yaw * spring, 0)));
            }
        }

        public void Advance(float deltaTime) { if (IsAnimating) tween.ManualUpdate(deltaTime, deltaTime); }
        public bool ConsumeStart() { bool result = pendingStart; pendingStart = false; return result; }
        public bool ConsumeArrival() { bool result = pendingArrival; pendingArrival = false; return result; }
        public bool ConsumeDiscardStart() { bool result = pendingDiscardStart; pendingDiscardStart = false; return result; }
        public void Complete() { Kill(); view.ResetTransition(); }
        private void Kill()
        {
            // Filter by private ID so edit-mode cleanup works even before DOTween's runtime initialization.
            if (tween != null) DOTween.Kill(tweenId);
            tween = null; IsEntering = false; IsExiting = false; pendingStart = false; pendingArrival = false; pendingDiscardStart = false;
        }
    }
}
