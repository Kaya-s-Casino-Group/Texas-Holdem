using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace CardPresentation
{
    internal static class CardMobileHaptics
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CardZoneLightImpact();
#endif
        internal static void Play()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                if (activity == null) return;
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() => {
                    try
                    {
                        // Resolve again on the UI thread; the outer activity wrapper has been disposed by then.
                        using (var uiPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        using (var uiActivity = uiPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                        {
                            if (uiActivity == null) return;
                            using (var window = uiActivity.Call<AndroidJavaObject>("getWindow"))
                            using (var view = window.Call<AndroidJavaObject>("getDecorView"))
                                view.Call<bool>("performHapticFeedback", 3); // KEYBOARD_TAP; respects device/user settings.
                        }
                    }
                    catch (AndroidJavaException) { } // Activity may disappear while a queued callback waits.
                }));
            }
#elif UNITY_IOS && !UNITY_EDITOR
            CardZoneLightImpact();
#endif
        }
    }
}
