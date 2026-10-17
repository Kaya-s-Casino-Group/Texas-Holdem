using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Poker.SingleBattle
{
    /// <summary>Optional, non-blocking presentation audio. No unit state or turn authority.</summary>
    public interface IBattlePresentationAudio
    {
        void Play(BattlePresentationKind kind);
        void StopSounds();
    }

    [Serializable]
    public sealed class BattleSoundSettings
    {
        [Tooltip("Optional. Leave empty to make this cue silent.")] public AudioClip Clip;
        [Range(0, 1)] public float Volume = 1;
        [Range(.1f, 3)] public float Pitch = 1;
        [Min(0)] public float DelaySeconds;
        [Range(0, 1)] public float SpatialBlend = 1;
        [Min(.001f)] public float MinDistance = .25f;
        [Min(.001f)] public float MaxDistance = 8;
    }

    /// <summary>Three independent voices, with empty clips by default. Different cues can overlap without changing each other's pitch.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleActorAudio : MonoBehaviour, IBattlePresentationAudio
    {
        [SerializeField] private BattleSoundSettings attack = new BattleSoundSettings();
        [SerializeField] private BattleSoundSettings hit = new BattleSoundSettings();
        [SerializeField] private BattleSoundSettings death = new BattleSoundSettings();
        [SerializeField] private AudioSource attackSource, hitSource, deathSource;
        [SerializeField] private AudioMixerGroup output;
        public BattleSoundSettings GetSettings(BattlePresentationKind kind) => kind == BattlePresentationKind.Attack ? attack :
            kind == BattlePresentationKind.Hit ? hit : kind == BattlePresentationKind.Death ? death : null;
        public AudioSource GetSource(BattlePresentationKind kind) => kind == BattlePresentationKind.Attack ? attackSource :
            kind == BattlePresentationKind.Hit ? hitSource : kind == BattlePresentationKind.Death ? deathSource : null;
        public void Configure(AudioSource attackVoice, AudioSource hitVoice, AudioSource deathVoice)
        {
            if (!Owns(attackVoice) || !Owns(hitVoice) || !Owns(deathVoice) ||
                attackVoice == hitVoice || attackVoice == deathVoice || hitVoice == deathVoice)
                throw new ArgumentException("Bind three distinct voices in this audio subtree.");
            StopSounds(); attackSource = attackVoice; hitSource = hitVoice; deathSource = deathVoice;
            Apply(attackSource, attack); Apply(hitSource, hit); Apply(deathSource, death);
        }
        private bool Owns(AudioSource source) => source != null && (source.transform == transform || source.transform.IsChildOf(transform));
        public void Play(BattlePresentationKind kind)
        {
            var settings = GetSettings(kind); var source = GetSource(kind);
            if (!Application.isPlaying || !isActiveAndEnabled || settings == null || settings.Clip == null ||
                source == null || !source.isActiveAndEnabled) return;
            source.Stop(); Apply(source, settings);
            source.PlayDelayed(Mathf.Max(0, FiniteOr(settings.DelaySeconds, 0)));
        }
        private void Apply(AudioSource source, BattleSoundSettings settings)
        {
            source.playOnAwake = false; source.loop = false; source.dopplerLevel = 0;
            source.clip = settings.Clip; source.outputAudioMixerGroup = output;
            source.volume = Mathf.Clamp01(FiniteOr(settings.Volume, 1));
            source.pitch = Mathf.Clamp(FiniteOr(settings.Pitch, 1), .1f, 3);
            source.spatialBlend = Mathf.Clamp01(FiniteOr(settings.SpatialBlend, 1));
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = Mathf.Max(.001f, FiniteOr(settings.MinDistance, .25f));
            source.maxDistance = Mathf.Max(source.minDistance + .001f, FiniteOr(settings.MaxDistance, 8));
        }
        private static float FiniteOr(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        public void StopSounds()
        {
            if (attackSource != null) attackSource.Stop();
            if (hitSource != null) hitSource.Stop();
            if (deathSource != null) deathSource.Stop();
        }
        private void OnDisable() => StopSounds();
    }
}
