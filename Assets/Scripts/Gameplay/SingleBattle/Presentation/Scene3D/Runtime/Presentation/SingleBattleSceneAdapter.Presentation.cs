using CardInteraction;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CardPresentation;
using PokerEngine;
using Poker.BattlePresentation;
using UnityEngine;
using global::BattlePresentation;
using global::BattlePresentation.Unity;

namespace Poker.SingleBattle
{
    /// <summary>Scene adapter: rules enter only through the shared bridge; scheduling consumes neutral facts.</summary>
    public sealed partial class SingleBattleSceneAdapter
    {
        [SerializeField] private ScenePresentationDirector presentationDirector;
        private BattlePresentationBridge bridge;
        private bool busy;
        private bool instant;
        /// <summary>Optional coroutine scheduler for deterministic frontend tests or another Unity execution loop.</summary>
        private Func<IEnumerator, Action> presentationRoutine;
        public Func<IEnumerator, Action> StartPresentationRoutine
        {
            get => presentationRoutine;
            set {
                presentationRoutine = value;
                foreach (var action in GetComponentsInChildren<PresentationActionBehaviour>(true))
                    action.StartPresentationRoutine = value;
            }
        }
        private BattleSessionResult pendingPresentation;
        private readonly Dictionary<string, ShieldSnapshot> shieldFrames = new Dictionary<string, ShieldSnapshot>();
        private readonly List<string> feedback = new List<string>();
        private InteractionSession interaction;
        private string candidateKey;
        public BattleSessionResult LastResolution { get; private set; }
        public BattleDisplaySnapshot Display => presentationDirector != null ? presentationDirector.Display.Current : null;
        public InteractionSession Interaction => interaction ?? (interaction = new InteractionSession(
            GetComponent<SceneCandidateFeedback>() ?? gameObject.AddComponent<SceneCandidateFeedback>()));
        public CandidatePresentationModel Candidate => Interaction.Current;
        public string DisplayFeedback => string.Join("\n", feedback);
        public string ContentDiagnosticsText => ContentDiagnostics.Count == 0 ? "" : "【XML 配置诊断】\n" +
            string.Join("\n", ContentDiagnostics.Select(d => d.Code + " · " + d.Location.Line + ":" + d.Location.Column + "\n" + d.Location.Path + "\n" + d.Message)) + "\n\n";
        public ScenePresentationDirector PresentationDirector => EnsureBridgeAndDirector();
        private ScenePresentationDirector EnsureBridgeAndDirector()
        {
            if (bridge != null) return presentationDirector;
            if (presentationDirector == null) presentationDirector = GetComponent<ScenePresentationDirector>();
            if (presentationDirector == null || !presentationDirector.HasConfiguredTimeline)
                throw new InvalidOperationException("Configure the scene presentation timeline before starting this battle.");
            BattleRecoveryTimeline.Upgrade(this, presentationDirector);
            BattleEffectTimeline.Upgrade(this, presentationDirector);
            BattleTurnBannerTimeline.Upgrade(this, presentationDirector);
            presentationDirector.Configure(null);
            presentationDirector.Display.Changed += DisplayChanged;
            presentationDirector.PlaybackFinished += FinishPresentation;
            bridge = new BattlePresentationBridge(() => Engine, () => playerActor.ParticipantId, presentationDirector);
            bridge.BeforeInitialize = InitializeScene;
            bridge.BeforePresent = batch => {
                GetComponent<BattleCardEffectBridge>()?.Prepare(batch, playerActor.ParticipantId);
                pendingPresentation = batch; LastResolution = batch; busy = true; feedback.Clear(); shieldFrames.Clear();
                Interaction.Close(); candidateKey = null;
                var first = batch.Steps[0];
                if (first.Action == CardDuelAction.Play) { LastPlayResolution = first.Transition; LastPlayedBy = first.Actor; }
                if (first.Action == CardDuelAction.Discard) { LastDiscardResolution = first.Transition; LastDiscardedBy = first.Actor; }
            };
            bridge.BeforeSynchronize = state => {
                GetComponent<BattleCardEffectBridge>()?.Clear();
                Interaction.Close(); candidateKey = null;
                // Reconstruct presentation-only shield attribution from already-resolved facts, never run rules.
                if (pendingPresentation != null) RecordAllShields(pendingPresentation);
                PresentedState = state;
                ReconcileActors();
                foreach (var actor in Actors) if (actor != null) actor.CancelPresentation();
                if (actions != null && actions.Hand != null) actions.Hand.Presentation.Synchronize(actions.Hand.Presentation.Current);
            };
            return presentationDirector;
        }
        private void InitializeScene(CardDuelState state)
        {
            GetComponent<BattleCardEffectBridge>()?.Clear();
            busy = false; pendingPresentation = null; LastResolution = null; shieldFrames.Clear(); feedback.Clear();
            Interaction.Close(); candidateKey = null; PresentedState = state;
            LastScore = null; LastDamage = 0; LastPlayResolution = null; LastDiscardResolution = null;
            LastPlayedBy = LastDiscardedBy = default;
            var limits = state.Duel.Player(state.Duel.PlayerId).Rules;
            Message = "出牌 1–" + limits.MaxPlayCards + " 张 · 弃牌 1–" + limits.MaxDiscardCards + " 张 · 能量用尽或手动结束回合";
            BindActors(); Bind();
            presentationDirector.Bind("cards", actions.Hand.Presentation);
            foreach (var actor in Actors) presentationDirector.Bind(actor.ParticipantId.Value, actor);
        }
        void IBattleSessionPresentation.Initialize(CardDuelState state) { EnsureBridgeAndDirector(); bridge.Initialize(state); }
        void IBattleSessionPresentation.Present(BattleSessionResult batch, bool animate)
        {
            EnsureBridgeAndDirector(); instant = !animate || !Application.isPlaying && StartPresentationRoutine == null || !isActiveAndEnabled || !presentationDirector.isActiveAndEnabled;
            try { bridge.Present(batch, !instant); }
            finally { instant = false; }
        }
        void IBattleSessionPresentation.Synchronize(CardDuelState state)
        { EnsureBridgeAndDirector(); bridge.Synchronize(state); }
        private void DisplayChanged(DisplayUpdate update)
        {
            PresentedState = bridge.StateAt(update.Current.Cursor);
            if (PresentedState == null || actions == null || actions.Hand == null || match == null) return;
            ReconcileActors();
            if (update.IsSynchronization || instant)
            {
                if (pendingPresentation != null) {
                    RecordAllShields(pendingPresentation);
                    feedback.Clear();
                    foreach (var revealed in pendingPresentation.Timeline)
                        if (revealed.RuleEvent != null) feedback.AddRange(BattlePresentationRuleText.Events(new[] { revealed.RuleEvent }, revealed.State.Duel, Engine.Duel.Definition.StatusCatalog));
                }
                foreach (var actor in Actors) actor.SynchronizeDisplay(update.Current.Unit(actor.ParticipantId.Value), true);
                actions.Hand.Presentation.Synchronize(actions.Hand.Presentation.Current);
                Project(false); Publish(); return;
            }
            if (presentationDirector.TryGetChange(update.Current.Cursor, out var fact))
            {
                if (fact.Effect != null) {
                    var effectActor = Actors.FirstOrDefault(actor => actor.ParticipantId.Value == fact.Effect.TargetId);
                    if (effectActor != null) RecordShield(fact.Values["effectKey"], fact, effectActor);
                }
                if (fact.Values.TryGetValue("feedback", out var text) && !string.IsNullOrWhiteSpace(text)) feedback.Add(text);
                if (fact.SemanticKey == "ActionResolved" && pendingPresentation != null && fact.Values.TryGetValue("stepIndex", out var index))
                {
                    var step = pendingPresentation.Steps[int.Parse(index)];
                    if (step.Transition.Score != null) {
                        LastScore = step.Transition.Score; LastDamage = OpponentDamage(step.Transition);
                        Message = BattleDisplayProjector.HandName(LastScore.Category) + " · " + LastScore.Total + " 分 → " + LastDamage + " 伤害";
                    }
                }
                if (fact.SemanticKey == "CardsMoved" || fact.SemanticKey == "CardsDealt" || fact.SemanticKey == "DeckRecycled")
                {
                    bool discarded = fact.SemanticKey == "CardsMoved" && fact.Values["action"] == "Discard";
                    Project(true, discarded ? fact.CardIds : null);
                }
            }
            foreach (var actor in Actors) actor.SynchronizeDisplay(update.Current.Unit(actor.ParticipantId.Value));
            Publish();
        }
        private void FinishPresentation()
        {
            if (presentationDirector == null || presentationDirector.IsPresenting) return;
            if (pendingPresentation != null) {
                PresentedState = pendingPresentation.State;
                UpdateBatchSummary(pendingPresentation);
                RecordAllShields(pendingPresentation);
                pendingPresentation = null;
            }
            busy = false;
            if (State != null && match != null && actions != null && actions.Hand != null && ReferenceEquals(match.Commands, playerActor))
            {
                Project(false);
                // Start the final sort in this handoff, so the decision window includes its animation.
                if (Application.isPlaying && !instant) actions.Hand.TryCompletePendingSort();
            }
            if (presentationDirector.LastError != null) Debug.LogError("Battle presentation: " + presentationDirector.LastError, this);
            candidateKey = null; RefreshCandidate(); Publish();
        }
        private void UpdateBatchSummary(BattleSessionResult batch)
        {
            var scored = batch.Steps.LastOrDefault(step => step.Transition.Score != null);
            if (scored == null) return;
            LastScore = scored.Transition.Score;
            LastDamage = OpponentDamage(scored.Transition);
            Message = BattleDisplayProjector.HandName(LastScore.Category) + " · " + LastScore.Total + " 分 → " + LastDamage + " 伤害";
        }
        private long OpponentDamage(CardDuelTransition transition)
        {
            var enemies = new HashSet<ParticipantId>(transition.State.Duel.Enemies.Select(enemy => enemy.Id));
            return transition.Events.Where(effect => enemies.Contains(effect.Effect.Target)).Sum(effect => effect.HealthLost);
        }
        private ShieldSnapshot RecordShield(string key, BattleDisplayChange fact, BattleActor actor)
        {
            if (shieldFrames.TryGetValue(key, out var snapshot)) return snapshot;
            ShieldCard[] cards = null;
            if (fact.Values.TryGetValue("action", out var action) && action == "Discard" && fact.Effect.Kind == "GainBlock")
                cards = fact.CardIds.Select(id => fact.Before.Zones.SelectMany(z => z.Cards).FirstOrDefault(c => c.CardId == id))
                    .Where(c => c != null).Select(c => new ShieldCard(c.CardId, c.ArtKey)).ToArray();
            return shieldFrames[key] = actor.RecordDisplayBlock(key, fact.Effect, cards);
        }
        private void RecordAllShields(BattleSessionResult batch)
        {
            foreach (var fact in batch.Timeline)
            {
                var effect = fact.RuleEvent as CombatEvent; if (effect == null) continue;
                var actor = Actors.FirstOrDefault(a => a.ParticipantId == effect.Effect.Target); if (actor == null) continue;
                string key = "rule/" + fact.ActionId + "/" + effect.Revision + "/" + effect.SequenceIndex;
                // Use the same stable key for animated and recovery paths.
                if (shieldFrames.ContainsKey(key)) continue;
                var data = new EffectDisplayData(effect.Effect.Kind.ToString(), effect.Effect.Source.Value, effect.Effect.Target.Value,
                    effect.SettledAmount, effect.HealthLost, effect.BlockAbsorbed,
                    BattleDisplayProjector.Unit(effect.Before, fact.State.Duel), BattleDisplayProjector.Unit(effect.After, fact.State.Duel), effect.Died);
                var cards = fact.Action == CardDuelAction.Discard && effect.Effect.Kind == CombatEffectKind.GainBlock ? fact.Selected
                    .Select(id => new ShieldCard(id.Value, BattleDisplayProjector.StandardArt(Engine.Face(fact.State.Table.GetCard(id))))).ToArray() : null;
                shieldFrames[key] = actor.RecordDisplayBlock(key, data, cards);
            }
        }
        private void Project(bool animate, IEnumerable<string> discarded = null)
        {
            // CardMatchHost disables before this host. Keep display recovery independent of a stopped mirror;
            // OnEnable rebinds it and projects the latest snapshot after the scene becomes active again.
            if (match == null || !match.IsRunning) return;
            var old = actions.Hand.Cards.ToDictionary(card => card.Card.Id);
            var zones = State.Table.Zones.Select(zone => zone.Id != PlayerZone(CardZoneKinds.Hand).Id ? zone : new CardZone(zone.Id, zone.Kind, zone.ControllerId,
                actions.Hand.Cards.Select(card => card.Card.Id).Where(zone.Cards.Contains).Concat(zone.Cards.Where(id => !old.ContainsKey(id))))).ToArray();
            var styles = State.Table.Cards.Select(card => Style(card, old.TryGetValue(card.Id, out var before) &&
                PlayerZone(CardZoneKinds.Hand).Cards.Contains(card.Id) ? before.Interaction : default)).ToArray();
            var update = match.PrepareExternalProjection(playerActor, new CardTableState(State.Table.MatchId, State.Table.Cards, zones, State.Revision), styles);
            actions.Hand.PresentProjection(update, discarded, animate);
            overview?.Refresh(); drawPile?.Refresh();
            if (PlayerZone(CardZoneKinds.Hand).Cards.Any(id => !old.ContainsKey(id))) actions.Hand.RequestSortAfterLanding(actions.Hand.SortOrder);
        }
        public void RefreshCandidate()
        {
            if (State == null || actions == null || actions.Hand == null || !actions.Hand.IsRunning || playerActor == null) return;
            var selected = actions.Hand.CaptureSelection().Cards.OrderBy(x => x.Value, StringComparer.Ordinal).ToArray();
            var targetBindings = ResolvePlayTargetBindings(selected);
            bool allowed = CanReceivePlayerInput;
            string key = State.Table.MatchId + "/" + State.Revision + "/" + playerActor.ParticipantId + "/" + allowed + "/" + string.Join(",", selected.Select(x => x.Value)) + "/" + PlayTargetSelectionKey;
            if (key == candidateKey) return; candidateKey = key;
            long version = Interaction.BeginRequest(State.Table.MatchId.ToString(), playerActor.ParticipantId.Value, State.Revision, selected.Select(x => x.Value));
            Interaction.Apply(BattleInteractionQueries.Query(State, Engine, playerActor.ParticipantId, selected, allowed, version, targetBindings: targetBindings));
        }
        private void LateUpdate() => RefreshCandidate();
        public void SnapToState() => SynchronizeBattle();
    }
}
