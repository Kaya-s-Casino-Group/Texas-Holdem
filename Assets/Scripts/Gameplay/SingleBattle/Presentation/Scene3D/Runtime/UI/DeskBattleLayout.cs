using System.Collections.Generic;
using System.Linq;
using CardInteraction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>desk_UI battle composition. Repositions the existing controls and reads the displayed snapshot.</summary>
    [DefaultExecutionOrder(550), DisallowMultipleComponent]
    public sealed class DeskBattleLayout : MonoBehaviour
    {
        [SerializeField] private SingleBattleSceneAdapter view;
        private SingleBattleHud hud;
        private BattleDemoOutcomePreview preview;
        private DrawPilePresenter pile;
        private readonly HashSet<int> styled = new HashSet<int>();
        
        public void Configure(SingleBattleSceneAdapter owner) { view = owner; Apply(); }
        private void LateUpdate() => Apply();
        public void Apply()
        {
            if (view == null) return;
            if (hud == null) hud = GetComponent<SingleBattleHud>();
            if (hud == null) return;
            Region("Turn", new Rect(.035f, .785f, .145f, .19f));
            ClearSurface(FindHud("Turn"));
            var roundFrame = Frame(FindHud("Turn"), "Round Frame", new Rect(0, .64f, 1, .36f), "RoundFrame");
            var actionFrame = Frame(FindHud("Turn"), "Action Frame", new Rect(0, 0, 1, .58f), "StatFrame");
            TextInFrame(FindHud("Turn/Round"), roundFrame, new Rect(.14f, .20f, .72f, .60f), 24);
            TextInFrame(FindHud("Turn/Energy"), actionFrame, new Rect(.14f, .15f, .72f, .70f), 22);
            Region("Player", new Rect(.035f, .70f, .145f, .07f));
            Style(FindHud("Player"), "HealthFrame");
            Label("Player/Health", new Rect(.10f, .52f, .80f, .32f), 21);
            Region("Player/Health Bar", new Rect(.10f, .22f, .80f, .17f));

            int relicCount = FindHud("Status Rail/Rules Feedback/Relic Icons")?.GetComponentsInChildren<Image>().Length ?? 0;
            float railHeight = Mathf.Clamp(.09f + Mathf.Ceil(relicCount / 2f) * .085f, .16f, .43f);
            Region("Status Rail", new Rect(.855f, .77f - railHeight, .115f, railHeight));
            ClearSurface(FindHud("Status Rail"));
            var feedback = FindHud("Status Rail/Rules Feedback");
            if (feedback != null)
            {
                DeskUISkin.Place(feedback, new Rect(0, 0, 1, 1));
                Style(feedback, "RelicTray");
                var heading = feedback.Find("Heading") as RectTransform;
                if (heading != null) { DeskUISkin.Place(heading, new Rect(.12f, 1 - .045f / railHeight, .76f, .03f / railHeight)); Format(heading, 20); }
                var viewport = feedback.Find("Viewport") as RectTransform;
                if (viewport != null && relicCount == 0)
                {
                    DeskUISkin.Place(viewport, new Rect(.10f, .14f, .80f, .48f));
                    var emptyText = viewport.GetComponentInChildren<TMP_Text>();
                    if (emptyText != null) { emptyText.fontSize = 18; emptyText.alignment = TextAlignmentOptions.Center; }
                }
                var icons = feedback.Find("Relic Icons") as RectTransform;
                if (icons != null)
                {
                    icons.anchorMin = icons.anchorMax = new Vector2(0, 1); icons.pivot = new Vector2(0, 1);
                    var frameSize = ((RectTransform)feedback).rect.size;
                    float iconSize = Mathf.Min(70, frameSize.x * .30f);
                    float step = frameSize.x * .36f;
                    icons.anchoredPosition = new Vector2(frameSize.x * .17f, -frameSize.y * (.05f / railHeight));
                    var entries = icons.GetComponentsInChildren<Image>();
                    for (int i = 0; i < entries.Length; i++)
                    {
                        var rect = entries[i].rectTransform; rect.sizeDelta = Vector2.one * iconSize;
                        rect.anchoredPosition = new Vector2(i % 2 * step, -(i / 2) * step);
                    }
                }
            }

            Region("Battle Info", new Rect(.197f, .916f, .18f, .062f));
            Style(FindHud("Battle Info"), "MenuButton");
            Label("Battle Info/Counts", new Rect(.10f, .49f, .80f, .33f), 17);
            Label("Battle Info/Score", new Rect(.10f, .17f, .80f, .30f), 17);
            Region("Battle Tools", new Rect(.197f, .848f, .18f, .058f));
            var tools = FindHud("Battle Tools");
            if (tools != null)
            {
                var toggle = tools.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.transform.parent == tools);
                if (toggle != null) { PlaceButton(toggle, new Rect(0, 0, 1, 1), "MenuButton", 19); }
                var detail = tools.Find("Details") as RectTransform;
                if (detail != null)
                {
                    detail.anchorMin = Vector2.zero; detail.anchorMax = Vector2.right; detail.pivot = new Vector2(0, 1);
                    detail.anchoredPosition = new Vector2(0, -8); detail.sizeDelta = new Vector2(0, 490);
                }
            }
            if (hud.AdministratorButton != null)
                PlaceButton(hud.AdministratorButton, new Rect(.948f, .81f, .045f, .07f), "Menu", 15);
            Region("Draw Controls", new Rect(.755f, .095f, .225f, .17f));
            if (hud.EndTurnButton != null) PlaceButton(hud.EndTurnButton, new Rect(.755f, .025f, .225f, .055f), "Button", 25);
            if (view.Actions?.Panel != null)
            {
                var actions = view.Actions.Panel;
                DeskUISkin.Place(actions.PlayButton.transform.parent, new Rect(.755f, .095f, .225f, .135f));
                ClearSurface(actions.PlayButton.transform.parent);
                PlaceButton(actions.PlayButton, new Rect(0, .48f, .49f, .52f), "Button", 26);
                PlaceButton(actions.DiscardButton, new Rect(.51f, .48f, .49f, .52f), "Button", 26);
                PlaceButton(actions.RankSortButton, new Rect(.05f, .02f, .42f, .35f), "SortButton", 18);
                PlaceButton(actions.SuitSortButton, new Rect(.53f, .02f, .42f, .35f), "SortButton", 18);
            }
            var flow = FindHud("Draw Controls/Turn Flow");
            if (flow != null)
            {
                Style(flow, "Panel");
                Text(flow.Find("Flow Hint"), new Rect(.06f, .68f, .88f, .25f), 18);
                foreach (var button in flow.GetComponentsInChildren<Button>(true))
                {
                    Rect area;
                    switch (button.name)
                    {
                        case "−": area = new Rect(.05f, .37f, .18f, .25f); break;
                        case "+": area = new Rect(.77f, .37f, .18f, .25f); break;
                        case "摸牌": area = new Rect(.25f, .37f, .50f, .25f); break;
                        case "跳过": area = new Rect(.53f, .06f, .42f, .25f); break;
                        case "先弃所选牌": area = new Rect(.05f, .06f, .46f, .25f); break;
                        default: area = new Rect(.08f, .19f, .84f, .31f); break;
                    }
                    PlaceButton(button, area, "Button", 19);
                }
            }

            if (preview == null) preview = gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleDemoOutcomePreview>(true)).FirstOrDefault();
            if (preview != null)
            {
                DeskUISkin.Place(preview.transform, new Rect(.285f, .275f, .245f, .09f));
                Style(preview.transform, "StatFrame");
                Text(preview.transform.Find("Outcome Hand"), new Rect(.14f, .59f, .72f, .23f), 24);
                Text(preview.transform.Find("Outcome Value"), new Rect(.14f, .34f, .72f, .25f), 24);
                Text(preview.transform.Find("Outcome Details"), new Rect(.14f, .15f, .72f, .19f), 17);
            }
            if (pile == null) pile = gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DrawPilePresenter>(true)).FirstOrDefault();
            if (pile != null)
            {
                var icon = pile.View.transform.Find("Icon");
                if (icon != null)
                {
                    DeskUISkin.Place(icon, new Rect(.15f, .02f, .065f, .19f));
                    Frame(icon, "Desk Deck Frame", new Rect(-.15f, -.09f, 1.3f, 1.18f), "DeckFrame");
                }
            }
            if (Application.isPlaying) { CompactValues(); StyleActorHud(); }
        }

        private void CompactValues()
        {
            var snapshot = view.Display; if (snapshot == null) return;
            var hero = snapshot.Unit(snapshot.ObserverId); if (hero == null) return;
            var round = FindHud("Turn/Round")?.GetComponent<TMP_Text>();
            var energy = FindHud("Turn/Energy")?.GetComponent<TMP_Text>();
            var health = FindHud("Player/Health")?.GetComponent<TMP_Text>();
            if (round != null) round.text = "回合 " + (snapshot.IsFinished ? snapshot.CompletedRounds : snapshot.CompletedRounds + 1) + " / " + (snapshot.Resource("", "roundLimit")?.Value ?? 20);
            if (energy != null) energy.text = "行动点  <size=125%>" + (snapshot.Resource(hero.Id, "actions")?.Value ?? 0) + "</size>\n<size=70%>出牌 " + (snapshot.Resource(hero.Id, "playCost")?.Value ?? 0) + "  弃牌 " + (snapshot.Resource(hero.Id, "discardCost")?.Value ?? 0) + "</size>";
            if (health != null) health.text = hero.Health + " / " + hero.MaxHealth + (hero.Block > 0 ? "  盾 " + hero.Block : "");
        }
        private void StyleActorHud()
        {
            var root = gameObject.scene.GetRootGameObjects().FirstOrDefault(r => r.name == "BattleDemo Enemy World UI");
            if (root == null) return;
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.name != "Health Bar") continue;
                image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Clamp(image.rectTransform.rect.width * .065f, 12, 22));
                if (!styled.Add(image.GetInstanceID())) continue;
                image.color = new Color32(24, 17, 20, 255);
                var rim = DeskUISkin.Decor(image.transform, "Desk Health Frame", new Rect(-.04f, -.40f, 1.08f, 1.8f), "BossHealthFrame");
                rim.transform.SetAsFirstSibling();
                var label = image.transform.Find("Health Value"); if (label != null) label.SetAsLastSibling();
            }
        }
        private Transform FindHud(string path)
        {
            // ScreenSafeArea moves authored children under its content root at runtime.
            var direct = transform.Find(path);
            return direct != null ? direct : transform.Find("Safe Area/" + path);
        }
        private void Region(string path, Rect area) => DeskUISkin.Place(FindHud(path), area);
        private void Label(string path, Rect area, int size) => Text(FindHud(path), area, size);
        private static void Text(Transform target, Rect area, int size)
        { if (target == null) return; DeskUISkin.Place(target, area); Format(target, size); }
        private static void Format(Transform target, int size)
        {
            var label = target.GetComponent<TMP_Text>(); if (label == null) return;
            label.fontSize = size; label.fontSizeMax = size; label.fontSizeMin = Mathf.Max(12, size * .62f); label.enableAutoSizing = true;
            label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Ellipsis;
            label.margin = Vector4.zero; label.alignment = TextAlignmentOptions.Center; label.color = RitualSkin.Ivory;
        }
        private void PlaceButton(Button button, Rect area, string sprite, int size)
        {
            if (button == null) return;
            DeskUISkin.Place(button.transform, area); StyleButton(button, sprite);
            DeskUISkin.FitRect((RectTransform)button.transform, DeskUISkin.Get(sprite));
            var caption = button.GetComponentInChildren<TMP_Text>();
            if (caption != null)
            {
                if (sprite == "Menu") caption.gameObject.SetActive(false);
                else Text(caption.transform, new Rect(.10f, .18f, .80f, .64f), size);
            }
        }
        private void StyleButton(Button button, string sprite)
        { if (button != null && styled.Add(button.GetInstanceID())) DeskUISkin.Button(button, sprite); }
        private void Style(Transform target, string sprite)
        {
            if (target == null) return;
            var image = target.GetComponent<Image>();
            if (image == null) image = target.gameObject.AddComponent<Image>();
            if (styled.Add(target.GetInstanceID())) { DeskUISkin.Panel(image, sprite); image.raycastTarget = false; }
            DeskUISkin.FitRect(image.rectTransform, DeskUISkin.Get(sprite));
        }
        private static void ClearSurface(Transform target)
        {
            if (target == null) return;
            var image = target.GetComponent<Image>(); if (image != null) image.enabled = false;
            foreach (var name in new[] { "Painted Surface", "Gold Fillet", "Ritual Surface" })
            { var child = target.Find(name); if (child != null) child.gameObject.SetActive(false); }
        }
        private static Image Frame(Transform parent, string name, Rect area, string sprite)
        {
            if (parent == null) return null;
            var image = DeskUISkin.Decor(parent, name, area, sprite);
            DeskUISkin.FitRect(image.rectTransform, image.sprite); image.transform.SetAsFirstSibling(); return image;
        }
        private static void TextInFrame(Transform text, Image frame, Rect interior, int size)
        {
            if (text == null || frame == null) return;
            var rect = frame.rectTransform; var parent = (RectTransform)rect.parent;
            var min = rect.anchorMin; var max = rect.anchorMax;
            var parentSize = parent.rect.size;
            // Anchor arithmetic also works while the editor's inactive Canvas has no world-space layout yet.
            if (parentSize.x > 0 && parentSize.y > 0 && !float.IsInfinity(parentSize.x) && !float.IsInfinity(parentSize.y))
            {
                min += new Vector2(rect.offsetMin.x / parentSize.x, rect.offsetMin.y / parentSize.y);
                max += new Vector2(rect.offsetMax.x / parentSize.x, rect.offsetMax.y / parentSize.y);
            }
            var span = max - min;
            Text(text, new Rect(min.x + span.x * interior.x, min.y + span.y * interior.y,
                span.x * interior.width, span.y * interior.height), size);
        }
    }
}
