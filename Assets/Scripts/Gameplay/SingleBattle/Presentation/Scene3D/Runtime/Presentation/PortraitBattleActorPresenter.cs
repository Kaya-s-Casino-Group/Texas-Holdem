using System;
using global::BattlePresentation;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Phase portraits consume neutral display facts through the actor framework.
    /// The artwork child slides independently of the parent's attack/hit/death animation.</summary>
    public sealed class PortraitBattleActorPresenter : BattleActorPresenter
    {
        [SerializeField] private MeshRenderer artwork;
        [SerializeField] private Texture2D idle, discard, action, guard;
        [SerializeField, Min(.01f)] private float fadeOutSeconds = .12f, fadeInSeconds = .18f;
        [SerializeField, Range(0, .2f)] private float rightShift = .055f;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap"), MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock properties;
        private Vector3 restPosition;
        private Color tint = Color.white;
        private Texture2D currentTexture, targetTexture;
        private string targetPose = "idle";
        private bool initialized;
        private int leg;
        private float elapsed, fromAlpha, fromShift, shift;
        public string Pose => targetPose;
        public Texture2D DisplayedTexture => currentTexture;
        public float Opacity { get; private set; } = 1;
        public bool IsChangingPose => leg != 0;

        public void ConfigurePortrait(MeshRenderer surface, Texture2D neutral, Texture2D selection,
            Texture2D playing, Texture2D ending)
        {
            if (surface == null || !surface.transform.IsChildOf(transform) || surface.transform == Model || neutral == null)
                throw new ArgumentException("Bind a separate artwork child and its default portrait.");
            artwork = surface; idle = neutral; discard = selection; action = playing; guard = ending;
            restPosition = artwork.transform.localPosition;
            tint = artwork.sharedMaterial != null && artwork.sharedMaterial.HasProperty(BaseColor)
                ? artwork.sharedMaterial.GetColor(BaseColor) : Color.white;
            ResetPresentation();
        }

        public override void SynchronizeDisplay(UnitDisplayData unit)
        {
            if (unit == null) return;
            if (!unit.IsAlive) { CancelPresentation(); return; }
            if (unit.Labels.TryGetValue("portraitPose", out var pose))
                PresentPose(pose, Application.isPlaying);
        }

        /// <summary>Visual-only entry point, also used by editor previews. No rule/session access.</summary>
        public void PresentPose(string pose, bool animate = true)
        {
            if (artwork == null) return;
            var texture = pose == "discard" ? discard : pose == "action" ? action : pose == "guard" ? guard : idle;
            texture = texture != null ? texture : idle;
            if (texture == null) return;
            if (initialized && pose == targetPose && texture == targetTexture) return;
            targetPose = pose; targetTexture = texture;
            if (!initialized || !animate || !isActiveAndEnabled || currentTexture == texture)
            {
                initialized = true; leg = 0; SetVisual(texture, 1, 0); return;
            }
            // Retarget from the visible alpha/position, never restart at full opacity.
            elapsed = 0; fromAlpha = Opacity; fromShift = shift; leg = 1;
        }

        public void Advance(float seconds)
        {
            if (leg == 0 || artwork == null || seconds <= 0) return;
            float remaining = seconds;
            while (leg != 0 && remaining > 0)
            {
                float duration = Mathf.Max(.01f, leg == 1 ? fadeOutSeconds : fadeInSeconds);
                float step = Mathf.Min(remaining, duration - elapsed);
                elapsed += step; remaining -= step;
                if (duration - elapsed < .000001f) elapsed = duration;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3 - 2 * t);
                if (leg == 1) SetVisual(currentTexture, Mathf.Lerp(fromAlpha, 0, eased), Mathf.Lerp(fromShift, rightShift, eased));
                else SetVisual(targetTexture, eased, Mathf.Lerp(-rightShift * .5f, 0, eased));
                if (t < 1) break;
                if (leg == 1)
                {
                    leg = 2; elapsed = 0;
                    SetVisual(targetTexture, 0, -rightShift * .5f);
                }
                else { leg = 0; SetVisual(targetTexture, 1, 0); }
            }
        }

        private void SetVisual(Texture2D texture, float alpha, float offset)
        {
            currentTexture = texture; Opacity = alpha; shift = offset;
            if (artwork == null) return;
            // Keep the full 0..1 UV quad at the texture's actual aspect for every phase portrait.
            GetComponentInParent<BattleBillboardLayout>()?.SetArtworkAspect((float)texture.width / texture.height);
            properties = properties ?? new MaterialPropertyBlock();
            artwork.GetPropertyBlock(properties);
            properties.SetTexture(BaseMap, texture); properties.SetTexture(MainTex, texture);
            var color = tint; color.a *= alpha;
            properties.SetColor(BaseColor, color); properties.SetColor(ColorId, color);
            artwork.SetPropertyBlock(properties);
            artwork.transform.localPosition = restPosition + Vector3.right * offset;
        }

        private void Update() => Advance(Time.unscaledDeltaTime);
        public override void CancelPresentation()
        {
            base.CancelPresentation(); leg = 0;
            if (targetTexture != null) SetVisual(targetTexture, 1, 0);
        }
        public override void ResetPresentation()
        {
            base.ResetPresentation();
            initialized = false; targetPose = "idle"; targetTexture = idle;
            if (idle != null) SetVisual(idle, 1, 0);
        }
        private void Awake()
        {
            if (artwork == null) return;
            restPosition = artwork.transform.localPosition;
            tint = artwork.sharedMaterial != null && artwork.sharedMaterial.HasProperty(BaseColor)
                ? artwork.sharedMaterial.GetColor(BaseColor) : Color.white;
        }
    }
}
