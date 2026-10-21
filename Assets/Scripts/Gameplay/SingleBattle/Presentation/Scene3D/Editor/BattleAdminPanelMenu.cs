using UnityEditor;
using UnityEngine;

namespace Poker.SingleBattle.Editor
{
    public static class BattleAdminPanelMenu
    {
        [MenuItem("Tools/BattleDemo/Open Runtime Administrator")]
        public static void Open()
        {
            var hud = Object.FindObjectOfType<SingleBattleHud>();
            if (hud?.Administrator != null) hud.OpenAdministrator();
        }

        [MenuItem("Tools/BattleDemo/Open Runtime Administrator", true)]
        private static bool CanOpen() => Application.isPlaying && Object.FindObjectOfType<SingleBattleHud>()?.Administrator != null;
    }
}
