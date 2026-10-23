#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using CardInteraction;
using CardPresentation;
using NUnit.Framework;
using Poker.SingleBattle;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Poker.Adventure.Tests
{
    public sealed class HandGuideTargetCacheTests
    {
        const string SourceScene = "Assets/Scenes/SingleChapterAdventure.unity";
        const string SourceBattle = "Packages/com.kaya.battle-engine/Content/SingleChapterBattle.xml";
        const int FullHouseKey = (int)HandGuideType.FullHouse * 2;
        string scenePath, battlePath;
        AdventureSceneHost host;
        SingleBattleHud hud;
        bool previousBackground;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            // Keep the real rules and scene, adding only a second normal enemy in an isolated asset.
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(SourceBattle);
            Assert.NotNull(source);
            var battle = XDocument.Parse(source.text);
            var encounter = battle.Root.Element("Encounters").Elements("Encounter")
                .Single(element => (string)element.Attribute("id") == "single-chapter-battle-1");
            encounter.Add(new XElement("Enemy", new XAttribute("id", "enemy-second"), new XAttribute("ref", "rotator")));
            battlePath = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/__HandGuideTargets.xml");
            File.WriteAllText(battlePath, battle.ToString());
            AssetDatabase.ImportAsset(battlePath, ImportAssetOptions.ForceSynchronousImport);
            scenePath = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/__HandGuideTargets.unity");
            Assert.IsTrue(AssetDatabase.CopyAsset(SourceScene, scenePath));
            string scene = File.ReadAllText(scenePath);
            string sourceGuid = AssetDatabase.AssetPathToGUID(SourceBattle);
            string battleGuid = AssetDatabase.AssetPathToGUID(battlePath);
            Assert.That(sourceGuid, Is.Not.Empty); Assert.That(battleGuid, Is.Not.Empty);
            Assert.That(scene, Does.Contain("saveAutomatically: 1"));
            Assert.That(scene, Does.Contain(sourceGuid));
            File.WriteAllText(scenePath, scene.Replace("saveAutomatically: 1", "saveAutomatically: 0")
                .Replace(sourceGuid, battleGuid));
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceSynchronousImport);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            host = Object.FindObjectOfType<AdventureSceneHost>(); Assert.NotNull(host);
            float until = Time.realtimeSinceStartup + 30;
            while (host.IsLoading && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsFalse(host.IsLoading); Assert.IsFalse(host.HasSavedRun);
            Assert.IsTrue(host.NewRun("fool", 312), host.Message);
            Assert.IsTrue(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, "battle-1")), host.Message);
            yield return Idle();
            hud = Object.FindObjectOfType<SingleBattleHud>(); Assert.NotNull(hud);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (hud != null) hud.CloseHandGuide();
            if (scenePath != null)
            {
                var scene = SceneManager.GetSceneByPath(scenePath);
                if (scene.IsValid() && scene.isLoaded)
                {
                    SceneManager.SetActiveScene(SceneManager.CreateScene("Hand guide target cleanup"));
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
                AssetDatabase.DeleteAsset(scenePath);
            }
            if (battlePath != null) AssetDatabase.DeleteAsset(battlePath);
            Application.runInBackground = previousBackground;
        }

        [UnityTest]
        public IEnumerator ClosedTargetChangeRebuildsSettlementWithoutReplacingSnapshotAndStableTargetReusesIt()
        {
            var view = host.BattleView;
            var firstTarget = new ParticipantId("enemy");
            var secondTarget = new ParticipantId("enemy-second");
            Assert.AreEqual(2, view.Enemies.Count);
            var cardIds = new[] { "standard:1:2", "standard:2:2", "standard:3:2", "standard:1:3", "standard:2:3" };
            var patch = new BattleAdminPatch(
                vitals: new[] {
                    new BattleAdminVitals(firstTarget, health: 100000, maxHealth: 100000, block: 0),
                    new BattleAdminVitals(secondTarget, health: 100000, maxHealth: 100000, block: 1000000) },
                zones: new[] {
                    new BattleAdminZone(view.Player.ParticipantId, CardZoneKinds.Hand,
                        cardIds.Select(id => new BattleAdminCard(definitionId: id))),
                    new BattleAdminZone(view.Player.ParticipantId, CardZoneKinds.Deck, Array.Empty<BattleAdminCard>()),
                    new BattleAdminZone(view.Player.ParticipantId, CardZoneKinds.Discard, Array.Empty<BattleAdminCard>()) },
                relics: new[] { new BattleAdminRelics(view.Player.ParticipantId, Array.Empty<BattleAdminRelic>()) });
            var changed = view.Playback.ApplyAdmin(host.Revision, patch);
            Assert.IsTrue(changed.Accepted, changed.Error.ToString());
            yield return Idle();
            var hand = view.Actions.Hand;
            foreach (var card in hand.Cards.ToArray()) hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
            hand.Publish(); hand.GetComponent<HandZoneView>().CompleteTransitions(); view.RefreshCandidate();
            yield return null;
            Assert.IsTrue(view.SelectPlayTarget(BattleTargetBindings.DefaultSlot, firstTarget.Value));
            var state = view.State;
            Assert.AreSame(state, host.Session.State);

            hud.OpenHandGuide();
            ((Button)((IDictionary)Field(hud, "guideTypeButtons"))[HandGuideType.FullHouse]).onClick.Invoke();
            yield return FullHouseFinished();
            var result = (HandGuideResult)Cache("guideQueries")[FullHouseKey];
            Assert.AreEqual(1, result.Candidates.Count, "The isolated five-card hand has one complete full house.");
            var candidate = result.Candidates.Single();
            Assert.AreEqual(0, candidate.MissingCount);
            var firstText = Cache("guideTexts")[candidate.Id];
            var firstDamage = ExpectedPairDamage(state, candidate, firstTarget);
            Assert.Greater(firstDamage.HealthLost, 0);
            Assert.AreEqual(0, firstDamage.BlockAbsorbed);
            AssertSettlement(firstText, firstDamage);
            hud.CloseHandGuide();

            Assert.IsTrue(view.SelectPlayTarget(BattleTargetBindings.DefaultSlot, secondTarget.Value));
            Assert.AreSame(state, view.State, "Target selection is local input and must not replace the rules snapshot.");
            hud.OpenHandGuide();
            yield return FullHouseFinished();
            Assert.AreSame(result, Cache("guideQueries")[FullHouseKey], "Target changes do not require a new card search.");
            var secondText = Cache("guideTexts")[candidate.Id];
            Assert.AreNotSame(firstText, secondText, "A closed-window target change must invalidate settled text.");
            var secondDamage = ExpectedPairDamage(state, candidate, secondTarget);
            Assert.AreEqual(0, secondDamage.HealthLost);
            Assert.Greater(secondDamage.BlockAbsorbed, 0);
            AssertSettlement(secondText, secondDamage);
            Assert.AreNotEqual(SettlementLines(firstText).Last(), SettlementLines(secondText).Last(),
                "The selected pair damage must now report absorption by the second enemy's shield.");
            hud.CloseHandGuide();

            hud.OpenHandGuide();
            yield return FullHouseFinished();
            Assert.AreSame(secondText, Cache("guideTexts")[candidate.Id], "Reopening with the same target must reuse settled text.");
            AssertSettlement(secondText, secondDamage);
            Assert.AreSame(state, view.State); Assert.AreSame(state, host.Session.State);
        }

        CombatEvent ExpectedPairDamage(CardDuelState state, HandGuideCandidate candidate, ParticipantId target)
        {
            var preview = host.BattleView.Engine.PreviewHandGuide(state, candidate, true,
                targetBindings: new BattleTargetBindings(BattleTargetBindings.DefaultSlot, new[] { target }));
            Assert.AreEqual(CardDuelError.None, preview.Error);
            Assert.AreEqual(CardDuelError.None, preview.SettlementError);
            // Full house first damages all enemies with its triple, then the requested target with its pair.
            var damage = preview.Events.Where(item => item.Effect.Kind == CombatEffectKind.Damage).ToArray();
            Assert.AreEqual(3, damage.Length);
            Assert.AreEqual(target, damage.Last().Effect.Target);
            return damage.Last();
        }
        void AssertSettlement(object text, CombatEvent damage)
        {
            Assert.NotNull(text);
            string expected = "伤害 " + damage.SettledDamage + " · 实际扣血 " + damage.HealthLost + " · 护盾吸收 " + damage.BlockAbsorbed;
            Assert.AreEqual(expected, SettlementLines(text).Last());
            Assert.That(((TMP_Text)Field(hud, "guideFormula")).text, Does.Contain(expected));
        }
        IEnumerator Idle()
        {
            float until = Time.realtimeSinceStartup + 20;
            while (host.IsBusy && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsFalse(host.IsBusy, "Battle presentation did not finish.");
            yield return null;
        }
        IEnumerator FullHouseFinished()
        {
            float until = Time.realtimeSinceStartup + 20;
            while ((!Cache("guideQueries").Contains(FullHouseKey) || HasFullHouseJob()) && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.IsTrue(Cache("guideQueries").Contains(FullHouseKey));
            Assert.IsFalse(HasFullHouseJob(), "The selected full-house search must finish; other filters may stay paused.");
            Assert.Greater(Cache("guideTexts").Count, 0);
        }
        bool HasFullHouseJob() => ((IEnumerable)Field(hud, "guidePreloadJobs")).Cast<object>()
            .Any(job => (int)Field(job, "Key") == FullHouseKey);
        IDictionary Cache(string name) => (IDictionary)Field(hud, name);
        static object Field(object owner, string name) => owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(owner);
        // This test assembly intentionally does not depend on the shared presentation assembly.
        static IEnumerable<string> SettlementLines(object text) => (IEnumerable<string>)text.GetType()
            .GetProperty("SettlementLines").GetValue(text);
    }
}
#endif
