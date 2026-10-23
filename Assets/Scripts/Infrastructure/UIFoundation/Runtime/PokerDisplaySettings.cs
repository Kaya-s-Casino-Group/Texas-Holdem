using UnityEngine;

namespace UIFoundation
{
    /// <summary>Desktop remembers the selected window size; mobile keeps its native landscape display.</summary>
    public static class PokerDisplaySettings
    {
        public const string PreferenceKey = "Poker.Display.ResolutionPreset";
        private static readonly Vector2Int[] Presets =
        {
            new Vector2Int(2340, 1080),
            new Vector2Int(1920, 1080),
            new Vector2Int(1560, 720),
            new Vector2Int(1280, 720)
        };

        public static bool SupportsDesktopResolution => !Application.isMobilePlatform;
        public static int PresetCount => Presets.Length;
        public static int SelectedPreset
        {
            get
            {
                var index = PlayerPrefs.GetInt(PreferenceKey, 0);
                return index >= 0 && index < Presets.Length ? index : 0;
            }
        }
        public static Vector2Int GetResolution(int index) => Presets[index];

        public static void SelectPreset(int index)
        {
            if (index < 0 || index >= Presets.Length)
                throw new System.ArgumentOutOfRangeException(nameof(index));
            if (!SupportsDesktopResolution) return;
            PlayerPrefs.SetInt(PreferenceKey, index);
            PlayerPrefs.Save();
            ApplyDesktopResolution();
        }

        private static void ApplyDesktopResolution()
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            var size = Presets[SelectedPreset];
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            ApplyDesktopResolution();
#elif (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
#endif
        }
    }
}
