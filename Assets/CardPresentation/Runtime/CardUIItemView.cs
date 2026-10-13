using CardArt;
using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CardPresentation
{
    /// <summary>One inspection card holds this page's local hover projection; it never selects or submits cards.</summary>
    public sealed class CardUIItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private CardUIGraphic graphic;
        [SerializeField] private float hoverScale = 1.095445f;
        private CardVisualData card;
        private CardZoneMotionSettings motion;
        private Tween hoverTween;
        private float amplitude, period, phase, time, idleYaw, idleRoll;
        private int slotSibling;
        public CardVisualData Card => card;
        public CardInteractionState Interaction => card?.Interaction ?? default;
        public CardUIGraphic Graphic => graphic;
        public float IdleYaw => idleYaw;
        public float IdleRoll => idleRoll;
        public bool IsRemaining { get; private set; }

        public void Configure(CardUIGraphic surface) { graphic = surface != null ? surface : throw new ArgumentNullException(nameof(surface)); }
        public void Present(CardVisualData data, bool remaining, CardZoneMotionSettings motionSettings)
        {
            if (graphic == null || data == null) throw new ArgumentNullException();
            motionSettings.Validate();
            bool hovered = card != null && card.ViewId == data.ViewId && Interaction.IsHovered;
            var next = LocalCard(data, hovered); graphic.Present(next, remaining ? 1 : hovered ? .48f : .28f, remaining ? 0 : .65f);
            card = next; IsRemaining = remaining; motion = motionSettings;
            motion.GetProfile(data.ViewId, out amplitude, out period, out phase);
            ApplyPose();
        }
        public void SetSlotSibling(int index) { slotSibling = index; if (!Interaction.IsHovered) transform.SetSiblingIndex(index); }
        private static CardVisualData LocalCard(CardVisualData data, bool hovered) => new CardVisualData(data.ViewId,
            data.FaceArtId, data.CardId, data.Tint, data.Effects, data.IsDisabled, new CardInteractionState(isHovered: hovered));
        public void SetHovered(bool hovered, bool animate = true)
        {
            if (card == null || hovered == Interaction.IsHovered) return;
            card = LocalCard(card, hovered);
            graphic.Present(card, IsRemaining ? 1 : hovered ? .48f : .28f, IsRemaining ? 0 : .65f);
            var surface = graphic.rectTransform;
            hoverTween?.Kill(); hoverTween = null;
            if (hovered) transform.SetAsLastSibling();
            else transform.SetSiblingIndex(slotSibling);
            float scale = hovered ? hoverScale : 1;
            if (animate) hoverTween = surface.DOScale(Vector3.one * scale, hovered ? .12f : .14f).SetEase(Ease.OutBack).SetUpdate(true);
            else surface.localScale = Vector3.one * scale;
        }
        public void AdvanceMotion(float delta)
        {
            if (!CardZoneRowLayout.Finite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            time += delta; ApplyPose();
        }
        private void ApplyPose()
        {
            if (graphic == null || card == null) return;
            idleYaw = motion.Enabled ? amplitude * Mathf.Sin((time / period + phase) * Mathf.PI * 2) : 0;
            // Unlit Canvas needs a small in-plane turn to make the hand's slight Y tilt perceptible.
            idleRoll = motion.Enabled ? amplitude * .55f * Mathf.Sin((time / period + phase + .25f) * Mathf.PI * 2) : 0;
            graphic.rectTransform.localRotation = Quaternion.Euler(0, idleYaw, idleRoll);
        }
        public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);
        public void OnPointerExit(PointerEventData eventData) => SetHovered(false);
        private void OnDisable()
        {
            hoverTween?.Kill(); hoverTween = null;
            if (card != null) card = LocalCard(card, false);
            if (graphic != null)
            {
                graphic.rectTransform.localScale = Vector3.one; graphic.rectTransform.localRotation = Quaternion.identity;
                if (card != null) graphic.Present(card, IsRemaining ? 1 : .28f, IsRemaining ? 0 : .65f);
            }
        }
        private void OnDestroy() { hoverTween?.Kill(); }
    }
}
