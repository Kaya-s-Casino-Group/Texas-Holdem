using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CardArt;
using DG.Tweening;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UIFoundation;

namespace CardPresentation
{
    /// <summary>Canvas composition of four inspection rows and remaining counts. No logical zones are created.</summary>
    [ExecuteAlways]
    public sealed class DeckOverviewView : MonoBehaviour, IPointerClickHandler
    {
        public static readonly IReadOnlyList<Suit> SuitOrder = Array.AsReadOnly(new[] { Suit.Spades, Suit.Hearts, Suit.Clubs, Suit.Diamonds });
        [SerializeField] private CardAtlasData atlas;
        [SerializeField] private Material cardMaterial;
        [SerializeField] private CardUIItemView cardPrefab;
        [SerializeField] private RectTransform[] rows;
        [SerializeField] private RectTransform suitRowsRoot;
        [SerializeField, Range(0,.3f)] private float verticalOverlap = .12f;
        [SerializeField] private TMP_Text[] statistics;
        [SerializeField] private TMP_Text title, other;
        [SerializeField] private Button closeButton;
        [Header("固定行宽内的卡牌排列（Canvas 单位）")]
        [SerializeField, Min(1)] private float cardHeight = 120;
        [SerializeField, Min(0)] private float arcHeight = 5;
        [SerializeField, Min(0)] private float rowPadding = 2;
        [SerializeField] private TMP_Text[] categoryTotals;
        [SerializeField] private TMP_Text[] rankTotals;
        [SerializeField] private CardArtProfile artProfile;
        public CardArtProfile ArtProfile => artProfile;
        public void BindArtProfile(CardArtProfile profile)
        {
            if (profile != null && atlas != null) profile.ValidateAtlas(atlas);
            artProfile = profile;
            foreach (var item in items.Values) if (item != null) item.Graphic.SetArtProfile(profile);
        }
        [SerializeField] private CardZoneMotionSettings motion = CardZoneMotionSettings.Default;
        [Header("牌组面板显隐（Canvas 单位）")]
        [SerializeField, Min(0)] private float entranceDistance = 90;
        [SerializeField, Min(.01f)] private float entranceSeconds = .22f;
        [SerializeField, Min(.01f)] private float exitSeconds = .14f;
        private RectTransform frame;
        private CanvasGroup presentationGroup;
        private Vector2 frameRest;
        private Sequence visibilityTween;
        private bool wantsVisible = true;
        public event Action Closed;
        public bool IsOpen => wantsVisible && isActiveAndEnabled;
        public RectTransform Frame => frame != null ? frame : (frame = (transform.Find("Frame") ?? transform.Find("Safe Area/Frame")) as RectTransform);
        public bool ContainsPointer(Vector2 screenPoint) => IsOpen && Frame != null &&
            RectTransformUtility.RectangleContainsScreenPoint(Frame,screenPoint,GetComponent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay ? null : GetComponent<Canvas>().worldCamera);
        private readonly Dictionary<string, CardUIItemView> items = new Dictionary<string, CardUIItemView>(StringComparer.Ordinal);
        private readonly Vector2[] presentedRowSizes = new Vector2[4];
        private const float ReferenceRowHeight = 132;
        public DeckOverviewData Data { get; private set; }
        public IReadOnlyList<CardUIItemView> Cards => Array.AsReadOnly(items.Values.ToArray());
        public CardAtlasData Atlas => atlas;
        public Material CardMaterial => cardMaterial;
        public Button CloseButton => closeButton;
        public float CardHeight => cardHeight;
        public float ArcHeight => arcHeight;
        public RectTransform SuitRowsRoot => suitRowsRoot;
        public float VerticalOverlap => verticalOverlap;

        public void ConfigureRowsRoot(RectTransform root)
        {
            if (root == null || rows == null || rows.Any(row => row == null || row.parent != root))
                throw new ArgumentException("All four suit rows must be direct children of their layout region.");
            suitRowsRoot = root;
            ApplyRowAnchors();
        }
        public void SetVerticalOverlap(float overlap)
        {
            if (!CardZoneRowLayout.Finite(overlap) || overlap < 0 || overlap > .3f) throw new ArgumentOutOfRangeException(nameof(overlap));
            verticalOverlap = overlap; ApplyRowAnchors(); RefreshLayout();
        }
        private void ApplyRowAnchors()
        {
            if (suitRowsRoot == null) return;
            float fraction = 1 / (4-3*verticalOverlap);
            for (int index = 0; index < 4; index++)
            {
                float top = 1-index*(1-verticalOverlap)*fraction;
                var row = rows[index]; var min = new Vector2(0,top-fraction); var max = new Vector2(1,top);
                if (row.anchorMin != min || row.anchorMax != max) { row.anchorMin = min; row.anchorMax = max; }
            }
        }

        public void ConfigureStatistics(TMP_Text[] aceFaceNumber, TMP_Text[] aceToTwo)
        {
            if (aceFaceNumber == null || aceFaceNumber.Length != 3 || aceFaceNumber.Any(label => label == null) ||
                aceToTwo == null || aceToTwo.Length != 13 || aceToTwo.Any(label => label == null))
                throw new ArgumentException("Bind three category counts and thirteen rank counts.");
            categoryTotals = aceFaceNumber; rankTotals = aceToTwo;
        }
        public void SetRowLayout(float height, float arc, float padding = 2)
        {
            if (!CardZoneRowLayout.Finite(height) || height <= 0 || !CardZoneRowLayout.Finite(arc) || arc < 0 ||
                !CardZoneRowLayout.Finite(padding) || padding < 0) throw new ArgumentOutOfRangeException();
            // Validate every row before changing settings or the current visual instances.
            for (int index = 0; index < rows.Length; index++)
            {
                float scale = RowScale(rows[index],height,padding);
                CalculateRow(rows[index], Data?.Row(SuitOrder[index]).Count ?? 0, height*scale, arc*scale, padding*scale);
            }
            cardHeight = height; arcHeight = arc; rowPadding = padding;
            if (Data != null) Present(Data);
        }

        public void Configure(CardAtlasData skin, Material sharedMaterial, CardUIItemView prefab, RectTransform[] suitRows,
            TMP_Text[] suitStatistics, TMP_Text heading, TMP_Text otherLabel, Button close)
        {
            if (skin == null || sharedMaterial == null || prefab == null || suitRows == null || suitRows.Length != 4 ||
                suitRows.Any(row => row == null) || suitStatistics == null || suitStatistics.Length != 4 ||
                suitStatistics.Any(text => text == null) || heading == null || otherLabel == null || close == null)
                throw new ArgumentException("Configure the atlas, shared UI material, card prefab and four suit rows.");
            if (items.Count != 0) throw new InvalidOperationException("Clear before changing overview resources.");
            atlas = skin; cardMaterial = sharedMaterial; cardPrefab = prefab; rows = suitRows; statistics = suitStatistics;
            title = heading; other = otherLabel; closeButton = close; WireClose();
        }
        public void Present(DeckOverviewData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (atlas == null || cardPrefab == null || rows == null || rows.Length != 4) throw new InvalidOperationException("Configure the overview.");
            if (artProfile != null) artProfile.ValidateAtlas(atlas);
            motion.Validate();
            foreach (var card in data.Cards.Where(card => card.Suit.HasValue)) CardUIGraphic.ValidateCard(atlas, card.Visual);
            ApplyRowAnchors();
            var rowScales = rows.Select(RowScale).ToArray();
            var rowPoses = rows.Select((row,index) => CalculateRow(row,data.Row(SuitOrder[index]).Count,
                cardHeight*rowScales[index],arcHeight*rowScales[index],rowPadding*rowScales[index])).ToArray();
            // Editor domain reload loses the instance map; discard only orphaned visual children, never match data.
            foreach (var item in rows.SelectMany(row => row.GetComponentsInChildren<CardUIItemView>(true)).ToArray())
                if (!items.Values.Contains(item)) Release(item.gameObject);
            var wanted = new HashSet<string>(data.Cards.Where(card => card.Suit.HasValue).Select(card => card.Visual.ViewId));
            foreach (var id in items.Keys.Where(id => !wanted.Contains(id) || items[id] == null).ToArray())
            { if (items[id] != null) Release(items[id].gameObject); items.Remove(id); }
            for (int rowIndex = 0; rowIndex < SuitOrder.Count; rowIndex++)
            {
                var row = rows[rowIndex]; var cards = data.Row(SuitOrder[rowIndex]); var count = data.Counts(SuitOrder[rowIndex]);
                statistics[rowIndex].text = CountText(statistics[rowIndex], count);
                float height = cardHeight * rowScales[rowIndex];
                float width = height * atlas.cardSize.x / Mathf.Max(1f, atlas.cardSize.y);
                for (int i = 0; i < cards.Count; i++)
                {
                    var entry = cards[i];
                    if (!items.TryGetValue(entry.Visual.ViewId, out var item))
                    {
                        item = Instantiate(cardPrefab, row, false); item.name = "Card " + entry.Visual.ViewId;
                        if (!Application.isPlaying) item.gameObject.hideFlags = HideFlags.DontSave;
                        item.Graphic.Bind(atlas, cardMaterial); items.Add(entry.Visual.ViewId, item);
                    }
                    item.transform.SetParent(row, false); item.SetSlotSibling(i);
                    var rect = (RectTransform)item.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                    var pose = rowPoses[rowIndex][i];
                    rect.sizeDelta = new Vector2(width, height);
                    rect.anchoredPosition = new Vector2(pose.Position.x,pose.Position.z);
                    // The hand's artwork top faces -Z. Map XZ to Canvas XY without turning it upside down.
                    var forward = pose.Rotation * Vector3.forward;
                    float roll = arcHeight > 0 && cards.Count > 1 ? Mathf.Atan2(forward.x,-forward.z) * Mathf.Rad2Deg : 0;
                    rect.localRotation = Quaternion.Euler(0,0,roll);
                    item.Graphic.SetArtProfile(artProfile != null ? artProfile : cardPrefab.Graphic.ArtProfile);
                    item.Present(entry.Visual, entry.IsRemaining, motion);
                }
                foreach (var card in cards) if (items[card.Visual.ViewId].Interaction.IsHovered) items[card.Visual.ViewId].transform.SetAsLastSibling();
            }
            title.text = "牌组总览     剩余 " + data.RemainingCount + " / " + data.Cards.Count;
            var suitCounts = SuitOrder.Select(data.Counts).ToArray();
            if (categoryTotals != null && categoryTotals.Length == 3)
            {
                categoryTotals[0].text = suitCounts.Sum(count => count.Aces).ToString(CultureInfo.InvariantCulture);
                categoryTotals[1].text = suitCounts.Sum(count => count.Faces).ToString(CultureInfo.InvariantCulture);
                categoryTotals[2].text = suitCounts.Sum(count => count.Numbers).ToString(CultureInfo.InvariantCulture);
            }
            if (rankTotals != null && rankTotals.Length == 13)
                for (int index = 0; index < 13; index++)
                    rankTotals[index].text = data.Cards.Count(card => card.IsRemaining && card.Rank.HasValue && (int)card.Rank.Value == 14-index)
                        .ToString(CultureInfo.InvariantCulture);
            other.text = data.OtherCount == 0 ? "" : "其他 / 不可见 " + data.OtherRemainingCount + " / " + data.OtherCount;
            Data = data;
            for (int index = 0; index < rows.Length; index++) presentedRowSizes[index] = rows[index].rect.size;
            WireClose();
        }
        private float RowScale(RectTransform row) => RowScale(row,cardHeight,rowPadding);
        private float RowScale(RectTransform row, float height, float padding)
        {
            // Height controls the whole row's proportions; narrow regions also fit one complete card.
            float aspect = atlas.cardSize.x / Mathf.Max(1f,atlas.cardSize.y);
            return Mathf.Min(row.rect.height/ReferenceRowHeight,row.rect.width/(height*aspect+2*padding));
        }
        public void RefreshLayout()
        {
            if (Data != null) Present(Data);
        }
        private void RefreshLayoutIfResized()
        {
            if (Data == null || rows == null || rows.Any(row => row == null || row.rect.width <= 0 || row.rect.height <= 0)) return;
            ApplyRowAnchors();
            // Across overlapping rows, raise the hovered row too; leave the other suit order stable.
            if (suitRowsRoot != null)
            {
                int sibling = 0;
                for (int pass = 0; pass < 2; pass++)
                foreach (var row in rows)
                {
                    bool hovered = items.Values.Any(item => item != null && item.Interaction.IsHovered && item.transform.parent == row);
                    if (hovered != (pass == 1)) continue;
                    if (row.GetSiblingIndex() != sibling) row.SetSiblingIndex(sibling);
                    sibling++;
                }
            }
            for (int index = 0; index < rows.Length; index++)
                if (presentedRowSizes[index] != rows[index].rect.size) { RefreshLayout(); return; }
        }
        public void AdvanceMotion(float delta) { foreach (var item in items.Values) item.AdvanceMotion(delta); }
        private CardZoneCardPose[] CalculateRow(RectTransform row, int count, float height, float arc, float padding)
        {
            float width = height * atlas.cardSize.x / Mathf.Max(1f,atlas.cardSize.y);
            return CardZoneRowLayout.CalculatePoses(new CardZoneLayoutSettings {
                Size = row.rect.size, Padding = Vector2.one * padding,
                PreferredGap = row.rect.width, ArcHeight = arc, Alignment = CardZoneAlignment.Center
            }, new Vector3(width,0,height),count);
        }
        private static string CountText(TMP_Text label, DeckSuitCount count)
        {
            var values = new[] { count.Numbers, count.Faces, count.Aces, count.Total };
            return string.Concat(values.Select((value,index) => {
                string text = value.ToString(CultureInfo.InvariantCulture);
                float x = 8 + (index + .5f) * ((label.rectTransform.rect.width - 16) / 4) - label.GetPreferredValues(text).x * .5f;
                return "<pos=" + x.ToString("0.##",CultureInfo.InvariantCulture) + ">" + text;
            }));
        }
        public void Clear() { foreach (var item in items.Values) if (item != null) Release(item.gameObject); items.Clear(); Data = null; }
        private void PreparePresentation()
        {
            if (presentationGroup != null) return;
            frame = Frame; if (frame != null) frameRest = frame.anchoredPosition;
            presentationGroup = GetComponent<CanvasGroup>();
            if (presentationGroup == null) presentationGroup = gameObject.AddComponent<CanvasGroup>();
        }
        public void Show(bool animate = true)
        {
            PreparePresentation(); bool wasOpen = IsOpen; bool wasActive = gameObject.activeSelf;
            wantsVisible = true; gameObject.SetActive(true); BringToFront();
            if (wasOpen) return;
            visibilityTween?.Kill(); visibilityTween = null;
            presentationGroup.blocksRaycasts = presentationGroup.interactable = true;
            if (!animate || !Application.isPlaying) { if (frame != null) frame.anchoredPosition = frameRest; presentationGroup.alpha = 1; return; }
            if (!wasActive || presentationGroup.alpha <= .001f)
            { if (frame != null) frame.anchoredPosition = frameRest-Vector2.up*entranceDistance; presentationGroup.alpha = 0; }
            visibilityTween = DOTween.Sequence().SetUpdate(true);
            if (frame != null) visibilityTween.Join(DOTween.To(() => frame.anchoredPosition,value => frame.anchoredPosition = value,frameRest,entranceSeconds).SetEase(Ease.OutBack,1.2f));
            visibilityTween.Join(DOTween.To(() => presentationGroup.alpha,value => presentationGroup.alpha = value,1,entranceSeconds*.6f));
        }
        public void Close() => Close(true);
        public void Close(bool animate)
        {
            if (animate && !wantsVisible) return;
            PreparePresentation(); bool wasOpen = IsOpen; wantsVisible = false;
            visibilityTween?.Kill(); visibilityTween = null;
            // Keep the full-screen input shield until the closing animation has finished.
            presentationGroup.interactable = false;
            ClearInspection();
            if (wasOpen) Closed?.Invoke();
            if (!animate || !Application.isPlaying || !gameObject.activeSelf)
            { presentationGroup.alpha = 0; if (frame != null) frame.anchoredPosition = frameRest; gameObject.SetActive(false); return; }
            visibilityTween = DOTween.Sequence().SetUpdate(true);
            if (frame != null) visibilityTween.Join(DOTween.To(() => frame.anchoredPosition,value => frame.anchoredPosition = value,frameRest-Vector2.up*entranceDistance,exitSeconds).SetEase(Ease.InBack,1.2f));
            visibilityTween.Join(DOTween.To(() => presentationGroup.alpha,value => presentationGroup.alpha = value,0,exitSeconds)).OnComplete(() => gameObject.SetActive(false));
        }
        public void AdvanceAnimation(float seconds)
        {
            if (!CardZoneRowLayout.Finite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (visibilityTween != null && visibilityTween.IsActive()) visibilityTween.Goto(visibilityTween.Elapsed()+seconds,false);
        }
        private void WireClose() { if (closeButton != null) { closeButton.onClick.RemoveListener(Close); closeButton.onClick.AddListener(Close); } }
        public void ClearInspection()
        {
            foreach (var item in items.Values) if (item != null) item.SetHovered(false);
        }
        public void OnPointerClick(PointerEventData eventData) => ClearInspection();
        public void BringToFront()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) return;
            // Overlay HUDs always draw after camera canvases, regardless of their sorting order.
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            int layer = canvas.sortingLayerID, order = 500;
            foreach (var other in FindObjectsOfType<Canvas>())
            {
                if (other == canvas || other.transform.IsChildOf(transform) || !other.isRootCanvas ||
                    other.renderMode == RenderMode.WorldSpace || other.targetDisplay != canvas.targetDisplay) continue;
                if (SortingLayer.GetLayerValueFromID(other.sortingLayerID) > SortingLayer.GetLayerValueFromID(layer))
                    layer = other.sortingLayerID;
                order = Mathf.Max(order, other.sortingOrder + 1);
            }
            canvas.sortingLayerID = layer; canvas.sortingOrder = Mathf.Min(order, 32766);
            transform.SetAsLastSibling();
        }
        private void OnEnable()
        {
            WireClose();
            if (Application.isPlaying)
            {
                BringToFront();
                ScreenSafeArea.Ensure(GetComponent<Canvas>(), Frame, transform.Find("Backdrop"));
                CardUIInput.EnsureEventSystem();
            }
            Canvas.willRenderCanvases += RefreshLayoutIfResized;
        }
        private void OnDisable()
        {
            visibilityTween?.Kill(); visibilityTween = null;
            if (frame != null && presentationGroup != null) frame.anchoredPosition = frameRest;
            Canvas.willRenderCanvases -= RefreshLayoutIfResized;
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        }
        private void Update() { if (Application.isPlaying) AdvanceMotion(Time.unscaledDeltaTime); }
        private void OnDestroy() { visibilityTween?.Kill(); Clear(); }
        private static void Release(GameObject item) { item.SetActive(false); if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
    }
}
