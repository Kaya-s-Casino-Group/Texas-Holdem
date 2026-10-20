using System;
using CardArt;
using CardPresentation;
using UnityEngine;

namespace CardInteraction
{
    /// <summary>Projects the runtime deck and owns only local preview/pin intent. Drawing remains a gameplay command.</summary>
    [DisallowMultipleComponent]
    public sealed class DrawPilePresenter : MonoBehaviour
    {
        [SerializeField] private CardZonePlayHost source;
        [SerializeField] private DrawPileView view;
        [SerializeField] private DeckOverviewPresenter overview;
        [SerializeField] private DeckSummaryView summaryPrefab;
        private DeckSummaryView summary;
        private bool changingVisibility;
        private bool pinned, dismissed;
        private DrawPileView subscribedView;
        private DeckOverviewView subscribedOverview;
        public CardZonePlayHost Source => source;
        public DrawPileView View => view;
        public DeckOverviewPresenter Overview => overview;
        public bool IsPinned => pinned;
        public DeckSummaryView Summary => summary;
        public DeckSummaryView SummaryPrefab => summaryPrefab;
        public void BindSummary(DeckSummaryView prefab)
        {
            if (prefab == null || !prefab.IsConfigured) throw new ArgumentException("Bind the deck summary prefab.",nameof(prefab));
            if (summaryPrefab != prefab) ReleaseSummary();
            summaryPrefab = prefab;
        }

        public void Configure(CardZonePlayHost host, DrawPileView icon)
        {
            if (host == null || host.GetComponent<HandZoneView>() == null) throw new ArgumentException("Select a hand deck host.",nameof(host));
            if (icon == null || !icon.IsConfigured) throw new ArgumentException("Configure the draw pile icon first.",nameof(icon));
            Unsubscribe(); source = host; view = icon; Subscribe(); Refresh();
        }
        public void BindOverview(DeckOverviewPresenter target)
        {
            if (target == null || source == null || target.Match != source.Match) throw new ArgumentException("Use this hand's match overview.");
            Unsubscribe(); overview = target; Subscribe();
        }
        private void Subscribe()
        {
            if (!isActiveAndEnabled) return;
            // A host can configure us before OnEnable; subscribe once regardless of scene order.
            Unsubscribe();
            subscribedView = view;
            if (subscribedView != null) { subscribedView.HoverChanged += HoverChanged; subscribedView.Clicked += TogglePinned; }
            subscribedOverview = overview != null ? overview.View : null;
            if (subscribedOverview != null) subscribedOverview.Closed += OverviewClosed;
        }
        private void Unsubscribe()
        {
            if (subscribedView != null) { subscribedView.HoverChanged -= HoverChanged; subscribedView.Clicked -= TogglePinned; }
            if (subscribedOverview != null) subscribedOverview.Closed -= OverviewClosed;
            subscribedView = null; subscribedOverview = null;
        }
        private void HoverChanged(bool hovered) { if (!isActiveAndEnabled) return; if (!hovered) dismissed = false; UpdateVisibility(); }
        public void SetPinned(bool value)
        { pinned = value; if (value) dismissed = false; UpdateVisibility(); }
        public void TogglePinned() { if (isActiveAndEnabled) SetPinned(!pinned); }
        private void OverviewClosed()
        {
            if (changingVisibility) return;
            pinned = false; dismissed = view != null && view.IsHovered; if (summary != null) summary.Hide();
        }
        private void UpdateVisibility()
        {
            if (overview == null || overview.View == null) return;
            if (pinned)
            {
                if (summary != null) summary.Hide();
                if (!overview.View.IsOpen) overview.Open();
                return;
            }
            if (overview.View.IsOpen)
            {
                changingVisibility = true;
                try { overview.Close(); } finally { changingVisibility = false; }
            }
            if (!dismissed && view != null && view.IsHovered && summaryPrefab != null)
            {
                if (summary == null)
                {
                    var canvas = view.GetComponentInParent<Canvas>().rootCanvas;
                    summary = Instantiate(summaryPrefab,canvas.transform,false); summary.name = "Deck Hover Summary (Runtime)";
                    summary.gameObject.hideFlags = HideFlags.DontSave;
                    var rect = (RectTransform)summary.transform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
                    rect.anchoredPosition = Vector2.zero;
                    summary.Hide(false);
                }
                var data = overview.ReadData(); if (data != null) { summary.Present(data); summary.Show(); }
            }
            else if (summary != null) summary.Hide();
        }
        public void Refresh()
        {
            if (view == null) return;
            var skin = source?.GetComponent<HandZoneView>()?.CardPrefab?.GetComponentInChildren<CardView>(true)?.Atlas;
            if (skin != null && skin != view.Atlas) view.BindAtlas(skin);
            int count = source == null || !source.isActiveAndEnabled ? 0 : source.IsRunning ? source.DeckCards.Count :
                !Application.isPlaying && source.HasSnapshot ? source.InitialDeckCards.Count : 0;
            view.SetRemainingCards(count);
            if (summary != null && summary.IsOpen && overview != null)
            { var data = overview.ReadData(); if (data != null) summary.Present(data); }
        }
        private void OnEnable() { Subscribe(); Refresh(); }
        private void Start() { if (Application.isPlaying && overview != null) { pinned = false; overview.Close(false); } }
        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            Refresh();
        }
        private void OnDisable()
        {
            Unsubscribe(); pinned = dismissed = false;
            if (summary != null) summary.Hide(false);
            if (Application.isPlaying && overview != null) overview.Close(false);
        }
        private void ReleaseSummary()
        {
            if (summary == null) return;
            summary.Hide(false);
            if (Application.isPlaying) Destroy(summary.gameObject); else DestroyImmediate(summary.gameObject);
            summary = null;
        }
        private void OnDestroy() { Unsubscribe(); ReleaseSummary(); }
    }
}
