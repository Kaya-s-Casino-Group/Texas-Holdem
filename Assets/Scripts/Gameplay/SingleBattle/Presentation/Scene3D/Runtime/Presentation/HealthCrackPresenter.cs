using System;
using global::BattlePresentation;
using System.Linq;
using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    [Serializable]
    public struct HealthCrackStage
    {
        [Range(0, 1)] public float AtOrBelowHealth;
        [Range(0, 1)] public float Progress;
        [Min(0)] public float DepthMultiplier;
        public HealthCrackStage(float health, float progress, float depth)
        { AtOrBelowHealth = health; Progress = progress; DepthMultiplier = depth; }
    }

    /// <summary>Read-only health projection with discrete stages. Preserves VAT properties and shared material assets.</summary>
    [DisallowMultipleComponent]
    public sealed class HealthCrackPresenter : MonoBehaviour
    {
        [SerializeField] private Transform model;
        [SerializeField] private HealthCrackStage[] stages = {
            new HealthCrackStage(1, 0, 0), new HealthCrackStage(.75f, .35f, .35f),
            new HealthCrackStage(.5f, .7f, .7f), new HealthCrackStage(.25f, 1, 1)
        };
        private sealed class Binding
        { public Renderer Renderer; public float MaxDepth; public readonly MaterialPropertyBlock Block = new MaterialPropertyBlock(); }
        private Binding[] bindings;
        private static readonly int ProgressId = Shader.PropertyToID("_CrackProgress"), DepthId = Shader.PropertyToID("_CrackDepth");
        public int CurrentStage { get; private set; }
        public float CurrentProgress => stages[CurrentStage].Progress;
        public void Configure(Transform target)
        {
            if (target == null || target != transform && !target.IsChildOf(transform)) throw new ArgumentException("Bind this model subtree.");
            model = target; bindings = null; ResetVisual();
        }
        private void EnsureBindings()
        {
            if (bindings != null) return;
            if (model == null) throw new InvalidOperationException("Configure the health crack model first.");
            // Prepared VAT meshes use one material slot. Renderer-wide blocks share the VAT time across all slots.
            bindings = model.GetComponentsInChildren<Renderer>(true).Select(renderer => new {
                Renderer = renderer,
                Material = renderer.sharedMaterials.FirstOrDefault(material => material != null && material.HasProperty(ProgressId) && material.HasProperty(DepthId))
            }).Where(item => item.Material != null).Select(item => new Binding {
                Renderer = item.Renderer, MaxDepth = item.Material.GetFloat(DepthId)
            }).ToArray();
        }
        public void Synchronize(CombatantState unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            SynchronizeHealth(unit.Health, unit.MaxHealth);
        }
        public void SynchronizeDisplay(UnitDisplayData unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            SynchronizeHealth(unit.Health, unit.MaxHealth);
        }
        private void SynchronizeHealth(long current, long maximum)
        {
            if (maximum <= 0 || current < 0 || current > maximum) throw new ArgumentException("Invalid display health.");
            ValidateStages();
            float health = (float)current / maximum; int stage = 0;
            for (int i = 1; i < stages.Length; ++i) if (health <= stages[i].AtOrBelowHealth) stage = i;
            Apply(stage);
        }
        private void ValidateStages()
        {
            if (stages == null || stages.Length == 0 || stages[0].AtOrBelowHealth != 1)
                throw new InvalidOperationException("Crack stages must begin at full health.");
            for (int i = 0; i < stages.Length; ++i)
                if (!Finite(stages[i].AtOrBelowHealth) || stages[i].AtOrBelowHealth < 0 || stages[i].AtOrBelowHealth > 1 ||
                    !Finite(stages[i].Progress) || stages[i].Progress < 0 || stages[i].Progress > 1 ||
                    !Finite(stages[i].DepthMultiplier) || stages[i].DepthMultiplier < 0 ||
                    i > 0 && stages[i].AtOrBelowHealth >= stages[i - 1].AtOrBelowHealth)
                    throw new InvalidOperationException("Configure descending health thresholds and valid crack values.");
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void Apply(int stage)
        {
            EnsureBindings(); CurrentStage = stage;
            foreach (var binding in bindings)
            {
                if (binding.Renderer == null) continue;
                binding.Renderer.GetPropertyBlock(binding.Block);
                binding.Block.SetFloat(ProgressId, stages[stage].Progress);
                binding.Block.SetFloat(DepthId, binding.MaxDepth * stages[stage].DepthMultiplier);
                binding.Renderer.SetPropertyBlock(binding.Block);
            }
        }
        public void ResetVisual() { ValidateStages(); Apply(0); }
    }
}
