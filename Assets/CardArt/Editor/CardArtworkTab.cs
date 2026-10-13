#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace CardArt.Editor
{
    public sealed partial class CardAtlasWindow
    {
        [SerializeField] private CardArtProfile artProfile;
        [SerializeField] private GameObject artworkPrefab;
        [SerializeField] private bool previewFaceUp = true;
        [SerializeField] private List<string> previewCombinations = new List<string>();
        private List<CardShaderCombination> combinationDraft;
        private int editCombination;
        [SerializeField] private bool showCombinationEditor;
        private CardArtProfile draftProfile;
        [SerializeField] private int previewFace;
        private CardArtworkPreviewRenderer artworkRenderer;
        [SerializeField] private float previewTilt = 20;
        [SerializeField] private float previewKeyLight = 1.5f, previewFillLight = .5f, previewAmbient = .5f;
        [SerializeField] private Vector2 previewLightDirection = new Vector2(45, -35);
        [SerializeField] private bool showPreviewLighting = true;
        private string artworkError;
        public const string DefaultProfilePath = "Assets/CardArt/Profiles/StandardCardArt.asset";

        [MenuItem("Tools/卡牌美术/Shader 参数组合")]
        public static void OpenCombinations() => OpenArtwork();
        [MenuItem("Tools/卡牌美术/属性表现")]
        public static void OpenArtwork()
        {
            var window = GetWindow<CardAtlasWindow>("卡牌美术工具"); window.ConfigureWindowSize(); window.tab = 3; window.Show();
        }
        private void DrawArtwork()
        {
            using (var area = new EditorGUILayout.ScrollViewScope(contentScroll))
            {
                contentScroll = area.scrollPosition;
                EditorGUILayout.HelpBox("美术只定义命名的 Shader 参数组合。玩法侧另行配置状态与组合的映射；这里预览参数组合，不解释强化、塑封等规则状态。Atlas 和实例化采样约定保持不变。", MessageType.Info);
                if (artProfile == null) artProfile = AssetDatabase.LoadAssetAtPath<CardArtProfile>(DefaultProfilePath);
                if (artworkPrefab == null) artworkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab");
                EditorGUI.BeginChangeCheck();
                var nextProfile = (CardArtProfile)EditorGUILayout.ObjectField("共享美术配置", artProfile, typeof(CardArtProfile), false);
                var nextPrefab = (GameObject)EditorGUILayout.ObjectField("单牌美术 Prefab", artworkPrefab, typeof(GameObject), false);
                if (EditorGUI.EndChangeCheck()) { artProfile = nextProfile; artworkPrefab = nextPrefab; DisposeArtworkPreview(); }
                if (artProfile == null)
                {
                    EditorGUILayout.HelpBox("创建或选择卡牌美术配置后编辑属性表现。", MessageType.Info);
                    if (GUILayout.Button("创建美术配置…"))
                    {
                        var path = EditorUtility.SaveFilePanelInProject("创建卡牌美术配置", "卡牌美术配置", "asset", "配置由整套牌共用。");
                        if (!string.IsNullOrEmpty(path))
                        {
                            var skin = artworkPrefab != null ? artworkPrefab.GetComponentInChildren<CardView>(true)?.Atlas : null;
                            if (skin == null) { artworkError = "请先选择带有效图集的单牌 Prefab。"; }
                            else { artProfile = CreateInstance<CardArtProfile>(); artProfile.Configure(skin, new[] { new CardShaderCombination("base",CardShaderParameter.Color("_ColorTint",Color.white)) }); AssetDatabase.CreateAsset(artProfile, path); }
                        }
                    }
                    return;
                }
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("图集", artProfile.Atlas, typeof(CardAtlasData), false);
                DrawShaderCombinations();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("保存美术配置")) { try { ValidateProfile(artProfile,ArtworkMaterial); AssetDatabase.SaveAssetIfDirty(artProfile); } catch(Exception exception) { artworkError=exception.Message; } }
                    if (GUILayout.Button("定位美术配置")) EditorGUIUtility.PingObject(artProfile);
                    if (GUILayout.Button("绑定到单牌 Prefab")) BindArtworkPrefab();
                }
                EditorGUILayout.Space(); EditorGUILayout.LabelField("实例组合预览（左：基础组合；右：所选组合）", EditorStyles.boldLabel);
                previewFaceUp = EditorGUILayout.Toggle("显示正面", previewFaceUp);
                foreach(var combination in artProfile.Combinations.Where(c=>c.Id!=artProfile.BaseCombination))
                {
                    bool selected=previewCombinations.Contains(combination.Id);
                    bool next=EditorGUILayout.Toggle(combination.Id,selected);
                    if(next && !selected) previewCombinations.Add(combination.Id);
                    if(!next && selected) previewCombinations.Remove(combination.Id);
                }
                previewCombinations.RemoveAll(id=>!artProfile.Combinations.Any(c=>c.Id==id));
                EditorGUILayout.LabelField("按选择顺序叠加：" + string.Join(" → ",previewCombinations),EditorStyles.miniLabel);
                DrawArtworkPreview();
                if (!string.IsNullOrEmpty(artworkError)) EditorGUILayout.HelpBox(artworkError, MessageType.Error);
            }
        }
        private void BindArtworkPrefab()
        {
            if (artworkPrefab == null || !EditorUtility.IsPersistent(artworkPrefab)) { artworkError = "选择已保存的单牌 Prefab。"; return; }
            var path = AssetDatabase.GetAssetPath(artworkPrefab); var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var surface = root.GetComponentInChildren<CardView>(true);
                if (surface == null) throw new ArgumentException("Prefab 中没有 CardView。");
                surface.SetArtProfile(artProfile); ValidateArtwork(surface);
                PrefabUtility.SaveAsPrefabAsset(root, path); DisposeArtworkPreview(); artworkError = null;
            }
            catch (Exception exception) { artworkError = exception.Message; }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private Material ArtworkMaterial => artworkPrefab != null ? artworkPrefab.GetComponentInChildren<CardView>(true)?.GetComponent<MeshRenderer>().sharedMaterial : null;
        private static void ValidateProfile(CardArtProfile profile, Material material)
        {
            if(material==null) throw new ArgumentException("缺少单牌材质。");
            CardPrefabExporter.ValidateProfileParameters(profile,material);
        }
        private static void ValidateArtwork(CardView surface) => ValidateProfile(surface.ArtProfile,surface.GetComponent<MeshRenderer>().sharedMaterial);
        private void DrawShaderCombinations()
        {
            showCombinationEditor=EditorGUILayout.Foldout(showCombinationEditor,"编辑 Shader 参数组合",true);
            if(!showCombinationEditor) return;
            if(combinationDraft==null || draftProfile!=artProfile)
            { combinationDraft=artProfile.Combinations.Select(c=>c.Copy()).ToList(); draftProfile=artProfile; }
            if(combinationDraft.Count==0) { EditorGUILayout.HelpBox("请先在配置中添加有效的基础组合。",MessageType.Error); return; }
            using(new EditorGUILayout.HorizontalScope())
            {
                editCombination=EditorGUILayout.Popup("编辑组合",Mathf.Clamp(editCombination,0,combinationDraft.Count-1),combinationDraft.Select(c=>c.Id).ToArray());
                if(GUILayout.Button("新增组合",GUILayout.Width(90))) { combinationDraft.Add(new CardShaderCombination("组合_"+Guid.NewGuid().ToString("N").Substring(0,6))); editCombination=combinationDraft.Count-1; }
            }
            var combination=combinationDraft[editCombination];
            using(new EditorGUI.DisabledScope(combination.Id==artProfile.BaseCombination)) combination.Id=EditorGUILayout.TextField("组合 ID",combination.Id);
            for(int i=0;i<combination.Parameters.Count;i++)
            {
                var parameter=combination.Parameters[i];
                using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using(new EditorGUILayout.HorizontalScope())
                    {
                        parameter.Name=EditorGUILayout.TextField("Shader 参数",parameter.Name);
                        if(GUILayout.Button("移除",GUILayout.Width(55))) { combination.Parameters.RemoveAt(i); break; }
                    }
                    parameter.Kind=(CardShaderValueKind)EditorGUILayout.Popup("类型",(int)parameter.Kind,new[]{"数值","颜色（HDR）","向量"});
                    parameter.Operation=(CardShaderParameterOperation)EditorGUILayout.Popup("叠加方式",(int)parameter.Operation,new[]{"覆盖","乘算已有值"});
                    if(parameter.Kind==CardShaderValueKind.Float) parameter.Value=new Vector4(EditorGUILayout.FloatField("值",parameter.Value.x),0,0,0);
                    else if(parameter.Kind==CardShaderValueKind.Color) parameter.Value=EditorGUILayout.ColorField(new GUIContent("值"),parameter.Value,true,true,true);
                    else parameter.Value=EditorGUILayout.Vector4Field("值",parameter.Value);
                }
            }
            if(GUILayout.Button("新增参数")) combination.Parameters.Add(CardShaderParameter.Float("_Parameter",0));
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("应用组合修改"))
                {
                    CardArtProfile candidate=null;
                    try
                    {
                        candidate=CreateInstance<CardArtProfile>(); candidate.Configure(artProfile.Atlas,combinationDraft,artProfile.BaseCombination);
                        ValidateProfile(candidate,ArtworkMaterial); Undo.RecordObject(artProfile,"编辑 Shader 参数组合");
                        artProfile.SetCombinations(combinationDraft); EditorUtility.SetDirty(artProfile); artworkError=null;
                    }
                    catch(Exception exception) { artworkError=exception.Message; }
                    finally { if(candidate!=null) DestroyImmediate(candidate); }
                }
                if(GUILayout.Button("重载组合")) { combinationDraft=null; artworkError=null; }
                using(new EditorGUI.DisabledScope(combination.Id==artProfile.BaseCombination))
                    if(GUILayout.Button("移除组合")) { combinationDraft.RemoveAt(editCombination); editCombination=0; }
            }
        }
        private void DrawArtworkPreview()
        {
            if (artworkPrefab == null || artProfile.Atlas == null) return;
            try
            {
                if (artworkRenderer == null) artworkRenderer = new CardArtworkPreviewRenderer(artworkPrefab);
                var faces = artProfile.Atlas.cards.FindAll(card => !card.clipped && card.id != "card_back").ConvertAll(card => card.id).ToArray();
                if (faces.Length == 0) throw new ArgumentException("图集中没有可用正面。");
                previewFace = EditorGUILayout.Popup("预览牌面", Mathf.Clamp(previewFace, 0, faces.Length - 1), faces);
                previewTilt = EditorGUILayout.Slider("观察倾角", previewTilt, 0, 55);
                showPreviewLighting = EditorGUILayout.Foldout(showPreviewLighting, "预览光源（只影响预览场景）", true);
                if (showPreviewLighting)
                {
                    previewKeyLight = EditorGUILayout.Slider("主光强度", previewKeyLight, 0, 5);
                    previewFillLight = EditorGUILayout.Slider("补光强度", previewFillLight, 0, 3);
                    previewAmbient = EditorGUILayout.Slider("环境亮度", previewAmbient, 0, 1);
                    previewLightDirection.x = EditorGUILayout.Slider("主光俯仰", previewLightDirection.x, 0, 90);
                    previewLightDirection.y = EditorGUILayout.Slider("主光方向", previewLightDirection.y, -180, 180);
                    if (GUILayout.Button("恢复预览光源"))
                    { previewKeyLight = 1.5f; previewFillLight = .5f; previewAmbient = .5f; previewLightDirection = new Vector2(45,-35); }
                }
                artworkRenderer.ConfigureLighting(previewKeyLight, previewFillLight, previewAmbient, previewLightDirection);
                artworkRenderer.Present(artProfile, faces[previewFace], previewCombinations,previewFaceUp);
                EditorGUILayout.LabelField(artworkRenderer.InstancingEnabled ? "共享材质 · GPU Instancing 已开启 · 独立实例属性" : "共享材质未开启 GPU Instancing，请检查导出材质。", EditorStyles.miniLabel);
                var rect = GUILayoutUtility.GetRect(180, 300, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint)
                    GUI.DrawTexture(rect, artworkRenderer.Render(rect, previewTilt), ScaleMode.StretchToFill, false);
            }
            catch (Exception exception) { artworkError = exception.Message; }
        }
        private void OnInspectorUpdate() { if (tab == 3) Repaint(); }
        private void DisposeArtworkPreview()
        {
            combinationDraft=null; draftProfile=null;
            artworkRenderer?.Dispose(); artworkRenderer = null;
        }
    }
}
#endif
