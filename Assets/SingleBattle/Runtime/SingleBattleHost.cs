using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CardInteraction;
using CardPresentation;
using PokerEngine;
using UnityEngine;

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
        private PlayerBattleActor playerActor;
        [Header("战斗阵容")]
        [SerializeField] private PlayerBattleActor[] playerActors = Array.Empty<PlayerBattleActor>();
        [SerializeField] private EnemyBattleActor[] enemyActors = Array.Empty<EnemyBattleActor>();
        private IBattleTargetPolicy targetPolicy;
        private IBattleAllyTargetPolicy allyTargetPolicy;
        public IReadOnlyList<PlayerBattleActor> Players => Array.AsReadOnly(playerActors);
        public IReadOnlyList<EnemyBattleActor> Enemies => Array.AsReadOnly(enemyActors);
        private IEnumerable<BattleActor> Actors => Players.Cast<BattleActor>().Concat(Enemies);
        public CardZone PlayerZone(string kind) => State.Zone(kind, playerActor.ParticipantId);
        public int PlayerDiscards => State.DiscardsFor(playerActor.ParticipantId);
        public bool CanSwitchPlayer => State != null && State.HasStarted && !busy && !State.Duel.IsFinished &&
            State.Duel.Phase == ScoredDuelPhase.AwaitingPlayerHand && actions != null &&
            !actions.Hand.GetComponent<HandZoneView>().IsTransitioning && !actions.PlayRoute.IsTransferring;
        public bool SelectPlayer(PlayerBattleActor player)
        {
            if (!CanSwitchPlayer || !Players.Contains(player) || !State.Duel.CanAct(player.ParticipantId)) return false;
            if (player == playerActor) return true;
            // Stop only this observer's sessions. Authoritative cards, HP and round are untouched.
            actions.Hand.StopSession(); actions.Played.StopSession(); match.StopMatch();
            playerActor = player; Bind(); Publish(); return true;
        }
        public void ConfigureTargetPolicy(IBattleTargetPolicy policy)
        {
            if (State != null) throw new InvalidOperationException("Configure targeting before starting the battle.");
            targetPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
        }
        public void ConfigureAllyTargetPolicy(IBattleAllyTargetPolicy policy)
        {
            if (State != null) throw new InvalidOperationException("Configure support targeting before starting the battle.");
            allyTargetPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
        }
        [SerializeField, InspectorName("公共战局规则")] private BattleRulesProfile rulesProfile;
        public BattleRulesProfile RulesProfile => rulesProfile;
        public void ConfigureRules(BattleRulesProfile configuration)
        {
            if (busy) throw new InvalidOperationException("Wait for presentation before replacing next battle's rules.");
            rulesProfile = configuration != null ? configuration : throw new ArgumentNullException(nameof(configuration));
        }
        private Coroutine playback;
        private bool busy;
        public CardDuelEngine Engine { get; private set; }
        public CardDuelState State { get; private set; }
        public CardMatchHost Match => match;
        public HandActionPresenter Actions => actions;
        public PlayerBattleActor Player => playerActor != null ? playerActor : playerActors.FirstOrDefault();
        public EnemyBattleActor Enemy => Enemies.FirstOrDefault(actor => actor != null && actor.IsAlive) ?? Enemies.FirstOrDefault();
        private EnemyBattleActor ActingEnemy => Enemies.FirstOrDefault(actor => actor.ParticipantId == State.Duel.NextEnemy);
        public bool IsPresenting => busy;
        public string Message { get; private set; } = "选择 1–5 张牌组成牌型";
        public ScoreResult LastScore { get; private set; }
        public long LastDamage { get; private set; }
        public CardDuelTransition LastPlayResolution { get; private set; }
        public ParticipantId LastPlayedBy { get; private set; }
        public CardDuelTransition LastDiscardResolution { get; private set; }
        public ParticipantId LastDiscardedBy { get; private set; }
        public CardDuelTransition PreviewSelectedDiscard() => CanReceivePlayerInput
            ? Engine.PreviewDiscard(State, State.Revision, playerActor.ParticipantId, playerActor.SelectedCards()) : null;
        /// <summary>Read-only evaluation of the currently selected cards. Does not commit state or emit presentation events.</summary>
        public CardDuelTransition PreviewSelectedPlay() => CanReceivePlayerInput
            ? Engine.PreviewPlay(State, State.Revision, playerActor.ParticipantId, playerActor.SelectedCards()) : null;
        public event Action StateChanged;
        internal bool CanReceivePlayerInput => isActiveAndEnabled && State != null && State.HasStarted && !busy && match != null && match.IsRunning &&
            ReferenceEquals(match.Commands, playerActor) && State.Duel.Phase == ScoredDuelPhase.AwaitingPlayerHand &&
            PlayerZone(CardZoneKinds.Played).Cards.Count == 0 &&
            playerActor != null && State.Duel.CanAct(playerActor.ParticipantId) && Enemies.All(actor => actor != null && actor.IsBound && (!actor.IsAlive || actor.isActiveAndEnabled)) &&
            actions != null && actions.Hand.IsRunning && !actions.Hand.GetComponent<HandZoneView>().IsTransitioning;
        public bool CanEndTurn => playerActor != null && playerActor.CanEndTurn;

        public void Configure(CardMatchHost owner, HandActionPresenter panel, DeckOverviewPresenter deckOverview, DrawPilePresenter pile)
        {
            if (owner == null || panel == null || panel.Hand == null || panel.Played == null || panel.PlayRoute == null ||
                panel.Hand.Match != owner || panel.Played.Match != owner || owner.gameObject.scene != gameObject.scene)
                throw new ArgumentException("Bind a complete existing hand/played UI in this scene.");
            match = owner; actions = panel; overview = deckOverview; drawPile = pile;
        }
        public void ConfigureActors(PlayerBattleActor player, EnemyBattleActor enemy) => ConfigureActors(new[] { player }, new[] { enemy });
        public void ConfigureActors(IEnumerable<PlayerBattleActor> players, IEnumerable<EnemyBattleActor> enemies)
        {
            if (State != null) throw new InvalidOperationException("Configure actors before starting the battle.");
            var p = players?.ToArray(); var e = enemies?.ToArray();
            if (p == null || e == null || p.Length == 0 || e.Length == 0) throw new ArgumentException("Both rosters must be populated.");
            var all = p.Cast<BattleActor>().Concat(e).ToArray();
            if (all.Length > 64 || all.Any(actor => actor == null || actor.Battle != this || actor.gameObject.scene != gameObject.scene) ||
                all.Select(actor => actor.gameObject).Distinct().Count() != all.Length)
                throw new ArgumentException("Bind distinct actors owned by this battle in this scene.");
            playerActors = p; enemyActors = e; playerActor = p[0];
        }
        internal bool OwnsPlayer(PlayerBattleActor actor, ParticipantId id) => Players.Contains(actor) &&
            State.Duel.Players[Players.ToList().IndexOf(actor)].Id == id;
        internal bool OwnsEnemy(EnemyBattleActor actor, ParticipantId id) => Enemies.Contains(actor) &&
            State.Duel.Enemies[Enemies.ToList().IndexOf(actor)].Id == id;
        private void BindActors()
        {
            for (int i = 0; i < Players.Count; i++) Players[i].Bind(State.Duel.Players[i].Id);
            for (int i = 0; i < Enemies.Count; i++) Enemies[i].Bind(State.Duel.Enemies[i].Id);
        }
        public void StartBattle()
        {
            if (busy) return;
            if (match == null || actions == null || Players.Count == 0 || Enemies.Count == 0 || Actors.Any(actor => actor == null || actor.Battle != this) || Actors.Distinct().Count() != Actors.Count())
                throw new InvalidOperationException("Configure the battle and both scene actors first.");
            if (playerActor == null) playerActor = Players[0];
            if (match.Commands != null && !ReferenceEquals(match.Commands, playerActor)) throw new InvalidOperationException("Another authority owns the card UI.");
            if (rulesProfile == null) throw new InvalidOperationException("Bind the shared battle rules first.");
            var nextEngine = new CardDuelEngine(rulesProfile.BuildRules(), scoring: rulesProfile.BuildScoring(), targetPolicy: targetPolicy, allyTargetPolicy: allyTargetPolicy);
            string id = "battle/" + Guid.NewGuid().ToString("N");
            var nextState = nextEngine.Create(id, HmacDeckShuffler.CreateRandomState(),
                Players.Select((actor, index) => actor.CreateSetup(new ParticipantId(index == 0 ? "player" : id + "/player/" + index))),
                Enemies.Select((actor, index) => actor.CreateSetup(new ParticipantId(id + "/enemy/" + index))));
            Engine = nextEngine; State = nextState;
            LastScore = null; LastDamage = 0; LastPlayResolution = null; LastPlayedBy = default; LastDiscardResolution = null; LastDiscardedBy = default; Message = "选择 1–5 张牌组成牌型 · 能量用尽或手动结束回合";
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
            var deck = PlayerZone(CardZoneKinds.Deck).Id; var discard = PlayerZone(CardZoneKinds.Discard).Id;
            actions.Hand.Bind(match, PlayerZone(CardZoneKinds.Hand).Id, deck, discard, actions.Hand.GetComponent<HandZoneView>().FacingCamera);
            actions.Played.Bind(match, PlayerZone(CardZoneKinds.Played).Id, deck, discard);
            actions.Hand.BeginSession(); actions.Played.BeginSession();
            if (overview != null) { overview.BindArtwork(actions.Hand.GetComponent<HandZoneView>().CardPrefab); overview.Configure(match, overview.View, deck, State.Table.Zones.Where(zone => zone.ControllerId == playerActor.ParticipantId).SelectMany(zone => zone.Cards)); overview.Close(false); }
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
            var result = Engine.Execute(State, State.Revision, actor.ParticipantId, CardDuelAction.EndTurn);
            if (!result.Accepted) return false;
            State = result.State; busy = true; BeginPresentation(result, animate); return true;
        }
        internal int ExecutePlayerPlay(PlayerBattleActor actor, CardZonePlayHost hand, CardZonePlayHost destination, CardZoneTransferPresenter route, bool animate)
        {
            if (actor != playerActor || !playerActor.CanPlay(hand) || destination != actions.Played || route != actions.PlayRoute || route.IsTransferring) return 0;
            var ids = playerActor.SelectedCards(); var frame = match.PrepareExternalPlay(playerActor, hand.Zone.Id, destination.Zone.Id, ids, PlayedZoneView.SlotCount);
            var result = Engine.Execute(State, State.Revision, actor.ParticipantId, CardDuelAction.Play, ids); if (!result.Accepted) return 0;
            LastPlayResolution = result; LastPlayedBy = actor.ParticipantId;
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
            var result = Engine.Execute(State, State.Revision, actor.ParticipantId, CardDuelAction.Discard, ids); if (!result.Accepted) return 0;
            LastDiscardResolution = result; LastDiscardedBy = actor.ParticipantId;
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
                        foreach (var actor in Actors) actor.CancelPresentation();
                        CollectPlayed(); AdvanceEnemyImmediately();
                        foreach (var actor in Actors) actor.SnapToCurrentState(); Project(false);
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
            while (State.Duel.Phase == ScoredDuelPhase.AwaitingEnemyAction)
            {
                var enemy = ActingEnemy;
                Message = "玩家回合结束 · 敌人准备攻击 " + enemy.ReadIntent().Damage; Publish();
                yield return WaitForPresentation(enemy.PresentPreparation(true));
                var enemyResult = CommitEnemyTurn();
                Message = "敌人造成 " + enemyResult.Events.Sum(item => item.HealthLost) + " 伤害"; Publish();
                yield return PlayEffects(enemyResult);
            }
            CollectPlayed();
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
                ? "弃牌" + ((playerActor.Rules.DealPolicy as CardDuelDealPolicy)?.AfterDiscard == CardDuelRefillTiming.AfterAction ? "并补牌" : " · 下回合补牌") + " · 消耗 " + playerActor.Rules.DiscardEnergyCost + " 能量"
                : State.Duel.Phase == ScoredDuelPhase.AwaitingEnemyAction ? "玩家主动结束回合" : "选择牌型继续行动";
            Publish();
        }
        private BattleActor Actor(ParticipantId id) => Actors.Single(actor => actor.ParticipantId == id);
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
            if (actor != ActingEnemy || !actor.CanAct || !actor.IsBound) return null;
            return Engine.Execute(State, expectedRevision, actor.ParticipantId, CardDuelAction.EnemyAction);
        }
        private CardDuelTransition CommitEnemyTurn()
        {
            var result = ActingEnemy?.ExecuteTurn();
            if (result == null) throw new InvalidOperationException("No active enemy actor can act in this phase.");
            if (!result.Accepted) throw new InvalidOperationException("Enemy action rejected: " + result.Error);
            State = result.State; return result;
        }
        private void AdvanceEnemyImmediately()
        {
            while (State.Duel.Phase == ScoredDuelPhase.AwaitingEnemyAction)
            {
                var result = CommitEnemyTurn(); SnapEffects(result);
                Message = "敌人造成 " + result.Events.Sum(item => item.HealthLost) + " 伤害";
            }
        }

        private void CollectPlayed()
        {
            if (State.Duel.Phase != ScoredDuelPhase.AwaitingPlayerHand || PlayerZone(CardZoneKinds.Played).Cards.Count == 0) return;
            var collected = Engine.Execute(State, State.Revision, playerActor.ParticipantId, CardDuelAction.CollectPlayed);
            if (!collected.Accepted) throw new InvalidOperationException("Played collection rejected: " + collected.Error);
            State = collected.State;
            if (PlayerZone(CardZoneKinds.Hand).Cards.Count == 0) Message = "手牌已用尽 · 请结束回合";
        }
        private void Project(bool animate)
        {
            var old = actions.Hand.Cards.ToDictionary(card => card.Card.Id);
            var zones = State.Table.Zones.Select(zone => zone.Id != PlayerZone(CardZoneKinds.Hand).Id ? zone : new CardZone(zone.Id, zone.Kind, zone.ControllerId,
                actions.Hand.Cards.Select(card => card.Card.Id).Where(zone.Cards.Contains).Concat(zone.Cards.Where(id => !old.ContainsKey(id))))).ToArray();
            var styles = State.Table.Cards.Select(card => Style(card, old.TryGetValue(card.Id, out var before) &&
                PlayerZone(CardZoneKinds.Hand).Cards.Contains(card.Id) ? before.Interaction : default)).ToArray();
            var update = match.PrepareExternalProjection(playerActor, new CardTableState(State.Table.MatchId, State.Table.Cards, zones, State.Revision), styles);
            actions.Hand.GetComponent<HandZoneView>().PresentCommitted(update.Project(actions.Hand.Zone.Id), () => match.Commit(update), animate);
            actions.Played.GetComponent<PlayedZoneView>().Present(update.Project(actions.Played.Zone.Id), animate);
            match.Notify(update, actions.Hand, actions.Played); overview?.Refresh(); drawPile?.Refresh();
            if (PlayerZone(CardZoneKinds.Hand).Cards.Any(id => !old.ContainsKey(id))) actions.Hand.RequestSortAfterLanding(actions.Hand.SortOrder);
        }
        public void SnapToState()
        {
            if (playback != null) { StopCoroutine(playback); playback = null; }
            busy = false;
            foreach (var actor in Actors) if (actor != null) actor.CancelPresentation();
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
                foreach (var actor in Actors) if (actor != null) actor.SnapToCurrentState();
                Project(false); Publish();
            }
        }
        private void Publish()
        {
            if (CanSwitchPlayer && !State.Duel.CanAct(playerActor.ParticipantId))
            {
                var next = Players.FirstOrDefault(player => State.Duel.CanAct(player.ParticipantId));
                if (next != null && SelectPlayer(next)) return;
            }
            StateChanged?.Invoke(); actions?.Refresh();
        }
        private void OnEnable()
        {
            if (!Application.isPlaying || match == null || actions == null) return;
            if (State == null) StartBattle();
            else { BindActors(); Bind(); CollectPlayed(); AdvanceEnemyImmediately(); Project(false); Publish(); }
        }
        private void OnDisable() { if (State != null) SnapToState(); }
    }
}
