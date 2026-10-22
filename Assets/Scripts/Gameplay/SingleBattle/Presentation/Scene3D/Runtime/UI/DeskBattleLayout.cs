using System.Collections.Generic;
using System.Linq;
using CardInteraction;
using CardPresentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>Approved concept layout. Existing presenters retain all commands, ownership and previews.</summary>
    [DefaultExecutionOrder(550), DisallowMultipleComponent]
    public sealed class DeskBattleLayout : MonoBehaviour
    {
        [SerializeField] private SingleBattleSceneAdapter view;
        private SingleBattleHud hud;
        private BattleDemoOutcomePreview preview;
        private DrawPilePresenter pile;
        private TMP_FontAsset font;
        private object discardCandidate, discardState;
        private PlayerBattleActor discardPlayer;
        private bool discardReady;
        private long discardShieldGain;
        private const float FooterY = .913f, FooterHeight = .051f;
        private const float ActionCenterX = .875f;
        private readonly Dictionary<string, Transform> targets = new Dictionary<string, Transform>();
        private readonly List<Image> relicSlots = new List<Image>();
        public void Configure(SingleBattleSceneAdapter owner) { view = owner; Apply(); }
        private void LateUpdate() => Apply();
        private Transform Find(string path)
        {
            if (targets.TryGetValue(path, out var value) && value != null) return value;
            value = transform.Find(path) ?? transform.Find("Safe Area/" + path);
            if (value != null) targets[path] = value; return value;
        }
        private Transform Root => Find("Turn") != null ? Find("Turn").parent : transform;
        private void Place(string path, Rect rect) { var t = Find(path); if (t != null) ConceptBattleSkin.Place(t, rect); }
        private void Label(string path, Rect rect, int size)
        {
            var t = Find(path); if (t == null) return;
            ConceptBattleSkin.Place(t, rect); var label = t.GetComponent<TMP_Text>();
            if (label != null) ConceptBattleSkin.Format(label, size);
        }
        private static void Clear(Transform t)
        {
            if (t == null) return; var image = t.GetComponent<Image>(); if (image != null) image.enabled = false;
            ConceptBattleSkin.HideLegacy(t);
        }
        public void Apply()
        {
            if (view == null) return;
            if (hud == null) hud = GetComponent<SingleBattleHud>();
            if (hud == null) return;
            if (font == null) font = hud.GetComponentsInChildren<TMP_Text>(true).First(t => t.font != null).font;
            foreach (var canvas in new[] { hud.GetComponent<Canvas>(), view.Actions?.Panel?.GetComponentInParent<Canvas>() })
            {
                var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
                if (scaler != null) { scaler.referenceResolution = new Vector2(1856, 850); scaler.matchWidthOrHeight = 1; }
            }
            Place("Turn", new Rect(.43f, .025f, .135f, .075f));
            ConceptBattleSkin.Surface(Find("Turn"), "Panels/panel_round");
            Label("Turn/Round", new Rect(.12f, .18f, .76f, .64f), 25);
            var energy = hud.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "Energy");
            if (energy != null)
            {
                var orb = ConceptBattleSkin.Art(Root, "Concept Action Points", "Frames/frame_action_points", new Rect(ActionCenterX - .0265f, .76f, .053f, .105f));
                BattleResourceWidgets.PresentActions(orb.transform, view.Display?.Resource(view.Display.ObserverId, "actions")?.Value ?? 3);
                if (energy.transform.parent != orb.transform) energy.transform.SetParent(orb.transform, false);
                ConceptBattleSkin.Place(energy.transform, new Rect(.16f, .16f, .68f, .66f)); ConceptBattleSkin.Format(energy, 38);
                ConceptBattleSkin.Text(Root, "Action Caption", font, new Rect(ActionCenterX - .0305f, .86f, .061f, .028f), "行动点", 18);
            }
            Place("Player", new Rect(.072f, .029f, .21f, .105f));
            Clear(Find("Player"));
            ConceptBattleSkin.Art(Root, "Idle Portrait", "Portraits/portrait_hero", new Rect(.012f, .018f, .066f, .142f));
            ConceptBattleSkin.Art(Root, "Idle Portrait Frame", "Frames/frame_player_portrait", new Rect(.007f, .009f, .076f, .16f));
            var heroName = Find("Player/Hero Name");
            if (heroName != null) heroName.gameObject.SetActive(false);
            Label("Player/Health", new Rect(.09f, .61f, .67f, .33f), 16);
            Place("Player/Health Bar", new Rect(.07f, .63f, .70f, .31f));
            var bar = Find("Player/Health Bar");
            var hpBar = bar.GetComponent<ConceptHealthBar>() ?? bar.gameObject.AddComponent<ConceptHealthBar>();
            var hero = view.Display?.Unit(view.Display.ObserverId);
            hpBar.Present("player", hero != null && hero.MaxHealth > 0 ? (float)hero.Health / hero.MaxHealth : 1);
            ConceptBattleSkin.Art(Find("Player"), "Block Icon", "Icons/icon_shield", new Rect(.79f, .49f, .075f, .43f));
            BattleBadgeText.Apply(ConceptBattleSkin.Text(Find("Player"), "Block Count", font, new Rect(.875f, .50f, .095f, .34f), (hero?.Block ?? 0).ToString(), 20));
            LayoutRelics(); LayoutTools(); LayoutActions(); LayoutPreview(); LayoutPiles(); LayoutFlow();
            ConceptBattleSkin.Art(Root, "Concept Hand Tray", "Frames/frame_hand_tray", new Rect(.295f, .85f, .462f, .075f));
            var info = Find("Battle Info"); if (info != null) info.gameObject.SetActive(false);
            if (view.Display == null) return;
            var snapshot = view.Display;
            var round = Find("Turn/Round")?.GetComponent<TMP_Text>();
            if (round != null) round.text = "回合 " + (snapshot.IsFinished ? snapshot.CompletedRounds : snapshot.CompletedRounds + 1) + " / " + (snapshot.Resource("", "roundLimit")?.Value ?? 20);
            if (energy != null) energy.text = (snapshot.Resource(snapshot.ObserverId, "actions")?.Value ?? 0).ToString();
            var hp = Find("Player/Health")?.GetComponent<TMP_Text>();
            if (hp != null && hero != null) { hp.text = hero.Health + " / " + hero.MaxHealth; hp.transform.SetAsLastSibling(); }
        }
        private void LayoutActions()
        {
            var panel = view.Actions?.Panel; if (panel == null) return;
            ConceptBattleSkin.Place(panel.PlayButton.transform.parent, new Rect(0, 0, 1, 1)); Clear(panel.PlayButton.transform.parent);
            ConceptBattleSkin.Button(panel.PlayButton, new Rect(.769f, .77f, .079f, .085f), "Panels/panel_button_play_compact", 27);
            ConceptBattleSkin.Button(panel.DiscardButton, new Rect(.902f, .77f, .079f, .085f), "Panels/panel_button_discard_compact", 27);
            bool canDiscard = view.Actions.CanDiscard;
            if (!ReferenceEquals(discardCandidate, view.Candidate) || !ReferenceEquals(discardState, view.AuthorityState) ||
                discardPlayer != view.Player || discardReady != canDiscard)
            {
                discardCandidate = view.Candidate; discardState = view.AuthorityState;
                discardPlayer = view.Player; discardReady = canDiscard;
                discardShieldGain = canDiscard && view.Player != null
                    ? BattleResourceWidgets.DiscardShieldGain(view.PreviewSelectedDiscard(), view.Player.ParticipantId) : 0;
            }
            BattleResourceWidgets.PresentShield(panel.DiscardButton.transform, font, discardShieldGain);
            var sort = ConceptBattleSkin.Art(panel.PlayButton.transform.parent, "Concept Sort Frame", "Panels/panel_sort", new Rect(.627f, FooterY, .112f, FooterHeight));
            sort.transform.SetAsFirstSibling();
            foreach (var button in new[] { panel.RankSortButton, panel.SuitSortButton })
            {
                if(button.transform.parent != sort.transform) button.transform.SetParent(sort.transform, false);
                ConceptBattleSkin.Place(button.transform, button == panel.RankSortButton ? new Rect(.10f, .08f, .39f, .84f) : new Rect(.51f, .08f, .39f, .84f));
                ConceptBattleSkin.HideLegacy(button.transform);
                var image = button.GetComponent<Image>(); image.sprite = null; image.type = Image.Type.Simple; image.color = Color.clear; image.raycastTarget = true;
                var label = button.GetComponentInChildren<TMP_Text>(); ConceptBattleSkin.Place(label.transform, new Rect(0, .1f, 1, .8f)); ConceptBattleSkin.Format(label, 16);
            }
            bool suit = view.Actions.Hand.SortOrder == CardZoneSortOrder.SuitFirst;
            var rank = panel.RankSortButton.GetComponentInChildren<TMP_Text>(); var suits = panel.SuitSortButton.GetComponentInChildren<TMP_Text>();
            rank.text = "点数"; suits.text = "花色";
            ConceptBattleSkin.Art(sort.transform, "Rank Arrow", "Icons/icon_sort_triangle", new Rect(.06f, .31f, .06f, .38f));
            var arrow = ConceptBattleSkin.Art(sort.transform, "Suit Arrow", "Icons/icon_sort_triangle", new Rect(.88f, .31f, .06f, .38f));
            arrow.transform.localRotation = Quaternion.Euler(0,0,180);
            ConceptBattleSkin.Text(sort.transform, "Divider", font, new Rect(.47f,.12f,.06f,.76f), "|", 17);
            rank.color = suit ? ConceptBattleSkin.Paper : ConceptBattleSkin.Gold; suits.color = suit ? ConceptBattleSkin.Gold : ConceptBattleSkin.Paper;
            ConceptBattleSkin.Button(hud.EndTurnButton, new Rect(ActionCenterX - .0855f, FooterY + (FooterHeight - .073f) * .5f, .171f, .073f), "Panels/panel_button_secondary", 25);
        }
        private void LayoutPreview()
        {
            if (preview == null) preview = GetComponentInChildren<BattleDemoOutcomePreview>(true);
            if (preview == null) return;
            ConceptBattleSkin.Place(preview.transform, new Rect(.44f, FooterY, .172f, FooterHeight));
            ConceptBattleSkin.Surface(preview.transform, "Panels/panel_effect_preview", true, true);
            foreach (var name in new[] { "Outcome Hand", "Outcome Details" }) { var t = preview.transform.Find(name); if (t != null) t.gameObject.SetActive(false); }
            var value = preview.transform.Find("Outcome Value");
            if (value != null) { value.gameObject.SetActive(true); ConceptBattleSkin.Place(value, new Rect(.07f, .16f, .86f, .68f)); ConceptBattleSkin.Format(value.GetComponent<TMP_Text>(), 19); }
        }
        private void LayoutPiles()
        {
            if (pile == null) pile = gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DrawPilePresenter>(true)).FirstOrDefault();
            if (pile != null)
            {
                var icon = pile.View.transform.Find("Icon") ?? pile.View.transform.Find("Safe Area/Icon");
                if (icon != null)
                {
                    ConceptBattleSkin.Place(icon, new Rect(.326f, .902f, .034f, .074f)); Clear(icon);
                    foreach (var old in icon.GetComponentsInChildren<CardUIGraphic>(true)) old.enabled = false;
                    ConceptBattleSkin.Art(icon, "Concept Draw Art", "Icons/icon_draw_pile", new Rect(0, 0, 1, 1)).raycastTarget = true;
                    var count = pile.View.RemainingCount;
                    if (count != null)
                    {
                        var badge = ConceptBattleSkin.Art(icon, "Draw Count Frame", "Frames/frame_relic_slot", new Rect(.023f/.034f,.041f/.074f,.021f/.034f,.039f/.074f));
                        if(count.transform.parent != badge.transform) count.transform.SetParent(badge.transform, false);
                        ConceptBattleSkin.Place(count.transform, new Rect(.10f,.04f,.80f,.92f)); ConceptBattleSkin.Format(count,19);
                        badge.transform.SetAsLastSibling();
                    }
                }
            }
            ConceptBattleSkin.Art(Root, "Discard Pile Art", "Icons/icon_discard_trash", new Rect(.386f, .902f, .034f, .074f));
            var snapshot = view.Display; int discarded = snapshot?.Zones.FirstOrDefault(z => z.OwnerId == snapshot.ObserverId && z.Kind == "discard")?.Cards.Count ?? 0;
            var discardBadge = ConceptBattleSkin.Art(Root, "Discard Count Frame", "Frames/frame_relic_slot", new Rect(.409f,.943f,.021f,.039f));
            var previousCount = Root.Find("Discard Pile Count");
            if(previousCount != null) previousCount.SetParent(discardBadge.transform, false);
            ConceptBattleSkin.Text(discardBadge.transform, "Discard Pile Count", font, new Rect(.10f,.04f,.80f,.92f), discarded.ToString(), 19);
        }
        private void LayoutRelics()
        {
            var rail = Find("Status Rail"); if (rail == null) return;
            ConceptBattleSkin.Place(rail, new Rect(.897f, .102f, .085f, .535f)); Clear(rail);
            var backing = ConceptBattleSkin.Art(rail, "Concept Relic Backing", "Panels/panel_relic_rail", new Rect(-.055f,-.025f,1.11f,1.09f));
            backing.transform.SetAsFirstSibling();
            var feedback = Find("Status Rail/Rules Feedback"); if (feedback == null) return;
            ConceptBattleSkin.Place(feedback, new Rect(0, 0, 1, 1)); Clear(feedback);
            var viewport = feedback.Find("Viewport"); if (viewport != null) viewport.gameObject.SetActive(false);
            int count = 0; var icons = feedback.Find("Relic Icons") as RectTransform;
            if (icons != null)
            {
                var entries = icons.GetComponentsInChildren<Image>(true).Where(i => i.transform.parent == icons && i.gameObject.activeSelf).ToArray(); count = entries.Length;
                ConceptBattleSkin.Place(icons, new Rect(0, .10f, 1, .90f));
                int rows = Mathf.Max(6, Mathf.CeilToInt(count / 2f));
                float cell = Mathf.Min(icons.rect.width * .45f, icons.rect.height / rows * .91f), step = cell * 1.08f;
                float insetX = (icons.rect.width - cell - step) * .5f;
                float insetY = (icons.rect.height - cell - (rows - 1) * step) * .5f;
                int slots = Mathf.Max(12, count);
                while (relicSlots.Count < slots) relicSlots.Add(ConceptBattleSkin.Art(feedback, "Concept Relic Slot " + relicSlots.Count, "Frames/frame_relic_slot", new Rect(0,0,1,1), false));
                for (int i = 0; i < relicSlots.Count; i++)
                {
                    var slot = relicSlots[i]; slot.gameObject.SetActive(i < slots); var rect = slot.rectTransform;
                    slot.color = Color.white; slot.raycastTarget = false;
                    var target = slot.GetComponent<BattleDemoRelicHoverTarget>();
                    if (target != null) target.enabled = false;
                    if (i < entries.Length) entries[i].GetComponent<BattleDemoRelicHoverTarget>()?.BindBase(slot);
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                    rect.anchoredPosition = new Vector2(i % 2 * step + insetX, -((RectTransform)rail).rect.height * .10f - insetY - i / 2 * step);
                    rect.sizeDelta = Vector2.one * cell; rect.SetAsFirstSibling();
                }
                icons.SetAsLastSibling();
                for (int i = 0; i < entries.Length; i++)
                {
                    var rect = entries[i].rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                    rect.anchoredPosition = new Vector2(i % 2 * step + insetX + cell * .06f, -insetY - i / 2 * step - cell * .06f);
                    rect.sizeDelta = Vector2.one * cell * .88f; entries[i].preserveAspect = true;
                }
            }
            ConceptBattleSkin.Art(feedback, "Relic Heading Frame", "Panels/panel_counter", new Rect(0, 0, 1, .087f));
            var heading = feedback.Find("Heading");
            if (heading != null) { ConceptBattleSkin.Place(heading, new Rect(.08f, .005f, .84f, .076f)); var label = heading.GetComponent<TMP_Text>(); ConceptBattleSkin.Format(label, 19); label.text = "遗物 " + count; heading.SetAsLastSibling(); }
        }
        private void LayoutTools()
        {
            var tools = Find("Battle Tools"); if (tools == null) return;
            ConceptBattleSkin.Place(tools, new Rect(.868f, .032f, .072f, .061f)); var toggle = hud.BattleToolsButton;
            if (toggle != null)
            {
                ConceptBattleSkin.Button(toggle, new Rect(0, 0, 1, 1), "Panels/panel_counter", 22);
                ConceptBattleSkin.Art(toggle.transform, "Coin Icon", "Icons/icon_coin", new Rect(.10f, .18f, .23f, .64f));
                var label = toggle.GetComponentInChildren<TMP_Text>(); ConceptBattleSkin.Place(label.transform, new Rect(.37f, .16f, .53f, .68f));
                label.text = (view.Display?.Resource(view.Display.ObserverId, "gold")?.Value ?? 0).ToString();
            }
            var details = tools.Find("Details") as RectTransform; if (details == null) return;
            details.anchorMin = details.anchorMax = new Vector2(1, 0); details.pivot = new Vector2(1, 1); details.anchoredPosition = new Vector2(0, -8); details.sizeDelta = new Vector2(480, 490);
            var image = details.GetComponent<Image>(); image.sprite = null; image.type = Image.Type.Simple; image.color = new Color32(24, 22, 27, 250); ConceptBattleSkin.HideLegacy(details);
            var viewport = details.Find("Viewport") as RectTransform; if (viewport != null) viewport.offsetMax = new Vector2(-12, -42);
            if (hud.AdministratorButton != null)
            {
                hud.AdministratorButton.transform.SetParent(details, false);
                ConceptBattleSkin.Button(hud.AdministratorButton, new Rect(.76f, .012f, .22f, .065f), "Panels/panel_counter", 15);
                var label = hud.AdministratorButton.GetComponentInChildren<TMP_Text>(true); label.gameObject.SetActive(true); label.text = "修改器";
            }
        }
        private void LayoutFlow()
        {
            Place("Draw Controls", new Rect(.76f, .64f, .22f, .22f)); var flow = Find("Draw Controls/Turn Flow"); if (flow == null) return; Clear(flow);
            var hint = flow.Find("Flow Hint"); if (hint != null) { ConceptBattleSkin.Place(hint, new Rect(.04f, .02f, .92f, .28f)); ConceptBattleSkin.Format(hint.GetComponent<TMP_Text>(), 17); }
            foreach (var button in flow.GetComponentsInChildren<Button>(true))
            {
                Rect area;
                switch (button.name)
                {
                    case "−": area = new Rect(.02f, .34f, .17f, .25f); break;
                    case "+": area = new Rect(.81f, .34f, .17f, .25f); break;
                    case "摸牌": area = new Rect(.23f, .34f, .54f, .25f); break;
                    case "跳过": area = new Rect(.53f, .68f, .45f, .25f); break;
                    case "先弃所选牌": area = new Rect(.02f, .68f, .47f, .25f); break;
                    default: area = new Rect(.10f, .4f, .80f, .25f); break;
                }
                ConceptBattleSkin.Button(button, area, "Panels/panel_button_secondary", 19);
            }
        }
    }
}
