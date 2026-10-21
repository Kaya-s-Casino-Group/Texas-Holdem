using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleHud
    {
        [Header("对局修改器 · 场景 GUI 固定入口")]
        [SerializeField] private BattleAdminPanel adminPanel;
        [SerializeField] private Button adminButton;
        public BattleAdminPanel Administrator => adminPanel;
        public Button AdministratorButton => adminButton;

        private void RefreshAdminEntry()
        {
            if (!Application.isPlaying || adminButton == null) return;
            adminButton.interactable = adminPanel != null && host?.Session != null;
        }

        // Bound persistently by the scene button, so no runtime clone or listener installation is required.
        public void OpenAdministrator()
        {
            if (!Application.isPlaying || adminPanel == null || host?.Session == null || message == null) return;
            adminPanel.Configure(host, message.font);
            adminPanel.Open();
        }
    }
}
