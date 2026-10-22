using System;
using System.Collections;
using System.Linq;
using CardPresentation;
using Poker.SingleBattle;
using PokerEngine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Poker.Adventure
{
    /// <summary>Scene-local music and feedback. Observes accepted adventure changes without owning game state.</summary>
    [DisallowMultipleComponent]
    public sealed class AdventureAudioController : MonoBehaviour
    {
        public enum MusicContext { MainMenu, NormalBattle, EliteBattle, Boss, Rest }

        [SerializeField] private AdventureSceneHost host;
        [Header("飞书交付音乐，依次为主菜单、普通战斗、精英、Boss、休息")]
        [SerializeField] private AudioClip[] music = Array.Empty<AudioClip>();
        [SerializeField] private AudioSource musicSource, feedbackSource;
        [SerializeField, Range(0, 1)] private float musicVolume = .34f;
        [SerializeField, Min(.01f)] private float fadeSeconds = .4f;
        [Header("抽牌交替变体")]
        [SerializeField] private AudioClip[] drawSounds = Array.Empty<AudioClip>();
        [SerializeField, Range(0, 1)] private float drawVolume = .55f;
        [SerializeField, Range(0, 1)] private float longDrawVolume = .2f;
        [Header("未交付音效暂用现有素材")]
        [SerializeField] private AudioClip click, denied, coins, victory, defeat, warning;
        [SerializeField, Range(0, 1)] private float feedbackVolume = .65f;
        [SerializeField, Range(0, 1)] private float effectsVolume = 1;
        private Coroutine musicFade;
        private AudioClip desiredMusic;
        private HandZoneView hand;
        private bool subscribed, bindButtons;
        private int drawIndex;
        private float longDrawUntil;
        private string runId, battleId;
        private long gold;
        private AdventurePhase phase;
        private bool warningVisible;

        public MusicContext CurrentMusic { get; private set; }
        public AudioSource MusicSource => musicSource;
        public AudioSource FeedbackSource => feedbackSource;
        public float MusicVolume
        {
            get => musicVolume;
            set
            {
                musicVolume = SafeVolume(value);
                if (musicSource != null && musicFade == null) musicSource.volume = musicVolume;
            }
        }
        public float EffectsVolume
        {
            get => effectsVolume;
            set { effectsVolume = SafeVolume(value); ApplyEffectsVolume(); }
        }

        public void Configure(AdventureSceneHost owner, AudioSource musicVoice, AudioSource feedbackVoice,
            AudioClip[] tracks, AudioClip[] draws)
        {
            if (owner == null || owner.gameObject.scene != gameObject.scene || musicVoice == null || feedbackVoice == null ||
                musicVoice == feedbackVoice || !musicVoice.transform.IsChildOf(transform) || !feedbackVoice.transform.IsChildOf(transform))
                throw new ArgumentException("Bind this scene's adventure and two distinct audio children.");
            Unsubscribe(); host = owner; musicSource = musicVoice; feedbackSource = feedbackVoice;
            music = tracks ?? Array.Empty<AudioClip>(); drawSounds = draws ?? Array.Empty<AudioClip>();
            if (Application.isPlaying && isActiveAndEnabled) { Subscribe(); Refresh(); }
        }

        public void ConfigureFeedback(AudioClip button, AudioClip invalid, AudioClip goldChange, AudioClip win, AudioClip lose, AudioClip bossWarning)
        { click = button; denied = invalid; coins = goldChange; victory = win; defeat = lose; warning = bossWarning; }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Subscribe(); Refresh(); bindButtons = true;
        }
        private void Start() { Subscribe(); Refresh(); }
        private void Subscribe()
        {
            if (host == null || subscribed) return;
            subscribed = true; host.Changed += Refresh;
            if (host.BattleView != null) host.BattleView.StateChanged += BattleChanged;
            var handObject = host.BattleView?.Actions?.Hand;
            hand = handObject != null ? handObject.GetComponent<HandZoneView>() : null;
            if (hand != null) hand.CardEnteredZone += NextDrawSound;
            ApplyEffectsVolume();
        }
        private void Unsubscribe()
        {
            if (subscribed && host != null)
            {
                host.Changed -= Refresh;
                if (host.BattleView != null) host.BattleView.StateChanged -= BattleChanged;
            }
            if (hand != null) hand.CardEnteredZone -= NextDrawSound;
            hand = null; subscribed = false;
        }
        private void OnDisable()
        {
            Unsubscribe();
            if (musicFade != null) StopCoroutine(musicFade);
            musicFade = null; desiredMusic = null;
            if (musicSource != null) musicSource.Stop();
            if (feedbackSource != null) feedbackSource.Stop();
        }

        private void LateUpdate()
        {
            if (!bindButtons) return;
            bindButtons = false;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var button in root.GetComponentsInChildren<Button>(true)) BindButton(button, this);
        }

        private void Refresh()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || host == null) return;
            bindButtons = true;
            var run = host.Adventure;
            var context = MusicContext.MainMenu;
            if (!host.IsAtMenu && run != null)
            {
                if (run.Phase == AdventurePhase.Rest) context = MusicContext.Rest;
                else if (run.Phase == AdventurePhase.Battle || run.Phase == AdventurePhase.Reward)
                    context = run.CurrentNode?.Kind == AdventureNodeKind.Boss ? MusicContext.Boss :
                        run.CurrentNode?.Kind == AdventureNodeKind.Elite ? MusicContext.EliteBattle : MusicContext.NormalBattle;
            }
            SetMusic(context);
            if (run == null) { runId = null; return; }
            if (runId == run.RunId && !host.IsAtMenu)
            {
                if (gold != run.Gold) Play(coins, .8f);
                if (phase != run.Phase)
                {
                    if (run.Phase == AdventurePhase.Reward || run.Phase == AdventurePhase.Completed) Play(victory);
                    else if (run.Phase == AdventurePhase.Failed) Play(defeat);
                }
            }
            runId = run.RunId; gold = run.Gold; phase = run.Phase;
        }

        private void BattleChanged()
        {
            bindButtons = true;
            var snapshot = host?.BattleView?.Display;
            if (snapshot == null) return;
            bool active = !snapshot.IsFinished && snapshot.Resources.Any(r => r.Key == "wrath.warning" && r.Value == 1);
            if (battleId == snapshot.BattleId && active && !warningVisible) Play(warning);
            battleId = snapshot.BattleId; warningVisible = active;
        }

        private void NextDrawSound(CardInstanceView card)
        {
            if (hand == null || drawSounds.Length == 0) return;
            var played = hand.DrawSoundOverride;
            if (played != null && played.length > 1.5f)
                longDrawUntil = Time.unscaledTime + played.length;
            drawIndex = (drawIndex + 1) % drawSounds.Length;
            var next = drawSounds[drawIndex];
            // The delivered long variant contains a full shuffle. Keep one tail, with short single-card arrivals over it.
            if (next != null && next.length > 1.5f && Time.unscaledTime < longDrawUntil)
            {
                int shortIndex = Array.FindIndex(drawSounds, clip => clip != null && clip.length <= 1.5f);
                if (shortIndex >= 0) { drawIndex = shortIndex; next = drawSounds[shortIndex]; }
            }
            // The hand already played this card's arrival. Select the next variant only.
            hand.SetDrawSound(next, DrawVolume(next) * effectsVolume, hand.DrawSoundEnabled);
        }
        private float DrawVolume(AudioClip clip) => clip != null && clip.length > 1.5f ? longDrawVolume : drawVolume;

        private void SetMusic(MusicContext context)
        {
            CurrentMusic = context;
            var track = (int)context < music.Length ? music[(int)context] : null;
            if (musicSource == null || desiredMusic == track && (musicFade != null || musicSource.isPlaying)) return;
            desiredMusic = track;
            if (musicFade != null) StopCoroutine(musicFade);
            musicFade = StartCoroutine(ChangeMusic(track));
        }
        private IEnumerator ChangeMusic(AudioClip track)
        {
            float duration = Mathf.Max(.01f, fadeSeconds);
            float from = musicSource.volume;
            if (musicSource.isPlaying)
                for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                { musicSource.volume = Mathf.Lerp(from, 0, elapsed / duration); yield return null; }
            musicSource.Stop(); musicSource.clip = track; musicSource.volume = 0;
            musicSource.loop = true; musicSource.playOnAwake = false; musicSource.spatialBlend = 0;
            if (track != null)
            {
                musicSource.Play();
                for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                { musicSource.volume = Mathf.Lerp(0, musicVolume, elapsed / duration); yield return null; }
                musicSource.volume = musicVolume;
            }
            musicFade = null;
        }

        public void PlayClick() { bindButtons = true; Play(click); }
        public void PlayDenied() => Play(denied);
        public void Play(AudioClip clip, float volume = 1)
        {
            if (Application.isPlaying && isActiveAndEnabled && clip != null && feedbackSource != null && feedbackSource.isActiveAndEnabled)
                feedbackSource.PlayOneShot(clip, Mathf.Clamp01(volume) * feedbackVolume * effectsVolume);
        }
        private static float SafeVolume(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp01(value);
        private void ApplyEffectsVolume()
        {
            if (host?.BattleView == null) return;
            var handObject = host.BattleView.Actions?.Hand;
            var targetHand = handObject != null ? handObject.GetComponent<HandZoneView>() : null;
            if (targetHand != null)
            {
                targetHand.SetDrawSound(targetHand.DrawSoundOverride, DrawVolume(targetHand.DrawSoundOverride) * effectsVolume, targetHand.DrawSoundEnabled);
                targetHand.SetDiscardSound(targetHand.DiscardSoundOverride, .65f * effectsVolume, targetHand.DiscardSoundEnabled);
                targetHand.SetSelectionSound(targetHand.SelectionSoundOverride, .7f * effectsVolume, targetHand.SelectionSoundEnabled);
                targetHand.SetDeselectionSound(targetHand.DeselectionSound, .7f * effectsVolume, targetHand.DeselectionSoundEnabled);
                targetHand.SetSelectionDeniedSound(targetHand.SelectionDeniedSound, .55f * effectsVolume, targetHand.SelectionDeniedSoundEnabled);
                targetHand.SetReflowSound(targetHand.ReflowSound, .5f * effectsVolume, targetHand.ReflowSoundEnabled);
            }
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var actor in root.GetComponentsInChildren<BattleActor>(true))
                {
                    if (actor.Battle != host.BattleView || !(actor.Presentation is BattleActorPresenter presenter) ||
                        !(presenter.Audio is BattleActorAudio audio)) continue;
                    ScaleCue(audio, BattlePresentationKind.Attack, .75f);
                    ScaleCue(audio, BattlePresentationKind.Hit, .65f);
                    ScaleCue(audio, BattlePresentationKind.Death, .6f);
                }
                foreach (var shield in root.GetComponentsInChildren<DiscardShieldPresenter>(true))
                { shield.BlockHitVolume = .5f * effectsVolume; shield.LandingVolume = .65f * effectsVolume; }
            }
        }
        private void ScaleCue(BattleActorAudio audio, BattlePresentationKind kind, float nominalVolume)
        {
            var settings = audio.GetSettings(kind); settings.Volume = nominalVolume * effectsVolume;
            var source = audio.GetSource(kind); if (source != null) source.volume = settings.Volume;
        }
        public static AdventureAudioController FindInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var audio = root.GetComponentInChildren<AdventureAudioController>(true);
                if (audio != null) return audio;
            }
            return null;
        }
        public static void BindButton(Button button, AdventureAudioController audio = null)
        {
            if (button == null) return;
            audio = audio != null ? audio : FindInScene(button.gameObject.scene);
            if (audio == null) return;
            var feedback = button.GetComponent<AdventureButtonAudio>();
            if (feedback == null) feedback = button.gameObject.AddComponent<AdventureButtonAudio>();
            feedback.Configure(audio, button);
        }
    }
}
