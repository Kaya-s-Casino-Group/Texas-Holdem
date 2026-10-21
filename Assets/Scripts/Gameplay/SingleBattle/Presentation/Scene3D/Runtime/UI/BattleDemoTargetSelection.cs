using System.Collections.Generic;
using System.Linq;
using global::BattlePresentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>BattleDemo's portrait click targets. Uses existing named rule choices, never overrides automatic targeting.</summary>
    public sealed class BattleDemoTargetSelection : MonoBehaviour
    {
        [SerializeField] private SingleBattleSceneAdapter host;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Material outlineMaterial;
        private readonly List<Entry> entries = new List<Entry>();
        private string preferred;
        private bool pendingChoice;
        private sealed class Entry
        {
            internal EnemyBattleActor Actor;
            internal RectTransform Rect;
            internal Button Button;
            internal MeshRenderer Artwork, Outline;
        }
        public void Configure(SingleBattleSceneAdapter owner, Canvas ui, Camera camera, TMP_FontAsset typeface, Material outline)
        { host = owner; canvas = ui; viewCamera = camera; font = typeface; outlineMaterial = outline; }

        private void Start()
        {
            if (host == null || canvas == null || viewCamera == null) { enabled = false; return; }
            EnsureTargets();
        }
        private void EnsureTargets()
        {
            foreach (var actor in host.Enemies)
            {
                if (actor == null || entries.Any(entry => entry.Actor == actor)) continue;
                var go = new GameObject("Target " + actor.name, typeof(RectTransform), typeof(Image), typeof(Button));
                var rect = (RectTransform)go.transform; rect.SetParent(canvas.transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                go.GetComponent<Image>().color = new Color(1, 1, 1, .001f);
                var button = go.GetComponent<Button>();
                button.transition = Selectable.Transition.None;
                var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
                button.onClick.AddListener(() => ChooseTarget(actor));
                var artwork = actor.Visual != null ? actor.Visual.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(renderer => renderer.name != "Selected enemy contour") : null;
                MeshRenderer outline = null;
                if (artwork != null && outlineMaterial != null)
                {
                    // A spawned enemy may inherit the disabled contour from its visual template.
                    var inherited = artwork.transform.Find("Selected enemy contour");
                    var contour = inherited != null ? inherited.gameObject : new GameObject("Selected enemy contour", typeof(MeshFilter), typeof(MeshRenderer));
                    contour.layer = artwork.gameObject.layer; contour.transform.SetParent(artwork.transform, false);
                    contour.GetComponent<MeshFilter>().sharedMesh = artwork.GetComponent<MeshFilter>().sharedMesh;
                    outline = contour.GetComponent<MeshRenderer>(); outline.sharedMaterial = outlineMaterial;
                    outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    outline.receiveShadows = false; outline.enabled = false;
                    var properties = new MaterialPropertyBlock(); artwork.GetPropertyBlock(properties);
                    var texture = properties.GetTexture("_BaseMap");
                    if (texture == null) texture = artwork.sharedMaterial.HasProperty("_BaseMap") ? artwork.sharedMaterial.GetTexture("_BaseMap") : artwork.sharedMaterial.mainTexture;
                    properties.Clear();
                    if (texture != null) properties.SetTexture("_MainTex", texture);
                    outline.SetPropertyBlock(properties);
                }
                entries.Add(new Entry { Actor = actor, Rect = rect, Button = button, Artwork = artwork, Outline = outline });
            }
        }

        public bool ChooseTarget(EnemyBattleActor actor)
        {
            if (host == null || actor == null || !host.Enemies.Contains(actor) || !actor.IsAlive || !host.CanChoosePlayTarget) return false;
            var choices = host.PlayTargetChoices.Where(choice => choice.Candidates.Any(candidate => candidate.Id == actor.DisplayId)).ToArray();
            if (choices.Length == 0 && host.Candidate?.State != CandidateState.Empty) return false;
            preferred = actor.DisplayId; pendingChoice = choices.Length == 0;
            foreach (var choice in choices) host.SelectPlayTarget(choice.Slot, preferred);
            return true;
        }

        private void LateUpdate()
        {
            if (host == null || canvas == null || viewCamera == null) return;
            EnsureTargets();
            if (preferred != null && !host.Enemies.Any(actor => actor.DisplayId == preferred && actor.IsAlive)) preferred = null;
            if (pendingChoice && preferred != null && host.CanChoosePlayTarget)
            {
                var choices = host.PlayTargetChoices.Where(choice => choice.Candidates.Any(candidate => candidate.Id == preferred)).ToArray();
                foreach (var choice in choices) host.SelectPlayTarget(choice.Slot, preferred);
                if (choices.Length > 0) pendingChoice = false;
            }
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            foreach (var entry in entries)
            {
                var unit = entry.Actor.DisplayUnit;
                bool alive = host.Enemies.Contains(entry.Actor) && entry.Actor.isActiveAndEnabled && unit != null && unit.IsAlive;
                entry.Rect.gameObject.SetActive(alive);
                if (entry.Outline != null) entry.Outline.enabled = false;
                if (!alive) continue;
                var surface = entry.Artwork != null ? entry.Artwork.transform : entry.Actor.transform;
                var low = viewCamera.WorldToScreenPoint(surface.TransformPoint(new Vector3(-.5f, -.5f, 0)));
                var high = viewCamera.WorldToScreenPoint(surface.TransformPoint(new Vector3(.5f, .5f, 0)));
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, low, uiCamera, out var min);
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, high, uiCamera, out var max);
                entry.Rect.anchoredPosition = (min + max) * .5f; entry.Rect.sizeDelta = max - min;
                var choice = host.PlayTargetChoices.FirstOrDefault(item => item.Candidates.Any(candidate => candidate.Id == unit.Id));
                bool selected = choice?.SelectedId == unit.Id || host.Candidate?.State == CandidateState.Empty && preferred == unit.Id;
                entry.Button.interactable = host.CanChoosePlayTarget && (choice != null || host.Candidate?.State == CandidateState.Empty);
                if (entry.Outline != null) entry.Outline.enabled = selected;

            }
        }
        private void OnDisable()
        {
            foreach (var entry in entries)
            { if (entry.Rect != null) entry.Rect.gameObject.SetActive(false); if (entry.Outline != null) entry.Outline.enabled = false; }
        }
        private void OnDestroy()
        {
            foreach (var entry in entries)
            { if (entry.Rect != null) Destroy(entry.Rect.gameObject); if (entry.Outline != null) Destroy(entry.Outline.gameObject); }
        }
    }
}
