using System;
using System.Linq;
using CardInteraction;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardPresentation.Editor
{
    public sealed partial class CardZoneValidationWindow
    {
        [SerializeField] private HandZoneView transferSource;
        [SerializeField] private PlayedZoneView transferDestination;
        private CardZoneTransferPresenter TransferRoute => transferSource != null ? transferSource.GetComponent<CardZoneTransferPresenter>() : null;

        [MenuItem("Tools/Cards/Configure Hand To Played Route")]
        public static void ConfigureHandToPlayedRoute()
        {
            Open(); var window = GetWindow<CardZoneValidationWindow>();
            window.Run(() => {
                window.FindTransferEndpoints(); window.page = 4; window.scroll = Vector2.zero;
                if (window.transferSource == null) throw new InvalidOperationException("请先选择一个手牌区。");
                if (window.transferDestination == null && !EditorApplication.isPlayingOrWillChangePlaymode) window.CreateEmptyTransferDestination();
                if (window.TransferRoute == null && !EditorApplication.isPlayingOrWillChangePlaymode) window.SaveTransferRoute(CardZoneTransferSettings.Default, null);
            });
        }

        private void FindTransferEndpoints()
        {
            if (transferSource == null) transferSource = Hand;
            if (transferSource == null && target != null)
                transferSource = target.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HandZoneView>(true)).FirstOrDefault();
            if (TransferRoute != null) transferDestination = TransferRoute.Destination;
            else if (transferDestination == null) transferDestination = target as PlayedZoneView;
        }

        private void DrawTransferPage()
        {
            FindTransferEndpoints();
            EditorGUILayout.LabelField("手牌 → 出牌区", EditorStyles.boldLabel);
            var from = (HandZoneView)EditorGUILayout.ObjectField("源手牌区", transferSource, typeof(HandZoneView), true);
            if (from != transferSource) { transferSource = from; transferDestination = TransferRoute != null ? TransferRoute.Destination : null; }
            var to = (PlayedZoneView)EditorGUILayout.ObjectField("目标出牌区", transferDestination, typeof(PlayedZoneView), true);
            if (to != transferDestination)
            {
                transferDestination = to;
                if (from != null && to != null) Run(() => SaveTransferRoute(TransferRoute != null ? TransferRoute.Settings : CardZoneTransferSettings.Default, TransferRoute?.ControlPoint));
            }
            using (new EditorGUI.DisabledScope(transferSource == null || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("新建空出牌区并连接")) Run(CreateEmptyTransferDestination);
            var route = TransferRoute;
            if (route == null || route.Destination != transferDestination)
            {
                using (new EditorGUI.DisabledScope(transferSource == null || transferDestination == null || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("建立出牌路径")) Run(() => SaveTransferRoute(CardZoneTransferSettings.Default, null));
                return;
            }
            EditorGUI.BeginChangeCheck(); var next = route.Settings;
            next.Enabled = EditorGUILayout.Toggle("出牌动画", next.Enabled);
            next.FlightSeconds = EditorGUILayout.FloatField("飞行时间（秒）", next.FlightSeconds);
            next.Interval = EditorGUILayout.FloatField("逐张间隔（秒）", next.Interval);
            next.LandingPause = EditorGUILayout.FloatField("全员落位后停顿", next.LandingPause);
            next.ReorderSeconds = EditorGUILayout.FloatField("整理排列时间（秒）", next.ResolvedReorderSeconds);
            var control = (Transform)EditorGUILayout.ObjectField("曲线控制点（可选）", route.ControlPoint, typeof(Transform), true);
            using (new EditorGUI.DisabledScope(control != null)) next.ArcHeight = EditorGUILayout.FloatField("默认弧高（世界单位）", next.ArcHeight);
            if (EditorGUI.EndChangeCheck()) Run(() => SaveTransferRoute(next, control));
            EditorGUILayout.HelpBox("每次可打出 1–5 张，受剩余容量限制。从左到右依次起飞，先反向落位形成交叉；等本批全部到齐后，再一起整理回原手牌顺序。单张直接落位。绿线为交叉飞行，黄线为整理方向；可见性和卡牌效果不变。两区使用相同卡牌 Prefab 和等比缩放。", MessageType.None);
            var sourceHost = transferSource.GetComponent<CardZonePlayHost>();
            var targetHost = transferDestination.GetComponent<CardZonePlayHost>();
            var cards = sourceHost != null ? (Application.isPlaying ? sourceHost.Cards : sourceHost.InitialCards) : null;
            int count = cards?.Count(card => card.Interaction.IsSelected) ?? 0;
            int occupied = targetHost != null ? (Application.isPlaying ? targetHost.Cards.Count : targetHost.InitialCards.Count) : 0;
            bool blocked = cards != null && cards.Any(card => card.Interaction.IsSelected && !card.Card.CanPlay);
            EditorGUILayout.LabelField("选中 " + count + " 张 · 目标 " + occupied + " / 5 · " + (route.IsTransferring ? "飞行中" : "空闲"));
            using (new EditorGUI.DisabledScope(sourceHost == null || targetHost == null || count == 0 || blocked || occupied + count > 5))
                if (GUILayout.Button("打出选中的牌", GUILayout.Height(30))) Run(SubmitPlayedSelection);
            if (blocked || occupied + count > 5) EditorGUILayout.HelpBox("禁用牌或容量不足会拒绝整批出牌。", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("查看手牌列表")) { SelectTarget(transferSource); page = 0; }
                if (GUILayout.Button("查看出牌列表")) { SelectTarget(transferDestination); page = 0; }
                if (GUILayout.Button("定位路径")) Selection.activeGameObject = route.gameObject;
            }
            if (!Application.isPlaying)
            {
                previewMotion = EditorGUILayout.Toggle("播放预览", previewMotion);
                previewSpeed = EditorGUILayout.Slider("预览速度", previewSpeed, .1f, 2);
                if (GUILayout.Button("暂停并推进 0.05 秒")) Run(() => {
                    previewMotion = false; transferSource.AdvanceMotion(.05f); transferDestination.AdvanceMotion(.05f); SceneView.RepaintAll();
                });
            }
            if (GUILayout.Button("立即完成出牌飞行")) route.CompleteTransfers();
            EditorGUILayout.LabelField(Application.isPlaying ? "在 Game 中选牌，再从这里提交；运行时也可由 UI 调用 PlaySelected。" :
                "在手牌列表勾选“选中 / 待提交”。编辑模式提交会修改两区初始卡牌列表，可撤销；Play 只改运行时副本。", EditorStyles.wordWrappedMiniLabel);
        }

        private void SaveTransferRoute(CardZoneTransferSettings settings, Transform control)
        {
            settings.Validate();
            var route = TransferRoute;
            if (route == null)
            {
                if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后建立路径组件。");
                route = Undo.AddComponent<CardZoneTransferPresenter>(transferSource.gameObject);
            }
            if (!Application.isPlaying) Undo.RecordObject(route, "调整出牌路径");
            route.Configure(transferSource, transferDestination, settings, control);
            if (!Application.isPlaying) { EditorUtility.SetDirty(route); EditorSceneManager.MarkSceneDirty(route.gameObject.scene); }
            EnsureTransferPreviews(false); SceneView.RepaintAll();
        }

        private void CreateEmptyTransferDestination()
        {
            if (transferSource == null || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式选择手牌区。");
            int group = Undo.GetCurrentGroup();
            var root = new GameObject("PlayedZoneView Preview");
            SceneManager.MoveGameObjectToScene(root, transferSource.gameObject.scene); Undo.RegisterCreatedObjectUndo(root, "创建空出牌区与路径");
            root.transform.SetPositionAndRotation(transferSource.transform.TransformPoint(new Vector3(0, 0, .28f)), transferSource.transform.rotation);
            var played = Undo.AddComponent<PlayedZoneView>(root);
            try { played.Configure(transferSource.CardPrefab, CardZoneLayoutSettings.Default); }
            catch { Undo.DestroyObjectImmediate(root); throw; }
            var host = Undo.AddComponent<CardZonePlayHost>(root);
            host.Capture(new CardZone(new ZoneId("editor/played/" + Guid.NewGuid().ToString("N")), CardZoneKinds.Played, null, new CardInstanceId[0]), new CardZoneCardState[0]);
            transferDestination = played;
            SaveTransferRoute(CardZoneTransferSettings.Default, null);
            Undo.CollapseUndoOperations(group);
        }

        private void SubmitPlayedSelection()
        {
            var sourceHost = transferSource.GetComponent<CardZonePlayHost>(); var targetHost = transferDestination.GetComponent<CardZonePlayHost>();
            if (Application.isPlaying)
            {
                int count = sourceHost.PlaySelected(targetHost, TransferRoute); SyncRuntimeState(); status = "已打出 " + count + " 张牌。";
            }
            else
            {
                EnsureTransferPreviews(false);
                Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
                Undo.RecordObjects(new UnityEngine.Object[] { this, sourceHost, targetHost }, "打出选中卡牌");
                try
                {
                    int count = sourceHost.PlaySelectedPreview(targetHost, TransferRoute);
                    LoadInitialSnapshot(); EditorUtility.SetDirty(sourceHost); EditorUtility.SetDirty(targetHost);
                    EditorSceneManager.MarkSceneDirty(sourceHost.gameObject.scene); EditorSceneManager.MarkSceneDirty(targetHost.gameObject.scene);
                    status = "已打出 " + count + " 张牌；两区卡牌数据已更新，可撤销。";
                }
                finally { Undo.CollapseUndoOperations(group); }
            }
            SceneView.RepaintAll();
        }

        private void EnsureTransferPreviews(bool restore)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var zone in new CardZoneView[] { transferSource, transferDestination })
            {
                if (zone == null) continue;
                var host = zone.GetComponent<CardZonePlayHost>();
                if (host == null || !host.HasSnapshot || (!restore && zone.Data != null)) continue;
                zone.CompleteTransitions();
                zone.Present(new CardZoneViewData(host.Zone.Id, host.InitialCards.Select(card => card.Project())), false);
            }
        }
        private void AdvanceTransferPreviews(float delta)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var zone in new CardZoneView[] { transferSource, transferDestination })
            {
                if (zone == null || zone == target || !zone.isActiveAndEnabled) continue;
                zone.RefreshCameraFacing(); if (previewMotion) zone.AdvanceMotion(delta * previewSpeed);
            }
        }
        private void ClearTransferPreviews()
        {
            if (Application.isPlaying) return;
            TransferRoute?.CompleteTransfers();
            foreach (var zone in new CardZoneView[] { transferSource, transferDestination })
                if (zone != null && zone != target && zone.GetComponent<CardZonePlayHost>()?.HasSnapshot == true) zone.Clear();
        }
    }
}
