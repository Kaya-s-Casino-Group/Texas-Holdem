using UnityEngine;
using UnityEngine.Rendering;

namespace RetroRendering
{
    public sealed class PokerFrameRate : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            QualitySettings.vSyncCount = 0;
            OnDemandRendering.renderFrameInterval = 1;
            // Mobile builds use an explicit 60 FPS cap; -1 would restore Unity's 30 FPS default.
            Application.targetFrameRate = Application.isMobilePlatform ? 60 : -1;

#if DEVELOPMENT_BUILD
            Debug.Log($"POKER_FRAME_RATE: target={Application.targetFrameRate}, " +
                      $"displayHz={Screen.currentResolution.refreshRateRatio.value:F2}, " +
                      $"vSync={QualitySettings.vSyncCount}, renderInterval={OnDemandRendering.renderFrameInterval}");
            var probe = new GameObject("Frame Rate Startup Check").AddComponent<PokerFrameRate>();
            DontDestroyOnLoad(probe.gameObject);
#endif
        }

#if DEVELOPMENT_BUILD
        private System.Collections.IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(5f);
            var started = Time.realtimeSinceStartupAsDouble;
            var firstFrame = Time.frameCount;
            yield return new WaitForSecondsRealtime(5f);
            var elapsed = Time.realtimeSinceStartupAsDouble - started;
            Debug.Log($"POKER_FRAME_RATE_SAMPLE: fps={(Time.frameCount - firstFrame) / elapsed:F1}, " +
                      $"seconds={elapsed:F2}, target={Application.targetFrameRate}");
            Destroy(gameObject);
        }
#endif
    }
}
