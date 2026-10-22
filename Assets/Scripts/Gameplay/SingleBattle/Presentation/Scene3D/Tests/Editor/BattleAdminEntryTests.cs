using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Poker.SingleBattle.Tests
{
    public sealed class BattleAdminEntryTests
    {
        [Test]
        public void LegacySceneRetainsAdministratorBindingsForShortcutMigration()
        {
            var scene = File.ReadAllText("Assets/Scenes/BattleDemo.unity");
            StringAssert.IsMatch(@"adminPanel: \{fileID: [1-9][0-9]*\}", scene);
            StringAssert.IsMatch(@"adminButton: \{fileID: [1-9][0-9]*\}", scene);
            Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(scene, "m_Name: Open Battle Administrator").Count);
            StringAssert.Contains("m_MethodName: OpenAdministrator", scene);
            StringAssert.Contains("m_SortingOrder: 100", scene);
        }

        // Verifies migration and opening without resetting an existing draft.
        [MenuItem("Tools/BattleDemo/Verify Administrator Entry")]
        public static void VerifyRunningEntry()
        {
            Assert.IsTrue(Application.isPlaying);
            var hud = Object.FindObjectOfType<SingleBattleHud>();
            Assert.NotNull(hud); Assert.NotNull(hud.Administrator);
            if (hud.AdministratorButton != null) Assert.IsFalse(hud.AdministratorButton.gameObject.activeSelf);
            bool wasOpen = hud.Administrator.IsOpen;
            hud.OpenAdministrator(); Assert.IsTrue(hud.Administrator.IsOpen);
            var draft = hud.Administrator.Draft;
            hud.OpenAdministrator(); Assert.AreSame(draft, hud.Administrator.Draft, "Opening again must preserve the draft.");
            if (!wasOpen) hud.Administrator.Close();
            Directory.CreateDirectory("Library/BattleRuleAlignment");
            File.WriteAllText("Library/BattleRuleAlignment/admin-entry.txt", "PASS: legacy button hidden; administrator opens; repeated opening preserves draft.");
        }
    }
}
