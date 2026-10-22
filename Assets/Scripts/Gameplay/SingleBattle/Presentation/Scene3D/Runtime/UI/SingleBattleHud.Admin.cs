using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleHud
    {
        [Header("对局修改器 · F5 打开窗口")]
        [SerializeField] private BattleAdminPanel adminPanel;
        [SerializeField] private Button adminButton;
        public BattleAdminPanel Administrator => adminPanel;
        public Button AdministratorButton => adminButton;

        private void RefreshAdminEntry()
        {
            // Keep the serialized legacy reference so existing scenes migrate without rebuilding.
            if (adminButton != null && adminButton.gameObject.activeSelf) adminButton.gameObject.SetActive(false);
        }

        private void UpdateAdministratorShortcut()
        {
            if (Application.isPlaying && Application.isFocused && Keyboard.current?.f5Key.wasPressedThisFrame == true)
                OpenAdministrator();
        }

        public void OpenAdministrator()
        {
            if (!Application.isPlaying || host?.IsHandGuideOpen == true || adminPanel == null || adminPanel.IsOpen || host?.Session == null || message == null) return;
            adminPanel.Configure(host, message.font);
            adminPanel.Open();
        }
    }
}
