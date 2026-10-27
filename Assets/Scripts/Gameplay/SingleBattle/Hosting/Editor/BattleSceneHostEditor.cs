using System;
using Poker.BattlePresentation;
using PokerEngine;
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
                EditorGUILayout.HelpBox("进入 Play 后，若规则采用手动摸牌，请在此点击“摸牌”。编辑模式和开局空手牌时不会创建卡牌实例。", MessageType.Info);
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
                if (host.SceneAdapter is BattleTimelineSceneAdapter timeline && timeline.CanAct)
                {
                    var flow = host.AuthorityState.FlowFor(timeline.ObserverId);
                    if (flow.Window == BattlePlayerWindow.Draw)
                    {
                        int count = host.Engine.DrawLimit(host.AuthorityState, timeline.ObserverId);
                        EditorGUILayout.HelpBox("等待规则摸牌，手牌区会在摸牌后实例化卡牌。", MessageType.Info);
                        using (new EditorGUI.DisabledScope(count <= 0 || flow.DrawUsesRemaining <= 0))
                            if (GUILayout.Button("摸牌（" + count + " 张）")) DrawAvailableCards(host);
                        if (GUILayout.Button("跳过摸牌")) timeline.SubmitAction(CardDuelAction.SkipDraw);
                    }
                    else if (GUILayout.Button("结束玩家回合（验证）")) timeline.EndTurn();
                }
            }
            if (host.Session != null && GUILayout.Button("同步到规则结果")) host.SynchronizeBattle();
        }

        private static void DrawAvailableCards(BattleSceneHost host)
        {
            var adapter = (BattleTimelineSceneAdapter)host.SceneAdapter;
            int count = host.Engine.DrawLimit(host.AuthorityState, adapter.ObserverId);
            var result = adapter.SubmitAction(CardDuelAction.Draw, drawCount: count);
            if (!result.Accepted) Debug.LogWarning("规则拒绝摸牌：" + result.Error, host);
        }
    }
}
