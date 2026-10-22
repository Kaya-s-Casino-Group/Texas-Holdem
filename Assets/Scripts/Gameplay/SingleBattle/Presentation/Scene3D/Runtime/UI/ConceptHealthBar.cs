using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    [DisallowMultipleComponent]
    public sealed class ConceptHealthBar : MonoBehaviour
    {
        private string role;
        private Image track, fill, frame;
        public Image Fill => fill;
        public string Role => role;
        public void Present(string value, float fraction)
        {
            if (role != value || frame == null)
            {
                role = value;
                track = ConceptBattleSkin.Art(transform, "Concept Track", "HealthBars/" + role + "/track", new Rect(0, 0, 1, 1), false);
                fill = ConceptBattleSkin.Art(transform, "Concept Fill", "HealthBars/" + role + "/fill", new Rect(0, 0, 1, 1), false);
                frame = ConceptBattleSkin.Art(transform, "Concept Frame", "HealthBars/" + role + "/frame", new Rect(0, 0, 1, 1), false);
                fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; frame.transform.SetAsLastSibling();
            }
            var old = GetComponent<Image>(); if (old != null) old.enabled = false;
            ConceptBattleSkin.HideLegacy(transform);
            foreach (var name in new[] { "Fill", "Health Fill" }) { var legacy = transform.Find(name); if (legacy != null) legacy.gameObject.SetActive(false); }
            ConceptBattleSkin.Fit((RectTransform)transform, frame.sprite);
            Rect slot = role == "player" ? new Rect(124f / 2132, 71f / 209, 1885f / 2132, 67f / 209) :
                role == "boss" ? new Rect(189f / 2187, 118f / 327, 1808f / 2187, 82f / 327) : new Rect(138f / 2110, 58f / 191, 1834f / 2110, 74f / 191);
            ConceptBattleSkin.Place(track.transform, slot); ConceptBattleSkin.Place(fill.transform, slot);
            fill.fillAmount = Mathf.Clamp01(fraction);
            var label = transform.Find("Health Value"); if (label != null) label.SetAsLastSibling();
        }
    }
}
