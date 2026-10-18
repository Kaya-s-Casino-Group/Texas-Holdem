using System;
using Poker.BattleHosting;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleSceneAdapter
    {
        [SerializeField] private BattleSceneHost sceneHost;
        public BattleSceneHost SceneHost => EnsureSceneHost();
        public BattleSession Session => (sceneHost != null ? sceneHost : GetComponent<BattleSceneHost>())?.Session;
        public BattleSessionPlayback Playback => (sceneHost != null ? sceneHost : GetComponent<BattleSceneHost>())?.Playback;
        public CardDuelEngine Engine => Session?.Engine;
        public CardDuelState AuthorityState => Session?.State;
        public BattleHost Lifecycle => SceneHost.Lifecycle;

        public BattleSceneHost EnsureSceneHost()
        {
            if (sceneHost == null) sceneHost = GetComponent<BattleSceneHost>();
            // Compatibility for already serialized scenes. The editor migration saves this component explicitly.
            if (sceneHost == null) sceneHost = gameObject.AddComponent<BattleSceneHost>();
            if (sceneHost.SceneAdapter == null) sceneHost.Configure(this, this);
            else if (sceneHost.SceneAdapter != this) throw new InvalidOperationException("This battle already has a different scene adapter.");
            return sceneHost;
        }
        void IBattleSceneAdapter.BindHost(BattleSceneHost owner)
        {
            if (owner == null || owner.gameObject != gameObject || sceneHost != null && sceneHost != owner)
                throw new InvalidOperationException("The card scene adapter belongs to exactly one co-located BattleSceneHost.");
            sceneHost = owner;
        }
        // Compatibility forwarding for existing buttons and integrations; no session is owned here.
        public void StartBattle() => SceneHost.StartBattle();
        public void RestartBattle() => SceneHost.RestartBattle();
        public void AttachSession(BattleSession session) => SceneHost.AttachSession(session);
        public void SynchronizeBattle() { if (Session != null) SceneHost.SynchronizeBattle(); }
    }
}
