using System;
using System.Linq;
using PokerEngine;
using CardPresentation.Editor;
using NUnit.Framework;
using UnityEngine;

namespace CardPresentation.Tests
{
    [TestFixture]
    public sealed class CardZonePreviewTests
    {
        [Test] public void ArbitraryInsertionDuplicationAndMovementKeepPropertiesWithIndependentIdentity()
        {
            var model = new CardZonePreviewModel(); model.Add("spades_a"); model.Add("clubs_j");
            var first = model.Cards[0]; var last = model.Cards[1];
            model.Insert(1, "hearts_q", 2, faceUp: false);
            Assert.AreSame(first, model.Cards[0]); Assert.AreSame(last, model.Cards[3]);
            var original = model.Cards[1]; original.Effects = CardEffects.Enhanced | CardEffects.Laminated;
            original.IsDisabled = true; original.IsHovered = true; original.IsSelected = true; original.Tint = Color.cyan;
            model.Duplicate(1, 0); var copy = model.Cards[0];
            Assert.AreNotEqual(original.Id, copy.Id); Assert.AreEqual(original.Effects, copy.Effects);
            Assert.AreEqual(Color.cyan, copy.Tint); Assert.IsTrue(copy.IsDisabled); Assert.IsFalse(copy.FaceUp);
            Assert.IsFalse(copy.IsHovered); Assert.IsFalse(copy.IsSelected);
            model.Move(0, 4); Assert.AreSame(copy, model.Cards.Last());
            model.SetCardProperties(new CardInstanceId(copy.Id), CardZonePreviewCatalog.DefinitionFor("diamonds_a"), CardEffects.None, false);
            Assert.AreEqual("hearts_q", original.ArtId); Assert.AreEqual("diamonds_a", copy.ArtId);
            Assert.AreEqual(5, model.Cards.Select(card => card.Id).Distinct().Count());
            var restored = JsonUtility.FromJson<CardZonePreviewModel>(JsonUtility.ToJson(model));
            CollectionAssert.AreEqual(model.BuildZone().Cards, restored.BuildZone().Cards);
        }

        [Test] public void InvalidListEditsRejectBeforeChangingTheSerializedList()
        {
            var model = new CardZonePreviewModel(); model.Add("spades_a"); var before = JsonUtility.ToJson(model);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Insert(3, "hearts_q", 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Insert(0, "hearts_q", 0));
            Assert.Throws<ArgumentException>(() => model.Insert(0, " "));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Duplicate(3, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Duplicate(0, -1));
            Assert.Throws<InvalidOperationException>(() => model.Insert(0, "hearts_q", 64));
            Assert.AreEqual(before, JsonUtility.ToJson(model));
        }

        [Test] public void RuntimeEditingImportCanPreserveHoverAndOriginWhileStartupImportClearsHover()
        {
            var card = new CardInstance(new CardInstanceId("live"), CardZonePreviewCatalog.DefinitionFor("hearts_q"), "reward/source");
            var state = new CardInteraction.CardZoneCardState(card, "hearts_q", false, Color.red, new CardInteractionState(true, true));
            var model = new CardZonePreviewModel(); model.Load(new[] { state }, clearHover: false);
            Assert.IsTrue(model.Cards[0].IsHovered); Assert.IsTrue(model.Cards[0].IsSelected);
            Assert.AreEqual("reward/source", model.Cards[0].ToCard().OriginId);
            model.Load(new[] { state }); Assert.IsFalse(model.Cards[0].IsHovered);
            Assert.IsTrue(state.Interaction.IsHovered); Assert.IsTrue(model.Cards[0].IsSelected);
        }

        [Test] public void PreviewReorderingPreservesStableIdentitiesAndPerCardAppearance()
        {
            var model = new CardZonePreviewModel();
            model.Add("spades_a"); model.Add("spades_a"); model.Add("hearts_q");
            var ids = model.Cards.Select(c => c.Id).ToArray(); Assert.AreEqual(3, ids.Distinct().Count());
            var first = model.Cards[0]; first.Tint = Color.red;
            model.Move(0, 2); Assert.AreSame(first, model.Cards[2]);
            model.Reverse(); Assert.AreSame(first, model.Cards[0]);
            model.Shuffle(); CollectionAssert.AreEquivalent(ids, model.Cards.Select(c => c.Id));
            Assert.AreEqual(Color.red, model.Cards.Single(c => c.Id == first.Id).Tint);
            var snapshot = model.BuildZone(); model.RemoveAt(1);
            Assert.AreEqual(3, snapshot.Cards.Count); Assert.AreEqual(2, model.Cards.Count);
        }

        [Test] public void PreviewSerializationRestoresUnknownCardsWithoutDisclosingIdentity()
        {
            var model = new CardZonePreviewModel(); model.Add("hearts_q"); model.Add("clubs_j");
            model.Cards[0].FaceUp = false;
            var restored = JsonUtility.FromJson<CardZonePreviewModel>(JsonUtility.ToJson(model));
            var zone = restored.BuildZone(); var hidden = restored.Project(zone.Cards[0]);
            Assert.AreEqual(model.Cards[0].Id, hidden.ViewId); Assert.IsNull(hidden.CardId); Assert.IsNull(hidden.FaceArtId);
            Assert.AreEqual("clubs_j", restored.Project(zone.Cards[1]).FaceArtId);
            restored.Cards[0].FaceUp = true; Assert.AreEqual("hearts_q", restored.Project(zone.Cards[0]).FaceArtId);
        }

        [Test] public void PreviewCardPropertiesSurviveSerializationAndReorderBeforeProjection()
        {
            var model = new CardZonePreviewModel(); model.Add("hearts_q"); model.Add("clubs_j");
            var card = model.Cards[0]; card.Effects = CardEffects.Enhanced | CardEffects.Laminated | CardEffects.Ineffective;
            card.IsDisabled = true; card.IsHovered = true; card.IsSelected = true; card.FaceUp = false;
            model.Move(0, 1);
            var restored = JsonUtility.FromJson<CardZonePreviewModel>(JsonUtility.ToJson(model));
            var restoredCard = restored.Cards[1]; Assert.AreEqual(card.Id, restoredCard.Id);
            Assert.AreEqual((CardEffects)7, restoredCard.ToCard().Effects); Assert.IsTrue(restoredCard.ToCard().IsDisabled);
            Assert.IsFalse(restoredCard.ToCard().CanScore); Assert.IsFalse(restoredCard.ToCard().CanPlay);
            var hidden = restored.Project(restored.BuildZone().Cards[1]);
            Assert.IsTrue(hidden.Interaction.IsHovered); Assert.IsTrue(hidden.Interaction.IsSelected); Assert.IsFalse(hidden.FaceUp);
            restoredCard.FaceUp = true; restoredCard.IsHovered = false;
            var shown = restored.Project(restored.BuildZone().Cards[1]);
            Assert.AreEqual((CardEffects)7, shown.Effects); Assert.IsTrue(shown.IsDisabled); Assert.IsTrue(shown.Interaction.IsRaised);
            Assert.IsTrue(shown.FaceUp); Assert.AreEqual("hearts_q", shown.FaceArtId);
        }

        [Test] public void EffectSamplesUseRuleAttributesAndStableIndependentCardIdentities()
        {
            var model = new CardZonePreviewModel(); model.LoadEffectSamples("hearts_q");
            Assert.AreEqual(8, model.Cards.Count); Assert.AreEqual(8, model.Cards.Select(c => c.Id).Distinct().Count());
            Assert.AreEqual(CardEffects.Enhanced, model.Cards[1].ToCard().Effects);
            Assert.AreEqual(CardEffects.Laminated, model.Cards[2].ToCard().Effects);
            Assert.IsTrue(model.Cards[6].ToCard().IsDisabled); Assert.AreEqual(CardEffects.None, model.Cards[6].ToCard().Effects);
            var projected = model.Project(model.BuildZone().Cards[7]);
            Assert.AreEqual((CardEffects)7, projected.Effects); Assert.IsTrue(projected.IsDisabled);
        }

        [Test] public void PreviewInputLimitsAndInvalidMovesDoNotLoseCards()
        {
            var model = new CardZonePreviewModel();
            for (int i = 0; i < CardZonePreviewModel.MaximumCards; i++) model.Add("spades_a");
            Assert.Throws<InvalidOperationException>(() => model.Add("spades_a"));
            var ids = model.Cards.Select(c => c.Id).ToArray();
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Move(0, 999));
            CollectionAssert.AreEqual(ids, model.Cards.Select(c => c.Id));
            model.Clear(); model.Reverse(); model.Shuffle(); Assert.AreEqual(0, model.BuildZone().Cards.Count);
        }

        [Test] public void CorePropertyEditsRejectInvalidValuesAndPreserveIndependentObservationState()
        {
            var model = new CardZonePreviewModel(); model.Add("spades_a"); model.Add("spades_a");
            var sample = model.Cards[0]; sample.FaceUp = false; sample.IsHovered = true; sample.IsSelected = true; sample.Tint = Color.red;
            var original = sample.ToCard(); var neighbor = JsonUtility.ToJson(model.Cards[1]);
            model.SetCardProperties(original.Id, StandardDeck.CardId(Suit.Hearts, Rank.Queen), CardEffects.Ineffective, true);
            var changed = model.GetCard(original.Id);
            Assert.AreEqual(original.Id, changed.Id); Assert.AreEqual("hearts_q", sample.ArtId);
            Assert.IsFalse(changed.CanPlay); Assert.IsFalse(changed.CanDiscard); Assert.IsFalse(changed.CanScore);
            Assert.AreEqual(CardEffects.None, original.Effects); Assert.IsFalse(original.IsDisabled);
            Assert.IsFalse(sample.FaceUp); Assert.IsTrue(sample.IsHovered); Assert.IsTrue(sample.IsSelected); Assert.AreEqual(Color.red, sample.Tint);
            Assert.AreEqual(neighbor, JsonUtility.ToJson(model.Cards[1]));
            var beforeInvalid = JsonUtility.ToJson(model);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.SetCardProperties(original.Id, changed.DefinitionId, (CardEffects)int.MaxValue, false));
            Assert.Throws<ArgumentException>(() => model.SetCardProperties(original.Id, new CardDefinitionId("missing"), CardEffects.None, false));
            Assert.AreEqual(beforeInvalid, JsonUtility.ToJson(model));
            var restored = JsonUtility.FromJson<CardZonePreviewModel>(beforeInvalid); restored.Reverse();
            Assert.AreEqual(changed.DefinitionId, restored.GetCard(original.Id).DefinitionId);
            Assert.AreEqual(CardEffects.Ineffective, restored.GetCard(original.Id).Effects);
            Assert.IsNull(restored.Project(original.Id).FaceArtId);
        }

        [Test] public void SortingUsesCardDefinitionsPreservesStateAndKeepsArtOnlySamplesAtTheEnd()
        {
            var model = new CardZonePreviewModel();
            foreach (var art in new[] { "joker_big", "hearts_a", "spades_02", "clubs_k", "diamonds_a", "hearts_a", "joker_small" }) model.Add(art);
            var originals = model.Cards.ToArray(); var selected = originals[1];
            selected.FaceUp = false; selected.IsSelected = true; selected.IsHovered = true;
            selected.Effects = CardEffects.Laminated | CardEffects.Ineffective; selected.IsDisabled = true; selected.Tint = Color.red;
            var originalData = originals.ToDictionary(c => c.Id, c => JsonUtility.ToJson(c));
            model.SortBySuit();
            CollectionAssert.AreEqual(new[] { originals[2], originals[1], originals[5], originals[3], originals[4], originals[0], originals[6] }, model.Cards);
            model.SortByRank();
            CollectionAssert.AreEqual(new[] { originals[1], originals[5], originals[4], originals[3], originals[2], originals[0], originals[6] }, model.Cards);
            foreach (var card in model.Cards) Assert.AreEqual(originalData[card.Id], JsonUtility.ToJson(card));
            var hidden = model.Project(new CardInstanceId(selected.Id));
            Assert.IsFalse(hidden.FaceUp); Assert.IsTrue(hidden.Interaction.IsSelected);
            var restored = JsonUtility.FromJson<CardZonePreviewModel>(JsonUtility.ToJson(model));
            CollectionAssert.AreEqual(model.BuildZone().Cards, restored.BuildZone().Cards);
        }

        [Test] public void StandardDefinitionsMapToExistingArtworkAndKeepArtOnlySamplesSeparate()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab");
            var atlas = prefab.GetComponentInChildren<CardArt.CardView>().Atlas;
            foreach (var definition in StandardDeck.Cards())
            {
                var art = CardZonePreviewCatalog.ArtFor(definition.Id);
                Assert.IsTrue(atlas.TryGetCard(art, out var region), art); Assert.IsFalse(region.clipped);
                Assert.AreEqual(definition.Id, CardZonePreviewCatalog.DefinitionFor(art));
            }
            Assert.AreEqual(StandardDeck.CardId(Suit.Spades, Rank.Ace), CardZonePreviewCatalog.DefinitionFor("spades_a"));
            var joker = CardZonePreviewCatalog.DefinitionFor("joker_big");
            Assert.IsTrue(joker.Value.StartsWith("editor-art:")); Assert.AreEqual("joker_big", CardZonePreviewCatalog.ArtFor(joker));
        }
    }
}
