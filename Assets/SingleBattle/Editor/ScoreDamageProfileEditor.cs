using System;
using UnityEditor;
using UnityEngine;

namespace Poker.SingleBattle.Editor
{
    [CustomEditor(typeof(ScoreDamageProfile))]
    public sealed class ScoreDamageProfileEditor : UnityEditor.Editor
    {
        [MenuItem("Tools/Poker/战局/打开默认伤害配置")]
        public static void OpenDefault()
        {
            var player = AssetDatabase.LoadAssetAtPath<PlayerBattleProfile>("Assets/SingleBattle/Profiles/SingleBattlePlayer.asset");
            var asset = player != null ? player.Damage : null;
            if (asset == null) throw new InvalidOperationException("默认玩家未绑定出牌伤害配置。");
            Selection.activeObject = asset; EditorGUIUtility.PingObject(asset);
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("version"), new GUIContent("规则版本"));
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("最终得分等额作为每个已选目标的基础伤害。攻击修正 → 目标承伤修正 → 格挡 → 生命，默认两处修正均原值通过。此资产仅定义攻击构建规则，筹码和倍率请在牌型计分配置中编辑。配置在新战局生效。", MessageType.Info);
            try { ((ScoreDamageProfile)target).BuildRule(); }
            catch (Exception error) { EditorGUILayout.HelpBox("配置无效：" + error.Message, MessageType.Error); }
        }
    }
}
