using System;
using System.Globalization;
using TMPro;
using CardArt;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CardPresentation
{
    /// <summary>Atlas-backed pile decoration and local pointer feedback. Never owns or draws logical cards.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DrawPileView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private CardUIGraphic[] cardLayers;
        [SerializeField] private RectTransform motionRoot;
        [SerializeField] private TMP_Text remainingCount;
        [SerializeField] private CardAtlasData atlas;
        [SerializeField] private Material cardMaterial;
        [SerializeField] private string coverCardId = "card_back";
        [SerializeField, Min(1)] private float hoverScale = 1.095445f;
        [SerializeField, Min(.01f)] private float hoverSeconds = .14f;
        private Tween hoverTween;
        public event Action<bool> HoverChanged;
        public event Action Clicked;
        public int RemainingCards { get; private set; }
        public bool IsHovered { get; private set; }
        public CardAtlasData Atlas => atlas;
        public Material CardMaterial => cardMaterial;
        public string CoverCardId => coverCardId;
        public RectTransform MotionRoot => motionRoot;
        public TMP_Text RemainingCount => remainingCount;
        public void BindRemainingCount(TMP_Text label)
        {
            if (label == null) throw new ArgumentNullException(nameof(label));
            remainingCount = label; remainingCount.raycastTarget = false; ApplyCount();
        }
        public bool IsConfigured => atlas != null && cardMaterial != null && motionRoot != null && cardLayers != null &&
            cardLayers.Length > 0 && Array.TrueForAll(cardLayers,image => image != null);

        public void Configure(CardUIGraphic[] images, RectTransform motion, CardAtlasData skin, Material material)
        {
            if (images == null || images.Length == 0 || Array.Exists(images,image => image == null) || motion == null)
                throw new ArgumentException("Bind atlas card layers and their motion root.");
            ValidateArt(skin,material,coverCardId);
            cardLayers = (CardUIGraphic[])images.Clone(); motionRoot = motion; atlas = skin; cardMaterial = material;
            RefreshArt();
        }
        private static void ValidateArt(CardAtlasData skin, Material material, string cover)
        {
            CardUIGraphic.ValidateCard(skin,new CardVisualData("pile/back",null));
            CardUIGraphic.ValidateCard(skin,new CardVisualData("pile/cover",cover));
            if (material == null || material.shader == null || material.shader.name != "CardPresentation/Card Canvas")
                throw new ArgumentException("Use the shared Canvas card material.");
        }
        public void BindAtlas(CardAtlasData skin)
        {
            if (skin == atlas) return;
            ValidateArt(skin,cardMaterial,coverCardId); atlas = skin; RefreshArt();
        }
        public void SetCoverCard(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Select an atlas card id.",nameof(id));
            ValidateArt(atlas,cardMaterial,id); coverCardId = id; RefreshArt();
        }
        private void RefreshArt()
        {
            if (!IsConfigured) return;
            ValidateArt(atlas,cardMaterial,coverCardId);
            for (int i = 0; i < cardLayers.Length; i++)
            {
                cardLayers[i].Bind(atlas,cardMaterial); cardLayers[i].raycastTarget = false;
                cardLayers[i].Present(new CardVisualData("pile/"+i,i == cardLayers.Length-1 && coverCardId != "card_back" ? coverCardId : null),
                    CardAppearanceSettings.Default);
            }
            ApplyCount();
        }
        public void SetRemainingCards(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (RemainingCards == count) return;
            RemainingCards = count; ApplyCount();
        }
        private void ApplyCount()
        {
            if (remainingCount != null) remainingCount.text = RemainingCards.ToString(CultureInfo.InvariantCulture);
            if (!IsConfigured) return;
            int visible = RemainingCards <= 1 ? 1 : Mathf.Clamp(Mathf.CeilToInt(RemainingCards/7f)+1,1,cardLayers.Length);
            for (int i = 0; i < cardLayers.Length; i++)
            {
                bool top = i == cardLayers.Length-1;
                cardLayers[i].gameObject.SetActive(top || i >= cardLayers.Length-visible);
                float shade = top ? (RemainingCards > 0 ? 1 : .4f) : .58f+i*.04f;
                cardLayers[i].color = new Color(shade,shade,shade,1);
            }
        }
        public void SetHovered(bool hovered, bool animate = true)
        {
            if (IsHovered == hovered) return;
            IsHovered = hovered; hoverTween?.Kill(); hoverTween = null;
            if (motionRoot != null)
            {
                var scale = Vector3.one * (hovered ? hoverScale : 1);
                if (animate) hoverTween = motionRoot.DOScale(scale,hoverSeconds).SetEase(Ease.OutBack,1.25f).SetUpdate(true);
                else motionRoot.localScale = scale;
            }
            HoverChanged?.Invoke(hovered);
        }
        public void AdvanceAnimation(float seconds)
        {
            if (!CardZoneRowLayout.Finite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (hoverTween != null && hoverTween.IsActive()) hoverTween.Goto(hoverTween.Elapsed()+seconds,false);
        }
        public void OnPointerEnter(PointerEventData data) => SetHovered(true);
        public void OnPointerExit(PointerEventData data) => SetHovered(false);
        public void OnPointerClick(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Clicked?.Invoke(); }
        private void OnEnable() { if (motionRoot != null) motionRoot.localScale = Vector3.one; RefreshArt(); }
        private void OnDisable() { hoverTween?.Kill(); hoverTween = null; SetHovered(false,false); if (motionRoot != null) motionRoot.localScale = Vector3.one; }
        private void OnDestroy() { hoverTween?.Kill(); }
#if UNITY_EDITOR
        private void OnValidate() { if (IsConfigured) RefreshArt(); }
#endif
    }
}
