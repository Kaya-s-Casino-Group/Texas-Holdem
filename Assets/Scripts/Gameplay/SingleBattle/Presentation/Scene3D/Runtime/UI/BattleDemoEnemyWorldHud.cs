using System.Collections.Generic;
using System.Linq;
using global::BattlePresentation;
using Poker.BattlePresentation;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>BattleDemo only. Each enemy owns a world-space canvas with intent, health, block and status rows.</summary>
    [DefaultExecutionOrder(150)]
    public sealed class BattleDemoEnemyWorldHud : MonoBehaviour
    {
        [SerializeField] private SingleBattleSceneAdapter host;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private TMP_FontAsset font;
        private Transform uiRoot;
        private BattleDisplaySnapshot previous;
        private readonly List<View> views = new List<View>();
        private sealed class IconView
        { internal RectTransform Root; internal BattleDemoEnemyIcon Icon; internal TMP_Text Value; internal BattleDemoEnemyBadgeTooltip Tooltip; }
        private sealed class View
        {
            internal EnemyBattleActor Actor;
            internal Transform Surface;
            internal RectTransform Root, Fill, Health, StatusRow, IntentRow, Tooltip;
            internal TMP_Text HealthText, TooltipText;
            internal IconView Block;
            internal BattleDemoEnemyBadgeTooltip OpenTooltip;
            internal readonly List<IconView> Intents = new List<IconView>(), Statuses = new List<IconView>();
        }
        public void Configure(SingleBattleSceneAdapter owner, Camera camera, TMP_FontAsset typeface)
        { host = owner; viewCamera = camera; font = typeface; }
        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.gameObject.layer = 5;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }
        private static Image Image(RectTransform rect, Color tint)
        { var graphic = rect.gameObject.AddComponent<Image>(); graphic.color = tint; graphic.raycastTarget = false; return graphic; }
        private TMP_Text Text(string name, RectTransform parent, Vector2 size, float fontSize)
        {
            var rect = Rect(name, parent, size, Vector2.zero);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = fontSize; label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false; label.enableWordWrapping = false; label.color = new Color32(249, 237, 221, 255);
            label.outlineWidth = .18f; label.outlineColor = new Color32(29, 21, 27, 255);
            return label;
        }
        private void Start()
        {
            if (host == null || viewCamera == null || font == null) { enabled = false; return; }
            var root = new GameObject("BattleDemo Enemy World UI");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene); uiRoot = root.transform;
            EnsureViews(); Refresh();
        }
        private void EnsureViews()
        {
            foreach (var actor in host.Enemies)
            {
                if (actor == null || views.Any(view => view.Actor == actor)) continue;
                var renderer = actor.Visual != null ? actor.Visual.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(item => item.name != "Selected enemy contour") : null;
                if (renderer == null) continue;
                var surface = renderer.transform;
                var rect = Rect("Enemy World HUD " + actor.name, uiRoot, new Vector2(240, 340), Vector2.zero);
                var canvas = rect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = viewCamera; canvas.sortingOrder = 20;
                rect.gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2;
                rect.gameObject.AddComponent<GraphicRaycaster>();
                var view = new View { Actor = actor, Surface = surface, Root = rect };
                view.Health = Rect("Health Bar", rect, new Vector2(184, 17), Vector2.zero);
                Image(view.Health, new Color32(28, 19, 25, 240));
                var border = view.Health.gameObject.AddComponent<Outline>(); border.effectColor = new Color32(113, 75, 80, 255); border.effectDistance = Vector2.one;
                view.Fill = Rect("Health Fill", view.Health, Vector2.zero, Vector2.zero);
                view.Fill.anchorMin = Vector2.zero; view.Fill.anchorMax = Vector2.one;
                view.Fill.offsetMin = Vector2.one; view.Fill.offsetMax = -Vector2.one;
                Image(view.Fill, new Color32(178, 40, 57, 255));
                view.HealthText = Text("Health Value", view.Health, new Vector2(180, 21), 13);
                view.Block = Icon("Current Block", rect, false, view);
                view.Block.Root.sizeDelta = new Vector2(46, 30);
                view.Block.Icon.rectTransform.sizeDelta = new Vector2(29, 29);
                view.Block.Icon.rectTransform.anchoredPosition = new Vector2(-7, 0);
                view.Block.Value.rectTransform.anchoredPosition = new Vector2(17, 0); view.Block.Value.fontSize = 17;
                view.StatusRow = Rect("Statuses", rect, new Vector2(184, 28), Vector2.zero);
                view.IntentRow = Rect("Intents", rect, new Vector2(210, 42), Vector2.zero);
                view.Tooltip = Rect("Enemy Badge Tooltip", rect, new Vector2(228, 94), Vector2.zero);
                Image(view.Tooltip, new Color32(25, 23, 32, 249));
                var tooltipBorder = view.Tooltip.gameObject.AddComponent<Outline>(); tooltipBorder.effectColor = new Color32(156, 140, 109, 255);
                view.TooltipText = Text("Tooltip Text", view.Tooltip, new Vector2(208, 78), 12);
                view.TooltipText.enableWordWrapping = true; view.TooltipText.alignment = TextAlignmentOptions.Left;
                view.Tooltip.gameObject.SetActive(false); views.Add(view);
            }
        }
        private IconView Icon(string name, RectTransform parent, bool status, View owner)
        {
            var rect = Rect(name, parent, status ? new Vector2(30, 29) : new Vector2(70, 42), Vector2.zero);
            var hit = Image(rect, new Color(0, 0, 0, .001f)); hit.raycastTarget = true;
            var icon = Rect("Icon", rect, status ? new Vector2(25, 25) : new Vector2(34, 34), status ? Vector2.zero : new Vector2(-16, 0))
                .gameObject.AddComponent<BattleDemoEnemyIcon>();
            var value = Text("Value", rect, status ? new Vector2(23, 15) : new Vector2(48, 31), status ? 11 : 23);
            value.rectTransform.anchoredPosition = status ? new Vector2(9, -8) : new Vector2(19, 0);
            var tooltip = rect.gameObject.AddComponent<BattleDemoEnemyBadgeTooltip>();
            tooltip.Show = description =>
            {
                owner.OpenTooltip = tooltip;
                owner.TooltipText.text = description;
                owner.Tooltip.gameObject.SetActive(true); owner.Tooltip.SetAsLastSibling();
                owner.Tooltip.anchoredPosition = rect.parent == owner.IntentRow ? owner.IntentRow.anchoredPosition + new Vector2(0, -73) : owner.StatusRow.anchoredPosition + new Vector2(0, 70);
            };
            tooltip.Hide = () =>
            {
                if (owner.OpenTooltip != tooltip) return;
                owner.OpenTooltip = null;
                if (owner.Tooltip != null) owner.Tooltip.gameObject.SetActive(false);
            };
            return new IconView { Root = rect, Icon = icon, Value = value, Tooltip = tooltip };
        }
        private static void Set(IconView view, BattleDemoEnemyHudModel.Badge badge, string detail = null)
        {
            view.Icon.Set(badge.Symbol, badge.Color); view.Value.text = badge.Value; view.Value.color = badge.Color;
            string description = detail ?? badge.Description;
            if (view.Tooltip.Description != description) view.Tooltip.Hide?.Invoke();
            view.Tooltip.Description = description;
        }
        private static void SetVisible(IconView view, bool visible)
        {
            // Editor refreshes and pooled badges cannot rely on MonoBehaviour.OnDisable alone.
            if (!visible) view.Tooltip.Hide?.Invoke();
            view.Root.gameObject.SetActive(visible);
        }
        private void Refresh()
        {
            EnsureViews();
            previous = host.Display;
            if (previous == null) return;
            foreach (var view in views)
            {
                var unit = previous.Unit(view.Actor.DisplayId);
                var regeneration = unit == null ? null : previous.Resource(unit.Id, "wrath.reviveRemaining");
                bool regenerating = unit != null && !unit.IsAlive && !previous.IsFinished && regeneration != null &&
                    previous.Labels.ContainsKey("wrath.hand/" + unit.Id);
                bool visible = host.Enemies.Contains(view.Actor) && view.Actor.isActiveAndEnabled && unit != null &&
                    (unit.IsAlive || regenerating);
                if (!visible) view.OpenTooltip?.Hide?.Invoke();
                view.Root.gameObject.SetActive(visible); if (!visible) continue;
                view.Health.gameObject.SetActive(!regenerating);
                view.StatusRow.gameObject.SetActive(!regenerating);
                view.HealthText.text = unit.Health + " / " + unit.MaxHealth;
                float fraction = unit.MaxHealth > 0 ? Mathf.Clamp01((float)unit.Health / unit.MaxHealth) : 0;
                view.Fill.anchorMin = new Vector2(1f / 184, 1f / 17);
                view.Fill.anchorMax = new Vector2((1 + 182 * fraction) / 184, 16f / 17);
                view.Fill.offsetMin = view.Fill.offsetMax = Vector2.zero;
                SetVisible(view.Block, !regenerating && unit.Block > 0);
                Set(view.Block, new BattleDemoEnemyHudModel.Badge(BattleDemoEnemySymbol.Shield, BattleDemoEnemyHudModel.ShieldColor, unit.Block.ToString(), "当前护盾：" + unit.Block));
                IReadOnlyList<BattleDemoEnemyHudModel.Badge> intents;
                if (regenerating)
                {
                    // Keep the original actor's world UI; the resolver owns the countdown and revival.
                    string remaining = regeneration.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    intents = new[] { new BattleDemoEnemyHudModel.Badge(BattleDemoEnemySymbol.Heal,
                        BattleDemoEnemyHudModel.AttackColor, remaining, regeneration.Value == 0
                            ? "再生：本轮敌方阶段再生" : "再生：剩余 " + remaining + " 轮") };
                }
                else
                {
                    var enemy = host.PresentedState.Duel.Enemies.FirstOrDefault(actor => actor.Id.Value == unit.Id);
                    var wrath = enemy?.Behavior?.Wrath;
                    long? charge = wrath != null && enemy.BehaviorState?.Intent?.Id == wrath.ChargeActionId
                        ? wrath.AttackPerCharge : (long?)null;
                    intents = previous.IsFinished ? new BattleDemoEnemyHudModel.Badge[0]
                        : BattleDemoEnemyHudModel.Intents(view.Actor.ReadIntent()?.Action, charge);
                    if (!previous.IsFinished && previous.Resource(unit.Id, "wrath.warning")?.Value == 1)
                    {
                        long release = previous.Resource(unit.Id, "wrath.releaseRound")?.Value ?? -1;
                        long damage = previous.Resource(unit.Id, "wrath.ultimateDamage")?.Value ?? 0;
                        intents = intents.Concat(new[] { new BattleDemoEnemyHudModel.Badge(BattleDemoEnemySymbol.Sword,
                            BattleDemoEnemyHudModel.AttackColor, "!", "大招预警：第 " + release +
                            " 轮常规动作后追加\n当前预计基础伤害 " + damage + "\n断手或眩晕本体可打断") }).ToArray();
                    }
                }
                while (view.Intents.Count < intents.Count) view.Intents.Add(Icon("Intent " + view.Intents.Count, view.IntentRow, false, view));
                for (int i = 0; i < view.Intents.Count; i++)
                {
                    var badge = view.Intents[i]; SetVisible(badge, i < intents.Count);
                    if (i >= intents.Count) continue;
                    Set(badge, intents[i]); badge.Root.anchoredPosition = new Vector2((i - (intents.Count - 1) * .5f) * 75, 0);
                }
                if (regenerating)
                {
                    foreach (var badge in view.Statuses) SetVisible(badge, false);
                    continue;
                }
                while (view.Statuses.Count < unit.Statuses.Count) view.Statuses.Add(Icon("Status " + view.Statuses.Count, view.StatusRow, true, view));
                for (int i = 0; i < view.Statuses.Count; i++)
                {
                    var badge = view.Statuses[i]; SetVisible(badge, i < unit.Statuses.Count);
                    if (i >= unit.Statuses.Count) continue;
                    var status = unit.Statuses[i]; var model = BattleDemoEnemyHudModel.Status(status.Id, status.Stacks);
                    var instance = host.PresentedState.Duel.Combat.Statuses.FirstOrDefault(item => item.DefinitionId.Value == status.Id && item.Target.Id == unit.Id);
                    var catalog = host.Engine.Duel.Definition.StatusCatalog;
                    var armor = catalog.Find(new StatusDefinitionId(status.Id))?.Behaviors.OfType<HitArmorSpec>().FirstOrDefault();
                    string description = model.Description + " ×" + status.Stacks;
                    if (armor != null)
                        description += "\n承受伤害 ×" + armor.Multiplier.ToString("0%") + (armor.Reduction > 0 ? "，再减少 " + armor.Reduction : "") +
                            "；每次命中消耗 " + armor.ConsumeStacks + " 层。";
                    else if (instance != null) description += "\n" + BattlePresentationRuleText.Status(instance, catalog);
                    Set(badge, model, description);
                    badge.Root.anchoredPosition = new Vector2(-77 + i % 6 * 31, -(i / 6) * 30);
                }
            }
        }
        private void LateUpdate()
        {
            if (host == null || viewCamera == null || uiRoot == null) return;
            if (!ReferenceEquals(previous, host.Display)) Refresh();
            foreach (var view in views)
            {
                if (view.Surface == null || !view.Root.gameObject.activeSelf) continue;
                var mesh = view.Surface.GetComponent<MeshFilter>().sharedMesh.bounds;
                float width = mesh.size.x * Mathf.Abs(view.Surface.lossyScale.x);
                float height = mesh.size.y * Mathf.Abs(view.Surface.lossyScale.y);
                float scale = Mathf.Max(.0001f, width / 240);
                view.Root.SetPositionAndRotation(view.Surface.TransformPoint(mesh.center) - viewCamera.transform.forward * .025f, viewCamera.transform.rotation);
                view.Root.localScale = Vector3.one * scale;
                float halfHeight = height / scale * .5f;
                view.Health.anchoredPosition = new Vector2(0, -halfHeight - 17);
                view.Block.Root.anchoredPosition = new Vector2(-120, -halfHeight - 17);
                view.StatusRow.anchoredPosition = new Vector2(0, -halfHeight - 46);
                view.IntentRow.anchoredPosition = new Vector2(0, halfHeight + 28);
            }
        }
        private void OnDisable()
        {
            foreach (var view in views) view.OpenTooltip?.Hide?.Invoke();
            if (uiRoot != null) uiRoot.gameObject.SetActive(false);
        }
        private void OnEnable() { if (uiRoot != null) { uiRoot.gameObject.SetActive(true); previous = null; } }
        private void OnDestroy() { if (uiRoot != null) Destroy(uiRoot.gameObject); }
    }

    public sealed class BattleDemoEnemyBadgeTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string Description { get; set; }
        public System.Action<string> Show;
        public System.Action Hide;
        public void OnPointerEnter(PointerEventData eventData) => Show?.Invoke(Description);
        public void OnPointerExit(PointerEventData eventData) => Hide?.Invoke();
        private void OnDisable() => Hide?.Invoke();
    }
}
