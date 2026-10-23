using System;
using System.Collections;
using Poker.SingleBattle;
using UnityEngine;

namespace Poker.Adventure
{
    public sealed partial class AdventureSceneHost
    {
        public bool IsLoading { get; private set; }
        private AdventureLoadingScreen loadingScreen;
        private AdventureScreen startupScreen;
        private bool startupUiReady;

        private bool BeginStartup()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var screen in root.GetComponentsInChildren<AdventureScreen>(true))
                if (screen.Host == this && screen.enabled && screen.gameObject.activeInHierarchy) startupScreen = screen;
            // Headless hosts retain their synchronous initialization contract.
            if (startupScreen == null) return false;
            IsLoading = true;
            loadingScreen = AdventureLoadingScreen.Create(transform, startupScreen.Font);
            // Prevent a battle's Start from creating a standalone session before warmup.
            if (battleView != null) battleView.SceneHost.Configure(battleView, battleView, false, true);
            ShowBattle(false);
            return true;
        }

        private IEnumerator Start()
        {
            if (!IsLoading) yield break;
            var steps = Warmup();
            try
            {
                while (true)
                {
                    object next;
                    try
                    {
                        if (!steps.MoveNext()) break;
                        next = steps.Current;
                    }
                    catch (Exception error)
                    {
                        Debug.LogException(error, this);
                        Report("配置载入失败：" + error.Message);
                        break;
                    }
                    yield return next;
                }
            }
            finally
            {
                (steps as IDisposable)?.Dispose();
                ShowBattle(false);
                IsLoading = false;
                if (loadingScreen != null)
                {
                    loadingScreen.gameObject.SetActive(false);
                    Destroy(loadingScreen.gameObject);
                }
                if (!startupUiReady) Changed?.Invoke();
            }
        }

        private IEnumerator Warmup()
        {
            float started = Time.realtimeSinceStartup;
            // Start may run before the first render; allow the cover to actually reach the screen.
            yield return null;
            yield return null;
            Initialize();
            yield return null;

            // Load into the same cache used by subsequent page builds, one request at a time.
            foreach (var key in new[] { "ContentButton", "ContentPanel", "MenuBackground", "MenuLogo", "MapBackground", "ShopBackground", "RestBackground" })
            {
                yield return Resources.LoadAsync<Sprite>("DeskUI/" + key);
                DeskUISkin.Get(key);
                yield return null;
            }

            // Run Awake/OnEnable/Start on the real components, retaining their generated HUDs.
            // No session is attached and the adventure remains at its menu throughout.
            foreach (var root in battleRoots)
            {
                if (root == null) continue;
                root.SetActive(true);
                yield return null;
            }
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            ShowBattle(false);

            IsLoading = false;
            try { Changed?.Invoke(); startupUiReady = true; }
            finally { IsLoading = true; }
            Canvas.ForceUpdateCanvases();
            yield return null;
            // A brief minimum avoids a one-frame flash; long loads remain covered until finished.
            while (Time.realtimeSinceStartup - started < .65f) yield return null;
        }
    }
}
