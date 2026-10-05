using System;
using CardArt;
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    [TestFixture]
    public sealed class CardAppearanceTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene;
            public readonly HandZoneView Zone;
            public readonly GameObject Prefab;
            public Fixture()
            {
                Scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var root = new GameObject("Appearance Test") { hideFlags = HideFlags.HideAndDontSave };
                    SceneManager.MoveGameObjectToScene(root, Scene);
                    Zone = root.AddComponent<HandZoneView>();
                    var transitions = CardZoneTransitionSettings.Default; transitions.Enabled = false; Zone.SetTransitionSettings(transitions);
                    Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab");
                    Zone.Configure(Prefab, CardZoneLayoutSettings.Default);
                }
                catch { EditorSceneManager.ClosePreviewScene(Scene); throw; }
            }
            public void Dispose() => EditorSceneManager.ClosePreviewScene(Scene);
        }
        private static CardZoneViewData Data(params CardVisualData[] cards) => new CardZoneViewData(new ZoneId("zone"), cards);

        private static void CheckColor(Color expected, Color actual)
        {
            // MaterialPropertyBlock color conversion can introduce a small floating-point round-trip error.
            for (int channel = 0; channel < 4; channel++)
                Assert.That(actual[channel], Is.EqualTo(expected[channel]).Within(0.00001f), "Color channel " + channel);
        }

        private static void CheckBlock(CardInstanceView view, bool foil, bool seal, bool cross, float foilStrength, float emission, Color tint)
        {
            var block = new MaterialPropertyBlock(); view.Surface.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            Assert.AreEqual(foil ? 1f : 0f, block.GetFloat(CardShaderProperties.FoilEnabled));
            Assert.AreEqual(seal ? 1f : 0f, block.GetFloat(CardShaderProperties.SealEnabled));
            Assert.AreEqual(cross ? 1f : 0f, block.GetFloat(CardShaderProperties.RedCrossEnabled));
            Assert.That(block.GetFloat(CardShaderProperties.FoilStrength), Is.EqualTo(foilStrength).Within(0.000001f));
            Assert.That(block.GetFloat(CardShaderProperties.SealEmissionStrength), Is.EqualTo(emission).Within(0.000001f));
            CheckColor(tint, block.GetColor(CardShaderProperties.ColorTint));
        }

        [Test] public void AllEffectAndDisableCombinationsReachShaderWithoutTouchingSharedAssets()
        {
            using (var f = new Fixture())
            {
                var material = f.Prefab.GetComponentInChildren<CardView>().GetComponent<MeshRenderer>().sharedMaterial;
                var materialBefore = EditorJsonUtility.ToJson(material);
                var prefabBefore = EditorJsonUtility.ToJson(f.Prefab.GetComponentInChildren<CardView>());
                CardInstanceView kept = null;
                var style = f.Zone.AppearanceSettings;
                for (int bits = 0; bits < 8; bits++)
                for (int disabled = 0; disabled < 2; disabled++)
                {
                    var card = new CardInstance(new CardInstanceId("card"), new CardDefinitionId("definition"), effects: (CardEffects)bits, isDisabled: disabled == 1);
                    var visual = CardVisualData.FromCard(card, "same-view", true, _ => "hearts_q");
                    f.Zone.Present(Data(visual, new CardVisualData("neighbor", "hearts_q")));
                    var view = f.Zone.Views[0]; if (kept == null) kept = view; else Assert.AreSame(kept, view);
                    bool foil = (bits & 2) != 0, seal = (bits & 1) != 0, ineffective = (bits & 4) != 0;
                    float brightness = ineffective ? style.IneffectiveBrightness : 1;
                    CheckBlock(view, foil, seal, disabled == 1,
                        foil ? style.FoilStrength * (ineffective ? style.IneffectiveFoilMultiplier : 1) : 0,
                        seal && !ineffective ? style.SealEmissionStrength : 0, new Color(brightness, brightness, brightness, 1));
                    CheckBlock(f.Zone.Views[1], false, false, false, 0, 0, Color.white);
                    Assert.AreSame(material, view.Surface.GetComponent<MeshRenderer>().sharedMaterial);
                    Assert.AreSame(material, f.Zone.Views[1].Surface.GetComponent<MeshRenderer>().sharedMaterial);
                }
                Assert.AreEqual(materialBefore, EditorJsonUtility.ToJson(material));
                Assert.AreEqual(prefabBefore, EditorJsonUtility.ToJson(f.Prefab.GetComponentInChildren<CardView>()));
            }
        }

        [Test] public void HideRemovalAndRepeatedUpdatesRestoreCompleteAppearanceWithoutResidue()
        {
            using (var f = new Fixture())
            {
                var full = new CardVisualData("same", "hearts_q", effects: (CardEffects)7, isDisabled: true);
                f.Zone.Present(Data(full)); var view = f.Zone.Views[0];
                for (int i = 0; i < 4; i++) f.Zone.Present(Data(full));
                var style = f.Zone.AppearanceSettings;
                Assert.AreEqual(new Color(style.IneffectiveBrightness, style.IneffectiveBrightness, style.IneffectiveBrightness, 1), view.Surface.Tint);
                f.Zone.Present(Data(new CardVisualData("same", tint: Color.red, effects: (CardEffects)7, isDisabled: true)));
                Assert.AreSame(view, f.Zone.Views[0]); Assert.AreEqual("card_back", view.Surface.CardId);
                CheckBlock(view, false, false, false, 0, 0, Color.white);
                f.Zone.Present(Data(new CardVisualData("same", "spades_a", effects: CardEffects.Enhanced | CardEffects.Laminated)));
                CheckBlock(view, true, true, false, style.FoilStrength, style.SealEmissionStrength, Color.white);
                view.gameObject.SetActive(false); view.gameObject.SetActive(true);
                CheckBlock(view, true, true, false, style.FoilStrength, style.SealEmissionStrength, Color.white);
                var normal = Data(new CardVisualData("same", "spades_a")); f.Zone.Present(normal);
                // A direct art preview override must not become the presentation's source of truth.
                view.Surface.SetFoilEnabled(true); view.Surface.SetSealEnabled(true); view.Surface.SetRedCrossEnabled(true);
                view.Surface.SetTint(Color.red); view.Surface.SetSealEmissionStrength(1);
                f.Zone.Present(normal); CheckBlock(view, false, false, false, 0, 0, Color.white);
            }
        }

        [Test] public void StyleRefreshPreservesHdrColorsAndCardAttributesAndRejectsInvalidInput()
        {
            using (var f = new Fixture())
            {
                var data = Data(new CardVisualData("a", "spades_a", tint: new Color(0.8f, 0.6f, 0.4f, 1), effects: (CardEffects)7));
                f.Zone.Present(data); var view = f.Zone.Views[0]; var style = f.Zone.AppearanceSettings;
                style.FoilColor = new Color(1.2f, 3.2f, 4, 0); style.FoilStrength = 2;
                style.SealColor = new Color(0.3f, 0.2f, 0.1f, 0); style.IneffectiveBrightness = 0.5f;
                style.IneffectiveFoilMultiplier = 0.25f; f.Zone.SetAppearanceSettings(style);
                Assert.AreSame(data, f.Zone.Data); Assert.AreSame(view, f.Zone.Views[0]);
                var block = new MaterialPropertyBlock(); view.Surface.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                CheckColor(new Color(1.2f, 3.2f, 4, 1), block.GetColor(CardShaderProperties.FoilColor));
                CheckColor(new Color(0.3f, 0.2f, 0.1f, 1), block.GetColor(CardShaderProperties.SealColor));
                CheckBlock(view, true, true, false, 0.5f, 0, new Color(0.4f, 0.3f, 0.2f, 1));
                var bad = style; bad.FoilStrength = float.NaN;
                Assert.Throws<ArgumentOutOfRangeException>(() => f.Zone.SetAppearanceSettings(bad));
                bad = style; bad.FoilColor.r = float.PositiveInfinity;
                Assert.Throws<ArgumentException>(() => f.Zone.SetAppearanceSettings(bad));
                bad = style; bad.SealEmissionStrength = 2;
                Assert.Throws<ArgumentOutOfRangeException>(() => f.Zone.SetAppearanceSettings(bad));
                Assert.AreEqual(style.FoilStrength, f.Zone.AppearanceSettings.FoilStrength);
                CheckBlock(view, true, true, false, 0.5f, 0, new Color(0.4f, 0.3f, 0.2f, 1));
            }
        }

        [Test] public void MissingShaderFeatureOrTextureRejectsWholeUpdateBeforeChangingViews()
        {
            using (var f = new Fixture())
            {
                var prefab = Object.Instantiate(f.Prefab); SceneManager.MoveGameObjectToScene(prefab, f.Scene); prefab.SetActive(false);
                var surface = prefab.GetComponentInChildren<CardView>(true);
                var renderer = surface.GetComponent<MeshRenderer>(); var material = new Material(renderer.sharedMaterial);
                try
                {
                    surface.Bind(surface.GetComponent<MeshFilter>().sharedMesh, material, surface.Atlas, "hearts_q");
                    f.Zone.Configure(prefab, CardZoneLayoutSettings.Default);
                    var baseline = Data(new CardVisualData("a", "hearts_q")); f.Zone.Present(baseline); var view = f.Zone.Views[0];
                    material.SetTexture(CardShaderProperties.RedCrossMap, null);
                    Assert.Throws<ArgumentException>(() => f.Zone.Present(Data(new CardVisualData("a", "spades_a", effects: CardEffects.Enhanced),
                        new CardVisualData("bad", "hearts_q", isDisabled: true))));
                    material.SetOverrideTag("CardFoilContract", "Unsupported");
                    Assert.Throws<ArgumentException>(() => f.Zone.Present(Data(new CardVisualData("a", "hearts_q", effects: CardEffects.Laminated))));
                    material.SetTexture(CardShaderProperties.SealMap, null);
                    Assert.Throws<ArgumentException>(() => f.Zone.Present(Data(new CardVisualData("a", "hearts_q", effects: CardEffects.Enhanced))));
                    Assert.AreSame(baseline, f.Zone.Data); Assert.AreSame(view, f.Zone.Views[0]); Assert.AreEqual(1, f.Zone.Views.Count);
                    Assert.AreEqual("hearts_q", view.Surface.CardId); CheckBlock(view, false, false, false, 0, 0, Color.white);
                }
                finally { f.Zone.Clear(); Object.DestroyImmediate(prefab); Object.DestroyImmediate(material); }
            }
        }

        [Test] public void EditorCardEditsReachShaderByIdentityAcrossReorderHideRevealAndReset()
        {
            using (var f = new Fixture())
            {
                var model = new CardPresentation.Editor.CardZonePreviewModel(); model.Add("spades_a"); model.Add("spades_a");
                var first = model.Cards[0]; var id = first.ToCard().Id;
                f.Zone.Present(model.BuildZone(), model.Project); var view = f.Zone.Views[0]; var neighbor = f.Zone.Views[1];
                model.Reverse();
                model.SetCardProperties(id, StandardDeck.CardId(Suit.Hearts, Rank.Queen), (CardEffects)7, true);
                f.Zone.Present(model.BuildZone(), model.Project);
                Assert.AreSame(view, f.Zone.Views[1]); Assert.AreSame(neighbor, f.Zone.Views[0]);
                Assert.AreEqual("hearts_q", view.Surface.CardId);
                var style = f.Zone.AppearanceSettings;
                CheckBlock(view, true, true, true, style.FoilStrength * style.IneffectiveFoilMultiplier, 0,
                    new Color(style.IneffectiveBrightness, style.IneffectiveBrightness, style.IneffectiveBrightness, 1));
                CheckBlock(neighbor, false, false, false, 0, 0, Color.white);
                first.FaceUp = false;
                model.SetCardProperties(id, model.GetCard(id).DefinitionId, CardEffects.Enhanced, false);
                f.Zone.Present(model.BuildZone(), model.Project);
                Assert.AreEqual(CardEffects.Enhanced, model.GetCard(id).Effects);
                Assert.AreEqual(CardEffects.None, view.Data.Effects); CheckBlock(view, false, false, false, 0, 0, Color.white);
                first.FaceUp = true; f.Zone.Present(model.BuildZone(), model.Project);
                CheckBlock(view, false, true, false, 0, style.SealEmissionStrength, Color.white);
                model.SetCardProperties(id, model.GetCard(id).DefinitionId, CardEffects.None, false);
                f.Zone.Present(model.BuildZone(), model.Project); CheckBlock(view, false, false, false, 0, 0, Color.white);
                Assert.AreSame(view, f.Zone.Views[1]); CheckBlock(neighbor, false, false, false, 0, 0, Color.white);
            }
        }
    }
}
