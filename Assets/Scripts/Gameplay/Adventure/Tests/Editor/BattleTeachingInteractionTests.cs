using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NUnit.Framework;
using Poker.SingleBattle;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.Adventure.Tests
{
    public sealed class BattleTeachingInteractionTests
    {
        private const string TeachingPreference = "Poker.Adventure.BattleTeaching.v1";
        // Catches a missing input gate, an acknowledgement lost on redraw, or a help button that advances combat.
        [Test]
        public void FirstDecisionTeachingBlocksBattleAndSkipRestoresInputWithoutAdvancingAuthority()
        {
            using (var fixture = new Fixture())
            {
                fixture.EnterFirstBattle();
                long revision = fixture.Host.Revision;
                fixture.SettlePresentation();
                Assert.That(Texts(fixture.Screen.transform), Has.Some.EqualTo("首次战斗"));
                Assert.IsTrue(fixture.View.IsHandGuideOpen, "Teaching must use the battle's existing modal input gate.");
                Assert.IsFalse(fixture.View.Player.CanInteract);
                Assert.IsFalse(fixture.View.Player.EndTurn(false));
                Assert.AreEqual(revision, fixture.Host.Revision);

                Click(fixture.Screen.transform, "跳过教学");
                Assert.IsFalse(fixture.View.IsHandGuideOpen);
                Assert.IsTrue(fixture.View.Player.CanInteract, "Dismissing help must restore the original player decision window.");
                Assert.AreEqual(revision, fixture.Host.Revision);
                fixture.Screen.Refresh();
                Assert.That(Texts(fixture.Screen.transform), Has.None.EqualTo("首次战斗"));

                fixture.RecreateScreen();
                Assert.That(Texts(fixture.Screen.transform), Has.None.EqualTo("首次战斗"),
                    "Acknowledged teaching must stay dismissed when the screen is recreated for the same saved run.");
                Assert.AreEqual(revision, fixture.Host.Revision);
            }
        }

        // Catches a tutorial opened while cards are still dealing, which would steal the opening animation's input state.
        [Test]
        public void TeachingWaitsUntilOpeningPresentationHasSettled()
        {
            using (var fixture = new Fixture())
            {
                fixture.EnterFirstBattle();
                Assert.IsTrue(fixture.Host.IsBusy);
                fixture.Screen.Refresh();
                Assert.That(Texts(fixture.Screen.transform), Has.None.EqualTo("首次战斗"));
                Assert.IsFalse(fixture.View.IsHandGuideOpen);
                fixture.SettlePresentation();
                Assert.That(Texts(fixture.Screen.transform), Has.Some.EqualTo("首次战斗"));
                Click(fixture.Screen.transform, "开始战斗");
            }
        }

        [Test]
        public void HandGuideCannotTakeOrReleaseTheTeachingModalInputGate()
        {
            using (var fixture = new Fixture())
            {
                fixture.EnterFirstBattle(); fixture.SettlePresentation();
                var hud = fixture.Hud;
                long revision = fixture.Host.Revision;
                hud.OpenHandGuide();
                Assert.IsFalse(hud.IsHandGuideOpen, "A second modal must not open over teaching's battle input gate.");
                hud.CloseHandGuide();
                Assert.IsTrue(fixture.View.IsHandGuideOpen, "Closing an unopened hand guide must not release another modal's gate.");
                Assert.IsFalse(fixture.View.Player.CanInteract);
                Assert.AreEqual(revision, fixture.Host.Revision);
                Click(fixture.Screen.transform, "开始战斗");
                Assert.IsTrue(fixture.View.Player.CanInteract);
                hud.OpenHandGuide();
                Assert.IsTrue(hud.IsHandGuideOpen, "The guide must still open after teaching is dismissed.");
                hud.CloseHandGuide();
                Assert.IsFalse(fixture.View.IsHandGuideOpen);
            }
        }

        [Test]
        public void TeachingKeepsKeyboardFocusInsideItsTwoActionsAndRestoresPriorActiveFocus()
        {
            using (var fixture = new Fixture())
            {
                var events = fixture.Events; var eventType = events.GetType();
                var current = eventType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                var original = current.GetValue(null);
                // Opening a scene in EditMode does not run this MonoBehaviour's OnEnable.
                // Register the real EventSystem through its lifecycle before testing global focus.
                eventType.GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(events, null);
                try
                {
                    current.SetValue(null, events);
                    Assert.AreSame(events, current.GetValue(null), "The focus fixture must register the EventSystem used by production.");
                    fixture.EnterFirstBattle();
                    var previous = (Component)typeof(SingleBattleHud).GetProperty("EndTurnButton").GetValue(fixture.Hud);
                    eventType.GetMethod("SetSelectedGameObject", new[] { typeof(GameObject) }).Invoke(events, new object[] { previous.gameObject });
                    fixture.SettlePresentation();
                    foreach (bool redraw in new[] { false, true })
                    {
                        if (redraw) fixture.Screen.Refresh();
                        var skip = FindButton(fixture.Screen.transform, "跳过教学");
                        var start = FindButton(fixture.Screen.transform, "开始战斗");
                        var focused = (GameObject)eventType.GetProperty("currentSelectedGameObject").GetValue(events);
                        Assert.That(new[] { skip.gameObject, start.gameObject }, Does.Contain(focused), "Teaching must own the keyboard's selected action.");
                        foreach (var button in new[] { skip, start })
                            foreach (string direction in new[] { "Left", "Right", "Up", "Down" })
                            {
                                var destination = (Component)button.GetType().GetMethod("FindSelectableOn" + direction).Invoke(button, null);
                                Assert.That(new[] { skip, start }, Does.Contain(destination), "Directional selection must stay inside teaching.");
                            }
                    }
                    Click(fixture.Screen.transform, "跳过教学");
                    Assert.AreSame(previous.gameObject, eventType.GetProperty("currentSelectedGameObject").GetValue(events));
                }
                finally
                {
                    eventType.GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(events, null);
                    if (original != null) current.SetValue(null, original);
                }
            }
        }

        // Catches a duplicated help rule table: altered content must change the visible explanation.
        [TestCase("PlayerTurnStart", "下次自己的回合开始时清空")]
        [TestCase("AfterEnemyPhase", "敌人阶段结束后清空")]
        [TestCase("Retain", "跨回合保留")]
        public void MenuHelpReadsActiveSelectionCostsDiscardRefillAndBlockTiming(string expiry, string timing)
        {
            using (var fixture = new Fixture(xml =>
            {
                xml.Root.Element("BattleRules").Elements("Rules").First().SetAttributeValue("blockExpiry", expiry);
                var player = xml.Root.Element("Players").Elements("Player").First();
                player.SetAttributeValue("maxPlayCards", 4);
                player.SetAttributeValue("playEnergyCost", 2);
                player.SetAttributeValue("discardEnergyCost", 2);
                player.SetAttributeValue("discardBlockPerPoint", 2);
            }))
            {
                typeof(AdventureScreen).GetMethod("HowToPlay", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.Screen, null);
                string text = string.Join("\n", Texts(fixture.Screen.transform));
                Assert.That(text, Does.Contain("最多 4 张"));
                Assert.That(text, Does.Contain("出牌消耗 2 点能量"));
                Assert.That(text, Does.Contain("弃牌消耗 2 点能量"));
                Assert.That(text, Does.Contain("弃几张补几张"));
                Assert.That(text, Does.Contain("点数合计 × 2"));
                Assert.That(text, Does.Contain(timing));
                Assert.IsTrue(fixture.Host.IsAtMenu);
                Assert.IsNull(fixture.Host.Adventure, "Reading help must not create or advance a run.");
            }
        }

        // Catches teaching the hidden energy cost as though it were the visible action-point cost.
        [TestCase(true, 1, 2)]
        [TestCase(false, 0, 0)]
        public void TurnFlowHelpKeepsActionPointConsumptionSeparateFromEnergy(bool discardCostsAction, int discardActions, int discardEnergy)
        {
            using (var fixture = new Fixture(xml =>
            {
                var player = xml.Root.Element("Players").Elements("Player").First();
                player.SetAttributeValue("actionsPerTurn", 5);
                player.SetAttributeValue("playEnergyCost", 2);
                player.SetAttributeValue("discardEnergyCost", 2);
                player.SetAttributeValue("discardCostsAction", discardCostsAction);
            }))
            {
                typeof(AdventureScreen).GetMethod("HowToPlay", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.Screen, null);
                string text = string.Join("\n", Texts(fixture.Screen.transform));
                Assert.That(text, Does.Contain("每回合 5 个行动点"));
                Assert.That(text, Does.Contain("出牌消耗 1 个行动点"));
                Assert.That(text, Does.Contain("弃牌消耗 " + discardActions + " 个行动点"));
                Assert.That(text, Does.Contain("另受能量限制"));
                Assert.That(text, Does.Contain("出牌消耗 2 点能量"));
                Assert.That(text, Does.Contain("弃牌消耗 " + discardEnergy + " 点能量"));
            }
        }

        [Test]
        public void StandardTurnFlowHelpUsesVisibleActionPointsWithoutAddingAHiddenEnergyBudget()
        {
            using (var fixture = new Fixture())
            {
                typeof(AdventureScreen).GetMethod("HowToPlay", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.Screen, null);
                string text = string.Join("\n", Texts(fixture.Screen.transform));
                Assert.That(text, Does.Contain("每回合 3 个行动点"));
                Assert.That(text, Does.Contain("出牌消耗 1 个行动点"));
                Assert.That(text, Does.Contain("弃牌消耗 1 个行动点"));
                Assert.That(text, Does.Not.Contain("能量"));
            }
        }

        // Catches opening novice teaching over an already-started saved encounter when no local preference exists.
        [Test]
        public void ResumingMidbattleDoesNotShowFirstDecisionTeachingWhenLocalAcknowledgementIsMissing()
        {
            using (var fixture = new Fixture())
            {
                PlayerPrefs.SetInt(TeachingPreference, 1);
                fixture.EnterFirstBattle(); fixture.SettlePresentation();
                var actor = fixture.Host.Adventure.PlayerId;
                Assert.IsTrue(fixture.Host.Execute(fixture.Host.Revision, actor, CardDuelAction.Discard,
                    fixture.Host.Session.State.Zone(CardZoneKinds.Hand, actor).Cards.Take(1)).Accepted);
                fixture.SettlePresentation();
                Assert.IsNotEmpty(fixture.Host.Adventure.Battle.State.Duel.Actions);
                long revision = fixture.Host.Revision;
                fixture.Host.ReturnToMenu();
                PlayerPrefs.DeleteKey(TeachingPreference);
                Assert.IsTrue(fixture.Host.Resume(), fixture.Host.Message);
                fixture.SettlePresentation(); fixture.RecreateScreen();
                Assert.That(Texts(fixture.Screen.transform), Has.None.EqualTo("首次战斗"));
                Assert.IsFalse(fixture.View.IsHandGuideOpen);
                Assert.IsTrue(fixture.View.Player.CanInteract);
                Assert.AreEqual(revision, fixture.Host.Revision, "Resuming and reading help must not spend an action.");
            }
        }

        // Catches a blanket turn-start expiry explanation that ignores the active retention modifier.
        [Test]
        public void HelpExplainsCurrentRetainBlockRelicAndReturnsToNormalExpiryWhenItIsRemoved()
        {
            using (var fixture = new Fixture(xml => xml.Root.Element("Relics").Add(
                new XElement("Relic", new XAttribute("id", "teaching-retain"), new XAttribute("name", "保盾测试"),
                    new XAttribute("version", "1"), new XElement("RuleValue", new XAttribute("id", "retain"),
                        new XAttribute("key", "RetainBlock"), new XAttribute("delta", 1))))))
            {
                PlayerPrefs.SetInt(TeachingPreference, 1);
                fixture.EnterFirstBattle(); fixture.SettlePresentation();
                var actor = fixture.Host.Adventure.PlayerId;
                Assert.IsTrue(fixture.Host.ApplyAdmin(fixture.Host.Revision, new BattleAdminPatch(relics: new[] {
                    new BattleAdminRelics(actor, new[] { new BattleAdminRelic("teaching-retain") }) })).Accepted);
                fixture.SettlePresentation();
                long revision = fixture.Host.Revision;
                typeof(AdventureScreen).GetMethod("HowToPlay", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.Screen, null);
                string retained = string.Join("\n", Texts(fixture.Screen.transform));
                Assert.That(retained, Does.Contain("当前保盾效果"));
                Assert.That(retained, Does.Contain("跨回合保留"));
                Assert.That(retained, Does.Contain("失去该效果"));
                Assert.That(retained, Does.Not.Contain("下次自己的回合开始时清空"));
                Assert.AreEqual(revision, fixture.Host.Revision);
                Click(fixture.Screen.transform, "关闭");

                Assert.IsTrue(fixture.Host.ApplyAdmin(fixture.Host.Revision, new BattleAdminPatch(relics: new[] {
                    new BattleAdminRelics(actor, Array.Empty<BattleAdminRelic>()) })).Accepted);
                fixture.SettlePresentation();
                typeof(AdventureScreen).GetMethod("HowToPlay", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.Screen, null);
                Assert.That(string.Join("\n", Texts(fixture.Screen.transform)), Does.Contain("下次自己的回合开始时清空"));
            }
        }

        private static string[] Texts(Transform root) => root.GetComponentsInChildren<Component>(false)
            .Where(component => component != null && component.GetType().Name == "TextMeshProUGUI")
            .Select(component => (string)component.GetType().GetProperty("text").GetValue(component)).ToArray();

        private static void Click(Transform root, string caption)
        {
            var button = FindButton(root, caption);
            var click = button.GetType().GetProperty("onClick").GetValue(button);
            click.GetType().GetMethod("Invoke").Invoke(click, null);
        }

        private static Component FindButton(Transform root, string caption) => root.GetComponentsInChildren<Component>(false)
            .Single(component => component != null && component.GetType().Name == "Button" && Texts(component.transform).Contains(caption));

        private sealed class Fixture : IDisposable
        {
            private readonly string copy;
            private readonly Scene scene;
            private readonly TextAsset battle, adventure;
            private readonly bool hadPreference;
            private readonly int preference;
            private GameObject additionalScreen;
            public readonly AdventureSceneHost Host;
            public readonly SingleBattleSceneAdapter View;
            public AdventureScreen Screen { get; private set; }
            public SingleBattleHud Hud => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleHud>(true)).Single();
            public Component Events => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true))
                .Single(component => component != null && component.GetType().Name == "EventSystem");

            public Fixture(Action<XDocument> configure = null)
            {
                hadPreference = PlayerPrefs.HasKey(TeachingPreference); preference = PlayerPrefs.GetInt(TeachingPreference);
                PlayerPrefs.DeleteKey(TeachingPreference);
                copy = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BattleTeachingTest.unity");
                Assert.IsTrue(AssetDatabase.CopyAsset("Assets/Scenes/SingleChapterAdventure.unity", copy));
                scene = EditorSceneManager.OpenScene(copy, OpenSceneMode.Additive);
                var roots = scene.GetRootGameObjects();
                Host = roots.SelectMany(root => root.GetComponentsInChildren<AdventureSceneHost>(true)).Single();
                View = Host.BattleView;
                Screen = roots.SelectMany(root => root.GetComponentsInChildren<AdventureScreen>(true)).Single();
                var source = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/StandardSingleBattle-2026-10-21.xml");
                var xml = XDocument.Parse(source.text); configure?.Invoke(xml);
                battle = new TextAsset(xml.ToString()) { name = "Teaching battle authority" };
                adventure = new TextAsset("<AdventureContent schemaVersion='1' id='teaching-test' version='1' startNode='fight' " +
                    "startingEncounter='standard-single-battle' defaultTarot='fool'><Tarots><Tarot id='fool' initiallyUnlocked='true'/></Tarots><Nodes>" +
                    "<Node id='fight' kind='Battle' chapter='1' encounter='standard-single-battle' next='boss'/>" +
                    "<Node id='boss' kind='Boss' chapter='1' encounter='standard-wrath-thousand-hands'/></Nodes><Shop/><Services/><Rest/></AdventureContent>");
                Host.Configure(battle, adventure, View,
                    roots.Where(root => root.name == "Gameplay" || root.name == "Battle Hand Backdrop").ToArray(), false);
                Host.Initialize();
                typeof(AdventureScreen).GetMethod("Configure").Invoke(Screen, new[] { (object)Host, typeof(AdventureScreen).GetProperty("Font").GetValue(Screen) });
                Screen.Refresh();
            }

            public void EnterFirstBattle()
            {
                Assert.IsTrue(Host.NewRun("fool", 42), Host.Message);
                Assert.IsTrue(Host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, "fight")), Host.Message);
            }

            public void SettlePresentation()
            {
                View.SynchronizeBattle();
                foreach (var hand in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                    .Where(component => component != null && component.GetType().Name == "HandZoneView"))
                    hand.GetType().GetMethod("CompleteTransitions").Invoke(hand, null);
                typeof(AdventureSceneHost).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Host, null);
                Screen.Refresh();
            }

            public void RecreateScreen()
            {
                var font = typeof(AdventureScreen).GetProperty("Font").GetValue(Screen); Screen.gameObject.SetActive(false);
                additionalScreen = new GameObject("Restored teaching screen"); SceneManager.MoveGameObjectToScene(additionalScreen, scene);
                Screen = additionalScreen.AddComponent<AdventureScreen>();
                typeof(AdventureScreen).GetMethod("Configure").Invoke(Screen, new[] { (object)Host, font }); Screen.Refresh();
            }

            public void Dispose()
            {
                if (hadPreference) PlayerPrefs.SetInt(TeachingPreference, preference); else PlayerPrefs.DeleteKey(TeachingPreference);
                PlayerPrefs.Save();
                foreach (var hand in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                    .Where(component => component != null && component.GetType().Name == "CardZonePlayHost"))
                    hand.GetType().GetMethod("StopSession").Invoke(hand, null);
                EditorSceneManager.CloseScene(scene, true); AssetDatabase.DeleteAsset(copy);
                UnityEngine.Object.DestroyImmediate(battle); UnityEngine.Object.DestroyImmediate(adventure);
            }
        }
    }
}
