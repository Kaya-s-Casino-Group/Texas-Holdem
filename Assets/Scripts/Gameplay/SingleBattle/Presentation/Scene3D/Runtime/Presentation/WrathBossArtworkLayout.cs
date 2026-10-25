using System.Collections.Generic;
using System.Linq;
using global::BattlePresentation;
using UnityEngine;
using UIFoundation;

namespace Poker.SingleBattle
{
    /// <summary>Assembles separate actor planes using the PSD's shared pixel scale and stacking order.</summary>
    [DefaultExecutionOrder(120), DisallowMultipleComponent]
    public sealed class WrathBossArtworkLayout : MonoBehaviour
    {
        [SerializeField] private SingleBattleSceneAdapter host;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private WrathBossArtwork artwork;
        [SerializeField] private Rect viewportArea = new Rect(.22f, -.08f, .82f, 1.08f);
        [SerializeField] private float depth = 3.2f;
        private readonly Dictionary<EnemyBattleActor, WrathBossArtwork.Part> assigned = new Dictionary<EnemyBattleActor, WrathBossArtwork.Part>();
        private MeshRenderer warning;
        private BattleBillboardLayout warningLayout;
        private bool warningPromptVisible;
        public bool IsComposed { get; private set; }
        public WrathBossArtwork Artwork => artwork;
        public Rect CanvasViewport { get; private set; }
        public bool WarningVisible => warning != null && warning.enabled;
        public WrathBossArtwork.Part PartFor(EnemyBattleActor actor) => assigned.TryGetValue(actor, out var part) ? part : null;

        // Adventure warnings share the HUD's timed presentation; this does not consume the pending ultimate.
        public void SetWarningPromptVisible(bool visible)
        {
            warningPromptVisible = visible;
            if (!visible && warning != null) warning.enabled = false;
        }

        public void Configure(SingleBattleSceneAdapter owner, Camera camera, WrathBossArtwork definition)
        { host = owner; viewCamera = camera; artwork = definition; viewportArea = new Rect(.22f, -.08f, .82f, 1.08f); }

        public bool TryApply(BattleDisplaySnapshot snapshot)
        {
            if (host == null || viewCamera == null || artwork == null || snapshot == null ||
                !snapshot.Labels.TryGetValue("wrath.body", out var bodyId))
            { Clear(); return false; }
            PrepareCanvas();
            assigned.Clear();
            int blood = 0;
            // The explicit body/hand channels keep this independent of enemy roster order.
            foreach (var unit in snapshot.Units.Where(unit => unit.Team == "enemy"))
            {
                int index;
                if (unit.Id == bodyId) index = 0;
                else if (snapshot.Labels.TryGetValue("wrath.role/" + unit.Id, out var role))
                    index = role == "binding" ? 5 : ++blood;
                else continue;
                var actor = host.Enemies.FirstOrDefault(enemy => enemy.DisplayId == unit.Id);
                if (actor != null && index < artwork.Parts.Length) ApplyPart(actor, artwork.Parts[index]);
            }
            IsComposed = assigned.Count > 0;
            SetWarning(!snapshot.IsFinished && snapshot.Unit(bodyId)?.IsAlive == true &&
                snapshot.Resource(bodyId, "wrath.warning")?.Value == 1 &&
                (!host.UsesExternalCommands || warningPromptVisible));
            return IsComposed;
        }

        public void Preview()
        {
            if (host == null || viewCamera == null || artwork == null || host.Enemies.Count != 6) return;
            PrepareCanvas(); assigned.Clear();
            for (int i = 0; i < 6; i++) ApplyPart(host.Enemies[i], artwork.Parts[i]);
            IsComposed = true;
        }

        private void PrepareCanvas()
        {
            var pixels = viewCamera.pixelRect;
            var safe = Application.isPlaying ? ScreenSafeArea.CameraRect(viewCamera) : pixels;
            float aspect = viewCamera.aspect * (safe.width / pixels.width) / (safe.height / pixels.height);
            CanvasViewport = artwork.CanvasViewport(viewportArea, aspect);
        }

        private void ApplyPart(EnemyBattleActor actor, WrathBossArtwork.Part part)
        {
            if (part.Material == null || actor.Visual == null) return;
            var surface = actor.Visual.Find("Artwork")?.GetComponent<MeshRenderer>();
            if (surface == null) return;
            var ash = actor.Visual.GetComponent<BattleDemoAshPresenter>();
            if (ash != null) ash.ConfigureArtwork(part.Material);
            else if (surface.sharedMaterial != part.Material)
            { surface.sharedMaterial = part.Material; surface.SetPropertyBlock(null); }
            surface.sortingOrder = part.Order;
            var layout = actor.GetComponent<BattleBillboardLayout>();
            if (layout != null)
            {
                layout.SetDepth(depth);
                layout.SetArtworkAspect(part.Pixels.width / part.Pixels.height);
                layout.SetViewportArea(artwork.PartViewport(part, CanvasViewport));
                layout.Refresh();
            }
            assigned[actor] = part;
        }

        private void SetWarning(bool visible)
        {
            if (warning == null && visible && artwork.Warning?.Material != null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "Wrath PSD Warning Glow"; go.layer = host.Enemy.gameObject.layer;
                go.transform.SetParent(transform, false);
                var collider = go.GetComponent<Collider>();
                if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
                warning = go.GetComponent<MeshRenderer>();
                warning.sharedMaterial = artwork.Warning.Material;
                warning.sortingOrder = artwork.Warning.Order;
                warning.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                warning.receiveShadows = false;
                warningLayout = go.AddComponent<BattleBillboardLayout>();
                warningLayout.Configure(viewCamera, artwork.PartViewport(artwork.Warning, CanvasViewport),
                    depth, artwork.Warning.Pixels.width / artwork.Warning.Pixels.height);
            }
            if (warning == null) return;
            warning.enabled = visible;
            if (visible)
            {
                // Match actor billboards: viewport changes refit, camera shake never moves this
                // layer independently of the assembled body and hands.
                warningLayout.SetViewportArea(artwork.PartViewport(artwork.Warning, CanvasViewport));
                warningLayout.SetDepth(depth);
                warningLayout.Refresh();
            }
        }

        private void Clear()
        {
            if (IsComposed)
                foreach (var actor in assigned.Keys.Where(actor => actor != null))
                {
                    var surface = actor.Visual != null ? actor.Visual.Find("Artwork")?.GetComponent<MeshRenderer>() : null;
                    if (surface != null) surface.sortingOrder = 0;
                }
            assigned.Clear(); IsComposed = false;
            if (warning != null) warning.enabled = false;
        }

        // The workshop can override the PSD placement after composition. Keep the glow
        // attached to the resulting body instead of leaving it on the old viewport fit.
        public void AlignWarningToBody()
        {
            if (!IsComposed || warning == null || !warning.enabled || artwork.Warning == null) return;
            foreach (var pair in assigned)
            {
                if (pair.Key == null || pair.Value != artwork.Parts[0] || pair.Key.Visual == null) continue;
                var body = pair.Key.Visual.Find("Artwork");
                if (body == null) return;
                var pixels = pair.Value.Pixels;
                var glow = artwork.Warning.Pixels;
                var offset = new Vector3((glow.center.x - pixels.x) / pixels.width - .5f,
                    .5f - (glow.center.y - pixels.y) / pixels.height, 0);
                warning.transform.SetPositionAndRotation(body.TransformPoint(offset), body.rotation);
                var size = Vector3.Scale(body.lossyScale, new Vector3(glow.width / pixels.width, glow.height / pixels.height, 1));
                var parent = warning.transform.parent != null ? warning.transform.parent.lossyScale : Vector3.one;
                warning.transform.localScale = new Vector3(size.x / parent.x, size.y / parent.y, size.z / parent.z);
                return;
            }
        }

        private void LateUpdate() { if (Application.isPlaying) TryApply(host != null ? host.Display : null); }
        private void OnDisable() { if (warning != null) warning.enabled = false; }
    }
}
