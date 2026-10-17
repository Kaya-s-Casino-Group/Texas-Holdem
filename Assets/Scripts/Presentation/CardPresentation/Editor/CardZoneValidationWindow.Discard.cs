#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace CardPresentation.Editor
{
    public sealed partial class CardZoneValidationWindow
    {
        [SerializeField] private CardZoneDiscardSettings discardSettings = CardZoneDiscardSettings.Default;
        [SerializeField] private Vector3 discardLocalPosition = new Vector3(-.38f, .035f, 0);
        [SerializeField] private Transform discardAnchor;
        private void DrawDiscardSettings()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("弃牌 / 离场动画", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck(); var next = discardSettings;
            var anchor = (Transform)EditorGUILayout.ObjectField("弃牌堆锚点（可选）", discardAnchor, typeof(Transform), true);
            var localPosition = EditorGUILayout.Vector3Field("弃牌堆局部位置", discardLocalPosition);
            next.FlightSeconds = EditorGUILayout.FloatField("飞出时间（秒）", next.FlightSeconds);
            next.Interval = EditorGUILayout.FloatField("逐张间隔（秒）", next.Interval);
            next.FlightLift = EditorGUILayout.FloatField("飞行抬高", next.FlightLift);
            if (EditorGUI.EndChangeCheck()) Mutate("调整弃牌动画", () => {
                next.Validate(); discardSettings = next; discardAnchor = anchor; discardLocalPosition = localPosition;
            });
            EditorGUILayout.HelpBox("按原手牌顺序离场；可见牌翻到牌背，不可见牌保持牌背。到达后消失，无回弹。粉红色 Gizmo 为弃牌终点，锚点优先；每批起飞时记录终点。", MessageType.None);
        }
    }
}
#endif
