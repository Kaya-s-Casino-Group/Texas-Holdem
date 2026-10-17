#if UNITY_EDITOR
using System.Linq;
using CardInteraction;
using UnityEditor;
using UnityEngine;
namespace CardPresentation.Editor
{
    public sealed partial class CardZoneValidationWindow
    {
        private CardZoneTransferPresenter TransferRoute
        {
            get
            {
                if (target == null) return null;
                if (Hand != null) return Hand.GetComponent<CardZoneTransferPresenter>();
                return target.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CardZoneTransferPresenter>(true))
                    .FirstOrDefault(route => route.Destination == target);
            }
        }
        private void DrawTransferPage()
        {
            var route = TransferRoute;
            if (route == null) { EditorGUILayout.HelpBox("当前牌区没有实战出牌路径，请通过玩法场景配置连接。", MessageType.Info); return; }
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("源手牌区", route.Source, typeof(HandZoneView), true);
                EditorGUILayout.ObjectField("目标出牌区", route.Destination, typeof(PlayedZoneView), true);
            }
            EditorGUILayout.LabelField("实战飞行", route.IsTransferring ? "进行中" : "空闲");
            using (new EditorGUI.DisabledScope(!CanEdit || route.IsTransferring))
            {
                EditorGUI.BeginChangeCheck(); var next = route.Settings;
                next.Enabled = EditorGUILayout.Toggle("出牌动画", next.Enabled);
                next.FlightSeconds = EditorGUILayout.FloatField("飞行时间（秒）", next.FlightSeconds);
                next.Interval = EditorGUILayout.FloatField("逐张间隔（秒）", next.Interval);
                next.LandingPause = EditorGUILayout.FloatField("全员落位后停顿", next.LandingPause);
                next.ReorderSeconds = EditorGUILayout.FloatField("整理排列时间（秒）", next.ResolvedReorderSeconds);
                var control = (Transform)EditorGUILayout.ObjectField("曲线控制点", route.ControlPoint, typeof(Transform), true);
                using (new EditorGUI.DisabledScope(control != null)) next.ArcHeight = EditorGUILayout.FloatField("默认弧高", next.ArcHeight);
                if (EditorGUI.EndChangeCheck()) Run(() => {
                    if (!CanEdit || route.IsTransferring) return;
                    next.Validate();
                    if (!Application.isPlaying) Undo.RecordObject(route, "调整实战出牌动画");
                    route.Configure(route.Source, route.Destination, next, control);
                    if (!Application.isPlaying)
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(route); EditorUtility.SetDirty(route);
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(route.gameObject.scene);
                    }
                });
            }
            EditorGUILayout.HelpBox("通过 Game 内的出牌按钮观察路径，工具不提交出牌或跳过动画完成。", MessageType.None);
            if (GUILayout.Button("定位路径组件")) Selection.activeGameObject = route.gameObject;
        }
    }
}
#endif
