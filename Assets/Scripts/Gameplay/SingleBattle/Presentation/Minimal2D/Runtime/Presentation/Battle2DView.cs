using System;
using System.Collections.Generic;
using System.Linq;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.Battle2DValidation
{
    /// <summary>A rule-independent frontend. The same display store and director drive cards, actors and UI.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ScenePresentationDirector))]
    public sealed class Battle2DView : MonoBehaviour, IBattlePresentationFrontend, IInteractionFeedback
    {
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Transform cardRoot;
        [SerializeField] private Transform playerActor, enemyActor;
        [SerializeField] private TMP_Text status, details, log;
        [SerializeField] private Button play, discard, endTurn, restart;
        private IDisplayReader store;
        [SerializeField] private ScenePresentationDirector director;
        public ScenePresentationDirector SceneDirector => director != null ? director : GetComponent<ScenePresentationDirector>();
        private InteractionSession interaction;
        private PresentationScope feedbackScope;
        private BattleDisplaySnapshot settledState;
        private readonly List<string> selected = new List<string>();
        private readonly List<Battle2DCard> cards = new List<Battle2DCard>();
        private readonly Queue<string> history = new Queue<string>();
        private readonly SortedDictionary<long, string> pendingLogs = new SortedDictionary<long, string>();
        private string logBatchId;
        private Action<string> selectionIntent;
        private Action playIntent, discardIntent, endTurnIntent, restartIntent;
        private bool canPlay, canDiscard, canEndTurn;
        private long playbackEpoch;
        public event Action DisplayChanged;
        public event Action InteractionClosed;
        public event Action PresentationFinished;
        public bool IsPresenting => director != null && director.IsPresenting;
        public Transform PlayerActor => playerActor;
        public Transform EnemyActor => enemyActor;
        public BattleDisplaySnapshot PresentedState => store?.Current;
        public IDisplayReader Display => store;
        public CandidatePresentationModel Candidate => interaction?.Current;
        public long NextSelectionVersion => (interaction?.SelectionVersion ?? 0) + 1;
        public IReadOnlyList<string> Selected => selected.AsReadOnly();
        public IReadOnlyList<Battle2DCard> Cards => cards.AsReadOnly();
        public Button PlayButton => play;
        public Button DiscardButton => discard;
        public Button EndTurnButton => endTurn;
        public Button RestartButton => restart;
        public string LogText => log != null ? log.text : "";

        public void ConfigureDirector(ScenePresentationDirector frontend)
        {
            if (frontend == null || frontend.gameObject != gameObject)
                throw new ArgumentException("The 2D view requires its co-located scene director.");
            if (store != null && director != frontend) throw new InvalidOperationException("Cannot replace an initialized display store.");
            director = frontend; EnsurePresentation();
        }
        public void Configure(GameObject prefab, Transform root, TMP_Text statusText, TMP_Text detailText, TMP_Text logText,
            Button playButton, Button discardButton, Button endTurnButton, Button restartButton)
        {
            Unsubscribe();
            cardPrefab = prefab; cardRoot = root; status = statusText; details = detailText; log = logText;
            play = playButton; discard = discardButton; endTurn = endTurnButton; restart = restartButton;
            if (cardPrefab == null || cardRoot == null || status == null || details == null || log == null ||
                play == null || discard == null || endTurn == null || restart == null)
                throw new ArgumentException("Configure the card artwork and minimal 2D controls.");
            EnsurePresentation();
            if (isActiveAndEnabled) Subscribe();
        }
        public void ConfigureActors(Transform player, Transform enemy)
        {
            playerActor = player; enemyActor = enemy;
        }
        private void Awake() => EnsurePresentation();
        private void EnsurePresentation()
        {
            if (store != null) return;
            director = SceneDirector;
            if (director == null) throw new InvalidOperationException("Bind a ScenePresentationDirector first.");
            store = director.Display;
            ResetInteraction();
            store.Changed += OnDisplayChanged;
        }
        private void ResetInteraction()
        {
            if (interaction != null)
            {
                interaction.Changed -= OnCandidateChanged;
                interaction.Dispose();
            }
            interaction = new InteractionSession(this);
            interaction.Changed += OnCandidateChanged;
        }
        private void OnDisplayChanged(DisplayUpdate update)
        {
            if (update.Current.Cursor.BatchId == logBatchId)
            {
                foreach (var entry in pendingLogs.Where(entry => entry.Key <= update.Current.Cursor.Sequence).ToArray())
                { AddLog(entry.Value); pendingLogs.Remove(entry.Key); }
            }
            Render(); DisplayChanged?.Invoke();
        }
        public void BindCommands(Action<string> select, Action playAction, Action discardAction, Action endAction, Action restartAction)
        {
            selectionIntent = select; playIntent = playAction; discardIntent = discardAction;
            endTurnIntent = endAction; restartIntent = restartAction; RefreshButtons();
        }
        public void SetInteraction(CandidatePresentationModel candidate, bool endAllowed)
        {
            EnsurePresentation(); canEndTurn = endAllowed;
            interaction.Reconcile(candidate); RefreshButtons();
        }
        public void BeginCommit() { interaction?.BeginCommit(); canEndTurn = false; RefreshButtons(); }
        public void CloseInteraction()
        {
            interaction?.Close(); canPlay = canDiscard = canEndTurn = false;
            selected.Clear(); ReconcileSelection(); RefreshButtons(); InteractionClosed?.Invoke();
        }
        private void OnCandidateChanged(CandidatePresentationModel candidate)
        {
            canPlay = candidate.CanPlay; canDiscard = candidate.CanDiscard;
            if (candidate.State == CandidateState.Closed || candidate.State == CandidateState.Committing)
            { selected.Clear(); ReconcileSelection(); }
            Render();
        }
        public void Reconcile(CandidatePresentationModel next, PresentationScope scope)
        {
            feedbackScope = scope;
            selected.Clear(); selected.AddRange(next.SelectedIds);
            ReconcileSelection();
        }
        public void Release(PresentationScope scope, ReleaseMode mode)
        {
            if (!ReferenceEquals(feedbackScope, scope)) return;
            feedbackScope = null; selected.Clear();
            // Selection contributes an offset/tint only. Removing it reuses each card's latest layout slot.
            ReconcileSelection();
        }
        public void Initialize(BattleDisplaySnapshot snapshot)
        {
            EnsurePresentation(); ++playbackEpoch; settledState = null;
            CloseInteraction();
            // Initialize starts a new session generation, even when a host reuses the battle id.
            ResetInteraction(); ClearCards(); history.Clear(); pendingLogs.Clear(); logBatchId = null;
            director.Initialize(snapshot ?? throw new ArgumentNullException(nameof(snapshot)));
            Render();
        }
        public IPresentationOperation Present(PresentationBatch batch, PlaybackMode mode)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            EnsurePresentation();
            CloseInteraction();
            settledState = batch.FinalSnapshot; long epoch = ++playbackEpoch;
            PrepareLogs(batch);
            var operation = director.Present(batch,
                mode == PlaybackMode.Animated && Application.isPlaying && isActiveAndEnabled ? PlaybackMode.Animated : PlaybackMode.Instant);
            bool finished = false;
            void Finish(PresentationStatus value)
            {
                if (finished || epoch != playbackEpoch || value == PresentationStatus.Pending) return;
                finished = true; settledState = null; Render(); PresentationFinished?.Invoke();
            }
            operation.Finished += Finish;
            Finish(operation.Status);
            RefreshButtons();
            return operation;
        }
        public void Synchronize(BattleDisplaySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            EnsurePresentation(); ++playbackEpoch; settledState = null;
            CloseInteraction();
            if (snapshot.Cursor.BatchId != logBatchId) { pendingLogs.Clear(); logBatchId = null; }
            director.Synchronize(snapshot); Render(); PresentationFinished?.Invoke();
        }

        private static string Value(BattleDisplayChange change, string key) => change.Values.TryGetValue(key, out var value) ? value : "";
        private void PrepareLogs(PresentationBatch batch)
        {
            pendingLogs.Clear(); logBatchId = batch.Id;
            foreach (var completed in batch.Changes.Where(fact => fact.SemanticKey == "ActionResolved"))
            {
                string actionId = Value(completed, "actionId");
                var facts = batch.Changes.Where(fact => Value(fact, "actionId") == actionId).ToArray();
                var started = facts.FirstOrDefault(fact => fact.SemanticKey == "ActionStarted");
                int count = started?.CardIds.Count ?? 0;
                string score = facts.Select(fact => Value(fact, "score")).LastOrDefault(value => !string.IsNullOrEmpty(value)) ?? "0";
                long damage = facts.Sum(fact => fact.Effect?.HealthLost ?? 0);
                switch (Value(completed, "action"))
                {
                    case "Start": pendingLogs[completed.Sequence] = "开局 · 发牌完成"; break;
                    case "Play": pendingLogs[completed.Sequence] = "出牌 " + count + " 张 · " + score + " 分 · 造成 " + damage + " 伤害"; break;
                    case "Discard": pendingLogs[completed.Sequence] = "弃牌 " + count + " 张" + (facts.Any(fact => fact.Effect != null) ? " · 格挡/治疗收益已结算" : ""); break;
                    case "EndTurn": pendingLogs[completed.Sequence] = "玩家结束回合"; break;
                    case "EnemyAction": pendingLogs[completed.Sequence] = "敌人行动 · 造成 " + damage + " 伤害"; break;
                }
            }
        }
        public void ToggleSelection(string id)
        {
            if (!IsPresenting && !string.IsNullOrEmpty(id)) selectionIntent?.Invoke(id);
        }
        public void ShowRejected(string reason) { AddLog("操作未执行：" + reason); Render(); }
        private void AddLog(string value) { history.Enqueue(value); while (history.Count > 5) history.Dequeue(); }
        private CardZoneDisplayData Hand => PresentedState?.Zones.FirstOrDefault(zone => zone.OwnerId == PresentedState.ObserverId && zone.Kind == "hand");
        private void ReconcileSelection()
        {
            var hand = Hand;
            foreach (var card in cards)
            {
                var data = hand?.Cards.FirstOrDefault(item => item.CardId == card.Id);
                card.ShowSelection(selected.Contains(card.Id), data != null && !string.IsNullOrEmpty(data.DisabledReason));
            }
        }
        private void Render()
        {
            var snapshot = PresentedState;
            if (snapshot == null || cardRoot == null) return;
            var hand = Hand;
            if (hand == null) return;
            var player = snapshot.Unit(snapshot.ObserverId);
            if (player == null) return;
            var displayedCards = hand.Cards.Where(card => !card.Hidden && !string.IsNullOrEmpty(card.CardId)).ToArray();
            foreach (var old in cards.Where(card => displayedCards.All(item => item.CardId != card.Id)).ToArray())
            { cards.Remove(old); Remove(old.gameObject); }
            foreach (var data in displayedCards)
            {
                var existing = cards.FirstOrDefault(card => card.Id == data.CardId);
                if (existing != null) { existing.Surface.SetCard(data.ArtKey); continue; }
                var instance = Instantiate(cardPrefab, cardRoot); instance.name = "Card " + data.ViewId;
                var card = instance.AddComponent<Battle2DCard>(); card.Bind(this, data.CardId, data.ArtKey); cards.Add(card);
            }
            var order = displayedCards.Select(item => item.CardId).ToList();
            cards.Sort((a, b) => order.IndexOf(a.Id).CompareTo(order.IndexOf(b.Id)));
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
            }
            ReconcileSelection();
            string outcome = snapshot.Phase == "won" ? "战斗胜利" : snapshot.Phase == "lost" ? "战斗失败" : snapshot.Phase == "enemy" ? "敌人行动" : "玩家行动";
            status.text = outcome + "     回合 " + (snapshot.IsFinished ? snapshot.CompletedRounds : snapshot.CompletedRounds + 1);
            status.color = snapshot.Phase == "won" ? new Color(.4f, 1, .6f) : snapshot.Phase == "lost" ? new Color(1, .45f, .4f) : Color.white;
            long discardBudget = snapshot.Resource(player.Id, "discards")?.Value ?? -1;
            details.text = "玩家 HP " + player.Health + "/" + player.MaxHealth + "   格挡 " + player.Block + "   能量 " + player.Energy + "/" + player.MaxEnergy +
                "\n" + string.Join("    ", snapshot.Units.Where(unit => unit.Team == "enemy").Select((unit, index) => "敌人 " + (index + 1) + " HP " + unit.Health + "/" + unit.MaxHealth)) +
                "\n手牌 " + hand.Cards.Count + "   牌库 " + ZoneCount("deck") + "   弃牌堆 " + ZoneCount("discard") +
                "   剩余弃牌次数 " + (discardBudget < 0 ? "不限" : discardBudget.ToString()) + CandidateText();
            log.text = string.Join("\n", history); RefreshButtons();
        }
        private string CandidateText()
        {
            var candidate = Candidate;
            if (candidate == null || candidate.State == CandidateState.Empty || candidate.State == CandidateState.Closed || candidate.State == CandidateState.Committing) return "";
            return candidate.State == CandidateState.Valid
                ? "\n候选：" + candidate.PatternName + "   " + candidate.Points + " × " + candidate.Multiplier + " = " + candidate.Total
                : "\n" + (candidate.Reason ?? "当前选择不可用");
        }
        private int ZoneCount(string kind) => PresentedState.Zones.FirstOrDefault(zone => zone.OwnerId == PresentedState.ObserverId && zone.Kind == kind)?.Cards.Count ?? 0;
        private void RefreshButtons()
        {
            if (play != null) play.interactable = playIntent != null && canPlay && !IsPresenting;
            if (discard != null) discard.interactable = discardIntent != null && canDiscard && !IsPresenting;
            if (endTurn != null) endTurn.interactable = endTurnIntent != null && canEndTurn && !IsPresenting;
            if (restart != null) restart.interactable = restartIntent != null && !IsPresenting;
        }
        private void OnPlay() { if (canPlay && !IsPresenting) playIntent?.Invoke(); }
        private void OnDiscard() { if (canDiscard && !IsPresenting) discardIntent?.Invoke(); }
        private void OnEndTurn() { if (canEndTurn && !IsPresenting) endTurnIntent?.Invoke(); }
        private void OnRestart() { if (!IsPresenting) restartIntent?.Invoke(); }
        private void Subscribe() { play?.onClick.AddListener(OnPlay); discard?.onClick.AddListener(OnDiscard); endTurn?.onClick.AddListener(OnEndTurn); restart?.onClick.AddListener(OnRestart); }
        private void Unsubscribe() { play?.onClick.RemoveListener(OnPlay); discard?.onClick.RemoveListener(OnDiscard); endTurn?.onClick.RemoveListener(OnEndTurn); restart?.onClick.RemoveListener(OnRestart); }
        private void ClearCards() { foreach (var card in cards) if (card != null) Remove(card.gameObject); cards.Clear(); }
        private static void Remove(GameObject value) { value.SetActive(false); if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnEnable() { Subscribe(); if (PresentedState != null) { Render(); PresentationFinished?.Invoke(); } }
        private void OnDisable()
        {
            Unsubscribe();
            var final = settledState ?? PresentedState;
            if (final != null) Synchronize(final);
        }
        private void OnDestroy() { if (store != null) store.Changed -= OnDisplayChanged; interaction?.Dispose(); ClearCards(); }
    }
}
