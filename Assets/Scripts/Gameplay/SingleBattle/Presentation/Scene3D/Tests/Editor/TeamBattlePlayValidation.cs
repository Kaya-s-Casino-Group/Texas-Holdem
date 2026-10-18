using System;
using System.IO;
using System.Linq;
using CardPresentation;
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEngine;

namespace Poker.SingleBattle.Tests
{
    /// <summary>Focused live validation through existing HUD callbacks, waiting for actual animation completion.</summary>
    public static class TeamBattlePlayValidation
    {
        private static SingleBattleSceneAdapter host;
        private static SingleBattleHud hud;
        private static double deadline;
        private static int stage, attacks;
        private static ParticipantId firstPlayer;
        private static CardInstanceId[] firstHand;
        public static void Run()
        {
            if (!Application.isPlaying || host != null) throw new InvalidOperationException("请在多人 Demo 的 Play 中运行，等待上次验证完成。");
            var candidate = UnityEngine.Object.FindObjectsOfType<SingleBattleSceneAdapter>().Single();
            if (candidate.Players.Count != 2 || candidate.Enemies.Count != 3) throw new InvalidOperationException("此验证需要 2v3 阵容。");
            host = candidate; hud = UnityEngine.Object.FindObjectsOfType<SingleBattleHud>().Single();
            host.SnapToState(); host.StartBattle(); stage = 0; attacks = 0;
            foreach (var actor in host.Enemies) actor.ActionPresented += CountAttack;
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
        }
        private static void CountAttack() => attacks++;
        private static void Tick()
        {
            try
            {
                if (!Application.isPlaying || host == null) throw new InvalidOperationException("验证被退出 Play 中断。");
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("多人回合演出未完成。");
                if (host.IsPresenting || host.Actions.Hand.GetComponent<HandZoneView>().IsTransitioning) return;
                switch (stage)
                {
                    case 0:
                        firstPlayer = host.Player.ParticipantId;
                        firstHand = host.PlayerZone(CardZoneKinds.Hand).Cards.ToArray();
                        Assert.That(hud.SwitchPlayerButton, Is.Not.Null); hud.SwitchPlayerButton.onClick.Invoke(); stage++; break;
                    case 1:
                        Assert.That(host.Player.ParticipantId, Is.Not.EqualTo(firstPlayer));
                        Assert.That(host.Match.Commands, Is.SameAs(host.Player));
                        Assert.That(host.PlayerZone(CardZoneKinds.Hand).Cards.Intersect(firstHand), Is.Empty);
                        hud.EndTurnButton.onClick.Invoke(); stage++; break;
                    case 2:
                        Assert.That(attacks, Is.Zero); Assert.That(host.State.Duel.CompletedRounds, Is.Zero);
                        Assert.That(host.Player.ParticipantId, Is.EqualTo(firstPlayer));
                        CollectionAssert.AreEqual(firstHand, host.PlayerZone(CardZoneKinds.Hand).Cards);
                        hud.EndTurnButton.onClick.Invoke(); stage++; break;
                    default:
                        Assert.That(attacks, Is.EqualTo(3)); Assert.That(host.State.Duel.CompletedRounds, Is.EqualTo(1));
                        Assert.That(host.State.Duel.Players.All(p => !p.HasEnded && p.Energy.Energy == p.Rules.MaxEnergy), Is.True);
                        foreach (var zone in host.State.Table.Zones) CollectionAssert.AreEquivalent(zone.Cards, host.Match.State.GetZone(zone.Id).Cards);
                        Finish(null); break;
                }
            }
            catch (Exception error) { Finish(error); }
        }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            if (host != null)
            {
                foreach (var actor in host.Enemies) if (actor != null) actor.ActionPresented -= CountAttack;
                if (Application.isPlaying) { host.SnapToState(); host.StartBattle(); }
            }
            host = null; hud = null;
            Directory.CreateDirectory("Library/SingleBattleTests");
            File.WriteAllText("Library/SingleBattleTests/team-play.json", JsonUtility.ToJson(new Report { status = error == null ? "Passed" : "Failed", error = error?.ToString() }, true));
            if (error == null) Debug.Log("多人战局通过：切换玩家、独立手牌、全员结束、三怪依次实际演出、整轮恢复与牌区一致。");
            else Debug.LogException(error);
        }
        [Serializable] private sealed class Report { public string status, error; }
    }
}
