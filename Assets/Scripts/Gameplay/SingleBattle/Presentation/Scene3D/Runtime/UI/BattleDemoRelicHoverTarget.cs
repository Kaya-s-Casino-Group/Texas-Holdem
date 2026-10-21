using UnityEngine;
using UnityEngine.EventSystems;

namespace Poker.SingleBattle
{
    /// <summary>Pointer bridge for BattleDemo relic icons and their interactive description panel.</summary>
    public sealed class BattleDemoRelicHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private SingleBattleHud owner;
        private string instanceId;
        public void Configure(SingleBattleHud hud, string id) { owner = hud; instanceId = id; }
        public void OnPointerEnter(PointerEventData data)
        {
            if (instanceId == null) owner?.KeepRelicHover();
            else owner?.ShowRelicHover(instanceId, (RectTransform)transform);
        }
        public void OnPointerExit(PointerEventData data) => owner?.LeaveRelicHover();
    }
}
