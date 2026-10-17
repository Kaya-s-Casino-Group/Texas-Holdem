using System;
using UnityEngine;

namespace CardPresentation
{
    public partial class HandZoneView
    {
        [SerializeField, Min(1), InspectorName("最多选中卡牌数")] private int maxSelectedCards = 5;
        [Header("无法选中反馈")]
        [SerializeField] private Vector3 selectionDeniedOffset = new Vector3(0, 0, .004f);
        [SerializeField, Min(.01f)] private float selectionDeniedRiseSeconds = .08f;
        [SerializeField, Min(.01f)] private float selectionDeniedReturnSeconds = .18f;
        public int MaxSelectedCards => maxSelectedCards;
        public Vector3 SelectionDeniedOffset => selectionDeniedOffset;
        public float SelectionDeniedRiseSeconds => selectionDeniedRiseSeconds;
        public float SelectionDeniedReturnSeconds => selectionDeniedReturnSeconds;
        public event Action<CardInstanceView> CardSelectionDenied;

        public void SetMaxSelectedCards(int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            maxSelectedCards = count;
        }
        public void SetSelectionDeniedAnimation(Vector3 offset, float riseSeconds = .08f, float returnSeconds = .18f)
        {
            ValidateSelectionDeniedAnimation(offset, riseSeconds, returnSeconds);
            selectionDeniedOffset = offset; selectionDeniedRiseSeconds = riseSeconds; selectionDeniedReturnSeconds = returnSeconds;
        }
        private static void ValidateSelectionDeniedAnimation(Vector3 offset, float riseSeconds, float returnSeconds)
        {
            ValidateOffset(offset);
            if (!CardZoneRowLayout.Finite(riseSeconds) || !CardZoneRowLayout.Finite(returnSeconds) || riseSeconds < .01f || returnSeconds < .01f)
                throw new ArgumentException("无法选中反馈的上升/回弹时间须为至少 0.01 秒的有限值。");
        }
        private void ValidateSelectionSettings()
        {
            if (maxSelectedCards < 1) throw new ArgumentOutOfRangeException(nameof(maxSelectedCards));
            ValidateSelectionDeniedAnimation(selectionDeniedOffset, selectionDeniedRiseSeconds, selectionDeniedReturnSeconds);
        }
        /// <summary>One feedback path for every refused selection. Never changes the card's interaction state.</summary>
        public bool PlaySelectionDenied(string viewId)
        {
            if (!isActiveAndEnabled || !TryGetView(viewId, out var view) || !view.isActiveAndEnabled || !view.IsTransitionVisible) return false;
            ValidateSelectionSettings();
            view.BeginSelectionDenied(selectionDeniedOffset, selectionDeniedRiseSeconds, selectionDeniedReturnSeconds);
            PlayCardSound(ref selectionDeniedAudioSource, selectionDeniedSound, selectionDeniedSoundVolume, selectionDeniedSoundEnabled, "Selection Denied Audio");
            CardSelectionDenied?.Invoke(view);
            return true;
        }
    }
}
