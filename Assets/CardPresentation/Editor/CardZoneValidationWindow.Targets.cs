#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CardInteraction;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CardPresentation.Editor
{
    public sealed partial class CardZoneValidationWindow
    {
        [SerializeField] private bool followSelection = true;
        [SerializeField] private string targetScenePath, targetSceneName, targetHierarchy;
        /// <summary>Polymorphic lookup also handles inactive zones and a selected card below a zone.</summary>
        public static CardZoneView ResolveTarget(GameObject selected)
        {
            if (selected == null || !selected.scene.IsValid() || EditorUtility.IsPersistent(selected)) return null;
            return selected.GetComponentInParent<CardZoneView>(true);
        }


        public void SelectTarget(CardZoneView next)
        {
            if (next == target) return;
            if (next != null && ResolveTarget(next.gameObject) != next) throw new ArgumentException("Select a scene CardZoneView.");
            target = next; inspectedCardId = null; scroll = Vector2.zero; error = status = null;
            ReadSettings(); RememberTarget(); Repaint();
        }
        private void OnSelectionChanged()
        {
            if (!followSelection || ChangingMode) return;
            var next = ResolveTarget(Selection.activeGameObject);
            if (next != null && next != target) SelectTarget(next);
        }
        private void DrawTargetSelector()
        {
            var next = (CardZoneView)EditorGUILayout.ObjectField("实际牌区 View", target, typeof(CardZoneView), true);
            if (next != target) Run(() => SelectTarget(next));
            followSelection = EditorGUILayout.Toggle("跟随场景选择", followSelection);
            if (target != null) EditorGUILayout.LabelField("类型 / 容量", target.GetType().Name + " / " + CurrentCapacity);
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("牌局镜像", Host != null ? Host.Match : null, typeof(CardMatchHost), true);
            if (HasLiveBattle)
            {
                EditorGUILayout.LabelField("牌区 / 镜像版本", Host.Zone.Id.Value + " / " + Host.Match.State.Revision);
                EditorGUILayout.LabelField("当前卡牌 / 抽牌堆 / 弃牌堆", CurrentCards.Count + " / " + Host.DeckCards.Count + " / " + Host.DiscardedCards.Count);
            }
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
#endif
