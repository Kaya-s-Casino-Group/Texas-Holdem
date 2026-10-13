using System;
using System.Globalization;
using System.Linq;
using DG.Tweening;
using PokerEngine;
using TMPro;
using UnityEngine;

namespace CardPresentation
{
    /// <summary>Read-only suit/rank count table for deck-icon hover. Never captures input.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasGroup))]
    public sealed class DeckSummaryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text[] rankTotals, suitTotals, cells;
        [SerializeField] private TMP_Text other;
        [SerializeField] private Color zeroColor = new Color(1,1,1,.2f);
        [SerializeField, Min(.01f)] private float showSeconds = .18f, hideSeconds = .12f;
        [SerializeField, Min(0)] private float travel = 28;
        private CanvasGroup group;
        private Sequence tween;
        private Vector2 rest;
        private bool initialized;
        public bool IsOpen { get; private set; }
        public DeckOverviewData Data { get; private set; }
        public bool IsConfigured => rankTotals?.Length == 13 && suitTotals?.Length == 4 && cells?.Length == 52 &&
            rankTotals.Concat(suitTotals).Concat(cells).All(label => label != null) && other != null;
        public TMP_Text Cell(int suitIndex, int rankIndex) => cells[suitIndex*13+rankIndex];
        public TMP_Text RankTotal(int rankIndex) => rankTotals[rankIndex];
        public TMP_Text SuitTotal(int suitIndex) => suitTotals[suitIndex];
        public TMP_Text Other => other;
        public void Configure(TMP_Text[] ranks, TMP_Text[] suits, TMP_Text[] grid, TMP_Text unknown)
        {
            if (ranks == null || ranks.Length != 13 || suits == null || suits.Length != 4 || grid == null || grid.Length != 52 ||
                ranks.Concat(suits).Concat(grid).Any(label => label == null) || unknown == null) throw new ArgumentException("Bind thirteen ranks and four suit rows.");
            rankTotals = (TMP_Text[])ranks.Clone(); suitTotals = (TMP_Text[])suits.Clone(); cells = (TMP_Text[])grid.Clone(); other = unknown;
        }
        public void Present(DeckOverviewData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!IsConfigured) throw new InvalidOperationException("Configure the summary table.");
            if (ReferenceEquals(Data,data)) return;
            for (int rank = 0; rank < 13; rank++) SetCount(rankTotals[rank],data.Remaining((Rank)(14-rank)));
            for (int suit = 0; suit < 4; suit++)
            {
                var value = DeckOverviewView.SuitOrder[suit]; SetCount(suitTotals[suit],data.Counts(value).Total);
                for (int rank = 0; rank < 13; rank++) SetCount(cells[suit*13+rank],data.Remaining(value,(Rank)(14-rank)));
            }
            other.text = data.OtherRemainingCount == 0 ? "" : "其他 / 不可见\n" + data.OtherRemainingCount.ToString(CultureInfo.InvariantCulture);
            Data = data;
        }
        private void SetCount(TMP_Text label, int count)
        { label.text = count.ToString(CultureInfo.InvariantCulture); label.color = count == 0 ? zeroColor : Color.white; }
        private void Initialize()
        {
            if (initialized) return;
            group = GetComponent<CanvasGroup>(); rest = ((RectTransform)transform).anchoredPosition; initialized = true;
            group.blocksRaycasts = group.interactable = false;
        }
        public void Show(bool animate = true)
        {
            Initialize(); if (IsOpen) return;
            bool active = gameObject.activeSelf; IsOpen = true; tween?.Kill(); gameObject.SetActive(true);
            var rect = (RectTransform)transform;
            if (!animate || !Application.isPlaying) { rect.anchoredPosition = rest; rect.localScale = Vector3.one; group.alpha = 1; return; }
            if (!active) { rect.anchoredPosition = rest-Vector2.up*travel; rect.localScale = Vector3.one*.94f; group.alpha = 0; }
            tween = DOTween.Sequence().SetUpdate(true);
            tween.Join(DOTween.To(() => rect.anchoredPosition,value => rect.anchoredPosition = value,rest,showSeconds).SetEase(Ease.OutBack,1.2f));
            tween.Join(rect.DOScale(1,showSeconds).SetEase(Ease.OutBack,1.2f));
            tween.Join(DOTween.To(() => group.alpha,value => group.alpha = value,1,showSeconds*.6f));
        }
        public void Hide(bool animate = true)
        {
            Initialize(); if (!IsOpen && animate) return;
            IsOpen = false; tween?.Kill(); tween = null;
            if (!animate || !Application.isPlaying || !gameObject.activeSelf) { group.alpha = 0; gameObject.SetActive(false); return; }
            var rect = (RectTransform)transform; tween = DOTween.Sequence().SetUpdate(true);
            tween.Join(rect.DOScale(.94f,hideSeconds).SetEase(Ease.InBack,1.1f));
            tween.Join(DOTween.To(() => rect.anchoredPosition,value => rect.anchoredPosition = value,rest-Vector2.up*travel,hideSeconds));
            tween.Join(DOTween.To(() => group.alpha,value => group.alpha = value,0,hideSeconds)).OnComplete(() => gameObject.SetActive(false));
        }
        public void AdvanceAnimation(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (tween != null && tween.IsActive()) tween.Goto(tween.Elapsed()+seconds,false);
        }
        private void OnDisable() { tween?.Kill(); tween = null; IsOpen = false; }
        private void OnDestroy() { tween?.Kill(); }
    }
}
