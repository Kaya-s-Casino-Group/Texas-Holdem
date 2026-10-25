using System.Collections.Generic;
using System.Linq;
using global::BattlePresentation;
using Poker.BattlePresentation;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UIFoundation;

namespace Poker.SingleBattle
{
    /// <summary>Screen-space enemy HUDs follow artwork anchors without shrinking with world depth.</summary>
    [DefaultExecutionOrder(150)]
    public sealed class BattleDemoEnemyWorldHud : MonoBehaviour
    {
        private const float StatusBadgeScale = 1.3f;
        [SerializeField] private SingleBattleSceneAdapter host;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private bool conceptLayout;
        public void ConfigureConceptLayout(bool value) { conceptLayout = value; }
        private Transform uiRoot;
        private Canvas screenCanvas;
        private BattleAuthoredLayout authoredLayout;
        private BattleDisplaySnapshot previous;
        private readonly List<View> views = new List<View>();
        private sealed class IconView
        { internal RectTransform Root; internal BattleDemoEnemyIcon Icon; internal Image Art; internal TMP_Text Value, Caption; internal BattleDemoEnemyBadgeTooltip Tooltip; internal bool Status; }
        private sealed class View
        {
            internal EnemyBattleActor Actor;
            internal Transform Surface;
            internal RectTransform Root, Fill, Health, StatusRow, IntentRow, Tooltip;
            internal TMP_Text HealthText, TooltipText, Regeneration;
            internal CanvasGroup Visibility;
            internal IconView Block;
            internal BattleDemoEnemyBadgeTooltip OpenTooltip;
            internal long ShellStacks;
            internal float ShellExhaustedUntil;
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
            authoredLayout = host.GetComponent<BattleAuthoredLayout>();
            // Preserve the object/component names used by existing scenes and validation tools.
            var root = new GameObject("BattleDemo Enemy World UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene); uiRoot = root.transform;
            root.layer = 5;
            screenCanvas = root.GetComponent<Canvas>();
            // Badge hover must win over the portrait target canvas (20), below the main HUD (30).
            screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay; screenCanvas.sortingOrder = 21;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 1;
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
                var view = new View { Actor = actor, Surface = surface, Root = rect,
                    Visibility = rect.gameObject.AddComponent<CanvasGroup>() };
                view.Health = Rect("Health Bar", rect, new Vector2(184, 17), Vector2.zero);
                Image(view.Health, new Color32(28, 19, 25, 240));
                var border = view.Health.gameObject.AddComponent<Outline>(); border.effectColor = new Color32(113, 75, 80, 255); border.effectDistance = Vector2.one;
                view.Fill = Rect("Health Fill", view.Health, Vector2.zero, Vector2.zero);
                view.Fill.anchorMin = Vector2.zero; view.Fill.anchorMax = Vector2.one;
                view.Fill.offsetMin = Vector2.one; view.Fill.offsetMax = -Vector2.one;
                Image(view.Fill, new Color32(178, 40, 57, 255));
                view.HealthText = Text("Health Value", view.Health, new Vector2(180, 21), 13);
                if (conceptLayout) view.Health.gameObject.AddComponent<ConceptHealthBar>();
                view.Block = Icon("Current Block", rect, conceptLayout, view);
                if (!conceptLayout)
                {
                    view.Block.Root.sizeDelta = new Vector2(46, 30);
                    view.Block.Icon.rectTransform.sizeDelta = new Vector2(29, 29);
                    view.Block.Icon.rectTransform.anchoredPosition = new Vector2(-7, 0);
                    view.Block.Value.rectTransform.anchoredPosition = new Vector2(17, 0); view.Block.Value.fontSize = 17;
                }
                view.StatusRow = Rect("Statuses", rect, new Vector2(184, 28), Vector2.zero);
                view.Regeneration = Text("Revive Countdown", rect, new Vector2(200, 36), 20);
                view.Regeneration.color = new Color32(193, 136, 239, 255);
                view.Regeneration.outlineWidth = .25f;
                view.Regeneration.outlineColor = new Color32(35, 16, 49, 255);
                view.Regeneration.gameObject.SetActive(false);
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
            var rect = Rect(name, parent, status ? new Vector2(30, 29) * StatusBadgeScale : new Vector2(70, 42), Vector2.zero);
            var hit = Image(rect, new Color(0, 0, 0, .001f)); hit.raycastTarget = true;
            var icon = Rect("Icon", rect, status ? new Vector2(25, 25) * StatusBadgeScale : new Vector2(34, 34), status ? Vector2.zero : new Vector2(-16, 0))
                .gameObject.AddComponent<BattleDemoEnemyIcon>();
            var value = Text("Value", rect, status ? new Vector2(23, 15) * StatusBadgeScale : new Vector2(48, 31), status ? 11 * StatusBadgeScale : 23);
            value.rectTransform.anchoredPosition = status ? new Vector2(9, -8) * StatusBadgeScale : new Vector2(19, 0);
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
            Image art = null; TMP_Text caption = null;
            if (conceptLayout)
            {
                ConceptBattleSkin.Surface(rect, status ? "Panels/panel_counter" : "Panels/panel_enemy_intent", false, true);
                art = ConceptBattleSkin.Art(rect, "Concept Icon", "Icons/icon_attack_sword", new Rect(.08f, .16f, .24f, .68f));
                value.rectTransform.anchorMin = Vector2.zero; value.rectTransform.anchorMax = Vector2.one;
                value.rectTransform.offsetMin = new Vector2(status ? 23 : 38, 3); value.rectTransform.offsetMax = new Vector2(-8, -3);
                value.rectTransform.pivot = new Vector2(.5f,.5f);
                value.fontSize = status ? 12 * StatusBadgeScale : 23; value.enableAutoSizing = true; value.fontSizeMin = status ? 8 * StatusBadgeScale : 15;
                value.fontSizeMax = status ? 12 * StatusBadgeScale : 23; value.overflowMode = TextOverflowModes.Overflow;
                if (!status) caption = ConceptBattleSkin.Text(rect, "Action Caption", font, new Rect(.64f,.12f,.32f,.76f), "下次行动", 9);
                value.transform.SetAsLastSibling();
            }
            BattleBadgeText.Apply(value);
            return new IconView { Root = rect, Icon = icon, Art = art, Value = value, Caption = caption, Tooltip = tooltip, Status = status };
        }
        private static void Set(IconView view, BattleDemoEnemyHudModel.Badge badge, string detail = null)
        {
            view.Icon.Set(badge.Symbol, badge.Color); view.Value.text = badge.Value; view.Value.color = badge.Color;
            if (view.Art != null)
            {
                string key = ConceptIcon(badge.Symbol);
                view.Art.gameObject.SetActive(key != null); view.Icon.enabled = key == null;
                if (key != null) view.Art.sprite = ConceptBattleSkin.Get("Icons/" + key);
                view.Value.color = badge.Symbol == BattleDemoEnemySymbol.Sword ? new Color32(209, 67, 72, 255) : ConceptBattleSkin.Paper;
                if (view.Status) view.Value.text = badge.Symbol == BattleDemoEnemySymbol.Shield ? "护盾 " + badge.Value : badge.Description.Length <= 4 ? badge.Description + " " + badge.Value : badge.Value;
                if (view.Caption != null)
                {
                    // Keep the longer forecast label inside its column, clear of the icon and damage number.
                    view.Caption.margin = badge.Symbol == BattleDemoEnemySymbol.Sword ? new Vector4(-8, 0, 0, 0) : Vector4.zero;
                    view.Caption.text = badge.Symbol == BattleDemoEnemySymbol.Wait
                        ? (badge.Description == "准备行动" ? "准备\n<size=80%>即将行动</size>" : "等待\n<size=80%>下回合</size>")
                        : ConceptCaption(badge.Symbol) + (badge.Symbol == BattleDemoEnemySymbol.Sword ? "" : "\n<size=80%>下次行动</size>");
                }
            }
            BattleBadgeText.Apply(view.Value);
            string description = detail ?? badge.Description;
            if (view.Tooltip.Description != description) view.Tooltip.Hide?.Invoke();
            view.Tooltip.Description = description;
        }
        private static string ConceptIcon(BattleDemoEnemySymbol symbol)
        {
            switch(symbol)
            {
                case BattleDemoEnemySymbol.Wait: return null;
                case BattleDemoEnemySymbol.Sword: return "icon_attack_sword";
                case BattleDemoEnemySymbol.Shield: case BattleDemoEnemySymbol.Armor: return "icon_shield";
                case BattleDemoEnemySymbol.Poison: return "icon_poison";
                case BattleDemoEnemySymbol.Stun: return "icon_stun";
                case BattleDemoEnemySymbol.Immunity: return "icon_immunity";
                case BattleDemoEnemySymbol.Reflect: return "icon_reflect";
                case BattleDemoEnemySymbol.LifeSteal: case BattleDemoEnemySymbol.Bleeding: return "icon_bleeding";
                case BattleDemoEnemySymbol.Overheal: return "icon_overheal";
                case BattleDemoEnemySymbol.Heal: return "icon_heal";
                case BattleDemoEnemySymbol.Buff: return "icon_buff";
                case BattleDemoEnemySymbol.Debuff: return "icon_weakness";
                case BattleDemoEnemySymbol.Cards: return "icon_draw_pile";
                case BattleDemoEnemySymbol.Cleanse: return "icon_cleanse";
                default: return "icon_unknown";
            }
        }
        private static string ConceptCaption(BattleDemoEnemySymbol symbol)
        {
            switch(symbol)
            {
                case BattleDemoEnemySymbol.Sword: return "<size=9>预计\n伤害</size>";
                case BattleDemoEnemySymbol.Shield: return "防御";
                case BattleDemoEnemySymbol.Poison: return "中毒";
                case BattleDemoEnemySymbol.Stun: return "眩晕";
                case BattleDemoEnemySymbol.Immunity: return "免疫";
                case BattleDemoEnemySymbol.Reflect: return "盾反";
                case BattleDemoEnemySymbol.LifeSteal: return "吸血";
                case BattleDemoEnemySymbol.Bleeding: return "流血";
                case BattleDemoEnemySymbol.Overheal: return "溢疗";
                case BattleDemoEnemySymbol.Armor: return "护甲";
                case BattleDemoEnemySymbol.Heal: return "治疗";
                case BattleDemoEnemySymbol.Buff: return "蓄力";
                case BattleDemoEnemySymbol.Debuff: return "破盾";
                case BattleDemoEnemySymbol.Cards: return "抽牌";
                case BattleDemoEnemySymbol.Cleanse: return "净化";
                default: return "特殊";
            }
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
            if (previous != null && host.Display != null && host.Display.CompletedRounds < previous.CompletedRounds)
                foreach (var view in views) { view.ShellStacks = 0; view.ShellExhaustedUntil = 0; }
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
                if (!visible || regenerating) view.OpenTooltip?.Hide?.Invoke();
                view.Root.gameObject.SetActive(visible); if (!visible) continue;
                view.Health.gameObject.SetActive(!regenerating);
                view.StatusRow.gameObject.SetActive(!regenerating);
                view.IntentRow.gameObject.SetActive(!regenerating);
                view.Regeneration.gameObject.SetActive(regenerating);
                SetVisible(view.Block, !regenerating && unit.Block > 0);
                if (regenerating)
                {
                    // A severed hand only shows its countdown, never the normal HUD or intent plate.
                    view.Regeneration.text = regeneration.Value == 0 ? "本轮复活" : "复活倒计时 " + regeneration.Value;
                    continue;
                }
                bool bossBody = host.UsesExternalCommands && previous.Labels.TryGetValue("wrath.body", out var bodyId) && bodyId == unit.Id;
                PresentHealth(view, unit, bossBody);
                float fraction = unit.MaxHealth > 0 ? Mathf.Clamp01((float)unit.Health / unit.MaxHealth) : 0;
                view.Fill.anchorMin = new Vector2(1f / 184, 1f / 17);
                view.Fill.anchorMax = new Vector2((1 + 182 * fraction) / 184, 16f / 17);
                view.Fill.offsetMin = view.Fill.offsetMax = Vector2.zero;
                if (conceptLayout) view.Health.GetComponent<ConceptHealthBar>().Present(bossBody ? "boss" : "minion", fraction);
                Set(view.Block, new BattleDemoEnemyHudModel.Badge(BattleDemoEnemySymbol.Shield, BattleDemoEnemyHudModel.ShieldColor, unit.Block.ToString(), "当前护盾：" + unit.Block));
                var enemy = host.PresentedState.Duel.Enemies.FirstOrDefault(actor => actor.Id.Value == unit.Id);
                var wrath = enemy?.Behavior?.Wrath;
                long? charge = wrath != null && enemy.BehaviorState?.Intent?.Id == wrath.ChargeActionId
                    ? wrath.AttackPerCharge : (long?)null;
                IReadOnlyList<BattleDemoEnemyHudModel.Badge> intents = previous.IsFinished ? new BattleDemoEnemyHudModel.Badge[0]
                    : previous.Labels.TryGetValue("intentWait/" + unit.Id, out var waitReason)
                        ? new[] { new BattleDemoEnemyHudModel.Badge(BattleDemoEnemySymbol.Wait, ConceptBattleSkin.Gold, "", waitReason) }
                        : BattleDemoEnemyHudModel.Intents(view.Actor.ReadIntent()?.Action, charge, enemy?.BehaviorState?.Intent);
                // The timed warning and boss artwork glow already present the pending ultimate.
                // Keep actual damage forecasts here without a second, attack-shaped warning.
                while (view.Intents.Count < intents.Count) view.Intents.Add(Icon("Intent " + view.Intents.Count, view.IntentRow, false, view));
                for (int i = 0; i < view.Intents.Count; i++)
                {
                    var badge = view.Intents[i]; SetVisible(badge, i < intents.Count);
                    if (i >= intents.Count) continue;
                    Set(badge, intents[i]); badge.Root.anchoredPosition = new Vector2((i - (intents.Count - 1) * .5f) * 75, 0);
                }
                var statuses = unit.Statuses.ToList();
                var shell = statuses.FirstOrDefault(status => status.Id == "wrath-shell");
                bool wrathBody = previous.Labels.TryGetValue("wrath.body", out var wrathBodyId) && wrathBodyId == unit.Id;
                if (wrathBody && shell == null && view.ShellStacks > 0)
                    view.ShellExhaustedUntil = Time.unscaledTime + 1.25f;
                view.ShellStacks = shell?.Stacks ?? 0;
                if (shell != null || !wrathBody || previous.IsFinished) view.ShellExhaustedUntil = 0;
                if (wrathBody && shell == null && view.ShellExhaustedUntil > Time.unscaledTime)
                    statuses.Add(new StatusDisplayData("wrath-shell", 0));
                while (view.Statuses.Count < statuses.Count) view.Statuses.Add(Icon("Status " + view.Statuses.Count, view.StatusRow, true, view));
                int statusSlot = 0;
                for (int i = 0; i < view.Statuses.Count; i++)
                {
                    var badge = view.Statuses[i]; SetVisible(badge, i < statuses.Count);
                    if (i >= statuses.Count) continue;
                    var status = statuses[i]; var model = BattleDemoEnemyHudModel.Status(status.Id, status.Stacks);
                    var instance = host.PresentedState.Duel.Combat.Statuses.FirstOrDefault(item => item.DefinitionId.Value == status.Id && item.Target.Id == unit.Id);
                    var catalog = host.Engine.Duel.Definition.StatusCatalog;
                    var armor = catalog.Find(new StatusDefinitionId(status.Id))?.Behaviors.OfType<HitArmorSpec>().FirstOrDefault();
                    bool shellBadge = status.Id == "wrath-shell";
                    string description = shellBadge ? "免伤剩余" + status.Stacks + "次\n仅保护暴怒本体，不保护手。每段伤害消耗1次免伤；0伤害不消耗，不按回合消耗，也不免疫状态。"
                        : model.Description + " ×" + status.Stacks;
                    if (!shellBadge && armor != null)
                        description += "\n承受伤害 ×" + armor.Multiplier.ToString("0%") + (armor.Reduction > 0 ? "，再减少 " + armor.Reduction : "") +
                            "；每次命中消耗 " + armor.ConsumeStacks + " 层。";
                    else if (!shellBadge && instance != null) description += "\n" + BattlePresentationRuleText.Status(instance, catalog);
                    Set(badge, model, description);
                    if (shellBadge) badge.Value.text = "免伤剩余" + status.Stacks + "次";
                    if (!conceptLayout)
                    {
                        // Keep the existing icon anchor and row, allowing the count label to occupy adjacent badge space.
                        float extra = shellBadge ? 74 : 0;
                        badge.Root.sizeDelta = new Vector2(30 + extra, 29) * StatusBadgeScale;
                        badge.Icon.rectTransform.anchoredPosition = new Vector2(-extra * .5f, 0) * StatusBadgeScale;
                        badge.Value.rectTransform.sizeDelta = (shellBadge ? new Vector2(76, 25) : new Vector2(23, 15)) * StatusBadgeScale;
                        badge.Value.rectTransform.anchoredPosition = (shellBadge ? new Vector2(14, 0) : new Vector2(9, -8)) * StatusBadgeScale;
                        badge.Value.fontSize = (shellBadge ? 10 : 11) * StatusBadgeScale;
                        badge.Root.anchoredPosition = new Vector2(-77 + (statusSlot % 6 * 31 + extra * .5f) * StatusBadgeScale, -(statusSlot / 6) * 30 * StatusBadgeScale);
                        statusSlot += shellBadge ? 4 : 1;
                    }
                }
            }
        }
        private void LateUpdate()
        {
            if (host == null || viewCamera == null || uiRoot == null) return;
            if (!ReferenceEquals(previous, host.Display) || views.Any(view => view.ShellExhaustedUntil > 0 && view.ShellExhaustedUntil <= Time.unscaledTime))
            {
                foreach (var view in views.Where(view => view.ShellExhaustedUntil > 0 && view.ShellExhaustedUntil <= Time.unscaledTime))
                    view.ShellExhaustedUntil = 0;
                Refresh();
            }
            var composition = host.GetComponent<WrathBossArtworkLayout>();
            foreach (var view in views)
            {
                if (view.Surface == null || !view.Root.gameObject.activeSelf) continue;
                var mesh = view.Surface.GetComponent<MeshFilter>().sharedMesh.bounds;
                bool body = host.UsesExternalCommands && previous != null &&
                    previous.Labels.TryGetValue("wrath.body", out var bodyId) && bodyId == view.Actor.DisplayId;
                var unit = previous?.Unit(view.Actor.DisplayId);
                if (unit != null && unit.IsAlive) PresentHealth(view, unit, body);
                if (body)
                {
                    PositionBossHeader(view);
                    if (conceptLayout) PositionConceptBoss(view);
                    if (conceptLayout && authoredLayout != null) authoredLayout.ApplyHud(view.Actor, view.Root, screenCanvas);
                    continue;
                }
                var part = composition != null && composition.IsComposed ? composition.PartFor(view.Actor) : null;
                bool hand = part != null && part.Name != "Body";
                var center = mesh.center;
                if (hand)
                    center = new Vector3((part.HudPixel.x - part.Pixels.x) / part.Pixels.width - .5f,
                        .5f - (part.HudPixel.y - part.Pixels.y) / part.Pixels.height, 0);
                var projected = viewCamera.WorldToScreenPoint(view.Surface.TransformPoint(center));
                bool onScreen = projected.z > viewCamera.nearClipPlane && viewCamera.pixelRect.Contains(projected);
                view.Visibility.alpha = onScreen ? 1 : 0; view.Visibility.blocksRaycasts = onScreen;
                if (!onScreen) { view.OpenTooltip?.Hide?.Invoke(); continue; }
                PlaceOnScreen(view.Root, projected);
                var upper = viewCamera.WorldToScreenPoint(view.Surface.TransformPoint(mesh.center + Vector3.up * mesh.extents.y));
                var lower = viewCamera.WorldToScreenPoint(view.Surface.TransformPoint(mesh.center - Vector3.up * mesh.extents.y));
                float height = Mathf.Abs(upper.y - lower.y) / Mathf.Max(.001f, screenCanvas.scaleFactor);
                view.Health.sizeDelta = new Vector2(184, 17);
                view.HealthText.rectTransform.sizeDelta = new Vector2(180, 21);
                view.HealthText.fontSize = hand ? 18 : 13;
                float halfHeight = hand ? 42 : height * .5f;
                view.Health.anchoredPosition = new Vector2(0, -halfHeight - 17);
                view.Block.Root.anchoredPosition = new Vector2(-120, -halfHeight - 17);
                view.StatusRow.anchoredPosition = new Vector2(0, -halfHeight - 46);
                view.IntentRow.anchoredPosition = new Vector2(0, halfHeight + 28);
                if (conceptLayout) PositionConcept(view, height, hand);
                if (conceptLayout && authoredLayout != null) authoredLayout.ApplyHud(view.Actor, view.Root, screenCanvas);
                view.Regeneration.rectTransform.position = view.IntentRow.position;
            }
        }

        private void PresentHealth(View view, UnitDisplayData unit, bool bossBody)
        {
            view.HealthText.text = (bossBody ? "暴怒  " : "") + unit.Health + " / " + unit.MaxHealth;
        }

        private void PositionConcept(View view, float height, bool hand)
        {
            // These offsets are reference-screen pixels, independent of the model's size/depth.
            float up = hand ? 42 : height * .5f;
            view.Root.anchoredPosition += Vector2.up * (up + 18);
            view.Health.sizeDelta = new Vector2(220, 30); view.Health.anchoredPosition = new Vector2(0, 35);
            view.Health.GetComponent<ConceptHealthBar>().Present("minion", Fraction(view));
            view.HealthText.rectTransform.sizeDelta = new Vector2(206, 23); view.HealthText.fontSize = 15;
            LayoutConceptBadges(view, 67, 8);
        }
        private float Fraction(View view)
        {
            var unit = previous?.Unit(view.Actor.DisplayId);
            return unit != null && unit.MaxHealth > 0 ? Mathf.Clamp01((float)unit.Health / unit.MaxHealth) : 0;
        }
        private void PositionConceptBoss(View view)
        {
            view.Health.sizeDelta = new Vector2(560, 84); view.Health.anchoredPosition = Vector2.zero;
            view.Health.GetComponent<ConceptHealthBar>().Present("boss", Fraction(view));
            view.HealthText.rectTransform.sizeDelta = new Vector2(530, 28); view.HealthText.fontSize = 20;
            LayoutConceptBadges(view, 65, -48);
        }
        private static void LayoutConceptBadges(View view, float intentY, float statusY)
        {
            view.IntentRow.anchoredPosition = new Vector2(0, intentY);
            var intents = view.Intents.Where(b => b.Root.gameObject.activeSelf).ToArray();
            for (int i = 0; i < intents.Length; i++)
            {
                float width = intents.Length == 1 ? 144 : 104;
                intents[i].Root.sizeDelta = new Vector2(width, 40);
                intents[i].Root.anchoredPosition = new Vector2((i % 2 - (Mathf.Min(2, intents.Length) - 1) * .5f) * (width + 4), i / 2 * 40);
                PositionConceptBadge(intents[i]);
            }
            view.StatusRow.anchoredPosition = new Vector2(0, statusY);
            var statuses = view.Statuses.Where(b => b.Root.gameObject.activeSelf).ToList();
            if (view.Block.Root.gameObject.activeSelf) statuses.Insert(0, view.Block);
            for (int i = 0; i < statuses.Count; i++)
            {
                var badge = statuses[i]; badge.Root.sizeDelta = new Vector2(74, 24) * StatusBadgeScale;
                var position = new Vector2((i % 3 - (Mathf.Min(3, statuses.Count) - 1) * .5f) * 78, -(i / 3) * 26) * StatusBadgeScale;
                badge.Root.anchoredPosition = badge == view.Block ? position + new Vector2(0, statusY) : position;
                PositionConceptBadge(badge);
            }
        }
        private static void PositionConceptBadge(IconView badge)
        {
            // Reapply contained art after the badge receives its final dimensions.
            var iconArea = badge.Status ? new Rect(.05f,.13f,.22f,.74f) : new Rect(.08f,.14f,.22f,.72f);
            ConceptBattleSkin.Place(badge.Art.transform, iconArea);
            ConceptBattleSkin.Place(badge.Icon.transform, iconArea);
            badge.Art.preserveAspect = true;
            ConceptBattleSkin.Place(badge.Value.transform, badge.Status ? new Rect(.29f,.08f,.65f,.84f) : new Rect(.30f,.08f,.31f,.84f));
        }

        private void PositionBossHeader(View view)
        {
            var safe = ScreenSafeArea.CameraRect(viewCamera);
            var screen = new Vector2(safe.x + safe.width * .64f, safe.y + safe.height * (conceptLayout ? .82f : .90f));
            float barWidth = safe.width * .40f / Mathf.Max(.001f, screenCanvas.scaleFactor);
            PlaceOnScreen(view.Root, screen);
            view.Visibility.alpha = 1; view.Visibility.blocksRaycasts = true;
            view.Health.anchoredPosition = Vector2.zero;
            view.Health.sizeDelta = new Vector2(barWidth, 22);
            view.HealthText.rectTransform.sizeDelta = new Vector2(barWidth - 4, 27);
            view.HealthText.fontSize = 18;
            view.Block.Root.anchoredPosition = new Vector2(-barWidth * .5f - 22, 0);
            view.StatusRow.anchoredPosition = new Vector2(-barWidth * .5f + 95, -32);
            view.IntentRow.anchoredPosition = new Vector2(barWidth * .5f - 80, -32);
        }
        private void PlaceOnScreen(RectTransform rect, Vector2 position)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)uiRoot, position, null, out var local);
            rect.anchoredPosition = local;
            rect.localRotation = Quaternion.identity; rect.localScale = Vector3.one;
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
