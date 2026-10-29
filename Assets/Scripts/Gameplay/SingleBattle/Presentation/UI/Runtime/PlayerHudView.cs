using BattlePresentation;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.BattlePresentation
{
    [DisallowMultipleComponent]
    public sealed class PlayerHudView : MonoBehaviour
    {
        [SerializeField] private BattleTimelineSceneAdapter source;
        [SerializeField] private Image healthFill;
        [SerializeField] private Text healthValue, shieldValue;
        private BattleDisplaySnapshot displayed;
        private HealthDissolve dissolve;
        private bool hasUnit;

        public void Configure(BattleTimelineSceneAdapter adapter, Image fill, Text health, Text shield)
        { source = adapter; healthFill = fill; healthValue = health; shieldValue = shield; displayed = null; }

        public void Render(UnitDisplayData unit)
        {
            float amount = unit == null || unit.MaxHealth <= 0 ? 0 : Mathf.Clamp01((float)unit.Health / unit.MaxHealth);
            if (dissolve == null) dissolve = healthFill.GetComponent<HealthDissolve>();
            if (dissolve != null) dissolve.SetAmount(amount, !hasUnit || unit == null);
            else healthFill.fillAmount = amount;
            hasUnit = unit != null;
            healthValue.text = unit == null ? "— / —" : unit.Health + " / " + unit.MaxHealth;
            shieldValue.text = unit == null ? "—" : unit.Block.ToString();
        }

        private void OnEnable() { displayed = null; hasUnit = false; }
        private void LateUpdate()
        {
            var snapshot = source != null ? source.Display : null;
            if (snapshot != null && ReferenceEquals(displayed, snapshot)) return;
            displayed = snapshot;
            Render(snapshot?.Unit(snapshot.ObserverId));
        }
    }
}
