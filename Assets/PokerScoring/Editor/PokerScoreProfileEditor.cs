using System;
using PokerEngine;
using UnityEditor;
using UnityEngine;

namespace Poker.Scoring.Editor
{
    [CustomEditor(typeof(PokerScoreProfile))]
    public sealed class PokerScoreProfileEditor : UnityEditor.Editor
    {
        public const string DefaultPath = "Assets/PokerScoring/Profiles/DefaultPokerScore.asset";
        private static readonly string[] HandNames = { "高牌", "一对", "两对", "三条", "顺子", "同花", "葫芦", "四条", "同花顺（含皇家同花顺）", "五条（已有扩展）" };
        private static readonly string[] RankNames = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };
        private static readonly string[] SuitNames = { "梅花", "方块", "红桃", "黑桃" };
        [MenuItem("Tools/Poker/牌型计分/打开默认配置")]
        public static void OpenDefault()
        {
            var profile = AssetDatabase.LoadAssetAtPath<PokerScoreProfile>(DefaultPath);
            if (profile == null) throw new InvalidOperationException("未找到默认牌型计分配置：" + DefaultPath);
            Selection.activeObject = profile; EditorGUIUtility.PingObject(profile);
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("version"), new GUIContent("规则版本"));
            EditorGUILayout.HelpBox("得分 =（牌型筹码 + 有效计分牌筹码 + 加成）× 倍率。修改后在新战局生效；发布数值更新时请同步更换规则版本。", MessageType.Info);
            EditorGUILayout.LabelField("牌型基础数值与每级成长", EditorStyles.boldLabel);
            var hands = serializedObject.FindProperty("hands");
            for (int i = 0; i < hands.arraySize; i++)
            {
                var entry = hands.GetArrayElementAtIndex(i);
                var category = entry.FindPropertyRelative("category").intValue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(category >= 1 && category <= HandNames.Length ? HandNames[category - 1] : "未知牌型", EditorStyles.boldLabel);
                Field(entry, "chips", "基础筹码"); Field(entry, "multiplier", "基础倍率");
                Field(entry, "chipsPerLevel", "每级增加筹码"); Field(entry, "multiplierPerLevel", "每级增加倍率");
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(); EditorGUILayout.LabelField("每张牌的默认筹码（按点数）", EditorStyles.boldLabel);
            var ranks = serializedObject.FindProperty("ranks");
            for (int i = 0; i < ranks.arraySize; i++)
            {
                var entry = ranks.GetArrayElementAtIndex(i);
                var rank = entry.FindPropertyRelative("rank").intValue;
                Field(entry, "chips", rank >= 2 && rank <= 14 ? RankNames[rank - 2] : "未知点数");
            }
            EditorGUILayout.Space(); EditorGUILayout.LabelField("指定单牌覆盖", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("可给 52 张牌中的任意牌面单独设置筹码，例如仅修改黑桃 A。未覆盖的牌面沿用上表。牌实例的局内成长另行叠加。", MessageType.None);
            var overrides = serializedObject.FindProperty("overrides");
            for (int i = 0; i < overrides.arraySize; i++)
            {
                var entry = overrides.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                var suit = entry.FindPropertyRelative("suit");
                suit.intValue = EditorGUILayout.Popup("花色", suit.intValue - 1, SuitNames) + 1;
                var rank = entry.FindPropertyRelative("rank");
                rank.intValue = EditorGUILayout.Popup("点数", rank.intValue - 2, RankNames) + 2;
                Field(entry, "chips", "筹码");
                bool remove = GUILayout.Button("移除此覆盖");
                EditorGUILayout.EndVertical();
                if (remove) { overrides.DeleteArrayElementAtIndex(i); break; }
            }
            if (GUILayout.Button("添加单牌覆盖"))
            {
                int index = overrides.arraySize++; var entry = overrides.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("suit").intValue = (int)Suit.Spades;
                entry.FindPropertyRelative("rank").intValue = (int)Rank.Ace;
                entry.FindPropertyRelative("chips").longValue = 11;
            }
            serializedObject.ApplyModifiedProperties();
            try { ((PokerScoreProfile)target).Build(); }
            catch (Exception error) { EditorGUILayout.HelpBox("配置无效（请检查重复牌面、负数、缺失项）：" + error.Message, MessageType.Error); }
        }
        private static void Field(SerializedProperty entry, string name, string label) =>
            EditorGUILayout.PropertyField(entry.FindPropertyRelative(name), new GUIContent(label));
    }
}
