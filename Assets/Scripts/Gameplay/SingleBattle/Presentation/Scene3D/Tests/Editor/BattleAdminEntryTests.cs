using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Poker.SingleBattle.Tests
{
    public sealed class BattleAdminEntryTests
    {
        [Test]
        public void SavedSceneHasOneFixedEntryWithPersistentClickAndRaycaster()
        {
            var scene = File.ReadAllText("Assets/Scenes/BattleDemo.unity");
            StringAssert.IsMatch(@"adminPanel: \{fileID: [1-9][0-9]*\}", scene);
            StringAssert.IsMatch(@"adminButton: \{fileID: [1-9][0-9]*\}", scene);
            Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(scene, "m_Name: Open Battle Administrator").Count);
            StringAssert.Contains("m_MethodName: OpenAdministrator", scene);
            StringAssert.Contains("m_SortingOrder: 100", scene);
        }

        // Checks the actual scene's global pointer hit order, then invokes its saved button event.
        [MenuItem("Tools/BattleDemo/Verify Fixed Administrator Entry")]
        public static void VerifyRunningEntry()
        {
            Assert.IsTrue(Application.isPlaying);
            var hud = Object.FindObjectOfType<SingleBattleHud>(); var button = hud.AdministratorButton;
            Assert.NotNull(button); Assert.NotNull(hud.Administrator);
            Assert.AreEqual(1, button.onClick.GetPersistentEventCount());
            Assert.AreSame(hud, button.onClick.GetPersistentTarget(0));
            Assert.AreEqual(nameof(SingleBattleHud.OpenAdministrator), button.onClick.GetPersistentMethodName(0));
            Assert.IsTrue(button.GetComponent<GraphicRaycaster>().enabled);
            Assert.Greater(button.GetComponent<Canvas>().sortingOrder, hud.GetComponent<Canvas>().sortingOrder);
            hud.Administrator.Close(); Canvas.ForceUpdateCanvases();
            var rect = button.GetComponent<RectTransform>();
            var position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var hits = new List<RaycastResult>();
            Assert.NotNull(EventSystem.current);
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.IsNotEmpty(hits); Assert.AreEqual(button.gameObject, hits[0].gameObject, "The fixed button must be the first pointer hit.");
            button.onClick.Invoke(); Assert.IsTrue(hud.Administrator.IsOpen, "The saved click must open the runtime panel.");
            Directory.CreateDirectory("Library/BattleRuleAlignment");
            File.WriteAllText("Library/BattleRuleAlignment/fixed-admin-entry.txt", "PASS: saved button is first raycast hit; persistent click opens panel; no runtime entry creation.");
        }
    }
}
