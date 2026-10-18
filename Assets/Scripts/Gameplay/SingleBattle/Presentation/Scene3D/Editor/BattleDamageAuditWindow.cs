using System;
using System.Linq;
using PokerEngine;
using UnityEditor;
using UnityEngine;

namespace Poker.SingleBattle.Editor
{
    /// <summary>Read-only observer. All numbers come from the real CardDuel transition, never a parallel calculator.</summary>
    public sealed class BattleDamageAuditWindow : EditorWindow
    {
        private SingleBattleSceneAdapter host;
        private CardDuelState observedState;
        private PlayerBattleActor observedPlayer;
        private CardInstanceId[] observedSelection;
        private CardDuelTransition preview;
        private string failure;
        private int mode;
        private Vector2 scroll;
        [MenuItem("工具/卡牌战斗/验算/出牌伤害")]
        public static void Open() => GetWindow<BattleDamageAuditWindow>("出牌伤害验算");
        private void OnEnable() { minSize = new Vector2(480, 420); }
        private void OnInspectorUpdate() { Repaint(); }
        private void ClearPreview()
        { observedState = null; observedPlayer = null; observedSelection = null; preview = null; failure = null; }
        private void RefreshPreview()
        {
            if (host == null || host.State == null || host.Player == null || !host.CanEndTurn)
            { ClearPreview(); return; }
            var selected = host.Actions.Hand.Cards.Where(card => card.Interaction.IsSelected).Select(card => card.Card.Id).ToArray();
            if (ReferenceEquals(host.State, observedState) && host.Player == observedPlayer &&
                observedSelection != null && selected.SequenceEqual(observedSelection)) return;
            observedState = host.State; observedPlayer = host.Player; observedSelection = selected;
            try { preview = host.PreviewSelectedPlay(); failure = null; }
            catch (Exception error) { preview = null; failure = "验算失败：" + error.Message; }
        }
        private void OnGUI()
        {
            if (host == null)
            {
                var hosts = FindObjectsOfType<SingleBattleSceneAdapter>();
                if (hosts.Length == 1) { host = hosts[0]; ClearPreview(); }
            }
            EditorGUI.BeginChangeCheck();
            host = (SingleBattleSceneAdapter)EditorGUILayout.ObjectField("观察战局", host, typeof(SingleBattleSceneAdapter), true);
            if (EditorGUI.EndChangeCheck()) ClearPreview();
            EditorGUILayout.HelpBox("只读：读取当前选牌，调用正式出牌结算入口；不提交返回状态，不扣能量、不移牌、不播放动作。参数请在独立配置资产中编辑，重新开局生效。", MessageType.Info);
            mode = GUILayout.Toolbar(mode, new[] { "当前选牌预计结果", "最近一次实际出牌" });
            if (GUILayout.Button("刷新读取")) ClearPreview();
            if (!Application.isPlaying || host == null || host.State == null)
            { EditorGUILayout.HelpBox("请进入战局 Play 模式，在游戏中选择手牌。此面板不会创建或启动战局。", MessageType.Info); return; }
            RefreshPreview();
            var report = mode == 0 ? preview : host.LastPlayResolution;
            if (mode == 0 && failure != null) { EditorGUILayout.HelpBox(failure, MessageType.Error); return; }
            if (report == null)
            { EditorGUILayout.HelpBox(mode == 0 ? "当前不可出牌（正在发牌、演出、敌方阶段或战局已结束）。" : "尚无已提交的出牌记录。", MessageType.Info); return; }
            if (!report.Accepted)
            { EditorGUILayout.HelpBox(ErrorName(report.Error), MessageType.Warning); return; }
            if (report.Score == null || report.Pattern == null || report.Damage == null)
            { EditorGUILayout.HelpBox("该记录没有完整的出牌结算信息。", MessageType.Warning); return; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var actor = mode == 0 ? host.Player.ParticipantId : host.LastPlayedBy;
            Row("攻击者", UnitName(actor));
            Row(mode == 0 ? "验算所依据的战局版本" : "实际出牌提交后版本", (mode == 0 ? observedState.Revision : report.State.Revision).ToString());
            EditorGUILayout.LabelField("一、攻击来源与出牌快照", EditorStyles.boldLabel);
            var origin = report.Events.FirstOrDefault()?.Effect.Attack;
            Row("攻击来源", OriginName(origin));
            var hand = origin?.Hand;
            if (hand != null)
            {
                Row("正式判定牌型", SingleBattleHud.HandName(hand.Pattern.Category));
                CardList("提交牌", hand.SubmittedCards);
                CardList("牌型组成牌", hand.PatternCards);
                CardList("实际计分牌", hand.ScoringCards);
                EditorGUILayout.HelpBox("提交牌包含杂牌；失效牌仍可组成牌型，但不进入实际计分牌。花色条件由修正实现明确选择检查范围。", MessageType.None);
            }
            else EditorGUILayout.LabelField("本次攻击没有出牌来源信息。");
            EditorGUILayout.Space(); EditorGUILayout.LabelField("二、正式计分过程", EditorStyles.boldLabel);
            var score = report.Score;
            Row("牌型基础筹码 × 倍率", score.BasePoints + " × " + score.BaseMultiplier);
            foreach (var step in score.Steps)
            {
                string source = step.CardId == null ? step.SourceId : CardName(new CardInstanceId(step.CardId), report);
                string op = step.Operation == ScoreOperation.AddPoints ? "加筹码" : step.Operation == ScoreOperation.AddMultiplier ? "加倍率" : "乘倍率";
                EditorGUILayout.LabelField(source + " · " + op + " " + step.Amount + " → " + step.PointsAfter + " 筹码 × " + step.MultiplierAfter, EditorStyles.wordWrappedLabel);
            }
            Row("最终筹码 × 倍率", score.Points + " × " + score.Multiplier);
            Row("最终得分", score.Total.ToString());
            EditorGUILayout.Space(); EditorGUILayout.LabelField("三、构建攻击", EditorStyles.boldLabel);
            Row("攻击构建规则版本", report.Damage.RuleVersion);
            EditorGUILayout.LabelField(report.Damage.Explanation, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(); EditorGUILayout.LabelField("四、各目标修正与受伤结算", EditorStyles.boldLabel);
            foreach (var hit in report.Events)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                Row("目标", UnitName(hit.Effect.Target));
                var attack = hit.Effect.AttackModification;
                Row("基础伤害", (hit.Effect.Attack?.BaseDamage ?? hit.Effect.Amount).ToString());
                DrawModification("攻击修正", attack, hit.Effect.Attack);
                Row("攻击修正后伤害", hit.Effect.Amount.ToString());
                DrawModification("目标承伤修正", hit.IncomingModification, hit.Effect.Attack);
                Row("格挡前伤害", hit.SettledDamage.ToString());
                Row("格挡吸收", hit.BlockAbsorbed.ToString());
                Row("实际生命损失", hit.HealthLost.ToString()); Row("溢出伤害", hit.Overkill.ToString());
                Row("生命", hit.Before.Health + " → " + hit.After.Health); Row("格挡", hit.Before.Block + " → " + hit.After.Block);
                Row("结果", hit.Died ? "死亡" : "存活"); EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }
        private static void DrawModification(string title, DamageModification modification, AttackContext attack)
        {
            if (modification == null) return;
            Row(title, modification.InputAmount + " → " + modification.OutputAmount);
            Row(title + "规则版本", modification.RuleVersion);
            EditorGUILayout.LabelField(modification.Explanation, EditorStyles.wordWrappedLabel);
            if (modification.Triggers.Count == 0) Row(title + "触发原因", "无能力触发记录");
            foreach (var trigger in modification.Triggers)
            {
                Row("触发来源", trigger.SourceId);
                EditorGUILayout.LabelField("触发原因：" + trigger.Reason, EditorStyles.wordWrappedLabel);
                Row("检查范围", ScopeName(trigger.CardScope));
                if (trigger.CardIds.Count > 0)
                    Row("关联牌", string.Join("、", trigger.CardIds.Select(id => SnapshotCardName(attack.Hand.SubmittedCards.Single(card => card.Id == id)) + " [" + id.Value + "]")));
            }
        }
        private static string OriginName(AttackContext attack) => attack == null ? "无来源记录" :
            attack.Origin == AttackOrigin.ScoredHand ? "出牌攻击" : attack.Origin == AttackOrigin.EnemyAttack ? "怪物普通攻击" : "其他攻击（无出牌快照）";
        private static string ScopeName(AttackCardScope scope) => scope == AttackCardScope.Submitted ? "提交牌" :
            scope == AttackCardScope.Pattern ? "牌型组成牌" : scope == AttackCardScope.Scoring ? "实际计分牌" : "不检查卡牌";
        private static void CardList(string title, System.Collections.Generic.IReadOnlyList<AttackCardSnapshot> cards) =>
            Row(title + "（" + cards.Count + "）", cards.Count == 0 ? "无" : string.Join("、", cards.Select(SnapshotCardName)));
        private string UnitName(ParticipantId id)
        {
            var player = host.Players.ToList().FindIndex(actor => actor.ParticipantId == id);
            if (player >= 0) return "玩家 " + (player + 1) + "（" + host.Players[player].name + "）";
            var enemy = host.Enemies.ToList().FindIndex(actor => actor.ParticipantId == id);
            return enemy >= 0 ? "怪物 " + (enemy + 1) + "（" + host.Enemies[enemy].name + "）" : id.Value;
        }
        private static string CardName(CardInstanceId id, CardDuelTransition report)
        {
            var card = report.Events.FirstOrDefault()?.Effect.Attack?.Hand?.SubmittedCards.FirstOrDefault(item => item.Id == id);
            return card == null ? id.Value : SnapshotCardName(card);
        }
        private static string SnapshotCardName(AttackCardSnapshot card)
        {
            var suit = new[] { "梅花", "方块", "红桃", "黑桃" }[(int)card.Suit - 1];
            string rank = card.Rank == Rank.Ace ? "A" : card.Rank == Rank.King ? "K" : card.Rank == Rank.Queen ? "Q" : card.Rank == Rank.Jack ? "J" : ((int)card.Rank).ToString();
            return suit + rank + (!card.CanScore ? "（失效）" : "") + (card.IsDisabled ? "（禁用）" : "");
        }
        private static void Row(string name, string value)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(name, EditorStyles.wordWrappedLabel, GUILayout.Width(150));
            GUILayout.Label(value, EditorStyles.wordWrappedLabel, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
        }
        internal static string ErrorName(CardDuelError error)
        {
            switch (error)
            {
                case CardDuelError.InvalidSelection: return "请选择合法的 1～5 张手牌；禁用牌不能操作。";
                case CardDuelError.InsufficientEnergy: return "当前能量不足，正式出牌会被拒绝。";
                case CardDuelError.InvalidPhase: return "当前阶段不能出牌。";
                case CardDuelError.StaleRevision: return "战局已变化，请重新读取。";
                case CardDuelError.InvalidTarget: return "当前目标无效。";
                case CardDuelError.InvalidDamagePlan: return "伤害策略返回了不符合来源、目标或版本约定的结果。";
                case CardDuelError.InvalidDiscardReward: return "弃牌收益规则返回了无效结果；本次操作未提交。";
                case CardDuelError.InvalidDamageModifier: return "攻击修正或目标承伤修正返回了无效结果，本次出牌会被拒绝。";
                case CardDuelError.Overflow: return "结算数值溢出，正式出牌会被拒绝。";
                case CardDuelError.InvalidState: return "战局与规则不匹配。";
                case CardDuelError.NoDiscards: return "弃牌次数不足。";
                default: return "无法完成本次验算。";
            }
        }
    }
}
