using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace PokerEngine.Tests
{
    [TestFixture]
    public sealed class SingleBattleLegacyContentIntegrationTests
    {
        [Test] public void SceneXmlRetainsEveryValueFromTheOriginalScoringAsset()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>("Packages/com.kaya.battle-engine/Content/SingleBattleDemoNew.xml");
            Assert.That(asset, Is.Not.Null);
            var result = BattleContentCompiler.CompileXml(asset.text);
            Assert.That(result.Success, Is.True);
            var score = result.Content.ScoringProfiles["single-battle-score"];
            string legacy = File.ReadAllText("Assets/GameAssets/PokerScoring/Profiles/DefaultPokerScore.asset");
            var entries = Regex.Matches(legacy, @"- category: (\d+)\s+chips: (\d+)\s+multiplier: (\d+)\s+chipsPerLevel: (\d+)\s+multiplierPerLevel: (\d+)");
            Assert.AreEqual(10, entries.Count); Assert.AreEqual("score.balatro.1", score.Version);
            foreach (Match entry in entries)
            {
                var values = Enumerable.Range(1, 5).Select(index => int.Parse(entry.Groups[index].Value, CultureInfo.InvariantCulture)).ToArray();
                var rule = score.Rule((HandCategory)values[0]); Assert.AreEqual(values[1], rule.BasePoints); Assert.AreEqual(values[2], rule.BaseMultiplier);
                Assert.AreEqual(values[3], rule.PointsPerLevel); Assert.AreEqual(values[4], rule.MultiplierPerLevel);
            }
            var ranks = Regex.Matches(legacy, @"- rank: (\d+)\s+chips: (\d+)"); Assert.AreEqual(13, ranks.Count);
            foreach (Match rank in ranks) Assert.AreEqual(long.Parse(rank.Groups[2].Value, CultureInfo.InvariantCulture),
                score.CardPoints.GetBasePoints(new CardFace((Rank)int.Parse(rank.Groups[1].Value, CultureInfo.InvariantCulture), Suit.Clubs)));
        }

    }
}
