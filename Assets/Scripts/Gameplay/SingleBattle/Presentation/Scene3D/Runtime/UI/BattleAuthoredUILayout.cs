using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UIFoundation;

namespace Poker.SingleBattle
{
    // Run after DeskBattleLayout (550), leaving gameplay presenters and safe-area handling in charge.
    [DefaultExecutionOrder(650), DisallowMultipleComponent]
    public sealed class BattleAuthoredUILayout : MonoBehaviour
    {
        [Serializable] public sealed class Root { public string name; public Transform transform; }
        [SerializeField] private BattleUILayoutProfile profile;
        [SerializeField] private Root[] roots = Array.Empty<Root>();
        private readonly Dictionary<string, RectTransform> targets = new Dictionary<string, RectTransform>();
        private BattleUILayoutRootState[] rootStates = Array.Empty<BattleUILayoutRootState>();
        private Root[] observedRoots;
        private BattleUILayoutProfile observedProfile;
        private BattleUILayoutProfile.Node[] observedNodes;
        private BattleUILayoutProfile.CanvasSettings[] observedCanvases;
        private int observedProfileVersion, observedDeskVersion;
        private DeskBattleLayout deskLayout;
        private bool dirty = true;
        public BattleUILayoutProfile Profile => profile;
        public int LayoutVersion { get; private set; }
        public void Configure(BattleUILayoutProfile value, Root[] bindings)
        { profile = value; roots = bindings ?? Array.Empty<Root>(); InvalidateLayout(); }
        public void InvalidateLayout() { dirty = true; targets.Clear(); }
        private void OnEnable() => InvalidateLayout();
        private void OnValidate() => InvalidateLayout();
        private void LateUpdate() => RefreshLayout();

        // Desk creates and styles runtime controls first. The authored profile has the final geometry,
        // and is reapplied only after those controls or their reference rectangle actually change.
        public bool RefreshLayout()
        {
            if (profile == null) return false;
            bool changed = ObserveLayout();
            if (!dirty && !changed) return false;
            Apply(); return true;
        }

        private bool ObserveLayout()
        {
            if (deskLayout == null) deskLayout = GetComponent<DeskBattleLayout>();
            int deskVersion = deskLayout != null ? deskLayout.LayoutVersion : 0;
            bool changed = observedProfile != profile || observedProfileVersion != profile.Version ||
                observedNodes != profile.nodes || observedCanvases != profile.canvases || observedDeskVersion != deskVersion;
            if (observedRoots != roots || rootStates.Length != roots.Length)
            {
                rootStates = new BattleUILayoutRootState[roots.Length];
                for (int i = 0; i < rootStates.Length; i++) rootStates[i] = new BattleUILayoutRootState();
                observedRoots = roots; changed = true;
            }
            for (int i = 0; i < roots.Length; i++) changed |= rootStates[i].Observe(roots[i]?.transform);
            if (changed) targets.Clear(); // A runtime control can replace a previously resolved node.
            observedProfile = profile; observedProfileVersion = profile.Version;
            observedNodes = profile.nodes; observedCanvases = profile.canvases; observedDeskVersion = deskVersion;
            return changed;
        }
        public Transform FindRoot(string name)
        { foreach (var root in roots) if (root.name == name) return root.transform; return null; }
        public RectTransform Resolve(string rootName, string path)
        {
            var key = rootName + "/" + path;
            if (targets.TryGetValue(key, out var found) && found != null) return found;
            var root = FindRoot(rootName); if (root == null) return null;
            found = (root.Find(path) ?? root.Find("Safe Area/" + path)) as RectTransform;
            if (found != null) targets[key] = found;
            return found;
        }
        public void Apply()
        {
            if (profile == null) return;
            foreach (var canvas in profile.canvases)
            {
                var root = FindRoot(canvas.root);
                if (root == null || !root.TryGetComponent<CanvasScaler>(out var scaler)) continue;
                canvas.Apply(scaler);
                if (Application.isPlaying && root.TryGetComponent<Canvas>(out var owner) && owner.isActiveAndEnabled)
                {
                    ScreenSafeArea.Ensure(owner)?.SetReferenceSize(canvas.referenceResolution);
                    if (root.TryGetComponent<DeskBattleLayout>(out var desk) && canvas.referenceResolution.y > 0)
                        desk.SetWorldReferenceAspect(canvas.referenceResolution.x / canvas.referenceResolution.y);
                }
            }
            foreach (var node in profile.nodes)
            {
                if (!BattleUILayoutProfile.Includes(node.root, node.pose.path)) continue;
                var target = Resolve(node.root, node.pose.path);
                if (target != null) node.Apply(target);
            }
            dirty = false; LayoutVersion++;
            ObserveLayout();
        }
    }

    // A constant-size observation of a Canvas/root: no hierarchy traversal, strings or per-frame arrays.
    // hierarchyCount also catches descendants created while a page is closed; active state handles reopening.
    internal sealed class BattleUILayoutRootState
    {
        private Transform root;
        private Canvas canvas;
        private Rect rect, pixelRect, safeArea;
        private Vector2 screenSize, displaySize;
        private float canvasScale;
        private int hierarchyCount;
        private bool active, initialized;

        public bool Observe(Transform value)
        {
            bool replaced = root != value;
            if (replaced || !initialized)
            {
                root = value;
                canvas = value != null ? value.GetComponentInParent<Canvas>(true) : null;
            }
            // Authored poses can resize a watched child. Only the reference Canvas size invalidates
            // layout; observing our own child output would make Desk/profile alternate forever.
            var reference = canvas != null ? canvas.transform : value;
            var nextRect = reference is RectTransform transform ? transform.rect : default;
            var nextPixels = canvas != null ? canvas.pixelRect : default;
            var nextDisplay = canvas != null ? canvas.renderingDisplaySize : Vector2.zero;
            float nextScale = canvas != null ? canvas.scaleFactor : 0;
            var nextScreen = new Vector2(Screen.width, Screen.height);
            var nextSafe = Screen.safeArea;
            int nextCount = value != null ? value.hierarchyCount : 0;
            bool nextActive = value != null && value.gameObject.activeInHierarchy;
            bool changed = !initialized || replaced || rect != nextRect || pixelRect != nextPixels || displaySize != nextDisplay ||
                canvasScale != nextScale || screenSize != nextScreen || safeArea != nextSafe || hierarchyCount != nextCount || active != nextActive;
            rect = nextRect; pixelRect = nextPixels; displaySize = nextDisplay; canvasScale = nextScale;
            screenSize = nextScreen; safeArea = nextSafe; hierarchyCount = nextCount; active = nextActive; initialized = true;
            return changed;
        }
    }
}
