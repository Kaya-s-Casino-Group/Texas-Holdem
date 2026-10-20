using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RetroRendering;
using CardInteraction;
using CardPresentation;
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Poker.SingleBattle.Tests
{
    /// <summary>Real UI/animation integration; uses runtime-only rule overrides to exercise both terminal paths.</summary>
    public static class SingleBattlePlayValidation
    {
        private static SingleBattleSceneAdapter host;
        private static TextAsset originalContent;
        private static string originalEncounter;
        private static BattleTestContent testContent;
        private static int stage, automaticPlays;
        private static long playerHealth, enemyHealth, enemyDamage;
        private static int maxEnergy, initialEnergy, playEnergyCost, discardEnergyCost;
        private static long scoreRevision;
        private static double deadline;
        private static bool sawEnemyTurn;
        private static Vector3 enemyRestScale;
        private static bool dealingOnly;
        private static CardDuelRefillTiming savedPlayRefill, savedDiscardRefill;

        public static void RunDealing()
        {
            Run(); if (host == null) return;
            dealingOnly = true;
            savedPlayRefill = GetTiming("playRefillTiming"); savedDiscardRefill = GetTiming("discardRefillTiming");
            Set("initialEnergy", 3); Set("playEnergyCost", 1); Set("discardEnergyCost", 1);
            SetTiming("playRefillTiming", CardDuelRefillTiming.AfterAction);
            SetTiming("discardRefillTiming", CardDuelRefillTiming.NextPlayerTurn);
            host.StartBattle(); stage = -8;
        }
        private static CardDuelRefillTiming GetTiming(string field) => (CardDuelRefillTiming)Get(field);
        private static void SetTiming(string field, CardDuelRefillTiming value) => Set(field, (int)value);

        public static void Run()
        {
            if (!Application.isPlaying || host != null) throw new InvalidOperationException("Enter Play and finish any previous validation first.");
            var candidate = Object.FindObjectsOfType<SingleBattleSceneAdapter>().Single(item => item.State != null);
            if (candidate.Players.Count != 1 || candidate.Enemies.Count != 1) throw new InvalidOperationException("此项验证需要 1v1 的 SingleBattleDemo 场景。");
            host = candidate; originalContent = host.ContentXml; originalEncounter = host.ContentEncounterId;
            testContent = new BattleTestContent(host, originalContent.text, originalEncounter);
            playerHealth = testContent.GetLong("playerHealth"); enemyHealth = testContent.GetLong("enemyHealth"); enemyDamage = testContent.GetLong("enemyDamage");
            maxEnergy = Get("maxEnergy"); initialEnergy = Get("initialEnergy"); playEnergyCost = Get("playEnergyCost"); discardEnergyCost = Get("discardEnergyCost");
            enemyRestScale = host.Enemy.Visual.localScale;
            try
            {
                host.SnapToState(); Set("enemyHealth", 10000);
                Set("maxEnergy", 3); Set("initialEnergy", 5); Set("playEnergyCost", 3); Set("discardEnergyCost", 1);
                host.StartBattle(); Assert.That(host.State.Table.Cards.Count, Is.EqualTo(52));
                Assert.That(host.State.Duel.PlayerTurn.Energy, Is.EqualTo(5)); Assert.That(host.State.Duel.PlayerTurn.MaxEnergy, Is.EqualTo(3));
                Assert.That(host.Match.Commands, Is.SameAs(host.Player)); Assert.That(host.Player.IsBound, Is.True); Assert.That(host.Enemy.IsBound, Is.True);
                stage = -5; sawEnemyTurn = false; deadline = EditorApplication.timeSinceStartup + 120; EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish(error); }
        }
        private static int Get(string field) => testContent.Get(field);
        private static void Set(string field, int value) => testContent.Set(field, value);
        private static void Select()
        {
            foreach (var card in host.Actions.Hand.Cards) host.Actions.Hand.SetInteraction(card.Id, default);
            host.Actions.Hand.SetInteraction(host.Actions.Hand.Cards[0].Id, new CardInteractionState(isSelected: true));
            host.Actions.Hand.Publish(); host.Actions.Hand.GetComponent<HandZoneView>().CompleteTransitions(); host.Actions.Refresh();
        }
        private static void Play()
        {
            Select(); Assert.That(host.Actions.CanPlay, Is.True); host.Actions.Panel.PlayButton.onClick.Invoke();
            scoreRevision = host.State.Revision; host.Actions.Panel.PlayButton.onClick.Invoke();
            Assert.That(host.State.Revision, Is.EqualTo(scoreRevision)); Assert.That(host.Actions.CanPlay, Is.False);
        }
        private static void Tick()
        {
            try
            {
                if (!Application.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Single battle UI validation timed out.");
                if (host.State.Duel.Phase == ScoredDuelPhase.AwaitingEnemyAction) sawEnemyTurn = true;
                if (host.IsPresenting || host.Actions.Hand.GetComponent<HandZoneView>().IsTransitioning) return;
                var hud = Object.FindObjectOfType<SingleBattleHud>();
                Assert.That(host.Player.Unit, Is.SameAs(host.State.Duel.Combat.Get(host.State.Duel.PlayerId)));
                Assert.That(host.Enemy.Unit, Is.SameAs(host.State.Duel.Combat.Get(host.State.Duel.EnemyId)));
                AssertCracks(host.Player); AssertCracks(host.Enemy);
                foreach (var zone in host.State.Table.Zones) CollectionAssert.AreEquivalent(zone.Cards, host.Match.State.GetZone(zone.Id).Cards);
                if (dealingOnly) { TickDealing(); return; }
                if (stage == -5)
                {
                    Assert.That(hud.EnergyText, Does.Contain("5 / 3")); Select();
                    host.Actions.Panel.DiscardButton.onClick.Invoke(); Assert.That(host.State.Duel.PlayerTurn.Energy, Is.EqualTo(4)); stage = -4;
                }
                else if (stage == -4)
                {
                    Assert.That(hud.EnergyText, Does.Contain("4 / 3")); Assert.That(host.State.Duel.CompletedRounds, Is.Zero);
                    Play(); stage = -3;
                }
                else if (stage == -3)
                {
                    Assert.That(host.State.Duel.PlayerTurn.Energy, Is.EqualTo(1)); Assert.That(host.State.Duel.CompletedRounds, Is.Zero);
                    Select(); Assert.That(host.Actions.CanPlay, Is.False); Assert.That(host.Actions.CanDiscard, Is.True);
                    host.Actions.Panel.DiscardButton.onClick.Invoke(); stage = -6;
                }
                else if (stage == -6)
                {
                    Assert.That(host.State.Duel.CompletedRounds, Is.EqualTo(1)); Assert.That(host.State.Duel.PlayerTurn.Energy, Is.EqualTo(3));
                    Assert.That(host.Player.Unit.Health, Is.EqualTo(playerHealth - enemyDamage));
                    RestoreEnergy(); host.StartBattle(); sawEnemyTurn = false; stage = -2;
                }
                else if (stage == -2)
                {
                    // Opening deal can finish without a StateChanged event; the end button must still unlock.
                    if (!hud.EndTurnButton.interactable) return;
                    Assert.That(host.State.Duel.Scores, Is.Empty); hud.EndTurnButton.onClick.Invoke(); stage = -1;
                }
                else if (stage == -1)
                {
                    Assert.That(host.State.Duel.CompletedRounds, Is.EqualTo(1)); Assert.That(host.State.Duel.Scores, Is.Empty);
                    Assert.That(host.State.Duel.PlayerTurn.Energy, Is.EqualTo(3));
                    host.StartBattle(); Select(); host.Actions.Panel.DiscardButton.onClick.Invoke();
                    Assert.That(host.IsPresenting, Is.True); Assert.That(host.State.DiscardsRemaining, Is.EqualTo(2));
                    Assert.That(host.Actions.CanPlay, Is.False); stage = 0;
                }
                else if (stage == 0)
                {
                    Assert.That(host.Actions.Hand.Cards.Count, Is.EqualTo(8)); Assert.That(host.Actions.Hand.DiscardedCards.Count, Is.EqualTo(1));
                    Assert.That(host.State.Duel.PlayerTurn.Energy, Is.EqualTo(2));
                    Assert.That(hud.EndTurnButton.interactable, Is.True); Assert.That(hud.EnergyText, Does.Contain("2 / 3"));
                    Play(); stage = 1;
                }
                else if (stage == 1)
                {
                    Assert.That(host.State.Duel.CompletedRounds, Is.Zero); Assert.That(host.State.Duel.PlayerTurn.Energy, Is.EqualTo(1));
                    Assert.That(host.State.Duel.Combat.Get(host.State.Duel.PlayerId).Health, Is.EqualTo(playerHealth));
                    Assert.That(host.Actions.Hand.Cards.Count, Is.EqualTo(7)); Assert.That(host.PlayerZone(CardZoneKinds.Played).Cards, Is.Empty);
                    Assert.That(hud.EndTurnButton.interactable, Is.True); Assert.That(hud.EnergyText, Does.Contain("1 / 3"));
                    Assert.That(host.State.Duel.Combat.Get(host.State.Duel.EnemyId).Health, Is.LessThan(10000));
                    hud.EndTurnButton.onClick.Invoke(); long revision = host.State.Revision;
                    hud.EndTurnButton.onClick.Invoke(); Assert.That(host.State.Revision, Is.EqualTo(revision));
                    Assert.That(host.IsPresenting, Is.True); Assert.That(hud.EndTurnButton.interactable, Is.False);
                    stage = 2;
                }
                else if (stage == 2)
                {
                    Assert.That(sawEnemyTurn, Is.True); Assert.That(host.State.Duel.CompletedRounds, Is.EqualTo(1));
                    Assert.That(host.State.Duel.PlayerTurn.Energy, Is.EqualTo(3));
                    Assert.That(host.State.Duel.Combat.Get(host.State.Duel.PlayerId).Health, Is.EqualTo(playerHealth - enemyDamage));
                    Assert.That(host.Actions.Hand.Cards.Count, Is.EqualTo(8)); Assert.That(host.PlayerZone(CardZoneKinds.Played).Cards, Is.Empty);
                    var pile = Object.FindObjectsOfType<DrawPilePresenter>().Single(item => item.Source == host.Actions.Hand); pile.Refresh();
                    Assert.That(pile.View.RemainingCount.text, Is.EqualTo(host.State.Zone(CardZoneKinds.Deck).Cards.Count.ToString()));
                    ExecuteEvents.Execute(pile.View.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
                    Assert.That(pile.Overview.View.IsOpen, Is.True); Assert.That(pile.Overview.ReadData().Cards.Count, Is.EqualTo(52));
                    pile.Overview.Close(false); long revision = host.State.Revision;
                    host.Actions.Panel.SuitSortButton.onClick.Invoke(); Assert.That(host.State.Revision, Is.EqualTo(revision));
                    automaticPlays = 1; Play(); stage = 3;
                }
                else if (stage == 3)
                {
                    if (automaticPlays < 3)
                    {
                        Assert.That(host.State.Duel.CompletedRounds, Is.EqualTo(1));
                        Assert.That(host.State.Duel.PlayerTurn.Energy, Is.EqualTo(3 - automaticPlays));
                        Assert.That(host.State.Duel.Combat.Get(host.State.Duel.PlayerId).Health, Is.EqualTo(playerHealth - enemyDamage));
                        Assert.That(host.Actions.Hand.Cards.Count, Is.EqualTo(8 - automaticPlays));
                        automaticPlays++; Play();
                    }
                    else
                    {
                        Assert.That(host.State.Duel.CompletedRounds, Is.EqualTo(2)); Assert.That(host.State.Duel.Scores.Count, Is.EqualTo(4));
                        Assert.That(host.State.Duel.PlayerTurn.Energy, Is.EqualTo(3)); Assert.That(host.Actions.Hand.Cards.Count, Is.EqualTo(8));
                        Assert.That(host.State.Duel.Combat.Get(host.State.Duel.PlayerId).Health, Is.EqualTo(playerHealth - 2 * enemyDamage));
                        hud.EndTurnButton.onClick.Invoke(); stage = 4;
                    }
                }
                else if (stage == 4)
                {
                    Assert.That(host.State.Duel.CompletedRounds, Is.EqualTo(3)); Assert.That(host.State.Duel.Scores.Count, Is.EqualTo(4));
                    Assert.That(host.State.Duel.PlayerTurn.Energy, Is.EqualTo(3));
                    Set("enemyHealth", 1); Set("enemyDamage", 100); host.SnapToState(); host.StartBattle(); Play(); stage = 5;
                }
                else if (stage == 5)
                {
                    Assert.That(host.State.Duel.Phase, Is.EqualTo(ScoredDuelPhase.Won)); Assert.That(hud.ResultPanel.activeSelf, Is.True);
                    Assert.That(host.State.Duel.Combat.Get(host.State.Duel.PlayerId).Health, Is.EqualTo(playerHealth));
                    Assert.That(host.State.Duel.PlayerTurn.Energy, Is.EqualTo(2)); Assert.That(hud.EndTurnButton.interactable, Is.False);
                    Assert.That(host.Enemy.IsAlive, Is.False);
                    var presenter = host.Enemy.Presentation as BattleActorPresenter;
                    Assert.That(presenter, Is.Not.Null);
                    Assert.That(host.Enemy.Visual.localScale, Is.EqualTo(presenter.UsesDeathTimeline ? enemyRestScale : enemyRestScale * .75f));
                    if (presenter.UsesDeathTimeline)
                    {
                        var vat = host.Enemy.Visual.GetComponentInChildren<RigidVATPlayer>();
                        Assert.That(vat.Progress, Is.EqualTo(1)); Assert.That(vat.IsPlaying, Is.False);
                    }
                    Assert.That(host.Actions.CanPlay, Is.False); Assert.That(host.Actions.CanDiscard, Is.False);
                    Set("enemyHealth", 10000); Set("playerHealth", 1); Set("enemyDamage", 7);
                    hud.RestartButton.onClick.Invoke(); Assert.That(host.State.Revision, Is.EqualTo(1));
                    Assert.That(host.Enemy.Visual.localScale, Is.EqualTo(enemyRestScale)); Assert.That(host.Enemy.IsAlive, Is.True);
                    var restartedVat = host.Enemy.Visual.GetComponentInChildren<RigidVATPlayer>();
                    if (restartedVat != null) { Assert.That(restartedVat.Progress, Is.Zero); Assert.That(restartedVat.IsPlaying, Is.False); }
                    Assert.That(host.Actions.CanPlay, Is.False); Assert.That(host.CanEndTurn, Is.False); stage = 6;
                }
                else if (stage == 6)
                {
                    if (!hud.EndTurnButton.interactable) return;
                    hud.EndTurnButton.onClick.Invoke(); stage = 7;
                }
                else
                {
                    Assert.That(host.State.Duel.Phase, Is.EqualTo(ScoredDuelPhase.Lost)); Assert.That(hud.ResultPanel.activeSelf, Is.True);
                    Assert.That(host.State.Duel.Combat.Get(host.State.Duel.PlayerId).Health, Is.Zero); Assert.That(host.Actions.CanPlay, Is.False);
                    Assert.That(host.State.Duel.Scores, Is.Empty); Assert.That(hud.EndTurnButton.interactable, Is.False);
                    Finish(null);
                }
            }
            catch (Exception error) { Finish(error); }
        }
        private static void TickDealing()
        {
            if (stage == -8 || stage == -12) { Play(); stage--; }
            else if (stage == -9 || stage == -13)
            {
                Assert.That(host.Actions.Hand.Cards.Count, Is.EqualTo(stage == -9 ? 8 : 7));
                Assert.That(host.State.Duel.PlayerTurn.Energy, Is.EqualTo(2)); Assert.That(host.State.Duel.CompletedRounds, Is.Zero);
                Select(); host.Actions.Panel.DiscardButton.onClick.Invoke(); stage--;
            }
            else if (stage == -10)
            {
                Assert.That(host.Actions.Hand.Cards.Count, Is.EqualTo(7)); Assert.That(host.State.Duel.PlayerTurn.Energy, Is.EqualTo(1));
                Assert.That(host.EndTurn(), Is.True); stage = -11;
            }
            else if (stage == -11)
            {
                Assert.That(host.Actions.Hand.Cards.Count, Is.EqualTo(8)); Assert.That(host.State.Duel.CompletedRounds, Is.EqualTo(1));
                SetTiming("playRefillTiming", CardDuelRefillTiming.NextPlayerTurn);
                SetTiming("discardRefillTiming", CardDuelRefillTiming.AfterAction);
                host.StartBattle(); stage = -12;
            }
            else
            {
                Assert.That(host.Actions.Hand.Cards.Count, Is.EqualTo(8)); Assert.That(host.State.Duel.PlayerTurn.Energy, Is.EqualTo(1));
                Assert.That(host.State.Duel.CompletedRounds, Is.Zero); Finish(null);
            }
        }
        private static void AssertCracks(BattleActor actor)
        {
            if (actor.Presentation is BattleActorPresenter visual)
            {
                var audio = visual.Audio as BattleActorAudio; Assert.That(audio, Is.Not.Null);
                foreach (var kind in new[] { BattlePresentationKind.Attack, BattlePresentationKind.Hit, BattlePresentationKind.Death })
                {
                    Assert.That(audio.GetSettings(kind).Clip, Is.Null); // Reserved demo slots remain silent.
                    var source = audio.GetSource(kind); Assert.That(source, Is.Not.Null);
                    Assert.That(source.playOnAwake, Is.False); Assert.That(source.loop, Is.False); Assert.That(source.isPlaying, Is.False);
                }
            }
            if (!(actor.Presentation is VatBattleActorPresenter presenter) || presenter.Cracks == null) return;
            float health = (float)actor.Unit.Health / actor.Unit.MaxHealth;
            int expectedStage = health <= .25f ? 3 : health <= .5f ? 2 : health <= .75f ? 1 : 0;
            Assert.That(presenter.Cracks.CurrentStage, Is.EqualTo(expectedStage));
            foreach (var renderer in actor.Visual.GetComponentsInChildren<Renderer>())
            {
                var material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty("_CrackProgress")) continue;
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_CrackProgress"), Is.EqualTo(presenter.Cracks.CurrentProgress));
                Assert.That(block.GetFloat("_VATProgress"), Is.EqualTo(actor.IsAlive ? 0 : 1));
            }
        }
        private static void Finish(Exception error)
        {
            bool wasDealing = dealingOnly;
            EditorApplication.update -= Tick;
            Directory.CreateDirectory("Library/SingleBattleTests");
            File.WriteAllText("Library/SingleBattleTests/" + (dealingOnly ? "dealing.json" : "play.json"), JsonUtility.ToJson(new Report { status = error == null ? "Passed" : "Failed", error = error?.ToString() }, true));
            if (host != null && Application.isPlaying)
            {
                if (dealingOnly) { SetTiming("playRefillTiming", savedPlayRefill); SetTiming("discardRefillTiming", savedDiscardRefill); }
                host.ConfigureContent(originalContent, originalEncounter); host.SnapToState(); host.StartBattle();
            }
            testContent?.Dispose(); testContent = null;
            host = null; dealingOnly = false;
            if (error != null) Debug.LogException(error); else Debug.Log(wasDealing ? "SingleBattle dealing passed: immediate/deferred play and discard, actual draw animations, input lock and turn-start refill." :
                "SingleBattle UI passed: player energy, several plays per turn, automatic/manual/zero-play end, one enemy action, refill, input lock, overview, win/loss and restart.");
        }
        private static void RestoreEnergy()
        {
            Set("maxEnergy", maxEnergy); Set("initialEnergy", initialEnergy);
            Set("playEnergyCost", playEnergyCost); Set("discardEnergyCost", discardEnergyCost);
        }
        [Serializable] private sealed class Report { public string status, error; }
    }
}
