using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>Pointer bridge for BattleDemo relic icons and their interactive description panel.</summary>
    public sealed class BattleDemoRelicHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private SingleBattleHud owner;
        private string instanceId;
        private bool activeItem;
        public void Configure(SingleBattleHud hud, string id, bool active = false)
        { owner = hud; instanceId = id; activeItem = active; }
        public void BindBase(Image background)
        {
            background.color = activeItem ? (owner.IsRelicUseOpen(instanceId)
                ? new Color32(188, 255, 226, 255) : new Color32(112, 210, 176, 255)) : Color.white;
            background.raycastTarget = true;
            var target = background.GetComponent<BattleDemoRelicHoverTarget>() ?? background.gameObject.AddComponent<BattleDemoRelicHoverTarget>();
            target.enabled = true; target.Configure(owner, instanceId, activeItem);
        }
        public void OnPointerEnter(PointerEventData data)
        {
            if (instanceId == null) owner?.KeepRelicHover();
            else owner?.ShowRelicHover(instanceId, (RectTransform)transform);
        }
        public void OnPointerExit(PointerEventData data) => owner?.LeaveRelicHover();
        public void OnPointerClick(PointerEventData data)
        {
            if (activeItem && data.button == PointerEventData.InputButton.Left)
                owner?.ShowRelicHover(instanceId, (RectTransform)transform, forUse: true);
        }
    }
}
