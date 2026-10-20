using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleHud
    {
        private BattleAdminPanel adminPanel;
        private Button adminButton;
        public BattleAdminPanel Administrator => adminPanel;
        private void EnsureAdminPanel()
        {
            if (!Application.isPlaying || host?.Session == null || restart == null || message == null || adminPanel != null) return;
            adminPanel = gameObject.AddComponent<BattleAdminPanel>();
            adminPanel.Configure(host, message.font);
            adminButton = Instantiate(restart, transform);
            adminButton.name = "Open Battle Administrator"; adminButton.gameObject.SetActive(true);
            adminButton.onClick = new Button.ButtonClickedEvent(); adminButton.onClick.AddListener(adminPanel.Open);
            Place(adminButton.GetComponent<RectTransform>(), 480, -158, 150, 36);
            var label = adminButton.GetComponentInChildren<TMP_Text>();
            label.text = "对局修改器"; label.fontSize = 16; label.enableAutoSizing = false;
            GetComponent<BattleDemoUITheme>()?.StyleButton(adminButton, false);
        }
    }
}
