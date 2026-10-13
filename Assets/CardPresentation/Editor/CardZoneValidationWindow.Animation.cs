#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
namespace CardPresentation.Editor
{
    public sealed partial class CardZoneValidationWindow
    {
        private void ApplyGentleMotionPreset() => Mutate("应用轻弧摆动", () => { layout.ArcHeight = .008f; motionSettings = CardZoneMotionSettings.Default; });
        private void ApplySoftLandingPreset() => Mutate("应用柔和惯性", () => {
            var defaults = CardZoneTransitionSettings.Default;
            transitionSettings.SettleSeconds = defaults.SettleSeconds; transitionSettings.OvershootDistance = defaults.OvershootDistance;
            transitionSettings.LandingYaw = defaults.LandingYaw;
        });
        private void DrawTransitions()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("从牌堆补牌 / 入列动画", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck(); var next = transitionSettings;
            next.Enabled = EditorGUILayout.Toggle("启用列表变化动画", next.Enabled);
            var anchor = (Transform)EditorGUILayout.ObjectField("牌堆锚点（可选）", deckAnchor, typeof(Transform), true);
            var position = EditorGUILayout.Vector3Field("牌堆局部位置", deckLocalPosition);
            next.FlightSeconds = EditorGUILayout.FloatField("飞入时间（秒）", next.FlightSeconds);
            next.DealInterval = EditorGUILayout.FloatField("逐张间隔（秒）", next.DealInterval);
            next.SettleSeconds = EditorGUILayout.FloatField("惯性回稳（秒）", next.SettleSeconds);
            next.ReflowSeconds = EditorGUILayout.FloatField("让位 / 重排（秒）", next.ReflowSeconds);
            next.FlightLift = EditorGUILayout.FloatField("飞行抬高", next.FlightLift);
            next.OvershootDistance = EditorGUILayout.FloatField("惯性过冲距离", next.OvershootDistance);
            next.LandingYaw = EditorGUILayout.Slider("惯性摆角（度）", next.LandingYaw, 0, 15);
            if (EditorGUI.EndChangeCheck()) Mutate("调整补牌动画", () => {
                next.Validate(); transitionSettings = next; deckLocalPosition = position; deckAnchor = anchor;
            });
            if (GUILayout.Button("应用柔和惯性（0.65 秒）")) ApplySoftLandingPreset();
            EditorGUILayout.HelpBox("新增身份背面朝上从牌堆依次飞入；可见牌在飞行中翻正，不可见牌保持牌背，动画不修改卡牌属性或可见性。落位后过冲并回摆，原有牌平滑让位。锚点优先，否则使用右侧局部位置。橙色 Gizmo 表示牌堆来源。", MessageType.None);
        }

        private void DrawAudioControls()
        {
            if (Hand == null) { EditorGUILayout.HelpBox("当前牌区没有手牌音效配置。", MessageType.Info); return; }
            DrawAudioSection("抽牌音效", drawSoundEnabled, drawSound, drawSoundVolume, Hand.DrawSound,
                (enabled, clip, volume) => { drawSoundEnabled = enabled; drawSound = clip; drawSoundVolume = volume; });
            DrawAudioSection("弃牌音效", discardSoundEnabled, discardSound, discardSoundVolume, Hand.DiscardSound,
                (enabled, clip, volume) => { discardSoundEnabled = enabled; discardSound = clip; discardSoundVolume = volume; });
            DrawAudioSection("选中音效", selectionSoundEnabled, selectionSound, selectionSoundVolume, Hand.SelectionSound,
                (enabled, clip, volume) => { selectionSoundEnabled = enabled; selectionSound = clip; selectionSoundVolume = volume; });
            EditorGUILayout.HelpBox("三组音效独立设置，留空均使用默认占位音效。抽牌在新牌飞入结束、入列开始回稳时播放；弃牌在开始飞出时播放。选中在未选中 → 选中时播放，取消选中和悬停不播放。在 Play 中试听。", MessageType.None);
        }

        private void DrawAudioSection(string label, bool enabled, AudioClip clip, float volume, AudioClip current,
            Action<bool, AudioClip, float> apply)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            enabled = EditorGUILayout.Toggle("启用" + label, enabled);
            clip = (AudioClip)EditorGUILayout.ObjectField("替换音效（空为默认）", clip, typeof(AudioClip), false);
            volume = EditorGUILayout.Slider("音量", volume, 0, 1);
            if (EditorGUI.EndChangeCheck()) Mutate("调整" + label, () => apply(enabled, clip, volume));
            EditorGUILayout.LabelField("当前音效", current != null ? current.name : "未找到音效");
        }

        private void DrawMotion()
        {
            if (target != null && !target.SupportsSway)
            {
                EditorGUILayout.HelpBox("当前牌区保持静态：没有卡牌持续自转，也没有手牌的上浮或抽弃牌动画。", MessageType.Info);
                return;
            }
            EditorGUILayout.Space(); EditorGUILayout.LabelField("牌区轻微摆动 · DOTween", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck(); var next = motionSettings;
            next.Enabled = EditorGUILayout.Toggle("启用 local Y 摆动", next.Enabled);
            next.MaxSwayAngle = EditorGUILayout.Slider("最大角度（度）", next.MaxSwayAngle, 0, 10);
            next.PeriodSeconds = EditorGUILayout.Vector2Field("完整周期范围（秒）", next.PeriodSeconds);
            next.Seed = EditorGUILayout.IntField("表现随机种子", next.Seed);
            if (EditorGUI.EndChangeCheck()) Mutate("调整牌区摆动", () => motionSettings = next);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("应用轻弧摆动预设")) ApplyGentleMotionPreset();
                if (GUILayout.Button("恢复直排并关闭摆动")) Mutate("恢复静态直排", () => { layout.ArcHeight = 0; motionSettings.Enabled = false; });
            }
            EditorGUILayout.HelpBox("每张牌的幅度、周期与初始相位独立；按展示 ID 保持稳定。弧高受可用高度限制，布局预留旋转空间。", MessageType.None);
        }

    }
}
#endif
