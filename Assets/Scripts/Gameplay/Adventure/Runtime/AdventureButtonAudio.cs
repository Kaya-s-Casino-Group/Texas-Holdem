using UnityEngine;
using UnityEngine.UI;

namespace Poker.Adventure
{
    /// <summary>Uses the button's accepted click, including keyboard and gamepad submit.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class AdventureButtonAudio : MonoBehaviour
    {
        [SerializeField] private AdventureAudioController audioController;
        private Button button;

        public void Configure(AdventureAudioController audio, Button target)
        {
            if (button != null) button.onClick.RemoveListener(Click);
            audioController = audio; button = target;
            if (Application.isPlaying && isActiveAndEnabled && button != null) button.onClick.AddListener(Click);
        }
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            button = GetComponent<Button>(); button.onClick.RemoveListener(Click); button.onClick.AddListener(Click);
        }
        private void OnDisable() { if (button != null) button.onClick.RemoveListener(Click); }
        private void Click()
        {
            if (audioController == null) audioController = AdventureAudioController.FindInScene(gameObject.scene);
            if (audioController != null) audioController.PlayClick();
        }
    }
}
