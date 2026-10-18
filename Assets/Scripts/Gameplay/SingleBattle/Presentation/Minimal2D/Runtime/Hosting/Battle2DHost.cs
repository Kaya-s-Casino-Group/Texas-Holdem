using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using Poker.BattleHosting;
using Poker.BattlePresentation;
using Poker.SingleBattle;
using UnityEngine;

namespace Poker.Battle2DValidation
{
    /// <summary>Rules and command adapter. The view receives only public display and interaction data.</summary>
    public sealed class Battle2DHost : BaseBattleHost
    {
        [SerializeField] private BattleRulesProfile rules;
        [SerializeField] private PlayerBattleProfile player;
        [SerializeField] private EnemyBattleProfile enemy;
        [SerializeField] private Battle2DView view;
        private BattlePresentationBridge bridge;
        private Battle2DView boundView;
        private readonly List<CardInstanceId> selected = new List<CardInstanceId>();
        public Battle2DView View => view;
        public ParticipantId PlayerId => Session.State.Duel.PlayerId;
        public BattlePresentationBridge PresentationBridge => GetBridge();
        public bool CanPlay { get; private set; }
        public bool CanDiscard { get; private set; }
        public bool CanEndTurn { get; private set; }

        public void Configure(BattleRulesProfile battleRules, PlayerBattleProfile playerProfile,
            EnemyBattleProfile enemyProfile, Battle2DView presentation)
        {
            rules = battleRules != null ? battleRules : throw new ArgumentNullException(nameof(battleRules));
            player = playerProfile != null ? playerProfile : throw new ArgumentNullException(nameof(playerProfile));
            enemy = enemyProfile != null ? enemyProfile : throw new ArgumentNullException(nameof(enemyProfile));
            view = presentation != null ? presentation : throw new ArgumentNullException(nameof(presentation));
            BindView();
        }
        protected override IBattleSessionPresentation BattlePresentation => GetBridge();
        protected override bool AnimateOpening => false;
        private BattlePresentationBridge GetBridge()
        {
            if (view == null) throw new InvalidOperationException("Bind the 2D view first.");
            BindView();
            if (bridge != null) return bridge;
            bridge = new BattlePresentationBridge(() => Session.Engine, () => PlayerId, view);
            bridge.BeforeInitialize += _ => CloseInteraction();
            bridge.BeforePresent += _ => CloseInteraction();
            bridge.BeforeSynchronize += _ => CloseInteraction();
            return bridge;
        }
        private void BindView()
        {
            if (boundView != view)
            {
                if (boundView != null)
                {
                    boundView.DisplayChanged -= RefreshInteraction;
                    boundView.InteractionClosed -= ClearSelection;
                    boundView.PresentationFinished -= RefreshInteraction;
                    boundView.BindCommands(null, null, null, null, null);
                }
                bridge = null; boundView = view;
                view.DisplayChanged += RefreshInteraction;
                view.InteractionClosed += ClearSelection;
                view.PresentationFinished += RefreshInteraction;
            }
            view.BindCommands(ToggleSelection, () => PlaySelected(), () => DiscardSelected(), () => EndTurn(), StartBattle);
        }
        protected override BattleSession CreateSession()
        {
            if (rules == null || player == null || enemy == null || view == null)
                throw new InvalidOperationException("Bind the validation profiles and 2D view first.");
            return BattleSessionFactory.Create(rules,
                new[] { player.BuildSetup(new ParticipantId("player")) },
                new[] { enemy.BuildSetup(new ParticipantId("enemy")) }, BattleSessionFactory.CreateBattleId("battle-2d"));
        }
        protected override void ValidateSession(BattleSession session)
        {
            if (view == null) throw new InvalidOperationException("Bind the 2D view first.");
            base.ValidateSession(session);
            if (session.State.Duel.Players.Count != 1) throw new ArgumentException("The minimal validation UI supports one player.", nameof(session));
        }
        protected override void OnSessionAttached() { BindView(); RefreshInteraction(); }
        private void ClearSelection() { selected.Clear(); CanPlay = CanDiscard = CanEndTurn = false; }
        private void CloseInteraction()
        {
            selected.Clear(); CanPlay = CanDiscard = CanEndTurn = false;
            view.CloseInteraction();
        }
        private void ToggleSelection(string id)
        {
            if (Session == null || Playback == null || !Playback.CanAct(PlayerId) || !view.isActiveAndEnabled) return;
            var state = Session.State;
            var instanceId = new CardInstanceId(id);
            if (!state.Zone(CardZoneKinds.Hand, PlayerId).Cards.Contains(instanceId) || state.Table.GetCard(instanceId).IsDisabled) return;
            if (!selected.Remove(instanceId))
            {
                if (selected.Count >= state.Duel.Player(PlayerId).Rules.MaxSelection) return;
                selected.Add(instanceId);
            }
            RefreshInteraction();
        }
        private void RefreshInteraction()
        {
            CanPlay = CanDiscard = CanEndTurn = false;
            if (Session == null) { view.CloseInteraction(); return; }
            var hand = Session.State.Zone(CardZoneKinds.Hand, PlayerId).Cards;
            selected.RemoveAll(card => !hand.Contains(card) || Session.State.Table.GetCard(card).IsDisabled);
            CanEndTurn = Playback != null && view.isActiveAndEnabled && !view.IsPresenting && Playback.CanAct(PlayerId);
            var candidate = BattleInteractionQueries.Query(Session.State, Session.Engine, PlayerId, selected,
                CanEndTurn, view.NextSelectionVersion);
            CanPlay = candidate.CanPlay; CanDiscard = candidate.CanDiscard;
            view.SetInteraction(candidate, CanEndTurn);
        }
        public BattleSessionResult PlaySelected() => Submit(CardDuelAction.Play);
        public BattleSessionResult DiscardSelected() => Submit(CardDuelAction.Discard);
        public BattleSessionResult EndTurn() => Submit(CardDuelAction.EndTurn);
        private BattleSessionResult Submit(CardDuelAction action)
        {
            if (Session == null) throw new InvalidOperationException("Start or attach a session first.");
            var intent = action == CardDuelAction.EndTurn ? Array.Empty<CardInstanceId>() : selected.ToArray();
            view.BeginCommit();
            var result = Playback.Submit(PlayerId, action, intent, animate: true);
            if (!result.Accepted) { RefreshInteraction(); view.ShowRejected(result.Error.ToString()); }
            return result;
        }
        private void Start() { if (Session == null) StartBattle(); }
        private void OnDestroy()
        {
            if (boundView == null) return;
            boundView.DisplayChanged -= RefreshInteraction;
            boundView.InteractionClosed -= ClearSelection;
            boundView.PresentationFinished -= RefreshInteraction;
        }
    }
}
