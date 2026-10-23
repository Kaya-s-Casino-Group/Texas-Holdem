#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Poker.Adventure.Tests
{
    public sealed class AdventureStartupTests
    {
        private string copy;
        private float previousTimeScale;
        private bool previousRunInBackground;

        [UnityTest]
        public IEnumerator StartupWarmsRealComponentsWithoutStartingRunAndWorksWithPausedTime()
        {
            previousTimeScale = Time.timeScale;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            Time.timeScale = 0;
            copy = "Assets/Scenes/__StartupValidation_" + Guid.NewGuid().ToString("N") + ".unity";
            Assert.IsTrue(AssetDatabase.CopyAsset("Assets/Scenes/SingleChapterAdventure.unity", copy));
            File.WriteAllText(copy, File.ReadAllText(copy).Replace("saveAutomatically: 1", "saveAutomatically: 0"));
            AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(copy, new LoadSceneParameters(LoadSceneMode.Single));
            var host = Object.FindObjectOfType<AdventureSceneHost>();
            Assert.IsTrue(host.IsLoading);
            Assert.IsTrue(host.IsBusy);
            Assert.IsFalse(host.NewRun("fool", 42), "Loading must reject player commands.");
            var cover = Object.FindObjectOfType<AdventureLoadingScreen>();
            Assert.IsNotNull(cover);
            Assert.IsTrue(cover.GetComponentInChildren<Image>().raycastTarget);
            var dots = cover.GetComponentsInChildren<RectTransform>().Where(t => t.name.StartsWith("Dot ")).ToArray();
            Assert.AreEqual(3, dots.Length);
            var initial = dots.Select(t => t.anchoredPosition).ToArray();
            bool animated = false;
            Directory.CreateDirectory("Library/AdventureValidation");
            ScreenCapture.CaptureScreenshot("Library/AdventureValidation/startup-loading.png");
            float deadline = Time.realtimeSinceStartup + 30;
            while (host.IsLoading && Time.realtimeSinceStartup < deadline)
            {
                for (int i = 0; i < dots.Length; i++)
                    if (dots[i] != null && Vector2.Distance(initial[i], dots[i].anchoredPosition) > .1f) animated = true;
                yield return null;
            }
            Assert.IsFalse(host.IsLoading, "Startup must complete even with timeScale zero.");
            Assert.IsTrue(animated, "The dots must animate independently of game time.");
            yield return null;
            Assert.IsTrue(cover == null);
            Assert.IsNull(host.Adventure);
            Assert.IsNull(host.Session);
            Assert.IsNull(host.BattleView.Session, "Warmup must not start a standalone battle.");
            Assert.IsTrue(host.IsAtMenu);
            Assert.IsFalse(host.IsBusy);
            Assert.IsFalse(host.HasSavedRun);
            Assert.IsTrue(host.gameObject.scene.GetRootGameObjects().Any(root => root.name == "BattleDemo Enemy World UI"),
                "The real runtime enemy HUD should already have been created.");
            ScreenCapture.CaptureScreenshot("Library/AdventureValidation/startup-ready.png");
            yield return null;
            Time.timeScale = previousTimeScale;
            Assert.IsTrue(host.NewRun("fool", 42), host.Message);
            Assert.IsTrue(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, "battle-1")), host.Message);
            Assert.IsNotNull(host.BattleView.Session);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = previousTimeScale;
            Application.runInBackground = previousRunInBackground;
            if (string.IsNullOrEmpty(copy)) yield break;
            var scene = SceneManager.GetSceneByPath(copy);
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("Startup validation cleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            AssetDatabase.DeleteAsset(copy);
        }
    }
}
#endif
