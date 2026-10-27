using System;
using System.Linq;
using NUnit.Framework;
using Poker.BattleHosting.Editor;
using PokerEngine;
using UnityEditor;
using UnityEngine;

namespace Poker.BattleHosting.Tests
{
    public sealed class XmlBattleSessionSourceTests
    {
        [TestCase("Battle1", "single-chapter-battle-1", 1)]
        [TestCase("Battle2", "single-chapter-battle-2", 1)]
        [TestCase("Gatekeeper", "single-chapter-gatekeeper", 1)]
        [TestCase("Wrath", "single-chapter-wrath", 6)]
        public void CollectsTextAssetsAndStartsOneIndependentBattle(string entry, string encounter, int enemies)
        {
            var go = new GameObject("XML source test");
            try
            {
                var source = go.AddComponent<XmlBattleSessionSource>();
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/SingleChapter/" + entry + ".xml");
                Assert.NotNull(asset);
                source.Configure(asset, encounter);
                XmlBattleSessionSourceEditor.CollectImports(source);
                Assert.AreEqual(9, source.Modules.Count);
                Assert.IsTrue(source.Modules.All(m => m.Asset != null));
                var session = source.Build("unity-bundle-test", HmacDeckShuffler.FromSeed(new byte[32]));
                Assert.IsFalse(session.State.HasStarted);
                var player = new ParticipantId("player");
                Assert.IsTrue(session.Execute(0, player, CardDuelAction.Start).Accepted);
                Assert.AreEqual(8, session.State.Zone(CardZoneKinds.Hand, player).Cards.Count);
                Assert.AreEqual(enemies, session.State.Duel.Enemies.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test] public void MissingDependencyReportsSourceAndFailedCollectionKeepsBindings()
        {
            var go = new GameObject("XML source test");
            try
            {
                var source = go.AddComponent<XmlBattleSessionSource>();
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/SingleChapter/Wrath.xml");
                source.Configure(asset, "single-chapter-wrath");
                Assert.Throws<InvalidOperationException>(() => source.Build());
                Assert.IsTrue(source.Diagnostics.Any(d => d.Code == "IMPORT_READ" && d.Location.SourceName == "Wrath.xml"));
                XmlBattleSessionSourceEditor.CollectImports(source);
                var previous = source.Modules;
                source.Configure(asset, "missing-encounter", source.Modules.ToArray());
                Assert.Throws<InvalidOperationException>(() => XmlBattleSessionSourceEditor.CollectImports(source));
                CollectionAssert.AreEqual(previous, source.Modules);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
