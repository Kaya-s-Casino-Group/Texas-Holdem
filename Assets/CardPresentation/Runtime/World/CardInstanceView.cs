using System;
using CardArt;
using DG.Tweening;
using PokerEngine;
using UnityEngine;

namespace CardPresentation
{
    /// <summary>A scene instance with display identity; its child CardView remains an artwork renderer.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed partial class CardInstanceView : MonoBehaviour
    {
        public CardVisualData Data { get; private set; }
        public string ViewId => Data?.ViewId;
        public CardInstanceId? CardId => Data?.CardId;
        public CardView Surface { get; private set; }
        public Transform TransitionRoot { get; private set; }
        public Transform FaceRoot { get; private set; }
        public Transform DealFlipRoot { get; private set; }
        public float DealFlipAngle { get; private set; }
        public Transform SwayRoot { get; private set; }
        public CardShaderAppearance Appearance => Surface != null ? Surface.Appearance : null;
        private CardZoneMotionSettings motionSettings;
        private Tween swayTween;
        private readonly object swayTweenId = new object();
        private float swayAngle;
        private bool resumeSwayFromCurrent;
        private bool interactionSelected;
        private CardInstanceTransition transition;
        private Renderer surfaceRenderer;
        private Vector3 interactionOffset, interactionTarget;
        private Tween liftTween;
        private readonly object liftTweenId = new object();
        private const float SelectionOvershootScale = 1.35f;
        public float PresentationScale { get; private set; } = 1;
        public Vector3 InteractionOffset => (interactionOffset + selectionDeniedOffset) * PresentationScale;
        public bool IsLifting => liftTween != null && liftTween.IsActive();
        public bool IsTransitioning => IsTransferring || (transition != null && transition.IsAnimating);
        public bool IsEntering => IsTransferring || (transition != null && transition.IsEntering);
        public bool IsExiting => transition != null && transition.IsExiting;
        internal bool IsTransitionVisible => surfaceRenderer != null && surfaceRenderer.enabled;
        /// <summary>Displayed zone-local pose before hover, face flip and idle sway; Rotation describes upright artwork.</summary>
        public CardZoneCardPose DisplayPose => new CardZoneCardPose(
            transform.localPosition + transform.localRotation * TransitionRoot.localPosition,
            transform.localRotation * TransitionRoot.localRotation * ArtworkCorrection(transform.localRotation));

        internal void Initialize(GameObject prefab, Bounds bounds)
        {
            TransferRoot = new GameObject("Transfer").transform;
            TransferRoot.SetParent(transform, false);
            TransitionRoot = new GameObject("Transition").transform;
            TransitionRoot.SetParent(TransferRoot, false);
            FaceRoot = new GameObject("Face").transform;
            FaceRoot.SetParent(TransitionRoot, false);
            DealFlipRoot = new GameObject("DealFlip").transform;
            InitializeHoverRoot();
            DealFlipRoot.SetParent(HoverRoot, false);
            SwayRoot = new GameObject("Sway").transform;
            SwayRoot.SetParent(DealFlipRoot, false);
            var visual = Instantiate(prefab, SwayRoot, false);
            visual.name = prefab.name;
            visual.transform.localPosition = -bounds.center;
            visual.transform.localRotation = Quaternion.identity;
            visual.SetActive(true);
            Surface = visual.GetComponentInChildren<CardView>(true);
            surfaceRenderer = Surface.GetComponent<MeshRenderer>();
            transition = new CardInstanceTransition(this);
            foreach (var child in GetComponentsInChildren<Transform>(true)) child.gameObject.layer = gameObject.layer;
            // Input uses independent geometry ray tests; art-prefab physics colliders remain disabled.
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }

        internal void Present(CardVisualData data, Vector3 hoveredOffset, Vector3 selectedOffset, CardShaderAppearance appearance, CardZoneLiftSettings lift, bool animate, bool hoverScaling = false)
        {
            bool initialized = Data != null;
            // Unknown faces never retain an old known front texture, even when the same view is reused.
            Surface.SetCard(data.FaceArtId ?? "card_back");
            Data = data;
            ApplyAppearance(appearance);
            ApplyFaceRotation();
            ApplyInteractionOffset(hoveredOffset, selectedOffset, lift, animate && initialized, hoverScaling);
        }

        internal void ApplyLayoutPose(CardZoneCardPose pose)
        {
            transform.localPosition = pose.Position; transform.localRotation = pose.Rotation;
            if (Data != null) ApplyFaceRotation();
            RefreshInteractionPose();
        }

        internal void SetPresentationScale(float scale)
        {
            PresentationScale = scale;
            SwayRoot.localScale = Vector3.one * scale;
            // Tween values stay in prefab units: changing size never restarts an interaction tween.
            RefreshInteractionPose();
        }

        internal void SetPose(CardZoneCardPose pose, CardZoneTransitionSettings settings, bool animate,
            bool entering = false, Vector3 source = default(Vector3), float delay = 0)
        {
            if (!animate) transition.SetImmediate(pose);
            else if (entering) transition.Enter(pose, source, delay, settings);
            else transition.MoveTo(pose, settings);
            RefreshTransferPose();
        }

        internal void BeginDiscard(Vector3 destination, float delay, CardZoneDiscardSettings settings)
        {
            // Preserve the displayed lift and sway, including selection interrupted midway through its rise.
            KillLift(); KillHoverScale(); KillSwayTween(); CancelSelectionDenied();
            transition.Exit(destination, delay, settings, swayAngle);
        }

        internal void SetDiscardPose(Vector3 offset, float sway)
        {
            interactionOffset = offset; SetSwayAngle(sway); RefreshInteractionPose();
        }

        internal static Quaternion UprightRotation(Quaternion rotation) => rotation * ArtworkCorrection(rotation);
        private static Quaternion ArtworkCorrection(Quaternion rotation) =>
            (rotation * Vector3.forward).z < 0 ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;

        internal void ApplyDisplayPose(CardZoneCardPose pose)
        {
            TransitionRoot.localPosition = Quaternion.Inverse(transform.localRotation) * (pose.Position - transform.localPosition);
            TransitionRoot.localRotation = Quaternion.Inverse(transform.localRotation) * pose.Rotation *
                Quaternion.Inverse(ArtworkCorrection(transform.localRotation));
            RefreshInteractionPose();
        }

        internal void ResetTransition()
        {
            if (TransitionRoot == null) return;
            TransitionRoot.localPosition = Vector3.zero; TransitionRoot.localRotation = Quaternion.identity;
            SetDealFlipAngle(0); SetTransitionVisible(true); RefreshInteractionPose();
        }

        // Cosmetic offset only: a visible card is already bound to its final face/effects while flying back-up.
        internal void SetDealFlipAngle(float angle)
        {
            DealFlipAngle = Data != null && Data.FaceUp ? angle : 0;
            if (DealFlipRoot != null) DealFlipRoot.localRotation = Quaternion.Euler(0, 0, DealFlipAngle);
        }

        internal void SetTransitionVisible(bool visible) { if (surfaceRenderer != null) surfaceRenderer.enabled = visible; }
        internal void CompleteTransition() { transition?.Complete(); CompleteTransfer(); CancelSelectionDenied(); }
        internal bool ConsumeDealStart() => transition != null && transition.ConsumeStart();
        internal bool ConsumeDealArrival() => transition != null && transition.ConsumeArrival();
        internal bool ConsumeDiscardStart() => transition != null && transition.ConsumeDiscardStart();

        private void ApplyFaceRotation()
        {
            // The layout root points +Z inward. Artwork keeps the same reading direction as a straight row,
            // even when a positive arc puts its center behind the cards and turns that root by half a turn.
            var upright = ArtworkCorrection(transform.localRotation);
            FaceRoot.localRotation = upright * (Data.FaceUp ? Quaternion.identity : Quaternion.Euler(0, 0, 180));
            if (!Data.FaceUp) SetDealFlipAngle(0);
        }

        internal static CardShaderAppearance PrepareArtwork(CardView surface, CardVisualData card)
        {
            ValidateArt(surface, card.FaceArtId ?? "card_back");
            var appearance = CardAppearanceResolver.Resolve(card,surface.ArtProfile);
            appearance.ValidateFor(surface); return appearance;
        }

        internal void ApplyAppearance(CardShaderAppearance appearance)
        {
            Surface.PresentArtwork(CardAppearanceResolver.Combinations(Data),CardAppearanceResolver.Tint(Data), appearance);
        }

        // Offset belongs to the visual child, so zone reflow and flip rotation cannot accumulate or invert it.
        internal void ApplyInteractionOffset(Vector3 hoveredOffset, Vector3 selectedOffset, CardZoneLiftSettings settings, bool animate = true, bool hoverScaling = false)
        {
            ApplyHoverScale(hoverScaling && Data != null && Data.Interaction.IsHovered, settings, animate);
            bool selected = Data != null && Data.Interaction.IsSelected;
            bool selectionChanged = selected != interactionSelected;
            if (selected && selectionChanged)
            { interactionOffset += selectionDeniedOffset; CancelSelectionDenied(); }
            interactionSelected = selected;
            var next = selected ? selectedOffset : Vector3.zero;
            if (selected)
            {
                KillSwayTween(); // Stop idle motion at its actual angle, then let the lift bring it to zero.
                resumeSwayFromCurrent = true;
            }
            if (!animate || !settings.Enabled || !isActiveAndEnabled)
            { interactionTarget = next; CompleteLift(); return; }
            if (next == interactionTarget && !selectionChanged) { RefreshInteractionPose(); return; }
            KillLift(); interactionTarget = next;
            var fromOffset = interactionOffset;
            float fromAngle = swayAngle;
            float progress = 0;
            float duration = next.sqrMagnitude < interactionOffset.sqrMagnitude ? settings.LowerSeconds : settings.RaiseSeconds;
            // Emphasize the second rise into selection, while preserving zero as "no rebound".
            float overshoot = selected ? Mathf.Min(3, settings.Overshoot * SelectionOvershootScale) : settings.Overshoot;
            // Unclamped shared progress lets both height and selection's sway centering overshoot, then settle together.
            liftTween = DOTween.To(() => progress, value => {
                    progress = value;
                    interactionOffset = Vector3.LerpUnclamped(fromOffset, next, value);
                    if (selected && motionSettings.Enabled && motionSettings.MaxSwayAngle > 0)
                        SetSwayAngle(Mathf.LerpUnclamped(fromAngle, 0, value));
                    RefreshInteractionPose();
                }, 1, duration)
                .SetEase(Ease.OutBack, overshoot).SetUpdate(UpdateType.Manual).SetAutoKill(true).SetRecyclable(false)
                .SetTarget(FaceRoot).SetId(liftTweenId)
                .OnComplete(() => {
                    liftTween = null; interactionOffset = interactionTarget;
                    if (interactionSelected) SetSwayAngle(0);
                    RefreshInteractionPose();
                });
            liftTween.ForceInit();
        }

        private void RefreshInteractionPose()
        {
            if (FaceRoot != null && TransitionRoot != null)
                FaceRoot.localPosition = Quaternion.Inverse(transform.localRotation * TransitionRoot.localRotation) * InteractionOffset;
        }

        private void KillLift()
        {
            if (liftTween != null) DOTween.Kill(liftTweenId);
            liftTween = null;
        }

        internal void CompleteLift()
        {
            KillLift(); CompleteHoverScale(); interactionOffset = interactionTarget;
            if (interactionSelected) SetSwayAngle(0);
            RefreshInteractionPose();
        }

        internal void ConfigureMotion(CardZoneMotionSettings settings)
        {
            if (motionSettings.Equals(settings)) return;
            StopMotion(); motionSettings = settings;
        }

        internal void AdvanceMotion(float deltaTime)
        {
            if (!isActiveAndEnabled || Data == null) return;
            transition.Advance(deltaTime);
            AdvanceTransfer(deltaTime);
            if (IsExiting) return;
            if (IsLifting) liftTween.ManualUpdate(deltaTime, deltaTime);
            if (IsHoverScaling) hoverTween.ManualUpdate(deltaTime, deltaTime);
            if (IsSelectionDeniedAnimating) selectionDeniedTween.ManualUpdate(deltaTime, deltaTime);
            // During selection the lift owns sway centering; idle motion resumes only after deselection.
            if (Data.Interaction.IsSelected || !motionSettings.Enabled || motionSettings.MaxSwayAngle == 0) return;
            if (swayTween == null || !swayTween.IsActive())
            {
                motionSettings.GetProfile(ViewId, out float amplitude, out float period, out float phase);
                float resumeAngle = Mathf.Clamp(swayAngle, -amplitude, amplitude);
                swayAngle = -amplitude;
                swayTween = DOTween.To(() => swayAngle, SetSwayAngle, amplitude, period * 0.5f)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetAutoKill(false).SetRecyclable(false)
                    .SetUpdate(UpdateType.Manual).SetTarget(SwayRoot).SetId(swayTweenId);
                swayTween.ForceInit();
                // Invert the sine ease to resume at the actual angle, even if selection was cancelled mid-lift.
                float resumeTime = Mathf.Acos(-resumeAngle / amplitude) / Mathf.PI * period * 0.5f;
                swayTween.Goto(resumeSwayFromCurrent ? resumeTime : phase * period, true);
                if (resumeSwayFromCurrent) { SetSwayAngle(resumeAngle); resumeSwayFromCurrent = false; }
            }
            // Advance only this owned tween, never the global DOTween manual-update list.
            swayTween.ManualUpdate(deltaTime, deltaTime);
        }

        private void SetSwayAngle(float value)
        {
            swayAngle = value;
            if (SwayRoot != null) SwayRoot.localRotation = Quaternion.Euler(0, value, 0);
        }

        private void KillSwayTween()
        {
            // Filtered Kill also works before DOTween runtime initialization in edit-mode previews.
            if (swayTween != null) { DOTween.Kill(swayTweenId); swayTween = null; }
        }

        internal void StopMotion()
        {
            KillSwayTween(); SetSwayAngle(0);
        }

        private void OnDisable() { CompleteTransition(); CompleteLift(); StopMotion(); }
        private void OnDestroy() { CompleteTransition(); CompleteLift(); StopMotion(); }

        internal static Bounds ValidatePrefab(GameObject prefab, out CardView surface)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            var surfaces = prefab.GetComponentsInChildren<CardView>(true);
            if (surfaces.Length != 1 || prefab.GetComponentInChildren<Rigidbody>(true) != null)
                throw new ArgumentException("Use a visual card prefab with exactly one CardView and no Rigidbody.", nameof(prefab));
            surface = surfaces[0];
            var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
            var material = surface.GetComponent<MeshRenderer>().sharedMaterial;
            if (mesh == null || surface.Atlas == null || surface.Atlas.texture == null ||
                CardShaderProperties.GetContractErrors(material).Count != 0 || material.GetTexture(CardShaderProperties.BaseMap) != surface.Atlas.texture)
                throw new ArgumentException("The prefab must have a configured card mesh, material and atlas.", nameof(prefab));
            ValidateArt(surface, "card_back"); ValidateArt(surface, surface.CardId);
            var matrix = Matrix4x4.Scale(prefab.transform.localScale) * prefab.transform.worldToLocalMatrix * surface.transform.localToWorldMatrix;
            var bounds = mesh.bounds; var result = new Bounds(); bool first = true;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var corner = matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                        if (!CardZoneRowLayout.Finite(corner.x) || !CardZoneRowLayout.Finite(corner.y) || !CardZoneRowLayout.Finite(corner.z))
                            throw new ArgumentException("Invalid prefab transform or mesh bounds.", nameof(prefab));
                        if (first) { result = new Bounds(corner, Vector3.zero); first = false; } else result.Encapsulate(corner);
                    }
            return result;
        }

        internal static void ValidateArt(CardView surface, string artId)
        {
            if (!surface.Atlas.TryGetCard(artId, out var region) || region.clipped)
                throw new ArgumentException("Missing or cropped card artwork: " + artId);
        }
    }
}
