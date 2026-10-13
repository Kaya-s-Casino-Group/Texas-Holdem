using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CardInteraction;
using CardPresentation;
using PokerEngine;
using UnityEngine;
using UnityEngine.Serialization;

namespace Poker.SingleBattle
{
    /// <summary>Owns a standalone card duel and projects it to the existing card UI. No DrawEngine or Run authority.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-150)]
    public sealed class SingleBattleHost : MonoBehaviour
    {
        [SerializeField] private CardMatchHost match;
        [SerializeField] private HandActionPresenter actions;
        [SerializeField] private DeckOverviewPresenter overview;
        [SerializeField] private DrawPilePresenter drawPile;
        [SerializeField] private PlayerBattleActor playerActor;
        [SerializeField] private EnemyBattleActor enemyActor;
        [SerializeField, Min(1)] private int playerHealth = 40, enemyHealth = 75;
        [SerializeField, Min(0)] private int enemyDamage = 7;
        [SerializeField, Min(1)] private int pointsPerDamage = 5;
        [SerializeField, Range(1, 1000)] private int roundLimit = 8;
        [SerializeField, Range(0, 1000)] private int discards = 3;
        [Header("Player Energy")]
        [SerializeField, FormerlySerializedAs("energyPerTurn"), Min(1)] private int maxEnergy = 3;
        [SerializeField, Min(0)] private int initialEnergy = 3;
        [SerializeField, Min(1)] private int playEnergyCost = 1;
        [SerializeField, Min(1)] private int discardEnergyCost = 1;
        [Header("Hand Dealing")]
        [SerializeField, Range(1, 52)] private int handSize = 8;
        [SerializeField] private CardDuelRefillTiming playRefillTiming = CardDuelRefillTiming.NextPlayerTurn;
        [SerializeField] private CardDuelRefillTiming discardRefillTiming = CardDuelRefillTiming.AfterAction;
        private Coroutine playback;
        private bool busy;
        public CardDuelEngine Engine { get; private set; }
        public CardDuelState State { get; private set; }
        public CardMatchHost Match => match;
        public HandActionPresenter Actions => actions;
        public PlayerBattleActor Player => playerActor;
        public EnemyBattleActor Enemy => enemyActor;
        public bool IsPresenting => busy;
        public string Message { get; private set; } = "选择 1–5 张牌组成牌型";
        public ScoreResult LastScore { get; private set; }
        public long LastDamage { get; private set; }
        public event Action StateChanged;
        internal bool CanReceivePlayerInput => isActiveAndEnabled && State != null && State.HasStarted && !busy && match != null && match.IsRunning &&
            ReferenceEquals(match.Commands, playerActor) && State.Duel.Phase == ScoredDuelPhase.AwaitingPlayerHand &&
            State.Zone(CardZoneKinds.Played).Cards.Count == 0 &&
            playerActor != null && enemyActor != null && enemyActor.isActiveAndEnabled && enemyActor.IsBound &&
            actions != null && actions.Hand.IsRunning && !actions.Hand.GetComponent<HandZoneView>().IsTransitioning;
        public bool CanEndTurn => playerActor != null && playerActor.CanEndTurn;

        public void Configure(CardMatchHost owner, HandActionPresenter panel, DeckOverviewPresenter deckOverview, DrawPilePresenter pile)
        {
            if (owner == null || panel == null || panel.Hand == null || panel.Played == null || panel.PlayRoute == null ||
                panel.Hand.Match != owner || panel.Played.Match != owner || owner.gameObject.scene != gameObject.scene)
                throw new ArgumentException("Bind a complete existing hand/played UI in this scene.");
            match = owner; actions = panel; overview = deckOverview; drawPile = pile;
        }
        public void ConfigureActors(PlayerBattleActor player, EnemyBattleActor enemy)
        {
            if (State != null) throw new InvalidOperationException("Configure actors before starting the battle.");
            if (player == null || enemy == null || player.gameObject == enemy.gameObject || player.Battle != this || enemy.Battle != this ||
                player.gameObject.scene != gameObject.scene || enemy.gameObject.scene != gameObject.scene)
                throw new ArgumentException("Bind distinct player/enemy actors owned by this battle in this scene.");
            playerActor = player; enemyActor = enemy;
        }
        private void BindActors()
        {
            playerActor.Bind(State.Duel.PlayerId); enemyActor.Bind(State.Duel.EnemyId);
        }
        public void StartBattle()
        {
            if (busy) return;
            if (match == null || actions == null || playerActor == null || enemyActor == null || playerActor.Battle != this || enemyActor.Battle != this)
                throw new InvalidOperationException("Configure the battle and both scene actors first.");
            if (match.Commands != null && !ReferenceEquals(match.Commands, playerActor)) throw new InvalidOperationException("Another authority owns the card UI.");
            Engine = new CardDuelEngine(new ScoredDuelDefinition("single-battle.demo.3", new CombatantDefinition("hero", playerHealth),
                new CombatantDefinition("guardian", enemyHealth), new ScoreDamagePolicy(pointsPerDamage), enemyDamage, roundLimit,
                maxEnergy, playEnergyCost, initialEnergy, discardEnergyCost), handSize, discards,
                dealPolicy: new CardDuelDealPolicy(playRefillTiming, discardRefillTiming));
            State = Engine.Create("battle/" + Guid.NewGuid().ToString("N"), HmacDeckShuffler.CreateRandomState());
            LastScore = null; LastDamage = 0; Message = "选择 1–5 张牌组成牌型 · 能量用尽或手动结束回合";
            // Bind the empty table before the first turn deals, so opening cards use the normal draw presentation.
            BindActors(); Bind();
            var opening = Engine.Execute(State, State.Revision, CardDuelAction.Start);
            if (!opening.Accepted) throw new InvalidOperationException("Opening turn rejected: " + opening.Error);
            State = opening.State; Project(Application.isPlaying); Publish();
        }
        private void Bind()
        {
            actions.Hand.StopSession(); actions.Played.StopSession();
            match.StartExternalMatch(playerActor, State.Table, State.Table.Cards.Select(card => Style(card)));
            var deck = State.Zone(CardZoneKinds.Deck).Id; var discard = State.Zone(CardZoneKinds.Discard).Id;
            actions.Hand.Bind(match, State.Zone(CardZoneKinds.Hand).Id, deck, discard, actions.Hand.GetComponent<HandZoneView>().FacingCamera);
            actions.Played.Bind(match, State.Zone(CardZoneKinds.Played).Id, deck, discard);
            actions.Hand.BeginSession(); actions.Played.BeginSession();
            if (overview != null) { overview.BindArtwork(actions.Hand.GetComponent<HandZoneView>().CardPrefab); overview.Configure(match, overview.View, deck, State.Table.Cards.Select(card => card.Id)); overview.Close(false); }
            if (drawPile != null) { drawPile.Configure(actions.Hand, drawPile.View); if (overview != null) drawPile.BindOverview(overview); }
        }
        private CardZoneCardState Style(CardInstance card, CardInteractionState interaction = default) =>
            new CardZoneCardState(card, StandardArt(Engine.Face(card)), true, Color.white, interaction);
        public static string StandardArt(CardDefinition card)
        {
            string rank = card.Rank == Rank.Ace ? "a" : card.Rank == Rank.King ? "k" : card.Rank == Rank.Queen ? "q" :
                card.Rank == Rank.Jack ? "j" : ((int)card.Rank).ToString("00", CultureInfo.InvariantCulture);
            return card.Suit.ToString().ToLowerInvariant() + "_" + rank;
        }
        public bool EndTurn(bool animate = true) => playerActor != null && playerActor.EndTurn(animate);
        internal bool ExecutePlayerEndTurn(PlayerBattleActor actor, bool animate)
        {
            if (actor != playerActor || !playerActor.CanEndTurn) return false;
            var result = Engine.Execute(State, State.Revision, CardDuelAction.EndTurn);
            if (!result.Accepted) return false;
            State = result.State; busy = true; BeginPresentation(result, animate); return true;
        }
        internal int ExecutePlayerPlay(PlayerBattleActor actor, CardZonePlayHost hand, CardZonePlayHost destination, CardZoneTransferPresenter route, bool animate)
        {
            if (actor != playerActor || !playerActor.CanPlay(hand) || destination != actions.Played || route != actions.PlayRoute || route.IsTransferring) return 0;
            var ids = playerActor.SelectedCards(); var frame = match.PrepareExternalPlay(playerActor, hand.Zone.Id, destination.Zone.Id, ids, PlayedZoneView.SlotCount);
            var result = Engine.Execute(State, State.Revision, CardDuelAction.Play, ids); if (!result.Accepted) return 0;
            State = result.State; busy = true;
            try
            {
                route.PresentTransfer(frame.Project(hand.Zone.Id), frame.Project(destination.Zone.Id), ids.Select(id => id.Value), animate, () => match.Commit(frame));
                match.Notify(frame, hand, destination); BeginPresentation(result, animate);
            }
            catch { SnapToState(); throw; }
            return ids.Length;
        }
        internal int ExecutePlayerDiscard(PlayerBattleActor actor, CardZonePlayHost hand, bool animate)
        {
            if (actor != playerActor || !playerActor.CanDiscard(hand)) return 0;
            var ids = playerActor.SelectedCards(); var frame = match.PrepareExternalDiscard(playerActor, hand.Zone.Id, hand.DiscardZone.Id, ids);
            var result = Engine.Execute(State, State.Revision, CardDuelAction.Discard, ids); if (!result.Accepted) return 0;
            State = result.State; busy = true;
            try
            {
                hand.GetComponent<HandZoneView>().PresentDiscardCommitted(frame.Project(hand.Zone.Id), ids.Select(id => id.Value), () => match.Commit(frame), animate);
                match.Notify(frame, hand); BeginPresentation(result, animate);
            }
            catch { SnapToState(); throw; }
            return ids.Length;
        }
        private void BeginPresentation(CardDuelTransition result, bool animate)
        {
            if (animate && Application.isPlaying && isActiveAndEnabled)
            {
                var running = StartCoroutine(RunPresentation(Present(result)));
                playback = busy ? running : null;
            }
            else { Show(result); SnapEffects(result); CollectPlayed(); AdvanceEnemyImmediately(); Project(false); busy = false; Publish(); }
            actions.Refresh();
        }
        // Flatten nested sequences so exceptions/cancellation from any protocol operation reach one recovery boundary.
        private IEnumerator RunPresentation(IEnumerator sequence)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(sequence);
            try
            {
                while (stack.Count > 0)
                {
                    bool next = false; object current = null; Exception failure = null;
                    try { next = stack.Peek().MoveNext(); if (next) current = stack.Peek().Current; }
                    catch (Exception error) { failure = error; }
                    if (failure != null)
                    {
                        Debug.LogException(failure, this);
                        playerActor.CancelPresentation(); enemyActor.CancelPresentation();
                        CollectPlayed(); AdvanceEnemyImmediately();
                        playerActor.SnapToCurrentState(); enemyActor.SnapToCurrentState(); Project(false);
                        yield break;
                    }
                    if (!next) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current is IEnumerator nested && !(current is CustomYieldInstruction)) { stack.Push(nested); continue; }
                    yield return current;
                }
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                playback = null; busy = false; Publish();
            }
        }
        private static IEnumerator WaitForPresentation(BattlePresentationOperation operation)
        {
            yield return operation;
            if (operation.Status != BattlePresentationStatus.Completed)
                throw new InvalidOperationException("Battle presentation did not complete: " + operation.Status, operation.Error);
        }
        private IEnumerator Present(CardDuelTransition result)
        {
            while (actions.PlayRoute.IsTransferring || actions.Hand.GetComponent<HandZoneView>().IsTransitioning) yield return null;
            Show(result);
            yield return PlayEffects(result);
            if (State.Duel.Phase == ScoredDuelPhase.AwaitingEnemyAction)
            {
                Message = "玩家回合结束 · 敌人准备攻击 " + enemyActor.ReadIntent().Damage; Publish();
                yield return WaitForPresentation(enemyActor.PresentPreparation(true));
                var enemyResult = CommitEnemyTurn();
                Message = "敌人造成 " + enemyResult.Events.Sum(item => item.HealthLost) + " 伤害"; Publish();
                yield return PlayEffects(enemyResult);
            }
            else CollectPlayed();
            Project(true); Publish();
            while (actions.Hand.GetComponent<HandZoneView>().IsTransitioning || actions.Played.GetComponent<PlayedZoneView>().IsTransitioning) yield return null;
        }
        private void Show(CardDuelTransition result)
        {
            if (result.Score != null)
            {
                LastScore = result.Score; LastDamage = result.Events.Sum(item => (long)item.HealthLost);
                Message = SingleBattleHud.HandName(LastScore.Category) + " · " + LastScore.Total + " 分 → " + LastDamage + " 伤害";
            }
            else Message = result.State.Duel.Actions.LastOrDefault()?.Kind == ScoredDuelActionKind.Discard
                ? "弃牌" + (discardRefillTiming == CardDuelRefillTiming.AfterAction ? "并补牌" : " · 下回合补牌") + " · 消耗 " + Engine.Duel.Definition.DiscardEnergyCost + " 能量"
                : State.Duel.Phase == ScoredDuelPhase.AwaitingEnemyAction ? "玩家主动结束回合" : "选择牌型继续行动";
            Publish();
        }
        private BattleActor Actor(ParticipantId id) => id == playerActor.ParticipantId ? (BattleActor)playerActor :
            id == enemyActor.ParticipantId ? enemyActor : throw new InvalidOperationException("Unbound presentation target.");
        private IEnumerator PlayEffects(CardDuelTransition result)
        {
            foreach (var group in result.Events.Where(item => item.Effect.Kind == CombatEffectKind.Damage).GroupBy(item => item.Effect.Source))
                yield return WaitForPresentation(Actor(group.Key).PresentAction(group.First(), true));
            foreach (var effect in result.Events)
            {
                var target = Actor(effect.Effect.Target);
                yield return WaitForPresentation(target.PresentHit(effect, true));
                yield return WaitForPresentation(target.PresentDeath(effect, true));
            }
        }
        private void SnapEffects(CardDuelTransition result)
        {
            foreach (var group in result.Events.Where(item => item.Effect.Kind == CombatEffectKind.Damage).GroupBy(item => item.Effect.Source))
                Actor(group.Key).PresentAction(group.First(), false);
            foreach (var effect in result.Events)
            { var target = Actor(effect.Effect.Target); target.PresentHit(effect, false); target.PresentDeath(effect, false); }
        }
        internal CardDuelTransition ExecuteEnemyTurn(EnemyBattleActor actor, long expectedRevision)
        {
            if (actor != enemyActor || !enemyActor.CanAct || !enemyActor.IsBound) return null;
            return Engine.Execute(State, expectedRevision, CardDuelAction.EnemyAction);
        }
        private CardDuelTransition CommitEnemyTurn()
        {
            var result = enemyActor.ExecuteTurn();
            if (result == null) throw new InvalidOperationException("No active enemy actor can act in this phase.");
            if (!result.Accepted) throw new InvalidOperationException("Enemy action rejected: " + result.Error);
            State = result.State; return result;
        }
        private void AdvanceEnemyImmediately()
        {
            if (State.Duel.Phase != ScoredDuelPhase.AwaitingEnemyAction) return;
            var result = CommitEnemyTurn(); SnapEffects(result);
            Message = "敌人造成 " + result.Events.Sum(item => item.HealthLost) + " 伤害 · 轮到你了";
        }
        private void CollectPlayed()
        {
            if (State.Duel.Phase != ScoredDuelPhase.AwaitingPlayerHand || State.Zone(CardZoneKinds.Played).Cards.Count == 0) return;
            var collected = Engine.Execute(State, State.Revision, CardDuelAction.CollectPlayed);
            if (!collected.Accepted) throw new InvalidOperationException("Played collection rejected: " + collected.Error);
            State = collected.State;
            if (State.Zone(CardZoneKinds.Hand).Cards.Count == 0) Message = "手牌已用尽 · 请结束回合";
        }
        private void Project(bool animate)
        {
            var old = actions.Hand.Cards.ToDictionary(card => card.Card.Id);
            var zones = State.Table.Zones.Select(zone => zone.Kind != CardZoneKinds.Hand ? zone : new CardZone(zone.Id, zone.Kind, zone.ControllerId,
                actions.Hand.Cards.Select(card => card.Card.Id).Where(zone.Cards.Contains).Concat(zone.Cards.Where(id => !old.ContainsKey(id))))).ToArray();
            var styles = State.Table.Cards.Select(card => Style(card, old.TryGetValue(card.Id, out var before) &&
                State.Zone(CardZoneKinds.Hand).Cards.Contains(card.Id) ? before.Interaction : default)).ToArray();
            var update = match.PrepareExternalProjection(playerActor, new CardTableState(State.Table.MatchId, State.Table.Cards, zones, State.Revision), styles);
            actions.Hand.GetComponent<HandZoneView>().PresentCommitted(update.Project(actions.Hand.Zone.Id), () => match.Commit(update), animate);
            actions.Played.GetComponent<PlayedZoneView>().Present(update.Project(actions.Played.Zone.Id), animate);
            match.Notify(update, actions.Hand, actions.Played); overview?.Refresh(); drawPile?.Refresh();
            if (State.Zone(CardZoneKinds.Hand).Cards.Any(id => !old.ContainsKey(id))) actions.Hand.RequestSortAfterLanding(actions.Hand.SortOrder);
        }
        public void SnapToState()
        {
            if (playback != null) { StopCoroutine(playback); playback = null; }
            busy = false;
            if (playerActor != null) playerActor.CancelPresentation();
            if (enemyActor != null) enemyActor.CancelPresentation();
            // Scene teardown order is independent of our hierarchy; Unity's destroyed objects
            // do not compare null through the null-conditional operator.
            if (actions == null || actions.Hand == null || actions.Played == null || match == null) return;
            if (actions.PlayRoute != null) actions.PlayRoute.CompleteTransfers();
            var handView = actions.Hand.GetComponent<HandZoneView>();
            if (handView != null) handView.CompleteTransitions();
            if (State != null && ReferenceEquals(match.Commands, playerActor))
            {
                CollectPlayed();
                if (isActiveAndEnabled) AdvanceEnemyImmediately();
                if (playerActor != null) playerActor.SnapToCurrentState();
                if (enemyActor != null) enemyActor.SnapToCurrentState();
                Project(false); Publish();
            }
        }
        private void Publish() { StateChanged?.Invoke(); actions?.Refresh(); }
        private void OnEnable()
        {
            if (!Application.isPlaying || match == null || actions == null) return;
            if (State == null) StartBattle();
            else { BindActors(); Bind(); CollectPlayed(); AdvanceEnemyImmediately(); Project(false); Publish(); }
        }
        private void OnDisable() { if (State != null) SnapToState(); }
    }
}
