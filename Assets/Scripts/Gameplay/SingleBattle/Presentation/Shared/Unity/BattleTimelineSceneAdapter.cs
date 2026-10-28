using System;
using System.Collections.Generic;
using System.Linq;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using Poker.BattleHosting;
using PokerEngine;
using UnityEngine;

namespace Poker.BattlePresentation
{
    /// <summary>Scene-independent bridge. Usable with an empty timeline, or any configured timeline actions.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleTimelineSceneAdapter : MonoBehaviour, IBattleSceneAdapter
    {
        [SerializeField] private ScenePresentationDirector director;
        [SerializeField, Tooltip("Empty selects the first player in each new session.")]
        private string observerId;
        [SerializeField, Tooltip("Scene-authored placement data. Optional for card-only frontends.")]
        private BattleSceneLayout sceneLayout;
        [SerializeField] private BattleUnitLayoutOverride[] layoutOverrides = Array.Empty<BattleUnitLayoutOverride>();
        private BattleLayoutFrontend layoutFrontend;
        [SerializeField] private CameraSceneConfiguration cameraScene;
        [SerializeField] private CameraPresentation cameraPresent;
        public CameraSceneConfiguration CameraScene => cameraScene;
        public CameraPresentation CameraPresent => cameraPresent;
        public void ConfigureCamera(CameraSceneConfiguration configuration, CameraPresentation executor)
        {
            if (host != null && host.Session != null) throw new InvalidOperationException("Configure scene camera before attaching a session.");
            if (configuration == null || executor == null || configuration.gameObject.scene != gameObject.scene ||
                executor.gameObject.scene != gameObject.scene || executor.transform != transform && !executor.transform.IsChildOf(transform))
                throw new ArgumentException("Bind a scene camera configuration and an executor belonging to this Battle.");
            configuration.Validate(); cameraScene = configuration; cameraPresent = executor;
        }
        public void ValidateCameraConfiguration()
        {
            if (cameraScene == null && cameraPresent == null) return;
            if (cameraScene == null || cameraPresent == null || cameraScene.gameObject.scene != gameObject.scene ||
                cameraPresent.transform != transform && !cameraPresent.transform.IsChildOf(transform) ||
                cameraPresent.GetComponentInParent<BattleTimelineSceneAdapter>(true) != this)
                throw new InvalidOperationException("Camera scene configuration and executor must be explicitly bound to this Battle bridge.");
            cameraPresent.ValidateSceneBindings(cameraScene);
        }
        public void BindCameraPresentation()
        {
            ValidateCameraConfiguration();
            if (cameraPresent != null) cameraPresent.BindScene(cameraScene, director);
        }
        public BattleSceneLayout SceneLayout => sceneLayout;
        public BattleLayoutBindings LayoutBindings => layoutFrontend?.Bindings;
        public IReadOnlyList<BattleUnitLayoutOverride> LayoutOverrides => Array.AsReadOnly(layoutOverrides);
        private BattleSceneHost host;
        private BattlePresentationBridge bridge;
        private readonly List<CardInstanceId> selected = new List<CardInstanceId>();
        private readonly InteractionSession interaction = new InteractionSession();
        public BattleSceneHost Host => host;
        public ScenePresentationDirector Director => director;
        public BattleDisplaySnapshot Display => director != null ? director.Display.Current : null;
        public CandidatePresentationModel Candidate => interaction.Current;
        public bool IsPresenting => director != null && director.IsPresenting;
        public ParticipantId ObserverId => string.IsNullOrEmpty(observerId) ? host.Session.State.Duel.PlayerId : new ParticipantId(observerId);
        public bool CanAct => host != null && host.isActiveAndEnabled && isActiveAndEnabled && director != null &&
            director.isActiveAndEnabled && host.Playback != null && host.Playback.CanAct(ObserverId);
        public event Action<CandidatePresentationModel> CandidateChanged;

        public void ConfigureLayout(BattleSceneLayout layout, BattleUnitLayoutOverride[] placements = null)
        {
            if (host != null && host.Session != null) throw new InvalidOperationException("Configure layout before attaching a session.");
            if (layout != null)
            {
                if (layout.gameObject.scene != gameObject.scene) throw new ArgumentException("Layout must belong to the same scene.");
                layout.Validate();
            }
            layoutFrontend?.Dispose(); layoutFrontend = null; bridge = null;
            sceneLayout = layout; layoutOverrides = placements?.ToArray() ?? Array.Empty<BattleUnitLayoutOverride>();
        }

        public void Configure(ScenePresentationDirector frontend, string observer = null)
        {
            if (host != null && host.Session != null) throw new InvalidOperationException("Configure the frontend before attaching a session.");
            if (frontend == null || frontend.gameObject.scene != gameObject.scene ||
                frontend.transform != transform && !frontend.transform.IsChildOf(transform))
                throw new ArgumentException("Bind a director within this adapter hierarchy.");
            if (director != null) director.PlaybackFinished -= RefreshCandidate;
            layoutFrontend?.Dispose(); layoutFrontend = null;
            director = frontend; observerId = observer; bridge = null;
            director.PlaybackFinished += RefreshCandidate;
        }
        public void BindHost(BattleSceneHost owner)
        {
            if (owner == null || host != null && host != owner)
                throw new InvalidOperationException("Each scene adapter belongs to exactly one battle.");
            host = owner;
        }
        public void ValidateSession(BattleSession session)
        {
            if (session == null || director == null) throw new InvalidOperationException("Bind a session and presentation director.");
            if (director.GetComponentInParent<BattleSceneHost>(true) != host)
                throw new InvalidOperationException("The director must belong to this battle.");
            if (!string.IsNullOrEmpty(observerId) && !session.State.Duel.Players.Any(player => player.Id.Value == observerId))
                throw new InvalidOperationException("The observer must identify a player in this session.");
            ValidateCameraConfiguration();
            if (sceneLayout != null)
            {
                if (sceneLayout.gameObject.scene != gameObject.scene) throw new InvalidOperationException("Layout must belong to the same scene.");
                BattleLayoutBindings.Resolve(sceneLayout, BattleDisplayProjector.Build(session.State, session.Engine,
                    string.IsNullOrEmpty(observerId) ? session.State.Duel.PlayerId : new ParticipantId(observerId)), layoutOverrides);
            }
        }
        private BattlePresentationBridge Bridge
        {
            get
            {
                if (bridge != null) return bridge;
                ValidateSession(host?.Session);
                director.PlaybackFinished -= RefreshCandidate;
                director.PlaybackFinished += RefreshCandidate;
                IBattlePresentationFrontend frontend = director;
                if (sceneLayout != null) frontend = layoutFrontend = new BattleLayoutFrontend(director, sceneLayout, layoutOverrides);
                bridge = new BattlePresentationBridge(() => host.Engine, () => ObserverId, frontend);
                bridge.BeforeInitialize = _ => { CloseInteraction(); BindCameraPresentation(); };
                bridge.BeforePresent = _ => CloseInteraction();
                bridge.BeforeSynchronize = _ => CloseInteraction();
                return bridge;
            }
        }
        void IBattleSessionPresentation.Initialize(CardDuelState state) => Bridge.Initialize(state);
        void IBattleSessionPresentation.Present(BattleSessionResult result, bool animate) => Bridge.Present(result, animate);
        void IBattleSessionPresentation.Synchronize(CardDuelState state) => Bridge.Synchronize(state);
        public void SessionAttached() => RefreshCandidate();
        public void SelectCards(IEnumerable<string> ids)
        {
            if (!CanAct) return;
            var next = (ids ?? Array.Empty<string>()).Select(id => new CardInstanceId(id)).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
            selected.Clear(); selected.AddRange(next); RefreshCandidate();
        }
        public BattleSessionResult PlaySelected(bool animate = true) => SubmitAction(CardDuelAction.Play, selected.ToArray(), animate: animate);
        public BattleSessionResult DiscardSelected(bool animate = true) => SubmitAction(CardDuelAction.Discard, selected.ToArray(), animate: animate);
        public BattleSessionResult EndTurn(bool animate = true) => SubmitAction(CardDuelAction.EndTurn, animate: animate);

        /// <summary>Forwards a player intent, including manual draw and target bindings, to the rule authority.</summary>
        public BattleSessionResult SubmitAction(CardDuelAction action, IEnumerable<CardInstanceId> cards = null,
            IEnumerable<ParticipantId> targets = null, BattleTargetBindings targetBindings = null, int drawCount = 0, bool animate = true)
        {
            if (!CanAct) throw new InvalidOperationException("This battle is not accepting player input.");
            var submitted = (cards ?? Array.Empty<CardInstanceId>()).ToArray();
            interaction.BeginCommit();
            var result = host.Playback.Submit(ObserverId, action, submitted, targets, animate, targetBindings, drawCount);
            if (!result.Accepted) RefreshCandidate();
            return result;
        }
        private void CloseInteraction() { selected.Clear(); interaction.Close(); CandidateChanged?.Invoke(Candidate); }
        private void RefreshCandidate()
        {
            if (host == null || host.Session == null) return;
            var state = host.AuthorityState;
            var version = interaction.BeginRequest(state.Table.MatchId.ToString(), ObserverId.Value, state.Revision, selected.Select(id => id.Value));
            interaction.Apply(BattleInteractionQueries.Query(state, host.Engine, ObserverId, selected, CanAct, version));
            CandidateChanged?.Invoke(Candidate);
        }
        private void OnDisable()
        {
            CloseInteraction();
            if (host != null && host.Playback != null) host.SynchronizeBattle();
        }
        private void OnEnable() { if (host != null && host.Playback != null) RefreshCandidate(); }
        private void OnDestroy()
        {
            layoutFrontend?.Dispose();
            if (director != null) director.PlaybackFinished -= RefreshCandidate;
            interaction.Dispose();
        }
    }
}
