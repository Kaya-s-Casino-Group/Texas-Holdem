using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using global::BattlePresentation;
using Poker.BattlePresentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleHud
    {
        [Header("暴怒意图图标（留空使用占位图形）")]
        [SerializeField] private Sprite wrathAttackIcon, wrathDefenseIcon, wrathChargeIcon;
        [SerializeField, Min(.1f)] private float wrathOpeningSeconds = 4;
        [SerializeField, Min(.1f)] private float wrathUltimateSeconds = 4;
        [SerializeField, Min(.1f)] private float wrathRegenerationFloatSeconds = 1.4f;
        private readonly WrathPromptTracker wrathPromptTracker = new WrathPromptTracker();
        private readonly Dictionary<string, long> wrathUltimatePromptRounds = new Dictionary<string, long>();
        private readonly List<WrathIntentView> wrathIntentViews = new List<WrathIntentView>();
        private readonly Dictionary<string, WrathHandMarker> wrathHandMarkers = new Dictionary<string, WrathHandMarker>();
        private RectTransform wrathOpening, wrathUltimate;
        private TMP_Text wrathOpeningText, wrathRulesButtonText, wrathUltimatePrompt;
        private float wrathOpeningUntil;
        private float wrathUltimateUntil = float.NegativeInfinity;
        private bool wrathOpeningPinned, wrathPromptsVisible, wrathUltimateActive;
        private string wrathPromptBattle;
        public float WrathUltimateWarningRemaining => isActiveAndEnabled && wrathPromptsVisible && wrathUltimateActive &&
            host != null && host.UsesExternalCommands ? Mathf.Max(0, wrathUltimateUntil - Time.unscaledTime) : 0;


        private sealed class WrathIntentView
        {
            public Image Frame, Artwork;
            public WrathIntentPlaceholderGraphic Placeholder;
            public TMP_Text Label;
        }
        private sealed class WrathHandMarker
        {
            public EnemyBattleActor Actor;
            public RectTransform Root;
            public TMP_Text Number, Float;
            public bool Visible;
            public float FloatStarted = float.NegativeInfinity;
        }

        /// <summary>Optional formal artwork bindings; gameplay continues to use stable action identities.</summary>
        public void ConfigureWrathIcons(Sprite attack, Sprite defense, Sprite charge)
        { wrathAttackIcon = attack; wrathDefenseIcon = defense; wrathChargeIcon = charge; if (host?.Display != null) RefreshWrathIntentIcons(host.Display); }

        private void EnsureWrathPromptViews()
        {
            // The adventure keeps its authored world HUD; only transient prompts are shared.
            if (wrathPanel != null) EnsureWrathDemoPromptViews();
            if (wrathOpening != null) return;
            wrathOpening = new GameObject("Wrath Opening Warning", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            wrathOpening.SetParent(transform, false); wrathOpening.gameObject.layer = gameObject.layer;
            SetWrathRect(wrathOpening, new Vector2(.20f, .4375f), new Vector2(.90f, .5725f), Vector2.zero, Vector2.zero);
            wrathOpening.GetComponent<Image>().color = new Color32(30, 9, 14, 240); wrathOpening.GetComponent<Image>().raycastTarget = false;
            wrathOpeningText = WrathLabel(wrathOpening, "Warning", 36); wrathOpeningText.alignment = TextAlignmentOptions.Center;
            wrathOpeningText.color = new Color32(255, 52, 65, 255);
            SetWrathRect(wrathOpeningText.rectTransform, Vector2.zero, Vector2.one, new Vector2(18, 8), new Vector2(-18, -8));
            wrathOpening.gameObject.SetActive(false);
            wrathUltimate = Instantiate(wrathOpening, transform);
            wrathUltimate.name = "Wrath Ultimate Warning";
            wrathUltimatePrompt = wrathUltimate.GetComponentInChildren<TMP_Text>(true);
            wrathUltimatePrompt.text = WrathPromptText.Ultimate;
            wrathUltimate.gameObject.SetActive(false);
        }

        private void EnsureWrathDemoPromptViews()
        {
            if (wrathIntentViews.Count == 0)
                for (int i = 0; i < 8; i++)
                {
                    var root = new GameObject("Regular Intent " + i, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    root.transform.SetParent(wrathPanel, false); root.gameObject.layer = gameObject.layer; root.raycastTarget = false;
                    SetWrathRect(root.rectTransform, new Vector2(i / 8f, .66f), new Vector2((i + 1) / 8f, .79f), new Vector2(3, 0), new Vector2(-3, 0));
                    var artwork = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    artwork.transform.SetParent(root.transform, false); artwork.gameObject.layer = gameObject.layer;
                    artwork.preserveAspect = true; artwork.raycastTarget = false;
                    SetWrathRect(artwork.rectTransform, new Vector2(.06f, .13f), new Vector2(.38f, .87f), Vector2.zero, Vector2.zero);
                    var placeholder = new GameObject("Placeholder", typeof(RectTransform), typeof(WrathIntentPlaceholderGraphic)).GetComponent<WrathIntentPlaceholderGraphic>();
                    placeholder.transform.SetParent(root.transform, false); placeholder.gameObject.layer = gameObject.layer; placeholder.raycastTarget = false;
                    SetWrathRect(placeholder.rectTransform, new Vector2(.06f, .13f), new Vector2(.38f, .87f), Vector2.zero, Vector2.zero);
                    var label = WrathLabel(root.transform, "Action", 12); label.alignment = TextAlignmentOptions.Center;
                    SetWrathRect(label.rectTransform, new Vector2(.4f, 0), Vector2.one, Vector2.zero, Vector2.zero);
                    wrathIntentViews.Add(new WrathIntentView { Frame = root, Artwork = artwork, Placeholder = placeholder, Label = label });
                }
            if (wrathRulesButtonText != null) return;
            var rules = new GameObject("Wrath Rules", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            rules.transform.SetParent(wrathPanel, false); rules.gameObject.layer = gameObject.layer;
            rules.targetGraphic = rules.GetComponent<Image>(); rules.targetGraphic.color = new Color32(80, 40, 45, 255);
            SetWrathRect(rules.GetComponent<RectTransform>(), new Vector2(.86f, .82f), new Vector2(.99f, .97f), Vector2.zero, Vector2.zero);
            wrathRulesButtonText = WrathLabel(rules.transform, "Label", 12); wrathRulesButtonText.text = "规则说明";
            wrathRulesButtonText.alignment = TextAlignmentOptions.Center;
            SetWrathRect(wrathRulesButtonText.rectTransform, Vector2.zero, Vector2.one, new Vector2(3, 0), new Vector2(-3, 0));
            rules.onClick.AddListener(() => {
                wrathOpeningPinned = !wrathOpeningPinned;
                wrathOpeningUntil = wrathOpeningPinned ? float.PositiveInfinity : float.NegativeInfinity;
                wrathOpeningText.text = WrathPromptText.Opening;
                UpdateWrathPromptViews();
            });
        }

        private void RefreshWrathIntentIcons(BattleDisplaySnapshot snapshot)
        {
            for (int i = 0; i < wrathIntentViews.Count; i++)
            {
                var view = wrathIntentViews[i]; snapshot.Labels.TryGetValue("wrath.intentId/" + i, out var id);
                var icon = WrathPromptText.IconFor(id);
                var sprite = icon == WrathIntentIcon.Attack ? wrathAttackIcon : icon == WrathIntentIcon.Defense ? wrathDefenseIcon : icon == WrathIntentIcon.Charge ? wrathChargeIcon : null;
                view.Artwork.sprite = sprite; view.Artwork.enabled = sprite != null;
                view.Placeholder.enabled = sprite == null; view.Placeholder.Show(icon);
                view.Placeholder.color = icon == WrathIntentIcon.Attack ? new Color32(255, 129, 111, 255) :
                    icon == WrathIntentIcon.Defense ? new Color32(128, 202, 255, 255) : new Color32(255, 215, 111, 255);
                snapshot.Labels.TryGetValue("wrath.intent/" + i, out var label);
                view.Label.text = label?.Split('·')[0] ?? "—";
                view.Frame.color = i == 0 ? new Color32(115, 76, 39, 255) : new Color32(49, 43, 54, 255);
            }
        }

        private void RefreshWrathPrompts(BattleDisplaySnapshot snapshot)
        {
            var body = WrathBody(snapshot);
            wrathPromptsVisible = snapshot != null && snapshot.HasStarted && !snapshot.IsFinished && body?.IsAlive == true;
            long releaseRound = body != null ? snapshot.Resource(body.Id, "wrath.releaseRound")?.Value ?? -1 : -1;
            wrathUltimateActive = wrathPromptsVisible && snapshot.Resource(body.Id, "wrath.warning")?.Value == 1 &&
                (!host.UsesExternalCommands || snapshot.CompletedRounds + 1 < releaseRound);
            if (!wrathPromptsVisible) { HideWrathPrompts(); return; }
            EnsureWrathPromptViews();
            if (UsesWrathDemoPresentation) RefreshWrathIntentIcons(snapshot);
            if (wrathPromptBattle != snapshot.BattleId)
            {
                wrathPromptBattle = snapshot.BattleId; wrathOpeningPinned = false; wrathOpeningUntil = float.NegativeInfinity;
                wrathUltimateUntil = float.NegativeInfinity;
                foreach (var marker in wrathHandMarkers.Values) marker.FloatStarted = float.NegativeInfinity;
            }
            foreach (var marker in wrathHandMarkers.Values) marker.Visible = false;
            // Adventure keeps the existing purple world-HUD countdown as its only regeneration prompt.
            foreach (var hand in snapshot.Units.Where(unit => !host.UsesExternalCommands &&
                snapshot.Labels.ContainsKey("wrath.hand/" + unit.Id)))
            {
                if (!wrathHandMarkers.TryGetValue(hand.Id, out var marker))
                { if (hand.IsAlive) continue; marker = CreateWrathHandMarker(hand.Id); wrathHandMarkers.Add(hand.Id, marker); }
                // Scene roots can be destroyed before the adapter publishes its final disable synchronization.
                if (marker.Root == null) continue;
                marker.Actor = host.Enemies.FirstOrDefault(actor => actor != null && actor.DisplayId == hand.Id);
                marker.Visible = !hand.IsAlive && !snapshot.IsFinished;
                marker.Number.text = (snapshot.Resource(hand.Id, "wrath.reviveRemaining")?.Value ?? 0).ToString(CultureInfo.InvariantCulture);
                if (!marker.Visible) marker.FloatStarted = float.NegativeInfinity;
            }
            var prompts = wrathPromptTracker.Observe(snapshot, committed: Application.isPlaying);
            if (prompts.OpeningText != null)
            { wrathOpeningText.text = prompts.OpeningText; wrathOpeningUntil = Time.unscaledTime + wrathOpeningSeconds; }
            if (!wrathUltimateActive) wrathUltimateUntil = float.NegativeInfinity;
            else if (host.UsesExternalCommands)
            {
                // Text and artwork share this one-shot clock; authority still owns the pending ultimate.
                if (releaseRound > 0 && (!wrathUltimatePromptRounds.TryGetValue(snapshot.BattleId, out long shownRound) || releaseRound > shownRound))
                {
                    wrathUltimatePromptRounds[snapshot.BattleId] = releaseRound;
                    wrathUltimateUntil = Time.unscaledTime + wrathUltimateSeconds;
                }
            }
            foreach (var prompt in prompts.Regenerations)
                if (wrathHandMarkers.TryGetValue(prompt.ActorId, out var marker))
                { marker.Float.text = prompt.Text; marker.FloatStarted = Time.unscaledTime; }
            UpdateWrathPromptViews();
        }

        private WrathHandMarker CreateWrathHandMarker(string id)
        {
            bool adventure = host.UsesExternalCommands;
            var root = new GameObject("Wrath Regeneration " + id, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(adventure ? transform : EnsureWorldUIRoot(), false); root.gameObject.layer = gameObject.layer;
            root.sizeDelta = adventure ? new Vector2(220, 90) : new Vector2(160, 90);
            if (!adventure)
            {
                var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                root.gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10;
            }
            var number = WrathLabel(root, "Countdown", 34); number.color = new Color32(255, 34, 48, 255); number.alignment = TextAlignmentOptions.Center;
            number.outlineWidth = .18f; number.outlineColor = new Color32(45, 0, 4, 255);
            SetWrathRect(number.rectTransform, new Vector2(0, .25f), new Vector2(1, .8f), Vector2.zero, Vector2.zero);
            number.gameObject.SetActive(!adventure); // The adventure's existing world HUD owns its countdown.
            var text = WrathLabel(root, "Regeneration Float", adventure ? 20 : 13); text.alignment = TextAlignmentOptions.Center;
            text.color = new Color32(255, 55, 65, 255);
            text.outlineWidth = .18f; text.outlineColor = new Color32(45, 0, 4, 255);
            SetWrathRect(text.rectTransform, new Vector2(0, .7f), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            text.gameObject.SetActive(false);
            return new WrathHandMarker { Root = root, Number = number, Float = text };
        }

        private void UpdateWrathPromptViews()
        {
            bool adventure = host != null && host.UsesExternalCommands;
            bool visible = isActiveAndEnabled && (adventure ? wrathPromptsVisible : wrathPanel != null && wrathPanel.gameObject.activeInHierarchy);
            bool ultimateVisible = visible && adventure && wrathUltimateActive && Time.unscaledTime < wrathUltimateUntil;
            bool openingVisible = visible && !ultimateVisible && Time.unscaledTime < wrathOpeningUntil;
            if (wrathOpening != null) wrathOpening.gameObject.SetActive(openingVisible);
            if (wrathUltimate != null) wrathUltimate.gameObject.SetActive(ultimateVisible);
            if (adventure) host.GetComponent<WrathBossArtworkLayout>()?.SetWarningPromptVisible(ultimateVisible);
            if (wrathRulesButtonText != null) wrathRulesButtonText.text = wrathOpeningPinned ? "收起说明" : "规则说明";
            var camera = ResolveIntentCamera();
            foreach (var marker in wrathHandMarkers.Values)
            {
                if (marker.Root == null) continue;
                bool show = visible && marker.Visible && marker.Actor != null && camera != null;
                marker.Root.gameObject.SetActive(show); if (!show) continue;
                if (adventure) PositionAdventureWrathPrompt(marker, camera);
                else
                {
                    // The logical actor remains stable even when its animated artwork collapses or becomes hidden.
                    marker.Root.position = marker.Actor.transform.position;
                    marker.Root.rotation = camera.transform.rotation; marker.Root.localScale = Vector3.one * intentWorldUnitsPerPixel;
                    marker.Root.GetComponent<Canvas>().worldCamera = camera;
                }
                float progress = (Time.unscaledTime - marker.FloatStarted) / wrathRegenerationFloatSeconds;
                marker.Float.gameObject.SetActive(progress >= 0 && progress < 1);
                marker.Float.rectTransform.anchoredPosition = new Vector2(0, Mathf.Clamp01(progress) * 24);
                marker.Float.alpha = 1 - Mathf.Clamp01(progress);
            }
        }

        private void PositionAdventureWrathPrompt(WrathHandMarker marker, Camera camera)
        {
            Vector3 anchor = marker.Actor.transform.position;
            var composition = host.GetComponent<WrathBossArtworkLayout>();
            var part = composition != null && composition.IsComposed ? composition.PartFor(marker.Actor) : null;
            var surface = marker.Actor.Visual != null ? marker.Actor.Visual.Find("Artwork") : null;
            if (part != null && surface != null)
                anchor = surface.TransformPoint(new Vector3((part.HudPixel.x - part.Pixels.x) / part.Pixels.width - .5f,
                    .5f - (part.HudPixel.y - part.Pixels.y) / part.Pixels.height, 0));
            var canvas = GetComponentInParent<Canvas>();
            var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
                camera.WorldToScreenPoint(anchor), uiCamera, out var position))
                marker.Root.localPosition = new Vector3(position.x, position.y, 0);
        }

        private void HideWrathPrompts()
        {
            wrathPromptsVisible = wrathUltimateActive = false;
            wrathOpeningPinned = false; wrathOpeningUntil = float.NegativeInfinity;
            wrathUltimateUntil = float.NegativeInfinity;
            foreach (var marker in wrathHandMarkers.Values)
            { marker.Visible = false; marker.FloatStarted = float.NegativeInfinity; }
            // Keep the tracker's per-battle/round memory when returning to the same adventure battle.
            UpdateWrathPromptViews();
        }

        private static void SetWrathRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 insetMin, Vector2 insetMax)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = insetMin; rect.offsetMax = insetMax; }
    }
}
