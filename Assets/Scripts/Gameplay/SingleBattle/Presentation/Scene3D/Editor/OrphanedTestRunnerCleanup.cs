using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Poker.SingleBattle.Editor
{
    /// <summary>
    /// Test Framework 1.3 can leave DontSave runners outside every scene after its
    /// PlayMode teardown throws. Their enabled cameras otherwise cover the Game view.
    /// Keep this recovery editor-only and never touch runners in a valid scene.
    /// </summary>
    [InitializeOnLoad]
    public static class OrphanedTestRunnerCleanup
    {
        private const string ControllerType = "UnityEngine.TestTools.TestRunner.PlaymodeTestsController";

        static OrphanedTestRunnerCleanup()
        {
            EditorApplication.delayCall += CleanupWhenIdle;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += CleanupWhenIdle;
            };
        }

        [MenuItem("Tools/BattleDemo/Clean Orphaned Test Runners")]
        public static void CleanupWhenIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            var runners = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                .Where(component => component != null && component.GetType().FullName == ControllerType)
                .Select(component => component.gameObject).Distinct().ToArray();
            int removed = 0;
            foreach (var runner in runners)
            {
                if (EditorUtility.IsPersistent(runner) || runner.scene.IsValid() ||
                    runner.name != "Code-based tests runner" ||
                    (runner.hideFlags & HideFlags.DontSave) != HideFlags.DontSave) continue;
                Object.DestroyImmediate(runner);
                removed++;
            }
            if (removed > 0) Debug.Log($"已清理 {removed} 个脱离场景的测试运行器及其相机。");
        }
    }
}
