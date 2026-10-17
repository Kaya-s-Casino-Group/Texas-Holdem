using System;
using System.Collections;
using RetroRendering;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Collapse completion is driven by the VAT player itself; no host-side duration assumption.</summary>
    public sealed class VatBattleActorPresenter : BattleActorPresenter
    {
        [SerializeField] private RigidVATPlayer collapse;
        [SerializeField] private HealthCrackPresenter cracks;
        public override bool UsesDeathTimeline => collapse != null;
        public HealthCrackPresenter Cracks => cracks;
        public void ConfigureCracks(HealthCrackPresenter view) => cracks = view;
        public override void Synchronize(CombatantState unit) { if (cracks != null) cracks.Synchronize(unit); }
        public void Configure(Transform model, RigidVATPlayer timeline)
        {
            if (timeline == null || !timeline.transform.IsChildOf(model) && timeline.transform != model)
                throw new ArgumentException("Bind a VAT timeline within this model.");
            base.Configure(model); collapse = timeline;
        }
        protected override IEnumerator PlayDeath()
        {
            if (collapse == null) throw new InvalidOperationException("The VAT timeline was destroyed.");
            collapse.Play();
            while (collapse != null && collapse.isActiveAndEnabled && collapse.IsPlaying) yield return null;
            if (collapse == null || !collapse.isActiveAndEnabled || collapse.Progress < 1)
                throw new InvalidOperationException("The VAT timeline stopped before completing.");
        }
        protected override void SnapDeath() { if (collapse != null) { collapse.Pause(); collapse.SetProgress(1); } }
        public override void CancelPresentation() { if (collapse != null) collapse.Pause(); base.CancelPresentation(); }
        public override void ResetPresentation()
        { base.ResetPresentation(); if (collapse != null) collapse.ResetPose(); if (cracks != null) cracks.ResetVisual(); }
    }
}
