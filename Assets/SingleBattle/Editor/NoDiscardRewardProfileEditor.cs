using System;
using UnityEditor;
using UnityEngine;

namespace Poker.SingleBattle.Editor
{
    [CustomEditor(typeof(NoDiscardRewardProfile))]
    public sealed class NoDiscardRewardProfileEditor : UnityEditor.Editor
    {
        [MenuItem("Tools/Poker/战局/打开默认弃牌收益配置")]
        public static void OpenDefault()
        {
            var asset = AssetDatabase.LoadAssetAtPath<NoDiscardRewardProfile>("Assets/SingleBattle/Profiles/DemoDiscardReward.asset");
            if (asset == null) throw new InvalidOperationException("未找到默认弃牌收益配置。");
            Selection.activeObject = asset; EditorGUIUtility.PingObject(asset);
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("version"));
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("默认无额外收益。弃牌只执行原有过牌、能耗和次数流程，不做牌型计分。未来可替换独立收益规则；收益仅可作用于存活友方。配置在重新开局后生效。", MessageType.Info);
            try { ((NoDiscardRewardProfile)target).BuildRule(); }
            catch (Exception error) { EditorGUILayout.HelpBox("配置无效：" + error.Message, MessageType.Error); }
        }
    }
}
