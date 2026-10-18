using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CardInteraction;
using CardPresentation;
using PokerEngine;
using Poker.BattleHosting;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Adapts scene bindings and player input to the shared battle factory and command port.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BattleSceneHost))]
    public sealed partial class SingleBattleSceneAdapter : MonoBehaviour, IBattleSceneAdapter, IBattleSessionSource
    {
        [SerializeField] private CardMatchHost match;
        [SerializeField] private HandActionPresenter actions;
        [SerializeField] private DeckOverviewPresenter overview;
        [SerializeField] private DrawPilePresenter drawPile;
        private PlayerBattleActor playerActor;
        [Header("战斗阵容")]
        [SerializeField] private PlayerBattleActor[] playerActors = Array.Empty<PlayerBattleActor>();
        [SerializeField] private EnemyBattleActor[] enemyActors = Array.Empty<EnemyBattleActor>();
        public IReadOnlyList<PlayerBattleActor> Players => Array.AsReadOnly(playerActors);
        public IReadOnlyList<EnemyBattleActor> Enemies => Array.AsReadOnly(enemyActors);
        private IEnumerable<BattleActor> Actors => Players.Cast<BattleActor>().Concat(Enemies);
        public CardZone PlayerZone(string kind) => State.Zone(kind, playerActor.ParticipantId);
        public int PlayerDiscards => State.DiscardsFor(playerActor.ParticipantId);
        public bool CanSwitchPlayer => State != null && ReferenceEquals(State, AuthorityState) && State.HasStarted && !IsPresenting && !State.Duel.IsFinished &&
            State.Duel.Phase == ScoredDuelPhase.AwaitingPlayerHand && actions != null &&
            !actions.Hand.GetComponent<HandZoneView>().IsTransitioning;
        public bool SelectPlayer(PlayerBattleActor player)
        {
            if (!CanSwitchPlayer || !Players.Contains(player) || !State.Duel.CanAct(player.ParticipantId)) return false;
            if (player == playerActor) return true;
            // Stop only this observer's sessions. Authoritative cards, HP and round are untouched.
            actions.Hand.StopSession(); match.StopMatch();
            playerActor = player; Bind(); bridge?.Synchronize(AuthorityState); Publish(); return true;
        }
        [SerializeField, InspectorName("公共战局规则")] private BattleRulesProfile rulesProfile;
        public BattleRulesProfile RulesProfile => rulesProfile;
        public void ConfigureRules(BattleRulesProfile configuration)
        {
            if (busy) throw new InvalidOperationException("Wait for presentation before replacing next battle's rules.");
            rulesProfile = configuration != null ? configuration : throw new ArgumentNullException(nameof(configuration));
            configurationSource = BattleConfigurationSource.LegacyProfiles;
            ContentDiagnostics = Array.Empty<BattleDiagnostic>();
        }
        /// <summary>The snapshot currently shown by actors and HUD. It may lag behind AuthorityState during playback.</summary>
        public CardDuelState PresentedState { get; private set; }
        /// <summary>Compatibility accessor for existing scene/UI observers. Commands use Session and AuthorityState.</summary>
        public CardDuelState State => PresentedState;
        public CardMatchHost Match => match;
        public HandActionPresenter Actions => actions;
        public PlayerBattleActor Player => playerActor != null ? playerActor : playerActors.FirstOrDefault();
        public EnemyBattleActor Enemy => Enemies.FirstOrDefault(actor => actor != null && actor.IsAlive) ?? Enemies.FirstOrDefault();
        public bool IsPresenting => busy || presentationDirector != null && presentationDirector.IsPresenting || actions != null && actions.Hand != null && actions.Hand.GetComponent<HandZoneView>().IsTransitioning;
        bool IBattleSessionPresentation.IsPresenting => IsPresenting;
        public string Message { get; private set; } = "选择 1–5 张牌组成牌型";
        public ScoreResult LastScore { get; private set; }
        public long LastDamage { get; private set; }
        public CardDuelTransition LastPlayResolution { get; private set; }
        public ParticipantId LastPlayedBy { get; private set; }
        public CardDuelTransition LastDiscardResolution { get; private set; }
        public ParticipantId LastDiscardedBy { get; private set; }
        public CardDuelTransition PreviewSelectedDiscard() => CanReceivePlayerInput
            ? Playback.PreviewDiscard(playerActor.ParticipantId, actions.Hand.CaptureSelection().Cards) : null;
        /// <summary>Read-only evaluation of the currently selected cards. Does not commit state or emit presentation events.</summary>
        public CardDuelTransition PreviewSelectedPlay() => CanReceivePlayerInput
            ? Playback.PreviewPlay(playerActor.ParticipantId, actions.Hand.CaptureSelection().Cards) : null;
        public event Action StateChanged;
        internal bool CanReceivePlayerInput => isActiveAndEnabled && SceneHost.isActiveAndEnabled && State != null && ReferenceEquals(State, AuthorityState) && match != null && match.IsRunning &&
            playerActor != null && ReferenceEquals(match.Commands, playerActor) && Playback != null && Playback.CanAct(playerActor.ParticipantId) &&
            Enemies.All(actor => actor != null && actor.IsBound && (!actor.IsAlive || actor.isActiveAndEnabled)) &&
            actions != null && actions.Hand.IsRunning && !actions.Hand.GetComponent<HandZoneView>().IsTransitioning;
        public bool CanEndTurn => playerActor != null && playerActor.CanEndTurn;

        public void Configure(CardMatchHost owner, HandActionPresenter panel, DeckOverviewPresenter deckOverview, DrawPilePresenter pile)
        {
            if (owner == null || panel == null || panel.Hand == null || panel.Hand.GetComponent<HandZoneView>() == null ||
                panel.Hand.Match != owner || owner.gameObject.scene != gameObject.scene)
                throw new ArgumentException("Bind a hand UI and match in this scene.");
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
        BattleSession IBattleSessionSource.CreateSession() => BuildConfiguredSession();
        /// <summary>Builds an undealt session for validation or play, without changing the current battle.</summary>
        public BattleSession BuildConfiguredSession(string battleId = null, RandomState random = null)
        {
            RequireSceneBindings();
            if (UsesXmlContent) return BuildContentSession(battleId, random);
            if (rulesProfile == null) throw new InvalidOperationException("Bind the shared battle rules first.");
            string id = battleId ?? BattleSessionFactory.CreateBattleId();
            return BattleSessionFactory.Create(rulesProfile,
                Players.Select((actor, index) => actor.CreateSetup(new ParticipantId(index == 0 ? "player" : id + "/player/" + index))),
                Enemies.Select((actor, index) => actor.CreateSetup(new ParticipantId(id + "/enemy/" + index))), id, random);
        }
        private void RequireSceneBindings()
        {
            if (match == null || actions == null || Players.Count == 0 || Enemies.Count == 0 || Actors.Any(actor => actor == null || actor.Battle != this) || Actors.Distinct().Count() != Actors.Count())
                throw new InvalidOperationException("Configure the battle and both scene actors first.");
            if (playerActor == null) playerActor = Players[0];
            if (match.Commands != null && !ReferenceEquals(match.Commands, playerActor)) throw new InvalidOperationException("Another authority owns the card UI.");
        }
        void IBattleSceneAdapter.ValidateSession(BattleSession session)
        {
            if (busy) throw new InvalidOperationException("Finish or synchronize the current presentation before replacing its session.");
            if (session == null) throw new ArgumentNullException(nameof(session));
            RequireSceneBindings();
            if (session.State.Duel.Players.Count != Players.Count || session.State.Duel.Enemies.Count != Enemies.Count)
                throw new ArgumentException("The scene actors must match the session's roster slots.", nameof(session));
            if (UsesXmlContent) ValidateContentRoster(ReadContentEncounter(), session);
        }
        void IBattleSceneAdapter.SessionAttached() => Publish();
        private void Bind()
        {
            actions.Hand.StopSession();
            match.StartExternalMatch(playerActor, State.Table, State.Table.Cards.Select(card => Style(card)));
            var deck = PlayerZone(CardZoneKinds.Deck).Id; var discard = PlayerZone(CardZoneKinds.Discard).Id;
            actions.Hand.Bind(match, PlayerZone(CardZoneKinds.Hand).Id, deck, discard, actions.Hand.GetComponent<HandZoneView>().FacingCamera);
            actions.Hand.BeginSession();
            actions.Hand.GetComponent<HandZoneView>().SetMaxSelectedCards(playerActor.Rules.MaxSelection);
            // Rebinding an observer also restores the selected ordering for an already dealt hand.
            actions.Hand.RequestSortAfterLanding(actions.Hand.SortOrder);
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
            Interaction.BeginCommit();
            var result = Playback.Submit(actor.ParticipantId, CardDuelAction.EndTurn, animate: animate);
            if (!result.Accepted) { candidateKey = null; RefreshCandidate(); }
            return result.Accepted;
        }
        internal int ExecutePlayerPlay(PlayerBattleActor actor, CardSelectionRequest request, bool animate)
        {
            if (actor != playerActor || !playerActor.CanPlay(request)) return 0;
            RefreshCandidate(); Interaction.BeginCommit();
            var result = Playback.Submit(actor.ParticipantId, CardDuelAction.Play, request.Cards, animate: animate);
            if (!result.Accepted) { candidateKey = null; RefreshCandidate(); }
            return result.Accepted ? request.Cards.Count : 0;
        }
        internal int ExecutePlayerDiscard(PlayerBattleActor actor, CardSelectionRequest request, bool animate)
        {
            if (actor != playerActor || !playerActor.CanDiscard(request)) return 0;
            RefreshCandidate(); Interaction.BeginCommit();
            var result = Playback.Submit(actor.ParticipantId, CardDuelAction.Discard, request.Cards, animate: animate);
            if (!result.Accepted) { candidateKey = null; RefreshCandidate(); }
            return result.Accepted ? request.Cards.Count : 0;
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
        private void Awake() => EnsureSceneHost();
        private void OnEnable()
        {
            if (!Application.isPlaying || State == null || match == null || actions == null) return;
            SynchronizeBattle(); Bind(); Project(false); Publish();
        }
        private void OnDisable() { if (State != null) SnapToState(); }
    }
}
