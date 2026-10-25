using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CardInteraction;
using CardPresentation;
using NUnit.Framework;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Tests
{
    public sealed class SevenStationSelectionPreviewTests
    {
        // These tests catch missing first-screen scoring/effect information by reading the
        // same live TMP output as the desk layout. All previews use the real battle pipeline.
        private sealed class Fixture : IDisposable
        {
            private readonly Scene scene = EditorSceneManager.NewPreviewScene();
            private readonly TextAsset content;
            public readonly SingleBattleSceneAdapter Host;
            public readonly CardZonePlayHost Hand;
            public readonly HandZoneView HandView;
            public readonly BattleDemoOutcomePreview Preview;
            public readonly TMP_Text Calculation;
            public readonly GameObject Popup;

            public Fixture(bool legacy = false, bool immuneEnemies = false, int? fullHouseBaseMultiplier = null)
            {
                var root = New("Selection preview host");
                var match = root.AddComponent<CardMatchHost>(); match.ConfigureRuntimeManaged();
                var hand = New("Hand"); HandView = hand.AddComponent<HandZoneView>();
                HandView.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                Hand = hand.AddComponent<CardZonePlayHost>(); Hand.ConfigureRuntimeBinding(match);
                var ui = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/GameAssets/CardPresentation/Prefabs/HandActionPanel.prefab"), scene);
                var panel = ui.GetComponent<HandActionPanelView>();
                panel.Configure(panel.PlayButton, panel.DiscardButton, panel.RankSortButton, panel.SuitSortButton,
                    panel.PlayButton.GetComponentInChildren<TMP_Text>(), panel.DiscardButton.GetComponentInChildren<TMP_Text>(),
                    panel.RankSortButton.GetComponentInChildren<TMP_Text>(), panel.SuitSortButton.GetComponentInChildren<TMP_Text>());
                var actions = ui.GetComponent<HandActionPresenter>() ?? ui.AddComponent<HandActionPresenter>(); actions.Configure(Hand, panel);
                Host = root.AddComponent<SingleBattleSceneAdapter>(); Host.Configure(match, actions, null, null);
                var player = New("Player").AddComponent<PlayerBattleActor>(); player.Configure(Host);
                var enemies = Enumerable.Range(0, 3).Select(index =>
                { var enemy = New("Enemy " + index).AddComponent<EnemyBattleActor>(); enemy.Configure(Host); return enemy; }).ToArray();
                Host.ConfigureActors(new[] { player }, enemies);
                var xml = XDocument.Parse(File.ReadAllText("External/BattleToolkit/Engine/Content/" +
                    (legacy ? "StandardSingleBattle-2026-10-21.xml" : "SingleChapterBattle.xml")));
                if (fullHouseBaseMultiplier.HasValue)
                {
                    foreach (var handRule in xml.Root.Element("ScoringProfiles").Descendants("Hand").Where(item =>
                        (string)item.Attribute("category") == "ThreeOfAKind" || (string)item.Attribute("category") == "OnePair"))
                        handRule.SetAttributeValue("multiplier", fullHouseBaseMultiplier.Value);
                    xml.Root.Element("HandRules").Elements("HandRule").Single().SetAttributeValue("fullHouseTripleBaseMultiplier", fullHouseBaseMultiplier.Value);
                }
                var definition = xml.Root.Element("Enemies").Elements("Enemy").Single(item => (string)item.Attribute("id") == "rotator");
                definition.RemoveNodes(); definition.SetAttributeValue("maxHealth", 10000); definition.SetAttributeValue("damage", 0);
                if (immuneEnemies)
                {
                    definition.SetAttributeValue("damage", null);
                    definition.Add(XElement.Parse("<Behavior><Stage id='guard' mode='Sequence'><Action id='shell'><Effects>" +
                        "<Effect id='shell' kind='ApplyStatus' statusRef='wrath-shell' stacks='2' target='Self'/>" +
                        "</Effects></Action></Stage></Behavior>"));
                }
                xml.Root.Element("Loadouts").Elements("Loadout").Single(item => (string)item.Attribute("id") == "baseline").RemoveNodes();
                string encounterId = legacy ? "standard-single-battle" : "single-chapter-battle-1";
                var encounter = xml.Root.Element("Encounters").Elements("Encounter").Single(item => (string)item.Attribute("id") == encounterId);
                encounter.Elements("Enemy").Remove();
                encounter.Add(Enumerable.Range(0, 3).Select(index => new XElement("Enemy",
                    new XAttribute("id", "enemy-" + index), new XAttribute("ref", "rotator"))));
                content = new TextAsset(xml.ToString()); Host.ConfigureContent(content, encounterId);
                Editor.BattlePresentationSceneSetup.ConfigureHost(Host, includeFeedback: false); Host.StartBattle(); HandView.CompleteTransitions();
                var preview = New("Selection preview", true);
                Preview = preview.AddComponent<BattleDemoOutcomePreview>();
                var title = Label(preview, "Outcome Hand"); var outcome = Label(preview, "Outcome Value"); var details = Label(preview, "Outcome Details");
                outcome.rectTransform.sizeDelta = new Vector2(284, 37);
                // The authored desk leaves only Outcome Value visible.
                title.gameObject.SetActive(false); details.gameObject.SetActive(false);
                Popup = New("Outcome Calculation", true); Popup.transform.SetParent(preview.transform, false);
                Calculation = Label(Popup, "Calculation Text"); Popup.SetActive(false);
                Preview.Configure(Host, title, outcome, details, Calculation, Popup);
            }
            private GameObject New(string name, bool rect = false)
            { var value = rect ? new GameObject(name, typeof(RectTransform)) : new GameObject(name); SceneManager.MoveGameObjectToScene(value, scene); return value; }
            private TMP_Text Label(GameObject parent, string name)
            {
                var value = New(name, true); value.transform.SetParent(parent.transform, false);
                var label = value.AddComponent<TextMeshProUGUI>(); label.fontSize = 19; return label;
            }
            public void Edit(BattleAdminPatch patch)
            { var result = Host.Playback.ApplyAdmin(Host.AuthorityState.Revision, patch); Assert.IsTrue(result.Accepted, result.Error.ToString()); HandView.CompleteTransitions(); }
            public void Cards(params BattleAdminCard[] cards)
            {
                Edit(new BattleAdminPatch(zones: new[] { new BattleAdminZone(Host.Player.ParticipantId, CardZoneKinds.Hand, cards) }));
                foreach (var card in Hand.Cards) Hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
                Hand.Publish(); HandView.CompleteTransitions(); Host.RefreshCandidate(); Refresh();
            }
            public void Refresh() => typeof(BattleDemoOutcomePreview).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Preview, null);
            public string Visible => Plain(Preview.OutcomeText);
            public void Dispose()
            { Hand.StopSession(); EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(content); }
        }
        private static BattleAdminCard Card(int rank, int suit = 1) => new BattleAdminCard(definitionId: "standard:" + suit + ":" + rank);
        private static string Plain(string value) => Regex.Replace(Regex.Replace(value, "<[^>]+>", ""), @"\s+", "");

        [Test]
        public void PairShowsItsLivePointsAndMultiplierInTheVisibleOutcome()
        {
            using (var f = new Fixture())
            {
                f.Cards(Card(7), Card(7, 2));
                Assert.That(f.Visible, Does.Contain("14×2"));
                Assert.That(f.Visible, Does.Contain("伤害28"));
                var before = f.Host.AuthorityState; f.Refresh(); f.Preview.OnPointerEnter(null);
                Assert.IsTrue(f.Popup.activeSelf); Assert.That(Plain(f.Calculation.text), Does.Contain("14×2"));
                f.Preview.OnPointerExit(null); Assert.IsFalse(f.Popup.activeSelf); Assert.AreSame(before, f.Host.AuthorityState);
            }
        }

        [Test]
        public void StraightShowsComboAndPoisonTogetherWithTheRealScore()
        {
            using (var f = new Fixture())
            {
                f.Cards(Card(2), Card(3, 2), Card(4, 3), Card(5, 4), Card(6));
                Assert.That(f.Visible, Does.Contain("20×3"));
                Assert.That(f.Visible, Does.Contain("连击").And.Contain("中毒"));
                Assert.That(f.Preview.DetailsText, Does.Contain("5 次命中"));
            }
        }

        [TestCase(false, "35×5", "治疗17")]
        [TestCase(true, "35×15", "治疗28")]
        public void FlushShowsEffectiveMultiplierAndHealingEvenAtFullHealth(bool relics, string formula, string heal)
        {
            using (var f = new Fixture())
            {
                if (relics) f.Edit(new BattleAdminPatch(relics: new[] { new BattleAdminRelics(f.Host.Player.ParticipantId,
                    new[] { new BattleAdminRelic("10001"), new BattleAdminRelic("11002") }) }));
                f.Cards(Card(2), Card(4), Card(7), Card(9), Card(13));
                Assert.That(f.Visible, Does.Contain(formula));
                Assert.That(f.Visible, Does.Contain("同花").And.Contain(heal));
                Assert.That(f.Preview.DetailsText, Does.Contain("实际恢复 0"));
            }
        }

        [Test]
        public void FullHouseShowsSeparateEffectiveComponentScoresAndAllCoreEffects()
        {
            using (var f = new Fixture())
            {
                f.Cards(Card(2), Card(2, 2), Card(2, 3), Card(3), Card(3, 2));
                Assert.IsTrue(f.Host.SelectPlayTarget(f.Host.PlayTargetChoices.Single().Slot, f.Host.Enemies[1].ParticipantId.Value)); f.Refresh();
                Assert.That(f.Visible, Does.Contain("三条6×4").And.Contain("对子6×4"));
                Assert.That(f.Visible, Does.Contain("群攻").And.Contain("眩晕").And.Contain("净化").And.Contain("控制免疫"));
                Assert.That(f.Preview.DetailsText, Does.Contain("怪物2 生命 10000→9952"));
                Assert.IsTrue(f.Host.SelectPlayTarget(f.Host.PlayTargetChoices.Single().Slot, f.Host.Enemies[2].ParticipantId.Value)); f.Refresh();
                Assert.That(f.Preview.DetailsText, Does.Contain("怪物3 生命 10000→9952"));
                Assert.That(f.Preview.DetailsText, Does.Contain("怪物2 生命 10000→9976"));
            }
        }

        [TestCase(false, "三条6×4", "对子6×4", "群攻96")]
        [TestCase(true, "三条42×8", "对子26×8", "群攻1216")]
        public void ShortFooterKeepsBothFullHouseMultipliersAndItsFirstEffectReadable(bool highCardsAndRelic,
            string triple, string pair, string firstEffect)
        {
            using (var f = new Fixture())
            {
                var label = PrepareShortFooter(f);
                if (highCardsAndRelic) f.Edit(new BattleAdminPatch(relics: new[] { new BattleAdminRelics(f.Host.Player.ParticipantId,
                    new[] { new BattleAdminRelic("13003") }) }));
                f.Cards(highCardsAndRelic
                    ? new[] { Card(14), Card(14, 2), Card(14, 3), Card(13), Card(13, 2) }
                    : new[] { Card(2), Card(2, 2), Card(2, 3), Card(3), Card(3, 2) });
                Assert.IsTrue(f.Host.SelectPlayTarget(f.Host.PlayTargetChoices.Single().Slot, f.Host.Enemies[1].ParticipantId.Value)); f.Refresh();
                string rendered = AssertShortFooterReadable(label);
                Assert.That(rendered, Does.Contain(triple).And.Contain(pair).And.Contain(firstEffect));
                Assert.That(Plain(f.Calculation.text), Does.Contain("眩晕").And.Contain("净化").And.Contain("控制免疫"));
            }
        }

        [Test]
        public void ShortFooterBoundsTheFirstEffectWhenConfiguredComponentMultipliersAreWide()
        {
            using (var f = new Fixture(fullHouseBaseMultiplier: 100))
            {
                var label = PrepareShortFooter(f);
                f.Edit(new BattleAdminPatch(relics: new[] { new BattleAdminRelics(f.Host.Player.ParticipantId,
                    new[] { new BattleAdminRelic("13003") }) },
                    vitals: f.Host.Enemies.Select(enemy => new BattleAdminVitals(enemy.ParticipantId, health: 10000000, maxHealth: 10000000))));
                f.Cards(Card(14), Card(14, 2), Card(14, 3), Card(13), Card(13, 2));
                Assert.IsTrue(f.Host.SelectPlayTarget(f.Host.PlayTargetChoices.Single().Slot, f.Host.Enemies[1].ParticipantId.Value)); f.Refresh();
                string rendered = AssertShortFooterReadable(label);
                Assert.That(rendered, Does.Contain("三条42").And.Contain("对子26").And.Contain("×204").And.Contain("群攻31008"));
                Assert.That(Plain(f.Calculation.text), Does.Contain("三条42×204=8568").And.Contain("对子26×204=5304"));
            }
        }

        private static TMP_Text PrepareShortFooter(Fixture fixture)
        {
            fixture.Preview.gameObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var label = fixture.Preview.GetComponentsInChildren<TMP_Text>(true).Single(text => text.name == "Outcome Value");
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansCJK/TMP/NotoSansCJKSCRegular.asset");
            Assert.IsNotNull(label.font);
            label.rectTransform.sizeDelta = new Vector2(224, 23);
            label.fontSize = label.fontSizeMax = 19; label.fontSizeMin = 13; label.enableAutoSizing = true;
            label.enableWordWrapping = false; label.overflowMode = TextOverflowModes.Overflow; label.margin = Vector4.zero;
            return label;
        }
        private static string AssertShortFooterReadable(TMP_Text label)
        {
            label.ForceMeshUpdate(true, true);
            Assert.That(label.rectTransform.rect.size, Is.EqualTo(new Vector2(224, 23)), "Presentation must retain the supplied footer rectangle.");
            Assert.IsFalse(label.isTextOverflowing, "The real CJK glyphs must fit the 224×23 footer: " + label.text);
            Assert.That(label.textBounds.size.x, Is.LessThanOrEqualTo(224.01f),
                "TMP Overflow mode ignores horizontal overflow flags; the actual rendered glyph bounds must fit: " + label.text);
            var visible = label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(character => character.isVisible).ToArray();
            Assert.That(visible.Length, Is.GreaterThan(0), "The real text mesh must be generated before assessing readability.");
            Assert.That(visible.All(character => character.pointSize >= 13), Is.True, "Both formulas and effect glyphs must remain at least 13 template pixels.");
            return new string(visible.Select(character => character.character).ToArray());
        }

        [Test]
        public void DamageImmunityKeepsTheScoreButReportsThePreventedOutcome()
        {
            using (var f = new Fixture(immuneEnemies: true))
            {
                Assert.IsTrue(f.Host.EndTurn(false)); f.HandView.CompleteTransitions();
                Assert.That(f.Host.State.Duel.Combat.Statuses.Count(status => status.DefinitionId.Value == "wrath-shell"), Is.EqualTo(3));
                f.Cards(Card(7), Card(7, 2));
                Assert.That(f.Visible, Does.Contain("14×2").And.Contain("免疫").And.Contain("伤害0"));
                Assert.That(f.Preview.DetailsText, Does.Not.Contain("生命 10000→9972"));
            }
        }

        [Test]
        public void RoyalFlushKeepsEliminationDistinctFromAnOrdinaryDamageScore()
        {
            using (var f = new Fixture())
            {
                f.Cards(Card(10), Card(11), Card(12), Card(13), Card(14));
                Assert.That(f.Visible, Does.Contain("皇家同花顺").And.Contain("消灭3"));
                Assert.That(f.Visible, Does.Contain("计分60×8").And.Not.Contain("伤害"));
            }
        }

        [Test]
        public void LegacyOutcomeKeepsItsExistingCompactDamagePresentation()
        {
            using (var f = new Fixture(legacy: true))
            {
                f.Cards(Card(7), Card(7, 2));
                Assert.That(f.Visible, Does.Contain("伤害28").And.Not.Contain("×"));
            }
        }

        [Test]
        public void DiscardGainExplicitlyNamesBlockAndPreservesTheAuthoredRow()
        {
            var root = new GameObject("Discard", typeof(RectTransform));
            try
            {
                BattleResourceWidgets.PresentShield(root.transform, null, 12);
                var row = (RectTransform)root.transform.Find("Discard Shield Preview");
                row.anchoredPosition = new Vector2(13, -28);
                Assert.That(row.Find("Shield Count").GetComponent<TMP_Text>().text, Is.EqualTo("格挡+12"));
                BattleResourceWidgets.PresentShield(root.transform, null, 0);
                Assert.That(row.Find("Shield Count").GetComponent<TMP_Text>().text, Is.EqualTo("格挡+0"));
                Assert.That(row.anchoredPosition, Is.EqualTo(new Vector2(13, -28)));
                Assert.That(root.GetComponentsInChildren<TMP_Text>(true).Length, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
