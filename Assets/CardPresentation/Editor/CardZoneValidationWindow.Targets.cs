using System;
using System.Collections.Generic;
using System.Linq;
using CardInteraction;
using PokerEngine;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardPresentation.Editor
{
    public sealed partial class CardZoneValidationWindow
    {
        [SerializeField] private bool followSelection = true;
        [SerializeField] private bool hasEditableSnapshot = true;
        [SerializeField] private string previewZoneId;
        [SerializeField] private string targetScenePath, targetSceneName, targetHierarchy;

        /// <summary>Polymorphic lookup also handles inactive zones and a selected card below a zone.</summary>
        public static CardZoneView ResolveTarget(GameObject selected)
        {
            if (selected == null || !selected.scene.IsValid() || EditorUtility.IsPersistent(selected)) return null;
            return selected.GetComponentInParent<CardZoneView>(true);
        }

        /// <summary>Switch editor context without copying card data or settings between zones.</summary>
        public void SelectTarget(CardZoneView next)
        {
            if (next == target) return;
            if (next != null && ResolveTarget(next.gameObject) != next)
                throw new ArgumentException("Select a scene CardZoneView or one of its subclasses.", nameof(next));
            Undo.RecordObject(this, "切换验证牌区");
            target = next; playModel = null; drawExperimentContext = null;
            model = new CardZonePreviewModel(); discardedCards.Clear();
            inspectedCardId = null; scroll = Vector2.zero; error = status = null;
            previewZoneId = "editor/zone/" + Guid.NewGuid().ToString("N");
            RefreshTargetSource(); ReadSettings();
            if (!Application.isPlaying) LoadInitialSnapshot();
            else SyncRuntimeState();
            inspectedDeckId = null;
            CurrentExperiment.EnsureDeck(ArtIds()); RememberTarget();
            // Only restore a known card-layer source. An external view snapshot is insufficient to reconstruct hidden cards.
            if (!Application.isPlaying && Host != null && Host.HasSnapshot) RestorePreview();
            Repaint();
        }

        private void RefreshTargetSource()
        {
            hasEditableSnapshot = target == null || target.Data == null || (Host != null && Host.HasSnapshot);
            if (target != null && (Host == null || !Host.HasSnapshot)) { model.Clear(); discardedCards.Clear(); }
            if (string.IsNullOrEmpty(previewZoneId)) previewZoneId = "editor/zone/" + Guid.NewGuid().ToString("N");
            RememberTarget();
        }

        private CardZone BuildPreviewZone()
        {
            var source = Host != null && Host.HasSnapshot ? Host.Zone : null;
            // Upgrade old validation-only kinds; preserve explicit gameplay kinds and ownership.
            string kind = source == null || source.Kind == "editor.preview" ? target.DefaultZoneKind : source.Kind;
            return model.BuildZone(source?.Id ?? new ZoneId(previewZoneId), kind, source?.ControllerId);
        }

        private void OnSelectionChanged()
        {
            if (!followSelection || EditorApplication.isPlayingOrWillChangePlaymode != Application.isPlaying) return;
            var next = ResolveTarget(Selection.activeGameObject);
            if (next != null && next != target) Run(() => SelectTarget(next));
        }

        private void DrawTargetSelector()
        {
            var next = (CardZoneView)EditorGUILayout.ObjectField("目标牌区", target, typeof(CardZoneView), true);
            if (next != target) Run(() => SelectTarget(next));
            EditorGUI.BeginChangeCheck();
            followSelection = EditorGUILayout.Toggle("跟随场景选择", followSelection);
            if (EditorGUI.EndChangeCheck() && followSelection) OnSelectionChanged();
            if (target != null)
                EditorGUILayout.LabelField("实际类型", target.GetType().Name + " · " + target.DefaultZoneKind);
            if (!hasEditableSnapshot)
                EditorGUILayout.HelpBox("此牌区由外部数据驱动，未提供 CardZonePlayHost 卡牌数据快照。仅查看，避免从视觉投影反推或覆盖卡牌数据。", MessageType.Info);
        }

        private void RememberTarget()
        {
            if (target == null) return;
            var indices = new List<int>();
            for (var node = target.transform; node != null; node = node.parent) indices.Add(node.GetSiblingIndex());
            indices.Reverse(); targetHierarchy = string.Join("/", indices);
            targetScenePath = target.gameObject.scene.path; targetSceneName = target.gameObject.scene.name;
        }

        // Serialized component references normally survive reload. The hierarchy locator also covers Play scene copies.
        private CardZoneView RestoreTarget()
        {
            if (string.IsNullOrEmpty(targetHierarchy)) return null;
            var indices = targetHierarchy.Split('/').Select(int.Parse).ToArray();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene.path != targetScenePath || scene.name != targetSceneName) continue;
                var roots = scene.GetRootGameObjects();
                if (indices[0] >= roots.Length) return null;
                Transform node = roots[indices[0]].transform;
                for (int j = 1; j < indices.Length; j++)
                {
                    if (indices[j] >= node.childCount) return null;
                    node = node.GetChild(indices[j]);
                }
                return node.GetComponent<CardZoneView>();
            }
            return null;
        }
    }
}
