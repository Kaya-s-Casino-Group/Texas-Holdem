using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CardArt.Editor
{
    public sealed partial class CardAtlasWindow
    {
        [SerializeField] CardPrefabExportSettings prefabSettings = new CardPrefabExportSettings();
        [SerializeField] Vector2 prefabScroll;
        CardPrefabCheck prefabCheck;
        string prefabError, prefabStatus;
        GameObject lastExportedPrefab;

        [MenuItem("Tools/Cards/Prefab Exporter")]
        public static void OpenPrefabExporter()
        {
            var window = GetWindow<CardAtlasWindow>("卡牌工具");
            window.ConfigureWindowSize();
            window.tab = 2;
            window.Show();
        }

        void InitializePrefabExport()
        {
            if (prefabSettings == null) prefabSettings = new CardPrefabExportSettings();
            if (prefabSettings.atlas == null) prefabSettings.atlas = AssetDatabase.LoadAssetAtPath<CardAtlasData>(CardRenderingValidation.AtlasPath);
            if (prefabSettings.shader == null) prefabSettings.shader = Shader.Find(CardShaderProperties.ShaderName);
            if (prefabSettings.mesh == null) prefabSettings.mesh = AssetDatabase.LoadAllAssetsAtPath(CardRenderingValidation.MeshPath).OfType<Mesh>().FirstOrDefault();
            prefabCheck = null;
        }

        void DrawPrefabExport()
        {
            if (prefabSettings == null) InitializePrefabExport();
            if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.KeyDown || Event.current.type == EventType.DragPerform)
                Undo.RecordObject(this, "编辑卡牌 Prefab 导出配置");
            prefabScroll = EditorGUILayout.BeginScrollView(prefabScroll);
            var oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Min(140, position.width * 0.34f);
            try
            {
                GUILayout.Label("卡牌 Prefab 导出", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("选择图集 Data、兼容 Shader 和双 UV 的实体卡牌 Mesh，导出一个可切换牌面的 Prefab 及一份共享材质。图集纹理保留原有采样配置。", MessageType.None);
                EditorGUI.BeginChangeCheck();
                var atlas = (CardAtlasData)EditorGUILayout.ObjectField("图集 Data", prefabSettings.atlas, typeof(CardAtlasData), false);
                if (atlas != prefabSettings.atlas)
                {
                    prefabSettings.atlas = atlas;
                    var faces = AvailableFaces();
                    if (!faces.Contains(prefabSettings.cardId)) prefabSettings.cardId = faces.FirstOrDefault() ?? "";
                }
                prefabSettings.shader = (Shader)EditorGUILayout.ObjectField("Shader", prefabSettings.shader, typeof(Shader), false);
                prefabSettings.mesh = (Mesh)EditorGUILayout.ObjectField("Mesh", prefabSettings.mesh, typeof(Mesh), false);
                var ids = AvailableFaces();
                if (ids.Length == 0) EditorGUILayout.LabelField("默认牌面", "暂无完整正面区域");
                else
                {
                    var current = Array.IndexOf(ids, prefabSettings.cardId);
                    var labels = current >= 0 ? ids : new[] { "请选择默认牌面" }.Concat(ids).ToArray();
                    var next = EditorGUILayout.Popup("默认牌面", current >= 0 ? current : 0, labels);
                    if (current >= 0) prefabSettings.cardId = ids[next];
                    else if (next > 0) prefabSettings.cardId = ids[next - 1];
                }
                prefabSettings.addCollider = EditorGUILayout.Toggle("添加 BoxCollider", prefabSettings.addCollider);
                GUILayout.Space(8);
                GUILayout.Label("火漆印章（可选）", EditorStyles.boldLabel);
                prefabSettings.sealMap = (Texture2D)EditorGUILayout.ObjectField("火漆颜色贴图", prefabSettings.sealMap, typeof(Texture2D), false);
                prefabSettings.sealNormalMap = (Texture2D)EditorGUILayout.ObjectField("火漆法线贴图", prefabSettings.sealNormalMap, typeof(Texture2D), false);
                if (GUILayout.Button("使用内置爪印火漆贴图"))
                {
                    prefabSettings.sealMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CardArt/Textures/WaxSeal_Paw.png");
                    prefabSettings.sealNormalMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CardArt/Textures/WaxSeal_Paw_Normal.png");
                    GUI.changed = true;
                }
                prefabSettings.sealEnabled = EditorGUILayout.Toggle("默认开启火漆", prefabSettings.sealEnabled);
                prefabSettings.sealColor = EditorGUILayout.ColorField(new GUIContent("火漆颜色"), prefabSettings.sealColor, true, false, false);
                prefabSettings.sealEmissionStrength = EditorGUILayout.Slider(new GUIContent("火漆自发光强度", "沿用火漆颜色与纹理；0 关闭，1 添加完整颜色亮度，仍保留正常受光。"), prefabSettings.sealEmissionStrength, 0, 1);
                using (new EditorGUI.DisabledScope(prefabSettings.sealNormalMap == null))
                    prefabSettings.sealNormalScale = EditorGUILayout.Slider("火漆法线强度", prefabSettings.sealNormalScale, 0, 2);
                var sealCenter = EditorGUILayout.Vector2Field("中心（UV，左下为原点）", new Vector2(prefabSettings.sealRect.x, prefabSettings.sealRect.y));
                var sealSize = EditorGUILayout.Vector2Field("尺寸（UV 宽 / 高）", new Vector2(prefabSettings.sealRect.z, prefabSettings.sealRect.w));
                prefabSettings.sealRect = new Vector4(sealCenter.x, sealCenter.y, sealSize.x, sealSize.y);
                prefabSettings.sealRotation = EditorGUILayout.Slider("火漆旋转（逆时针 °）", prefabSettings.sealRotation, -180, 180);
                if (GUILayout.Button("恢复默认火漆布局"))
                {
                    var defaults = new CardPrefabExportSettings();
                    prefabSettings.sealRect = defaults.sealRect;
                    prefabSettings.sealRotation = defaults.sealRotation;
                    GUI.changed = true;
                }
                EditorGUILayout.HelpBox("法线使用 Normal Map 导入。贴图写入共享材质；开启与颜色可以逐张设置。只绘制在正面，覆盖镭射效果。", MessageType.None);
                GUILayout.Space(8);
                GUILayout.Label("手绘红叉（可选）", EditorStyles.boldLabel);
                prefabSettings.redCrossMap = (Texture2D)EditorGUILayout.ObjectField("红叉贴图", prefabSettings.redCrossMap, typeof(Texture2D), false);
                if (GUILayout.Button("使用内置低分辨率红叉"))
                {
                    prefabSettings.redCrossMap = AssetDatabase.LoadAssetAtPath<Texture2D>(CardRedCrossSetup.TexturePath);
                    GUI.changed = true;
                }
                prefabSettings.redCrossEnabled = EditorGUILayout.Toggle("默认显示红叉", prefabSettings.redCrossEnabled);
                EditorGUILayout.HelpBox("内置 128×192，Point / Clamp，无 mipmap。红叉覆盖正面的镭射与火漆颜色，开关逐牌独立。", MessageType.None);
                GUILayout.Space(8);
                GUILayout.Label("导出设置", EditorStyles.boldLabel);
                prefabSettings.outputName = EditorGUILayout.TextField("Prefab 名称", prefabSettings.outputName);
                prefabSettings.outputFolder = EditorGUILayout.TextField("Assets 内目录", prefabSettings.outputFolder);
                if (GUILayout.Button("选择导出目录…"))
                {
                    var folder = EditorUtility.OpenFolderPanel("Prefab 导出目录", CardAtlasBuilder.Absolute(prefabSettings.outputFolder), "");
                    if (!string.IsNullOrEmpty(folder)) prefabSettings.outputFolder = CardAtlasBuilder.ProjectPath(folder);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    prefabCheck = null; prefabError = null; prefabStatus = null; lastExportedPrefab = null;
                    EditorUtility.SetDirty(this);
                }
                if (prefabCheck == null) prefabCheck = CardPrefabExporter.Validate(prefabSettings);
                GUILayout.Space(8);
                GUILayout.Label("兼容性校验", EditorStyles.boldLabel);
                if (GUILayout.Button("重新校验")) { prefabCheck = CardPrefabExporter.Validate(prefabSettings); prefabError = null; }
                foreach (var issue in prefabCheck.errors) EditorGUILayout.HelpBox(issue, MessageType.Error);
                foreach (var warning in prefabCheck.warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
                if (prefabCheck.CanExport)
                {
                    var size = prefabCheck.surfaceBounds.size * 1000;
                    EditorGUILayout.HelpBox($"校验通过 · {prefabCheck.faceCount} 个可用正面 + 卡背\nMesh：{size.x:0.###} × {size.z:0.###} mm · 厚度 {size.y:0.###} mm\n共享材质 · 牌面与 Color Tint 可逐张设置 · Surface 自动对齐 XZ 平面", MessageType.Info);
                    var texture = prefabSettings.atlas.texture;
                    EditorGUILayout.LabelField("图集采样", texture.filterMode + " / " + texture.wrapMode);
                }
                if (!string.IsNullOrEmpty(prefabCheck.prefabPath))
                {
                    EditorGUILayout.LabelField("Prefab", prefabCheck.prefabPath, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField("共享材质", prefabCheck.materialPath, EditorStyles.wordWrappedMiniLabel);
                }
                DrawPrefabFacePreview();
                if (!string.IsNullOrEmpty(prefabError)) EditorGUILayout.HelpBox(prefabError, MessageType.Error);
                if (!string.IsNullOrEmpty(prefabStatus)) EditorGUILayout.HelpBox(prefabStatus, MessageType.Info);
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLabelWidth;
                EditorGUILayout.EndScrollView();
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(prefabCheck != null && prefabCheck.CanExport ? "可导出" : "请修复校验问题", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (lastExportedPrefab != null && GUILayout.Button("定位 Prefab", EditorStyles.toolbarButton)) EditorGUIUtility.PingObject(lastExportedPrefab);
                using (new EditorGUI.DisabledScope(prefabCheck == null || !prefabCheck.CanExport || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("导出 Card Prefab", EditorStyles.toolbarButton)) ExportPrefab();
            }
        }

        string[] AvailableFaces() => prefabSettings.atlas?.cards?.Where(c => c != null && !c.clipped && c.id != "card_back" && !string.IsNullOrWhiteSpace(c.id))
            .OrderBy(c => c.index).Select(c => c.id).Distinct().ToArray() ?? Array.Empty<string>();

        void DrawPrefabFacePreview()
        {
            var atlas = prefabSettings.atlas;
            if (atlas == null || atlas.texture == null || atlas.cards == null) return;
            var face = atlas.cards.FirstOrDefault(c => c != null && c.id == prefabSettings.cardId && !c.clipped);
            var back = atlas.cards.FirstOrDefault(c => c != null && c.id == "card_back" && !c.clipped);
            if (face == null || back == null || atlas.cardSize.x <= 0 || atlas.cardSize.y <= 0) return;
            GUILayout.Space(8);
            GUILayout.Label("牌面 / 卡背（图集区域预览）", EditorStyles.boldLabel);
            var ratio = (float)atlas.cardSize.x / atlas.cardSize.y;
            var height = Mathf.Min(210, Mathf.Max(1, (position.width - 65) / (2 * ratio)));
            var area = GUILayoutUtility.GetRect(1, height, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint) return;
            var width = height * ratio;
            var start = area.x + (area.width - 2 * width - 14) * 0.5f;
            foreach (var item in new[] { face, back })
            {
                var rect = new Rect(start, area.y, width, height);
                var uv = item.uv;
                GUI.DrawTextureWithTexCoords(rect, atlas.texture, new Rect(uv.x, uv.y, uv.z, uv.w), true);
                start += width + 14;
            }
        }

        void ExportPrefab()
        {
            try
            {
                prefabCheck = CardPrefabExporter.Validate(prefabSettings);
                if (!prefabCheck.CanExport) return;
                var overwrite = File.Exists(prefabCheck.prefabPath) || File.Exists(prefabCheck.materialPath);
                if (overwrite && !EditorUtility.DisplayDialog("更新同名 Prefab 和共享材质？",
                    "将覆盖生成内容；使用此材质的实例也会更新。\n" + prefabCheck.prefabPath + "\n" + prefabCheck.materialPath, "更新", "取消")) return;
                lastExportedPrefab = CardPrefabExporter.Export(prefabSettings, overwrite);
                prefabStatus = "已导出 Prefab 和共享材质：\n" + prefabCheck.prefabPath;
                prefabError = null;
                EditorGUIUtility.PingObject(lastExportedPrefab);
            }
            catch (Exception e) { prefabError = e.Message; }
        }
    }
}
