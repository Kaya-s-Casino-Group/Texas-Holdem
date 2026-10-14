using System;
using System.Linq;
using PokerEngine;
using UnityEditor;
using UnityEngine;

namespace Poker.SingleBattle.Editor
{
    /// <summary>Read-only projection of the formal discard transition, with no separate reward calculator.</summary>
    public sealed class DiscardRewardAuditWindow : EditorWindow
    {
        private SingleBattleHost host;
        private CardDuelState observedState;
        private PlayerBattleActor observedPlayer;
        private CardInstanceId[] observedCards;
        private CardDuelTransition preview;
        private string failure;
        private int mode;
        private Vector2 scroll;
        [MenuItem("Tools/Poker/战局/弃牌收益验算")]
        public static void Open() => GetWindow<DiscardRewardAuditWindow>("弃牌收益验算");
        private void OnEnable() { minSize = new Vector2(480, 420); }
        private void OnInspectorUpdate() { Repaint(); }
        private void ClearPreview()
        { observedState = null; observedPlayer = null; observedCards = null; preview = null; failure = null; }
        private void RefreshPreview()
        {
            if (host == null || host.State == null || host.Player == null || !host.CanEndTurn) { ClearPreview(); return; }
            var cards = host.Actions.Hand.Cards.Where(card => card.Interaction.IsSelected).Select(card => card.Card.Id).ToArray();
            if (ReferenceEquals(host.State, observedState) && host.Player == observedPlayer && observedCards != null && cards.SequenceEqual(observedCards)) return;
            observedState = host.State; observedPlayer = host.Player; observedCards = cards;
            try { preview = host.PreviewSelectedDiscard(); failure = null; }
            catch (Exception error) { preview = null; failure = "验算失败：" + error.Message; }
        }
        private void OnGUI()
        {
            if (host == null)
            {
                var hosts = FindObjectsOfType<SingleBattleHost>();
                if (hosts.Length == 1) { host = hosts[0]; ClearPreview(); }
            }
            EditorGUI.BeginChangeCheck();
            host = (SingleBattleHost)EditorGUILayout.ObjectField("观察战局", host, typeof(SingleBattleHost), true);
            if (EditorGUI.EndChangeCheck()) ClearPreview();
            EditorGUILayout.HelpBox("只读：复用正式弃牌入口，不提交状态、不消耗能量或次数、不移动卡牌。默认无额外收益；不进行牌型计分。配置修改后重新开局生效。", MessageType.Info);
            mode = GUILayout.Toolbar(mode, new[] { "当前选牌预计结果", "最近一次实际弃牌" });
            if (GUILayout.Button("刷新读取")) ClearPreview();
            if (!Application.isPlaying || host == null || host.State == null)
            { EditorGUILayout.HelpBox("请进入战局 Play 模式，在游戏中选择手牌。面板不会启动战局。", MessageType.Info); return; }
            if (mode == 0) RefreshPreview();
            var report = mode == 0 ? preview : host.LastDiscardResolution;
            if (mode == 0 && failure != null) { EditorGUILayout.HelpBox(failure, MessageType.Error); return; }
            if (report == null)
            { EditorGUILayout.HelpBox(mode == 0 ? "当前不可操作（发牌、演出、敌方阶段或战局已结束）。" : "尚无已提交的弃牌记录。", MessageType.Info); return; }
            if (!report.Accepted) { EditorGUILayout.HelpBox(BattleDamageAuditWindow.ErrorName(report.Error), MessageType.Warning); return; }
            if (report.DiscardReward == null) { EditorGUILayout.HelpBox("该记录没有弃牌收益信息。", MessageType.Warning); return; }
            var resolution = report.DiscardReward; var context = resolution.Context; var plan = resolution.Plan;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            Row("弃牌者", UnitName(context.Source));
            Row("收益规则版本", plan.RuleVersion);
            EditorGUILayout.LabelField("一、实际弃牌快照", EditorStyles.boldLabel);
            foreach (var card in context.Cards) Row(CardName(card), card.Id.Value);
            Row("目标范围", "仅存活友方（包含自己）");
            Row("已选收益目标", string.Join("、", context.Targets.Select(UnitName)));
            EditorGUILayout.Space(); EditorGUILayout.LabelField("二、原有弃牌流程", EditorStyles.boldLabel);
            var before = context.State.Player(context.Source); var after = report.State.Duel.Player(context.Source);
            Row("能量消耗", before.Rules.DiscardEnergyCost.ToString());
            Row("能量", before.Energy.Energy + " → " + after.Energy.Energy);
            Row("弃牌次数消耗", before.Rules.UnlimitedDiscards ? "0（不限次数）" : "1");
            Row("执行后剩余次数", report.State.RemainingDiscardBudgetFor(context.Source)?.ToString() ?? "不限");
            Row("执行后手牌数量", report.State.Zone(CardZoneKinds.Hand, context.Source).Cards.Count.ToString());
            EditorGUILayout.Space(); EditorGUILayout.LabelField("三、额外收益", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(plan.Explanation, EditorStyles.wordWrappedLabel);
            if (plan.Effects.Count == 0) EditorGUILayout.HelpBox("无额外收益，未生成战斗效果。", MessageType.Info);
            foreach (var effect in report.Events)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                Row("目标", UnitName(effect.Effect.Target));
                Row("收益类型", effect.Effect.Kind == CombatEffectKind.GainBlock ? "获得格挡" : "恢复生命");
                Row("请求数值", effect.Effect.Amount.ToString());
                Row("实际增加", (effect.Effect.Kind == CombatEffectKind.GainBlock ? effect.After.Block - effect.Before.Block : effect.HealthRestored).ToString());
                Row("格挡", effect.Before.Block + " → " + effect.After.Block); Row("生命", effect.Before.Health + " → " + effect.After.Health);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }
        private string UnitName(ParticipantId id)
        {
            var index = host.Players.ToList().FindIndex(actor => actor.ParticipantId == id);
            return index >= 0 ? "玩家 " + (index + 1) + "（" + host.Players[index].name + "）" : id.Value;
        }
        private static string CardName(DiscardCardSnapshot card)
        {
            var face = card.Face;
            string rank = face.Rank == Rank.Ace ? "A" : face.Rank == Rank.King ? "K" : face.Rank == Rank.Queen ? "Q" : face.Rank == Rank.Jack ? "J" : ((int)face.Rank).ToString();
            return new[] { "梅花", "方块", "红桃", "黑桃" }[(int)face.Suit - 1] + rank;
        }
        private static void Row(string name, string value)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(name, EditorStyles.wordWrappedLabel, GUILayout.Width(150));
            GUILayout.Label(value, EditorStyles.wordWrappedLabel, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
        }
    }
}
