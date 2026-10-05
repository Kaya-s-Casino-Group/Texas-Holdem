using System.IO;
using UnityEditor;
using UnityEngine;

namespace CardPresentation.Editor
{
    public sealed partial class CardZoneValidationWindow
    {
        [SerializeField] private GameObject lastExportedZonePrefab;

        private void DrawPrefabExport()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool changingMode = EditorApplication.isPlayingOrWillChangePlaymode != Application.isPlaying;
                using (new EditorGUI.DisabledScope(target == null || target.CardPrefab == null || changingMode))
                    if (GUILayout.Button(new GUIContent("导出 View Prefab…",
                        "保存当前牌区实际子类的展示设置。保留布局、动画、音效和外观；不包含测试卡牌、输入宿主或临时视觉。Play 中也可导出当前调好的参数。")))
                        Run(ExportViewPrefab);
                if (lastExportedZonePrefab != null && GUILayout.Button("定位 Prefab", GUILayout.Width(90)))
                    EditorGUIUtility.PingObject(lastExportedZonePrefab);
            }
        }

        private void ExportViewPrefab()
        {
            string folder = CardZoneViewPrefabExporter.DefaultFolder;
            string suggested = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + target.GetType().Name + ".prefab");
            string path = EditorUtility.SaveFilePanelInProject("导出牌区 View 设置", Path.GetFileNameWithoutExtension(suggested),
                "prefab", "保存可复用的牌区展示配置，不包含测试卡牌列表。", folder);
            if (string.IsNullOrEmpty(path)) return;
            bool exists = AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path);
            if (exists && !EditorUtility.DisplayDialog("覆盖牌区 Prefab", "将替换 " + path +
                "\n新 Prefab 只包含当前牌区的展示设置，不包含原 Prefab 的其他组件和子对象。", "覆盖", "取消")) return;
            lastExportedZonePrefab = CardZoneViewPrefabExporter.Export(target, path, exists);
            EditorGUIUtility.PingObject(lastExportedZonePrefab);
            status = "已导出：" + path + "。锚点已转换为局部位置，相机使用 MainCamera；卡牌数据由运行时宿主提供。";
        }
    }
}
