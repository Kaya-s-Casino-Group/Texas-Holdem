using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using Poker.BattleHosting;
using Poker.BattlePresentation;
using UnityEngine;

namespace Poker.Battle2DValidation
{
    /// <summary>Rules and command adapter. The view receives only public display and interaction data.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BattleSceneHost))]
    public sealed class Battle2DSceneAdapter : MonoBehaviour, IBattleSceneAdapter
    {
        private BattleSceneHost host;
        public BattleSceneHost SceneHost => host != null ? host : GetComponent<BattleSceneHost>();
        public BattleSession Session => SceneHost.Session;
        public BattleSessionPlayback Playback => SceneHost.Playback;
        public CardDuelState AuthorityState => SceneHost.AuthorityState;
        public bool IsPresenting => view != null && view.IsPresenting;
        [SerializeField] private Battle2DView view;
        private BattlePresentationBridge bridge;
        private Battle2DView boundView;
        private readonly List<CardInstanceId> selected = new List<CardInstanceId>();
        private readonly BattleTargetSelection playTargets = new BattleTargetSelection();
        public IReadOnlyList<BattleTargetChoice> PlayTargetChoices => playTargets.Choices;
        public Battle2DView View => view;
        public ParticipantId PlayerId => Session.State.Duel.PlayerId;
        public bool CanPlay { get; private set; }
        public bool CanDiscard { get; private set; }
        public bool CanEndTurn { get; private set; }

        public void Configure(Battle2DView presentation)
        {
            if (Session != null) throw new InvalidOperationException("Configure the view before attaching a session.");
            view = presentation != null ? presentation : throw new ArgumentNullException(nameof(presentation));
            BindView();
        }
        public void BindHost(BattleSceneHost owner)
        {
            if (owner == null || owner.gameObject != gameObject || host != null && host != owner)
                throw new InvalidOperationException("The 2D adapter belongs to one co-located BattleSceneHost.");
            host = owner;
        }
        public void ValidateSession(BattleSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (view == null || view.GetComponentInParent<BattleSceneHost>(true) != SceneHost ||
                view.SceneDirector.GetComponentInParent<BattleSceneHost>(true) != SceneHost)
                throw new InvalidOperationException("Bind the 2D view and director within this battle.");
            if (!view.SceneDirector.HasConfiguredTimeline)
                throw new InvalidOperationException("Configure the 2D scene timeline before starting a battle.");
            if (session.State.Duel.Players.Count != 1)
                throw new ArgumentException("The minimal validation UI supports one player.", nameof(session));
        }
        public void SessionAttached() { playTargets.Clear(); BindView(); RefreshInteraction(); }
        void IBattleSessionPresentation.Initialize(CardDuelState state) => GetBridge().Initialize(state);
        void IBattleSessionPresentation.Present(BattleSessionResult result, bool animate) => GetBridge().Present(result, animate);
        void IBattleSessionPresentation.Synchronize(CardDuelState state) => GetBridge().Synchronize(state);
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
            view.BindCommands(ToggleSelection, () => PlaySelected(), () => DiscardSelected(), () => EndTurn(), () => SceneHost.RestartBattle());
        }
        private void ClearSelection() { selected.Clear(); CanPlay = CanDiscard = CanEndTurn = false; }
        private void CloseInteraction()
        {
            selected.Clear(); CanPlay = CanDiscard = CanEndTurn = false;
            view.CloseInteraction();
        }
        private void ToggleSelection(string id)
        {
            if (!CanReceiveInput) return;
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
            CanEndTurn = CanReceiveInput;
            var candidate = BattleInteractionQueries.Query(Session.State, Session.Engine, PlayerId, selected,
                CanEndTurn, view.NextSelectionVersion,
                playTargets.Reconcile(Session.State, Session.Engine, PlayerId, selected));
            CanPlay = candidate.CanPlay; CanDiscard = candidate.CanDiscard;
            view.SetInteraction(candidate, CanEndTurn);
            view.SetTargets(playTargets.Choices, CanEndTurn, SelectPlayTarget);
        }
        public bool SelectPlayTarget(string slot, string participantId)
        {
            if (!CanReceiveInput) return false;
            playTargets.Reconcile(Session.State, Session.Engine, PlayerId, selected);
            if (!playTargets.Select(slot, participantId)) return false;
            RefreshInteraction(); return true;
        }
        public BattleSessionResult PlaySelected() => Submit(CardDuelAction.Play);
        public BattleSessionResult DiscardSelected() => Submit(CardDuelAction.Discard);
        public BattleSessionResult EndTurn() => Submit(CardDuelAction.EndTurn);
        private BattleSessionResult Submit(CardDuelAction action)
        {
            if (Session == null) throw new InvalidOperationException("Start or attach a session first.");
            if (!isActiveAndEnabled || !SceneHost.isActiveAndEnabled || !view.isActiveAndEnabled || !view.SceneDirector.isActiveAndEnabled)
                throw new InvalidOperationException("This battle is not accepting input.");
            var intent = action == CardDuelAction.EndTurn ? Array.Empty<CardInstanceId>() : selected.ToArray();
            var targets = action == CardDuelAction.Play ? playTargets.Reconcile(Session.State, Session.Engine, PlayerId, intent) : null;
            view.BeginCommit();
            var result = Playback.Submit(PlayerId, action, intent,
                BattleInteractionQueries.LegacyTargets(Session.State, PlayerId, targets), animate: true,
                targetBindings: BattleInteractionQueries.EffectBindings(Session.State, PlayerId, targets));
            if (!result.Accepted) { RefreshInteraction(); view.ShowRejected(result.Error.ToString()); }
            return result;
        }
        private bool CanReceiveInput => isActiveAndEnabled && SceneHost.isActiveAndEnabled && view != null &&
            view.isActiveAndEnabled && view.SceneDirector.isActiveAndEnabled && Playback != null && Playback.CanAct(PlayerId);
        private void OnEnable() { if (Session != null && view != null) { BindView(); RefreshInteraction(); } }
        private void OnDisable()
        {
            if (view == null) return;
            CloseInteraction();
            if (Playback != null) SceneHost.SynchronizeBattle();
        }
        private void OnDestroy()
        {
            if (boundView == null) return;
            boundView.DisplayChanged -= RefreshInteraction;
            boundView.InteractionClosed -= ClearSelection;
            boundView.PresentationFinished -= RefreshInteraction;
            boundView.BindCommands(null, null, null, null, null);
        }
    }
}
