#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using CardInteraction;
using CardPresentation;
using NUnit.Framework;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using global::BattlePresentation;

namespace Poker.SingleBattle.Tests
{
    /// <summary>Runs the saved six-actor scene, authoritative session and authored asynchronous timeline together.</summary>
    public sealed class WrathBossScenePlaybackTests
    {
        private const string ScenePath = "Assets/Scenes/WrathBossDemo.unity";
        private const string ContentPath = "Packages/com.kaya.battle-engine/Content/StandardSingleBattle-2026-10-21.xml";
        private const string Encounter = "standard-wrath-thousand-hands";
        private const string OpeningText = "警告：每存活一只手，暴怒攻击力+5！！";
        private const string UltimateText = "警告：Boss有五只手臂，将释放大招！！";
        private readonly List<string> errors = new List<string>();
        private readonly HashSet<string> regenerationFloats = new HashSet<string>();
        private SingleBattleSceneAdapter host;
        private SingleBattleHud hud;
        private TextAsset runtimeContentFixture;
        private bool previousLogPolicy, sawOpening, sawWarning;

        [SetUp]
        public void CaptureProductErrors()
        {
            errors.Clear(); regenerationFloats.Clear(); sawOpening = sawWarning = false;
            previousLogPolicy = LogAssert.ignoreFailingMessages;
            // Match the existing scene suite's connector-reconnection exception; retain every product error.
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += CaptureError;
        }

        [TearDown]
        public void StopOwnedPlaybackAndCheckProductErrors()
        {
            // Recovery is cleanup only. Every behavior assertion below awaits the real timeline.
            if (host != null) host.SnapToState();
            if (runtimeContentFixture != null) Object.DestroyImmediate(runtimeContentFixture);
            Application.logMessageReceived -= CaptureError;
            LogAssert.ignoreFailingMessages = previousLogPolicy;
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [UnityTest]
        public IEnumerator SavedSceneBindsSixActorsAndShowsInitialAttackEightIntentsAndOneOpening()
        {
            yield return LoadBoss();
            CollectionAssert.AreEqual(new[] { "enemy", "hand-1", "hand-2", "hand-3", "hand-4", "hand-5" },
                host.Enemies.Select(actor => actor.ParticipantId.Value));
            Assert.AreEqual(6, host.Enemies.Select(actor => actor.gameObject).Distinct().Count());
            Assert.AreEqual(6, host.Enemies.Select(actor => actor.transform.position).Distinct().Count());
            var composition = host.GetComponent<WrathBossArtworkLayout>();
            Assert.IsTrue(composition.IsComposed);
            CollectionAssert.AreEqual(new[] { "Body", "Hand1", "Hand2", "Hand3", "Hand4", "Hand5" },
                host.Enemies.Select(actor => composition.PartFor(actor).Name));
            Assert.IsFalse(composition.WarningVisible);
            Assert.IsTrue(host.Enemies.All(actor => actor.IsBound && actor.Battle == host && actor.Visual != null));
            Assert.AreEqual(1500, host.Display.Unit("enemy").Health);
            Assert.IsTrue(host.Enemies.Skip(1).All(actor => actor.DisplayUnit.Health == 1 && actor.DisplayUnit.MaxHealth == 1));
            Assert.AreEqual(33, Resource("enemy", "wrath.attack"));
            StringAssert.Contains("攻击力 33", Text("Wrath Boss/Attack").text);
            AssertIntents(new[] { "strike", "strike-2", "guard", "charge", "strike", "strike-2", "guard", "charge" },
                new[] { "普攻", "普攻", "防御", "蓄力", "普攻", "普攻", "防御", "蓄力" });
            Assert.IsTrue(sawOpening, "The real scene opening must reveal its red warning during playback.");
            var opening = Text("Wrath Opening Warning/Warning");
            Assert.AreEqual(OpeningText, opening.text);
            Assert.Greater(opening.color.r, opening.color.g * 2);

            // The public rules control can reopen and dismiss the same message; refresh/preview cannot replay it.
            var rules = Find("Wrath Boss/Wrath Rules").GetComponent<Button>();
            rules.onClick.Invoke();
            Assert.IsTrue(opening.gameObject.activeInHierarchy);
            rules.onClick.Invoke();
            Assert.IsFalse(opening.gameObject.activeInHierarchy);
            yield return PrepareCards("standard:1:2");
            var before = host.AuthorityState;
            var preview = host.PreviewSelectedPlay();
            Assert.IsTrue(preview.Accepted, preview.Error.ToString());
            hud.Refresh(); hud.Refresh();
            Assert.AreSame(before, host.AuthorityState);
            Assert.IsFalse(opening.gameObject.activeInHierarchy, "Repeated display/preview must not restart the entrance warning.");
            Assert.AreEqual(6, host.PlayTargetChoices.Single().Candidates.Count);
            Assert.IsTrue(host.Enemies.All(actor => Target(actor.ParticipantId.Value).interactable));
            AssertSynchronized();
            Directory.CreateDirectory("Library/WrathBossArtwork");
            ScreenCapture.CaptureScreenshot("Library/WrathBossArtwork/assembled.png");
            yield return new WaitForEndOfFrame();
        }

        [UnityTest]
        public IEnumerator HudSelectedHandDiesAtImpactShowsRedEightAndEnemyEntryFloatThenRegeneratesAsSameActor()
        {
            yield return LoadBoss();
            var hand = host.Enemies.Single(actor => actor.ParticipantId.Value == "hand-1");
            var position = hand.transform.position;
            yield return PrepareCards("standard:1:2");
            ChooseTarget("hand-1");
            long bodyHealth = host.Display.Unit("enemy").Health;
            Assert.AreEqual(1, host.Player.Play(host.Actions.Hand.CaptureSelection(), true));
            Assert.IsTrue(host.IsPresenting, "A real card play must use the saved asynchronous timeline.");
            Assert.AreEqual(0, host.AuthorityState.Duel.Combat.Get(hand.ParticipantId).Health);
            Assert.AreEqual(1, host.Display.Unit("hand-1").Health, "Death must wait for the impact checkpoint.");
            yield return Idle();
            Assert.AreEqual(bodyHealth, host.Display.Unit("enemy").Health, "The explicitly selected hand receives the single hit.");
            Assert.AreEqual(0, hand.DisplayUnit.Health);
            Assert.AreEqual(28, Resource("enemy", "wrath.attack"));
            var marker = Marker("hand-1");
            Assert.IsTrue(marker.gameObject.activeInHierarchy);
            var countdown = marker.Find("Countdown").GetComponent<TMP_Text>();
            Assert.AreEqual("8", countdown.text);
            Assert.Greater(countdown.color.r, countdown.color.g * 2);
            Assert.Less(Vector3.Distance(position, marker.position), .0001f, "The red countdown stays at the logical hand position.");

            yield return PrepareCards("standard:2:2");
            Assert.IsFalse(Target("hand-1").interactable);
            Assert.IsFalse(host.PlayTargetChoices.SelectMany(choice => choice.Candidates).Any(candidate => candidate.Id == "hand-1"));
            string selected = host.PlayTargetChoices.Single().SelectedId;
            Target("hand-1").onClick.Invoke();
            Assert.AreEqual(selected, host.PlayTargetChoices.Single().SelectedId, "Even a direct callback cannot select a dead hand.");
            yield return EndTurn();
            CollectionAssert.Contains(regenerationFloats, "8个回合后再生");
            Assert.AreEqual("7", countdown.text, "After round 1 the next player window is round 2, so authority reports 7.");
            yield return AdvanceThrough(8);
            Assert.AreEqual(0, hand.DisplayUnit.Health);
            Assert.AreEqual("0", countdown.text, "Round 9's player window precedes its scheduled enemy-phase regeneration.");
            yield return EndTurn();
            Assert.AreEqual(9, host.State.Duel.CompletedRounds);
            Assert.AreSame(hand, host.Enemies.Single(actor => actor.ParticipantId.Value == "hand-1"));
            Assert.AreEqual(1, hand.DisplayUnit.Health);
            Assert.Less(Vector3.Distance(position, hand.transform.position), .0001f);
            Assert.IsFalse(marker.gameObject.activeInHierarchy, "Revival removes both the countdown and its floating text.");
            Assert.AreEqual(53, Resource("enemy", "wrath.attack"), "Five live hands plus two completed charges: 8 + 25 + 20.");
            Assert.IsFalse(regenerationFloats.Contains("0个回合后再生"));
            yield return PrepareCards("standard:3:2");
            Assert.IsTrue(Target("hand-1").interactable);
            AssertSynchronized();
        }

        [UnityTest]
        public IEnumerator BindingAtTwentyFixtureLetsRealHandPlayCancelWarningAndRestartClearsOldHud()
        {
            yield return LoadBoss();
            var originalXml = host.ContentXml;
            string originalText = originalXml.text;
            byte[] originalScene = File.ReadAllBytes(ScenePath);
            // Only this test moves binding to round 20 to retain a player window after warning.
            // Boss rules, six actors, command execution and presentation remain the real implementation.
            var document = new XmlDocument(); document.LoadXml(originalText);
            var binding = document.SelectSingleNode("/BattleContent/Enemies/Enemy[@id='wrath-binding']/Behavior/Stage/Action[@id='binding']/When");
            Assert.NotNull(binding);
            Assert.AreEqual("5", binding.Attributes["value"].Value);
            binding.Attributes["value"].Value = "20";
            runtimeContentFixture = new TextAsset(document.OuterXml) { name = "Wrath binding-at-20 PlayMode fixture" };
            host.ConfigureContent(runtimeContentFixture, Encounter); host.RestartBattle();
            yield return Idle();
            yield return AdvanceThrough(5);
            Assert.AreEqual(5, host.State.Duel.CompletedRounds);
            Assert.AreEqual(1, Resource("enemy", "wrath.warning"));
            Assert.AreEqual(6, Resource("enemy", "wrath.releaseRound"));
            AssertWarningVisible(true);
            Directory.CreateDirectory("Library/WrathBossArtwork");
            ScreenCapture.CaptureScreenshot("Library/WrathBossArtwork/warning.png");
            yield return new WaitForEndOfFrame();
            var sameHud = hud;
            string previousBattle = host.Display.BattleId;
            yield return PrepareCards("standard:1:2");
            ChooseTarget("hand-1");
            Assert.AreEqual(1, host.Player.Play(host.Actions.Hand.CaptureSelection(), true));
            Assert.IsFalse(WrathBossQuery.Read(host.AuthorityState.Duel).WarningActive, "Real positive hand damage immediately cancels authority's release.");
            yield return Idle();
            AssertWarningVisible(false);
            Assert.IsTrue(Marker("hand-1").gameObject.activeInHierarchy);
            ScreenCapture.CaptureScreenshot("Library/WrathBossArtwork/severed.png");
            yield return new WaitForEndOfFrame();
            Assert.AreEqual("8", Marker("hand-1").Find("Countdown").GetComponent<TMP_Text>().text);
            Assert.AreEqual(38, Resource("enemy", "wrath.attack"));
            yield return EndTurn();
            CollectionAssert.AreEqual(new long[] { 38 }, BodyHits().Select(hit => hit.SettledDamage),
                "Round 6 retains its regular attack and omits the cancelled ultimate.");

            // Use the actual HUD restart listener, restoring the default saved content for the new battle.
            host.ConfigureContent(originalXml, Encounter);
            sawOpening = false;
            hud.RestartButton.onClick.Invoke();
            yield return Idle();
            Assert.AreSame(sameHud, Object.FindObjectsOfType<SingleBattleHud>().Single());
            Assert.AreNotEqual(previousBattle, host.Display.BattleId);
            Assert.IsTrue(sawOpening, "The same HUD must show a fresh entrance warning for a new battle identity.");
            Assert.IsFalse(Marker("hand-1").gameObject.activeInHierarchy);
            AssertWarningVisible(false);
            Assert.AreEqual(0, host.State.Duel.CompletedRounds);
            Assert.AreEqual(33, Resource("enemy", "wrath.attack"));
            Assert.IsTrue(host.Enemies.All(actor => actor.DisplayUnit.IsAlive));
            Assert.AreEqual(originalText, originalXml.text);
            Assert.AreEqual(ContentPath, AssetDatabase.GetAssetPath(host.ContentXml));
            CollectionAssert.AreEqual(originalScene, File.ReadAllBytes(ScenePath), "A runtime fixture must not save over the independent scene asset.");
            AssertSynchronized();
        }

        [UnityTest]
        public IEnumerator DefaultBindingShowsRoundFiveWarningBeforeRoundSixRegularAndExtraUltimateImpacts()
        {
            yield return LoadBoss();
            yield return AdvanceThrough(4);
            Assert.AreEqual(43, Resource("enemy", "wrath.attack"));
            Assert.AreEqual(734, host.Player.DisplayUnit.Health);
            var healthFrames = new List<long>();
            host.PresentationDirector.Display.Changed += update => healthFrames.Add(update.Current.Unit("player").Health);
            sawWarning = false;
            yield return EndTurn();
            Assert.AreEqual(6, host.State.Duel.CompletedRounds, "Default round-5 binding skips the next player window in the same command.");
            Assert.IsTrue(sawWarning, "Inspect intermediate presented HUD frames, not only the final post-ultimate snapshot.");
            CollectionAssert.AreEqual(new long[] { 43, 43, 129 }, BodyHits().Select(hit => hit.SettledDamage));
            CollectionAssert.Contains(healthFrames, 691L);
            CollectionAssert.Contains(healthFrames, 648L);
            CollectionAssert.Contains(healthFrames, 519L);
            Assert.Less(healthFrames.IndexOf(691), healthFrames.IndexOf(648));
            Assert.Less(healthFrames.IndexOf(648), healthFrames.IndexOf(519));
            Assert.AreEqual(519, host.Player.DisplayUnit.Health);
            AssertWarningVisible(false);
            Assert.AreEqual("guard", host.Display.Labels["wrath.intentId/0"], "The extra ultimate cannot advance the ordinary sequence a second time.");
            Assert.AreEqual(4, Resource("enemy", "wrath.cooldown"));
            AssertSynchronized();
        }

        [UnityTest]
        public IEnumerator RealStraightConsumesTwoBodyImmunitiesAndRoyalEliminationStillWinsThroughFreshShell()
        {
            yield return LoadBoss();
            yield return AdvanceThrough(3);
            Assert.AreEqual(2, ShellStacks());
            yield return PrepareCards("standard:1:2", "standard:2:3", "standard:3:4", "standard:4:5", "standard:1:6");
            ChooseTarget("enemy");
            Assert.AreEqual(5, host.Player.Play(host.Actions.Hand.CaptureSelection(), true));
            yield return Idle();
            Assert.AreEqual(HandCategory.Straight, host.LastPlayResolution.Score.Category);
            CollectionAssert.AreEqual(new long[] { 0, 0, 8, 10, 12 }, host.LastPlayResolution.Events
                .Where(hit => hit.Effect.Kind == CombatEffectKind.Damage && hit.Effect.Target.Value == "enemy")
                .Select(hit => hit.SettledDamage));
            Assert.AreEqual(1470, host.Display.Unit("enemy").Health);
            Assert.AreEqual(0, ShellStacks());
            Assert.IsTrue(host.Enemies.Skip(1).All(actor => actor.DisplayUnit.Health == 1), "Choosing the body keeps a surviving chain on the body.");

            // Restart and earn a fresh shell through three real phases before exercising royal bypass.
            hud.RestartButton.onClick.Invoke(); yield return Idle();
            yield return AdvanceThrough(3);
            Assert.AreEqual(2, ShellStacks());
            yield return PrepareCards("standard:1:10", "standard:1:11", "standard:1:12", "standard:1:13", "standard:1:14");
            Assert.IsEmpty(host.PlayTargetChoices, "An all-enemy royal play has no artificial single-target slot.");
            Assert.AreEqual(5, host.Player.Play(host.Actions.Hand.CaptureSelection(), true));
            yield return Idle();
            Assert.AreEqual(ScoredDuelPhase.Won, host.State.Duel.Phase);
            Assert.AreEqual(6, host.LastPlayResolution.Events.Count(hit => hit.Effect.Kind == CombatEffectKind.Eliminate));
            Assert.AreEqual(0, host.Display.Unit("enemy").Health);
            Assert.IsTrue(hud.ResultPanel.activeInHierarchy);
            Assert.IsFalse(Find("Wrath Boss").gameObject.activeInHierarchy);
            AssertSynchronized();
        }

        [UnityTest]
        public IEnumerator RealReviverDuringRoundSixNormalAttackStopsThePendingUltimateAndLateDeathPose()
        {
            yield return LoadBoss();
            yield return AdvanceThrough(4);
            Apply(new BattleAdminPatch(vitals: new[] { new BattleAdminVitals(host.Player.ParticipantId, health: 44) },
                relics: new[] { new BattleAdminRelics(host.Player.ParticipantId, new[] { new BattleAdminRelic("13004", charges: 1) }) },
                reason: "Wrath PlayMode: survive round 5 with 1 HP; revive on round 6 regular hit"));
            yield return Idle();
            var visual = host.Player.Visual;
            Vector3 scale = visual.localScale; Quaternion rotation = visual.localRotation;
            var healthFrames = new List<long>();
            host.PresentationDirector.Display.Changed += update => healthFrames.Add(update.Current.Unit("player").Health);
            yield return EndTurn();
            CollectionAssert.AreEqual(new long[] { 43, 43 }, BodyHits().Select(hit => hit.SettledDamage),
                "A lethal regular attack plus revival must abort before the extra 129 damage.");
            CollectionAssert.Contains(healthFrames, 1L);
            CollectionAssert.Contains(healthFrames, 0L);
            CollectionAssert.Contains(healthFrames, 480L);
            Assert.Less(healthFrames.IndexOf(0), healthFrames.IndexOf(480));
            Assert.AreEqual(480, host.Player.DisplayUnit.Health);
            Assert.IsFalse(host.State.Duel.Player(host.Player.ParticipantId).Relics.Relics.Any(item => item.DefinitionId == "13004"));
            AssertWarningVisible(false);
            Assert.AreEqual("strike-2", host.Display.Labels["wrath.intentId/0"], "The interrupted ordinary action is retained.");
            yield return new WaitForSecondsRealtime(.5f);
            Assert.AreEqual(480, host.Player.DisplayUnit.Health);
            Assert.Less(Vector3.Distance(scale, visual.localScale), .00001f);
            Assert.Less(Quaternion.Angle(rotation, visual.localRotation), .001f);
            AssertSynchronized();
        }

        [UnityTest]
        public IEnumerator HudGrenadeSeversFiveHandsWithoutActionsConsumesTwoShellLayersAndRegeneratesSameFiveActorsTogether()
        {
            yield return LoadBoss();
            yield return AdvanceThrough(3);
            Assert.AreEqual(2, ShellStacks());
            var hands = host.Enemies.Skip(1).ToArray();
            Apply(new BattleAdminPatch(relics: new[] { new BattleAdminRelics(host.Player.ParticipantId,
                new[] { new BattleAdminRelic("11004", charges: 3) }) }, reason: "Wrath PlayMode real grenade inventory"));
            yield return Idle(); yield return Ready();
            string itemId = host.State.Duel.Player(host.Player.ParticipantId).Relics.Relics.Single().InstanceId;
            long actions = Resource("player", "actions");
            hud.BattleToolsButton.onClick.Invoke();
            var select = ToolsButton("一串手雷 · 次数 3");
            Assert.IsTrue(select.interactable); select.onClick.Invoke();
            Assert.AreEqual(itemId, hud.SelectedItemId);
            for (int use = 0; use < 3; use++)
            {
                var button = ToolsButton("使用 一串手雷");
                Assert.IsTrue(button.interactable);
                button.onClick.Invoke();
                Assert.IsTrue(host.IsPresenting, "The real HUD item listener must start the authored timeline.");
                yield return Idle(); yield return Ready();
                Assert.AreEqual(actions, Resource("player", "actions"), "Grenades do not spend card actions.");
                var bodyHit = host.LastResolution.Timeline.Select(entry => entry.RuleEvent).OfType<CombatEvent>()
                    .Single(hit => hit.Effect.Kind == CombatEffectKind.Damage && hit.Effect.Target.Value == "enemy");
                Assert.AreEqual(use < 2 ? 0 : 20, bodyHit.SettledDamage);
                Assert.AreEqual(use == 0 ? 1 : 0, ShellStacks());
                Assert.IsTrue(hands.All(actor => actor.DisplayUnit.Health == 0));
                if (use == 0)
                    foreach (var hand in hands)
                    {
                        Assert.IsTrue(Marker(hand.ParticipantId.Value).gameObject.activeInHierarchy);
                        Assert.AreEqual("8", Marker(hand.ParticipantId.Value).Find("Countdown").GetComponent<TMP_Text>().text);
                    }
            }
            Assert.AreEqual(1480, host.Display.Unit("enemy").Health);
            Assert.IsFalse(host.State.Duel.Player(host.Player.ParticipantId).Relics.Relics.Any(item => item.InstanceId == itemId));
            Assert.AreEqual(8, Resource("enemy", "wrath.attack"), "All five hands die in round 4 before its first charge: D is still 8.");
            Assert.AreEqual(0, WrathBossQuery.Read(host.State.Duel).Charges);
            yield return AdvanceThrough(11);
            Assert.IsTrue(hands.All(actor => actor.DisplayUnit.Health == 0));
            yield return EndTurn();
            Assert.AreEqual(12, host.State.Duel.CompletedRounds, "Hands killed in player round 4 return at enemy round 12.");
            CollectionAssert.AreEqual(hands, host.Enemies.Skip(1));
            Assert.IsTrue(hands.All(actor => actor.DisplayUnit.Health == 1));
            foreach (var hand in hands) Assert.IsFalse(Marker(hand.ParticipantId.Value).gameObject.activeInHierarchy);
            var revived = host.LastResolution.Timeline.Select(entry => entry.RuleEvent).OfType<CombatEvent>()
                .Where(hit => hit.Effect.Kind == CombatEffectKind.Revive).Select(hit => hit.Effect.Target.Value);
            CollectionAssert.AreEquivalent(new[] { "hand-1", "hand-2", "hand-3", "hand-4", "hand-5" }, revived);
            Assert.AreEqual(63, Resource("enemy", "wrath.attack"));
            Assert.AreEqual(1500, host.Display.Unit("enemy").Health, "Four regenerated blood hands immediately restore the grenade's 20 HP deficit.");
            AssertSynchronized();
        }

        [UnityTest]
        public IEnumerator RealFullHouseBeforeDefaultFifthBindingShowsControlAndImmunityAndKeepsTheNextPlayerWindow()
        {
            yield return LoadBoss();
            yield return AdvanceThrough(4);
            string intent = host.Display.Labels["wrath.intentId/0"];
            long health = host.Player.DisplayUnit.Health;
            yield return PrepareCards("standard:1:2", "standard:2:2", "standard:3:2", "standard:1:3", "standard:2:3");
            ChooseTarget("enemy");
            Assert.AreEqual(5, host.Player.Play(host.Actions.Hand.CaptureSelection(), true));
            yield return Idle();
            Assert.AreEqual(HandCategory.FullHouse, host.LastPlayResolution.Score.Category);
            Assert.AreEqual(0, ShellStacks(), "Both positive full-house damage segments consume the body's remaining shell.");
            Assert.AreEqual(1500, host.Display.Unit("enemy").Health);
            Assert.IsTrue(host.Display.Unit("enemy").Statuses.Any(status => status.Id == "stun"), "Zero final damage cannot suppress the attached control.");
            Assert.IsTrue(host.Display.Unit("player").Statuses.Any(status => status.Id == "control-immunity"));
            var statusText = new SerializedObject(hud).FindProperty("playerStatuses").objectReferenceValue as TMP_Text;
            Assert.NotNull(statusText); StringAssert.Contains("控制免疫", statusText.text);
            Assert.IsTrue(host.Enemies.Skip(1).All(actor => !actor.DisplayUnit.IsAlive));
            yield return EndTurn();
            Assert.AreEqual(5, host.State.Duel.CompletedRounds, "The full-house combination prevents default fifth-round binding from skipping the player window.");
            Assert.AreEqual(health, host.Player.DisplayUnit.Health);
            Assert.IsEmpty(BodyHits());
            Assert.IsTrue(host.LastResolution.Timeline.Select(entry => entry.RuleEvent).OfType<CombatActionWindowSkippedEvent>()
                .Any(skipped => skipped.Actor.Value == "enemy"));
            Assert.AreEqual(intent, host.Display.Labels["wrath.intentId/0"]);
            Assert.IsFalse(host.Display.Unit("player").Statuses.Any(status => status.Id == "stun"));
            // FullHouse also kills the binding hand. This is the real combined counterplay,
            // not an isolated assertion that immunity rejected an application from a dead hand.
            AssertSynchronized();
        }

        [UnityTest]
        public IEnumerator RealLethalFlushWinsWithFiveLivingHandsAndStillPlaysItsAttachedHealing()
        {
            yield return LoadBoss();
            Apply(new BattleAdminPatch(vitals: new[] { new BattleAdminVitals(host.Player.ParticipantId, health: 100),
                new BattleAdminVitals(host.Enemies[0].ParticipantId, health: 1) }, reason: "Wrath PlayMode lethal flush and attached heal"));
            yield return Idle();
            yield return PrepareCards("standard:1:2", "standard:1:4", "standard:1:6", "standard:1:8", "standard:1:10");
            ChooseTarget("enemy");
            Assert.IsTrue(host.Engine.PreviewHandFacts(host.State, host.Player.ParticipantId,
                host.Actions.Hand.CaptureSelection().Cards).IsFlush,
                "Modern scoring keeps the rank category while treating flush as an attached attribute.");
            var healthFrames = new List<long>();
            host.PresentationDirector.Display.Changed += update => healthFrames.Add(update.Current.Unit("player").Health);
            Assert.AreEqual(5, host.Player.Play(host.Actions.Hand.CaptureSelection(), true));
            yield return Idle();
            Assert.AreEqual(ScoredDuelPhase.Won, host.State.Duel.Phase);
            Assert.AreEqual(0, host.Display.Unit("enemy").Health);
            Assert.IsTrue(host.Enemies.Skip(1).All(actor => actor.DisplayUnit.Health == 1));
            Assert.AreEqual(106, host.Player.DisplayUnit.Health, "The same play completes (2+4+6+8+10)*20%=6 healing after killing the body.");
            CollectionAssert.Contains(healthFrames, 106L);
            Assert.AreEqual(6, host.LastPlayResolution.Events.Single(hit => hit.Effect.Kind == CombatEffectKind.Heal).HealthRestored);
            Assert.IsTrue(hud.ResultPanel.activeInHierarchy);
            AssertSynchronized();
        }

        [UnityTest]
        public IEnumerator DurableHeroFixtureCompletesDefaultTwentyRoundsThenLosesWithoutSpendingReviver()
        {
            yield return LoadBoss();
            var originalXml = host.ContentXml;
            string originalText = originalXml.text;
            var document = new XmlDocument(); document.LoadXml(originalText);
            var hero = document.SelectSingleNode("/BattleContent/Players/Player[@id='hero']");
            Assert.NotNull(hero); Assert.AreEqual("800", hero.Attributes["maxHealth"].Value);
            hero.Attributes["maxHealth"].Value = "100000";
            runtimeContentFixture = new TextAsset(document.OuterXml) { name = "Wrath durable-hero PlayMode fixture" };
            host.ConfigureContent(runtimeContentFixture, Encounter); host.RestartBattle();
            yield return Idle();
            Apply(new BattleAdminPatch(relics: new[] { new BattleAdminRelics(host.Player.ParticipantId,
                new[] { new BattleAdminRelic("13004", charges: 1) }) }, reason: "Wrath PlayMode round-limit reviver inventory"));
            yield return Idle();
            string reviverId = host.State.Duel.Player(host.Player.ParticipantId).Relics.Relics.Single().InstanceId;
            yield return AdvanceThrough(20);
            Assert.AreEqual(ScoredDuelPhase.Lost, host.State.Duel.Phase);
            Assert.AreEqual(20, host.State.Duel.CompletedRounds);
            Assert.AreEqual("RoundLimit", host.Display.Labels["resultReason"]);
            Assert.AreEqual(98933, host.Player.DisplayUnit.Health, "Twenty unmodified normal phases and three extra ultimates total 1067 damage.");
            Assert.AreEqual(5, WrathBossQuery.Read(host.State.Duel).Charges, "The twentieth phase completes its charge before the round-limit loss.");
            Assert.AreEqual(83, Resource("enemy", "wrath.attack"));
            var reviver = host.State.Duel.Player(host.Player.ParticipantId).Relics.Relics.Single(item => item.InstanceId == reviverId);
            Assert.AreEqual(1, reviver.RemainingCharges);
            Assert.IsTrue(hud.ResultPanel.activeInHierarchy);
            Assert.IsFalse(hud.EndTurnButton.gameObject.activeInHierarchy);
            Assert.AreEqual(originalText, originalXml.text, "Only the in-memory hero-health fixture changes; default binding and boss content stay intact.");
            AssertSynchronized();
        }

        private IEnumerator LoadBoss()
        {
            Assert.IsTrue(File.Exists(ScenePath), "Generate the dedicated scene first with 工具/卡牌战斗/示例场景/打开暴怒 Boss（本体与五手）.");
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            host = Object.FindObjectsOfType<SingleBattleSceneAdapter>().Single();
            hud = Object.FindObjectsOfType<SingleBattleHud>().Single();
            Assert.AreEqual(Encounter, host.ContentEncounterId, "Load the saved dedicated scene; do not reconfigure BattleDemo at runtime.");
            Assert.AreEqual(ContentPath, AssetDatabase.GetAssetPath(host.ContentXml));
            Assert.AreEqual(6, host.Enemies.Count);
            Assert.AreSame(host.Session, host.SceneHost.Session);
            Assert.AreSame(host.Playback, host.SceneHost.Playback);
            Assert.IsTrue(host.PresentationDirector.HasConfiguredTimeline);
            // The adapter/HUD subscribed first. Inspect every presented checkpoint as well as rendered frames,
            // so automatic round 5 -> 6 progression cannot hide a short-lived warning from this regression.
            host.PresentationDirector.Display.Changed += update => ObserveHud();
            yield return Idle();
            AssertSynchronized();
        }

        private IEnumerator Idle()
        {
            float deadline = Time.realtimeSinceStartup + 25;
            while (Time.realtimeSinceStartup < deadline)
            {
                ObserveHud();
                yield return new WaitForSecondsRealtime(.01f);
                ObserveHud();
                // Remote scene behavior delays the result UI until 1.5 seconds after playback.
                // A terminal scene is idle only once that real HUD gate has opened.
                if (!host.IsPresenting && (!host.State.Duel.IsFinished || hud.ResultPanel.activeInHierarchy)) break;
            }
            Assert.IsFalse(host.IsPresenting, "The real scene timeline failed to finish within 25 seconds of real time.");
            if (host.State.Duel.IsFinished)
                Assert.IsTrue(hud.ResultPanel.activeInHierarchy, "The terminal result panel did not appear after the authored result delay.");
            Assert.IsNull(host.PresentationDirector.LastError);
        }

        private IEnumerator Ready()
        {
            float deadline = Time.realtimeSinceStartup + 25;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForSecondsRealtime(.01f); ObserveHud();
                if (host.CanEndTurn && hud.EndTurnButton.interactable) break;
            }
            Assert.IsTrue(host.CanEndTurn, "Player input did not return within 25 seconds of real time after timeline/card landing: presenting=" + host.IsPresenting +
                ", displayedAuthority=" + ReferenceEquals(host.State, host.AuthorityState) + ", round=" + host.State.Duel.CompletedRounds);
            Assert.IsTrue(hud.EndTurnButton.interactable, "The visible End Turn button did not reflect the ready input gate within 25 seconds of real time.");
        }

        private IEnumerator EndTurn()
        {
            yield return Ready();
            Assert.IsTrue(hud.EndTurnButton.interactable);
            var before = host.AuthorityState;
            hud.EndTurnButton.onClick.Invoke();
            Assert.AreNotSame(before, host.AuthorityState, "The real HUD listener must submit the command to authority.");
            Assert.IsTrue(host.IsPresenting);
            yield return Idle();
            if (!host.State.Duel.IsFinished && host.State.FlowFor(host.Player.ParticipantId).Window == BattlePlayerWindow.Ending)
            {
                yield return Ready();
                hud.EndTurnButton.onClick.Invoke();
                yield return Idle();
            }
            AssertSynchronized();
        }

        private IEnumerator AdvanceThrough(int round)
        {
            for (int commands = 0; host.State.Duel.CompletedRounds < round && commands < 20; commands++) yield return EndTurn();
            Assert.AreEqual(round, host.State.Duel.CompletedRounds, "The fixture must reach the intended real player window.");
        }

        private IEnumerator PrepareCards(params string[] definitions)
        {
            yield return Ready();
            Apply(new BattleAdminPatch(zones: new[] { new BattleAdminZone(host.Player.ParticipantId, CardZoneKinds.Hand,
                definitions.Select(id => new BattleAdminCard(definitionId: id))) }, reason: "Wrath PlayMode deterministic hand only"));
            yield return Idle(); yield return Ready();
            var hand = host.Actions.Hand;
            foreach (var card in hand.Cards.ToArray()) hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
            hand.Publish(); hand.GetComponent<HandZoneView>().CompleteTransitions(); host.RefreshCandidate();
            yield return null;
            Assert.AreEqual(definitions.Length, host.Candidate.SelectedIds.Count);
        }

        private void Apply(BattleAdminPatch patch)
        {
            var result = host.Playback.ApplyAdmin(host.AuthorityState.Revision, patch, animate: false);
            Assert.IsTrue(result.Accepted, result.Error.ToString());
            host.Actions.Hand.GetComponent<HandZoneView>().CompleteTransitions();
        }

        private void ChooseTarget(string id)
        {
            var button = Target(id); Assert.IsTrue(button.interactable, "Live target " + id + " must be selectable.");
            button.onClick.Invoke();
            Assert.AreEqual(id, host.PlayTargetChoices.Single().SelectedId);
            StringAssert.Contains("✓", button.GetComponentInChildren<TMP_Text>().text);
        }

        private void AssertIntents(string[] ids, string[] names)
        {
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(ids[i], host.Display.Labels["wrath.intentId/" + i]);
                var root = Find("Wrath Boss/Regular Intent " + i);
                Assert.IsTrue(root.gameObject.activeInHierarchy);
                StringAssert.Contains(names[i], root.Find("Action").GetComponent<TMP_Text>().text);
                Assert.IsTrue(root.Find("Icon").GetComponent<Image>().enabled || root.Find("Placeholder").GetComponent<WrathIntentPlaceholderGraphic>().enabled);
            }
        }

        private void AssertWarningVisible(bool active)
        {
            Assert.AreEqual(active, host.GetComponent<WrathBossArtworkLayout>().WarningVisible,
                "The PSD red-glow layer must follow the displayed warning and clear after interruption/restart.");
            var root = Find("Wrath Boss/Ultimate Warning");
            Assert.AreEqual(active, root.GetComponent<Outline>().enabled);
            if (active) StringAssert.Contains(UltimateText, root.Find("Warning").GetComponent<TMP_Text>().text);
            else StringAssert.DoesNotContain(UltimateText, root.Find("Warning").GetComponent<TMP_Text>().text);
        }

        private void ObserveHud()
        {
            if (hud == null) return;
            var opening = hud.transform.Find("Wrath Opening Warning/Warning");
            if (opening != null && opening.gameObject.activeInHierarchy && opening.GetComponent<TMP_Text>().text == OpeningText) sawOpening = true;
            var warning = hud.transform.Find("Wrath Boss/Ultimate Warning");
            if (warning != null && warning.gameObject.activeInHierarchy && warning.GetComponent<Outline>().enabled &&
                warning.Find("Warning").GetComponent<TMP_Text>().text.Contains(UltimateText)) sawWarning = true;
            foreach (var text in hud.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TMP_Text>()))
                if (text.name == "Regeneration Float" && text.gameObject.activeInHierarchy && text.alpha > 0) regenerationFloats.Add(text.text);
        }

        private Transform Find(string path)
        {
            var result = hud.transform.Find(path); Assert.NotNull(result, "Missing real HUD view: " + path); return result;
        }
        private TMP_Text Text(string path) => Find(path).GetComponent<TMP_Text>();
        private Button ToolsButton(string title)
        {
            var result = Find("Battle Tools/Details/Viewport/Content").GetComponentsInChildren<Button>()
                .SingleOrDefault(button => button.name == title);
            Assert.NotNull(result, "Missing real item control: " + title); return result;
        }
        private Button Target(string id) => Find("Wrath Boss/Wrath Target " + id).GetComponent<Button>();
        private Transform Marker(string id)
        {
            // WorldUIRoot deliberately lives at the scene root to avoid Canvas/actor scale distortion.
            var result = hud.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .SingleOrDefault(value => value.name == "Wrath Regeneration " + id);
            Assert.NotNull(result, "Missing hand world marker: " + id); return result;
        }
        private long Resource(string id, string kind)
        {
            var resource = host.Display.Resource(id, kind); Assert.NotNull(resource, id + "/" + kind); return resource.Value;
        }
        private long ShellStacks() => host.Display.Unit("enemy").Statuses.Where(status => status.Id == "wrath-shell").Sum(status => status.Stacks);
        private CombatEvent[] BodyHits() => host.LastResolution.Timeline.Select(entry => entry.RuleEvent).OfType<CombatEvent>()
            .Where(hit => hit.Effect.Kind == CombatEffectKind.Damage && hit.Effect.Source.Value == "enemy" && hit.Effect.Target.Value == "player").ToArray();
        private void AssertSynchronized()
        {
            Assert.AreSame(host.AuthorityState, host.State);
            Assert.AreEqual(host.AuthorityState.Revision, host.Display.RuleRevision);
            foreach (var actor in host.Players.Cast<BattleActor>().Concat(host.Enemies))
            {
                var authority = host.AuthorityState.Duel.Combat.Get(actor.ParticipantId);
                Assert.AreEqual(authority.Health, host.Display.Unit(actor.ParticipantId.Value).Health, actor.ParticipantId + " snapshot health");
                Assert.AreEqual(authority.Health, actor.DisplayUnit.Health, actor.ParticipantId + " actor health");
                Assert.AreEqual(authority.Block, actor.DisplayUnit.Block, actor.ParticipantId + " block");
            }
            Assert.IsNull(host.PresentationDirector.LastError);
        }
        private void CaptureError(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (message == "[MCP Unity] WebSocket error: An error has occurred in closing the connection.") return;
            errors.Add(message + "\n" + stack);
        }
    }
}
#endif
