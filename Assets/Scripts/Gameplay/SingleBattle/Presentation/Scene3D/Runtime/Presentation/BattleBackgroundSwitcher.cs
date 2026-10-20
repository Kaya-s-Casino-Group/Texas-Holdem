using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public enum BattleBackground { Elite, Minion, Boss }

    /// <summary>Changes scenery only; the active encounter and its authority stay alive.</summary>
    [ExecuteAlways]
    public sealed class BattleBackgroundSwitcher : MonoBehaviour
    {
        [SerializeField] private BattleBackground background;
        [SerializeField] private GameObject elite, minion, boss;
        [SerializeField] private Button eliteButton, minionButton, bossButton;
        private bool invalid = true;
        public BattleBackground Current => background;

        public void Configure(GameObject eliteRoot, GameObject minionRoot, GameObject bossRoot,
            Button eliteChoice = null, Button minionChoice = null, Button bossChoice = null)
        {
            Unbind();
            elite = eliteRoot; minion = minionRoot; boss = bossRoot;
            eliteButton = eliteChoice; minionButton = minionChoice; bossButton = bossChoice;
            if (isActiveAndEnabled) Bind();
            Apply();
        }

        public void Select(BattleBackground value) { background = value; Apply(); }
        public void SelectElite() => Select(BattleBackground.Elite);
        public void SelectMinion() => Select(BattleBackground.Minion);
        public void SelectBoss() => Select(BattleBackground.Boss);
        private void OnValidate() => invalid = true;
        private void OnEnable() { Bind(); Apply(); }
        private void OnDisable() => Unbind();
        private void Update() { if (invalid) Apply(); }
        private void Bind()
        {
            if (eliteButton != null) eliteButton.onClick.AddListener(SelectElite);
            if (minionButton != null) minionButton.onClick.AddListener(SelectMinion);
            if (bossButton != null) bossButton.onClick.AddListener(SelectBoss);
        }
        private void Unbind()
        {
            if (eliteButton != null) eliteButton.onClick.RemoveListener(SelectElite);
            if (minionButton != null) minionButton.onClick.RemoveListener(SelectMinion);
            if (bossButton != null) bossButton.onClick.RemoveListener(SelectBoss);
        }
        private void Apply()
        {
            invalid = false;
            Activate(elite, eliteButton, background == BattleBackground.Elite);
            Activate(minion, minionButton, background == BattleBackground.Minion);
            Activate(boss, bossButton, background == BattleBackground.Boss);
        }
        private static void Activate(GameObject root, Button button, bool selected)
        {
            if (root != null && root.activeSelf != selected) root.SetActive(selected);
            if (button != null)
            {
                button.interactable = !selected;
                button.GetComponent<Image>().color = selected ?
                    new Color32(112, 75, 62, 245) : new Color32(39, 30, 40, 230);
            }
        }
    }
}
