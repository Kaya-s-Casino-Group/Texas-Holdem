using System;
using System.Linq;
using CardArt;
using CardInteraction;
using PokerEngine;
using Poker.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardPresentation.Editor
{
    public sealed partial class CardZoneValidationWindow : EditorWindow
    {
        private readonly ToolWindowChrome windowChrome = new ToolWindowChrome();
        public const string PreviewName = "CardZoneView Preview";
        private const string DefaultPrefab = "Assets/CardArt/Prefabs/Card.prefab";
        [SerializeField] private CardZoneView target;
        [SerializeField] private GameObject prefab;
        [SerializeField] private CardZonePreviewModel model = new CardZonePreviewModel();
        [SerializeField] private CardZoneLayoutSettings layout = CardZoneLayoutSettings.Default;
        [SerializeField] private Vector3 raisedOffset = new Vector3(0, 0, 0.02f);
        [SerializeField] private Vector3 hoveredOffset = new Vector3(0, 0, 0.01f);
        [SerializeField] private CardAppearanceSettings appearanceSettings = CardAppearanceSettings.Default;
        [SerializeField] private CardZoneLiftSettings liftSettings = CardZoneLiftSettings.Default;
        [SerializeField] private CardZoneMotionSettings motionSettings = CardZoneMotionSettings.Default;
        [SerializeField] private CardZoneTransitionSettings transitionSettings = CardZoneTransitionSettings.Default;
        [SerializeField] private bool drawSoundEnabled = true;
        [SerializeField] private AudioClip drawSound;
        [SerializeField] private float drawSoundVolume = 1;
        [SerializeField] private bool discardSoundEnabled = true;
        [SerializeField] private AudioClip discardSound;
        [SerializeField] private float discardSoundVolume = 1;
        [SerializeField] private bool selectionSoundEnabled = true;
        [SerializeField] private AudioClip selectionSound;
        [SerializeField] private float selectionSoundVolume = 1;
        [SerializeField] private Vector3 deckLocalPosition = new Vector3(0.38f, 0.035f, 0);
        [SerializeField] private Transform deckAnchor;
        [SerializeField] private bool previewMotion = true;
        [SerializeField] private bool faceCamera;
        [SerializeField] private Camera facingCamera;
        private double lastMotionTime;
        [SerializeField] private string inspectedCardId;
        [SerializeField] private int page;
        [SerializeField] private bool initialized;
        private Vector2 scroll;
        private string error;
        private string status;

        private CardZonePreviewModel playModel;
        private bool restoringUndo;
        private CardZonePreviewModel CurrentModel => Application.isPlaying ? (playModel ?? (playModel = new CardZonePreviewModel())) : model;
        private CardZonePlayHost Host => target != null ? target.GetComponent<CardZonePlayHost>() : null;
        private HandZoneView Hand => target as HandZoneView;
        private int CurrentCapacity => Math.Min(CardZonePreviewModel.MaximumCards, target?.CardCapacity ?? CardZonePreviewModel.MaximumCards);
        private bool CanEdit => target != null && hasEditableSnapshot && (!Application.isPlaying || (Host != null && Host.IsRunning));

        [MenuItem("Tools/Cards/Card Zone Validator")]
        public static void Open()
        {
            var window = GetWindow<CardZoneValidationWindow>("牌区验证");
            window.ConfigureWindowSize(); window.Show();
        }
        [MenuItem("Tools/Cards/Create Card Zone In Current Scene")]
        public static void CreateInCurrentScene()
        {
            Open(); var window = GetWindow<CardZoneValidationWindow>();
            window.Run(() => { window.CreatePreview(); window.Frame(); }); window.Focus();
        }
        [MenuItem("Tools/Cards/Preview Gentle Row Motion")]
        public static void PreviewGentleMotion()
        {
            Open(); var window = GetWindow<CardZoneValidationWindow>();
            window.Run(() => { if (window.target == null) window.CreatePreview(); });
            window.ApplyGentleMotionPreset(); window.Focus();
        }
        [MenuItem("Tools/Cards/Deal Three Cards To Zone")]
        public static void DealThreeCards()
        {
            Open(); var window = GetWindow<CardZoneValidationWindow>();
            window.Run(() => { if (window.target == null) window.CreatePreview(); });
            window.SyncRuntimeState(); window.page = 1; window.drawMode = 1; window.scroll = Vector2.zero; window.DealFromDeck(3);
        }
        private void DealFromDeck(int count) { if (Hand == null) return; Mutate("从牌堆抽牌", () => {
            CurrentExperiment.EnsureDeck(ArtIds());
            CurrentExperiment.Draw(CurrentModel, count, DrawInsertIndex);
            InspectDrawnCard();
        }); }
        private void ApplyGentleMotionPreset() => Mutate("应用轻弧摆动", () => {
            layout.ArcHeight = 0.008f; motionSettings = CardZoneMotionSettings.Default;
            previewMotion = true; page = 2; stylePage = 1; scroll = Vector2.zero;
        });

        private void OnEnable()
        {
            ConfigureWindowSize();
            if (prefab == null) prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPrefab);
            if (model == null) model = new CardZonePreviewModel();
            if (!initialized) { ResetSample(); initialized = true; }
            if (target == null) target = RestoreTarget() ?? ResolveTarget(Selection.activeGameObject) ?? FindPreview();
            RefreshTargetSource();
            ReadSettings();
            InitializeDrawExperiments();
            if (!Application.isPlaying) LoadInitialSnapshot();
            SyncRuntimeState();
            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.delayCall += RestorePreview;
            lastMotionTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += UpdateMotionPreview;
        }
        private void ReadSettings()
        {
            if (target == null) return;
            prefab = target.CardPrefab != null ? target.CardPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPrefab); layout = target.Layout;
            appearanceSettings = target.AppearanceSettings; motionSettings = target.MotionSettings;
            faceCamera = target.FaceCamera; facingCamera = target.FacingCamera;
            if (Hand == null) return;
            raisedOffset = Hand.SelectedOffset; hoveredOffset = Hand.HoveredOffset; liftSettings = Hand.LiftSettings;
            transitionSettings = Hand.TransitionSettings; deckLocalPosition = Hand.DeckLocalPosition; deckAnchor = Hand.DeckAnchor;
            discardSettings = Hand.DiscardSettings; discardLocalPosition = Hand.DiscardLocalPosition; discardAnchor = Hand.DiscardAnchor;
            drawSoundEnabled = Hand.DrawSoundEnabled; drawSound = Hand.DrawSoundOverride; drawSoundVolume = Hand.DrawSoundVolume;
            discardSoundEnabled = Hand.DiscardSoundEnabled; discardSound = Hand.DiscardSoundOverride; discardSoundVolume = Hand.DiscardSoundVolume;
            selectionSoundEnabled = Hand.SelectionSoundEnabled; selectionSound = Hand.SelectionSoundOverride; selectionSoundVolume = Hand.SelectionSoundVolume;
        }
        private void OnDisable()
        {
            windowChrome.Release();
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnUndo;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.delayCall -= RestorePreview;
            EditorApplication.update -= UpdateMotionPreview;
            if (target != null && hasEditableSnapshot && !Application.isPlaying) target.Clear();
            ClearTransferPreviews(); SceneView.RepaintAll();
        }
        private void OnUndo()
        {
            if (target == null) target = RestoreTarget() ?? FindPreview();
            restoringUndo = true;
            try { RestorePreview(); EnsureTransferPreviews(true); if (target == transferSource || target == transferDestination) LoadInitialSnapshot(); } finally { restoringUndo = false; }
            Repaint();
        }
        private void OnLostFocus() => windowChrome.Release();
        private void ConfigureWindowSize() { minSize = new Vector2(360, 300); maxSize = new Vector2(10000, 10000); }
        private void OnHierarchyChange() => Repaint();
        private void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                drawExperiments.BeginPlay(); playModel = null;
                RememberTarget();
                if (target != null && hasEditableSnapshot) { BakeCurrentZone(); target.Clear(); }
                ClearTransferPreviews();
            }
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            { if (target == null) target = RestoreTarget() ?? FindPreview(); ReadSettings(); }
            if (state == PlayModeStateChange.EnteredPlayMode) SyncRuntimeState();
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                drawExperiments.EndPlay(); playModel = null;
                LoadInitialSnapshot();
                EditorApplication.delayCall += RestorePreview;
            }
            Repaint();
        }
        private void SyncRuntimeState()
        {
            if (Application.isPlaying && Host != null && Host.IsRunning) CurrentModel.Load(Host.Cards, clearHover: false);
        }
        private void RestorePreview()
        {
            if (this != null && target != null && Host != null && Host.HasSnapshot && hasEditableSnapshot && !EditorApplication.isPlayingOrWillChangePlaymode)
                Run(() => { target.Present(BuildPreviewZone(), model.Project, animate: false); EnsureTransferPreviews(false); });
        }
        private void UpdateMotionPreview()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)Math.Min(Math.Max(now - lastMotionTime, 0), 0.05); lastMotionTime = now;
            if (!EditorApplication.isPlayingOrWillChangePlaymode && target != null && target.isActiveAndEnabled)
            {
                target.RefreshCameraFacing();
                if (previewMotion && (target.Views.Count > 0 || target.IsTransitioning)) target.AdvanceMotion(delta * previewSpeed);
                if (previewMotion || target.FaceCamera) SceneView.RepaintAll();
            }
            AdvanceTransferPreviews(delta);
            if (now - lastAnimationRepaint > 0.1) { lastAnimationRepaint = now; Repaint(); }
        }
        private static CardZoneView FindPreview()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return null;
            return scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<CardZoneView>(true))
                .OrderByDescending(v => v.name == PreviewName).FirstOrDefault();
        }
        private string[] ArtIds()
        {
            var surface = prefab != null ? prefab.GetComponentInChildren<CardView>(true) : null;
            return surface?.Atlas == null ? new string[0] : surface.Atlas.cards
                .Where(c => !c.clipped && c.id != "card_back").Select(c => c.id).ToArray();
        }
        private void ResetSample()
        {
            var available = ArtIds(); CurrentModel.Clear();
            foreach (var id in new[] { "spades_a", "hearts_q", "clubs_j", "diamonds_a", "joker_big" })
                if (available.Contains(id)) CurrentModel.Add(id);
            if (CurrentModel.Cards.Count == 0) foreach (var id in available.Take(5)) CurrentModel.Add(id);
            inspectedCardId = CurrentModel.Cards.FirstOrDefault()?.Id;
        }
        private void CreatePreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play 后创建牌区。");
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) throw new InvalidOperationException("请先打开一个普通场景。");
            if (target != null && target.gameObject.scene != scene) SelectTarget(null);
            if (target == null)
            {
                var root = new GameObject(PreviewName); SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "创建测试牌区"); root.transform.position = new Vector3(0, 0.012f, -0.23f);
                target = Undo.AddComponent<HandZoneView>(root);
                hasEditableSnapshot = true; previewZoneId = "editor/hand/" + Guid.NewGuid().ToString("N");
                try { target.Configure(prefab, CardZoneLayoutSettings.Default); ReadSettings(); }
                catch { Undo.DestroyObjectImmediate(root); target = null; throw; }
            }
            RememberTarget(); Apply(false); Selection.activeGameObject = target.gameObject;
        }
        private void Apply(bool animate = true, string[] discardedViewIds = null)
        {
            if (target == null || !hasEditableSnapshot) return;
            target.ValidateCardCount(CurrentModel.Cards.Count);
            if (target.CardPrefab != prefab) { target.Clear(); target.Configure(prefab, layout); }
            if (Hand != null)
            {
                Hand.SetLiftSettings(liftSettings); Hand.SetTransitionSettings(transitionSettings);
                Hand.SetDrawSound(drawSound, drawSoundVolume, drawSoundEnabled); Hand.SetDeckSource(deckLocalPosition, deckAnchor);
                Hand.SetDiscardSound(discardSound, discardSoundVolume, discardSoundEnabled);
                Hand.SetSelectionSound(selectionSound, selectionSoundVolume, selectionSoundEnabled);
                Hand.SetDiscardSettings(discardSettings); Hand.SetDiscardDestination(discardLocalPosition, discardAnchor);
                Hand.SetInteractionOffsets(hoveredOffset, raisedOffset);
            }
            target.SetLayout(layout, motionSettings, animate);
            layout = target.Layout; motionSettings = target.MotionSettings;
            target.SetCameraFacing(faceCamera, facingCamera);
            target.SetAppearanceSettings(appearanceSettings);
            if (Application.isPlaying)
            {
                if (Host == null || !Host.IsRunning) throw new InvalidOperationException("没有运行中的测试牌区。");
                Host.ReplaceCards(CurrentModel.Cards.Select(card => new CardZoneCardState(
                    card.ToCard(), card.ArtId, card.FaceUp, card.Tint, card.Interaction)), animate);
            }
            else
            {
                if (Hand != null) Hand.Present(BuildPreviewZone(), model.Project, animate, discardedViewIds);
                else target.Present(BuildPreviewZone(), model.Project, animate);
                BakeCurrentZone(); EditorUtility.SetDirty(target);
            }
            SceneView.RepaintAll();
        }
        private void Frame()
        {
            if (target == null) return;
            Selection.activeGameObject = target.gameObject;
            var view = SceneView.lastActiveSceneView ?? GetWindow<SceneView>(); view.drawGizmos = true;
            view.LookAt(target.transform.position, Quaternion.Euler(65, 0, 0), Mathf.Max(layout.Size.x, layout.Size.y) * 0.85f); view.Repaint();
        }
        private void Mutate(string description, Action change, string[] discardedViewIds = null)
        {
            Run(() => {
                int undoGroup = -1;
                if (!Application.isPlaying)
                {
                    Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup();
                    Undo.RecordObject(this, description);
                    if (target != null) { Undo.RecordObject(target, description); Undo.RecordObject(target.transform, description); }
                }
                try { change(); status = null; Apply(discardedViewIds: discardedViewIds); }
                finally
                {
                    if (undoGroup >= 0) { Undo.SetCurrentGroupName(description); Undo.CollapseUndoOperations(undoGroup); }
                }
            });
        }
        private void CheckReuse()
        {
            var before = target.Views.ToDictionary(v => v.ViewId, v => v.gameObject.GetInstanceID()); Apply();
            bool same = before.Count == target.Views.Count && target.Views.All(v => before.TryGetValue(v.ViewId, out int id) && id == v.gameObject.GetInstanceID());
            if (!same) throw new InvalidOperationException("发现视觉实例被替换。");
            status = "复用检查通过：" + before.Count + " 张牌。";
        }
        private void Run(Action action)
        {
            try { action(); error = null; } catch (Exception e) { error = e.Message; }
            Repaint();
        }
        private void OnGUI()
        {
            SyncRuntimeState(); windowChrome.Draw(this);
            GUILayout.BeginArea(new Rect(0, 0, position.width, Mathf.Max(0, position.height - ToolWindowChrome.FooterHeight)));
            var oldLabelWidth = EditorGUIUtility.labelWidth;
            try
            {
                EditorGUIUtility.labelWidth = Mathf.Min(150, position.width * 0.4f);
                DrawHeader();
                using (var content = new EditorGUILayout.ScrollViewScope(scroll))
                {
                    scroll = content.scrollPosition;
                    var artIds = ArtIds();
                    if (page == 4) DrawTransferPage();
                    else using (new EditorGUI.DisabledScope(!CanEdit || artIds.Length == 0))
                    {
                        if (page == 0) DrawHandPage(artIds);
                        else if (page == 1) DrawAnimationExperiment(artIds);
                        else if (page == 2) DrawStylePage();
                        else DrawDiagnostics();
                    }
                }
                if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
                else if (!string.IsNullOrEmpty(status)) EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);
            }
            finally { EditorGUIUtility.labelWidth = oldLabelWidth; GUILayout.EndArea(); }
        }
        private void DrawHeader()
        {
            EditorGUILayout.LabelField("牌区验证", EditorStyles.boldLabel);
            DrawTargetSelector();
            EditorGUILayout.LabelField(Application.isPlaying ? "Play · 编辑运行时副本，退出后恢复" : "编辑模式 · 自动固化初始数据，支持撤销", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (target == null)
                {
                    using (new EditorGUI.DisabledScope(Application.isPlaying))
                        if (GUILayout.Button("创建牌区")) Run(CreatePreview);
                }
                else
                {
                    if (GUILayout.Button("定位牌区")) Frame();
                    using (new EditorGUI.DisabledScope(!CanEdit))
                        if (GUILayout.Button(Application.isPlaying ? "退出 Play" : "进入 Play"))
                        { if (Application.isPlaying) EditorApplication.isPlaying = false; else PlayCardZone(); }
                }
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("创建出牌区 · 五个固定槽位")) Run(CreatePlayedPreview);
            DrawPrefabExport();
            if (target == null || target.CardPrefab == null) prefab = (GameObject)EditorGUILayout.ObjectField("卡牌 Prefab", prefab, typeof(GameObject), false);
            EditorGUILayout.LabelField((Hand != null ? "手牌 " : "卡牌 ") + CurrentModel.Cards.Count + " / " + CurrentCapacity +
                "    牌堆 " + CurrentExperiment.DeckCards.Count + "    弃牌 " + DiscardCount + "    飞出 " + (Hand != null ? Hand.DepartingViews.Count : 0), EditorStyles.miniLabel);
            var labels = new[] { Hand != null ? "手牌列表" : "卡牌列表", "抽牌实验", "牌区样式", "诊断", "出牌路径" };
            int next = position.width < 440 ? GUILayout.SelectionGrid(page, labels, 2) : GUILayout.Toolbar(page, labels);
            if (next != page) { page = next; scroll = Vector2.zero; GUI.FocusControl(null); }
            EditorGUILayout.Space(6);
        }
        [SerializeField] private int stylePage;
        private void DrawStylePage()
        {
            stylePage = GUILayout.Toolbar(stylePage, new[] { "布局", "动画", "音效", "外观" });
            if (stylePage == 0) { DrawLayout(); DrawCameraFacing(); }
            else if (stylePage == 1) { if (Hand != null) { DrawLiftSettings(); DrawTransitions(); DrawDiscardSettings(); } DrawMotion(); }
            else if (stylePage == 2) DrawAudioControls();
            else DrawAppearance();
        }
        private void DrawLayout()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("牌列布局（局部 XZ）", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var next = layout;
            next.Size = EditorGUILayout.Vector2Field("宽度 / 高度", next.Size);
            next.Padding = EditorGUILayout.Vector2Field("水平 / 垂直留白", next.Padding);
            next.PreferredGap = EditorGUILayout.FloatField("期望间距", next.PreferredGap);
            if (!(target is PlayedZoneView)) next.OverlapLift = EditorGUILayout.FloatField("重叠抬高", next.OverlapLift);
            if (target == null || target.SupportsArc) next.ArcHeight = EditorGUILayout.FloatField("弧高（正值中间凸向 +Z）", next.ArcHeight);
            next.Alignment = (CardZoneAlignment)EditorGUILayout.EnumPopup("对齐", next.Alignment);
            if (EditorGUI.EndChangeCheck()) Mutate("调整测试牌区布局", () => layout = next);
            EditorGUILayout.HelpBox(target is PlayedZoneView
                ? "固定五个直排槽位，按列表顺序从左向右占位；空位保留。宽度至少容纳五张完整卡牌，间距可压缩，卡牌不重叠。弧度和持续摆动固定关闭。"
                : "布局根节点 +Z 指向圆心；牌面保持与直排相同的阅读方向。正弧高圆心在 −Z，负弧高在 +Z；摆动独立叠加。", MessageType.None);
            if (GUILayout.Button("恢复默认布局")) Mutate("恢复布局", () => layout = CardZoneLayoutSettings.Default);
            if (Hand == null) return;
            EditorGUI.BeginChangeCheck();
            var hover = EditorGUILayout.Vector3Field("悬停偏移", hoveredOffset);
            var selectedOffset = EditorGUILayout.Vector3Field("选中偏移", raisedOffset);
            if (EditorGUI.EndChangeCheck()) Mutate("调整上浮偏移", () => { hoveredOffset = hover; raisedOffset = selectedOffset; });
        }

        private void DrawCameraFacing()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("牌区朝向", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle("Y 指向摄像机", faceCamera);
            var camera = (Camera)EditorGUILayout.ObjectField("目标摄像机", facingCamera, typeof(Camera), true);
            if (EditorGUI.EndChangeCheck()) Mutate("调整牌区朝向", () => { faceCamera = enabled; facingCamera = camera; });
            EditorGUILayout.HelpBox("开启后整区 +Y 朝向相机位置，+Z 沿相机上方；未指定时使用 MainCamera。关闭后恢复原朝向。", MessageType.None);
        }

        private void DrawAppearance()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Shader 样式（卡牌属性决定开关）", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck(); var next = appearanceSettings;
            next.FoilColor = EditorGUILayout.ColorField(new GUIContent("塑封镭射颜色 HDR"), next.FoilColor, true, false, true);
            next.FoilStrength = EditorGUILayout.Slider("塑封镭射强度", next.FoilStrength, 0, 5);
            next.SealColor = EditorGUILayout.ColorField(new GUIContent("强化火漆颜色"), next.SealColor, true, false, false);
            next.SealEmissionStrength = EditorGUILayout.Slider("强化火漆发光", next.SealEmissionStrength, 0, 1);
            next.IneffectiveBrightness = EditorGUILayout.Slider("失效牌面亮度", next.IneffectiveBrightness, 0, 1);
            next.IneffectiveFoilMultiplier = EditorGUILayout.Slider("失效镭射系数", next.IneffectiveFoilMultiplier, 0, 1);
            if (EditorGUI.EndChangeCheck()) Mutate("调整卡牌 Shader 样式", () => appearanceSettings = next);
            if (GUILayout.Button("恢复默认 Shader 样式")) Mutate("恢复卡牌 Shader 样式", () => appearanceSettings = CardAppearanceSettings.Default);
        }
    }
}
