using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class HarborGameViewCapture
{
    [MenuItem("Tools/Harbor/Capture Current Game View")]
    public static void Capture()
    {
        var type = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        var window = EditorWindow.GetWindow(type);
        window.Repaint();
        EditorApplication.delayCall += () =>
        {
            RenderTexture source = null;
            for (var t = type; t != null && source == null; t = t.BaseType)
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (typeof(RenderTexture).IsAssignableFrom(field.FieldType))
                    {
                        var candidate = field.GetValue(window) as RenderTexture;
                        if (candidate != null && candidate.width > 0) { source = candidate; break; }
                    }
            if (source == null) throw new InvalidOperationException("Game View render texture is unavailable.");
            var previous = RenderTexture.active;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = source;
                image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    var pixels = image.GetPixels32();
                    var corrected = new Color32[pixels.Length];
                    for (int y = 0; y < source.height; y++)
                        Array.Copy(pixels, y * source.width, corrected, (source.height - 1 - y) * source.width, source.width);
                    image.SetPixels32(corrected);
                }
                image.Apply();
                Directory.CreateDirectory("output/harbor-enemies");
                File.WriteAllBytes("output/harbor-enemies/game-view.png", image.EncodeToPNG());
                Debug.Log($"Harbor Game View captured: {source.width}x{source.height}");
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
        };
    }
}
