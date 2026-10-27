using BattlePresentation;
using PokerEngine;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.BattlePresentation
{
    /// <summary>Scene buttons submit intents through the existing battle bridge.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleHandActionPanel : MonoBehaviour
    {
        [SerializeField] private BattleTimelineSceneAdapter adapter;
        [SerializeField] private Button draw, skipDraw, play, discard, endTurn;
        [SerializeField] private Text status, drawLabel;
        private bool bound;
        public void Configure(BattleTimelineSceneAdapter source, Button drawButton, Button skipButton,
            Button playButton, Button discardButton, Button endButton, Text statusLabel, Text drawText)
        {
            Unbind(); adapter = source; draw = drawButton; skipDraw = skipButton; play = playButton;
            discard = discardButton; endTurn = endButton; status = statusLabel; drawLabel = drawText;
            if (isActiveAndEnabled) Bind(); Refresh();
        }
        private bool Ready => isActiveAndEnabled && adapter != null && adapter.CanAct;
        private bool Drawing => Ready && adapter.Host.AuthorityState.FlowFor(adapter.ObserverId).Window == BattlePlayerWindow.Draw;
        private int DrawCount => Drawing ? adapter.Host.Engine.DrawLimit(adapter.Host.AuthorityState, adapter.ObserverId) : 0;
        public void Draw()
        {
            if (Drawing && DrawCount > 0 && adapter.Host.AuthorityState.FlowFor(adapter.ObserverId).DrawUsesRemaining > 0)
                adapter.SubmitAction(CardDuelAction.Draw, drawCount: DrawCount);
            Refresh();
        }
        public void SkipDraw() { if (Drawing) adapter.SubmitAction(CardDuelAction.SkipDraw); Refresh(); }
        public void Play() { if (Ready && adapter.Candidate?.CanPlay == true) adapter.PlaySelected(); Refresh(); }
        public void Discard() { if (Ready && adapter.Candidate?.CanDiscard == true) adapter.DiscardSelected(); Refresh(); }
        public void EndTurn()
        {
            if (Ready && adapter.Host.AuthorityState.FlowFor(adapter.ObserverId).Window == BattlePlayerWindow.Action) adapter.EndTurn();
            Refresh();
        }
        public void Refresh()
        {
            bool drawing = Drawing;
            var candidate = adapter?.Candidate;
            if (draw != null) draw.interactable = drawing && DrawCount > 0 && adapter.Host.AuthorityState.FlowFor(adapter.ObserverId).DrawUsesRemaining > 0;
            if (skipDraw != null) skipDraw.interactable = drawing;
            if (play != null) play.interactable = Ready && candidate?.CanPlay == true;
            if (discard != null) discard.interactable = Ready && candidate?.CanDiscard == true;
            if (endTurn != null) endTurn.interactable = Ready && adapter.Host.AuthorityState.FlowFor(adapter.ObserverId).Window == BattlePlayerWindow.Action;
            if (drawLabel != null) drawLabel.text = drawing ? "Draw (" + DrawCount + ")" : "Draw";
            if (status != null) status.text = adapter == null || adapter.Host?.Session == null ? "Waiting for battle" :
                adapter.IsPresenting ? "Playing..." : drawing ? "Draw cards to begin" :
                candidate?.SelectedIds.Count > 0 ? candidate.SelectedIds.Count + " selected  |  " +
                    (candidate.PatternKey ?? "Invalid hand") + "  |  " + candidate.Total : "Click cards to select";
        }
        private void Bind()
        {
            if (bound) return; bound = true;
            draw?.onClick.AddListener(Draw); skipDraw?.onClick.AddListener(SkipDraw);
            play?.onClick.AddListener(Play); discard?.onClick.AddListener(Discard); endTurn?.onClick.AddListener(EndTurn);
        }
        private void Unbind()
        {
            if (!bound) return; bound = false;
            draw?.onClick.RemoveListener(Draw); skipDraw?.onClick.RemoveListener(SkipDraw);
            play?.onClick.RemoveListener(Play); discard?.onClick.RemoveListener(Discard); endTurn?.onClick.RemoveListener(EndTurn);
        }
        private void OnEnable() { Bind(); Refresh(); }
        private void LateUpdate() => Refresh();
        private void OnDisable() { Unbind(); Refresh(); }
    }
}
