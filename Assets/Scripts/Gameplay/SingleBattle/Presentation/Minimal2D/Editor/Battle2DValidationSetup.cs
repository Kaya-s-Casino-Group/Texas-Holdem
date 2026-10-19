using System;
using System.IO;
using CardArt;
using CardArt.Editor;
using RetroRendering;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Poker.Battle2DValidation.Editor
{
    /// <summary>Builds validation art and a camera using its fixed renderer in the shared MainURP pipeline.</summary>
    public static class Battle2DValidationSetup
    {
        public const string ScenePath = "Assets/Scenes/Battle2DValidation.unity";
        public const string ArtFolder = "Assets/GameAssets/SingleBattle/Presentation/Minimal2D/Art";
        public const string SettingsFolder = "Assets/Settings";
        public const string CardPrefabPath = ArtFolder + "/Battle2DCard.prefab";
        public const string ArtProfilePath = ArtFolder + "/Battle2DCardArt.asset";
        public const string ActorSpritePath = ArtFolder + "/Battle2DActor.png";
        public const string ActorMaterialPath = ArtFolder + "/Battle2DActor.mat";
        public const string PipelinePath = SettingsFolder + "/MainURP.asset";
        public const string RendererPath = SettingsFolder + "/Battle2DRenderer.asset";
        private const string SourceCardPath = "Assets/GameAssets/CardArt/Prefabs/Card.prefab";
        private const string AtlasPath = "Assets/Art/CardAtlases/Classic.asset";
        private const string FontPath = "Assets/Fonts/FusionPixelFont/TMP/FusionPixel12pxProportional.asset";
        private const string ShaderName = "CardArt/Validation Unlit";

        [MenuItem("工具/卡牌战斗/2D验证/创建或更新场景")]
        public static void CreateOrUpdate()
        {
            RequireEditMode();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                var loaded = SceneManager.GetSceneAt(index);
                if (loaded.path == ScenePath)
                    throw new InvalidOperationException("请先保存并关闭已打开的 2D 验证场景，再更新其生成内容。其他场景无需关闭。");
            }

            var font = Required<TMP_FontAsset>(FontPath);
            EnsureAssets(); EnsureFolder("Assets/Scenes");
            var prefab = Required<GameObject>(CardPrefabPath);
            var pipeline = Required<UniversalRenderPipelineAsset>(PipelinePath);

            var originalActive = SceneManager.GetActiveScene();
            var scene = default(Scene);
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("无法创建临时验证场景。");
                BuildScene(scene, prefab, pipeline, font);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("无法保存 2D 验证场景：" + ScenePath);
            }
            finally
            {
                if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("2D 验证场景已生成：" + ScenePath + "。验证相机使用 MainURP 中固定的 2D Renderer；默认 3D Renderer 和现有管线配置保持原样。");
        }

        /// <summary>Creates validation art and registers its renderer in MainURP without opening or changing a scene.</summary>
        public static void EnsureAssets()
        {
            RequireEditMode();
            var source = Required<GameObject>(SourceCardPath);
            var sourceView = source.GetComponentInChildren<CardView>(true);
            var mesh = sourceView != null ? sourceView.GetComponent<MeshFilter>()?.sharedMesh : null;
            if (sourceView == null || mesh == null)
                throw new InvalidOperationException("原卡牌 Prefab 必须提供带 Mesh 的 CardView：" + SourceCardPath);
            var atlas = Required<CardAtlasData>(AtlasPath);
            var shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException("缺少验证 Shader：" + ShaderName + "。请等待 Unity 完成导入。");
            EnsureFolder(ArtFolder); EnsureFolder(SettingsFolder);
            var profile = CreateArtProfile(atlas);
            CardPrefabExporter.Export(new CardPrefabExportSettings
            {
                atlas = atlas,
                mesh = mesh,
                artProfile = profile,
                shader = shader,
                cardId = "spades_a",
                outputFolder = ArtFolder,
                outputName = "Battle2DCard",
                addCollider = true
            }, overwrite: true);
            EnsureRenderer();
            EnsureActorArtwork();
            AssetDatabase.SaveAssetIfDirty(profile);
        }

        private static void EnsureActorArtwork()
        {
            if (!File.Exists(ActorSpritePath))
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                    texture.Apply(); File.WriteAllBytes(ActorSpritePath, texture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                AssetDatabase.ImportAsset(ActorSpritePath, ImportAssetOptions.ForceSynchronousImport);
            }
            var importer = AssetImporter.GetAtPath(ActorSpritePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("无法导入矩形角色贴图：" + ActorSpritePath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 2; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();

            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new InvalidOperationException("缺少 URP 2D 无光照 Sprite Shader。");
            var material = AssetDatabase.LoadAssetAtPath<Material>(ActorMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Battle2DActor" };
                AssetDatabase.CreateAsset(material, ActorMaterialPath);
            }
            material.shader = shader; material.mainTexture = Required<Texture2D>(ActorSpritePath);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
        }

        [MenuItem("工具/卡牌战斗/2D验证/打开验证场景")]
        public static void Open()
        {
            RequireEditMode();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateOrUpdate();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static CardArtProfile CreateArtProfile(CardAtlasData atlas)
        {
            const string path = ArtProfilePath;
            var profile = AssetDatabase.LoadAssetAtPath<CardArtProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<CardArtProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            profile.Configure(atlas, new[]
            {
                new CardShaderCombination("base", CardShaderParameter.Color("_ColorTint", Color.white)),
                new CardShaderCombination("selected", CardShaderParameter.Color("_ColorTint", new Color(1f, .86f, .47f, 1f))),
                new CardShaderCombination("disabled", CardShaderParameter.Color("_ColorTint", new Color(.43f, .43f, .43f, 1f)))
            });
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void EnsureRenderer()
        {
            var pipeline = Required<UniversalRenderPipelineAsset>(PipelinePath);
            var renderer = AssetDatabase.LoadAssetAtPath<Simple2DRendererData>(RendererPath);
            if (renderer == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(RendererPath) != null)
                    throw new InvalidOperationException("固定 2D Renderer 路径存在其他类型资源：" + RendererPath);
                renderer = ScriptableObject.CreateInstance<Simple2DRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            var data = new SerializedObject(pipeline);
            var renderers = Property(data, "m_RendererDataList");
            for (int index = 0; index < renderers.arraySize; index++)
                if (renderers.GetArrayElementAtIndex(index).objectReferenceValue == renderer) return;
            // Append only. Default index, existing renderer slots and all shared pipeline settings stay intact.
            int addedIndex = renderers.arraySize;
            renderers.arraySize = addedIndex + 1;
            renderers.GetArrayElementAtIndex(addedIndex).objectReferenceValue = renderer;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(pipeline);
        }

        private static int RendererIndex(UniversalRenderPipelineAsset pipeline)
        {
            var renderer = Required<Simple2DRendererData>(RendererPath);
            var data = new SerializedObject(pipeline);
            var renderers = Property(data, "m_RendererDataList");
            for (int index = 0; index < renderers.arraySize; index++)
                if (renderers.GetArrayElementAtIndex(index).objectReferenceValue == renderer) return index;
            throw new InvalidOperationException("MainURP 尚未注册固定 2D Renderer，请先生成验证资源。");
        }

        private static void BuildScene(Scene scene, GameObject prefab, UniversalRenderPipelineAsset pipeline,
            TMP_FontAsset font)
        {
            var cameraObject = CreateObject(scene, "2D 正交相机", typeof(Camera), typeof(PhysicsRaycaster));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 5.5f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(24, 31, 43, 255);
            camera.nearClipPlane = .1f; camera.farClipPlane = 30;
            camera.allowHDR = false; camera.allowMSAA = false; camera.useOcclusionCulling = false;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false; cameraData.renderShadows = false;
            cameraData.allowHDROutput = false; cameraData.allowXRRendering = false;
            cameraData.requiresDepthOption = CameraOverrideOption.Off; cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.antialiasing = AntialiasingMode.None; cameraData.SetRenderer(RendererIndex(pipeline));

            var events = CreateObject(scene, "输入事件", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var root = CreateObject(scene, "2D 卡牌战斗");
            var cardRoot = CreateObject(scene, "卡牌").transform;
            cardRoot.SetParent(root.transform, false);
            var view = root.AddComponent<Battle2DView>();
            var playerVisual = ActorRectangle(scene, root.transform, "玩家矩形", new Vector3(-3, 2, 0), new Color32(77, 158, 232, 255));
            var enemyVisual = ActorRectangle(scene, root.transform, "敌人矩形", new Vector3(3, 2, 0), new Color32(230, 97, 99, 255));
            view.ConfigureActors(playerVisual, enemyVisual);

            var canvasObject = CreateObject(scene, "战斗界面", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = 5;
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;

            var status = Label(canvas.transform, font, "战斗状态", "卡牌战斗 · 2D 验证", 32,
                new Vector2(0, 1), new Vector2(28, -24), new Vector2(1544, 90));
            var details = Label(canvas.transform, font, "详细状态", "", 23,
                new Vector2(0, 1), new Vector2(28, -120), new Vector2(1544, 100));
            Label(canvas.transform, font, "操作提示", "点击卡牌选择，再出牌或弃牌。", 21,
                new Vector2(.5f, 1), new Vector2(0, -230), new Vector2(1100, 42), centered: true);
            var log = Label(canvas.transform, font, "战斗记录", "", 20,
                Vector2.zero, new Vector2(28, 24), new Vector2(1544, 150));
            var play = Button(canvas.transform, font, "出牌", -330);
            var discard = Button(canvas.transform, font, "弃牌", -110);
            var endTurn = Button(canvas.transform, font, "结束回合", 110);
            var restart = Button(canvas.transform, font, "重新开始", 330);
            view.Configure(prefab, cardRoot, status, details, log, play, discard, endTurn, restart);
            Battle2DTimelineSetup.ConfigureBattle(view);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static Transform ActorRectangle(Scene scene, Transform parent, string name, Vector3 position, Color color)
        {
            var gameObject = CreateObject(scene, name, typeof(SpriteRenderer));
            gameObject.transform.SetParent(parent, false); gameObject.transform.localPosition = position;
            gameObject.transform.localScale = new Vector3(1.2f, .8f, 1);
            var renderer = gameObject.GetComponent<SpriteRenderer>();
            renderer.sprite = Required<Sprite>(ActorSpritePath); renderer.sharedMaterial = Required<Material>(ActorMaterialPath);
            renderer.color = color; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return gameObject.transform;
        }

        private static TMP_Text Label(Transform parent, TMP_FontAsset font, string name, string text, int size,
            Vector2 anchor, Vector2 position, Vector2 dimensions, bool centered = false)
        {
            var gameObject = CreateObject(parent.gameObject.scene, name, typeof(RectTransform), typeof(TextMeshProUGUI));
            gameObject.layer = 5; var rect = gameObject.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = dimensions;
            var label = gameObject.GetComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = size; label.color = new Color32(232, 236, 240, 255);
            label.raycastTarget = false; label.enableWordWrapping = true;
            label.alignment = centered ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
            return label;
        }

        private static Button Button(Transform parent, TMP_FontAsset font, string text, float x)
        {
            var gameObject = CreateObject(parent.gameObject.scene, text, typeof(RectTransform), typeof(Image), typeof(Button));
            gameObject.layer = 5; var rect = gameObject.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(x, 194); rect.sizeDelta = new Vector2(200, 58);
            var image = gameObject.GetComponent<Image>(); image.color = new Color32(61, 78, 98, 255);
            var button = gameObject.GetComponent<Button>(); button.targetGraphic = image;
            var label = Label(rect, font, "文字", text, 25, new Vector2(.5f, .5f), Vector2.zero, new Vector2(180, 50), centered: true);
            label.color = Color.white;
            return button;
        }

        private static T Required<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("缺少资源：" + path);

        private static GameObject CreateObject(Scene scene, string name, params Type[] components)
        {
            var gameObject = new GameObject(name);
            // Scene construction also runs from automation and preview contexts where SetActiveScene
            // can return false. Move each object before its components execute any lifecycle callbacks.
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            foreach (var component in components) gameObject.AddComponent(component);
            return gameObject;
        }

        private static SerializedProperty Property(SerializedObject owner, string name) =>
            owner.FindProperty(name) ?? throw new InvalidOperationException("当前 URP 缺少所需配置字段：" + name);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); var parent = path.Substring(0, slash);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出运行模式，再创建或打开验证场景。");
        }
    }
}
