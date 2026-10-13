#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CardInteraction;
using UnityEditor;
using UnityEngine;

namespace CardPresentation.Editor
{
    public sealed partial class CardZoneValidationWindow
    {
        [SerializeField] private CardZoneDiscardSettings discardSettings = CardZoneDiscardSettings.Default;
        [SerializeField] private Vector3 discardLocalPosition = new Vector3(-0.38f, 0.035f, 0);
        [SerializeField] private Transform discardAnchor;
        [SerializeField] private List<CardZoneCardState> discardedCards = new List<CardZoneCardState>();
        private int DiscardCount => Application.isPlaying && Host != null ? Host.DiscardedCards.Count : discardedCards.Count;

        [MenuItem("Tools/Cards/Discard Selected Cards")]
        public static void DiscardSelectedCards()
        {
            Open(); var window = GetWindow<CardZoneValidationWindow>();
            window.SyncRuntimeState(); window.DiscardSelection();
        }

        private void LoadInitialSnapshot()
        {
            if (Host == null || !Host.HasSnapshot) return;
            model.Load(Host.InitialCards);
            discardedCards = Host.InitialDiscardedCards.Select(card => card.Copy()).ToList();
            if (Host.HasDeckSnapshot) CurrentExperiment.LoadDeck(Host.InitialDeckCards);
        }

        private void DiscardSelection()
        {
            if (!CanEdit || Hand == null) return;
            if (Application.isPlaying)
            {
                Run(() => {
                    int count = Host.DiscardSelected(); SyncRuntimeState();
                    status = "已弃掉 " + count + " 张牌。";
                });
                return;
            }
            SyncRuntimeState();
            CardZoneDiscardBatch batch = null;
            Run(() => {
                var source = BuildPreviewZone();
                var discardZone = CardZonePlayHost.BuildDiscardZone(source.Id.Value, source.ControllerId?.Value, discardedCards);
                batch = new CardZoneDiscardBatch(source, model.Cards.Select(card => new CardZoneCardState(
                    card.ToCard(), card.ArtId, card.FaceUp, card.Tint, card.Interaction)), discardZone);
            });
            if (batch == null || batch.Discarded.Count == 0) return;
            Mutate("弃掉选中卡牌", () => {
                model.Load(batch.Remaining, clearHover: false); discardedCards.AddRange(batch.Discarded);
            }, batch.Discarded.Select(card => card.Id).ToArray());
        }

        private void DrawDiscardAction()
        {
            if (Hand == null) return;
            var selected = CurrentModel.Cards.Where(card => card.IsSelected).ToArray();
            bool blocked = selected.Any(card => !card.ToCard().CanDiscard);
            EditorGUILayout.LabelField("选中后的操作", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(selected.Length == 0 || blocked))
                if (GUILayout.Button("弃掉选中 · " + selected.Length + " 张", GUILayout.Height(28))) DiscardSelection();
            if (blocked) EditorGUILayout.HelpBox("选中集合中有禁用牌，请先取消这些牌的选中。失效牌可以弃掉。", MessageType.Info);
            else EditorGUILayout.LabelField("Play 在 Game 中选牌；编辑模式勾选卡牌的“选中 / 待提交”。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("弃牌堆 " + DiscardCount + " 张 · 飞出中 " + (Hand != null ? Hand.DepartingViews.Count : 0), EditorStyles.miniLabel);
            EditorGUILayout.Space(8);
        }

        private void DrawDiscardSettings()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("弃牌 / 离场动画", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck(); var next = discardSettings;
            var anchor = (Transform)EditorGUILayout.ObjectField("弃牌堆锚点（可选）", discardAnchor, typeof(Transform), true);
            var localPosition = EditorGUILayout.Vector3Field("弃牌堆局部位置", discardLocalPosition);
            next.FlightSeconds = EditorGUILayout.FloatField("飞出时间（秒）", next.FlightSeconds);
            next.Interval = EditorGUILayout.FloatField("逐张间隔（秒）", next.Interval);
            next.FlightLift = EditorGUILayout.FloatField("飞行抬高", next.FlightLift);
            if (EditorGUI.EndChangeCheck()) Mutate("调整弃牌动画", () => {
                next.Validate(); discardSettings = next; discardAnchor = anchor; discardLocalPosition = localPosition;
            });
            EditorGUILayout.HelpBox("按原手牌顺序离场；可见牌翻到牌背，不可见牌保持牌背。到达后消失，无回弹。粉红色 Gizmo 为弃牌终点，锚点优先；每批起飞时记录终点。", MessageType.None);
        }
    }
}

#endif
