using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Poker.EditorTools.Tests
{
    public sealed class PokerBuildAssetPolicyTests
    {
        [TestCase("Assets/Art/UI/BattleConcept/Resources/BattleConcept/Icons/icon_hand_guide.png", 512)]
        [TestCase("Assets/Art/UI/Desk/Resources/DeskUI/MapPaper.png", 2048)]
        [TestCase("Assets/Art/Characters/BattleBillboards/WrathBoss/Hand1.png", 2048)]
        public void ReviewedArtworkHasRoleAppropriateLimits(string path, int maxSize)
        {
            Assert.That(PokerBuildAssetPolicy.TryGetRule(path, out var rule), Is.True);
            Assert.That(rule.MaxSize, Is.EqualTo(maxSize));
        }

        [TestCase("Assets/Art/CardAtlases/FourBeastsAtlas.png")]
        [TestCase("Assets/Fonts/NotoSansCJK/TMP/NotoSansCJKSCRegular.asset")]
        [TestCase("Assets/Art/UI/BattleConcept/Resources/BattleConcept/Textures/action_blood_liquid.png")]
        [TestCase("Assets/Art/UI/Relics/Resources/BattleRelics.png")]
        [TestCase("Assets/Art/UI/BattleConcept/Resources/BattleConcept/Icons/unreviewed.png")]
        public void FineCardArtDataAndUnreviewedFilesAreExcluded(string path)
        {
            Assert.That(PokerBuildAssetPolicy.TryGetRule(path, out _), Is.False);
        }

        [Test]
        public void MipmapsAreDisabledOnlyForTheFourMeasuredBlockAlignedImages()
        {
            var noMipPaths = PokerBuildAssetPolicy.Paths.Where(path =>
                PokerBuildAssetPolicy.TryGetRule(path, out var rule) && rule.DisableMipmaps);
            Assert.That(noMipPaths, Is.EquivalentTo(new[] {
                "Assets/Art/Characters/BattleBillboards/Elite-Updated.png",
                "Assets/Art/Characters/BattleBillboards/WrathBoss/WarningGlow.png",
                "Assets/Art/Environment/BattleBackgrounds/Sources/Elite_Updated.jpg",
                "Assets/Art/Environment/BattleBackgrounds/Sources/Minion_Updated.jpg"
            }));
        }

        [Test]
        public void CleanupSelectsOnlyInactiveKnownLayersAndRetainsReplacementAndBoss()
        {
            var root = Backgrounds();
            try
            {
                var old = Child(root.transform.Find("Elite - Existing Ruins"), "StylizedRuins Backdrop");
                old.gameObject.SetActive(false);
                var unrelated = Child(root.transform.Find("Elite - Existing Ruins"), "Authored Decoration");
                unrelated.gameObject.SetActive(false);
                Child(root.transform, "Boss - Castle Gate");
                Assert.That(PokerBuildAssetOptimizer.CollectRetiredBackgrounds(root.transform), Is.EqualTo(new[] { old }));
                Object.DestroyImmediate(old.gameObject);
                Assert.That(PokerBuildAssetOptimizer.CollectRetiredBackgrounds(root.transform), Is.Empty);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CleanupRefusesActiveOldArtOrMissingReplacement()
        {
            var root = Backgrounds();
            try
            {
                var old = Child(root.transform.Find("Elite - Existing Ruins"), "StylizedRuins Backdrop");
                Assert.Throws<InvalidOperationException>(() => PokerBuildAssetOptimizer.CollectRetiredBackgrounds(root.transform));
                old.gameObject.SetActive(false);
                Object.DestroyImmediate(root.transform.Find("Minion - Red Wastes/Updated Artwork").gameObject);
                Assert.Throws<InvalidOperationException>(() => PokerBuildAssetOptimizer.CollectRetiredBackgrounds(root.transform));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject Backgrounds()
        {
            var root = new GameObject("Battle Backgrounds");
            foreach (var name in new[] { "Elite - Existing Ruins", "Minion - Red Wastes" })
                Child(Child(root.transform, name), "Updated Artwork").gameObject.AddComponent<MeshRenderer>();
            return root;
        }
        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform; child.SetParent(parent, false); return child;
        }
    }
}
