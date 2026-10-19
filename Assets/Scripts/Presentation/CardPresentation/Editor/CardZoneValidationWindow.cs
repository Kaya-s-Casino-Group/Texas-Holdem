#if UNITY_EDITOR
using CardArt;
using BattlePresentation;
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
    /// <summary>Inspects any card presentation provider and previews display-only capabilities in EditMode.</summary>
    public sealed partial class CardZoneValidationWindow : EditorWindow
    {
        private readonly ToolWindowChrome windowChrome = new ToolWindowChrome();
        [SerializeField] private CardZoneView target;
        [SerializeField] private CardZoneLayoutSettings layout = CardZoneLayoutSettings.Default;
        [SerializeField] private Vector3 raisedOffset = new Vector3(0, 0, .02f), hoveredOffset;
        [SerializeField] private CardZoneLiftSettings liftSettings = CardZoneLiftSettings.Default;
        [SerializeField] private CardZoneMotionSettings motionSettings = CardZoneMotionSettings.Default;
        [SerializeField] private CardZoneTransitionSettings transitionSettings = CardZoneTransitionSettings.Default;
        [SerializeField] private bool drawSoundEnabled = true, discardSoundEnabled = true, selectionSoundEnabled = true;
        [SerializeField] private AudioClip drawSound, discardSound, selectionSound;
        [SerializeField] private float drawSoundVolume = 1, discardSoundVolume = 1, selectionSoundVolume = 1;
        [SerializeField] private CardHapticSettings hapticSettings = CardHapticSettings.Default;
        [SerializeField] private bool reflowSoundEnabled = true;
        [SerializeField] private AudioClip reflowSound;
        [SerializeField] private float reflowSoundVolume = 1;
        [SerializeField] private bool deselectionSoundEnabled = true;
        [SerializeField] private AudioClip deselectionSound;
        [SerializeField] private float deselectionSoundVolume = 1;
        [SerializeField] private int maxSelectedCards = 5;
        [SerializeField] private Vector3 selectionDeniedOffset = new Vector3(0, 0, .004f);
        [SerializeField] private float selectionDeniedRiseSeconds = .08f, selectionDeniedReturnSeconds = .18f;
        [SerializeField] private bool selectionDeniedSoundEnabled = true;
        [SerializeField] private AudioClip selectionDeniedSound;
        [SerializeField] private float selectionDeniedSoundVolume = 1;
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
        private bool CanEdit => preview == null && target != null && !ChangingMode && (!Application.isPlaying ||
            (HasLiveBattle && Host.Match.Commands.CanInteract && !target.IsTransitioning));
        private int CurrentCapacity => target != null ? target.CardCapacity ?? 0 : 0;

        [MenuItem("工具/卡牌表现/实战牌区验证")]
        [MenuItem("工具/卡牌表现/通用牌区验证")]
        public static void Open()
        {
            var window = GetWindow<CardZoneValidationWindow>("通用牌区验证");
            window.minSize = new Vector2(520, 420); window.page = 4; window.Show();
        }
        [MenuItem("工具/卡牌表现/牌区动画调试")]
        public static void OpenAnimationLab()
        {
            Open(); var window = GetWindow<CardZoneValidationWindow>(); window.page = 4;
        }
        private void OnEnable()
        {
            minSize = new Vector2(520, 420); titleContent = new GUIContent("通用牌区验证");
            if (target == null) target = RestoreTarget() ?? ResolveTarget(Selection.activeGameObject) ?? FindTarget();
            if (frontend == null) frontend = RestorePresentation() ?? ResolvePresentation(Selection.activeGameObject) ?? (target != null ? ResolvePresentation(target.gameObject) : null);
            if (frontend == null) frontend = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).FirstOrDefault(item => item is ICardZonePresentation);
            if (frontend != null) target = frontend.GetComponent<CardZoneView>();
            page = Mathf.Clamp(page, 0, 4); ReadSettings();
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Observe;
        }
        private void OnDisable()
        {
            StopPreview(); ReleaseFrontendEditor();
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= OnSceneSaving;
            AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
            windowChrome.Release(); Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnUndo; EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.update -= Observe;
        }
        private void OnLostFocus() => windowChrome.Release();
        private void OnUndo() { StopPreview(); ReadSettings(); Repaint(); }
        private void OnHierarchyChange() => Repaint();
        private void Observe()
        {
            AdvanceEditorPreview();
            if (EditorApplication.timeSinceStartup - lastRepaint < .1) return;
            lastRepaint = EditorApplication.timeSinceStartup; Repaint();
        }
        private void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode) StopPreview();
            if (state == PlayModeStateChange.ExitingEditMode) RememberTarget();
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                if (target == null) target = RestoreTarget() ?? FindTarget();
                if (frontend == null) frontend = RestorePresentation() ?? ResolvePresentation(target != null ? target.gameObject : Selection.activeGameObject);
                if (frontend != null) target = frontend.GetComponent<CardZoneView>();
                inspectedCardId = null; ReadSettings();
            }
            Repaint();
        }
        private void ReadSettings()
        {
            if (target == null) return;
            layout = target.Layout;
            motionSettings = target.MotionSettings;
            faceCamera = target.FaceCamera; facingCamera = target.FacingCamera;
            if (Hand == null) return;
            raisedOffset = Hand.SelectedOffset; hoveredOffset = Hand.HoveredOffset; liftSettings = Hand.LiftSettings;
            transitionSettings = Hand.TransitionSettings; deckLocalPosition = Hand.DeckLocalPosition; deckAnchor = Hand.DeckAnchor;
            discardSettings = Hand.DiscardSettings; discardLocalPosition = Hand.DiscardLocalPosition; discardAnchor = Hand.DiscardAnchor;
            drawSoundEnabled = Hand.DrawSoundEnabled; drawSound = Hand.DrawSoundOverride; drawSoundVolume = Hand.DrawSoundVolume;
            discardSoundEnabled = Hand.DiscardSoundEnabled; discardSound = Hand.DiscardSoundOverride; discardSoundVolume = Hand.DiscardSoundVolume;
            selectionSoundEnabled = Hand.SelectionSoundEnabled; selectionSound = Hand.SelectionSoundOverride; selectionSoundVolume = Hand.SelectionSoundVolume;
            hapticSettings = Hand.HapticSettings;
            reflowSoundEnabled = Hand.ReflowSoundEnabled; reflowSound = Hand.ReflowSound; reflowSoundVolume = Hand.ReflowSoundVolume;
            deselectionSoundEnabled = Hand.DeselectionSoundEnabled; deselectionSound = Hand.DeselectionSound; deselectionSoundVolume = Hand.DeselectionSoundVolume;
            maxSelectedCards = Hand.MaxSelectedCards; selectionDeniedOffset = Hand.SelectionDeniedOffset;
            selectionDeniedRiseSeconds = Hand.SelectionDeniedRiseSeconds; selectionDeniedReturnSeconds = Hand.SelectionDeniedReturnSeconds;
            selectionDeniedSoundEnabled = Hand.SelectionDeniedSoundEnabled; selectionDeniedSound = Hand.SelectionDeniedSound; selectionDeniedSoundVolume = Hand.SelectionDeniedSoundVolume;
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
                Hand.SetHapticSettings(hapticSettings);
                Hand.SetReflowSound(reflowSound, reflowSoundVolume, reflowSoundEnabled);
                Hand.SetDeselectionSound(deselectionSound, deselectionSoundVolume, deselectionSoundEnabled);
                Hand.SetMaxSelectedCards(maxSelectedCards);
                Hand.SetSelectionDeniedAnimation(selectionDeniedOffset, selectionDeniedRiseSeconds, selectionDeniedReturnSeconds);
                Hand.SetSelectionDeniedSound(selectionDeniedSound, selectionDeniedSoundVolume, selectionDeniedSoundEnabled);
                Hand.SetDiscardSettings(discardSettings); Hand.SetDiscardDestination(discardLocalPosition, discardAnchor);
                Hand.SetInteractionOffsets(hoveredOffset, raisedOffset);
            }
            target.SetLayout(layout, motionSettings, animate); layout = target.Layout; motionSettings = target.MotionSettings;
            target.SetCameraFacing(faceCamera, facingCamera);
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
            var selected = frontend != null ? frontend.gameObject : target != null ? target.gameObject : null;
            if (selected == null) return;
            Selection.activeGameObject = selected;
            if (target == null) { SceneView.lastActiveSceneView?.FrameSelected(); return; }
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
                EditorGUILayout.LabelField("通用牌区验证", EditorStyles.boldLabel); DrawTargetSelector();
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(target == null && frontend == null)) if (GUILayout.Button("定位牌区")) Frame();
                    if (GUILayout.Button("读取表现参数")) ReadSettings();
                }
                EditorGUILayout.HelpBox(Application.isPlaying ? "Play 中观察实际展示数据；通过 Game 内 UI 提交规则操作。" : "表现预览使用临时展示数据，可在 Scene / Game 视图观察。停止或退出预览时恢复原展示。", MessageType.Info);
                if (target != null) DrawPrefabExport();
                page = GUILayout.Toolbar(page, new[] { "展示数据", "表现参数", "视觉诊断", "出牌路径", "表现预览" });
                using (var area = new EditorGUILayout.ScrollViewScope(scroll))
                {
                    scroll = area.scrollPosition;
                    if (page == 0) { DrawDisplaySnapshot(); DrawLiveCards(); }
                    else if (page == 1)
                    {
                        if (Application.isPlaying && !CanEdit) EditorGUILayout.HelpBox("等待实战动画结束且允许交互时调整参数。", MessageType.Info);
                        if (target != null) { using (new EditorGUI.DisabledScope(!CanEdit)) DrawStylePage(); }
                        DrawFrontendSettings();
                    }
                    else if (page == 2) { DrawCapabilityDiagnostics(); DrawDiagnostics(); }
                    else if (page == 3) DrawTransferPage();
                    else DrawPresentationPreview();
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
            stylePage = GUILayout.Toolbar(Mathf.Clamp(stylePage, 0, 2), new[] { "布局", "动画", "音效" });
            if (stylePage == 0) { DrawLayout(); DrawCameraFacing(); }
            else if (stylePage == 1)
            {
                if (Hand != null) { DrawListAnimationToggle(); DrawLiftSettings(); DrawReflow(); DrawTransitions(); DrawDiscardSettings(); }
                DrawMotion();
            }
            else if (stylePage == 2) DrawAudioControls();
        }
        private void DrawLayout()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("牌列布局（局部 XZ）", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var next = layout;
            next.Size = EditorGUILayout.Vector2Field("宽度 / 高度", next.Size);
            next.FitCardsToZone = EditorGUILayout.Toggle("卡牌随区域缩放", next.FitCardsToZone);
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


    }
}
#endif
