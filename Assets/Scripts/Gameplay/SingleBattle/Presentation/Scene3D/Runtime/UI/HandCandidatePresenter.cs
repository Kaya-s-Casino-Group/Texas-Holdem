using global::BattlePresentation;
using TMPro;
using UIFoundation;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Displays the shared immutable candidate model; recognition and legality stay at the bridge.</summary>
    public sealed class HandCandidatePresenter : MonoBehaviour
    {
        [SerializeField] private SingleBattleHost host;
        [SerializeField] private TMP_Text handName, details;
        [SerializeField] private RollingNumberText chips, multiplier;
        private InteractionSession subscribedInteraction;
        private CandidatePresentationModel cached;
        public CandidatePresentationModel Candidate { get; private set; }
        public RollingNumberText Chips => chips;
        public RollingNumberText Multiplier => multiplier;
        public string HandText => handName != null ? handName.text : "";
        public string DetailText => details != null ? details.text : "";

        public void Configure(SingleBattleHost owner, TMP_Text name, TMP_Text info, RollingNumberText points, RollingNumberText mult)
        {
            if (owner == null) throw new System.ArgumentNullException(nameof(owner));
            Unsubscribe(); host = owner; ConfigureViews(name, info, points, mult);
            if (isActiveAndEnabled) Subscribe();
            Refresh();
        }
        /// <summary>Allows this visual adapter to be bound without any scene host or rule object.</summary>
        public void ConfigureViews(TMP_Text name, TMP_Text info, RollingNumberText points, RollingNumberText mult)
        {
            if (name == null || info == null || points == null || mult == null)
                throw new System.ArgumentException("Bind the labels and numeric views.");
            handName = name; details = info; chips = points; multiplier = mult; cached = null;
            ShowEmpty(0);
        }
        public void Refresh()
        {
            if (host == null) return;
            BindInteraction();
            PresentCandidate(host.Candidate);
        }
        public void PresentCandidate(CandidatePresentationModel next)
        {
            if (handName == null || details == null || chips == null || multiplier == null) return;
            if (ReferenceEquals(cached, next) && next != null) return;
            cached = next; Candidate = next;
            if (next == null || next.State == CandidateState.Closed || next.State == CandidateState.Committing || next.State == CandidateState.Empty)
            { ShowEmpty(0); return; }
            if (next.State != CandidateState.Valid)
            {
                ShowEmpty(next.SelectedIds.Count);
                handName.text = "无法判定";
                if (!string.IsNullOrEmpty(next.Reason)) details.text += " · " + next.Reason;
                return;
            }
            handName.text = next.PatternName ?? next.PatternKey ?? "候选牌型";
            string scored = next.Labels.TryGetValue("scoredCount", out var count) ? count : "0";
            details.text = "选中 " + next.SelectedIds.Count + " · 计分 " + scored;
            chips.SetTarget(next.Points); multiplier.SetTarget(next.Multiplier);
        }
        private void ShowEmpty(int count)
        {
            handName.text = "未选牌"; details.text = "选中 " + count + " · 计分 0";
            chips.SetTarget(0); multiplier.SetTarget(0);
        }
        private void BindInteraction()
        {
            var next = host != null ? host.Interaction : null;
            if (ReferenceEquals(subscribedInteraction, next)) return;
            if (subscribedInteraction != null) subscribedInteraction.Changed -= PresentCandidate;
            subscribedInteraction = next;
            if (isActiveAndEnabled && subscribedInteraction != null) subscribedInteraction.Changed += PresentCandidate;
        }
        private void Subscribe() { if (host != null) host.StateChanged += Refresh; BindInteraction(); }
        private void Unsubscribe()
        {
            if (host != null) host.StateChanged -= Refresh;
            if (subscribedInteraction != null) subscribedInteraction.Changed -= PresentCandidate;
            subscribedInteraction = null;
        }
        private void OnEnable() { cached = null; Subscribe(); Refresh(); }
        private void OnDisable()
        {
            Unsubscribe(); cached = null;
            // Kill outstanding number interpolation; enabling reconciles the latest candidate again.
            if (chips != null) chips.SnapTo(0);
            if (multiplier != null) multiplier.SnapTo(0);
        }
        private void LateUpdate() => Refresh();
    }
}
