#if UNITY_EDITOR
using CardInteraction;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardPresentation.Editor
{
    public sealed partial class CardZoneValidationWindow
    {
        [MenuItem("Tools/Cards/Preview Snappy Lift")]
        public static void PreviewSnappyLift()
        {
            Open(); var window = GetWindow<CardZoneValidationWindow>();
            window.Run(() => {
                var selected = ResolveTarget(Selection.activeGameObject) as HandZoneView;
                if (selected != null) window.SelectTarget(selected);
            });
            if (window.Hand == null || !window.CanEdit)
            { window.Run(() => { throw new System.InvalidOperationException("请先选择可编辑的手牌区。"); }); return; }
            window.ApplySnappyLiftPreset(); window.Focus();
        }

        private void ApplySnappyLiftPreset() => Mutate("应用快速上浮 / 回落惯性", () => {
            liftSettings = CardZoneLiftSettings.Default; page = 2; stylePage = 1; scroll = Vector2.zero;
        });

        [MenuItem("Tools/Cards/Preview Mouse Interaction")]
        [MenuItem("Tools/Cards/Bake Card Zone For Play")]
        public static void BakeCardZoneForPlay()
        {
            Open(); var window = GetWindow<CardZoneValidationWindow>();
            window.Run(() => {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new System.InvalidOperationException("请退出 Play 后修改固化数据。");
                if (window.target == null) window.CreatePreview();
                window.Apply(false); window.page = 0; window.scroll = Vector2.zero;
                window.status = "牌区和卡牌数据已固化到场景组件；进入 Play 后在 Game 视图操作。";
            });
        }

        [MenuItem("Tools/Cards/Play Card Zone")]
        public static void PlayCardZone()
        {
            Open(); var window = GetWindow<CardZoneValidationWindow>();
            window.Run(() => {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (window.target == null) window.CreatePreview();
                window.Apply(false);
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                EditorApplication.isPlaying = true;
            });
        }

        private void BakeCurrentZone()
        {
            if (target == null || !hasEditableSnapshot || Application.isPlaying) return;
            var host = target.GetComponent<CardZonePlayHost>();
            if (host == null) host = Undo.AddComponent<CardZonePlayHost>(target.gameObject);
            var match = host.Match != null ? host.Match : CardMatchSetup.GetOrCreate(target.gameObject.scene);
            if (!restoringUndo) { Undo.RecordObject(host, "绑定对局牌区"); Undo.RecordObject(match, "编辑对局启动数据"); }
            if (target.CompareTag("EditorOnly"))
            {
                Undo.RecordObject(target.gameObject, "牌区进入运行时"); target.gameObject.tag = "Untagged";
            }
            host.Capture(BuildPreviewZone(), model.Cards.Select(card => new CardZoneCardState(
                card.ToCard(), card.ArtId, card.FaceUp, card.Tint, card.Interaction)), facingCamera, discardedCards,
                Hand != null ? CurrentExperiment.DeckCards.Select(card => new CardZoneCardState(
                    card.ToCard(), card.ArtId, card.FaceUp, card.Tint, card.Interaction)) : null, owner: match);
            if (Hand != null) host.SetSortOrder(handSortOrder);
            if (!restoringUndo && target is HandZoneView) CardHoverPresenterSetup.Register(host, facingCamera);
            if (!restoringUndo && target is HandZoneView && HandActionPanelSetup.HasPrefab) HandActionPanelSetup.Register(host);
            EditorUtility.SetDirty(match); EditorUtility.SetDirty(host); EditorUtility.SetDirty(target.gameObject);
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
        }

        private void DrawLiftSettings()
        {
            EditorGUILayout.LabelField("悬停缩放 / 选中上浮", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck(); var next = liftSettings;
            next.Enabled = EditorGUILayout.Toggle("交互惯性动画", next.Enabled);
            next.RaiseSeconds = EditorGUILayout.FloatField("放大 / 上浮（秒）", next.RaiseSeconds);
            next.LowerSeconds = EditorGUILayout.FloatField("缩小 / 回落（秒）", next.LowerSeconds);
            next.Overshoot = EditorGUILayout.Slider("惯性强度", next.Overshoot, 0, 3);
            if (EditorGUI.EndChangeCheck()) Mutate("调整上浮动画", () => { next.Validate(); liftSettings = next; });
            EditorGUILayout.HelpBox("悬停面积扩大 20%，不产生位移；缩放与选中上浮独立叠加。时长包含越过目标后的回稳；选中上浮使用更明显的回弹（强度 ×1.35，上限 3），摆角同步归零。0 关闭全部回弹；快速反向从当前姿态接续。", MessageType.None);
            if (GUILayout.Button("应用快速惯性（上浮 0.12 / 回落 0.14 秒）")) ApplySnappyLiftPreset();
            if (GUILayout.Button("选中沿局部 +Z 上浮"))
                Mutate("设置 Z 方向上浮", () => {
                    hoveredOffset = Vector3.zero;
                    raisedOffset = new Vector3(0, 0, raisedOffset.magnitude > 0 ? raisedOffset.magnitude : 0.02f);
                });
        }
    }
}

#endif
