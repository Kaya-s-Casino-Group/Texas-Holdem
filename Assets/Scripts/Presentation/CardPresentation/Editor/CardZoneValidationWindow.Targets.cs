#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CardInteraction;
using BattlePresentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CardPresentation.Editor
{
    public sealed partial class CardZoneValidationWindow
    {
        [SerializeField] private bool followSelection = true;
        [SerializeField] private string targetScenePath, targetSceneName, targetHierarchy, targetPresentationType;
        /// <summary>Polymorphic lookup also handles inactive zones and a selected card below a zone.</summary>
        public static CardZoneView ResolveTarget(GameObject selected)
        {
            if (selected == null || !selected.scene.IsValid() || EditorUtility.IsPersistent(selected)) return null;
            return selected.GetComponentInParent<CardZoneView>(true);
        }


        [SerializeField] private MonoBehaviour frontend;
        public ICardZonePresentation Presentation => frontend != null ? frontend as ICardZonePresentation : null;
        public static MonoBehaviour ResolvePresentation(GameObject selected)
        {
            if (selected == null || !selected.scene.IsValid() || EditorUtility.IsPersistent(selected)) return null;
            return selected.GetComponentsInParent<MonoBehaviour>(true).FirstOrDefault(item => item is ICardZonePresentation);
        }
        public void SelectPresentation(MonoBehaviour next)
        {
            if (next != null && (!(next is ICardZonePresentation) || !next.gameObject.scene.IsValid() || EditorUtility.IsPersistent(next)))
                throw new ArgumentException("请选择场景中实现 ICardZonePresentation 的组件。");
            if (frontend == next) return;
            StopPreview(); ReleaseFrontendEditor(); frontend = next;
            target = next != null ? next.GetComponent<CardZoneView>() : null;
            inspectedCardId = null; scroll = Vector2.zero; error = status = null;
            ReadSettings(); RememberTarget(); Repaint();
        }
        public void SelectTarget(CardZoneView next)
        {
            if (next == target) return;
            if (next != null && ResolveTarget(next.gameObject) != next) throw new ArgumentException("Select a scene CardZoneView.");
            StopPreview(); ReleaseFrontendEditor(); frontend = next != null ? ResolvePresentation(next.gameObject) : null;
            target = next; inspectedCardId = null; scroll = Vector2.zero; error = status = null;
            ReadSettings(); RememberTarget(); Repaint();
        }
        private void OnSelectionChanged()
        {
            if (!followSelection || ChangingMode) return;
            var provider = ResolvePresentation(Selection.activeGameObject);
            if (provider != null) { SelectPresentation(provider); return; }
            var next = ResolveTarget(Selection.activeGameObject);
            if (next != null && next != target) SelectTarget(next);
        }
        private Vector2 presentationListScroll;
        public static MonoBehaviour[] AvailablePresentations()
        {
            var result = new List<MonoBehaviour>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                result.AddRange(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                    .Where(item => item != null && item is ICardZonePresentation));
            }
            return result.OrderBy(PresentationLabel, StringComparer.Ordinal).ThenBy(item => item.GetInstanceID()).ToArray();
        }
        private static string PresentationLabel(MonoBehaviour component)
        {
            var names = new List<string>();
            for (var node = component.transform; node != null; node = node.parent) names.Add(node.name);
            names.Reverse();
            return component.gameObject.scene.name + " / " + string.Join("/", names) + " · " + component.GetType().Name;
        }
        private void DrawTargetSelector()
        {
            var available = AvailablePresentations();
            EditorGUILayout.LabelField("已配置的牌区表现 · " + available.Length, EditorStyles.boldLabel);
            if (available.Length == 0) EditorGUILayout.HelpBox("已加载场景中还没有牌区表现组件。", MessageType.Info);
            using (var list = new EditorGUILayout.ScrollViewScope(presentationListScroll, GUILayout.MaxHeight(120)))
            {
                presentationListScroll = list.scrollPosition;
                foreach (var component in available)
                {
                    string label = PresentationLabel(component) + (!component.isActiveAndEnabled ? "（未启用）" : "");
                    if (GUILayout.Toggle(component == frontend, new GUIContent(label, label), "Button") && component != frontend)
                        Run(() => { SelectPresentation(component); page = 4; });
                }
            }
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
            var selected = frontend != null ? (Component)frontend : target;
            if (selected == null) return;
            targetPresentationType = frontend != null ? frontend.GetType().FullName : null;
            var indices = new List<int>();
            for (var node = selected.transform; node != null; node = node.parent) indices.Add(node.GetSiblingIndex());
            indices.Reverse(); targetHierarchy = string.Join("/", indices);
            targetScenePath = selected.gameObject.scene.path; targetSceneName = selected.gameObject.scene.name;
        }

        // Serialized component references normally survive reload. The hierarchy locator also covers Play scene copies.
        private CardZoneView RestoreTarget() => RestoreTargetObject()?.GetComponent<CardZoneView>();
        private MonoBehaviour RestorePresentation() => RestoreTargetObject()?.GetComponents<MonoBehaviour>()
            .FirstOrDefault(item => item is ICardZonePresentation && item.GetType().FullName == targetPresentationType);
        private GameObject RestoreTargetObject()
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
                return node.gameObject;
            }
            return null;
        }
    }
}
#endif
