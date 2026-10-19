using System;
using PokerEngine;
using UnityEditor;
using UnityEngine;

namespace Poker.SingleBattle.Editor
{
    [CustomEditor(typeof(SingleBattleSceneAdapter))]
    public sealed class SingleBattleSceneAdapterEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("contentXml"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("encounterId"));
                EditorGUILayout.HelpBox("规则由 XML 编成提供，场景角色按编成顺序绑定。修改后重新开战生效。", MessageType.Info);
                DrawPropertiesExcluding(serializedObject, "m_Script", "contentXml", "encounterId");
                serializedObject.ApplyModifiedProperties();
                if (GUILayout.Button("校验 XML 和场景绑定（不启动对局）")) Validate((SingleBattleSceneAdapter)target);
            }
            var host = (SingleBattleSceneAdapter)target;
            foreach (var diagnostic in host.ContentDiagnostics)
                EditorGUILayout.HelpBox(diagnostic.Code + " · " + diagnostic.Location.Line + ":" + diagnostic.Location.Column +
                    " · " + diagnostic.Location.Path + "\n" + diagnostic.Message,
                    diagnostic.Severity == BattleDiagnosticSeverity.Error ? MessageType.Error : MessageType.Warning);
        }

        internal static void Validate(SingleBattleSceneAdapter host)
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
            serializedObject.Update();
            EditorGUILayout.HelpBox("角色规则由战局入口的 XML 编成提供；此处绑定场景角色和表现。", MessageType.Info);
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
