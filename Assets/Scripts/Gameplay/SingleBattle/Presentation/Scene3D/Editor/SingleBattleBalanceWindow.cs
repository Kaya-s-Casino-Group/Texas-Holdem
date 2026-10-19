using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Editor
{
    /// <summary>Locates and validates the XML source; never edits a running session.</summary>
    public sealed class SingleBattleBalanceWindow : EditorWindow
    {
        [SerializeField] private SingleBattleSceneAdapter host;
        private string validation;
        private MessageType validationType;
        [MenuItem("工具/卡牌战斗/数值配置", priority = 0)]
        public static void Open() => GetWindow<SingleBattleBalanceWindow>("战斗 XML 配置").Show();
        private void OnGUI()
        {
            if (host == null)
            {
                var hosts = SceneManager.GetActiveScene().GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).ToArray();
                if (hosts.Length == 1) host = hosts[0];
            }
            host = (SingleBattleSceneAdapter)EditorGUILayout.ObjectField("场景适配器", host, typeof(SingleBattleSceneAdapter), true);
            if (host == null) { EditorGUILayout.HelpBox("请选择当前战斗的场景适配器。", MessageType.Info); return; }
            DrawXmlContent();
        }
        private static void Locate(UnityEngine.Object item, string label)
        {
            if (GUILayout.Button(label)) { Selection.activeObject = item; EditorGUIUtility.PingObject(item); }
        }
        private void DrawXmlContent()
        {
            EditorGUILayout.HelpBox("当前场景使用 XML 内容配置。使用 External/BattleToolkit/Tools/BattleContentEditor 外部工具导入此文件、配置并导出覆盖；Unity 刷新后重新开战生效。", MessageType.Info);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("XML 内容包", host.ContentXml, typeof(TextAsset), false);
                EditorGUILayout.TextField("编成 ID", host.ContentEncounterId ?? "");
            }
            if (host.ContentXml != null)
            {
                EditorGUILayout.SelectableLabel(AssetDatabase.GetAssetPath(host.ContentXml), EditorStyles.wordWrappedLabel, GUILayout.Height(40));
                Locate(host.ContentXml, "定位 XML 文件");
            }
            Locate(host, "定位场景入口 / 修改编成");
            EditorGUILayout.HelpBox("场景玩家和敌人按 XML 编成的各自顺序绑定；属性、牌型、状态、遗物、牌组均从 XML 组装。", MessageType.None);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("统一校验内容和场景阵容（不启动对局）"))
                {
                    try
                    {
                        host.BuildConfiguredSession();
                        validation = "校验通过。未启动对局或修改当前会话。";
                        validationType = MessageType.Info;
                    }
                    catch (Exception error) { validation = error.Message; validationType = MessageType.Error; }
                }
            if (!string.IsNullOrEmpty(validation)) EditorGUILayout.HelpBox(validation, validationType);
        }

    }
}
