using System;
using System.IO;
using CardInteraction;
using CardPresentation.Editor;
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
    public sealed class CardZoneViewPrefabExporterTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene = EditorSceneManager.NewPreviewScene();
            public readonly string Folder = "Assets/Scripts/Presentation/CardPresentation/Tests/ExportTemp_" + Guid.NewGuid().ToString("N");
            public Fixture() => AssetDatabase.CreateFolder("Assets/Scripts/Presentation/CardPresentation/Tests", Path.GetFileName(Folder));
            public GameObject Root(string name)
            {
                var root = new GameObject(name); SceneManager.MoveGameObjectToScene(root, Scene); return root;
            }
            public T Zone<T>() where T : CardZoneView
            {
                var view = Root(typeof(T).Name).AddComponent<T>();
                view.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                return view;
            }
            public void Dispose() { EditorSceneManager.ClosePreviewScene(Scene); AssetDatabase.DeleteAsset(Folder); }
        }
        private static CardZoneViewData Data(params CardVisualData[] cards) => new CardZoneViewData(new ZoneId("export/test"), cards);
        private static void Near(Vector3 a, Vector3 b) => Assert.Less(Vector3.Distance(a, b), .00001f);

        [Test] public void HandExportPreservesTuningBakesAnchorsAndLeavesLiveSourceUntouched()
        {
            using (var f = new Fixture())
            {
                var source = f.Zone<HandZoneView>();
                source.transform.localPosition = new Vector3(.2f, .1f, -.4f);
                source.transform.localRotation = Quaternion.Euler(15, 25, 0);
                source.transform.localScale = new Vector3(1.2f, 1, .8f);
                var authoredRotation = source.transform.localRotation;
                var camera = f.Root("Camera").AddComponent<Camera>(); camera.transform.position = new Vector3(1, 2, -3);
                source.SetCameraFacing(true, camera);
                var deck = f.Root("External Deck").transform; deck.position = new Vector3(2, .3f, 1);
                var discard = f.Root("External Discard").transform; discard.position = new Vector3(-2, .2f, -1);
                source.SetDeckSource(Vector3.zero, deck); source.SetDiscardDestination(Vector3.zero, discard);
                var deckLocal = source.transform.InverseTransformPoint(deck.position);
                var discardLocal = source.transform.InverseTransformPoint(discard.position);
                var layout = source.Layout; layout.ArcHeight = .006f; source.SetLayout(layout);
                var transition = source.TransitionSettings; transition.SettleSeconds = .82f; transition.ReflowOvershoot = 2.1f;
                source.SetTransitionSettings(transition);
                source.SetInteractionOffsets(new Vector3(0, 0, .011f), new Vector3(0, 0, .023f));
                source.SetDrawSound(source.DrawSound, .31f); source.SetDiscardSound(null, .54f, false); source.SetSelectionSound(null, .67f);
                source.SetMaxSelectedCards(3); source.SetSelectionDeniedAnimation(new Vector3(0, 0, .006f), .09f, .2f);
                source.SetSelectionDeniedSound(null, .45f, false);
                source.SetDeselectionSound(source.DrawSound, .42f, false);
                source.SetReflowSound(source.DrawSound, .37f, false);
                var haptics = source.HapticSettings; haptics.Enabled = false; haptics.GamepadSeconds = .08f; haptics.GamepadStrength = .42f;
                source.SetHapticSettings(haptics);
                source.gameObject.AddComponent<CardZonePlayHost>(); source.gameObject.AddComponent<CardZoneMouseInput>();
                var unrelated = new GameObject("Authored Child"); unrelated.transform.SetParent(source.transform, false);
                source.Present(Data(new CardVisualData("a", "spades_a"), new CardVisualData("b")));
                source.AdvanceMotion(.1f);
                var snapshot = source.Data; var first = source.Views[0]; var pose = first.DisplayPose;
                var serialized = EditorJsonUtility.ToJson(source); int children = source.transform.childCount;
                var asset = CardZoneViewPrefabExporter.Export(source, f.Folder + "/Hand.prefab");
                var hand = asset.GetComponent<HandZoneView>();
                Assert.NotNull(hand); Assert.AreEqual(2, asset.GetComponents<Component>().Length); Assert.AreEqual(0, asset.transform.childCount);
                Assert.AreSame(source.CardPrefab, hand.CardPrefab); Assert.AreEqual(source.Layout.ArcHeight, hand.Layout.ArcHeight);
                Assert.AreEqual(.82f, hand.TransitionSettings.SettleSeconds); Assert.AreSame(source.CardPrefab, hand.CardPrefab);
                Assert.AreEqual(2.1f, hand.TransitionSettings.ReflowOvershoot);
                Assert.AreEqual(source.SelectedOffset, hand.SelectedOffset); Assert.AreEqual(source.HoveredOffset, hand.HoveredOffset);
                Assert.AreEqual(.31f, hand.DrawSoundVolume); Assert.AreSame(source.DrawSoundOverride, hand.DrawSoundOverride);
                Assert.AreEqual(.54f, hand.DiscardSoundVolume); Assert.IsFalse(hand.DiscardSoundEnabled); Assert.AreEqual(.67f, hand.SelectionSoundVolume);
                Assert.AreEqual(3, hand.MaxSelectedCards); Assert.AreEqual(.006f, hand.SelectionDeniedOffset.z);
                Assert.AreEqual(.09f, hand.SelectionDeniedRiseSeconds); Assert.AreEqual(.2f, hand.SelectionDeniedReturnSeconds);
                Assert.IsNull(hand.SelectionDeniedSound); Assert.AreEqual(.45f, hand.SelectionDeniedSoundVolume); Assert.IsFalse(hand.SelectionDeniedSoundEnabled);
                Assert.AreSame(source.DrawSound, hand.DeselectionSound); Assert.AreEqual(.42f, hand.DeselectionSoundVolume);
                Assert.IsFalse(hand.DeselectionSoundEnabled);
                Assert.AreSame(source.DrawSound, hand.ReflowSound); Assert.AreEqual(.37f, hand.ReflowSoundVolume);
                Assert.IsFalse(hand.ReflowSoundEnabled);
                Assert.AreEqual(haptics, hand.HapticSettings);
                Assert.IsNull(hand.DeckAnchor); Assert.IsNull(hand.DiscardAnchor); Assert.IsNull(hand.FacingCamera); Assert.IsTrue(hand.FaceCamera);
                Near(deckLocal, hand.DeckLocalPosition); Near(discardLocal, hand.DiscardLocalPosition);
                Assert.Less(Quaternion.Angle(authoredRotation, asset.transform.localRotation), .001f);
                Near(Vector3.zero, asset.transform.localPosition); Near(source.transform.localScale, asset.transform.localScale);
                Assert.IsNull(hand.Data); Assert.AreEqual(0, hand.Views.Count);
                Assert.AreEqual(serialized, EditorJsonUtility.ToJson(source)); Assert.AreSame(snapshot, source.Data);
                Assert.AreSame(first, source.Views[0]); Near(pose.Position, first.DisplayPose.Position);
                Assert.AreEqual(children, source.transform.childCount); Assert.IsTrue(source.IsTransitioning);
            }
        }

        [Test] public void AutoSizedExportBakesAnchorsWithoutApplyingTheSizeTwice()
        {
            using (var f = new Fixture())
            {
                var source = f.Zone<HandZoneView>();
                var layout = source.Layout; layout.FitCardsToZone = true; layout.Size = new Vector2(.5f, .05f);
                source.SetLayout(layout);
                var anchor = f.Root("Anchor"); anchor.transform.position = new Vector3(.4f, .2f, .1f);
                source.SetDeckSource(Vector3.zero, anchor.transform);
                var deck = source.DeckWorldPosition; var discard = source.DiscardWorldPosition;
                var asset = CardZoneViewPrefabExporter.Export(source, f.Folder + "/AutoSized.prefab");
                var copy = ((GameObject)PrefabUtility.InstantiatePrefab(asset, f.Scene)).GetComponent<HandZoneView>();
                Assert.IsNull(copy.DeckAnchor); Assert.IsTrue(copy.Layout.FitCardsToZone);
                Near(deck, copy.DeckWorldPosition); Near(discard, copy.DiscardWorldPosition);
                copy.Present(Data(new CardVisualData("a", "hearts_q")), false);
                Assert.AreEqual(source.CardScale, copy.Views[0].PresentationScale);
            }
        }

        [Test] public void ExportedPrefabStartsEmptyAndCanBindFreshCardsAfterInstantiation()
        {
            using (var f = new Fixture())
            {
                var source = f.Zone<HandZoneView>(); source.Present(Data(new CardVisualData("old", "hearts_q")), false);
                var asset = CardZoneViewPrefabExporter.Export(source, f.Folder + "/Reusable.prefab");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(asset, f.Scene);
                var hand = root.GetComponent<HandZoneView>(); Assert.IsTrue(root.activeSelf); Assert.IsNull(hand.Data);
                Assert.AreEqual(0, hand.Views.Count); Assert.IsNull(root.GetComponent<CardZonePlayHost>());
                hand.Present(Data(new CardVisualData("new", "spades_a")), false);
                Assert.AreEqual("new", hand.Views[0].ViewId); Assert.AreEqual("old", source.Views[0].ViewId);
                Assert.AreSame(source.Views[0].Surface.GetComponent<MeshRenderer>().sharedMaterial,
                    hand.Views[0].Surface.GetComponent<MeshRenderer>().sharedMaterial);
            }
        }

        [Test] public void ExportKeepsActualSubclassFieldsAssetReferencesAndRemapsSelfReferences()
        {
            using (var f = new Fixture())
            {
                var source = f.Zone<SpecializedHandZoneViewForTests>();
                source.ExportTestSetting = 2.75f; source.Self = source; source.Owner = source.gameObject;
                source.Origin = source.transform; source.AdditionalReference = source.DrawSound;
                var asset = CardZoneViewPrefabExporter.Export(source, f.Folder + "/Specialized.prefab");
                var copy = asset.GetComponent<SpecializedHandZoneViewForTests>(); Assert.NotNull(copy);
                Assert.AreEqual(2.75f, copy.ExportTestSetting); Assert.AreSame(copy, copy.Self);
                Assert.AreSame(asset, copy.Owner); Assert.AreSame(asset.transform, copy.Origin);
                Assert.AreSame(source.DrawSound, copy.AdditionalReference);
                var common = f.Zone<CardZoneView>();
                var genericAsset = CardZoneViewPrefabExporter.Export(common, f.Folder + "/Common.prefab");
                Assert.AreEqual(typeof(CardZoneView), genericAsset.GetComponent<CardZoneView>().GetType());
                Assert.IsNull(genericAsset.GetComponent<HandZoneView>());
            }
        }

        [Test] public void ExplicitOverwriteKeepsGuidAndRejectsUnrelatedPrefabOrInvalidPath()
        {
            using (var f = new Fixture())
            {
                var source = f.Zone<HandZoneView>(); string path = f.Folder + "/Same.prefab";
                CardZoneViewPrefabExporter.Export(source, path); string guid = AssetDatabase.AssetPathToGUID(path);
                string original = File.ReadAllText(path);
                source.SetSelectionSound(null, .4f);
                Assert.Throws<InvalidOperationException>(() => CardZoneViewPrefabExporter.Export(source, path));
                Assert.AreEqual(original, File.ReadAllText(path));
                var updated = CardZoneViewPrefabExporter.Export(source, path, overwrite: true);
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
                Assert.AreEqual(.4f, updated.GetComponent<HandZoneView>().SelectionSoundVolume);
                var other = f.Root("Unrelated"); string otherPath = f.Folder + "/Other.prefab";
                PrefabUtility.SaveAsPrefabAsset(other, otherPath); string otherContents = File.ReadAllText(otherPath);
                Assert.Throws<ArgumentException>(() => CardZoneViewPrefabExporter.Export(source, otherPath, true));
                Assert.AreEqual(otherContents, File.ReadAllText(otherPath));
                Assert.Throws<ArgumentException>(() => CardZoneViewPrefabExporter.Export(source, "Assets/../Escaped.prefab"));
                Assert.Throws<ArgumentException>(() => CardZoneViewPrefabExporter.Export(source, "/tmp/Outside.prefab"));
                var generic = f.Zone<CardZoneView>();
                Assert.Throws<ArgumentException>(() => CardZoneViewPrefabExporter.Export(generic, path, true));
            }
        }

        [Test] public void UnknownSceneReferencesAndInvalidSettingsFailBeforeReplacingAsset()
        {
            using (var f = new Fixture())
            {
                var source = f.Zone<SpecializedHandZoneViewForTests>(); string path = f.Folder + "/Valid.prefab";
                CardZoneViewPrefabExporter.Export(source, path); string original = File.ReadAllText(path);
                var external = f.Root("Custom Scene Dependency"); source.AdditionalReference = external;
                Assert.Throws<ArgumentException>(() => CardZoneViewPrefabExporter.Export(source, path, true));
                Assert.AreSame(external, source.AdditionalReference); Assert.AreEqual(original, File.ReadAllText(path));
                source.AdditionalReference = null;
                var serialized = new SerializedObject(source); serialized.FindProperty("selectionSoundVolume").floatValue = float.NaN;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<ArgumentOutOfRangeException>(() => CardZoneViewPrefabExporter.Export(source, path, true));
                Assert.AreEqual(original, File.ReadAllText(path));
                Assert.AreEqual(0, source.transform.childCount);
            }
        }
    }
}
