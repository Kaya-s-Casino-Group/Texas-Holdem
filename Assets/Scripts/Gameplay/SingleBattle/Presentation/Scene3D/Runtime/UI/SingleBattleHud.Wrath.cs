using System.Collections.Generic;
using System.Linq;
using global::BattlePresentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleHud
    {
        private RectTransform wrathPanel;
        private TMP_Text wrathAttack, wrathRegular, wrathWarning;
        private Image wrathWarningBackground;
        private Outline wrathWarningGlow;
        private readonly Dictionary<string, Button> wrathTargets = new Dictionary<string, Button>();
        private string wrathTargetRoster;

        /// <summary>Explicit body identity avoids treating a live hand as the boss after victory.</summary>
        public static UnitDisplayData WrathBody(BattleDisplaySnapshot snapshot) =>
            snapshot != null && snapshot.Labels.TryGetValue("wrath.body", out var id) ? snapshot.Unit(id) : null;

        // Adventure owns the remote presentation; this panel belongs to the standalone validation scene.
        private bool UsesWrathDemoPresentation => host != null && !host.UsesExternalCommands;

        private bool RefreshWrathHud()
        {
            if (!UsesWrathDemoPresentation) return false;
            var snapshot = host.Display;
            var body = WrathBody(snapshot);
            if (body == null) return false;
            HideEnemyIntents();
            intent.gameObject.SetActive(false);
            if (intentPanel != null) intentPanel.SetActive(false);
            EnsureWrathHud();
            if (wrathPanel == null) return true;
            wrathPanel.gameObject.SetActive(!snapshot.IsFinished);
            wrathAttack.text = snapshot.Labels.TryGetValue("wrath.attack", out var attack) ? attack : "暴怒";
            if (snapshot.Labels.TryGetValue("wrath.current", out var current)) wrathAttack.text += "\n" + current;
            wrathRegular.text = "常规意图 · 首格为当前（眩晕时不推进）";
            wrathWarning.text = snapshot.Labels.TryGetValue("wrath.ultimate", out var warning) ? warning : "";
            bool active = snapshot.Resource(body.Id, "wrath.warning")?.Value == 1;
            wrathWarningBackground.color = active ? new Color32(145, 27, 42, 245) : new Color32(40, 36, 44, 235);
            wrathWarningGlow.enabled = active;
            wrathWarning.color = active ? new Color32(255, 102, 104, 255) : new Color32(228, 214, 187, 255);
            var units = new[] { body }.Concat(snapshot.Units.Where(u => snapshot.Labels.ContainsKey("wrath.hand/" + u.Id))).ToArray();
            string roster = string.Join("|", units.Select(unit => unit.Id));
            if (wrathTargetRoster != roster)
            {
                foreach (var target in wrathTargets.Values) if (target != null) Destroy(target.gameObject);
                wrathTargets.Clear(); wrathTargetRoster = roster;
                for (int i = 0; i < units.Length; i++)
                {
                    string id = units[i].Id;
                    var button = new GameObject("Wrath Target " + id, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
                    button.transform.SetParent(wrathPanel, false); button.gameObject.layer = gameObject.layer;
                    var rect = button.GetComponent<RectTransform>();
                    int column = i % 3, row = i / 3;
                    rect.anchorMin = new Vector2(column / 3f, .22f - row * .19f);
                    rect.anchorMax = new Vector2((column + 1) / 3f, .41f - row * .19f);
                    rect.pivot = new Vector2(.5f, 0); rect.offsetMin = new Vector2(4, 0); rect.offsetMax = new Vector2(-4, 0);
                    button.targetGraphic = button.GetComponent<Image>();
                    var text = WrathLabel(button.transform, "State", 11);
                    text.enableWordWrapping = false;
                    text.alignment = TextAlignmentOptions.Center;
                    text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
                    text.rectTransform.offsetMin = new Vector2(4, 3); text.rectTransform.offsetMax = new Vector2(-4, -3);
                    button.onClick.AddListener(() => SelectWrathTarget(id));
                    wrathTargets.Add(id, button);
                }
            }
            RefreshWrathTargetAvailability();
            return true;
        }

        private void EnsureWrathHud()
        {
            if (wrathPanel != null || message == null) return;
            wrathPanel = new GameObject("Wrath Boss", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            wrathPanel.SetParent(transform, false); wrathPanel.gameObject.layer = gameObject.layer;
            wrathPanel.anchorMin = new Vector2(.02f, .48f); wrathPanel.anchorMax = new Vector2(.38f, .90f);
            wrathPanel.offsetMin = wrathPanel.offsetMax = Vector2.zero;
            wrathPanel.GetComponent<Image>().color = new Color32(24, 21, 29, 245);
            wrathPanel.GetComponent<Image>().raycastTarget = false;
            wrathAttack = WrathLabel(wrathPanel, "Attack", 16);
            WrathTextRect(wrathAttack, .86f, .99f);
            wrathAttack.rectTransform.anchorMax = new Vector2(.85f, .99f);
            wrathRegular = WrathLabel(wrathPanel, "Regular Intents", 13);
            WrathTextRect(wrathRegular, .79f, .86f);
            var warningRoot = new GameObject("Ultimate Warning", typeof(RectTransform), typeof(Image), typeof(Outline)).GetComponent<RectTransform>();
            warningRoot.SetParent(wrathPanel, false); warningRoot.gameObject.layer = gameObject.layer;
            warningRoot.anchorMin = new Vector2(0, .43f); warningRoot.anchorMax = new Vector2(1, .64f);
            warningRoot.offsetMin = new Vector2(8, 0); warningRoot.offsetMax = new Vector2(-8, 0);
            wrathWarningBackground = warningRoot.GetComponent<Image>(); wrathWarningBackground.raycastTarget = false;
            wrathWarningGlow = warningRoot.GetComponent<Outline>(); wrathWarningGlow.effectColor = new Color32(255, 48, 64, 225);
            wrathWarningGlow.effectDistance = new Vector2(3, -3); wrathWarningGlow.enabled = false;
            wrathWarning = WrathLabel(warningRoot, "Warning", 13);
            wrathWarning.rectTransform.anchorMin = Vector2.zero; wrathWarning.rectTransform.anchorMax = Vector2.one;
            wrathWarning.rectTransform.offsetMin = new Vector2(8, 4); wrathWarning.rectTransform.offsetMax = new Vector2(-8, -4);
        }

        private TMP_Text WrathLabel(Transform parent, string labelName, int size)
        {
            var text = Instantiate(message, parent); text.name = labelName; text.gameObject.SetActive(true);
            text.fontSize = size; text.enableAutoSizing = true; text.fontSizeMin = 9; text.fontSizeMax = size;
            text.richText = false; text.raycastTarget = false; text.enableWordWrapping = true;
            text.alignment = TextAlignmentOptions.TopLeft; text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = new Color32(241, 226, 201, 255); text.rectTransform.localRotation = Quaternion.identity;
            return text;
        }

        private static void WrathTextRect(TMP_Text text, float bottom, float top)
        {
            var rect = text.rectTransform; rect.anchorMin = new Vector2(0, bottom); rect.anchorMax = new Vector2(1, top);
            rect.pivot = new Vector2(.5f, 1); rect.offsetMin = new Vector2(10, 0); rect.offsetMax = new Vector2(-10, 0);
        }

        private void SelectWrathTarget(string id)
        {
            if (host == null || !host.CanChoosePlayTarget) return;
            var choice = host.PlayTargetChoices.FirstOrDefault(slot => slot.Candidates.Any(candidate => candidate.Id == id));
            if (choice != null && host.SelectPlayTarget(choice.Slot, id)) RefreshWrathTargetAvailability();
        }

        private void RefreshWrathTargetAvailability()
        {
            if (wrathPanel == null || !wrathPanel.gameObject.activeInHierarchy || host?.Display == null) return;
            var snapshot = host.Display; var body = WrathBody(snapshot);
            foreach (var entry in wrathTargets)
            {
                var unit = snapshot.Unit(entry.Key); var button = entry.Value;
                if (unit == null || button == null) continue;
                var choice = host.PlayTargetChoices.FirstOrDefault(slot => slot.Candidates.Any(candidate => candidate.Id == unit.Id));
                bool selected = choice != null && choice.SelectedId == unit.Id;
                button.interactable = unit.IsAlive && host.CanChoosePlayTarget && choice != null;
                button.GetComponent<Image>().color = selected ? new Color32(125, 85, 43, 255) :
                    unit.IsAlive ? new Color32(52, 47, 57, 255) : new Color32(32, 30, 35, 255);
                string name = unit.Id == body?.Id ? "暴怒本体" : snapshot.Labels.TryGetValue("wrath.name/" + unit.Id, out var handName) ? handName : unit.Name;
                long remaining = snapshot.Resource(unit.Id, "wrath.reviveRemaining")?.Value ?? 0;
                string detail = unit.Id == body?.Id ? unit.Health + " / " + unit.MaxHealth : unit.IsAlive ? "1 / 1" :
                    remaining == 0 ? "本轮敌方阶段再生" : "再生 " + remaining + " 轮";
                // Use the displayed forecast, including skipped actions, rather than reconstructing hand rules in the HUD.
                if (unit.Id != body?.Id && unit.IsAlive && snapshot.Labels.TryGetValue("intent/" + unit.Id, out var handIntent))
                    detail += "\n" + handIntent.Split('\n')[0];
                button.GetComponentInChildren<TMP_Text>().text = (selected ? "✓ " : "") + name + "\n" + detail;
            }
        }

        private void HideWrathHud()
        { if (wrathPanel != null) wrathPanel.gameObject.SetActive(false); UpdateWrathPromptViews(); }
    }
}
