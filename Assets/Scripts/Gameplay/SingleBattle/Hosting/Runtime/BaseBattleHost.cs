using System;
using PokerEngine;
using UnityEngine;

namespace Poker.BattleHosting
{
    /// <summary>Thin Unity adapter for the pure BattleHost; subclasses supply scene bindings and presentation.</summary>
    public abstract class BaseBattleHost : MonoBehaviour
    {
        private BattleHost lifecycle;
        public BattleHost Lifecycle
        {
            get
            {
                if (lifecycle == null)
                {
                    lifecycle = new BattleHost(CreateValidatedSession);
                    lifecycle.SessionAttached += OnSessionAttached;
                }
                return lifecycle;
            }
        }
        public BattleSession Session => lifecycle?.Session;
        public BattleSessionPlayback Playback => lifecycle?.Playback;
        public IBattleSessionCommands Commands => lifecycle?.Commands;
        public CardDuelEngine Engine => Session?.Engine;
        public CardDuelState AuthorityState => lifecycle?.AuthorityState;

        protected abstract IBattleSessionPresentation BattlePresentation { get; }
        protected abstract BattleSession CreateSession();
        protected virtual bool CanStartBattle => true;
        protected virtual bool AnimateOpening => Application.isPlaying;

        public void StartBattle()
        {
            if (CanStartBattle) Lifecycle.StartBattle(RequirePresentation(), AnimateOpening);
        }
        public void RestartBattle()
        {
            if (CanStartBattle) Lifecycle.RestartBattle(RequirePresentation(), AnimateOpening);
        }
        public void AttachSession(BattleSession session)
        {
            ValidateSession(session);
            Lifecycle.AttachSession(session, RequirePresentation());
        }
        public void AttachCommands(IBattleSessionCommands commands, BattleSessionResult opening = null, bool animate = true)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            ValidateSession(commands.Session);
            Lifecycle.AttachCommands(commands, RequirePresentation(), opening, animate);
        }
        public void SynchronizeBattle() => lifecycle?.Synchronize();

        protected virtual void ValidateSession(BattleSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
        }
        protected virtual void OnSessionAttached() { }

        private BattleSession CreateValidatedSession()
        {
            var session = CreateSession();
            ValidateSession(session);
            return session;
        }
        private IBattleSessionPresentation RequirePresentation() =>
            BattlePresentation ?? throw new InvalidOperationException("Bind the battle presentation first.");
    }
}
