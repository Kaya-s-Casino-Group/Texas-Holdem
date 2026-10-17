using System;
using UnityEditor;
using UnityEngine;

namespace RetroRendering.Editor
{
    public sealed class VolumeNoiseGeneratorWindow : EditorWindow
    {
        [SerializeField] VolumeNoiseGenerator.Settings settings = new VolumeNoiseGenerator.Settings();
        [SerializeField] string outputPath = VolumeNoiseGenerator.DefaultPath;
        [SerializeField] int slice = 16;
        Texture3D texture;
        Texture2D preview;
        string error;

        [MenuItem("工具/渲染/三维噪声/噪声生成器")]
        public static void Open() => GetWindow<VolumeNoiseGeneratorWindow>("3D 噪声生成器");

        void OnEnable() { minSize = new Vector2(360, 520); Load(); }
        void OnDisable() { if (preview != null) DestroyImmediate(preview); }

        void Load()
        {
            texture = AssetDatabase.LoadAssetAtPath<Texture3D>(outputPath);
            if (texture != null)
            {
                var json = AssetImporter.GetAtPath(outputPath).userData;
                if (!string.IsNullOrEmpty(json))
                {
                    try { JsonUtility.FromJsonOverwrite(json, settings); }
                    catch (ArgumentException) { /* Assets without generator metadata can still be previewed. */ }
                }
            }
            UpdatePreview();
        }

        void UpdatePreview()
        {
            if (preview != null) DestroyImmediate(preview);
            preview = texture != null && texture.isReadable && texture.format == TextureFormat.R8 &&
                texture.width == 32 && texture.height == 32 && texture.depth == 32
                ? VolumeNoiseGenerator.Slice(texture, slice) : null;
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("离线烘焙可平铺的 32×32×32 噪声。R8 单通道、线性数据、无 mip，原始体素共 32 KiB。多层噪声合成到同一张纹理。", MessageType.Info);
            settings.seed = EditorGUILayout.IntField("随机种子", settings.seed);
            settings.baseCells = EditorGUILayout.IntSlider("基础格子数", settings.baseCells, 2, 8);
            int maxOctaves = Mathf.Min(4, 1 + Mathf.FloorToInt(Mathf.Log(16f / settings.baseCells, 2)));
            settings.octaves = EditorGUILayout.IntSlider("噪声层数", Mathf.Min(settings.octaves, maxOctaves), 1, maxOctaves);
            settings.persistence = EditorGUILayout.Slider("每层强度比例", settings.persistence, 0, 1);
            EditorGUILayout.LabelField("保存位置", outputPath, EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("选择输出资产…"))
            {
                var path = EditorUtility.SaveFilePanelInProject("噪声 Texture3D", "FogNoise32", "asset", "选择生成或更新的噪声资产");
                if (!string.IsNullOrEmpty(path)) { outputPath = path; Load(); }
            }
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                if (GUILayout.Button("生成 / 更新 32³ 噪声", GUILayout.Height(30)))
                {
                    try
                    {
                        texture = VolumeNoiseGenerator.Save(outputPath, settings); error = null;
                        UpdatePreview(); EditorGUIUtility.PingObject(texture);
                    }
                    catch (Exception e) { error = e.Message; }
                }
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("已保存资产的 XY 切片", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck(); slice = EditorGUILayout.IntSlider("Z 切片", slice, 0, 31);
            if (EditorGUI.EndChangeCheck()) UpdatePreview();
            if (preview != null)
            {
                var rect = GUILayoutUtility.GetAspectRect(1, GUILayout.MaxHeight(300));
                EditorGUI.DrawPreviewTexture(rect, preview, null, ScaleMode.ScaleToFit);
                if (GUILayout.Button("选中 Texture3D")) Selection.activeObject = texture;
            }
            else EditorGUILayout.HelpBox("生成后在这里查看切片；修改参数后需再次生成。", MessageType.None);
        }
    }
}
