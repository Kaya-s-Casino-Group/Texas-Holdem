using System;
using System.IO;
using System.Linq;
using CardInteraction;
using CardPresentation;
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEngine;

namespace Poker.SingleBattle.Tests
{
    public static class SingleBattleXmlPlayValidation
    {
        [MenuItem("工具/卡牌战斗/XML内容/验证当前运行场景")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("请先运行 SingleBattleDemoNew 场景。");
            var host = UnityEngine.Object.FindObjectsOfType<SingleBattleHost>().Single();
            var report = new Report();
            try
            {
                Assert.That(host.UsesXmlContent, Is.True);
                Assert.That(host.State, Is.Not.Null);
                host.SnapToState();
                var hand = host.Actions.Hand;
                var view = hand.GetComponent<HandZoneView>();
                view.CompleteTransitions();
                var hud = UnityEngine.Object.FindObjectsOfType<SingleBattleHud>().Single();
                hud.Refresh();
                Assert.That(host.Player.IsBound, Is.True);
                Assert.That(host.CanEndTurn, Is.True);
                Assert.That(hand.Cards.Count, Is.EqualTo(8));
                Assert.That(hud.RuleFeedbackText, Does.Contain("当前状态"));
                report.openingFeedback = hud.RuleFeedbackText;
                int health = host.Player.Unit.Health;
                int enemyHealth = host.Enemy.Unit.Health;
                SelectOne(hand, view);
                Assert.That(host.Player.Play(hand.CaptureSelection(), false), Is.EqualTo(1));
                Assert.That(host.Enemy.Unit.Health, Is.LessThan(enemyHealth));
                Assert.That(hand.Cards.Count, Is.EqualTo(8));
                Assert.That(host.Player.Turn.Energy, Is.EqualTo(2));
                SelectOne(hand, view);
                Assert.That(host.Player.Discard(hand.CaptureSelection(), false), Is.EqualTo(1));
                Assert.That(host.Player.Unit.Block, Is.EqualTo(8));
                Assert.That(host.Player.Turn.Energy, Is.EqualTo(1));
                Assert.That(host.PlayerDiscards, Is.EqualTo(2));
                Assert.That(hand.Cards.Count, Is.EqualTo(8));
                view.CompleteTransitions();
                Assert.That(host.EndTurn(false), Is.True);
                Assert.That(host.State.Duel.CompletedRounds, Is.EqualTo(1));
                Assert.That(host.Player.Unit.Health, Is.EqualTo(health - 232));
                Assert.That(host.Player.Turn.Energy, Is.EqualTo(3));
                Assert.That(host.Player.Unit.Block, Is.Zero);
                Assert.That(host.PlayerDiscards, Is.EqualTo(2));
                Assert.That(host.State, Is.SameAs(host.AuthorityState));
                hud.Refresh(); report.finalFeedback = hud.RuleFeedbackText;
                Assert.That(report.finalFeedback, Does.Contain("伤害"));
                report.status = "Passed";
                Debug.Log("XML 场景验证通过：真实开战、出弃费用与补牌、弃盾、敌人结算、状态同步及 HUD 文字。", host);
            }
            catch (Exception error) { report.status = "Failed"; report.error = error.ToString(); Debug.LogException(error); }
            finally
            {
                Directory.CreateDirectory("Library/SingleBattleTests");
                File.WriteAllText("Library/SingleBattleTests/xml-scene.json", JsonUtility.ToJson(report, true));
                if (report.status == "Passed") host.RestartBattle();
            }
        }

        private static void SelectOne(CardZonePlayHost hand, HandZoneView view)
        {
            foreach (var card in hand.Cards) hand.SetInteraction(card.Id, default);
            hand.SetInteraction(hand.Cards[0].Id, new CardInteractionState(isSelected: true));
            hand.Publish(); view.CompleteTransitions();
        }
        [Serializable] private sealed class Report { public string status, error, openingFeedback, finalFeedback; }
    }
}
