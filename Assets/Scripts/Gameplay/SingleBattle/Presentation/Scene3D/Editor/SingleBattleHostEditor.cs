using System;
using PokerEngine;
using UnityEditor;
using UnityEngine;

namespace Poker.SingleBattle.Editor
{
    [CustomEditor(typeof(SingleBattleHost))]
    public sealed class SingleBattleHostEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("configurationSource"), new GUIContent("配置来源"));
                bool xml = serializedObject.FindProperty("configurationSource").enumValueIndex == (int)BattleConfigurationSource.XmlContent;
                if (xml)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("contentXml"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("encounterId"));
                    EditorGUILayout.HelpBox("XML 编成决定规则、角色属性、牌组与遗物。下方场景角色按双方编成的顺序绑定，仅负责输入与表现。修改内容后重新开战生效。", MessageType.Info);
                }
                else EditorGUILayout.PropertyField(serializedObject.FindProperty("rulesProfile"));
                DrawPropertiesExcluding(serializedObject, "m_Script", "configurationSource", "contentXml", "encounterId", "rulesProfile");
                serializedObject.ApplyModifiedProperties();
                if (xml && GUILayout.Button("校验 XML 和场景绑定（不启动对局）")) Validate((SingleBattleHost)target);
            }
            var host = (SingleBattleHost)target;
            foreach (var diagnostic in host.ContentDiagnostics)
                EditorGUILayout.HelpBox(diagnostic.Code + " · " + diagnostic.Location.Line + ":" + diagnostic.Location.Column +
                    " · " + diagnostic.Location.Path + "\n" + diagnostic.Message,
                    diagnostic.Severity == BattleDiagnosticSeverity.Error ? MessageType.Error : MessageType.Warning);
        }

        internal static void Validate(SingleBattleHost host)
        {
            try
            {
                var session = host.BuildConfiguredSession();
                Debug.Log("XML 编成校验通过，未启动对局：" + host.ContentEncounterId +
                    " · 玩家 " + session.State.Duel.Players.Count + " · 敌人 " + session.State.Duel.Enemies.Count, host);
            }
            catch (Exception error) { Debug.LogError(error.Message, host); }
        }
    }

    [CustomEditor(typeof(BattleActor), true)]
    public sealed class BattleActorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var actor = (BattleActor)target;
            if (actor.Battle == null || !actor.Battle.UsesXmlContent) { DrawDefaultInspector(); return; }
            serializedObject.Update();
            EditorGUILayout.HelpBox("角色规则由战局入口的 XML 编成提供；此处绑定场景角色和表现。", MessageType.Info);
            DrawPropertiesExcluding(serializedObject, "m_Script", "profile");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
