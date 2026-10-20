using System;
using PokerEngine;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleSceneAdapter
    {
        public bool CanEditBattle => CanReceiveItemInput && CardDuelEngine.CanApplyAdmin(AuthorityState);

        public BattleSessionResult EditBattle(BattleAdminDraft draft, bool preview)
        {
            if (draft == null || Session == null) throw new InvalidOperationException("请先读取当前对局。");
            if (!draft.IsCurrent(Session)) throw new InvalidOperationException("对局已变化，请重新读取当前状态后修改。");
            if (!CanEditBattle) throw new InvalidOperationException("请等待玩家行动阶段及动画结算结束；终局不能修改。");
            var patch = draft.Build();
            if (preview) return Session.PreviewAdmin(draft.Revision, patch);
            // Keep the presentation/candidate/actors synchronized through the existing authority bridge.
            return Playback.ApplyAdmin(draft.Revision, patch, animate: false);
        }
    }
}
