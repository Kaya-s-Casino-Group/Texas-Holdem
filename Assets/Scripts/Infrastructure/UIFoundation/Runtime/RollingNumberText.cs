using System;
using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace UIFoundation
{
    /// <summary>Reusable numeric label. Receives numbers and owns only formatting and visual interpolation.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class RollingNumberText : MonoBehaviour
    {
        [SerializeField, Min(.01f)] private float durationSeconds = .25f;
        [SerializeField] private string numberFormat = "N0";
        private readonly RollingNumberValue value = new RollingNumberValue();
        private readonly object tweenId = new object();
        private TMP_Text label;
        private Tween tween;
        private float progress;
        public decimal CurrentValue => value.Current;
        public decimal TargetValue => value.Target;
        public bool IsAnimating => tween != null && tween.IsActive();
        public string DisplayText => Text.text;
        private TMP_Text Text => label != null ? label : label = GetComponent<TMP_Text>();

        public void Configure(string format, float seconds = .25f)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < .01f) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (string.IsNullOrEmpty(format)) throw new ArgumentException("Provide a numeric format.", nameof(format));
            0m.ToString(format, CultureInfo.InvariantCulture);
            durationSeconds = seconds; numberFormat = format; Render();
        }
        public void SetTarget(decimal target)
        {
            if (!value.Retarget(target)) return;
            KillTween();
            if (!isActiveAndEnabled) { SnapTo(target); return; }
            progress = 0;
            tween = DOTween.To(() => progress, next => {
                progress = next; value.SetProgress(next); Render();
            }, 1, durationSeconds).SetEase(Ease.OutCubic).SetUpdate(UpdateType.Manual)
                .SetAutoKill(true).SetRecyclable(false).SetId(tweenId)
                .OnComplete(() => { tween = null; value.SnapTo(value.Target); Render(); });
            tween.ForceInit();
        }
        public void SnapTo(decimal target) { KillTween(); value.SnapTo(target); Render(); }
        public void Advance(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (IsAnimating) tween.ManualUpdate(seconds, seconds);
        }
        private void Render() { if (Text != null) Text.text = value.Current.ToString(numberFormat, CultureInfo.InvariantCulture); }
        private void KillTween() { if (tween != null) DOTween.Kill(tweenId); tween = null; }
        private void Update() => Advance(Time.unscaledDeltaTime);
        private void OnEnable() => Render();
        private void OnDisable() => SnapTo(value.Target);
        private void OnDestroy() => KillTween();
    }
}
