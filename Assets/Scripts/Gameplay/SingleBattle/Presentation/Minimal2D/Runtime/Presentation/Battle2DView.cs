using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CardArt;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.Battle2DValidation
{
    /// <summary>Minimal card and rectangle movement using the same presentation port as the 3D battle.</summary>
    public sealed class Battle2DView : MonoBehaviour, IBattleSessionPresentation
    {
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Transform cardRoot;
        [SerializeField] private Transform playerActor, enemyActor;
        private Vector3 playerHome, enemyHome;
        private Coroutine playback;
        private CardDuelState settledState;
        [SerializeField] private TMP_Text status, details, log;
        [SerializeField] private Button play, discard, endTurn, restart;
        private Battle2DHost host;
        private readonly List<CardInstanceId> selected = new List<CardInstanceId>();
        private readonly List<Battle2DCard> cards = new List<Battle2DCard>();
        private readonly Queue<string> history = new Queue<string>();
        private static readonly Dictionary<CardDefinitionId, CardDefinition> Faces = StandardDeck.Cards().ToDictionary(card => card.Id);
        public bool IsPresenting { get; private set; }
        public Transform PlayerActor => playerActor;
        public Transform EnemyActor => enemyActor;
        public CardDuelState PresentedState { get; private set; }
        public IReadOnlyList<CardInstanceId> Selected => selected.AsReadOnly();
        public IReadOnlyList<Battle2DCard> Cards => cards.AsReadOnly();
        public Button PlayButton => play;
        public Button DiscardButton => discard;
        public Button EndTurnButton => endTurn;
        public Button RestartButton => restart;
        public string LogText => log != null ? log.text : "";

        public void Configure(GameObject prefab, Transform root, TMP_Text statusText, TMP_Text detailText, TMP_Text logText,
            Button playButton, Button discardButton, Button endTurnButton, Button restartButton)
        {
            Unsubscribe();
            cardPrefab = prefab; cardRoot = root; status = statusText; details = detailText; log = logText;
            play = playButton; discard = discardButton; endTurn = endTurnButton; restart = restartButton;
            if (cardPrefab == null || cardRoot == null || status == null || details == null || log == null ||
                play == null || discard == null || endTurn == null || restart == null)
                throw new ArgumentException("Configure the card artwork and minimal 2D controls.");
            if (isActiveAndEnabled) Subscribe();
        }
        public void ConfigureActors(Transform player, Transform enemy)
        {
            playerActor = player; enemyActor = enemy; RememberActorHomes();
        }
        private void Awake() => RememberActorHomes();
        private void RememberActorHomes()
        {
            if (playerActor != null) playerHome = playerActor.localPosition;
            if (enemyActor != null) enemyHome = enemyActor.localPosition;
        }
        private void ResetActors()
        {
            if (playerActor != null) playerActor.localPosition = playerHome;
            if (enemyActor != null) enemyActor.localPosition = enemyHome;
        }
        public void BindCommands(Battle2DHost owner) { host = owner; RefreshButtons(); }
        public void Initialize(CardDuelState initialState)
        {
            StopPresentation(); ClearCards(); history.Clear(); selected.Clear();
            PresentedState = initialState ?? throw new ArgumentNullException(nameof(initialState));
            Render();
        }
        public void Present(BattleSessionResult batch, bool animate)
        {
            if (batch == null || !batch.Accepted) throw new ArgumentException("Present an accepted session batch.", nameof(batch));
            if (animate && Application.isPlaying && isActiveAndEnabled)
            {
                settledState = batch.State; IsPresenting = true; RefreshButtons();
                playback = StartCoroutine(ShowSteps(batch));
                return;
            }
            foreach (var step in batch.Steps) LogStep(step);
            Synchronize(batch.State);
        }
        private IEnumerator ShowSteps(BattleSessionResult batch)
        {
            try
            {
                foreach (var step in batch.Steps)
                {
                    PresentedState = step.Before; Render();
                    var moving = cards.Where(card => step.Selected.Contains(card.Id)).Select(card => card.transform).ToArray();
                    var starts = moving.Select(card => card.localPosition).ToArray();
                    var actor = step.Action == CardDuelAction.Play ? playerActor : step.Action == CardDuelAction.EnemyAction ? enemyActor : null;
                    Vector3 actorStart = actor != null ? actor.localPosition : Vector3.zero;
                    if (moving.Length > 0 || actor != null)
                    {
                        float elapsed = 0;
                        while (elapsed < .18f)
                        {
                            elapsed += Time.deltaTime;
                            float t = Mathf.Clamp01(elapsed / .18f);
                            for (int index = 0; index < moving.Length; index++)
                                moving[index].localPosition = starts[index] + Vector3.up * (step.Action == CardDuelAction.Discard ? -.7f : .7f) * t;
                            if (actor != null) actor.localPosition = actorStart + Vector3.right *
                                (actor == playerActor ? .6f : -.6f) * (1 - Mathf.Abs(2 * t - 1));
                            yield return null;
                        }
                        if (actor != null) actor.localPosition = actorStart;
                    }
                    LogStep(step); selected.Clear(); PresentedState = step.Transition.State; Render();
                }
            }
            finally
            {
                // Coroutine errors and cancellation must release input without settling any rules again.
                playback = null; settledState = null; IsPresenting = false; ResetActors();
                selected.Clear(); PresentedState = batch.State; Render();
            }
        }
        private void LogStep(BattleSessionStep step)
        {
            var transition = step.Transition;
            switch (step.Action)
            {
                case CardDuelAction.Start: AddLog("开局 · 发牌完成"); break;
                case CardDuelAction.Play:
                    AddLog("出牌 " + step.Selected.Count + " 张 · " + transition.Score.Total + " 分 · 造成 " + transition.Events.Sum(e => e.HealthLost) + " 伤害"); break;
                case CardDuelAction.Discard:
                    AddLog("弃牌 " + step.Selected.Count + " 张" + (transition.Events.Count > 0 ? " · 格挡/治疗收益已结算" : "")); break;
                case CardDuelAction.EndTurn: AddLog("玩家结束回合"); break;
                case CardDuelAction.EnemyAction: AddLog("敌人行动 · 造成 " + transition.Events.Sum(e => e.HealthLost) + " 伤害"); break;
            }
        }
        public void Synchronize(CardDuelState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            StopPresentation(); PresentedState = state; selected.Clear(); Render();
        }
        private void StopPresentation()
        {
            if (playback != null) StopCoroutine(playback);
            playback = null; settledState = null; IsPresenting = false; ResetActors();
        }
        public void ToggleSelection(CardInstanceId id)
        {
            var state = PresentedState;
            if (IsPresenting || state == null || !state.HasStarted || !state.Duel.CanAct(state.Duel.PlayerId) ||
                !state.Zone(CardZoneKinds.Hand, state.Duel.PlayerId).Cards.Contains(id) || state.Table.GetCard(id).IsDisabled) return;
            if (!selected.Remove(id))
            {
                if (selected.Count >= state.Duel.Player(state.Duel.PlayerId).Rules.MaxSelection) return;
                selected.Add(id);
            }
            foreach (var card in cards) card.ShowSelection(selected.Contains(card.Id), state.Table.GetCard(card.Id).IsDisabled);
            RefreshButtons();
        }
        public void ShowRejected(CardDuelError error) { AddLog("操作未执行：" + error); Render(); }
        private void AddLog(string value) { history.Enqueue(value); while (history.Count > 5) history.Dequeue(); }
        private void Render()
        {
            if (PresentedState == null || cardRoot == null) return;
            var state = PresentedState; var duel = state.Duel; var player = duel.Player(duel.PlayerId);
            var unit = duel.Combat.Get(player.Id); var hand = state.Zone(CardZoneKinds.Hand, player.Id);
            foreach (var old in cards.Where(card => !hand.Cards.Contains(card.Id)).ToArray()) { cards.Remove(old); Remove(old.gameObject); }
            foreach (var id in hand.Cards)
            {
                if (cards.Any(card => card.Id == id)) continue;
                var instance = Instantiate(cardPrefab, cardRoot); instance.name = "Card " + id;
                var card = instance.AddComponent<Battle2DCard>(); card.Bind(this, id, ArtId(state.Table.GetCard(id)));
                cards.Add(card);
            }
            cards.Sort((a, b) => hand.Cards.ToList().IndexOf(a.Id).CompareTo(hand.Cards.ToList().IndexOf(b.Id)));
            int columns = Math.Min(8, cards.Count), rows = Math.Max(1, (cards.Count + 7) / 8);
            float height = Math.Min(1.8f, 4.3f / rows);
            var collider = cardPrefab.GetComponent<BoxCollider>();
            float scale = height / collider.size.z;
            float width = collider.size.x * scale, spacing = width + .16f;
            for (int index = 0; index < cards.Count; index++)
            {
                int row = index / 8, column = index % 8;
                cards[index].Place(new Vector3((column - (columns - 1) * .5f) * spacing,
                    -.15f + ((rows - 1) * .5f - row) * (height + .18f), 0), scale);
                cards[index].ShowSelection(selected.Contains(cards[index].Id), state.Table.GetCard(cards[index].Id).IsDisabled);
            }
            string outcome = duel.Phase == ScoredDuelPhase.Won ? "战斗胜利" : duel.Phase == ScoredDuelPhase.Lost ? "战斗失败" : duel.Phase == ScoredDuelPhase.AwaitingEnemyAction ? "敌人行动" : "玩家行动";
            status.text = outcome + "     回合 " + (duel.IsFinished ? duel.CompletedRounds : duel.CompletedRounds + 1);
            status.color = duel.Phase == ScoredDuelPhase.Won ? new Color(.4f, 1, .6f) : duel.Phase == ScoredDuelPhase.Lost ? new Color(1, .45f, .4f) : Color.white;
            details.text = "玩家 HP " + unit.Health + "/" + unit.MaxHealth + "   格挡 " + unit.Block + "   能量 " + player.Energy.Energy + "/" + player.Energy.MaxEnergy +
                "\n" + string.Join("    ", duel.Enemies.Select((enemy, index) => "敌人 " + (index + 1) + " HP " + duel.Combat.Get(enemy.Id).Health + "/" + duel.Combat.Get(enemy.Id).MaxHealth)) +
                "\n手牌 " + hand.Cards.Count + "   牌库 " + state.Zone(CardZoneKinds.Deck, player.Id).Cards.Count + "   弃牌堆 " + state.Zone(CardZoneKinds.Discard, player.Id).Cards.Count +
                "   剩余弃牌次数 " + (state.RemainingDiscardBudgetFor(player.Id)?.ToString() ?? "不限");
            log.text = string.Join("\n", history);
            RefreshButtons();
        }
        private static string ArtId(CardInstance card)
        {
            var face = Faces[card.DefinitionId];
            string rank = face.Rank == Rank.Ace ? "a" : face.Rank == Rank.King ? "k" : face.Rank == Rank.Queen ? "q" :
                face.Rank == Rank.Jack ? "j" : ((int)face.Rank).ToString("00", CultureInfo.InvariantCulture);
            return face.Suit.ToString().ToLowerInvariant() + "_" + rank;
        }
        private void RefreshButtons()
        {
            if (play != null) play.interactable = host != null && host.CanPlay;
            if (discard != null) discard.interactable = host != null && host.CanDiscard;
            if (endTurn != null) endTurn.interactable = host != null && host.CanEndTurn;
            if (restart != null) restart.interactable = host != null && !IsPresenting;
        }
        private void OnPlay() { if (host != null && host.CanPlay) host.PlaySelected(); }
        private void OnDiscard() { if (host != null && host.CanDiscard) host.DiscardSelected(); }
        private void OnEndTurn() { if (host != null && host.CanEndTurn) host.EndTurn(); }
        private void OnRestart() { if (host != null) host.StartBattle(); }
        private void Subscribe() { play?.onClick.AddListener(OnPlay); discard?.onClick.AddListener(OnDiscard); endTurn?.onClick.AddListener(OnEndTurn); restart?.onClick.AddListener(OnRestart); }
        private void Unsubscribe() { play?.onClick.RemoveListener(OnPlay); discard?.onClick.RemoveListener(OnDiscard); endTurn?.onClick.RemoveListener(OnEndTurn); restart?.onClick.RemoveListener(OnRestart); }
        private void ClearCards() { foreach (var card in cards) if (card != null) Remove(card.gameObject); cards.Clear(); }
        private static void Remove(GameObject value) { value.SetActive(false); if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnEnable() { Subscribe(); if (PresentedState != null) Render(); }
        private void OnDisable()
        {
            Unsubscribe();
            var final = settledState;
            if (final != null) Synchronize(final); else StopPresentation();
        }
        private void OnDestroy() => ClearCards();
    }
}
