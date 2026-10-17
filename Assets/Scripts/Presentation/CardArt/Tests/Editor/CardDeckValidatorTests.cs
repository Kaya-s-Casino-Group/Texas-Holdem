using System.Linq;
using CardArt.Editor;
using NUnit.Framework;

namespace CardArt.Tests
{
    public sealed class CardDeckValidatorTests
    {
        [TestCase("Assets/Art/spades_a.png", "spades_a")]
        [TestCase("/external/HEART-01.JPG", "hearts_a")]
        [TestCase("folder/02_clubs.png", "clubs_02")]
        [TestCase("黑桃13.png", "spades_k")]
        [TestCase("红桃 Q.png", "hearts_q")]
        [TestCase("diamond_11_v2.png", "diamonds_j")]
        [TestCase("Clubs_12_copy2.png", "clubs_q")]
        [TestCase("spade_A (2).png", "spades_a")]
        [TestCase("diamonds_10_alt.png", "diamonds_10")]
        [TestCase("CARD-BACK_variant3.png", "card_back")]
        [TestCase("big_joker.png", "joker_big")]
        [TestCase("joker_small_副本2.png", "joker_small")]
        [TestCase("大王.png", "joker_big")]
        [TestCase("卡背.png", "card_back")]
        public void NamesResolveToStandardCardIdentity(string path, string expected)
        {
            Assert.IsTrue(CardDeckValidator.TryParse(path, out var id));
            Assert.AreEqual(expected, id);
        }

        [TestCase("spades_00.png")]
        [TestCase("hearts_14.png")]
        [TestCase("joker.png")]
        [TestCase("preview_spades_a.png")]
        [TestCase("spades_a_reference.png")]
        [TestCase("deck_preview.png")]
        public void UnknownOrInvalidNamesRemainExtraCards(string path)
        {
            Assert.IsFalse(CardDeckValidator.TryParse(path, out _));
        }

        [Test]
        public void MissingDuplicatesAndExtrasAreReportedIndividually()
        {
            var cards = new[]
            {
                new CardAtlasEntry { sourcePath = "one/spades_a.png", id = "custom" },
                new CardAtlasEntry { sourcePath = "two/spade_01.png", id = "another" },
                new CardAtlasEntry { sourcePath = "back.png", id = "back" },
                new CardAtlasEntry { sourcePath = "logo.png", id = "logo" }
            };
            var report = CardDeckValidator.Analyze(cards);
            Assert.AreEqual(55, report.slots.Count);
            Assert.AreEqual(53, report.MissingCount);
            Assert.AreEqual(1, report.DuplicateCount);
            Assert.AreEqual(1, report.extras.Count);
            CollectionAssert.AreEqual(cards.Take(2), report.slots.Single(slot => slot.id == "spades_a").entries);
            Assert.AreEqual("logo.png", report.extras.Single().sourcePath);
            Assert.IsFalse(report.HasAllCards);
        }

        [Test]
        public void SelectionIsValidatedSeparatelyAndReplacementKeepsOriginalIdentity()
        {
            var cards = CardDeckValidator.Analyze(new CardAtlasEntry[0]).slots
                .Select(slot => new CardAtlasEntry { sourcePath = slot.id + ".png", id = slot.id }).ToList();
            var ace = cards.Single(card => card.id == "spades_a");
            ace.replacementPath = "art/fox.png";
            ace.id = "my_ace";
            Assert.IsTrue(CardDeckValidator.Analyze(cards).IsExactDeck);
            cards.Add(new CardAtlasEntry { sourcePath = "spades_01_copy.png", id = "spare_ace", included = false });
            cards.Add(new CardAtlasEntry { sourcePath = "extra.png", id = "extra", included = false });
            var all = CardDeckValidator.Analyze(cards);
            Assert.IsTrue(all.HasAllCards);
            Assert.IsFalse(all.IsExactDeck);
            Assert.AreEqual(1, all.DuplicateCount);
            Assert.AreEqual(1, all.extras.Count);
            Assert.IsTrue(CardDeckValidator.Analyze(cards, true).IsExactDeck);
            ace.included = false;
            var selected = CardDeckValidator.Analyze(cards, true);
            Assert.AreEqual(1, selected.MissingCount);
            Assert.AreEqual("spades_a", selected.slots.Single(slot => slot.entries.Count == 0).id);
            Assert.IsTrue(CardDeckValidator.Analyze(cards).HasAllCards);
        }
    }
}
