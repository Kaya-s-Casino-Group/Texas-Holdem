#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardArt;
using CardInteraction;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CardPresentation.Editor
{
    public static class DeckOverviewSetup
    {
        public const string MaterialPath = "Assets/Art/Materials/Cards/FourBeastsCanvas.mat";
        public const string ItemPath = "Assets/GameAssets/CardPresentation/Prefabs/CardUIItem.prefab";
        public const string PrefabPath = "Assets/GameAssets/CardPresentation/Prefabs/DeckOverview.prefab";
        public const string AtlasPath = "Assets/Art/CardAtlases/FourBeastsAtlas.asset";
        public const string CapturePath = "Library/DeckOverviewValidation/overview.png";

        [MenuItem("工具/卡牌表现/界面预制体/准备牌库总览")]
        public static void Prepare() => Prepare(false);

        [MenuItem("工具/卡牌表现/界面布局/更新牌库总览")]
        public static void UpdateLayout() { Prepare(true); Show(); }

        private static void Prepare(bool updateLayout)
        {
            var atlas = AssetDatabase.LoadAssetAtPath<CardAtlasData>(AtlasPath);
            var shader = Shader.Find("CardPresentation/Card Canvas");
            if (atlas == null || shader == null) throw new InvalidOperationException("Import the shared card atlas and Canvas shader first.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "FourBeastsCanvas" };
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab")
                    .GetComponentInChildren<MeshRenderer>().sharedMaterial;
                foreach (string property in new[] { "_FoilMap", "_SealMap", "_SealNormalMap", "_RedCrossMap" })
                    material.SetTexture(property, source.GetTexture(property));
                foreach (string property in new[] { "_FoilSpeed", "_SealRotation", "_SealNormalScale" }) material.SetFloat(property, source.GetFloat(property));
                material.SetVector("_SealRect", source.GetVector("_SealRect")); material.SetTexture("_MainTex", atlas.texture);
                AssetDatabase.CreateAsset(material, MaterialPath); AssetDatabase.SaveAssetIfDirty(material);
            }
            var itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPath);
            if (itemPrefab != null && itemPrefab.GetComponentInChildren<CanvasRenderer>() == null)
            {
                var contents = PrefabUtility.LoadPrefabContents(ItemPath);
                try { contents.GetComponentInChildren<CardUIGraphic>().gameObject.AddComponent<CanvasRenderer>(); PrefabUtility.SaveAsPrefabAsset(contents,ItemPath); }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPath);
            }
            if (itemPrefab == null)
            {
                var root = Rect("UI Card", null, Vector2.zero, new Vector2(80,120));
                try
                {
                    var surface = Rect("Surface", root, Vector2.zero, Vector2.zero); Stretch(surface);
                    var graphic = surface.gameObject.AddComponent<CardUIGraphic>(); graphic.Bind(atlas, material); graphic.raycastTarget = true;
                    root.gameObject.AddComponent<CardUIItemView>().Configure(graphic);
                    itemPrefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, ItemPath);
                }
                finally { Object.DestroyImmediate(root.gameObject); }
            }
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!updateLayout && existing != null && existing.transform.Find("Frame/Panel/StatisticsPanel") != null)
            {
                EnsureRowsRoot(existing); return;
            }
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CardTooltipPrefabBuilder.FontPath);
            if (font == null) throw new InvalidOperationException("Prepare the Noto Sans CJK SC TMP font first.");
            var icons = new[] { ImportIcon("DeckAce"), ImportIcon("DeckFace"), ImportIcon("DeckNumber") };
            var canvasRoot = existing != null ? PrefabUtility.LoadPrefabContents(PrefabPath) :
                new GameObject("Deck Overview UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                foreach (Transform child in canvasRoot.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                canvasRoot.layer = LayerMask.NameToLayer("UI");
                var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                if (canvasRoot.GetComponent<CanvasGroup>() == null) canvasRoot.AddComponent<CanvasGroup>();
                canvas.pixelPerfect = true; canvas.sortingOrder = 500; canvas.planeDistance = 1;
                canvas.additionalShaderChannels |= CardUIGraphic.RequiredChannels;
                var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = .5f;
                var dim = Rect("Backdrop", canvasRoot.transform, Vector2.zero, Vector2.zero); Stretch(dim);
                dim.gameObject.AddComponent<Image>().color = new Color(.025f,.06f,.065f,.88f);
                var frame = Box("Frame",canvasRoot.transform,Vector2.zero,new Vector2(1040,632),new Color(.55f,.66f,.6f));
                var panel = Box("Panel",frame,Vector2.zero,new Vector2(1032,624),new Color(.055f,.12f,.13f));
                var title = Label("Title",panel,new Vector2(140,278),new Vector2(680,32),"牌组总览",font,24);
                var sidebar = Box("StatisticsPanel",panel,new Vector2(-382,0),new Vector2(236,526),new Color(.028f,.07f,.075f));
                var header = Box("Deck Header",sidebar,new Vector2(0,222),new Vector2(224,70),new Color(.08f,.18f,.19f));
                Label("Deck Name",header,new Vector2(0,10),new Vector2(210,30),"基础牌组",font,24);
                Label("Deck Type",header,new Vector2(0,-20),new Vector2(210,24),"剩余统计",font,16);
                Label("Categories",sidebar,new Vector2(0,160),new Vector2(210,26),"基础卡牌",font,20).color = new Color(.92f,.34f,.22f);
                var totals = new TMP_Text[3];
                var categoryNames = new[] { "A", "人头牌", "数字牌" };
                for (int i = 0; i < 3; i++)
                {
                    float x = -70 + i * 70;
                    var icon = Rect("Icon " + categoryNames[i],sidebar,new Vector2(x,110),new Vector2(52,52));
                    var image = icon.gameObject.AddComponent<Image>(); image.sprite = icons[i]; image.preserveAspect = true; image.raycastTarget = false;
                    totals[i] = Label("Total " + categoryNames[i],sidebar,new Vector2(x,70),new Vector2(60,36),"0",font,32);
                }
                var rows = new RectTransform[4]; var counts = new TMP_Text[4];
                var rowsRoot = Rect("SuitRows",panel,new Vector2(164,-8),new Vector2(680,528));
                var names = new[] { "♠", "♥", "♣", "♦" };
                var headers = new[] { "数字", "JQK", "A", "合计" };
                for (int i = 0; i < 4; i++) Label("Count Header " + i,sidebar,
                    new Vector2(-33 + i * 34,24),new Vector2(34,26),headers[i],font,16);
                for (int i = 0; i < 4; i++)
                {
                    rows[i] = Rect("Suit Row " + DeckOverviewView.SuitOrder[i],rowsRoot,Vector2.zero,Vector2.zero);
                    AnchorRow(rows[i],i);
                    float y = -20 - i * 60;
                    var rowBackground = Box("Count Row " + i,sidebar,new Vector2(0,y),new Vector2(222,50),new Color(.055f,.12f,.125f));
                    var suit = Label("Suit " + i,rowBackground,new Vector2(-90,0),new Vector2(34,38),names[i],font,26);
                    suit.color = i == 1 || i == 3 ? new Color(.98f,.28f,.24f) : new Color(.8f,.87f,.85f);
                    counts[i] = Label("Counts " + i,rowBackground,new Vector2(18,0),new Vector2(152,30),"",font,16);
                    counts[i].alignment = TextAlignmentOptions.MidlineLeft;
                }
                var ranks = new TMP_Text[13];
                var rankNames = new[] { "A","K","Q","J","10","9","8","7","6","5","4","3","2" };
                for (int i = 0; i < 13; i++)
                {
                    float y = 220-i*34;
                    Box("Rank Token " + rankNames[i],panel,new Vector2(-240,y),new Vector2(28,26),new Color(.13f,.24f,.245f));
                    Label("Rank " + rankNames[i],panel,new Vector2(-240,y),new Vector2(28,26),rankNames[i],font,16).color = new Color(.61f,.75f,.7f);
                    ranks[i] = Label("Rank Count " + rankNames[i],panel,new Vector2(-205,y),new Vector2(30,26),"0",font,16);
                }
                var note = Label("Note",sidebar,new Vector2(0,-250),new Vector2(220,26),"",font,16);
                note.color = new Color(.6f,.73f,.7f);
                var closeRect = Box("Return",panel,new Vector2(0,-292),new Vector2(980,32),new Color(.89f,.35f,.14f));
                closeRect.GetComponent<Image>().raycastTarget = true;
                var close = closeRect.gameObject.AddComponent<Button>(); close.targetGraphic = closeRect.GetComponent<Image>();
                Label("Text",closeRect,Vector2.zero,closeRect.sizeDelta,"返回",font,20);
                var view = canvasRoot.GetComponent<DeckOverviewView>() ?? canvasRoot.AddComponent<DeckOverviewView>();
                view.Configure(atlas,material,itemPrefab.GetComponent<CardUIItemView>(),rows,counts,title,note,close);
                view.ConfigureRowsRoot(rowsRoot);
                view.ConfigureStatistics(totals,ranks); view.SetRowLayout(120,5);
                PrefabUtility.SaveAsPrefabAsset(canvasRoot,PrefabPath);
            }
            finally
            {
                if (existing != null) PrefabUtility.UnloadPrefabContents(canvasRoot); else Object.DestroyImmediate(canvasRoot);
            }
        }
        private static void AnchorRow(RectTransform row, int index)
        {
            row.anchorMin = new Vector2(0,1-(index+1)/4f); row.anchorMax = new Vector2(1,1-index/4f);
            row.pivot = new Vector2(.5f,.5f); row.anchoredPosition = Vector2.zero; row.sizeDelta = Vector2.zero;
        }
        private static void EnsureRowsRoot(GameObject prefab)
        {
            var view = prefab.GetComponent<DeckOverviewView>();
            if (prefab.GetComponent<CanvasGroup>() != null && view.SuitRowsRoot != null && Mathf.Approximately(
                ((RectTransform)view.SuitRowsRoot.Find("Suit Row Spades")).anchorMin.y,1-1/(4-3*view.VerticalOverlap))) return;
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (contents.GetComponent<CanvasGroup>() == null) contents.AddComponent<CanvasGroup>();
                var panel = contents.transform.Find("Frame/Panel");
                var root = contents.GetComponent<DeckOverviewView>().SuitRowsRoot;
                if (root == null)
                {
                    root = Rect("SuitRows",panel,new Vector2(164,-8),new Vector2(680,528));
                    for (int index = 0; index < 4; index++)
                    {
                        var row = (RectTransform)panel.Find("Suit Row "+DeckOverviewView.SuitOrder[index]);
                        row.SetParent(root,false); AnchorRow(row,index);
                    }
                }
                contents.GetComponent<DeckOverviewView>().ConfigureRowsRoot(root);
                PrefabUtility.SaveAsPrefabAsset(contents,PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        private static Sprite ImportIcon(string name)
        {
            string path = "Assets/GameAssets/CardPresentation/UI/Icons/" + name + ".png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Import generated deck statistics icon: " + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048; importer.alphaIsTransparency = true; importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static RectTransform Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var rect = Rect(name,parent,position,size); var image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false; return rect;
        }

        [MenuItem("工具/卡牌表现/界面预览/显示牌库总览")]
        public static void Show()
        {
            var scene = SceneManager.GetActiveScene();
            var host = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardZonePlayHost>(true))
                .FirstOrDefault(item => item.HasSnapshot && item.GetComponent<HandZoneView>() != null);
            if (host == null) throw new InvalidOperationException("Bind a hand to CardMatchHost first.");
            var presenter = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DeckOverviewPresenter>(true))
                .FirstOrDefault(item => item.Match == host.Match);
            if (Application.isPlaying)
            {
                if (presenter == null) throw new InvalidOperationException("Prepare the scene's overview in Edit mode first.");
                var pile = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DrawPilePresenter>(true))
                    .FirstOrDefault(item => item.Overview == presenter);
                if (pile != null) pile.SetPinned(true); else presenter.Open(); return;
            }
            presenter = Register(host, CardUICameraSetup.Register(scene, host.GetComponent<CardZoneView>().FacingCamera));
            presenter.Open(); Selection.activeGameObject = presenter.gameObject;
            EditorSceneManager.MarkSceneDirty(scene);
        }
        public static DeckOverviewPresenter Register(CardZonePlayHost host, Camera uiCamera)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Scene registration is editor-only.");
            Prepare(); var scene = host.gameObject.scene;
            var presenter = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DeckOverviewPresenter>(true))
                .FirstOrDefault(item => item.Match == host.Match);
            if (presenter == null)
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                Undo.RegisterCreatedObjectUndo(root, "创建 Canvas 牌组总览"); presenter = Undo.AddComponent<DeckOverviewPresenter>(root);
            }
            // Bootstrap this local hand's roster from explicitly related seed zones, never all cards in the match.
            var zoneIds = new HashSet<ZoneId> { host.Zone.Id, host.DeckZone.Id, host.DiscardZone.Id };
            var route = host.GetComponent<CardZoneTransferPresenter>();
            var played = route?.Destination?.GetComponent<CardZonePlayHost>();
            if (played != null && played.Match == host.Match) zoneIds.Add(played.Zone.Id);
            var ids = host.Match.InitialState.Zones.Where(zone => zoneIds.Contains(zone.Id)).SelectMany(zone => zone.Cards);
            Undo.RecordObject(presenter, "绑定对局牌组总览");
            Canvas.ForceUpdateCanvases(); presenter.Configure(host.Match, presenter.GetComponent<DeckOverviewView>(), host.DeckZone.Id, ids);
            var canvas = presenter.GetComponent<Canvas>(); Undo.RecordObject(canvas, "绑定总览 UI Camera");
            canvas.worldCamera = uiCamera;
            if (uiCamera != null) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.planeDistance = 1; }
            PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
            if (uiCamera != null)
                CardHoverPresenterSetup.Register(presenter,uiCamera.GetComponent<RetroRendering.UICameraStack>()?.SceneCamera);
            var pile = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DrawPilePresenter>(true))
                .FirstOrDefault(item => item.Source == host);
            if (pile != null) { Undo.RecordObject(pile,"绑定牌堆预览"); pile.BindOverview(presenter); EditorUtility.SetDirty(pile); }
            EditorUtility.SetDirty(presenter); EditorUtility.SetDirty(canvas); EditorSceneManager.MarkSceneDirty(scene); return presenter;
        }

        [MenuItem("工具/卡牌表现/截图/牌库总览")]
        public static void Capture()
        {
            Prepare(); var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1280,720,TextureFormat.RGBA32,false); var previous = RenderTexture.active;
            Camera camera = null;
            try
            {
                var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)); SceneManager.MoveGameObjectToScene(root,scene);
                foreach (var item in root.GetComponentsInChildren<Transform>()) item.gameObject.layer = LayerMask.NameToLayer("UI");
                camera = new GameObject("Canvas Deck Capture").AddComponent<Camera>(); SceneManager.MoveGameObjectToScene(camera.gameObject,scene);
                camera.scene = scene; camera.cullingMask = 1 << LayerMask.NameToLayer("UI"); camera.transform.position = new Vector3(0,0,-10);
                camera.orthographic = true; camera.orthographicSize = 360; camera.nearClipPlane = .01f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f,.07f,.08f);
                camera.allowHDR = false; camera.allowMSAA = false; camera.targetTexture = target;
                camera.GetUniversalAdditionalCameraData().SetRenderer(1); camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                // The standalone request renders world-space Canvas geometry; the shipped prefab stays ScreenSpaceCamera.
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                var rootRect = (RectTransform)root.transform; rootRect.sizeDelta = new Vector2(1280,720);
                rootRect.SetPositionAndRotation(Vector3.zero,Quaternion.identity); rootRect.localScale = Vector3.one;
                target.Create(); Canvas.ForceUpdateCanvases();
                var cards = StandardDeck.Cards().Select((definition,index) => {
                    string suit = definition.Suit.ToString().ToLowerInvariant();
                    string rank = (int)definition.Rank <= 10 ? ((int)definition.Rank).ToString("00") : definition.Rank.ToString().Substring(0,1).ToLowerInvariant();
                    var flags = index % 13 == 0 ? CardEffects.Laminated : index % 13 == 1 ? CardEffects.Enhanced : index % 13 == 2 ? CardEffects.Ineffective : CardEffects.None;
                    var card = new CardInstance(new CardInstanceId("preview/" + index), definition.Id, effects: flags, isDisabled: index == 3);
                    var visual = CardVisualData.FromCard(card, card.Id.Value, true, _ => suit + "_" + rank);
                    return new DeckOverviewCard(visual, definition, index % 7 != 0);
                });
                var view = root.GetComponent<DeckOverviewView>(); view.Present(new DeckOverviewData(cards)); view.AdvanceMotion(.7f);
                Canvas.ForceUpdateCanvases(); foreach (var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(CapturePath)); File.WriteAllBytes(CapturePath,pixels.EncodeToPNG());
                var first = view.Cards[0].Graphic;
                File.WriteAllText("Library/DeckOverviewValidation/frame.txt", "cards=" + view.Cards.Count +
                    " rect=" + first.rectTransform.rect + " opacity=" + first.Opacity + " rendererCull=" + first.canvasRenderer.cull +
                    " material=" + first.material.name + " position=" + first.transform.position + " texture=" + first.mainTexture.name);

            }
            finally
            {
                RenderTexture.active = previous;
                if (camera != null) camera.targetTexture = null;
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name,typeof(RectTransform)).transform; rect.SetParent(parent,false);
            rect.gameObject.layer = LayerMask.NameToLayer("UI"); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero; }
        private static TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, string text, TMP_FontAsset font, float fontSize)
        {
            var label = Rect(name,parent,position,size).gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = fontSize;
            label.text = text; label.color = new Color(.87f,.93f,.89f); label.alignment = TextAlignmentOptions.Midline;
            label.enableWordWrapping = false; label.overflowMode = TextOverflowModes.Overflow; label.raycastTarget = false; return label;
        }
    }
}
#endif
