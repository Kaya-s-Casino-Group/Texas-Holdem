using System.Linq;
using UnityEngine;
using UIFoundation;

namespace Poker.SingleBattle
{
    // After adventure (100) and boss composition (120), before enemy HUD positioning (150).
    [DefaultExecutionOrder(140), DisallowMultipleComponent]
    public sealed class BattleAuthoredLayout : MonoBehaviour
    {
        [SerializeField] private SingleBattleSceneAdapter host;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private BattleLayoutProfile profile;
        private Vector3 cameraPosition;
        private Quaternion cameraRotation;
        private bool hasCameraPose;
        public BattleLayoutProfile Profile => profile;
        public void Configure(SingleBattleSceneAdapter owner, Camera camera, BattleLayoutProfile value)
        { host = owner; viewCamera = camera; profile = value; hasCameraPose = false; }
        private void OnEnable() => hasCameraPose = false;
        private void LateUpdate() => ApplyActors();

        public void ApplyActors()
        {
            if (host == null || viewCamera == null || profile == null || host.Display == null) return;
            // Keep authored actors in the scene during camera shake, as BattleBillboardLayout does.
            if (!hasCameraPose || !Application.isPlaying)
            { cameraPosition = viewCamera.transform.position; cameraRotation = viewCamera.transform.rotation; hasCameraPose = true; }
            var safe = Application.isPlaying ? ScreenSafeArea.CameraRect(viewCamera) : viewCamera.pixelRect;
            var pixels = viewCamera.pixelRect;
            if (pixels.width <= 0 || pixels.height <= 0) return;
            foreach (var actor in host.Enemies)
            {
                var entry = profile.Find(host.ContentEncounterId, actor.DisplayId);
                if (entry == null)
                {
                    // Summons use adaptive roster placement, with ordinary artwork geometry.
                    if (host.UsesExternalCommands) ApplyArtwork(actor, profile.Find("single-chapter-battle-1", "enemy"));
                    continue;
                }
                float height = viewCamera.orthographic ? 2 * viewCamera.orthographicSize :
                    2 * entry.depth * Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * .5f);
                // Position and size must use the same fitted artboard. Stretching only the
                // centers separates the boss's body and hands when the window aspect changes.
                var board = ScreenSafeArea.FitAspect(safe, entry.referenceAspect);
                var viewport = new Vector2((board.x - pixels.x + board.width * entry.actorViewport.x) / pixels.width,
                    (board.y - pixels.y + board.height * entry.actorViewport.y) / pixels.height);
                actor.transform.SetPositionAndRotation(cameraPosition + cameraRotation * new Vector3(
                    (viewport.x - .5f) * height * viewCamera.aspect, (viewport.y - .5f) * height, entry.depth),
                    cameraRotation * entry.cameraRelativeRotation);
                float fit = board.height / pixels.height;
                var size = entry.actorScale * (height / entry.referenceFrustumHeight * fit);
                var parentScale = actor.transform.parent != null ? actor.transform.parent.lossyScale : Vector3.one;
                actor.transform.localScale = new Vector3(size.x / parentScale.x, size.y / parentScale.y, size.z / parentScale.z);
                ApplyArtwork(actor, entry);
            }
            host.GetComponent<WrathBossArtworkLayout>()?.AlignWarningToBody();
        }

        static void ApplyArtwork(EnemyBattleActor actor, BattleLayoutProfile.Entry entry)
        {
            var art = actor.Visual != null ? actor.Visual.Find("Artwork") : null;
            if (art == null || entry == null) return;
            art.localPosition = entry.artworkPosition; art.localRotation = entry.artworkRotation; art.localScale = entry.artworkScale;
        }

        public void ApplyHud(EnemyBattleActor actor, RectTransform root, Canvas canvas)
        {
            if (!enabled || profile == null || viewCamera == null || host == null) return;
            var entry = profile.Find(host.ContentEncounterId, actor.DisplayId);
            if (entry == null && host.UsesExternalCommands) entry = profile.Find("single-chapter-battle-1", "enemy");
            if (entry == null) return;
            entry.hudRoot.Apply(root, false);
            var safe = Application.isPlaying ? ScreenSafeArea.CameraRect(viewCamera) : viewCamera.pixelRect;
            var board = ScreenSafeArea.FitAspect(safe, entry.referenceAspect);
            float pixelScale = board.height / Mathf.Max(1, entry.referenceHudHeight);
            root.localScale *= pixelScale / Mathf.Max(.001f, canvas.scaleFactor);
            Vector2 screen;
            if (entry.fixedHud)
            {
                screen = board.position + Vector2.Scale(board.size, entry.hudViewport);
            }
            else screen = (Vector2)viewCamera.WorldToScreenPoint(actor.transform.position) + entry.hudOffset * pixelScale;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)root.parent, screen, null, out var local);
            // Convert from parent-local coordinates to the authored anchor reference.
            var parent = (RectTransform)root.parent;
            var anchor = Vector2.LerpUnclamped(root.anchorMin, root.anchorMax, root.pivot.x);
            anchor.y = Mathf.LerpUnclamped(root.anchorMin.y, root.anchorMax.y, root.pivot.y);
            root.anchoredPosition = local - parent.rect.min - Vector2.Scale(parent.rect.size, anchor);
            foreach (var node in entry.nodes)
            {
                var target = root.Find(node.path) as RectTransform;
                if (target != null) node.Apply(target);
            }
            ExtendBadges(entry, root, "Intents", "Intent ", 2);
            ExtendBadges(entry, root, "Statuses", "Status ", 3);
        }

        // Badges created after the snapshot inherit authored geometry without copying their old values.
        static void ExtendBadges(BattleLayoutProfile.Entry entry, RectTransform root, string rowName, string prefix, int columns)
        {
            var row = root.Find(rowName);
            if (row == null) return;
            var template = entry.nodes.FirstOrDefault(n => n.path == rowName + "/" + prefix + "0");
            if (template == null) return;
            string basePath = template.path;
            foreach (RectTransform badge in row)
            {
                if (!badge.name.StartsWith(prefix) || !int.TryParse(badge.name.Substring(prefix.Length), out int slot) ||
                    entry.nodes.Any(n => n.path == rowName + "/" + badge.name)) continue;
                template.Apply(badge);
                badge.anchoredPosition += new Vector2(slot % columns * (Mathf.Abs(template.sizeDelta.x * template.scale.x) + 4),
                    -(slot / columns) * (Mathf.Abs(template.sizeDelta.y * template.scale.y) + 4));
                foreach (var child in entry.nodes.Where(n => n.path.StartsWith(basePath + "/")))
                {
                    var target = badge.Find(child.path.Substring(basePath.Length + 1)) as RectTransform;
                    if (target != null) child.Apply(target);
                }
            }
        }
    }
}
