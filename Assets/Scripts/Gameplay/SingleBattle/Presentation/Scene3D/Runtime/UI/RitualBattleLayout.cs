using System.Linq;
using CardPresentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>Scene HUD layout. Leaves the original world portrait clear and reserves the bottom/right for input.</summary>
    [DefaultExecutionOrder(500), DisallowMultipleComponent]
    public sealed class RitualBattleLayout : MonoBehaviour
    {
        [SerializeField] private SingleBattleSceneAdapter view;
        private SingleBattleHud hud;
        private BattleDemoOutcomePreview preview;
        public void Configure(SingleBattleSceneAdapter owner) { view = owner; hud = GetComponent<SingleBattleHud>(); Apply(); }
        private void LateUpdate() => Apply();
        public void Apply()
        {
            if (hud == null) hud = GetComponent<SingleBattleHud>();
            if (hud == null || view == null) return;
            Region(transform.Find("Turn"), new Rect(.015f, .915f, .275f, .075f));
            Text(transform.Find("Turn/Round"), new Rect(.045f, .10f, .44f, .80f), 21);
            Text(transform.Find("Turn/Energy"), new Rect(.51f, .10f, .46f, .80f), 19);
            Region(transform.Find("Player"), new Rect(.015f, .837f, .275f, .065f));
            Text(transform.Find("Player/Health"), new Rect(.04f, .35f, .92f, .52f), 21);
            var bar = transform.Find("Player/Health Bar"); Region(bar, new Rect(.045f, .13f, .91f, .09f));
            Region(transform.Find("Status Rail"), new Rect(.905f, .37f, .082f, .50f));
            Region(transform.Find("Draw Controls"), new Rect(.845f, .075f, .14f, .195f));
            Region(transform.Find("Battle Info"), new Rect(.845f, .915f, .14f, .075f));
            Text(transform.Find("Battle Info/Counts"), new Rect(.05f, .5f, .90f, .5f), 18);
            Text(transform.Find("Battle Info/Score"), new Rect(.05f, 0, .90f, .5f), 17);
            Region(transform.Find("Battle Tools"), new Rect(.32f, .925f, .25f, .055f));
            var tools = transform.Find("Battle Tools");
            if (tools != null)
            {
                var toggle = tools.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.transform.parent == tools);
                if (toggle != null) { Region(toggle.transform, new Rect(0, 0, 1, 1)); Caption(toggle, 20); }
                var detail = tools.Find("Details") as RectTransform;
                if (detail != null) { detail.anchorMin = new Vector2(0, 0); detail.anchorMax = new Vector2(1, 0); detail.pivot = new Vector2(0, 1); detail.anchoredPosition = new Vector2(0, -8); detail.sizeDelta = new Vector2(0, 490); }
            }
            if (hud.AdministratorButton != null) { Region(hud.AdministratorButton.transform, new Rect(.60f, .935f, .105f, .045f)); Caption(hud.AdministratorButton, 18); }
            Region(hud.EndTurnButton.transform, new Rect(.845f, .016f, .14f, .05f)); Caption(hud.EndTurnButton, 19);
            if (view.Actions?.Panel != null)
            {
                var actions = view.Actions.Panel;
                var root = actions.PlayButton.transform.parent;
                Region(root, new Rect(.845f, .078f, .14f, .19f));
                Region(actions.PlayButton.transform, new Rect(0, .51f, .49f, .49f));
                Region(actions.DiscardButton.transform, new Rect(.51f, .51f, .49f, .49f));
                Region(actions.RankSortButton.transform, new Rect(0, .02f, .49f, .40f));
                Region(actions.SuitSortButton.transform, new Rect(.51f, .02f, .49f, .40f));
                Caption(actions.PlayButton, 26); Caption(actions.DiscardButton, 26);
                Caption(actions.RankSortButton, 18); Caption(actions.SuitSortButton, 18);
            }
            if (preview == null) preview = gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleDemoOutcomePreview>(true)).FirstOrDefault();
            if (preview != null)
            {
                Region(preview.transform, new Rect(.385f, .205f, .44f, .09f));
                Text(preview.transform.Find("Outcome Hand"), new Rect(.02f, .57f, .35f, .39f), 22);
                Text(preview.transform.Find("Outcome Value"), new Rect(.38f, .51f, .60f, .46f), 26);
                Text(preview.transform.Find("Outcome Details"), new Rect(.02f, .02f, .96f, .42f), 18);
            }
            var flow = transform.Find("Draw Controls/Turn Flow");
            if (flow != null)
            {
                Text(flow.Find("Flow Hint"), new Rect(.04f, .68f, .92f, .28f), 17);
                Region(flow.Find("−"), new Rect(.04f, .38f, .15f, .22f));
                Region(flow.Find("摸牌"), new Rect(.21f, .38f, .58f, .22f));
                Region(flow.Find("+"), new Rect(.81f, .38f, .15f, .22f));
                Region(flow.Find("先弃所选牌"), new Rect(.04f, .08f, .52f, .22f));
                Region(flow.Find("跳过"), new Rect(.59f, .08f, .37f, .22f));
                Region(flow.Find("确认强制弃牌"), new Rect(.04f, .15f, .92f, .40f));
                foreach (var button in flow.GetComponentsInChildren<Button>(true)) Caption(button, 18);
            }
        }
        private static void Region(Transform target, Rect area) { if (target != null) RitualSkin.Place(target, area); }
        private static void Caption(Button button, int size)
        {
            var label = button.GetComponentInChildren<TMP_Text>(); if (label == null) return;
            Text(label.transform, new Rect(.04f, .06f, .92f, .88f), size); label.alignment = TextAlignmentOptions.Center;
        }
        private static void Text(Transform target, Rect area, int size)
        {
            if (target == null) return; Region(target, area); var text = target.GetComponent<TMP_Text>(); if (text == null) return;
            text.fontSize = size; text.enableAutoSizing = true; text.fontSizeMax = size; text.fontSizeMin = size - 2;
            text.margin = Vector4.zero; text.enableWordWrapping = true;
        }
    }
}
