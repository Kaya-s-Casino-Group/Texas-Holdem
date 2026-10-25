#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using CardPresentation;
using NUnit.Framework;
using Poker.SingleBattle;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Poker.Adventure.Tests
{
    public sealed partial class SingleChapterAdventureSceneTests
    {
        private IEnumerator InformationHand(params string[] cards)
        {
            var view = host.BattleView;
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(zones: new[] {
                new BattleAdminZone(host.Adventure.PlayerId, CardZoneKinds.Hand, cards.Select(id => new BattleAdminCard(definitionId: id))) })).Accepted);
            yield return Idle();
            var hand = view.Actions.Hand;
            foreach (var card in hand.Cards.ToArray()) hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
            hand.Publish(); hand.GetComponent<HandZoneView>().CompleteTransitions(); view.RefreshCandidate(); yield return null;
        }

        private static bool InformationTextIsRendered(TMP_Text text)
        {
            if (!text.isActiveAndEnabled || text.color.a < .75f ||
                text.GetComponentsInParent<CanvasGroup>().Any(group => group.alpha < .75f)) return false;
            text.ForceMeshUpdate();
            var canvas = text.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            bool visible = false;
            foreach (var glyph in text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(glyph => glyph.isVisible))
            {
                visible = true;
                foreach (var corner in new[] { glyph.bottomLeft, glyph.topRight })
                {
                    var point = RectTransformUtility.WorldToScreenPoint(camera, text.transform.TransformPoint(corner));
                    if (point.x < 0 || point.y < 0 || point.x > Screen.width || point.y > Screen.height) return false;
                }
            }
            return visible;
        }
    }
}
#endif
