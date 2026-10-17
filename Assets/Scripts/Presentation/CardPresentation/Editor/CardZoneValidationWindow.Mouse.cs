#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace CardPresentation.Editor
{
    public sealed partial class CardZoneValidationWindow
    {
        private void ApplySnappyLiftPreset() => Mutate("应用快速惯性", () => liftSettings = CardZoneLiftSettings.Default);
        private void DrawLiftSettings()
        {
            EditorGUI.BeginChangeCheck();
            int limit = EditorGUILayout.IntField("最多选中卡牌数", maxSelectedCards);
            var deniedOffset = EditorGUILayout.Vector3Field("无法选中上升偏移", selectionDeniedOffset);
            float rise = EditorGUILayout.FloatField("无法选中上升（秒）", selectionDeniedRiseSeconds);
            float fall = EditorGUILayout.FloatField("无法选中回弹（秒）", selectionDeniedReturnSeconds);
            if (EditorGUI.EndChangeCheck()) Mutate("调整选牌限制与反馈", () => {
                maxSelectedCards = limit; selectionDeniedOffset = deniedOffset;
                selectionDeniedRiseSeconds = rise; selectionDeniedReturnSeconds = fall;
            });
            EditorGUILayout.HelpBox("默认最多选中 5 张，达到上限仍可取消选择。所有拒绝选中新牌的原因共用小幅上升后回弹反馈，不改变选择。", MessageType.None);
            EditorGUILayout.LabelField("悬停缩放 / 选中上浮", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck(); var next = liftSettings;
            next.Enabled = EditorGUILayout.Toggle("交互惯性动画", next.Enabled);
            next.RaiseSeconds = EditorGUILayout.FloatField("放大 / 上浮（秒）", next.RaiseSeconds);
            next.LowerSeconds = EditorGUILayout.FloatField("缩小 / 回落（秒）", next.LowerSeconds);
            next.Overshoot = EditorGUILayout.Slider("惯性强度", next.Overshoot, 0, 3);
            if (EditorGUI.EndChangeCheck()) Mutate("调整上浮动画", () => { next.Validate(); liftSettings = next; });
            EditorGUILayout.HelpBox("悬停面积扩大 20%，不产生位移；缩放与选中上浮独立叠加。时长包含越过目标后的回稳；选中上浮使用更明显的回弹（强度 ×1.35，上限 3），摆角同步归零。0 关闭全部回弹；快速反向从当前姿态接续。", MessageType.None);
            if (GUILayout.Button("应用快速惯性（上浮 0.12 / 回落 0.14 秒）")) ApplySnappyLiftPreset();
            if (GUILayout.Button("选中沿局部 +Z 上浮"))
                Mutate("设置 Z 方向上浮", () => {
                    hoveredOffset = Vector3.zero;
                    raisedOffset = new Vector3(0, 0, raisedOffset.magnitude > 0 ? raisedOffset.magnitude : 0.02f);
                });
        }
    }
}
#endif
