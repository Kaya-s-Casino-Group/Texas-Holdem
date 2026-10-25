using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CardInteraction;
using Poker.BattlePresentation;
using Poker.SingleBattle;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Poker.Adventure
{
    public sealed partial class AdventureScreen
    {
        private const string BattleTeachingPreference = "Poker.Adventure.BattleTeaching.v1";
        private bool battleTeachingOpen, previousTeachingGate;
        private string teachingRunId;
        private SingleBattleSceneAdapter teachingView;
        private GameObject teachingPreviousFocus;
        private readonly Dictionary<CardZoneMouseInput, bool> teachingInputs = new Dictionary<CardZoneMouseInput, bool>();

        private void RefreshBattleTeaching()
        {
            var run = host?.Adventure;
            if (battleTeachingOpen)
            {
                if (run == null || host.IsAtMenu || run.RunId != teachingRunId || run.Phase != AdventurePhase.Battle)
                { CloseBattleTeaching(); return; }
                if (run.Battle.State.Duel.Phase != ScoredDuelPhase.AwaitingPlayerHand || run.Battle.State.Duel.Actions.Count != 0)
                { CloseBattleTeaching(); return; }
                RenderBattleTeaching();
                return;
            }
            if (PlayerPrefs.GetInt(BattleTeachingPreference, 0) != 0 || run == null || host.IsAtMenu ||
                host.IsBusy || run.Phase != AdventurePhase.Battle || host.BattleView == null) return;
            if (run.VisitedNodeIds.Select(run.Definition.Node).Any(node => node.Kind == AdventureNodeKind.Battle ||
                node.Kind == AdventureNodeKind.Elite || node.Kind == AdventureNodeKind.Boss)) return;
            var battle = run.Battle;
            if (battle == null || !battle.State.HasStarted || battle.State.Duel.IsFinished || battle.State.Duel.Actions.Count != 0 ||
                battle.State.Duel.CompletedRounds != 0 || battle.State.Duel.Phase != ScoredDuelPhase.AwaitingPlayerHand ||
                battle.State.Duel.IsActionBlocked(run.PlayerId) || !battle.State.Duel.Combat.Get(run.PlayerId).IsAlive) return;
            var window = battle.State.FlowFor(run.PlayerId).Window;
            if (window != BattlePlayerWindow.Action && window != BattlePlayerWindow.Draw) return;
            var view = host.BattleView;
            if (view.IsPresenting || view.IsHandGuideOpen || view.Player?.CanInteract != true) return;
            battleTeachingOpen = true; teachingRunId = run.RunId; teachingView = view;
            previousTeachingGate = view.IsHandGuideOpen;
            teachingPreviousFocus = EventSystem.current?.currentSelectedGameObject;
            view.DeckOverview?.Close(false);
            foreach (var input in gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardZoneMouseInput>(true)))
            {
                teachingInputs[input] = input.enabled;
                input.Controller?.CancelPointer(); input.enabled = false;
            }
            view.SetHandGuideOpen(true);
            RenderBattleTeaching();
        }

        private void RenderBattleTeaching()
        {
            var modal = Modal("首次战斗", false);
            RenderBattleHelp(modal, "选牌进攻 · 弃牌防御 · 留意敌人意图", true);
            var skip = DeskButton(modal, "跳过教学", new Rect(.10f, .055f, .32f, .10f), DismissBattleTeaching, size: 25);
            var start = DeskButton(modal, "开始战斗", new Rect(.58f, .055f, .32f, .10f), DismissBattleTeaching, size: 25);
            skip.navigation = TeachingNavigation(start);
            start.navigation = TeachingNavigation(skip);
            EventSystem.current?.SetSelectedGameObject(start.gameObject);
        }

        private static Navigation TeachingNavigation(Button other) => new Navigation {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = other, selectOnRight = other, selectOnUp = other, selectOnDown = other
        };

        private void DismissBattleTeaching()
        {
            PlayerPrefs.SetInt(BattleTeachingPreference, 1); PlayerPrefs.Save();
            CloseBattleTeaching(); Clear(overlay);
        }

        private void CloseBattleTeaching()
        {
            foreach (var pair in teachingInputs) if (pair.Key != null) pair.Key.enabled = pair.Value;
            teachingInputs.Clear();
            if (teachingView != null) teachingView.SetHandGuideOpen(previousTeachingGate);
            if (battleTeachingOpen && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(teachingPreviousFocus != null && teachingPreviousFocus.activeInHierarchy ? teachingPreviousFocus : null);
            teachingPreviousFocus = null;
            teachingView = null; teachingRunId = null; battleTeachingOpen = false;
        }

        private BattleSession HelpBattle()
        {
            var battle = host.Adventure?.Battle;
            if (battle != null) return battle;
            return BattleContentFactory.Create(host.Content, new BattleSetupRequest(host.Definition.StartingEncounterId,
                "help-preview", HmacDeckShuffler.FromSeed(new byte[32]))).Session;
        }

        private void RenderBattleHelp(RectTransform modal, string heading, bool teaching)
        {
            var battle = HelpBattle();
            var actor = battle.State.Duel.PlayerId;
            var player = battle.State.Duel.Player(actor);
            var resolver = new BattleRuleResolver();
            var query = new BattleLimitQuery(player.Rules, player.Relics);
            int maxPlay = resolver.ResolveValue(query, BattleRuleValueKey.MaxPlayCards).Value;
            int actions = resolver.ResolveValue(query, BattleRuleValueKey.ActionsPerTurn).Value;
            int playCost = battle.Engine.Duel.QueryActionCost(battle.State.Duel, actor, ScoredDuelActionKind.ScoredHand).Cost;
            int discardCost = battle.Engine.Duel.QueryActionCost(battle.State.Duel, actor, ScoredDuelActionKind.Discard).Cost;
            string action;
            if (player.Rules.Flow.UsesTurnFlow)
            {
                int discardActions = player.Rules.Flow.DiscardCostsAction ? 1 : 0;
                int discardEnergy = player.Rules.Flow.DiscardCostsAction ? discardCost : 0;
                action = "出牌阶段每回合 " + actions + " 个行动点；出牌消耗 1 个行动点，弃牌消耗 " + discardActions + " 个行动点。";
                if (playCost != 1 || discardEnergy != discardActions || player.Limits.MaxEnergy.Value != actions ||
                    player.Limits.InitialEnergy.Value != actions)
                    action += "另受能量限制：出牌消耗 " + playCost + " 点能量，弃牌消耗 " + discardEnergy + " 点能量。";
            }
            else action = "出牌与弃牌共用本回合能量；出牌消耗 " + playCost + " 点能量，弃牌消耗 " + discardCost + " 点能量。";
            string block = BlockHelp(battle, query, resolver);
            string pair = BattleHandGuideText.BasicRuleLines(HandGuideType.OnePair, false, player,
                battle.Engine.Scoring, battle.Engine.Duel.Definition.StatusCatalog)[0];
            string triple = BattleHandGuideText.BasicRuleLines(HandGuideType.ThreeOfAKind, false, player,
                battle.Engine.Scoring, battle.Engine.Duel.Definition.StatusCatalog)[0];
            string body = "选牌：从手牌中选择最多 " + maxPlay + " 张，组成牌型后出牌。\n" +
                "行动：" + action + "\n" +
                "弃牌：" + DiscardRefillHelp(player) + DiscardRewardHelp(player) + "\n" +
                "格挡（护盾）：先抵挡伤害；" + block + "\n\n" +
                "牌型效果 · 一对：" + pair + "\n三条：" + triple + "\n" +
                "点击牌库旁的牌型指南，先读效果，再看组合与公式。";
            Label(modal, heading, new Rect(.07f, .735f, .86f, .09f), 31, Gold, TextAlignmentOptions.Center);
            var rules = Label(modal, body, new Rect(.09f, teaching ? .20f : .23f, .82f, teaching ? .51f : .48f), 26, Paper);
            rules.alignment = TextAlignmentOptions.TopLeft;
            Label(modal, teaching ? "可随时从主菜单的玩法说明回看。" :
                "前往地图高亮地点；商店可购买道具和改造牌库，休息处恢复生命。旅程自动保存。",
                new Rect(.08f, teaching ? .165f : .09f, .84f, teaching ? .045f : .10f), 22, Muted, TextAlignmentOptions.Center);
        }

        private static string DiscardRefillHelp(BattlePlayerTurn player)
        {
            var modifications = player.Relics.Relics.SelectMany(relic => player.Relics.RulesFor(relic.InstanceId).RefillRules)
                .Where(rule => rule.Trigger == CardDuelDealTrigger.AfterDiscard).ToArray();
            if (modifications.Any(rule => rule.Mode.HasValue || rule.CountDelta != 0)) return "补牌受当前遗物修正，数量以行动预览为准。";
            var policy = player.Rules.DealPolicy as CardDuelDealPolicy;
            return policy?.AfterDiscard == CardDuelRefillTiming.DiscardedCount ? "弃几张补几张（手牌上限与可用牌源内）。" :
                policy?.AfterDiscard == CardDuelRefillTiming.AfterAction ? "弃牌后立即补至手牌上限。" :
                policy?.AfterDiscard == CardDuelRefillTiming.NextPlayerTurn ? "弃牌后等到下次自己的回合补牌。" : "补牌数量按当前规则计算。";
        }

        private static string DiscardRewardHelp(BattlePlayerTurn player)
        {
            if (player.Rules.DiscardReward is ChipBlockRewardRule chips)
                return "基础格挡为弃牌点数合计 × " + chips.Ratio.ToString("0.####", CultureInfo.InvariantCulture) + "，向下取整；遗物可改变结果。";
            if (player.Rules.DiscardReward is PerCardBlockRewardRule cards)
                return "每张弃牌基础获得 " + cards.BlockPerCard + " 格挡；遗物可改变结果。";
            if (player.Rules.DiscardReward is NoDiscardRewardRule) return "当前基础规则不提供弃牌格挡。";
            return "弃牌效果按当前规则和遗物结算。";
        }

        private static string BlockHelp(BattleSession battle, BattleLimitQuery query, BattleRuleResolver resolver)
        {
            if (resolver.ResolveValue(query, BattleRuleValueKey.RetainBlock).Value > 0)
                return "当前保盾效果生效时，剩余护盾跨回合保留；失去该效果后恢复通常清空规则。";
            switch (battle.Engine.Duel.Definition.PlayerBlockExpiry)
            {
                case PlayerBlockExpiry.PlayerTurnStart: return "剩余护盾在下次自己的回合开始时清空，保盾遗物生效时例外。";
                case PlayerBlockExpiry.AfterEnemyPhase: return "剩余护盾在敌人阶段结束后清空，保盾遗物生效时例外。";
                default: return "当前规则下剩余护盾跨回合保留，受伤时仍会消耗。";
            }
        }
    }
}
