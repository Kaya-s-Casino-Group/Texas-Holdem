#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CardPresentation.Editor
{
    /// <summary>Prepare the suit/chips tooltip prefab without installing gameplay or scene hover behaviour.</summary>
    public static class CardTooltipPrefabBuilder
    {
        public const string FontPath = "Assets/Fonts/FusionPixelFont/TMP/FusionPixel12pxProportional.asset";
        public const string PrefabPath = "Assets/GameAssets/CardPresentation/Prefabs/CardSuitChipsTooltip.prefab";
        private static readonly Color BackgroundColor = new Color32(51, 42, 36, 255);
        private static readonly Color RowBackgroundColor = new Color32(242, 230, 204, 255);
        private const string SourceFont = "Assets/Fonts/FusionPixelFont/fusion-pixel-12px-proportional-zh_hans.ttf";
        private const string PreviewPath = "Library/CardTooltipValidation/tooltip.png";
        private static bool importingTextMeshPro;

        [MenuItem("工具/卡牌表现/界面预制体/准备花色与筹码提示")]
        public static void Prepare()
        {
            EnsureFolder(Path.GetDirectoryName(FontPath));
            EnsureFolder(Path.GetDirectoryName(PrefabPath));
            if (!EnsureTextMeshProResources()) return;
            var font = PrepareFont();
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            // Upgrade the display component while preserving authored layout, styling and asset GUIDs.
            existing = existing == null ? CreatePrefab(font) : UpdatePrefab();
            Validate(existing);
            AssetDatabase.SaveAssetIfDirty(font);
            AssetDatabase.SaveAssetIfDirty(existing);
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log("Suit/chips tooltip prepared: " + PrefabPath);
        }

        private static void ConfigureBackground(Image background)
        {
            if (background.sprite != null) background.color = BackgroundColor;
            background.sprite = null;
            background.overrideSprite = null;
            background.type = Image.Type.Simple;
            background.preserveAspect = false;
            background.raycastTarget = false;
        }

        [MenuItem("工具/卡牌表现/界面布局/应用紧凑提示样式")]
        public static void UseCompactStyle()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ApplyCompactStyle(root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Validate(prefab);
                Selection.activeObject = prefab;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ApplyCompactStyle(GameObject root)
        {
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(336, 180);
            var background = rect.Find("Background").GetComponent<Image>();
            ConfigureBackground(background);
            background.color = BackgroundColor;
            StretchRect(background.rectTransform, Vector2.zero, new Vector2(-4, -4));
            background.transform.SetAsFirstSibling();

            var border = Decoration("Border", rect, new Color32(105, 84, 69, 255));
            StretchRect(border.rectTransform, Vector2.zero, Vector2.zero);
            border.transform.SetAsFirstSibling();
            var shadow = Decoration("Shadow", rect, new Color32(32, 23, 15, 60));
            StretchRect(shadow.rectTransform, new Vector2(4, -5), Vector2.zero);
            shadow.transform.SetAsFirstSibling();
            var divider = rect.Find("Divider");
            if (divider != null) Object.DestroyImmediate(divider.gameObject);
            StyleRow(Decoration("SuitRankBackground", rect, RowBackgroundColor),
                new Vector2(0, 44), new Vector2(312, 64), 3);
            StyleRow(Decoration("ChipsBackground", rect, RowBackgroundColor),
                new Vector2(0, -38), new Vector2(312, 76), 4);

            StyleLabel(rect.Find("SuitRank").GetComponent<TMP_Text>(), 36, new Vector2(0, 44), new Vector2(296, 48));
            StyleLabel(rect.Find("Chips").GetComponent<TMP_Text>(), 48, new Vector2(0, -38), new Vector2(296, 64));
        }

        private static void StyleRow(Image image, Vector2 position, Vector2 size, int siblingIndex)
        {
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.SetSiblingIndex(siblingIndex);
        }

        private static Image Decoration(string name, RectTransform parent, Color color)
        {
            var image = parent.Find(name)?.GetComponent<Image>() ??
                CreateRect(name, parent, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            ConfigureBackground(image);
            image.color = color;
            return image;
        }

        private static void StretchRect(RectTransform rect, Vector2 position, Vector2 inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = position;
            rect.sizeDelta = inset;
        }

        private static void StyleLabel(TMP_Text label, float size, Vector2 position, Vector2 bounds)
        {
            label.fontSize = size;
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = bounds;
        }

        private static bool EnsureTextMeshProResources()
        {
            if (Shader.Find("TextMeshPro/Mobile/Bitmap") != null && TMP_Settings.GetSettings() != null) return true;
            if (importingTextMeshPro) return false;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
            if (package == null) throw new InvalidOperationException("TextMeshPro package is unavailable.");
            importingTextMeshPro = true;
            AssetDatabase.importPackageCompleted += OnResourcesImported;
            AssetDatabase.importPackageFailed += OnResourcesImportFailed;
            AssetDatabase.importPackageCancelled += OnResourcesImportCancelled;
            AssetDatabase.ImportPackage(Path.Combine(package.resolvedPath,
                "Package Resources/TMP Essential Resources.unitypackage"), false);
            return false;
        }

        private static void OnResourcesImported(string packageName)
        {
            RemoveImportHandlers();
            EditorApplication.delayCall += Prepare;
        }

        private static void OnResourcesImportFailed(string packageName, string error)
        {
            RemoveImportHandlers();
            Debug.LogError("Cannot import TextMeshPro essential resources: " + error);
        }

        private static void OnResourcesImportCancelled(string packageName) => RemoveImportHandlers();

        private static void RemoveImportHandlers()
        {
            importingTextMeshPro = false;
            AssetDatabase.importPackageCompleted -= OnResourcesImported;
            AssetDatabase.importPackageFailed -= OnResourcesImportFailed;
            AssetDatabase.importPackageCancelled -= OnResourcesImportCancelled;
        }

        private static TMP_FontAsset PrepareFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (existing != null)
            {
                PrewarmFont(existing);
                return existing;
            }
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFont);
            if (source == null) throw new FileNotFoundException("Missing Fusion Pixel font.", SourceFont);
            var font = TMP_FontAsset.CreateFontAsset(source, 12, 1, GlyphRenderMode.RASTER_HINTED,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (font == null) throw new InvalidOperationException("Cannot create Fusion Pixel TMP font.");
            font.name = "FusionPixel12pxProportional";
            font.material.name = font.name + " Material";
            PrewarmFont(font);
            font.atlasTextures[0].name = font.name + " Atlas";
            font.atlasTextures[0].filterMode = FilterMode.Point;
            font.atlasTextures[0].wrapMode = TextureWrapMode.Clamp;
            AssetDatabase.CreateAsset(font, FontPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            EditorUtility.SetDirty(font);
            return font;
        }

        private static void PrewarmFont(TMP_FontAsset font)
        {
            // Use the font's actual suit glyphs, with no sprite replacement or fallback font.
            string characters = new string(Enumerable.Range(32, 95).Select(value => (char)value).ToArray()) + "黑桃红桃梅花方块筹码♠♥♣♦";
            if (!font.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException("Fusion Pixel font lacks tooltip characters: " + missing);
            EditorUtility.SetDirty(font);
        }

        private static GameObject UpdatePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ConfigureBackground(root.transform.Find("Background").GetComponent<Image>());
                var header = root.transform.Find("SuitRank")?.GetComponent<TMP_Text>();
                var chips = root.transform.Find("Chips")?.GetComponent<TMP_Text>();
                var view = root.GetComponent<CardTooltipView>() ?? root.AddComponent<CardTooltipView>();
                view.Configure(header, chips);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static GameObject CreatePrefab(TMP_FontAsset font)
        {
            var root = new GameObject("CardSuitChipsTooltip", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            try
            {
                root.layer = LayerMask.NameToLayer("UI");
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(448, 320);
                rect.localScale = Vector3.one * 0.0004f;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 20;
                var group = root.GetComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;

                var background = CreateRect("Background", rect, Vector2.zero, rect.sizeDelta).gameObject.AddComponent<Image>();
                background.color = BackgroundColor;
                ConfigureBackground(background);
                var suit = CreateLabel("SuitRank", rect, font, new Vector2(0, 72), new Vector2(336, 64), 48);
                var chips = CreateLabel("Chips", rect, font, new Vector2(0, -46), new Vector2(352, 112), 60);
                root.AddComponent<CardTooltipView>().Configure(suit, chips);
                ApplyCompactStyle(root);
                Canvas.ForceUpdateCanvases();
                foreach (var label in root.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static TextMeshProUGUI CreateLabel(string name, RectTransform parent, TMP_FontAsset font,
            Vector2 position, Vector2 size, float fontSize)
        {
            var text = CreateRect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSharedMaterial = font.material;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Normal;
            text.color = new Color32(45, 30, 20, 255);
            text.alignment = TextAlignmentOptions.Midline;
            text.enableWordWrapping = false;
            text.enableAutoSizing = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.richText = true;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void Validate(GameObject prefab)
        {
            if (prefab == null || prefab.GetComponent<Canvas>()?.renderMode != RenderMode.WorldSpace)
                throw new InvalidOperationException("Tooltip must have a World Space Canvas.");
            if (prefab.GetComponent<GraphicRaycaster>() != null || prefab.GetComponent<CanvasGroup>().blocksRaycasts ||
                prefab.GetComponentsInChildren<Graphic>().Any(graphic => graphic.raycastTarget))
                throw new InvalidOperationException("Tooltip must not intercept card mouse input.");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var labels = prefab.GetComponentsInChildren<TMP_Text>();
            if (labels.Length != 2 || labels.Any(label => label.font != font))
                throw new InvalidOperationException("Both tooltip labels must use the Fusion Pixel font asset.");
            if (prefab.GetComponent<CardTooltipView>() == null || "♠♥♣♦".Any(symbol => !font.HasCharacter((int)symbol)))
                throw new InvalidOperationException("Tooltip must bind its display component and all four native font suit glyphs.");
        }

        [MenuItem("工具/卡牌表现/截图/花色与筹码提示")]
        public static void Capture()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Validate(prefab);
            Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath));
            // Isolate the capture from the user's unsaved scene and the gameplay camera.
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(896, 480, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var pixels = new Texture2D(896, 480, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                var root = Object.Instantiate(prefab);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                foreach (var child in root.GetComponentsInChildren<Transform>()) child.gameObject.layer = 31;
                var camera = new GameObject("Tooltip Preview Camera").AddComponent<Camera>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.transform.position = new Vector3(0, 0, -1);
                camera.orthographic = true;
                var tooltipRect = (RectTransform)root.transform;
                camera.orthographicSize = Mathf.Max(tooltipRect.rect.height * tooltipRect.localScale.y,
                    tooltipRect.rect.width * tooltipRect.localScale.x / (896f / 480f)) * 0.6f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 2;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.065f, 0.15f, 0.115f);
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.aspect = 896f / 480f;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                root.GetComponent<Canvas>().worldCamera = camera;
                target.Create();
                void Shot(string path)
                {
                    Canvas.ForceUpdateCanvases();
                    foreach (var label in root.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
                    var header = root.GetComponent<CardTooltipView>().SuitRankText;
                    if (header.textInfo.characterInfo.Take(header.textInfo.characterCount)
                        .Any(character => character.isVisible && character.fontAsset != header.font))
                        throw new InvalidOperationException("Tooltip heading must render entirely with Fusion Pixel glyphs.");
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 896, 480), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                }
                Shot(PreviewPath);
                var tooltip = root.GetComponent<CardTooltipView>();
                foreach (Suit suit in Enum.GetValues(typeof(Suit)))
                {
                    tooltip.SetContent(suit, Rank.Jack, 10);
                    Shot(Path.Combine(Path.GetDirectoryName(PreviewPath), "tooltip_" + suit.ToString().ToLowerInvariant() + ".png"));
                }
                Debug.Log("Tooltip prefab capture: " + PreviewPath);
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
