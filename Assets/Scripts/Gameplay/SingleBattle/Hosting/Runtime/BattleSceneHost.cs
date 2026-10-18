using System;
using System.Linq;
using PokerEngine;
using UnityEngine;

namespace Poker.BattleHosting
{
    /// <summary>One component per independent battle. Knows no cards, actors, UI, camera or timeline implementation.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-150)]
    public sealed class BattleSceneHost : BaseBattleHost
    {
        [SerializeField, Tooltip("Implements IBattleSessionSource. Creates an undealt session.")]
        private MonoBehaviour sessionSource;
        [SerializeField, Tooltip("Implements IBattleSceneAdapter. Binds input and presentation to this battle.")]
        private MonoBehaviour sceneAdapter;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool animateOpening = true;
        public MonoBehaviour SessionSource => sessionSource;
        public MonoBehaviour SceneAdapter => sceneAdapter;
        public bool AutoStart => autoStart;
        public bool IsPresenting => sceneAdapter is IBattleSceneAdapter adapter && adapter.IsPresenting;
        public event Action SessionChanged;

        public void Configure(MonoBehaviour source, MonoBehaviour adapter, bool startAutomatically = true, bool animate = true)
        {
            if (Session != null) throw new InvalidOperationException("Configure scene services before attaching a battle session.");
            ValidateService(source, typeof(IBattleSessionSource));
            ValidateService(adapter, typeof(IBattleSceneAdapter));
            ((IBattleSceneAdapter)adapter).BindHost(this);
            sessionSource = source; sceneAdapter = adapter; autoStart = startAutomatically; animateOpening = animate;
        }
        private void ValidateService(MonoBehaviour component, Type contract)
        {
            if (component == null || !contract.IsInstanceOfType(component))
                throw new InvalidOperationException("BattleSceneHost requires " + contract.Name + ".");
            if (component.gameObject.scene != gameObject.scene ||
                component.transform != transform && !component.transform.IsChildOf(transform) ||
                component.GetComponentInParent<BattleSceneHost>(true) != this)
                throw new InvalidOperationException("Battle services must belong to this BattleSceneHost hierarchy.");
        }
        private IBattleSceneAdapter RequireAdapter()
        {
            ValidateService(sceneAdapter, typeof(IBattleSceneAdapter));
            var adapter = (IBattleSceneAdapter)sceneAdapter;
            adapter.BindHost(this);
            return adapter;
        }
        protected override IBattleSessionPresentation BattlePresentation => RequireAdapter();
        protected override bool CanStartBattle => isActiveAndEnabled && RequireAdapter() != null &&
            sceneAdapter.isActiveAndEnabled && !IsPresenting;
        protected override bool AnimateOpening => animateOpening && Application.isPlaying;
        protected override BattleSession CreateSession()
        {
            ValidateService(sessionSource, typeof(IBattleSessionSource));
            return ((IBattleSessionSource)sessionSource).CreateSession();
        }
        protected override void ValidateSession(BattleSession session)
        {
            base.ValidateSession(session);
            RequireAdapter().ValidateSession(session);
        }
        protected override void OnSessionAttached()
        {
            RequireAdapter().SessionAttached();
            SessionChanged?.Invoke();
        }
        private void Start()
        {
            // Allows a migrated scene adapter to install its services in Awake first.
            if (autoStart && Session == null) StartBattle();
        }
        private void OnEnable()
        {
            if (Application.isPlaying && Session != null && Playback != null) SynchronizeBattle();
        }
        private void OnDisable()
        {
            if (Session != null && Playback != null) SynchronizeBattle();
        }
    }
}
