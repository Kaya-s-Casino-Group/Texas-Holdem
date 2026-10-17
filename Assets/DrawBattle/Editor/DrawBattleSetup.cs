using System;
using System.Linq;
using CardInteraction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.DrawBattle.Editor
{
    public static class DrawBattleSetup
    {
        [MenuItem("工具/分数挑战/接入当前场景")]
        public static void Connect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play before configuring Draw.");
            var scene = SceneManager.GetActiveScene();
            var presenters = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HandActionPresenter>(true))
                .Where(item => item.isActiveAndEnabled && item.Hand != null && item.Played != null).ToArray();
            if (presenters.Length != 1) throw new InvalidOperationException("Select a scene containing exactly one active, configured hand action panel.");
            var actions = presenters[0]; var match = actions.Hand.Match;
            var host = match.GetComponent<DrawBattleHost>();
            if (host == null) host = Undo.AddComponent<DrawBattleHost>(match.gameObject);
            Undo.RecordObject(host, "Connect Draw battle to existing card UI");
            var all = scene.GetRootGameObjects();
            var overview = all.SelectMany(root => root.GetComponentsInChildren<DeckOverviewPresenter>(true)).FirstOrDefault(item => item.Match == match);
            var pile = all.SelectMany(root => root.GetComponentsInChildren<DrawPilePresenter>(true)).FirstOrDefault(item => item.Source == actions.Hand);
            host.Configure(match, actions, overview, pile); EditorUtility.SetDirty(host); EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeObject = host;
            Debug.Log("Draw connected to existing hand, played route, deck/discard data and card UI. Enter Play to start. Scene remains unsaved.");
        }
    }
}
