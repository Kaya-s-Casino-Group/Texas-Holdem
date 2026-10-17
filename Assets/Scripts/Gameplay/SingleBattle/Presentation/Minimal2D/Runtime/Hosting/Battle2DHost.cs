using System;
using System.Linq;
using PokerEngine;
using Poker.BattleHosting;
using Poker.SingleBattle;
using UnityEngine;

namespace Poker.Battle2DValidation
{
    /// <summary>Minimal scene composition/input adapter. Every action goes through the shared session/playback port.</summary>
    public sealed class Battle2DHost : BaseBattleHost
    {
        [SerializeField] private BattleRulesProfile rules;
        [SerializeField] private PlayerBattleProfile player;
        [SerializeField] private EnemyBattleProfile enemy;
        [SerializeField] private Battle2DView view;
        public Battle2DView View => view;
        public ParticipantId PlayerId => Session.State.Duel.PlayerId;

        public void Configure(BattleRulesProfile battleRules, PlayerBattleProfile playerProfile,
            EnemyBattleProfile enemyProfile, Battle2DView presentation)
        {
            rules = battleRules != null ? battleRules : throw new ArgumentNullException(nameof(battleRules));
            player = playerProfile != null ? playerProfile : throw new ArgumentNullException(nameof(playerProfile));
            enemy = enemyProfile != null ? enemyProfile : throw new ArgumentNullException(nameof(enemyProfile));
            view = presentation != null ? presentation : throw new ArgumentNullException(nameof(presentation));
            view.BindCommands(this);
        }
        protected override IBattleSessionPresentation BattlePresentation => view;
        protected override bool AnimateOpening => false;
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
        protected override void OnSessionAttached() => view.BindCommands(this);
        public bool CanPlay => CanPreview(CardDuelAction.Play);
        public bool CanDiscard => CanPreview(CardDuelAction.Discard);
        public bool CanEndTurn => Playback != null && Playback.CanAct(PlayerId);
        private bool CanPreview(CardDuelAction action)
        {
            if (Playback == null) return false;
            return (action == CardDuelAction.Play
                ? Playback.PreviewPlay(PlayerId, view.Selected)
                : Playback.PreviewDiscard(PlayerId, view.Selected))?.Accepted == true;
        }
        public BattleSessionResult PlaySelected() => Submit(CardDuelAction.Play);
        public BattleSessionResult DiscardSelected() => Submit(CardDuelAction.Discard);
        public BattleSessionResult EndTurn() => Submit(CardDuelAction.EndTurn);
        private BattleSessionResult Submit(CardDuelAction action)
        {
            if (Session == null) throw new InvalidOperationException("Start or attach a session first.");
            var selected = action == CardDuelAction.EndTurn ? Array.Empty<CardInstanceId>() : view.Selected.ToArray();
            var result = Playback.Submit(PlayerId, action, selected, animate: true);
            if (!result.Accepted) view.ShowRejected(result.Error);
            return result;
        }
        private void Start() { if (Session == null) StartBattle(); }
    }
}
