#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CardInteraction;
using CardPresentation;
using NUnit.Framework;
using Poker.SingleBattle;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Poker.Adventure.Tests
{
    public sealed class SingleChapterAdventureSceneTests
    {
        private const string AuthoredScene = "Assets/Scenes/SingleChapterAdventure.unity";
        private const string Output = "Library/AdventureValidation";
        private readonly List<string> errors = new List<string>();
        private string temporaryScene;
        private bool previousLogPolicy, previousRunInBackground;
        private AdventureSceneHost host;
        private AdventureScreen screen;

        [UnitySetUp] public IEnumerator LoadIsolatedScene()
        {
            errors.Clear(); previousLogPolicy = LogAssert.ignoreFailingMessages;
            previousRunInBackground = Application.runInBackground; Application.runInBackground = true;
            LogAssert.ignoreFailingMessages = true; Application.logMessageReceived += CaptureError;
            temporaryScene = "Assets/Scenes/__AdventureValidation_" + Guid.NewGuid().ToString("N") + ".unity";
            Assert.IsTrue(AssetDatabase.CopyAsset(AuthoredScene, temporaryScene));
            string yaml = File.ReadAllText(temporaryScene);
            Assert.That(yaml, Does.Contain("saveAutomatically: 1"));
            File.WriteAllText(temporaryScene, yaml.Replace("saveAutomatically: 1", "saveAutomatically: 0"));
            AssetDatabase.ImportAsset(temporaryScene, ImportAssetOptions.ForceSynchronousImport);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(temporaryScene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            host = Object.FindObjectsOfType<AdventureSceneHost>().Single();
            screen = Object.FindObjectsOfType<AdventureScreen>().Single();
            float startupDeadline = Time.realtimeSinceStartup + 30;
            while (host.IsLoading && Time.realtimeSinceStartup < startupDeadline) yield return null;
            Assert.IsFalse(host.IsLoading, "Startup warmup timed out.");
            Assert.IsNotNull(host.Definition, host.Message);
            Assert.IsFalse(host.HasSavedRun, "The validation copy must not read the user's run save.");
            Assert.IsTrue(host.IsAtMenu); Assert.IsNotNull(EventSystem.current);
            Directory.CreateDirectory(Output);
            string marker = Path.Combine(Output, "single-chapter-flow.txt");
            if (File.Exists(marker)) File.Delete(marker);
        }

        private void CaptureError(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (message == "[MCP Unity] WebSocket error: An error has occurred in closing the connection.") return;
            errors.Add(message + "\n" + stack);
        }

        [UnityTearDown] public IEnumerator ReleaseIsolatedScene()
        {
            if (!string.IsNullOrEmpty(temporaryScene))
            {
                var loaded = SceneManager.GetSceneByPath(temporaryScene);
                if (loaded.IsValid() && loaded.isLoaded)
                {
                    var empty = SceneManager.CreateScene("Adventure validation cleanup");
                    SceneManager.SetActiveScene(empty);
                    yield return SceneManager.UnloadSceneAsync(loaded);
                }
                AssetDatabase.DeleteAsset(temporaryScene); temporaryScene = null;
            }
            Application.logMessageReceived -= CaptureError;
            LogAssert.ignoreFailingMessages = previousLogPolicy;
            Application.runInBackground = previousRunInBackground;
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [UnityTest]
        public IEnumerator AdventureBattleUsesOutcomeRelicsKickerFlightsAndAshBeforeReturningToRewards()
        {
            yield return Click("开始新旅程"); yield return Click("选择愚者"); yield return Click("选择"); yield return Click("踏上旅程");
            yield return Click("普通战斗"); yield return Idle(); AssertPhase(AdventurePhase.Battle); AssertRoster(1);
            var view = host.BattleView; var art = Object.FindObjectOfType<BattleDemoRelicArt>();
            var outcome = Object.FindObjectOfType<BattleDemoOutcomePreview>(); Assert.NotNull(outcome); Assert.NotNull(art);
            Assert.AreEqual(12, art.Entries.Length); Assert.AreEqual(0, host.Adventure.Relics.Count);
            Assert.NotNull(view.GetComponent<BattleCardEffectBridge>()); Assert.NotNull(view.GetComponent<BattleDemoTargetSelection>());
            Assert.NotNull(view.GetComponent<BattleDemoEnemyWorldHud>());
            var enemy = view.Enemies.Single(); var ash = enemy.Presentation as BattleDemoAshPresenter; Assert.NotNull(ash);
            var pairs = new[] { "standard:1:7", "standard:2:7", "standard:3:2", "standard:4:4", "standard:1:9" };
            var patch = new BattleAdminPatch(vitals: new[] { new BattleAdminVitals(enemy.ParticipantId, health: 600, maxHealth: 600) },
                zones: new[] { new BattleAdminZone(view.Player.ParticipantId, CardZoneKinds.Hand, pairs.Select(id => new BattleAdminCard(definitionId: id))) },
                relics: new[] { new BattleAdminRelics(view.Player.ParticipantId, new[] { new BattleAdminRelic("11004") }) });
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, patch).Accepted, host.Message); yield return Idle();
            var hud = art.GetComponent<SingleBattleHud>(); var grenade = view.State.Duel.Player(view.Player.ParticipantId).Relics.Relics.Single(relic => relic.DefinitionId == "11004");
            Assert.IsTrue(hud.SelectItem(grenade.InstanceId)); yield return null;
            var hover = art.transform.Find("Relic Hover"); Assert.IsTrue(hover.gameObject.activeSelf);
            var use = hover.Find("Use Item").GetComponent<Button>(); Assert.IsTrue(use.interactable);
            yield return ClickButton(use); yield return Idle();
            Assert.AreEqual(2, host.Adventure.Relics.Single(relic => relic.DefinitionId == "11004").Charges);
            Assert.IsTrue(art.GetComponentsInChildren<TMP_Text>().Any(text => text.name == "Remaining Uses" && text.text == "2"));
            ExecuteEvents.Execute(hover.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.23f);
            var hand = view.Actions.Hand;
            foreach (var card in hand.Cards.ToArray()) hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
            hand.Publish(); hand.GetComponent<HandZoneView>().CompleteTransitions(); view.RefreshCandidate(); yield return null;
            Assert.AreEqual(5, hand.CaptureSelection().Cards.Count); StringAssert.Contains("伤害", outcome.OutcomeText); StringAssert.DoesNotContain("×", outcome.OutcomeText);
            Assert.IsTrue(view.GetComponent<BattleDemoTargetSelection>().ChooseTarget(enemy));
            Assert.AreEqual(5, view.Player.Play(hand.CaptureSelection(), true));
            bool sawSplit = false, sawFlightCorridor = false; float deadline = Time.realtimeSinceStartup + 15;
            while (view.IsPresenting && Time.realtimeSinceStartup < deadline)
            {
                var meshes = view.GetComponentsInChildren<MeshRenderer>();
                var drops = meshes.Where(mesh => mesh.enabled && mesh.name.StartsWith("Unscored Card ")).ToArray();
                var shots = meshes.Where(mesh => mesh.enabled && mesh.name.StartsWith("Card Projectile ")).ToArray();
                if (drops.Length == 3 && shots.Length == 2)
                {
                    sawSplit = true; Assert.IsTrue(drops.All(mesh => mesh.GetComponentInChildren<TrailRenderer>() == null));
                    foreach (var shot in shots)
                    {
                        var tail = shot.GetComponentInChildren<TrailRenderer>(); Assert.NotNull(tail);
                        Assert.AreEqual(shot.GetComponent<MeshFilter>().sharedMesh.bounds.size.x * Mathf.Abs(shot.transform.lossyScale.x) * .55f, tail.widthMultiplier, .00001f);
                        var handView = hand.GetComponent<HandZoneView>();
                        if (!sawFlightCorridor && Vector3.Dot(shot.transform.position - hand.transform.position, handView.FacingCamera.transform.forward) > 1.5f)
                        {
                            ScreenCapture.CaptureScreenshot(Path.Combine(Output, "battle-flight.png"));
                            sawFlightCorridor = true;
                        }
                    }
                }
                yield return null;
            }
            Assert.IsTrue(sawSplit); Assert.IsTrue(sawFlightCorridor, "The scored cards must visibly traverse the expanded depth corridor."); yield return Idle();
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(
                vitals: new[] { new BattleAdminVitals(enemy.ParticipantId, health: 1, block: 0) },
                zones: new[] { new BattleAdminZone(view.Player.ParticipantId, CardZoneKinds.Hand,
                    new[] { new BattleAdminCard(definitionId: "standard:1:13") }) })).Accepted, host.Message);
            yield return Idle();
            foreach (var card in hand.Cards.ToArray()) hand.SetInteraction(card.Id, default);
            hand.SetInteraction(hand.Cards.First(card => card.Card.CanPlay).Id, new CardInteractionState(isSelected: true));
            hand.Publish(); hand.GetComponent<HandZoneView>().CompleteTransitions(); view.RefreshCandidate();
            Assert.AreEqual(1, view.Player.Play(hand.CaptureSelection(), true));
            bool sawAsh = false; deadline = Time.realtimeSinceStartup + 20;
            while (view.IsPresenting && Time.realtimeSinceStartup < deadline)
            {
                if (ash.Progress > .2f && ash.Progress < .8f)
                {
                    Assert.IsTrue(view.isActiveAndEnabled); Assert.AreEqual("Poker/BattleDemo Enemy Ash", ash.Artwork.sharedMaterial.shader.name);
                    if (!sawAsh) ScreenCapture.CaptureScreenshot(Path.Combine(Output, "battle-ash.png"));
                    sawAsh = true;
                }
                yield return null;
            }
            Assert.IsTrue(sawAsh); Assert.AreEqual(1, ash.Progress); yield return Idle();
            AssertPhase(AdventurePhase.Reward); Assert.AreEqual(0, ActiveEnemyHudCount());
            Assert.IsFalse(hover.gameObject.activeInHierarchy); Assert.AreEqual(50, host.Adventure.LastRewardGold);
        }

        [UnityTest]
        public IEnumerator AuthoredSceneCompletesSevenStopsThroughActualButtonsAndBattleAuthority()
        {
            yield return Capture("menu");
            yield return Click("开始新旅程");
            Assert.IsTrue(VisibleText("选择角色")); yield return Capture("role");
            yield return Click("选择愚者");
            Assert.IsTrue(VisibleText("选择开局天赋")); yield return Capture("tarot");
            yield return Click("选择");
            yield return Click("踏上旅程");
            AssertPhase(AdventurePhase.Map); Assert.AreEqual(0, host.Revision);
            yield return Capture("map");

            yield return Click("普通战斗");
            yield return Idle(); AssertPhase(AdventurePhase.Battle);
            AssertRoster(1); yield return Capture("battle");
            Assert.IsFalse(host.BattleView.GetComponentInChildren<SingleBattleHud>(true)?.ResultPanel.activeSelf ?? false);
            var authority = host.Session.State; long revision = host.Revision;
            yield return Click("旅程菜单"); Assert.IsTrue(host.IsAtMenu);
            Assert.AreEqual(0, ActiveEnemyHudCount());
            yield return Click("继续旅程"); yield return Idle();
            Assert.AreSame(authority, host.Session.State); Assert.AreEqual(revision, host.Revision);
            AssertRoster(1);
            yield return WinBattleWithCard(50, injurePlayer: true);
            yield return Click("继续旅程"); AssertPhase(AdventurePhase.Map);

            long beforeChestGold = host.Adventure.Gold;
            yield return Click("宝箱"); AssertPhase(AdventurePhase.Chest);
            Assert.AreEqual(beforeChestGold + 100, host.Adventure.Gold);
            Assert.AreEqual(1, host.Adventure.Relics.Count); yield return Capture("chest");
            yield return Click("收好奖励，继续旅程"); AssertPhase(AdventurePhase.Map);
            yield return Click("普通战斗"); yield return Idle(); AssertRoster(1);
            yield return WinBattleWithCard(60); yield return Click("继续旅程");

            yield return Click("商店"); AssertPhase(AdventurePhase.Shop);
            Assert.AreEqual("shop-1", host.Adventure.CurrentNode.Id); Assert.AreEqual(4, host.Adventure.ShopItems.Count);
            string[] stock = host.Adventure.ShopItems.Select(item => item.Id + "/" + item.RelicId + "/" + item.Price).ToArray();
            revision = host.Revision;
            yield return ClickWhere(text => text.EndsWith("金币 · 查看", StringComparison.Ordinal));
            Assert.IsTrue(VisibleText("道具详情")); yield return Capture("item-detail"); yield return Click("关闭");
            Assert.AreEqual(revision, host.Revision); AssertOverlayClosed();
            yield return Click("保存并返回"); Assert.IsTrue(host.IsAtMenu);
            yield return Click("继续旅程"); AssertPhase(AdventurePhase.Shop);
            CollectionAssert.AreEqual(stock, host.Adventure.ShopItems.Select(item => item.Id + "/" + item.RelicId + "/" + item.Price));
            Assert.AreEqual(revision, host.Revision);
            yield return Capture("shop");
            yield return ClickWhere(text => text.StartsWith("撕牌 ·", StringComparison.Ordinal)); yield return Capture("deck-service");
            var cards = screen.GetComponentsInChildren<Button>().Where(button => button.isActiveAndEnabled && button.interactable && button.transform.parent.name == "Cards").ToArray();
            Assert.IsNotEmpty(cards); int deckCount = host.Adventure.Deck.Count; long gold = host.Adventure.Gold;
            long price = host.Adventure.ServicePrice(AdventureCommandKind.DeleteCard);
            yield return ClickButton(cards[0]); yield return Click("确认撕牌");
            Assert.AreEqual(deckCount - 1, host.Adventure.Deck.Count); Assert.AreEqual(gold - price, host.Adventure.Gold);
            Assert.AreEqual(1, host.Adventure.ServiceUses(AdventureCommandKind.DeleteCard));
            AssertPhase(AdventurePhase.Shop); Assert.AreEqual("shop-1", host.Adventure.CurrentNode.Id);
            yield return Click("关闭"); AssertOverlayClosed();
            yield return Click("离开商店"); AssertPhase(AdventurePhase.Map);

            yield return Click("精英战斗"); yield return Idle(); AssertRoster(1);
            Assert.AreEqual(deckCount - 1, host.Session.State.Table.Cards.Count);
            Assert.IsTrue(host.BattleView.EndTurn(true)); yield return Idle();
            if (host.Session.State.FlowFor(host.Adventure.PlayerId).Window == BattlePlayerWindow.Ending)
            { Assert.IsTrue(host.BattleView.EndTurn(true)); yield return Idle(); }
            AssertRoster(1); Assert.IsFalse(host.Session.State.Duel.Enemies.Any(enemy => enemy.SummonerId.HasValue));
            Assert.AreEqual(18, host.Session.State.Duel.Enemies.Single().AttackAtRound(host.Session.State.Duel.CompletedRounds));
            yield return Capture("elite");
            yield return WinBattle(injurePlayer: true); yield return Click("继续旅程");
            long beforeRestHealth = host.Adventure.Health;
            long relicEntryHeal = host.Adventure.Relics.Sum(relic => host.Content.Relics[relic.DefinitionId].Rules.Item?.LocationEnterHeal ?? 0);
            yield return Click("休息"); AssertPhase(AdventurePhase.Rest);
            Assert.AreEqual("rest-2", host.Adventure.CurrentNode.Id);
            Assert.AreEqual(Math.Min(host.Adventure.MaxHealth, beforeRestHealth + relicEntryHeal + 20), host.Adventure.Health);
            Assert.AreEqual(20, host.Adventure.LastLocationReward.HealthDelta);
            Assert.IsFalse(host.Adventure.RestLotteryUsed); yield return Capture("rest");
            yield return Click("不抽奖，继续旅程"); AssertPhase(AdventurePhase.Map);

            yield return Click("首领战斗"); yield return Idle();
            AssertRoster(6); Assert.IsFalse(host.BattleView.Enemies.Any(actor => actor.ParticipantId.Value.Contains("summon")));
            yield return Capture("boss");
            yield return WinBattle(); yield return Click("完成旅程");
            AssertPhase(AdventurePhase.Completed); Assert.AreEqual(7, host.Adventure.VisitedNodeIds.Count);
            CollectionAssert.AreEqual(new[] { "battle-1", "chest", "battle-2", "shop-1", "elite", "rest-2", "boss" }, host.Adventure.VisitedNodeIds);
            Assert.AreEqual(0, ActiveEnemyHudCount()); AssertOverlayClosed();
            yield return Capture("result");
            Assert.IsEmpty(errors, string.Join("\n", errors));
            File.WriteAllText(Path.Combine(Output, "single-chapter-flow.txt"), "PASS\nSeven stops; actual card wins award 50/60 gold exactly once; battle/reward menu resume; stable shop stock; permanent delete; growing bleeding elite; rest skip; six-unit boss; completion. Elite/boss completion uses explicit admin shortcuts without node gold; their actual rules have dedicated regressions.\n");
        }

        [UnityTest]
        public IEnumerator SevenStationEliteUsesGrowingBleedingAttacksAndKeepsRemoteArtwork()
        {
            yield return ReachBattle("elite");
            AssertRoster(1);
            AssertRemotePresentation();
            yield return Capture("elite");
            var view = host.BattleView;
            Assert.AreEqual(500, view.Enemies.Single().DisplayUnit.MaxHealth);
            Assert.AreEqual(144, view.Player.DisplayUnit.MaxHealth, "The shipped 120 base HP retains the Fool's 20% bonus.");
            // Isolate the four-round skill/presentation check from the deliberately lower starting HP.
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(
                vitals: new[] { new BattleAdminVitals(host.Adventure.PlayerId, 100000, 100000) })).Accepted);
            yield return Idle();
            Assert.IsFalse(view.Display.Labels.ContainsKey("wrath.body"));
            long[] damage = { 4, 20, 11, 43 };
            for (int i = 0; i < damage.Length; i++)
            {
                long before = view.Player.DisplayUnit.Health;
                yield return EndAdventureRound();
                Assert.AreEqual(damage[i], before - view.Player.DisplayUnit.Health, "Actual elite round " + (i + 1));
                Assert.AreEqual(i + 1, view.State.Duel.CompletedRounds);
                AssertRoster(1);
            }
        }

        [UnityTest]
        public IEnumerator SevenStationBossShowsReadableOpeningAndWarningsBeforeRoundTwoAndEightUltimates()
        {
            yield return ReachBattle("boss", settleTarget: false);
            yield return WaitForWrathPrompt("Wrath Opening Warning", true);
            var openingArea = AssertReadableWrathPrompt("Wrath Opening Warning", "每存活一只手");
            yield return CaptureWrathProof("boss-opening-warning");
            yield return Idle();
            var view = host.BattleView;
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(
                vitals: new[] { new BattleAdminVitals(host.Adventure.PlayerId, 100000, 100000) },
                relics: new[] { new BattleAdminRelics(host.Adventure.PlayerId, Array.Empty<BattleAdminRelic>()) })).Accepted);
            yield return Idle();
            yield return WaitForWrathPrompt("Wrath Opening Warning", false);
            Assert.AreEqual(0, view.State.Duel.CompletedRounds);
            AssertWrathWarning(false);
            while (view.State.Duel.CompletedRounds < 8)
            {
                int previousRound = view.State.Duel.CompletedRounds;
                long previousHealth = view.Player.DisplayUnit.Health;
                yield return EndAdventureRound();
                int round = view.State.Duel.CompletedRounds;
                Assert.Greater(round, previousRound, "Each real end-turn must advance the seven-station battle.");
                bool warning = round == 1 || round == 7;
                AssertWrathWarning(warning);
                if (warning)
                {
                    Assert.AreEqual(round + 1, WrathBossQuery.Read(view.State.Duel).ReleaseRound);
                    var warningArea = AssertReadableWrathPrompt("Wrath Ultimate Warning", "将于下一回合释放大招");
                    Assert.Less(Vector2.Distance(openingArea.center, warningArea.center), 1.5f,
                        "The persistent warning must reuse the opening banner's visible position.");
                    Assert.Less(Vector2.Distance(openingArea.size, warningArea.size), 1.5f);
                    yield return CaptureWrathProof("boss-round-" + round + "-warning");
                }
                if (round == 2 || round == 8)
                {
                    string body = view.Display.Labels["wrath.body"];
                    var hits = view.LastResolution.Timeline.Select(entry => entry.RuleEvent).OfType<CombatEvent>()
                        .Where(hit => hit.Effect.Kind == CombatEffectKind.Damage && hit.Effect.Source.Value == body &&
                            hit.Effect.Target == host.Adventure.PlayerId).Select(hit => hit.SettledDamage).ToArray();
                    CollectionAssert.AreEqual(round == 2 ? new long[] { 33, 99 } : new long[] { 159 }, hits,
                        "The actual regular action must resolve before the extra ultimate; round 8 charges before its 3D hit.");
                    Assert.AreEqual(round == 2 ? 132 : 159, previousHealth - view.Player.DisplayUnit.Health);
                    yield return CaptureWrathProof("boss-round-" + round + "-released");
                }
            }
            Assert.IsTrue(view.Enemies.All(actor => actor.DisplayUnit.IsAlive),
                "Both warnings and releases must be exercised with all five original hands alive.");
        }

        [UnityTest]
        public IEnumerator SevenStationBossSeversAndRegeneratesFiveOriginalActorsWithPsdArtwork()
        {
            yield return ReachBattle("boss");
            var view = host.BattleView;
            AssertRoster(6);
            AssertRemotePresentation();
            yield return CaptureWrathProof("boss");
            Assert.AreEqual(2000, view.Enemies[0].DisplayUnit.MaxHealth);
            Assert.AreEqual(33, WrathBossQuery.Read(view.State.Duel).Attack);
            Assert.IsTrue(view.GetComponent<BattleDemoTargetSelection>().ChooseTarget(view.Enemies[0]));
            var hands = view.Enemies.Skip(1).ToArray();
            var positions = hands.Select(actor => actor.transform.position).ToArray();
            // This test waits for regeneration; normal player survival is covered separately.
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(
                vitals: new[] { new BattleAdminVitals(host.Adventure.PlayerId, 100000, 100000) },
                relics: new[] { new BattleAdminRelics(host.Adventure.PlayerId, new[] { new BattleAdminRelic("11004", charges: 3) }) })).Accepted);
            yield return Idle();
            string grenade = view.State.Duel.Player(host.Adventure.PlayerId).Relics.Relics.Single().InstanceId;
            Assert.IsTrue(view.Playback.UseItem(host.Revision, host.Adventure.PlayerId, grenade).Accepted);
            Assert.IsTrue(view.IsPresenting);
            yield return Idle();
            foreach (var hand in hands)
            {
                Assert.AreEqual(0, hand.DisplayUnit.Health);
                Assert.AreEqual(1, ((BattleDemoAshPresenter)hand.Presentation).Progress);
                var world = FindEnemyWorldHud(hand);
                Assert.IsTrue(world.gameObject.activeInHierarchy);
                Assert.IsFalse(world.Find("Intents").gameObject.activeSelf);
                var countdown = world.Find("Revive Countdown").GetComponent<TMP_Text>();
                Assert.IsTrue(countdown.isActiveAndEnabled);
                Assert.AreEqual("复活倒计时 8", countdown.text);
                Assert.IsFalse(world.Find("Health Bar").gameObject.activeSelf);
            }
            Assert.AreEqual(8, WrathBossQuery.Read(view.State.Duel).Attack);
            Assert.AreEqual(6, ActiveEnemyHudCount());
            yield return CaptureWrathProof("boss-severed");
            host.ReturnToMenu(); Assert.IsTrue(host.IsAtMenu);
            Assert.IsTrue(host.Resume()); yield return Idle();
            CollectionAssert.AreEqual(hands, view.Enemies.Skip(1), "Menu/resume must not replace dead actors.");
            while (view.State.Duel.CompletedRounds < 8) yield return EndAdventureRound();
            Assert.IsTrue(hands.All(actor => !actor.DisplayUnit.IsAlive));
            yield return EndAdventureRound();
            Assert.AreEqual(9, view.State.Duel.CompletedRounds);
            CollectionAssert.AreEqual(hands, view.Enemies.Skip(1));
            for (int i = 0; i < hands.Length; i++)
            {
                Assert.AreEqual(1, hands[i].DisplayUnit.Health);
                Assert.AreEqual(0, ((BattleDemoAshPresenter)hands[i].Presentation).Progress);
                Assert.Less(Vector3.Distance(positions[i], hands[i].transform.position), .0001f);
                var world = FindEnemyWorldHud(hands[i]);
                Assert.IsTrue(world.Find("Health Bar").gameObject.activeSelf);
                Assert.IsTrue(world.Find("Intents").gameObject.activeSelf);
                Assert.IsFalse(world.Find("Revive Countdown").gameObject.activeSelf);
            }
            Assert.AreEqual(53, WrathBossQuery.Read(view.State.Duel).Attack);
            foreach (var hand in hands)
                Assert.IsTrue(view.GetComponent<BattleDemoTargetSelection>().ChooseTarget(hand),
                    "Regenerated hands retain the remote target selection path.");
            AssertRoster(6);
            AssertRemotePresentation();
            yield return CaptureWrathProof("boss-regenerated");
        }

        private void AssertRemotePresentation()
        {
            Assert.IsTrue(host.BattleView.UsesExternalCommands);
            Assert.IsTrue(host.BattleView.GetComponent<BattleDemoEnemyWorldHud>().enabled);
            Assert.IsFalse(host.BattleView.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Any(item => item.name == "Wrath Boss" && item.gameObject.activeInHierarchy),
                "The standalone validation panel must not replace the authored adventure UI.");
            var view = host.BattleView;
            if (view.Display.Labels.TryGetValue("wrath.body", out var bodyId))
            {
                var body = view.Enemies.Single(actor => actor.DisplayId == bodyId);
                var health = FindEnemyWorldHud(body).Find("Health Bar").GetComponent<RectTransform>();
                var bossArea = ScreenArea(health);
                var viewport = new Rect(0, 0, Screen.width, Screen.height);
                var profile = view.GetComponent<BattleAuthoredLayout>().Profile;
                foreach (var actor in view.Enemies)
                {
                    var bar = FindEnemyWorldHud(actor).Find("Health Bar").GetComponent<RectTransform>();
                    var area = ScreenArea(bar);
                    Assert.IsTrue(viewport.Contains(area.min) && viewport.Contains(area.max), actor.name + " health must stay visible.");
                    var authored = profile.Find(view.ContentEncounterId, actor.DisplayId).nodes.Single(node => node.path == "Health Bar");
                    Assert.Less(Vector3.Distance(authored.position, bar.anchoredPosition3D), .01f,
                        "Keep the user's authored health-bar placement: " + actor.name);
                    if (actor != body) Assert.IsFalse(bossArea.Overlaps(area), "A hand health bar must not cover the boss header.");
                }
            }
        }

        private Transform WrathPrompt(string name) => host.BattleView.gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<SingleBattleHud>(true)).Single().transform.Find(name);

        private IEnumerator WaitForWrathPrompt(string name, bool visible)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while ((WrathPrompt(name)?.gameObject.activeInHierarchy == true) != visible && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(visible, WrathPrompt(name)?.gameObject.activeInHierarchy == true, name);
        }

        private void AssertWrathWarning(bool visible)
        {
            Assert.AreEqual(visible, WrathBossQuery.Read(host.BattleView.State.Duel).WarningActive);
            Assert.AreEqual(visible, WrathPrompt("Wrath Ultimate Warning").gameObject.activeInHierarchy);
            Assert.AreEqual(visible, host.BattleView.GetComponent<WrathBossArtworkLayout>().WarningVisible,
                "The PSD glow and the persistent banner must follow the same presented warning.");
        }

        private Rect AssertReadableWrathPrompt(string name, string message)
        {
            Canvas.ForceUpdateCanvases();
            var root = WrathPrompt(name).GetComponent<RectTransform>();
            Assert.IsTrue(root.gameObject.activeInHierarchy, name);
            var background = root.GetComponent<Image>(); Assert.IsNotNull(background);
            Assert.Greater(background.color.a, .8f); Assert.Less(Mathf.Max(background.color.r, background.color.g, background.color.b), .25f);
            Assert.GreaterOrEqual(root.rect.height / ((RectTransform)root.parent).rect.height, .12f,
                "The warning needs the enlarged opening banner, rather than the old narrow strip.");
            var label = root.Find("Warning").GetComponent<TMP_Text>(); label.ForceMeshUpdate();
            StringAssert.Contains(message, label.text);
            Assert.Greater(label.color.r, .8f); Assert.Less(label.color.g, .4f); Assert.Less(label.color.b, .4f);
            Assert.GreaterOrEqual(label.fontSize, 30); Assert.IsFalse(label.isTextTruncated);
            Assert.LessOrEqual(label.textInfo.lineCount, 2);
            Assert.LessOrEqual(label.preferredHeight, label.rectTransform.rect.height + 1, "All warning lines must fit the dark background.");
            var area = ScreenArea(root); var viewport = new Rect(0, 0, Screen.width, Screen.height);
            Assert.IsTrue(viewport.Contains(area.min) && viewport.Contains(area.max), name + " must stay fully visible.");
            return area;
        }

        private static Rect ScreenArea(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var points = corners.Select(corner => RectTransformUtility.WorldToScreenPoint(camera, corner)).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
                points.Max(point => point.x), points.Max(point => point.y));
        }

        private IEnumerator CaptureWrathProof(string name)
        {
            yield return null; Canvas.ForceUpdateCanvases();
            string path = Path.GetFullPath(Path.Combine(Output, name + ".png"));
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 5;
            do { yield return null; } while (!File.Exists(path) && Time.realtimeSinceStartup < deadline);
            Assert.IsTrue(File.Exists(path), "Screenshot was not produced: " + path);
        }

        private Transform FindEnemyWorldHud(EnemyBattleActor actor) =>
            host.BattleView.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Single(item => item.name == "Enemy World HUD " + actor.name);

        private IEnumerator ReachBattle(string target, bool settleTarget = true)
        {
            Assert.IsTrue(host.NewRun(host.AvailableTarots.First().Id, 312));
            foreach (string id in new[] { "battle-1", "chest", "battle-2", "rest-1", "elite", "rest-2", "boss" })
            {
                Assert.IsTrue(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, id)), host.Message);
                if (id == target && !settleTarget) yield break;
                yield return Idle();
                if (id == target) yield break;
                if (host.Adventure.Phase == AdventurePhase.Battle) yield return WinBattle();
                while (host.Adventure.PendingRelicRewards.Count > 0)
                    Assert.IsTrue(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.SkipRelicReward)), host.Message);
                Assert.IsTrue(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.Continue)), host.Message);
                yield return Idle();
            }
            Assert.Fail("Target battle was not reached: " + target);
        }

        private IEnumerator EndAdventureRound()
        {
            Assert.IsTrue(host.BattleView.EndTurn(true)); yield return Idle();
            if (host.Session.State.FlowFor(host.Adventure.PlayerId).Window == BattlePlayerWindow.Ending)
            { Assert.IsTrue(host.BattleView.EndTurn(true)); yield return Idle(); }
        }

        private IEnumerator WinBattleWithCard(long expectedReward, bool injurePlayer = false)
        {
            Assert.IsTrue(host.BattleView.CanEditBattle);
            var draft = new BattleAdminDraft(host.BattleView.Session);
            foreach (var unit in draft.Units)
            {
                if (unit.Id != host.Adventure.PlayerId) { unit.Health = "1"; unit.Block = "0"; }
                else if (injurePlayer) unit.Health = Math.Max(1, host.Adventure.MaxHealth - 100).ToString(CultureInfo.InvariantCulture);
            }
            Assert.IsTrue(host.BattleView.EditBattle(draft, preview: false).Accepted);
            yield return Idle(); AssertPhase(AdventurePhase.Battle);
            long beforeGold = host.Adventure.Gold; long revision = host.Revision;
            var hand = host.BattleView.Actions.Hand;
            foreach (var card in hand.Cards.ToArray()) hand.SetInteraction(card.Id, default);
            var selected = hand.Cards.First(card => card.Card.CanPlay);
            hand.SetInteraction(selected.Id, new CardInteractionState(isSelected: true));
            hand.Publish(); hand.GetComponent<HandZoneView>().CompleteTransitions();
            host.BattleView.RefreshCandidate();
            Assert.AreEqual(1, host.BattleView.Player.Play(hand.CaptureSelection(), animate: true));
            Assert.AreEqual(revision + 1, host.Revision); AssertPhase(AdventurePhase.Reward);
            Assert.AreEqual(expectedReward, host.Adventure.LastRewardGold);
            Assert.AreEqual(beforeGold + expectedReward, host.Adventure.Gold);
            Assert.IsTrue(host.IsBusy, "The lethal card's visuals must finish before reward navigation becomes available.");
            Assert.IsTrue(host.BattleView.isActiveAndEnabled); Assert.IsFalse(VisibleVictory());
            Assert.IsFalse(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.Continue)));
            yield return AssertResultDelay();
            yield return Idle(); Assert.IsTrue(VisibleVictory()); Assert.AreEqual(0, ActiveEnemyHudCount());
            long settledRevision = host.Revision; long settledGold = host.Adventure.Gold;
            host.BattleView.SynchronizeBattle(); host.BattleView.SynchronizeBattle();
            Assert.AreEqual(settledRevision, host.Revision); Assert.AreEqual(settledGold, host.Adventure.Gold);
            yield return Click("保存并返回"); Assert.IsTrue(host.IsAtMenu);
            yield return Click("继续旅程"); AssertPhase(AdventurePhase.Reward);
            Assert.AreEqual(settledRevision, host.Revision); Assert.AreEqual(settledGold, host.Adventure.Gold);
            Assert.AreEqual(expectedReward, host.Adventure.LastRewardGold, "Synchronization and resume must not award node gold again.");
        }

        private IEnumerator WinBattle(bool injurePlayer = false)
        {
            // Elite and boss use a direct administrative shortcut to keep the UI smoke test short.
            // An admin correction deliberately skips death/reward hooks and awards zero node gold.
            Assert.IsTrue(host.BattleView.CanEditBattle, "The editor command requires a settled player decision.");
            var draft = new BattleAdminDraft(host.BattleView.Session);
            foreach (var unit in draft.Units)
            {
                if (unit.Id != host.Adventure.PlayerId) unit.Health = "0";
                else if (injurePlayer) unit.Health = Math.Max(1, host.Adventure.MaxHealth - 100).ToString(CultureInfo.InvariantCulture);
            }
            long revision = host.Revision;
            var result = host.BattleView.EditBattle(draft, preview: false);
            Assert.IsTrue(result.Accepted); Assert.AreEqual(revision + 1, host.Revision);
            AssertPhase(AdventurePhase.Reward);
            Assert.AreEqual(0, host.Adventure.LastRewardGold);
            yield return AssertResultDelay();
            yield return Idle();
            Assert.IsTrue(VisibleVictory()); Assert.AreEqual(0, ActiveEnemyHudCount());
        }

        private IEnumerator AssertResultDelay()
        {
            float deadline = Time.realtimeSinceStartup + 25;
            while (host.BattleView.IsPresenting && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(host.BattleView.IsPresenting);
            double finishedAt = Time.unscaledTimeAsDouble;
            // The combat stage remains visible and navigation stays locked after the last effect ends.
            while (Time.unscaledTimeAsDouble - finishedAt < 1.2)
            {
                Assert.IsTrue(host.IsBusy);
                Assert.IsTrue(host.BattleView.isActiveAndEnabled);
                Assert.IsFalse(VisibleVictory());
                host.ReturnToMenu(); Assert.IsFalse(host.IsAtMenu);
                yield return null;
            }
            deadline = Time.realtimeSinceStartup + 2;
            while (host.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(host.IsBusy);
            Assert.GreaterOrEqual(Time.unscaledTimeAsDouble - finishedAt, 1.4, "Result UI appeared before the 1.5 second delay (one frame tolerance).");
            Assert.IsTrue(VisibleVictory());
        }

        private IEnumerator Idle()
        {
            float deadline = Time.realtimeSinceStartup + 25;
            yield return null;
            while (host.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(host.IsBusy, "Run presentation did not return control before the deadline.");
            if (host.Adventure?.Phase == AdventurePhase.Battle)
            {
                Assert.IsNull(host.BattleView.PresentationDirector.LastError);
                Assert.AreSame(host.Session.State, host.BattleView.PresentedState);
            }
            Assert.IsEmpty(host.Message, host.Message); yield return null;
        }

        private void AssertPhase(AdventurePhase phase)
        { Assert.IsFalse(host.IsAtMenu); Assert.AreEqual(phase, host.Adventure.Phase, host.Message); }
        private static int ActiveEnemyHudCount() => Object.FindObjectsOfType<RectTransform>().Count(rect =>
            rect.gameObject.activeInHierarchy && rect.name.StartsWith("Enemy World HUD ", StringComparison.Ordinal));
        private void AssertRoster(int count)
        {
            Assert.AreEqual(count, host.BattleView.Enemies.Count); Assert.AreEqual(count, host.Session.State.Duel.Enemies.Count);
            Assert.IsTrue(host.BattleView.Enemies.All(actor => actor.IsBound && actor.isActiveAndEnabled && actor.DisplayUnit?.Id == actor.ParticipantId.Value));
            Assert.IsTrue(host.BattleView.Enemies.All(actor => actor.Presentation is BattleDemoAshPresenter));
            foreach (var actor in host.BattleView.Enemies)
            {
                var ash = (BattleDemoAshPresenter)actor.Presentation; Assert.AreEqual(0, ash.Progress);
                var block = new MaterialPropertyBlock(); ash.Artwork.GetPropertyBlock(block); Assert.NotNull(block.GetTexture("_BaseMap"));
                var enemy = host.BattleView.PresentedState.Duel.Enemies.Single(unit => unit.Id == actor.ParticipantId);
                var composition = host.BattleView.GetComponent<WrathBossArtworkLayout>();
                string expectedTexture;
                if (composition != null && composition.IsComposed)
                {
                    var part = composition.PartFor(actor);
                    Assert.IsNotNull(part, "Each boss participant keeps its own PSD layer.");
                    expectedTexture = AssetDatabase.GetAssetPath(part.Texture);
                }
                else
                {
                    string portrait = host.Adventure.CurrentNode.Kind == AdventureNodeKind.Elite && !enemy.SummonerId.HasValue
                        ? "Elite-Updated.png" : "Minion-Updated.png";
                    expectedTexture = "Assets/Art/Characters/BattleBillboards/" + portrait;
                }
                Assert.AreEqual(expectedTexture, AssetDatabase.GetAssetPath(block.GetTexture("_BaseMap")),
                    "Encounter artwork must survive reuse and summoning: " + actor.name);
                Assert.AreEqual(1, ash.Artwork.GetComponentsInChildren<MeshRenderer>(true).Count(renderer => renderer.name == "Enemy Ash Fragments"));
            }
            Assert.AreEqual(count, ActiveEnemyHudCount(), "Inactive actors and previous encounters must not leave visible HUDs.");
            var layouts = host.BattleView.Enemies.Select(actor => actor.GetComponentInChildren<BattleBillboardLayout>(true)).ToArray();
            Assert.IsTrue(layouts.All(layout => layout != null), "Each enemy needs a viewport layout.");
            bool composedBoss = host.BattleView.GetComponent<WrathBossArtworkLayout>()?.IsComposed == true;
            for (int i = 0; !composedBoss && i < layouts.Length; i++)
                for (int j = i + 1; j < layouts.Length; j++)
                    Assert.IsFalse(layouts[i].ViewportArea.Overlaps(layouts[j].ViewportArea),
                        "Enemy viewports overlap: " + host.BattleView.Enemies[i].ParticipantId + " / " + host.BattleView.Enemies[j].ParticipantId);
        }
        private bool VisibleVictory() => VisibleText("战斗胜利") || screen.GetComponentsInChildren<Image>().Any(image =>
            image.isActiveAndEnabled && image.name == "Victory" && image.sprite != null && image.sprite == DeskUISkin.Get("VictoryLogo"));
        private bool VisibleText(string text) => screen.GetComponentsInChildren<TMP_Text>().Any(label => label.isActiveAndEnabled && label.text == text);
        private void AssertOverlayClosed()
        {
            var overlay = screen.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Run Overlay");
            Assert.IsFalse(overlay.Cast<Transform>().Any(child => child.gameObject.activeSelf));
        }
        private IEnumerator Click(string caption) => ClickWhere(text => text == caption);
        private IEnumerator ClickWhere(Func<string, bool> predicate)
        {
            yield return null; Canvas.ForceUpdateCanvases();
            var buttons = screen.GetComponentsInChildren<Button>().Where(button => button.isActiveAndEnabled && button.interactable &&
                predicate(button.GetComponentInChildren<TMP_Text>(true)?.text ?? "")).ToArray();
            Assert.IsNotEmpty(buttons, "No enabled button matches the requested action. " + host.Message);
            yield return ClickButton(buttons[0]);
        }
        private IEnumerator ClickButton(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left, clickCount = 1 };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits, "The button must be reachable by the actual UI raycaster: " + button.name);
            var clickable = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.AreEqual(button.gameObject, clickable, "Another UI surface blocks " + button.name + ": " + hits[0].gameObject.name);
            ExecuteEvents.Execute(clickable, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
        }
        private IEnumerator Capture(string name)
        {
            yield return null; Canvas.ForceUpdateCanvases();
            if (host.Adventure?.Phase == AdventurePhase.Battle)
            {
                var player = host.BattleView.Player;
                Assert.IsNull(player.GetComponent<PlayerPortraitOverlay>());
                var renderer = player.Visual.Find("Artwork").GetComponent<MeshRenderer>();
                Assert.IsFalse(renderer.forceRenderingOff); Assert.AreEqual(Vector3.one, renderer.transform.localScale);
                var portrait = player.Visual.GetComponent<PortraitBattleActorPresenter>();
                var texture = portrait.DisplayedTexture;
                Vector3 scale = renderer.transform.lossyScale;
                Assert.AreEqual((float)texture.width / texture.height, scale.x / scale.y, .002f, "Portrait must retain source image proportions.");
                Assert.AreEqual("FourBeastsAtlas", host.BattleView.Actions.Hand.GetComponentsInChildren<CardArt.CardView>().First().Atlas.name);
                var handView = host.BattleView.Actions.Hand.GetComponent<HandZoneView>();
                foreach (var enemy in host.BattleView.Enemies)
                    Assert.Greater(Vector3.Dot(enemy.transform.position - handView.transform.position, handView.FacingCamera.transform.forward), 4f,
                        "The enemy plane must be separated from the hand plane for a readable projectile flight.");
            }
            var truncated = new List<string>();
            foreach (var label in screen.GetComponentsInChildren<TMP_Text>())
            {
                if (!label.isActiveAndEnabled || label.GetComponentInParent<ScrollRect>() != null) continue;
                label.ForceMeshUpdate();
                if (label.isTextTruncated) truncated.Add(label.transform.parent.name + ": " + label.text);
            }
            string path = Path.GetFullPath(Path.Combine(Output, name + ".png"));
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 5;
            do { yield return null; } while (!File.Exists(path) && Time.realtimeSinceStartup < deadline);
            Assert.IsTrue(File.Exists(path), "Screenshot was not produced: " + path);
            Assert.IsEmpty(truncated, "Adventure text is truncated: " + string.Join("; ", truncated));
        }
    }
}
#endif
