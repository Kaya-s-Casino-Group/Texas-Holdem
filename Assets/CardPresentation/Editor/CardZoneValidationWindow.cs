#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CardInteraction;
using Poker.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardPresentation.Editor
{
    /// <summary>Observes a live gameplay table and edits presentation settings only. Never owns cards or zones.</summary>
    public sealed partial class CardZoneValidationWindow : EditorWindow
    {
        private readonly ToolWindowChrome windowChrome = new ToolWindowChrome();
        [SerializeField] private CardZoneView target;
        [SerializeField] private CardZoneLayoutSettings layout = CardZoneLayoutSettings.Default;
        [SerializeField] private Vector3 raisedOffset = new Vector3(0, 0, .02f), hoveredOffset;
        [SerializeField] private CardAppearanceSettings appearanceSettings = CardAppearanceSettings.Default;
        [SerializeField] private CardZoneLiftSettings liftSettings = CardZoneLiftSettings.Default;
        [SerializeField] private CardZoneMotionSettings motionSettings = CardZoneMotionSettings.Default;
        [SerializeField] private CardZoneTransitionSettings transitionSettings = CardZoneTransitionSettings.Default;
        [SerializeField] private bool drawSoundEnabled = true, discardSoundEnabled = true, selectionSoundEnabled = true;
        [SerializeField] private AudioClip drawSound, discardSound, selectionSound;
        [SerializeField] private float drawSoundVolume = 1, discardSoundVolume = 1, selectionSoundVolume = 1;
        [SerializeField] private Vector3 deckLocalPosition = new Vector3(.38f, .035f, 0);
        [SerializeField] private Transform deckAnchor;
        [SerializeField] private int page;
        private Vector2 scroll;
        private string error, status, inspectedCardId;
        private double lastRepaint;
        private CardZonePlayHost Host => target != null ? target.GetComponent<CardZonePlayHost>() : null;
        private HandZoneView Hand => target as HandZoneView;
        private bool ChangingMode => EditorApplication.isPlayingOrWillChangePlaymode != Application.isPlaying;
        public bool HasLiveBattle => Application.isPlaying && !ChangingMode && Host != null && Host.IsRunning &&
            Host.Match != null && Host.Match.Commands != null;
        public IReadOnlyList<CardZoneCardState> CurrentCards => HasLiveBattle ? Host.Cards : Array.Empty<CardZoneCardState>();
        private bool CanEdit => target != null && !ChangingMode && (!Application.isPlaying ||
            (HasLiveBattle && Host.Match.Commands.CanInteract && !target.IsTransitioning));
        private int CurrentCapacity => target != null ? target.CardCapacity ?? 0 : 0;

        [MenuItem("Tools/Cards/Card Zone Validator")]
        public static void Open()
        {
            var window = GetWindow<CardZoneValidationWindow>("实战牌区验证");
            window.minSize = new Vector2(360, 300); window.Show();
        }
        [MenuItem("Tools/Cards/Card Zone Animation Lab")]
        public static void OpenAnimationLab()
        {
            Open(); var window = GetWindow<CardZoneValidationWindow>(); window.page = 1; window.stylePage = 1;
        }
        private void OnEnable()
        {
            minSize = new Vector2(360, 300); titleContent = new GUIContent("实战牌区验证");
            if (target == null) target = RestoreTarget() ?? ResolveTarget(Selection.activeGameObject) ?? FindTarget();
            page = Mathf.Clamp(page, 0, 3); ReadSettings();
            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Observe;
        }
        private void OnDisable()
        {
            windowChrome.Release(); Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnUndo; EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.update -= Observe;
        }
        private void OnLostFocus() => windowChrome.Release();
        private void OnUndo() { ReadSettings(); Repaint(); }
        private void OnHierarchyChange() => Repaint();
        private void Observe()
        {
            if (EditorApplication.timeSinceStartup - lastRepaint < .1) return;
            lastRepaint = EditorApplication.timeSinceStartup; Repaint();
        }
        private void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) RememberTarget();
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                if (target == null) target = RestoreTarget() ?? FindTarget();
                inspectedCardId = null; ReadSettings();
            }
            Repaint();
        }
        private void ReadSettings()
        {
            if (target == null) return;
            layout = target.Layout;
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

        private static CardZoneView FindTarget()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return null;
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardZonePlayHost>(true))
                .OrderByDescending(host => host.Match != null && host.Match.IsRuntimeManaged)
                .Select(host => host.GetComponent<CardZoneView>()).FirstOrDefault(view => view != null);
        }
        // Settings are applied to the existing view; its identity, Data and gameplay table are never projected or replaced here.
        private void Apply(bool animate = true)
        {
            if (!CanEdit) return;
            if (Hand != null)
            {
                Hand.SetLiftSettings(liftSettings); Hand.SetTransitionSettings(transitionSettings);
                Hand.SetDrawSound(drawSound, drawSoundVolume, drawSoundEnabled); Hand.SetDeckSource(deckLocalPosition, deckAnchor);
                Hand.SetDiscardSound(discardSound, discardSoundVolume, discardSoundEnabled);
                Hand.SetSelectionSound(selectionSound, selectionSoundVolume, selectionSoundEnabled);
                Hand.SetDiscardSettings(discardSettings); Hand.SetDiscardDestination(discardLocalPosition, discardAnchor);
                Hand.SetInteractionOffsets(hoveredOffset, raisedOffset);
            }
            target.SetLayout(layout, motionSettings, animate); layout = target.Layout; motionSettings = target.MotionSettings;
            target.SetCameraFacing(faceCamera, facingCamera); target.SetAppearanceSettings(appearanceSettings);
        }
        private void Mutate(string description, Action change)
        {
            if (!CanEdit) return;
            Run(() => {
                if (!Application.isPlaying) Undo.RecordObject(target, description);
                try { change(); Apply(Application.isPlaying); status = null; }
                catch { ReadSettings(); throw; }
                if (!Application.isPlaying)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target); EditorUtility.SetDirty(target);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                }
            });
        }
        private void Frame()
        {
            if (target == null) return;
            Selection.activeGameObject = target.gameObject;
            var sceneView = SceneView.lastActiveSceneView ?? GetWindow<SceneView>();
            sceneView.Frame(new Bounds(target.transform.position, new Vector3(layout.Size.x, .1f, layout.Size.y)), false);
        }
        private void Run(Action action)
        {
            try { action(); error = null; } catch (Exception exception) { error = exception.Message; }
            Repaint();
        }
        public static void RefreshMatchInCurrentWindow()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<CardZoneValidationWindow>()) { window.ReadSettings(); window.Repaint(); }
        }
        private void OnGUI()
        {
            windowChrome.Draw(this);
            GUILayout.BeginArea(new Rect(0, 0, position.width, Mathf.Max(0, position.height - ToolWindowChrome.FooterHeight)));
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            try
            {
                EditorGUIUtility.labelWidth = Mathf.Min(150, position.width * .4f);
                EditorGUILayout.LabelField("实战牌区验证", EditorStyles.boldLabel); DrawTargetSelector();
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(target == null)) if (GUILayout.Button("定位牌区")) Frame();
                    if (GUILayout.Button("读取表现参数")) ReadSettings();
                }
                if (!HasLiveBattle) EditorGUILayout.HelpBox("进入实际牌局的 Play 后读取卡牌。编辑模式只调整表现参数；出牌、弃牌和排序请使用 Game 内的 UI。", MessageType.Info);
                DrawPrefabExport();
                page = GUILayout.Toolbar(page, new[] { "实战卡牌", "表现参数", "视觉诊断", "出牌路径" });
                using (var area = new EditorGUILayout.ScrollViewScope(scroll))
                {
                    scroll = area.scrollPosition;
                    if (page == 0) DrawLiveCards();
                    else if (page == 1)
                    {
                        if (Application.isPlaying && !CanEdit) EditorGUILayout.HelpBox("等待实战动画结束且允许交互时调整参数。", MessageType.Info);
                        using (new EditorGUI.DisabledScope(!CanEdit)) DrawStylePage();
                    }
                    else if (page == 2) DrawDiagnostics();
                    else DrawTransferPage();
                }
                if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
                else if (!string.IsNullOrEmpty(status)) EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);
            }
            finally { EditorGUIUtility.labelWidth = previousLabelWidth; GUILayout.EndArea(); }
        }
        [SerializeField] private bool faceCamera;
        [SerializeField] private Camera facingCamera;
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
            if (EditorGUI.EndChangeCheck()) Mutate("调整牌区布局", () => layout = next);
            EditorGUILayout.HelpBox(target is PlayedZoneView
                ? "固定五个直排槽位，按列表顺序从左向右占位；空位保留。宽度至少容纳五张完整卡牌，间距可压缩，卡牌不重叠。弧度和持续摆动固定关闭。"
                : "布局根节点 +Z 指向圆心；牌面保持与直排相同的阅读方向。正弧高圆心在 −Z，负弧高在 +Z；摆动独立叠加。", MessageType.None);
            if (GUILayout.Button("恢复默认布局")) Mutate("恢复布局", () => layout = CardZoneLayoutSettings.Default);
            if (Hand == null) return;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("悬停", "面积 +20%（等比缩放约 1.0954，不上浮）");
            var selectedOffset = EditorGUILayout.Vector3Field("选中偏移", raisedOffset);
            if (EditorGUI.EndChangeCheck()) Mutate("调整上浮偏移", () => { hoveredOffset = Vector3.zero; raisedOffset = selectedOffset; });
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
#endif
