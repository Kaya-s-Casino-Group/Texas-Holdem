#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CardPresentation;
using NUnit.Framework;
using Poker.SingleBattle;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Poker.Adventure.Tests
{
    public sealed partial class SingleChapterAdventureSceneTests
    {
        private static PointerEventData EnterEnemyBadgeThroughRaycast(BattleDemoEnemyBadgeTooltip badge, bool hoverText = false)
        {
            Canvas.ForceUpdateCanvases();
            var canvas = badge.GetComponentInParent<Canvas>(); var raycaster = canvas.GetComponent<GraphicRaycaster>();
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode); Assert.IsNull(raycaster.eventCamera);
            var rect = badge.transform.Find(hoverText ? "Value" : "Concept Icon") as RectTransform ?? badge.GetComponent<RectTransform>();
            var point = RectTransformUtility.WorldToScreenPoint(raycaster.eventCamera, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = point }; var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits, "No raycast at the displayed enemy badge icon " + point);
            var entered = ExecuteEvents.GetEventHandler<IPointerEnterHandler>(hits[0].gameObject);
            Assert.AreEqual(badge.gameObject, entered, "Enemy badge at " + point + " is blocked by " +
                string.Join(" | ", hits.Take(6).Select(hit => hit.gameObject.name + " order=" + hit.sortingOrder + " camera=" + (hit.module.eventCamera == null ? "overlay" : hit.module.eventCamera.name))));
            pointer.pointerCurrentRaycast = hits[0]; ExecuteEvents.Execute(entered, pointer, ExecuteEvents.pointerEnterHandler);
            return pointer;
        }

    }
}
#endif
