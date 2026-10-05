using System;
using UnityEngine;

namespace CardPresentation
{
    public partial class HandZoneView
    {
        public const string DefaultDrawSoundResource = "CardPresentation/Audio/CardDraw";
        public const string DefaultDiscardSoundResource = DefaultDrawSoundResource;
        public const string DefaultSelectionSoundResource = DefaultDrawSoundResource;
        [SerializeField] private bool drawSoundEnabled = true;
        [SerializeField] private AudioClip drawSound;
        [SerializeField, Range(0, 1)] private float drawSoundVolume = 1;
        [SerializeField] private bool discardSoundEnabled = true;
        [SerializeField] private AudioClip discardSound;
        [SerializeField, Range(0, 1)] private float discardSoundVolume = 1;
        [SerializeField] private bool selectionSoundEnabled = true;
        [SerializeField] private AudioClip selectionSound;
        [SerializeField, Range(0, 1)] private float selectionSoundVolume = 1;
        private AudioClip defaultDrawSound;
        private AudioSource drawAudioSource;
        private AudioSource discardAudioSource;
        private AudioSource selectionAudioSource;

        public bool DrawSoundEnabled => drawSoundEnabled;
        public AudioClip DrawSoundOverride => drawSound;
        private AudioClip DefaultCardSound => defaultDrawSound != null ? defaultDrawSound :
            defaultDrawSound = Resources.Load<AudioClip>(DefaultDrawSoundResource);
        public AudioClip DrawSound => drawSound != null ? drawSound : DefaultCardSound;
        public float DrawSoundVolume => drawSoundVolume;
        public bool DiscardSoundEnabled => discardSoundEnabled;
        public AudioClip DiscardSoundOverride => discardSound;
        public AudioClip DiscardSound => discardSound != null ? discardSound : DefaultCardSound;
        public float DiscardSoundVolume => discardSoundVolume;
        public bool SelectionSoundEnabled => selectionSoundEnabled;
        public AudioClip SelectionSoundOverride => selectionSound;
        public AudioClip SelectionSound => selectionSound != null ? selectionSound : DefaultCardSound;
        public float SelectionSoundVolume => selectionSoundVolume;

        /// <summary>Launch notification only; draw audio now belongs to CardEnteredZone.</summary>
        public event Action<CardInstanceView> CardDealStarted;

        /// <summary>New card finished its flight and entered the row, before settling. Fires once with draw audio.
        /// Immediate arrivals with transitions disabled also cue; silent restores do not. Playback is Play/Player only.</summary>
        public event Action<CardInstanceView> CardEnteredZone;

        /// <summary>One cue per discard launch, after removal from Data/Views and before visual destruction.</summary>
        public event Action<CardInstanceView> CardDiscardStarted;

        /// <summary>Existing card changed from unselected to selected, after the snapshot is committed. Restores are silent.</summary>
        public event Action<CardInstanceView> CardSelected;

        /// <summary>Configure draws only. A null clip uses the bundled default; discard settings are independent.</summary>
        public void SetDrawSound(AudioClip clip, float volume = 1, bool enabled = true)
        {
            if (!CardZoneRowLayout.Finite(volume) || volume < 0 || volume > 1)
                throw new ArgumentOutOfRangeException(nameof(volume), "Draw volume must be finite and between 0 and 1.");
            drawSound = clip; drawSoundVolume = volume; drawSoundEnabled = enabled;
            if (!enabled || volume == 0) StopDrawAudio();
        }

        /// <summary>Configure discards only. A null clip uses the same bundled placeholder as draws.</summary>
        public void SetDiscardSound(AudioClip clip, float volume = 1, bool enabled = true)
        {
            if (!CardZoneRowLayout.Finite(volume) || volume < 0 || volume > 1)
                throw new ArgumentOutOfRangeException(nameof(volume), "Discard volume must be finite and between 0 and 1.");
            discardSound = clip; discardSoundVolume = volume; discardSoundEnabled = enabled;
            if (!enabled || volume == 0) StopDiscardAudio();
        }

        /// <summary>Configure selection only. A null clip uses the bundled placeholder; draws/discards stay independent.</summary>
        public void SetSelectionSound(AudioClip clip, float volume = 1, bool enabled = true)
        {
            if (!CardZoneRowLayout.Finite(volume) || volume < 0 || volume > 1)
                throw new ArgumentOutOfRangeException(nameof(volume), "Selection volume must be finite and between 0 and 1.");
            selectionSound = clip; selectionSoundVolume = volume; selectionSoundEnabled = enabled;
            if (!enabled || volume == 0) StopSelectionAudio();
        }

        private void ConsumeDealSounds()
        {
            // Consume only after all views and Data agree. Reflows and property refreshes never create a cue.
            foreach (var view in ordered)
            {
                if (view == null) continue;
                if (view.ConsumeDealStart()) NotifyDealStarted(view);
                if (view != null && view.ConsumeDealArrival()) NotifyCardEnteredZone(view);
            }
        }

        private void NotifyDealStarted(CardInstanceView view) => CardDealStarted?.Invoke(view);

        private void NotifyCardEnteredZone(CardInstanceView view)
        {
            PlayCardSound(ref drawAudioSource, DrawSound, drawSoundVolume, drawSoundEnabled, "Draw Audio");
            CardEnteredZone?.Invoke(view);
        }

        private void ConsumeDiscardSounds()
        {
            if (departing.Count == 0) return;
            // Snapshot permits callbacks to clear the zone. Preserve hand order even when a long frame spans several launches.
            foreach (var view in departing.ToArray())
                if (view != null && view.ConsumeDiscardStart()) NotifyDiscardStarted(view);
        }

        private void NotifyDiscardStarted(CardInstanceView view)
        {
            PlayCardSound(ref discardAudioSource, DiscardSound, discardSoundVolume, discardSoundEnabled, "Discard Audio");
            CardDiscardStarted?.Invoke(view);
        }

        private void NotifyCardSelected(CardInstanceView view)
        {
            PlayCardSound(ref selectionAudioSource, SelectionSound, selectionSoundVolume, selectionSoundEnabled, "Selection Audio");
            CardSelected?.Invoke(view);
        }

        private void PlayCardSound(ref AudioSource source, AudioClip clip, float volume, bool enabled, string sourceName)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !enabled || volume <= 0) return;
            if (clip == null) return;
            if (source == null)
            {
                var root = new GameObject(sourceName) { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
                root.transform.SetParent(transform, false);
                source = root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0;
                source.dopplerLevel = 0;
            }
            // Separate sources keep one action's mute from cutting off the other action's tail.
            source.PlayOneShot(clip, volume);
        }

        private void StopDrawAudio() { if (drawAudioSource != null) drawAudioSource.Stop(); }
        private void StopDiscardAudio() { if (discardAudioSource != null) discardAudioSource.Stop(); }
        private void StopSelectionAudio() { if (selectionAudioSource != null) selectionAudioSource.Stop(); }
        private void StopCardAudio() { StopDrawAudio(); StopDiscardAudio(); StopSelectionAudio(); }
        private void ReleaseCardAudio()
        { ReleaseAudio(ref drawAudioSource); ReleaseAudio(ref discardAudioSource); ReleaseAudio(ref selectionAudioSource); }
        private static void ReleaseAudio(ref AudioSource source)
        {
            if (source == null) return;
            var root = source.gameObject; source = null;
            if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
        }
    }
}
