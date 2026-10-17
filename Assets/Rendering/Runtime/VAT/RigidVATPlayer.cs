using UnityEngine;

namespace RetroRendering
{
    /// <summary>One non-looping VAT timeline for all material slots of a renderer.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
    public sealed class RigidVATPlayer : MonoBehaviour
    {
        [SerializeField, Range(0, 1)] float progress;
        [SerializeField, Min(.001f)] float durationSeconds = 109f / 30f;
        [SerializeField] bool playOnStart;
        MeshRenderer target;
        MaterialPropertyBlock block;
        float appliedProgress = float.NaN;
        bool playing;
        static readonly int ProgressId = Shader.PropertyToID("_VATProgress");
        public float Progress => progress;
        public bool IsPlaying => playing;

        public void Configure(float duration) { durationSeconds = Mathf.Max(.001f, duration); Apply(); }
        public void SetProgress(float value) { progress = Mathf.Clamp01(value); Apply(); }
        [ContextMenu("Play Collapse (Play Mode)")]
        public void Play() { SetProgress(0); playing = true; }
        [ContextMenu("Pause Collapse")]
        public void Pause() => playing = false;
        [ContextMenu("Reset Pose")]
        public void ResetPose() { playing = false; SetProgress(0); }

        void OnEnable() => Apply();
        void Start() { if (Application.isPlaying && playOnStart) Play(); }
        void OnValidate() { progress = Mathf.Clamp01(progress); durationSeconds = Mathf.Max(.001f, durationSeconds); appliedProgress = float.NaN; }
        void Update()
        {
            // Edit-mode preview is controlled by the slider/menu, not wall-clock time.
            if (Application.isPlaying && playing)
            {
                progress = Mathf.Min(1, progress + Time.deltaTime / durationSeconds);
                if (progress >= 1) playing = false;
            }
            if (progress != appliedProgress) Apply();
        }
        void Apply()
        {
            if (target == null) target = GetComponent<MeshRenderer>();
            if (target == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetFloat(ProgressId, progress);
            target.SetPropertyBlock(block); // Applies the same time to exterior and caps.
            appliedProgress = progress;
        }
    }
}
