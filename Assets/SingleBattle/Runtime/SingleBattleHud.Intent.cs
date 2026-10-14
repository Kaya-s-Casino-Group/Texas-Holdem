using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleHud
    {
        [Header("敌人意图（世界空间）")]
        [SerializeField, Min(.00001f)] private float intentWorldUnitsPerPixel = .001f;
        [SerializeField] private Vector3 intentWorldOffset = new Vector3(0, -.012f, 0);

        private static readonly Vector2 IntentSize = new Vector2(104, 26);

        private sealed class IntentView
        {
            public RectTransform Root;
            public TMP_Text Text;
            public Renderer[] Renderers;
            public bool Visible;
        }
        private readonly Dictionary<EnemyBattleActor, IntentView> enemyIntents = new Dictionary<EnemyBattleActor, IntentView>();
        private Transform worldUIRoot;

        private Transform EnsureWorldUIRoot()
        {
            if (worldUIRoot != null) return worldUIRoot;
            // Scene root with identity transform: actor and HUD scaling must not distort world UI.
            var root = new GameObject("WorldUIRoot");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            worldUIRoot = root.transform;
            return worldUIRoot;
        }

        private void RefreshEnemyIntents()
        {
            if (intent == null) return;
            foreach (var actor in host.Enemies)
            {
                if (!enemyIntents.TryGetValue(actor, out var view))
                {
                    var frame = new GameObject("Enemy Intent " + (enemyIntents.Count + 1), typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(Image));
                    frame.layer = gameObject.layer;
                    frame.transform.SetParent(EnsureWorldUIRoot(), false);
                    var canvas = frame.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.WorldSpace;
                    frame.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10;
                    var root = (RectTransform)frame.transform;
                    root.pivot = new Vector2(.5f, 1);
                    root.sizeDelta = IntentSize;
                    var background = frame.GetComponent<Image>();
                    background.color = new Color32(25, 30, 35, 218); background.raycastTarget = false;
                    var label = Instantiate(intent, root);
                    label.gameObject.SetActive(true);
                    label.transform.SetParent(root, false);
                    label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                    label.rectTransform.offsetMin = new Vector2(4, 2); label.rectTransform.offsetMax = new Vector2(-4, -2);
                    label.fontSize = 14; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
                    view = new IntentView { Root = root, Text = label,
                        Renderers = actor.Visual != null ? actor.Visual.GetComponentsInChildren<Renderer>(true) : new Renderer[0] };
                    enemyIntents.Add(actor, view);
                }
                var action = actor.ReadIntent();
                view.Visible = action != null;
                view.Text.text = action == null ? "" : "攻击 " + action.Damage;
                view.Root.gameObject.SetActive(view.Visible);
            }
            intent.gameObject.SetActive(false);
            UpdateEnemyIntentBillboards();
        }

        private void UpdateEnemyIntentBillboards()
        {
            if (enemyIntents.Count == 0) return;
            var camera = ResolveIntentCamera();
            foreach (var pair in enemyIntents)
            {
                var actor = pair.Key; var view = pair.Value;
                if (!view.Visible || actor == null || actor.Visual == null || !actor.Visual.gameObject.activeInHierarchy || camera == null)
                { if (view.Root != null) view.Root.gameObject.SetActive(false); continue; }
                // Keep a real world-space anchor and scale; distance naturally changes its apparent size.
                view.Root.position = IntentBase(actor.Visual, view.Renderers) + intentWorldOffset;
                view.Root.localScale = Vector3.one * intentWorldUnitsPerPixel;
                var forward = view.Root.position - camera.transform.position;
                view.Root.rotation = IntentRotation(camera);
                view.Root.GetComponent<Canvas>().worldCamera = camera;
                view.Root.gameObject.SetActive(Vector3.Dot(forward, camera.transform.forward) > 0);
            }
        }

        private Camera ResolveIntentCamera()
        {
            var camera = Camera.main;
            return camera != null || host == null || host.Actions == null ? camera :
                host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().FacingCamera;
        }

        private static Vector3 IntentBase(Transform visual, Renderer[] renderers)
        {
            var bounds = new Bounds(visual.position, Vector3.zero); bool hasBounds = false;
            foreach (var renderer in renderers)
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                { if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; } else bounds.Encapsulate(renderer.bounds); }
            return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        // Screen-aligned in both projection modes; keep world size for perspective distance scaling.
        private static Quaternion IntentRotation(Camera camera) => camera.transform.rotation;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (host == null) return;
            var camera = ResolveIntentCamera();
            if (camera == null && UnityEditor.SceneView.currentDrawingSceneView != null)
                camera = UnityEditor.SceneView.currentDrawingSceneView.camera;
            if (camera == null) return;
            var savedColor = Gizmos.color; var savedMatrix = Gizmos.matrix;
            try
            {
                foreach (var actor in host.Enemies)
                {
                    if (actor == null || actor.Visual == null || !actor.Visual.gameObject.activeInHierarchy ||
                        Application.isPlaying && actor.IsBound && !actor.IsAlive) continue;
                    var basis = IntentBase(actor.Visual, actor.Visual.GetComponentsInChildren<Renderer>(true));
                    var position = basis + intentWorldOffset; var rotation = IntentRotation(camera);
                    // Match the Canvas under the identity WorldUIRoot, independent of actor scale.
                    var matrix = Matrix4x4.TRS(position, rotation,
                        Vector3.one * intentWorldUnitsPerPixel);
                    Gizmos.matrix = Matrix4x4.identity;
                    Gizmos.color = new Color(.25f, 1, .8f, .9f);
                    Gizmos.DrawWireSphere(basis, .002f); Gizmos.DrawLine(basis, position);
                    Gizmos.matrix = matrix; Gizmos.color = new Color(1, .75f, .25f, .95f);
                    var center = new Vector3(0, -IntentSize.y * .5f, 0);
                    Gizmos.DrawWireCube(center, new Vector3(IntentSize.x, IntentSize.y, 0));
                    Gizmos.color = new Color(.3f, .75f, 1, .95f);
                    var tip = center + Vector3.back * 20;
                    Gizmos.DrawLine(center, tip);
                    Gizmos.DrawLine(tip, tip + new Vector3(-4, 0, 6));
                    Gizmos.DrawLine(tip, tip + new Vector3(4, 0, 6));
                    UnityEditor.Handles.Label(matrix.MultiplyPoint3x4(new Vector3(-IntentSize.x * .5f, -IntentSize.y - 5, 0)),
                        actor.name + " · 意图 Billboard", UnityEditor.EditorStyles.miniLabel);
                }
            }
            finally { Gizmos.color = savedColor; Gizmos.matrix = savedMatrix; }
        }
#endif

        private void DestroyEnemyIntents()
        {
            foreach (var view in enemyIntents.Values) if (view.Root != null) Destroy(view.Root.gameObject);
            enemyIntents.Clear();
            if (worldUIRoot != null) Destroy(worldUIRoot.gameObject);
            worldUIRoot = null;
        }

        private void HideEnemyIntents()
        { foreach (var view in enemyIntents.Values) if (view.Root != null) view.Root.gameObject.SetActive(false); }
    }
}
