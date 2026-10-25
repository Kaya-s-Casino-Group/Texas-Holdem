using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle.Tests
{
    public sealed class EliteBleedingReadabilityTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly string path;
            private readonly UnityEngine.SceneManagement.Scene scene;
            private readonly TextAsset content;
            public readonly SingleBattleSceneAdapter Host;
            public readonly SingleBattleHud Hud;
            public readonly TMP_Text Status;
            public readonly BattleDemoEnemyWorldHud World;
            private readonly Transform worldRoot;

            public Fixture(string bleedingRate = "0.3", bool testExpiryAlongsideAnotherStatus = false, Action<XDocument> configure = null)
            {
                path = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/__EliteBleedingReadabilityTest.unity");
                Assert.IsTrue(AssetDatabase.CopyAsset("Assets/Scenes/SingleChapterAdventure.unity", path));
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                Host = All<SingleBattleSceneAdapter>().Single();
                for (var t = Host.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
                var xml = XDocument.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/SingleChapterBattle.xml").text);
                xml.Root.Element("Statuses").Elements("Status").Single(s => (string)s.Attribute("id") == "elite-bleeding")
                    .Element("IncomingAttackDamagePerStack").SetAttributeValue("rate", bleedingRate);
                if (testExpiryAlongsideAnotherStatus)
                {
                    // Only this fixture adds one ordinary attack, allowing the real lifecycle to reach zero.
                    // The untouched production sequence is exercised by the other tests.
                    var stage = xml.Root.Element("Enemies").Elements("Enemy").Single(e => (string)e.Attribute("id") == "summoner")
                        .Element("Behavior").Element("Stage");
                    stage.Elements("Action").First().Element("Effects").Add(XElement.Parse("<Effect id='test-reflect' kind='ApplyStatus' statusRef='shield-reflect' stacks='1'/>"));
                    var third = new XElement(stage.Elements("Action").Last()); third.SetAttributeValue("id", "test-extra-strike"); stage.Add(third);
                }
                configure?.Invoke(xml);
                content = new TextAsset(xml.ToString());
                AttachFreshEncounter();
                Hud = All<SingleBattleHud>().Single();
                Status = (TMP_Text)typeof(SingleBattleHud).GetField("playerStatuses", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Hud);
                Assert.NotNull(Status);
                World = Host.GetComponent<BattleDemoEnemyWorldHud>();
                Invoke(World, "Start");
                worldRoot = scene.GetRootGameObjects().Single(r => r.name == "BattleDemo Enemy World UI").transform;
                Refresh();
            }
            public void AttachFreshEncounter()
            {
                var compiled = BattleContentCompiler.CompileXml(content.text, "bleeding-ui-test");
                var assembled = BattleContentFactory.Create(compiled.Content,
                    new BattleSetupRequest("single-chapter-gatekeeper", "bleeding-ui-test", HmacDeckShuffler.FromSeed(new byte[32])));
                Assert.IsTrue(assembled.Success);
                var session = assembled.Session;
                var opening = session.Execute(session.State.Revision, session.State.Duel.PlayerId, CardDuelAction.Start);
                Assert.IsTrue(opening.Accepted);
                Host.ConfigureContent(content, "single-chapter-gatekeeper");
                Host.AttachCommands(new BattleSessionCommands(session), opening, false);
            }
            private T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
            public void Round()
            {
                var player = Host.Player.ParticipantId;
                var result = Host.Session.Execute(Host.AuthorityState.Revision, player, CardDuelAction.EndTurn);
                Assert.IsTrue(result.Accepted, result.Error.ToString());
                if (Host.AuthorityState.FlowFor(player).Window == BattlePlayerWindow.Ending)
                    Assert.IsTrue(Host.Session.Execute(Host.AuthorityState.Revision, player, CardDuelAction.FinishTurn).Accepted);
                Refresh();
            }
            public void Refresh()
            {
                Host.SynchronizeBattle(); Host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().CompleteTransitions();
                Hud.GetComponent<DeskBattleLayout>().Apply(); Hud.GetComponent<BattleAuthoredUILayout>().Apply();
                Hud.Refresh(); Invoke(World, "Refresh"); Invoke(World, "LateUpdate"); Canvas.ForceUpdateCanvases();
            }
            public Image Icon => Status.GetComponentsInChildren<Image>(true).SingleOrDefault(i => i.name == "Bleeding Status Icon");
            public void Dispose()
            {
                if (Host != null) { Host.SnapToState(); Host.Actions.Hand.StopSession(); }
                if (worldRoot != null) UnityEngine.Object.DestroyImmediate(worldRoot.gameObject);
                EditorSceneManager.CloseScene(scene, true); AssetDatabase.DeleteAsset(path);
                UnityEngine.Object.DestroyImmediate(content);
            }
        }
        private static void Invoke(object value, string method) => value.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, null);
        private static string Plain(string value) => Regex.Replace(Regex.Replace(value, "<[^>]+>", ""), @"\s+", "");

        [Test]
        public void EliteForecastUsesChineseBleedingCaptionAndTheExistingBloodDropArt()
        {
            using (var f = new Fixture())
            {
                var preview = f.Host.Engine.Duel.PreviewEnemyAction(f.Host.State.Duel, f.Host.Enemies.Single().ParticipantId);
                var badge = BattleDemoEnemyHudModel.Intents(preview).Single(b => b.Description.Contains("施加状态"));
                Assert.That(badge.Value, Is.EqualTo("3"));
                Assert.That(badge.Description, Does.Contain("流血").And.Not.Contain("elite-bleeding"));
                Assert.That(badge.Symbol, Is.Not.EqualTo(BattleDemoEnemySymbol.Unknown).And.Not.EqualTo(BattleDemoEnemySymbol.LifeSteal));
                var tooltip = f.World.GetComponentsInChildren<BattleDemoEnemyBadgeTooltip>(true)
                    .FirstOrDefault(t => t.Description != null && t.Description.Contains("施加状态") && t.Description.Contains("流血"));
                // World badges live under their generated canvas rather than the host component.
                if (tooltip == null) tooltip = UnityEngine.Object.FindObjectsOfType<BattleDemoEnemyBadgeTooltip>(true)
                    .SingleOrDefault(t => t.gameObject.scene == f.Host.gameObject.scene && t.Description != null && t.Description.Contains("施加状态") && t.Description.Contains("流血"));
                Assert.NotNull(tooltip);
                Assert.That(tooltip.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("流血")), Is.True);
                var image = tooltip.GetComponentsInChildren<Image>().Single(i => i.name == "Concept Icon");
                Assert.That(image.sprite, Is.SameAs(ConceptBattleSkin.Get("Icons/icon_bleeding")));
            }
        }

        [Test]
        public void BloodBarCueTracksCommittedStacksAndFitsItsExistingRectangle()
        {
            using (var f = new Fixture())
            {
                var rect = f.Status.rectTransform;
                var size = rect.sizeDelta; var position = rect.anchoredPosition; var anchors = rect.anchorMin;
                Assert.IsFalse(f.Status.gameObject.activeSelf);
                f.Round(); AssertStatus(f, 2, 60);
                f.Round(); AssertStatus(f, 1, 30);
                f.Round(); AssertStatus(f, 3, 90);
                Assert.That(rect.sizeDelta, Is.EqualTo(size)); Assert.That(rect.anchoredPosition, Is.EqualTo(position)); Assert.That(rect.anchorMin, Is.EqualTo(anchors));
                f.Status.ForceMeshUpdate(true, true);
                Assert.That(f.Status.textBounds.size.x, Is.LessThanOrEqualTo(rect.rect.width + .01f), "Real CJK glyph bounds must fit the authored blood-bar status slot.");
                var glyphs = f.Status.textInfo.characterInfo.Take(f.Status.textInfo.characterCount).Where(c => c.isVisible).ToArray();
                Assert.That(glyphs.Length, Is.GreaterThan(0));
                Assert.That(glyphs.All(c => c.bottomLeft.x >= rect.rect.xMin - .01f && c.topRight.x <= rect.rect.xMax + .01f), Is.True,
                    "The rendered glyph positions including the icon indentation must fit, even with TMP Overflow mode.");
                var statusBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rect, f.Icon.rectTransform);
                Assert.That(statusBounds.min.x, Is.GreaterThanOrEqualTo(rect.rect.xMin - .01f));
                Assert.That(statusBounds.max.x, Is.LessThanOrEqualTo(rect.rect.xMax + .01f));
                Assert.That(statusBounds.min.y, Is.GreaterThanOrEqualTo(rect.rect.yMin - .01f));
                Assert.That(statusBounds.max.y, Is.LessThanOrEqualTo(rect.rect.yMax + .01f));
                foreach (string sibling in new[] { "Health Bar", "Block Icon" })
                {
                    var other = rect.parent.Find(sibling) as RectTransform; Assert.NotNull(other);
                    Assert.IsFalse(ScreenRect(f.Icon.rectTransform).Overlaps(ScreenRect(other)), "Bleeding must not cover " + sibling);
                }
            }
        }

        [Test]
        public void BloodBarCueReadsCatalogRateAndClearsWhenTheEncounterIsReplaced()
        {
            using (var f = new Fixture("0.5"))
            {
                f.Round(); AssertStatus(f, 2, 100);
                f.AttachFreshEncounter(); f.Refresh();
                Assert.IsFalse(f.Status.gameObject.activeSelf);
                Assert.IsTrue(f.Icon == null || !f.Icon.gameObject.activeInHierarchy);
            }
        }

        [Test]
        public void ExpiredBleedingHidesItsIconAndPreservesOtherPlayerStatuses()
        {
            using (var f = new Fixture(testExpiryAlongsideAnotherStatus: true))
            {
                f.Round(); AssertStatus(f, 2, 60);
                Assert.That(f.Status.text, Does.Contain("盾反"));
                f.Round(); AssertStatus(f, 1, 30);
                f.Round();
                Assert.That(f.Host.Display.Unit(f.Host.Display.ObserverId).Statuses.Any(s => s.Id == "elite-bleeding"), Is.False);
                Assert.IsTrue(f.Status.gameObject.activeInHierarchy);
                Assert.That(f.Status.text, Does.Contain("盾反").And.Not.Contain("流血"));
                Assert.NotNull(f.Icon); Assert.IsFalse(f.Icon.gameObject.activeSelf);
            }
        }

        [Test]
        public void BleedingForecastNamesTheAlreadyAdjustedDamageBeforeBlockAndHealthCaps()
        {
            using (var f = new Fixture())
            {
                var player = f.Host.Player.ParticipantId;
                Assert.IsTrue(f.Host.Session.ApplyAdmin(f.Host.AuthorityState.Revision,
                    new BattleAdminPatch(vitals: new[] { new BattleAdminVitals(player, health: 100000, maxHealth: 100000) })).Accepted);
                for (int round = 0; round < 5; round++) f.Round();
                AssertStatus(f, 4, 120);
                Assert.IsTrue(f.Host.Session.ApplyAdmin(f.Host.AuthorityState.Revision,
                    new BattleAdminPatch(vitals: new[] { new BattleAdminVitals(player, health: 44, maxHealth: 144, block: 20) })).Accepted);
                f.Refresh();
                var before = f.Host.Session.State;
                var enemy = f.Host.Enemies.Single().ParticipantId;
                var preview = f.Host.Engine.Duel.PreviewEnemyAction(f.Host.State.Duel, enemy);
                Assert.IsTrue(preview.Accepted);
                var hit = preview.Hits.Single(item => item.Effect.Source == enemy);
                Assert.AreEqual(72, hit.SettledAmount, "33 attack with +120% bleeding is floored once, before shield or health limits.");
                Assert.AreEqual(20, hit.BlockAbsorbed);
                Assert.AreEqual(44, hit.DamageToHealth, "The forecast value is a threat amount, not actual health deducted.");
                var trigger = hit.IncomingModification.Triggers.Single(item => item.SourceId == "incoming.attack-per-stack");
                var badge = BattleDemoEnemyHudModel.Intents(preview).Single(item => item.Symbol == BattleDemoEnemySymbol.Sword);
                Assert.AreEqual("72", badge.Value);
                Assert.That(badge.Description, Does.Contain("预计伤害").And.Contain("格挡前").And.Contain("已含流血加伤"));
                Assert.That(badge.Description, Does.Contain(trigger.Reason), "Explain the recorded 33→72 and +39 contribution instead of reconstructing it by division.");
                Assert.AreSame(before, f.Host.Session.State, "Explaining a forecast must never commit or multiply damage again.");

                var attack = UnityEngine.Object.FindObjectsOfType<BattleDemoEnemyBadgeTooltip>(true).Single(item =>
                    item.gameObject.scene == f.Host.gameObject.scene && item.Description != null && item.Description.Contains("预计伤害") &&
                    item.Description.Contains(trigger.Reason));
                Assert.That(Plain(attack.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Action Caption").text),
                    Does.Contain("预计伤害"), "The actual world badge must label its number as a damage forecast.");
            }
        }

        [TestCase(72, 5)]
        [TestCase(160, 1)]
        public void NarrowEliteDamageCaptionFitsBesideTheActualSwordAndTwoOrThreeDigitGlyphs(int expectedDamage, int rounds)
        {
            using (var f = new Fixture(configure: xml =>
            {
                var behavior = xml.Root.Element("Enemies").Elements("Enemy").Single(enemy => (string)enemy.Attribute("id") == "summoner").Element("Behavior");
                if (expectedDamage == 160)
                {
                    behavior.SetAttributeValue("attack", "100"); behavior.SetAttributeValue("attackPerRound", "0");
                }
                // A second announced effect uses the real 104px multi-intent layout.
                // The test still obtains damage through the ordinary turn resolver.
                behavior.Element("Stage").Elements("Action").Single(action => (string)action.Attribute("id") == "strike")
                    .Element("Effects").Add(XElement.Parse("<Effect id='caption-layout-self-status' kind='ApplyStatus' statusRef='shield-reflect' stacks='1' target='Self'/>"));
            }))
            {
                var player = f.Host.Player.ParticipantId;
                Assert.IsTrue(f.Host.Session.ApplyAdmin(f.Host.AuthorityState.Revision,
                    new BattleAdminPatch(vitals: new[] { new BattleAdminVitals(player, health: 100000, maxHealth: 100000) })).Accepted);
                for (int round = 0; round < rounds; round++) f.Round();
                f.Refresh();
                var attack = UnityEngine.Object.FindObjectsOfType<BattleDemoEnemyBadgeTooltip>(true).Single(item =>
                    item.gameObject.scene == f.Host.gameObject.scene && item.gameObject.activeInHierarchy &&
                    item.Description != null && item.Description.Contains("预计伤害") &&
                    item.GetComponentsInChildren<TMP_Text>().Any(text => text.name == "Value" && text.text == expectedDamage.ToString()));
                var badge = (RectTransform)attack.transform;
                Assert.That(badge.rect.width, Is.EqualTo(104).Within(.01f), "Exercise the authored narrow elite-intent width without resizing its contents in the test.");
                var caption = attack.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Action Caption");
                var value = attack.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Value");
                var icon = attack.GetComponentsInChildren<Image>().Single(image => image.name == "Concept Icon");
                Assert.That(Plain(caption.text), Does.Contain("预计伤害"));
                var captionGlyphs = WorldGlyphBounds(caption);
                var valueGlyphs = WorldGlyphBounds(value);
                Assert.IsFalse(captionGlyphs.Overlaps(valueGlyphs), "The actual CJK caption glyphs must not overlap the resolved damage digits.");
                Assert.IsFalse(captionGlyphs.Overlaps(ScreenRect(icon.rectTransform)), "The caption must not paint over the sword artwork.");
                var frame = ScreenRect(badge);
                Assert.That(captionGlyphs.xMin, Is.GreaterThanOrEqualTo(frame.xMin - .01f));
                Assert.That(captionGlyphs.xMax, Is.LessThanOrEqualTo(frame.xMax + .01f));
                Assert.That(captionGlyphs.yMin, Is.GreaterThanOrEqualTo(frame.yMin - .01f));
                Assert.That(captionGlyphs.yMax, Is.LessThanOrEqualTo(frame.yMax + .01f));
            }
        }

        [Test]
        public void ZeroDamageForecastDoesNotClaimBleedingWasIncludedJustBecauseThePlayerHasStacks()
        {
            using (var f = new Fixture(configure: xml =>
            {
                var behavior = xml.Root.Element("Enemies").Elements("Enemy").Single(enemy => (string)enemy.Attribute("id") == "summoner").Element("Behavior");
                var hit = behavior.Element("Stage").Elements("Action").Single(action => (string)action.Attribute("id") == "strike")
                    .Element("Effects").Elements("Effect").Single(effect => (string)effect.Attribute("kind") == "Damage");
                hit.SetAttributeValue("amountKind", "Constant"); hit.SetAttributeValue("amount", "0");
            }))
            {
                f.Round(); AssertStatus(f, 2, 60);
                var before = f.Host.Session.State;
                var preview = f.Host.Engine.Duel.PreviewEnemyAction(f.Host.State.Duel, f.Host.Enemies.Single().ParticipantId);
                Assert.IsTrue(preview.Accepted);
                Assert.AreEqual(0, preview.Hits.Single().SettledAmount,
                    "The engine may still record a zero-to-zero modifier; it must not announce extra damage to the player.");
                var badge = BattleDemoEnemyHudModel.Intents(preview).Single(item => item.Symbol == BattleDemoEnemySymbol.Sword);
                Assert.AreEqual("0", badge.Value);
                Assert.That(badge.Description, Does.Contain("预计伤害").And.Contain("格挡前").And.Not.Contain("已含流血"));
                Assert.AreSame(before, f.Host.Session.State);
            }
        }

        [Test]
        public void ANewBleedingApplicationDoesNotMislabelTheEarlierUnmodifiedHit()
        {
            using (var f = new Fixture())
            {
                var preview = f.Host.Engine.Duel.PreviewEnemyAction(f.Host.State.Duel, f.Host.Enemies.Single().ParticipantId);
                Assert.IsTrue(preview.Accepted);
                var badges = BattleDemoEnemyHudModel.Intents(preview);
                Assert.That(badges.Any(item => item.Symbol == BattleDemoEnemySymbol.Bleeding), Is.True);
                var attack = badges.Single(item => item.Symbol == BattleDemoEnemySymbol.Sword);
                Assert.AreEqual("4", attack.Value);
                Assert.That(attack.Description, Does.Contain("预计伤害").And.Contain("格挡前").And.Not.Contain("已含流血"));
            }
        }

        private static void AssertStatus(Fixture f, int stacks, int percent)
        {
            Assert.That(f.Host.Display.Unit(f.Host.Display.ObserverId).Statuses.Single(s => s.Id == "elite-bleeding").Stacks, Is.EqualTo(stacks));
            Assert.IsTrue(f.Status.gameObject.activeInHierarchy);
            Assert.That(Plain(f.Status.text.Split('\n')[0]), Does.Contain("流血" + stacks + "层").And.Contain("加伤已计入意图").And.Not.Contain("%"));
            Assert.NotNull(f.Icon, "The persistent cue needs the existing bleeding icon, not just a paragraph.");
            Assert.IsTrue(f.Icon.gameObject.activeInHierarchy);
            Assert.That(f.Icon.sprite, Is.SameAs(ConceptBattleSkin.Get("Icons/icon_bleeding")));
            var detail = f.Icon.GetComponent<BattleDemoEnemyBadgeTooltip>().Description;
            Assert.That(Plain(detail), Does.Contain("承受敌方攻击伤害+" + percent + "%").And.Contain("已计入敌方伤害预告").And.Contain("无需再次计算"));
            Assert.That(detail, Does.Contain("护盾抵挡前"), "Keep the percent explanation in hover details and distinguish it from health loss.");
        }
        private static Rect ScreenRect(RectTransform transform)
        {
            var corners = new Vector3[4]; transform.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static Rect WorldGlyphBounds(TMP_Text text)
        {
            text.ForceMeshUpdate(true, true);
            var visible = text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(character => character.isVisible).ToArray();
            Assert.That(visible, Is.Not.Empty, "Check rendered glyphs rather than empty text rectangles.");
            var corners = visible.SelectMany(character => new[] { character.bottomLeft, character.topLeft, character.topRight, character.bottomRight })
                .Select(point => text.transform.TransformPoint(point)).ToArray();
            return Rect.MinMaxRect(corners.Min(point => point.x), corners.Min(point => point.y), corners.Max(point => point.x), corners.Max(point => point.y));
        }
    }
}
