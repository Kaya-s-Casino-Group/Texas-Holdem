#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
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
    public sealed class HandGuidePerformanceTests
    {
        string scenePath, battlePath;
        AdventureSceneHost host;
        SingleBattleHud hud;
        bool previousBackground;
        const string TeachingPreference = "Poker.Adventure.BattleTeaching.v1";
        bool hadTeachingPreference;
        int previousTeachingPreference;
        static object Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        static IDictionary Cache(SingleBattleHud owner, string name) => (IDictionary)Field(owner, name);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hadTeachingPreference = PlayerPrefs.HasKey(TeachingPreference);
            previousTeachingPreference = PlayerPrefs.GetInt(TeachingPreference);
            PlayerPrefs.SetInt(TeachingPreference, 1);
            previousBackground = Application.runInBackground; Application.runInBackground = true;
            scenePath = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/__HandGuidePerformance.unity");
            Assert.IsTrue(AssetDatabase.CopyAsset("Assets/Scenes/SingleChapterAdventure.unity", scenePath));
            string scene = File.ReadAllText(scenePath);
            Assert.That(scene, Does.Contain("saveAutomatically: 1"));
            if (TestContext.CurrentContext.Test.Name == nameof(LegacyRulesKeepOriginalGuideEntries))
            {
                const string sourceBattle = "Packages/com.kaya.battle-engine/Content/SingleChapterBattle.xml";
                var document = XDocument.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(sourceBattle).text);
                var rule = document.Root.Element("HandRules").Element("HandRule");
                foreach (string name in new[] { "modernScoring", "flushAsAttribute", "wheelAceScoresOne", "pureFlushScoresAllCards", "fullHouseTripleBaseMultiplier" })
                    rule.Attribute(name)?.Remove();
                battlePath = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/__LegacyHandGuide.xml");
                File.WriteAllText(battlePath, document.ToString());
                AssetDatabase.ImportAsset(battlePath, ImportAssetOptions.ForceSynchronousImport);
                scene = scene.Replace(AssetDatabase.AssetPathToGUID(sourceBattle), AssetDatabase.AssetPathToGUID(battlePath));
            }
            File.WriteAllText(scenePath, scene.Replace("saveAutomatically: 1", "saveAutomatically: 0"));
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceSynchronousImport);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            host = Object.FindObjectOfType<AdventureSceneHost>();
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
            if (hadTeachingPreference) PlayerPrefs.SetInt(TeachingPreference, previousTeachingPreference);
            else PlayerPrefs.DeleteKey(TeachingPreference);
            if (hud != null) hud.CloseHandGuide();
            if (scenePath != null)
            {
                var scene = SceneManager.GetSceneByPath(scenePath);
                if (scene.IsValid() && scene.isLoaded)
                {
                    SceneManager.SetActiveScene(SceneManager.CreateScene("Hand guide cleanup"));
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
                AssetDatabase.DeleteAsset(scenePath);
            }
            if (battlePath != null) AssetDatabase.DeleteAsset(battlePath);
            scenePath = battlePath = null;
            Application.runInBackground = previousBackground;
        }
        IEnumerator Idle()
        {
            float until = Time.realtimeSinceStartup + 20;
            while (host.IsBusy && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsFalse(host.IsBusy, "Battle presentation did not finish.");
            yield return null;
        }
        IEnumerator QueryFinished()
        {
            float until = Time.realtimeSinceStartup + 20;
            while (((IList)Field(hud, "guidePreloadJobs")).Count != 0 && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(0, ((IList)Field(hud, "guidePreloadJobs")).Count, "The selected guide search must terminate.");
            Assert.Greater(Cache(hud, "guideTexts").Count, 0);
        }
        void Select(HandGuideType type)
        {
            var buttons = (IDictionary)Field(hud, "guideTypeButtons");
            ((Button)buttons[type]).onClick.Invoke();
        }
        [UnityTest]
        public IEnumerator FlushEntriesExplainRulesEvenWhenTheDeckHasNoCombination()
        {
            var changed = host.BattleView.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(zones: new[] {
                new BattleAdminZone(host.Adventure.PlayerId, CardZoneKinds.Hand, new[] { new BattleAdminCard(definitionId: "standard:1:2") }),
                new BattleAdminZone(host.Adventure.PlayerId, CardZoneKinds.Deck, Array.Empty<BattleAdminCard>()),
                new BattleAdminZone(host.Adventure.PlayerId, CardZoneKinds.Discard, Array.Empty<BattleAdminCard>()) }));
            Assert.IsTrue(changed.Accepted, changed.Error.ToString());
            yield return Idle();
            var state = host.Session.State;
            hud.OpenHandGuide();
            var flush = hud.GetComponentsInChildren<Button>().SingleOrDefault(button => button.GetComponentInChildren<TMP_Text>()?.text == "同花");
            Assert.NotNull(flush, "Flush needs a visible independent guide entry, including when no example can be formed.");
            flush.onClick.Invoke();
            Assert.AreEqual(HandGuideType.HighCard, Field(hud, "guideType"));
            Assert.IsTrue((bool)Field(hud, "guideFlush"));
            Assert.That(((TMP_Text)Field(hud, "guideRuleSummary")).text, Does.Contain("点数合计"));
            Assert.That(((TMP_Text)Field(hud, "guideRuleSummary")).text, Does.Contain("基础治疗50%（未计遗物）"));
            Assert.That(((TMP_Text)Field(hud, "guideFormula")).text, Does.Contain("基础倍率 ×5"));
            yield return CaptureGuide("guide-flush");
            Select(HandGuideType.HighCard);
            Assert.IsFalse((bool)Field(hud, "guideFlush"), "Clicking the ordinary High Card entry must leave Flush mode.");
            Assert.AreEqual("高牌", ((TMP_Text)Field(hud, "guideHeading")).text);
            var straightFlush = hud.GetComponentsInChildren<Button>().SingleOrDefault(button => button.GetComponentInChildren<TMP_Text>()?.text == "同花顺");
            Assert.NotNull(straightFlush);
            straightFlush.onClick.Invoke();
            Assert.AreEqual(HandGuideType.Straight, Field(hud, "guideType"));
            Assert.IsTrue((bool)Field(hud, "guideFlush"));
            Assert.That(((TMP_Text)Field(hud, "guideFormula")).text, Does.Contain("基础倍率 ×15"));
            Assert.That(((TMP_Text)Field(hud, "guideRuleSummary")).text, Does.Contain("同花额外 ×5 · 基础治疗50%（未计遗物）"));
            yield return CaptureGuide("guide-straight-flush");
            Select(HandGuideType.Straight);
            Assert.IsFalse((bool)Field(hud, "guideFlush"), "Clicking the ordinary Straight entry must leave Straight Flush mode.");
            Assert.AreEqual("顺子", ((TMP_Text)Field(hud, "guideHeading")).text);
            straightFlush.onClick.Invoke();
            Select(HandGuideType.OnePair);
            Assert.AreEqual(HandGuideType.OnePair, Field(hud, "guideType"));
            ((Button)Field(hud, "guideFlushButton")).onClick.Invoke();
            Assert.IsFalse((bool)Field(hud, "guideFlush"), "The existing combination filter must remain usable.");
            Assert.AreSame(state, host.Session.State, "Reading rules and changing filters must not mutate the battle.");
        }
        [UnityTest]
        public IEnumerator LegacyRulesKeepOriginalGuideEntries()
        {
            hud.OpenHandGuide();
            yield return null;
            var visible = hud.GetComponentsInChildren<Button>().Select(button => button.GetComponentInChildren<TMP_Text>()?.text).ToArray();
            CollectionAssert.DoesNotContain(visible, "同花", "Attribute-mode shortcuts must not issue HighCard/RequireFlush queries against legacy Flush categories.");
            CollectionAssert.DoesNotContain(visible, "同花顺");
            Assert.AreEqual(Enum.GetValues(typeof(HandGuideType)).Length, ((IDictionary)Field(hud, "guideTypeButtons")).Count);
            Select(HandGuideType.Straight);
            Assert.AreEqual("顺子", ((TMP_Text)Field(hud, "guideHeading")).text);
        }
        static IEnumerator CaptureGuide(string name)
        {
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Directory.CreateDirectory("Library/AdventureValidation");
                File.WriteAllBytes("Library/AdventureValidation/" + name + ".png", image.EncodeToPNG());
            }
            finally { Object.Destroy(image); }
        }
        [UnityTest]
        public IEnumerator OpeningSearchesOnlyTheVisibleConditionAndReopeningReusesItsText()
        {
            var state = host.Session.State;
            hud.OpenHandGuide(); Assert.IsTrue(hud.IsHandGuideOpen);
            Assert.AreEqual(1, ((IList)Field(hud, "guidePreloadJobs")).Count, "Opening must not enqueue every filter.");
            yield return QueryFinished();
            var queries = Cache(hud, "guideQueries");
            Assert.AreEqual(1, queries.Count, "Unvisited filters must never be searched in the background.");
            var texts = Cache(hud, "guideTexts");
            Assert.AreEqual(1, texts.Count, "Only the first displayed example needs a settlement.");
            var iterator = texts.GetEnumerator(); iterator.MoveNext(); var first = iterator.Entry;
            hud.CloseHandGuide();
            Assert.IsFalse(host.BattleView.IsHandGuideOpen);
            hud.OpenHandGuide();
            Assert.AreSame(first.Value, Cache(hud, "guideTexts")[first.Key], "Same snapshot and targets must reuse settled text.");
            Assert.AreSame(state, host.Session.State, "Guide operations are read-only.");
            hud.CloseHandGuide();
        }
        [UnityTest]
        public IEnumerator FilterSwitchesAreLazyAndAChangedSnapshotCannotReuseOldCandidates()
        {
            hud.OpenHandGuide();
            yield return QueryFinished();
            int key = (int)HandGuideType.HighCard * 2;
            var oldResult = (HandGuideResult)Cache(hud, "guideQueries")[key];
            Select(HandGuideType.FourOfAKind);
            yield return null;
            hud.CloseHandGuide();
            Assert.AreEqual(0, ((IList)Field(hud, "guidePreloadJobs")).Count, "Closing must cancel all suspended searches.");
            var changed = host.BattleView.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(zones: new[] {
                new BattleAdminZone(host.Adventure.PlayerId, CardZoneKinds.Hand, Enumerable.Range(2, 5)
                    .Select(rank => new BattleAdminCard(definitionId: "standard:1:" + rank))) }));
            Assert.IsTrue(changed.Accepted, changed.Error.ToString());
            yield return Idle();
            hud.OpenHandGuide(); Select(HandGuideType.HighCard);
            // All nonselected jobs stay paused; only drive this filter to its final cached result.
            float until = Time.realtimeSinceStartup + 20;
            while (!Cache(hud, "guideQueries").Contains(key) && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(Cache(hud, "guideQueries").Contains(key));
            var result = (HandGuideResult)Cache(hud, "guideQueries")[key];
            Assert.AreNotSame(oldResult, result, "The same filter must be recomputed after the authoritative snapshot changes.");
            Assert.That(result.Candidates.Count, Is.GreaterThan(0));
            Assert.IsTrue(result.Candidates.All(candidate => candidate.MissingCount == 0));
            Assert.IsTrue(result.Candidates.All(candidate => candidate.Cards.All(card => card.ZoneKind == CardZoneKinds.Hand)));
            var currentCards = host.Session.State.Zone(CardZoneKinds.Hand, host.Adventure.PlayerId).Cards;
            Assert.IsTrue(result.Candidates.All(candidate => candidate.Cards.All(card => currentCards.Contains(card.Card.Id))),
                "No candidate may retain a card from the replaced hand.");
            Assert.IsFalse(Cache(hud, "guideQueries").Contains((int)HandGuideType.FullHouse * 2), "Unvisited full house stays unqueried.");
            hud.CloseHandGuide();
        }
    }
}
#endif
