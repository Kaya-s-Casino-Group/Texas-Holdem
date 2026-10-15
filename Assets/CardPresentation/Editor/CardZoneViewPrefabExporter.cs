#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CardPresentation.Editor
{
    /// <summary>Exports reusable view configuration, without copying a scene hierarchy or card-layer test data.</summary>
    public static class CardZoneViewPrefabExporter
    {
        public const string DefaultFolder = "Assets/CardPresentation/Prefabs";

        public static GameObject Export(CardZoneView source, string path, bool overwrite = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            ValidatePath(source, path, overwrite);
            if (source.CardPrefab == null || !EditorUtility.IsPersistent(source.CardPrefab))
                throw new ArgumentException("请先为牌区指定已保存的卡牌 Prefab 资源。");

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(Path.GetFileNameWithoutExtension(path));
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene);
                root.layer = source.gameObject.layer;
                root.transform.localScale = source.transform.localScale;
                var view = (CardZoneView)root.AddComponent(source.GetType());
                if (view == null) throw new InvalidOperationException("无法创建牌区子类：" + source.GetType().FullName);
                EditorUtility.CopySerialized(source, view);
                view.hideFlags = HideFlags.None;

                // Camera aiming caches scene-specific rotation. Export the authored orientation, not a tracked frame.
                var serialized = new SerializedObject(view);
                root.transform.localRotation = serialized.FindProperty("hasUnfacedRotation").boolValue
                    ? serialized.FindProperty("unfacedLocalRotation").quaternionValue : source.transform.localRotation;
                serialized.FindProperty("hasUnfacedRotation").boolValue = false;
                serialized.FindProperty("unfacedLocalRotation").quaternionValue = Quaternion.identity;
                serialized.FindProperty("facingCamera").objectReferenceValue = null;
                if (source is HandZoneView hand)
                {
                    serialized.FindProperty("deckLocalPosition").vector3Value = source.transform.InverseTransformPoint(hand.DeckWorldPosition) / source.CardScale;
                    serialized.FindProperty("discardLocalPosition").vector3Value = source.transform.InverseTransformPoint(hand.DiscardWorldPosition) / source.CardScale;
                    serialized.FindProperty("deckAnchor").objectReferenceValue = null;
                    serialized.FindProperty("discardAnchor").objectReferenceValue = null;
                }
                RemapReferences(serialized, source, view);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                // Validate through the runtime entry points on the isolated copy. Never publish/clear the source.
                view.Configure(view.CardPrefab, view.Layout);
                view.SetLayout(view.Layout, view.MotionSettings, animate: false);
                if (view is HandZoneView exportedHand)
                {
                    exportedHand.SetDeckSource(exportedHand.DeckLocalPosition);
                    exportedHand.SetDiscardDestination(exportedHand.DiscardLocalPosition);
                    exportedHand.SetDrawSound(exportedHand.DrawSoundOverride, exportedHand.DrawSoundVolume, exportedHand.DrawSoundEnabled);
                    exportedHand.SetDiscardSound(exportedHand.DiscardSoundOverride, exportedHand.DiscardSoundVolume, exportedHand.DiscardSoundEnabled);
                    exportedHand.SetSelectionSound(exportedHand.SelectionSoundOverride, exportedHand.SelectionSoundVolume, exportedHand.SelectionSoundEnabled);
                    exportedHand.SetMaxSelectedCards(exportedHand.MaxSelectedCards);
                    exportedHand.SetSelectionDeniedAnimation(exportedHand.SelectionDeniedOffset, exportedHand.SelectionDeniedRiseSeconds, exportedHand.SelectionDeniedReturnSeconds);
                    exportedHand.SetSelectionDeniedSound(exportedHand.SelectionDeniedSound, exportedHand.SelectionDeniedSoundVolume, exportedHand.SelectionDeniedSoundEnabled);
                }
                // Position belongs to the placement of each instance. The asset starts at its own origin.
                root.transform.localPosition = Vector3.zero;
                root.SetActive(source.gameObject.activeSelf);
                var asset = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success || asset == null) throw new IOException("牌区 Prefab 保存失败：" + path);
                return asset;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void RemapReferences(SerializedObject serialized, CardZoneView source, CardZoneView copy)
        {
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                Object value = property.objectReferenceValue;
                if (value == null || EditorUtility.IsPersistent(value)) continue;
                if (value == source) property.objectReferenceValue = copy;
                else if (value == source.gameObject) property.objectReferenceValue = copy.gameObject;
                else if (value == source.transform) property.objectReferenceValue = copy.transform;
                else if (value != copy && value != copy.gameObject && value != copy.transform)
                    throw new ArgumentException("设置“" + property.propertyPath + "”引用了场景对象或临时资源。请改用已保存的资源后导出。");
            }
        }

        private static void ValidatePath(CardZoneView source, string path, bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || path.Contains("\\") ||
                Array.Exists(path.Split('/'), part => part == "." || part == ".." || part.Length == 0))
                throw new ArgumentException("请选择 Assets 目录内的 .prefab 路径。", nameof(path));
            string directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(directory)) throw new ArgumentException("目标文件夹不存在：" + directory);
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null && !File.Exists(path)) return;
            if (!overwrite) throw new InvalidOperationException("已有同名资源，请使用新名称或明确选择覆盖。");
            var prefab = existing as GameObject;
            var zone = prefab != null ? prefab.GetComponent<CardZoneView>() : null;
            if (zone == null || zone.GetType() != source.GetType() || PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Regular)
                throw new ArgumentException("只能覆盖相同牌区子类的普通 Prefab；不能覆盖单张卡牌或其他资源。");
        }
    }
}
#endif
