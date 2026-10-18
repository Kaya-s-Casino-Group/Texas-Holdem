using System;
using Poker.BattlePresentation;
using UnityEditor;
using UnityEngine;

namespace Poker.BattleHosting.Editor
{
    [CustomEditor(typeof(BattleSceneHost))]
    public sealed class BattleSceneHostEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var host = (BattleSceneHost)target;
            using (new EditorGUI.DisabledScope(Application.isPlaying)) DrawDefaultInspector();
            EditorGUILayout.HelpBox("一个独立 Battle 对应一个 Host。Session Source 创建会话，Scene Adapter 接入输入和表现。最小预制体不创建相机、牌区或 UI。", MessageType.Info);
            if (!Application.isPlaying)
            {
                if (GUILayout.Button("校验会话与适配器（不启动）"))
                {
                    try
                    {
                        if (!(host.SessionSource is IBattleSessionSource source) || !(host.SceneAdapter is IBattleSceneAdapter adapter))
                            throw new InvalidOperationException("Bind Session Source and Scene Adapter first.");
                        adapter.BindHost(host); adapter.ValidateSession(source.CreateSession());
                        Debug.Log("Battle 入口校验通过。", host);
                    }
                    catch (Exception error) { Debug.LogError(error.Message, host); }
                }
                return;
            }
            EditorGUILayout.LabelField("会话", host.Session == null ? "未启动" : host.AuthorityState.Table.MatchId.ToString());
            EditorGUILayout.LabelField("表现", host.IsPresenting ? "播放中" : "就绪");
            using (new EditorGUI.DisabledScope(!host.isActiveAndEnabled || host.IsPresenting))
            {
                if (GUILayout.Button(host.Session == null ? "开始 Battle" : "重新开始 Battle")) host.RestartBattle();
                if (host.SceneAdapter is BattleTimelineSceneAdapter timeline && timeline.CanAct && GUILayout.Button("结束玩家回合（验证）")) timeline.EndTurn();
            }
            if (host.Session != null && GUILayout.Button("同步到规则结果")) host.SynchronizeBattle();
        }
    }
}
