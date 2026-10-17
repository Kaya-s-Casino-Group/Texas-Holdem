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
        private void DrawLiveCards()
        {
            if (!HasLiveBattle) return;
            EditorGUILayout.HelpBox("直接读取当前牌局，卡牌身份、牌面、属性和归属只读。使用 Game 内 UI 推进牌局后自动刷新。", MessageType.None);
            DrawCardRows("当前牌区", CurrentCards);
            DrawCardRows("抽牌堆（牌顶在前）", Host.DeckCards.Reverse().ToArray());
            DrawCardRows("弃牌堆", Host.DiscardedCards);
        }
        private void DrawCardRows(string title, IReadOnlyList<CardZoneCardState> cards)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField(title + " · " + cards.Count + " 张", EditorStyles.boldLabel);
            for (int index = 0; index < cards.Count; ++index)
            {
                var card = cards[index];
                EditorGUILayout.LabelField((index + 1) + ". " + (card.FaceUp ? card.Card.DefinitionId.Value : "隐藏牌"),
                    "效果 " + card.Card.Effects + (card.Card.IsDisabled ? " · 禁用" : "") + (card.Interaction.IsSelected ? " · 已选中" : ""));
                EditorGUILayout.SelectableLabel(card.Id, EditorStyles.miniLabel, GUILayout.Height(18));
            }
        }
        private void DrawDiagnostics()
        {
            if (!HasLiveBattle) return;
            var cards = CurrentCards;
            EditorGUILayout.LabelField("当前 View / 数据数量", target.Views.Count + " / " + cards.Count);
            EditorGUILayout.LabelField("牌区过渡", target.IsTransitioning ? "进行中" : "已落位");
            if (Hand != null) EditorGUILayout.LabelField("飞出中的视觉", Hand.DepartingViews.Count.ToString());
            if (cards.Count == 0) { EditorGUILayout.LabelField("当前牌区为空"); return; }
            int index = cards.ToList().FindIndex(card => card.Id == inspectedCardId);
            index = EditorGUILayout.Popup("检查实际卡牌", Mathf.Max(0, index), cards.Select((card, i) => (i + 1) + ". " + (card.FaceUp ? card.Card.DefinitionId.Value : "隐藏牌")).ToArray());
            var selected = cards[index]; inspectedCardId = selected.Id;
            EditorGUILayout.SelectableLabel(selected.Id, EditorStyles.miniLabel, GUILayout.Height(18));
            EditorGUILayout.LabelField("悬停 / 选中", selected.Interaction.IsHovered + " / " + selected.Interaction.IsSelected);
            if (!target.TryGetView(selected.Id, out var view)) { EditorGUILayout.LabelField("尚无对应视觉（等待牌局投影）"); return; }
            EditorGUILayout.LabelField("视觉实例", view.gameObject.GetInstanceID().ToString());
            EditorGUILayout.LabelField("飞入 / 过渡", view.IsEntering + " / " + view.IsTransitioning);
            EditorGUILayout.LabelField("位置 → 目标", view.DisplayPose.Position.ToString("F3") + " → " + view.transform.localPosition.ToString("F3"));
            EditorGUILayout.LabelField("翻面偏角", view.DealFlipAngle.ToString("F1") + "°");
            EditorGUILayout.LabelField("投影牌面", view.Data.FaceUp ? view.Data.FaceArtId : "隐藏");
            EditorGUILayout.LabelField("火漆 / 镭射 / 红叉", view.Surface.SealEnabled + " / " + view.Surface.FoilEnabled + " / " + view.Surface.RedCrossEnabled);
        }
    }
}
#endif
