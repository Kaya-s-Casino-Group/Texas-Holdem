using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UIFoundation;

namespace CardPresentation
{
    /// <summary>Player hand commands. Card ownership and automatic draws belong to the gameplay host.</summary>
    [DisallowMultipleComponent]
    public sealed class HandActionPanelView : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("drawButton")] private Button playButton;
        [SerializeField] private Button discardButton, rankSortButton, suitSortButton;
        [SerializeField, FormerlySerializedAs("drawText")] private TMP_Text playText;
        [SerializeField] private TMP_Text discardText, rankText, suitText;
        private bool listening, suitFirst;
        private string rankCaption = "点数理牌", suitCaption = "花色理牌";
        private Color sortNormalColor = Color.white, sortSelectedColor = new Color32(255, 208, 105, 255);
        public Button PlayButton => playButton;
        public Button DiscardButton => discardButton;
        public Button RankSortButton => rankSortButton;
        public Button SuitSortButton => suitSortButton;
        public bool IsConfigured => playButton != null && discardButton != null && rankSortButton != null && suitSortButton != null &&
            playText != null && discardText != null && rankText != null && suitText != null;
        public event Action PlayRequested, DiscardRequested, RankSortRequested, SuitSortRequested;

        public void Configure(Button play, Button discard, Button rank, Button suit, TMP_Text playLabel,
            TMP_Text discardLabel, TMP_Text rankLabel, TMP_Text suitLabel)
        {
            if (play == null || discard == null || rank == null || suit == null || playLabel == null || discardLabel == null ||
                rankLabel == null || suitLabel == null) throw new ArgumentException("Four buttons and their labels are required.");
            Unlisten();
            playButton = play; discardButton = discard; rankSortButton = rank; suitSortButton = suit;
            playText = playLabel; discardText = discardLabel; rankText = rankLabel; suitText = suitLabel;
            RefreshSortPresentation();
            if (isActiveAndEnabled) Listen();
        }

        /// <summary>Skins configure the captions and palette; this view remains their sole stateful writer.</summary>
        public void SetSortPresentation(string rank, string suit, Color normal, Color selected)
        {
            rankCaption = rank; suitCaption = suit; sortNormalColor = normal; sortSelectedColor = selected;
            RefreshSortPresentation();
        }

        public void SetState(bool canPlay, bool canDiscard, bool canSort, bool suitFirst)
        {
            this.suitFirst = suitFirst;
            if (!IsConfigured) return;
            playButton.interactable = canPlay; discardButton.interactable = canDiscard;
            rankSortButton.interactable = suitSortButton.interactable = canSort;
            SetText(playText, "出牌");
            SetText(discardText, "弃牌");
            RefreshSortPresentation();
        }
        private void RefreshSortPresentation()
        {
            if (rankText == null || suitText == null) return;
            SetText(rankText, rankCaption); SetText(suitText, suitCaption);
            SetColor(rankText, suitFirst ? sortNormalColor : sortSelectedColor);
            SetColor(suitText, suitFirst ? sortSelectedColor : sortNormalColor);
        }
        private static void SetColor(TMP_Text label, Color value) { if (label.color != value) label.color = value; }
        private static void SetText(TMP_Text label, string value) { if (label.text != value) label.text = value; }
        private void Play() => PlayRequested?.Invoke();
        private void Discard() => DiscardRequested?.Invoke();
        private void Rank() => RankSortRequested?.Invoke();
        private void Suit() => SuitSortRequested?.Invoke();
        private void Listen()
        {
            if (listening || !IsConfigured) return;
            playButton.onClick.AddListener(Play); discardButton.onClick.AddListener(Discard);
            rankSortButton.onClick.AddListener(Rank); suitSortButton.onClick.AddListener(Suit); listening = true;
        }
        private void Unlisten()
        {
            if (!listening) return;
            playButton.onClick.RemoveListener(Play); discardButton.onClick.RemoveListener(Discard);
            rankSortButton.onClick.RemoveListener(Rank); suitSortButton.onClick.RemoveListener(Suit); listening = false;
        }
        private void OnEnable()
        {
            Listen();
            if (Application.isPlaying) { ScreenSafeArea.Ensure(GetComponent<Canvas>()); CardUIInput.EnsureEventSystem(); }
        }
        private void OnDisable() => Unlisten();
    }
}
