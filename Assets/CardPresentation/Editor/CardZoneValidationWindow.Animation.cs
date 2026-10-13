#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CardPresentation.Editor
{
    public sealed partial class CardZoneValidationWindow
    {
        [SerializeField] private CardZoneDrawExperiment drawExperiment = new CardZoneDrawExperiment();
        [SerializeField] private CardZoneDrawExperimentStore drawExperiments = new CardZoneDrawExperimentStore();
        [SerializeField] private bool migratedDrawExperiment;
        [SerializeField] private string drawExperimentContext;
        [SerializeField] private int drawCount = 3, drawMode = 1, drawPosition = -1, deckInsertPosition = -1, deckInsertCount = 1;
        [SerializeField] private string specifiedArtId = "hearts_q", deckInsertArtId = "spades_a";
        [SerializeField] private bool specifiedFaceUp = true, showDrawLists, showViewStates, editDeckAdd;
        [SerializeField] private float previewSpeed = 1;
        private double lastAnimationRepaint;
        private string DrawExperimentKey
        {
            get
            {
                // Scene references can be temporarily unavailable while Unity restores the Play scene.
                if (target != null)
                    drawExperimentContext = target.gameObject.scene.path + ":" + target.gameObject.scene.name + ":" +
                        (Host != null && Host.HasSnapshot ? Host.Zone.Id.Value : previewZoneId);
                return string.IsNullOrEmpty(drawExperimentContext) ? "unbound:" + previewZoneId : drawExperimentContext;
            }
        }
        private CardZoneDrawExperiment CurrentExperiment => drawExperiments.Get(DrawExperimentKey, false);
        private int DrawInsertIndex => drawPosition < 0 || drawPosition >= CurrentModel.Cards.Count ? -1 : drawPosition;

        private void InitializeDrawExperiments()
        {
            if (drawExperiments == null) drawExperiments = new CardZoneDrawExperimentStore();
            // Preserve the configured deck in windows created before per-zone storage was introduced.
            if (!migratedDrawExperiment)
            {
                drawExperiments.Get(DrawExperimentKey, false, drawExperiment);
                migratedDrawExperiment = true; drawExperiment = null;
            }
            if (!EditorApplication.isPlayingOrWillChangePlaymode) drawExperiments.EndPlay();
            if (Host != null && Host.HasSnapshot) CurrentExperiment.LoadDeck(Application.isPlaying ? Host.DeckCards : Host.InitialDeckCards);
            else CurrentExperiment.EnsureDeck(ArtIds());
        }

        [MenuItem("Tools/Cards/Card Zone Animation Lab")]
        public static void OpenAnimationLab()
        {
            Open(); var window = GetWindow<CardZoneValidationWindow>();
            window.page = 1; window.drawMode = 1; window.scroll = Vector2.zero; window.Repaint();
        }
        [MenuItem("Tools/Cards/Preview Soft Landing")]
        public static void PreviewSoftLanding()
        {
            OpenAnimationLab(); var window = GetWindow<CardZoneValidationWindow>();
            window.ApplySoftLandingPreset(); window.page = 2; window.stylePage = 1; window.Focus();
        }
        private void ApplySoftLandingPreset() => Mutate("应用柔和惯性", () => {
            var defaults = CardZoneTransitionSettings.Default;
            transitionSettings.SettleSeconds = defaults.SettleSeconds;
            transitionSettings.OvershootDistance = defaults.OvershootDistance;
            transitionSettings.LandingYaw = defaults.LandingYaw;
        });
        private bool CanDraw(int count) => count > 0 && count <= CurrentExperiment.DeckCards.Count &&
            CurrentModel.Cards.Count + count <= CurrentCapacity;

        private void DrawAnimationExperiment(string[] artIds)
        {
            if (Hand == null) { EditorGUILayout.HelpBox("抽牌实验适用于 HandZoneView 及其子类。可在卡牌列表页验证当前牌区。", MessageType.Info); return; }
            drawMode = GUILayout.Toolbar(drawMode, new[] { "指定牌面", "从对局抽牌堆抽取" });
            EditorGUILayout.Space(5);
            drawPosition = InsertionPicker("入手牌的位置", drawPosition, CurrentModel.Cards.Count);
            EditorGUILayout.LabelField("整批飞入并回稳后，按手牌页所选的点数 / 花色顺序重排。", EditorStyles.miniLabel);
            drawCount = Mathf.Clamp(EditorGUILayout.IntField("抽牌数量", drawCount), 1, CardZonePreviewModel.MaximumCards);
            if (drawMode == 0)
            {
                specifiedArtId = CardPicker("指定牌面", specifiedArtId, artIds);
                specifiedFaceUp = EditorGUILayout.Toggle("观察者可见（正面）", specifiedFaceUp);
                using (new EditorGUI.DisabledScope(drawCount > CurrentCapacity - CurrentModel.Cards.Count))
                    if (GUILayout.Button("抽取 " + drawCount + " 张 · " + CardZonePreviewCatalog.LabelFor(specifiedArtId), GUILayout.Height(28)))
                        Mutate("抽取指定牌面", () => {
                            CurrentExperiment.DrawSpecified(CurrentModel, specifiedArtId, drawCount, DrawInsertIndex, specifiedFaceUp); InspectDrawnCard();
                        });
                EditorGUILayout.HelpBox("生成指定的新卡并走抽牌流程；不消耗对局抽牌堆，同牌面也有独立身份。", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(Application.isPlaying ?
                    "读取并编辑当前运行对局的抽牌堆；退出 Play 后恢复对局启动快照。" :
                    "读取绑定对局的抽牌堆；共享同一堆的牌区会看到同一份顺序、身份和属性。", MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!CanDraw(1))) if (GUILayout.Button("牌顶抽 1 张")) DealFromDeck(1);
                    using (new EditorGUI.DisabledScope(!CanDraw(drawCount))) if (GUILayout.Button("牌顶抽 " + drawCount + " 张")) DealFromDeck(drawCount);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("重建 12 张牌堆")) Mutate("重建牌堆", () => CurrentExperiment.PrepareDeck(artIds));
                    if (GUILayout.Button("重建 52 张标准牌")) Mutate("重建标准牌堆", () => {
                        var standard = artIds.Where(id => CardZonePreviewCatalog.StandardDefinitionFor(CardZonePreviewCatalog.DefinitionFor(id)) != null).ToArray();
                        CurrentExperiment.PrepareDeck(standard, standard.Length);
                    });
                }
                editDeckAdd = EditorGUILayout.Foldout(editDeckAdd, "向牌堆插入指定牌", true);
                if (editDeckAdd)
                {
                    deckInsertArtId = CardPicker("牌面", deckInsertArtId, artIds);
                    deckInsertCount = Mathf.Clamp(EditorGUILayout.IntField("数量", deckInsertCount), 1, CardZonePreviewModel.MaximumCards);
                    deckInsertPosition = InsertionPicker("插入位置", deckInsertPosition, CurrentExperiment.DeckCards.Count, deck: true);
                    using (new EditorGUI.DisabledScope(deckInsertCount > CardZonePreviewModel.MaximumCards - CurrentExperiment.DeckCards.Count))
                        if (GUILayout.Button("插入牌堆")) Mutate("插入牌堆", () => {
                            var display = new CardZonePreviewList(CurrentExperiment.Deck, topFirst: true);
                            int row = deckInsertPosition < 0 ? display.Cards.Count : deckInsertPosition;
                            display.Insert(row, deckInsertArtId, deckInsertCount);
                            inspectedDeckId = display.Cards[row].Id;
                        });
                }
                inspectedDeckId = DrawEditableList(CurrentExperiment.Deck, inspectedDeckId, artIds, true, ref deckScroll);
            }
            EditorGUILayout.Space(8); DrawDiscardAction(); DrawAnimationClock();
        }
        private void DrawAnimationClock()
        {
            EditorGUILayout.LabelField("动画预览", EditorStyles.boldLabel);
            if (!Application.isPlaying)
            {
                previewMotion = EditorGUILayout.Toggle("播放动画", previewMotion);
                previewSpeed = EditorGUILayout.Slider("预览速度", previewSpeed, 0.1f, 2);
                if (GUILayout.Button("暂停并推进 0.05 秒")) Run(() => { previewMotion = false; target.AdvanceMotion(0.05f); SceneView.RepaintAll(); });
                EditorGUILayout.LabelField("进入 Play 可同时验证音效与鼠标交互。", EditorStyles.miniLabel);
            }
            else EditorGUILayout.LabelField("随 Play 时间推进；暂停请使用 Unity 工具栏。", EditorStyles.miniLabel);
            if (GUILayout.Button("立即完成入列 / 让位 / 弃牌")) Run(() => target.CompleteTransitions());
            EditorGUILayout.LabelField("状态", target != null && target.IsTransitioning ? "过渡中" : "已落位");
        }
        private static string ShortId(string id) => id.Length <= 8 ? id : id.Substring(id.Length - 8);
        private void DrawIdList(string label, IEnumerable<string> ids)
        {
            var values = ids.ToArray();
            EditorGUILayout.LabelField(label + " · " + values.Length + " 张", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(values.Length == 0 ? "（空）" : string.Join(" → ", values.Select(ShortId)), EditorStyles.wordWrappedMiniLabel);
        }
        private void DrawDrawDiagnostics()
        {
            var discarded = Application.isPlaying && Host != null ? Host.DiscardedCards : discardedCards;
            DrawIdList("弃牌堆数据", discarded.Select(card => card.Id));
            if (Hand != null) foreach (var view in Hand.DepartingViews)
                if (view != null) EditorGUILayout.LabelField(ShortId(view.ViewId) + " · 飞向弃牌堆", view.DealFlipAngle.ToString("F1") + "°");
            var experiment = CurrentExperiment;
            showDrawLists = EditorGUILayout.Foldout(showDrawLists, "最近抽牌 · 列表前后对照", true);
            if (showDrawLists)
            {
                if (!experiment.HasDraw) EditorGUILayout.LabelField("尚未抽牌。", EditorStyles.miniLabel);
                else
                {
                    EditorGUILayout.LabelField(experiment.GeneratedDraw ? "来源：指定生成（对局抽牌堆不变）" : "来源：对局抽牌堆", EditorStyles.miniLabel);
                    DrawIdList("牌堆之前（牌顶 → 牌底）", experiment.DeckBefore.Reverse());
                    DrawIdList("牌堆之后（牌顶 → 牌底）", experiment.DeckAfter.Reverse());
                    DrawIdList("手牌之前", experiment.HandBefore); DrawIdList("手牌之后", experiment.HandAfter); DrawIdList("本次抽取", experiment.Drawn);
                    if (!CurrentModel.Cards.Select(card => card.Id).SequenceEqual(experiment.HandAfter))
                        EditorGUILayout.LabelField("抽牌后列表已编辑；以上保留当次记录。", EditorStyles.miniLabel);
                }
            }
            showViewStates = EditorGUILayout.Foldout(showViewStates, "逐牌视觉状态", true);
            if (showViewStates && target != null) foreach (var view in target.Views)
            {
                if (view == null) continue;
                string phase = view.IsEntering ? (view.Surface.GetComponent<MeshRenderer>().enabled ? "飞入 / 回稳" : "等待") : view.IsTransitioning ? "让位" : "已落位";
                EditorGUILayout.LabelField(ShortId(view.ViewId) + " · " + phase, EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("位置 → 目标", view.DisplayPose.Position.ToString("F3") + " → " + view.transform.localPosition.ToString("F3"));
                EditorGUILayout.LabelField("翻面偏角 / 可见", view.DealFlipAngle.ToString("F1") + "° / " + view.Data.FaceUp);
            }
        }
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
