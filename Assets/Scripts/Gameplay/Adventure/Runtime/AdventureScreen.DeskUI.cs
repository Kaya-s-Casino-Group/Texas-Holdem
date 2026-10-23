using System;
using Poker.SingleBattle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIFoundation;

namespace Poker.Adventure
{
    public sealed partial class AdventureScreen
    {
        // The authored desk UI is 1920 x 1080. All interactive elements remain native UI.
        private static Rect DeskPixels(float x, float y, float width, float height) =>
            new Rect(x / 1920f, 1 - (y + height) / 1080f, width / 1920f, height / 1080f);

        private Image DeskArt(Transform parent, string name, Rect area, string sprite, bool preserveAspect = true)
        {
            var art = DeskUISkin.Decor(parent, name, area, sprite);
            art.preserveAspect = preserveAspect;
            return art;
        }

        private RectTransform DeskPanel(Transform parent, string name, Rect area, string sprite = "Panel")
        {
            var panel = Box(parent, name, area, Panel);
            if (DeskUISkin.Get(sprite) != null) DeskUISkin.Panel(panel.GetComponent<Image>(), sprite);
            return panel;
        }

        private Button DeskButton(Transform parent, string caption, Rect area, Action action,
            bool enabled = true, string sprite = "Button", int size = 25)
        {
            var button = Button(parent, caption, area, action, enabled, false, sprite);
            var label = button.GetComponentInChildren<TMP_Text>();
            label.fontSize = label.fontSizeMax = size;
            label.fontSizeMin = Mathf.Max(12, size * .6f);
            label.color = enabled ? Paper : Muted;
            return button;
        }

        private void JourneyHeader()
        {
            var run = host.Adventure;
            var health = DeskPanel(header, "Journey Health", new Rect(0, 0, .17f, 1), "RoundFrame");
            Label(health, "生命 " + run.Health + " / " + run.MaxHealth, new Rect(.10f, .24f, .80f, .50f), 21, Paper, TextAlignmentOptions.Center);
            var gold = DeskPanel(header, "Journey Gold", new Rect(.18f, 0, .14f, 1), "RoundFrame");
            Label(gold, "金币 " + run.Gold, new Rect(.10f, .24f, .80f, .50f), 21, Gold, TextAlignmentOptions.Center);
            var progress = DeskPanel(header, "Journey Status", new Rect(.38f, 0, .28f, 1), "MenuButton");
            Label(progress, "第一章 · 破碎王庭", new Rect(.10f, .18f, .80f, .64f), 25, Paper, TextAlignmentOptions.Center);
            DeskButton(header, "设置", new Rect(.76f, .14f, .08f, .72f), GameSettings, true, "Button", 20);
            var home = DeskButton(header, "保存并返回首页", new Rect(.85f, .14f, .15f, .72f), host.ReturnToMenu, !host.IsBusy, "Button", 20);
            home.GetComponentInChildren<TMP_Text>().color = Muted;
        }

        private void GameSettings()
        {
            var modal = Modal("设置");
            var audio = AdventureAudioController.FindInScene(gameObject.scene);
            if (audio == null)
            {
                Label(modal, "此场景尚未绑定声音配置。", new Rect(.10f, .52f, .80f, .20f), 28, Paper, TextAlignmentOptions.Center);
            }
            else
            {
                AudioSlider(modal, "背景音乐", .68f, audio.MusicVolume, value => audio.MusicVolume = value);
                AudioSlider(modal, "音效", .48f, audio.EffectsVolume, value => audio.EffectsVolume = value);
            }
            if (PokerDisplaySettings.SupportsDesktopResolution)
            {
                var selected = PokerDisplaySettings.SelectedPreset;
                Label(modal, "窗口分辨率", new Rect(.10f, .32f, .80f, .07f), 28, Paper);
                var resolution = Label(modal, "", new Rect(.23f, .21f, .40f, .08f), 27, Gold, TextAlignmentOptions.Center);
                var status = Label(modal, "应用后自动保存", new Rect(.10f, .10f, .80f, .07f), 22, Muted, TextAlignmentOptions.Center);
                Action refresh = () =>
                {
                    var size = PokerDisplaySettings.GetResolution(selected);
                    resolution.text = size.x + " × " + size.y;
                    status.text = selected == PokerDisplaySettings.SelectedPreset ? "当前设置 · 下次启动沿用" : "点击应用以切换分辨率";
                };
                DeskButton(modal, "上一项", new Rect(.10f, .20f, .13f, .10f), () =>
                {
                    selected = (selected + PokerDisplaySettings.PresetCount - 1) % PokerDisplaySettings.PresetCount;
                    refresh();
                }, size: 21);
                DeskButton(modal, "下一项", new Rect(.63f, .20f, .13f, .10f), () =>
                {
                    selected = (selected + 1) % PokerDisplaySettings.PresetCount;
                    refresh();
                }, size: 21);
                DeskButton(modal, "应用", new Rect(.78f, .20f, .12f, .10f), () =>
                {
                    PokerDisplaySettings.SelectPreset(selected);
                    refresh();
                }, size: 23);
                refresh();
            }
            else
                Label(modal, "拖动调整音量 · 最左侧为静音", new Rect(.10f, .20f, .80f, .08f), 23, Muted, TextAlignmentOptions.Center);
        }

        private void AudioSlider(RectTransform parent, string caption, float y, float initial, Action<float> changed)
        {
            Label(parent, caption, new Rect(.10f, y + .08f, .6f, .08f), 28, Paper);
            var valueLabel = Label(parent, Mathf.RoundToInt(initial * 100) + "%", new Rect(.73f, y + .08f, .17f, .08f), 25, Gold, TextAlignmentOptions.Right);
            var root = Rect(parent, caption + " Volume", new Rect(.10f, y, .80f, .055f));
            var track = Rect(root, "Track", new Rect(0, .32f, 1, .36f));
            var trackImage = track.gameObject.AddComponent<Image>(); trackImage.color = new Color32(56, 43, 42, 255);
            var fillArea = Rect(root, "Fill Area", new Rect(.01f, .32f, .98f, .36f));
            var fill = Rect(fillArea, "Fill", new Rect(0, 0, 1, 1));
            var fillImage = fill.gameObject.AddComponent<Image>(); fillImage.color = Gold;
            var handleArea = Rect(root, "Handle Area", new Rect(.01f, 0, .98f, 1));
            var handle = Rect(handleArea, "Handle", new Rect(0, 0, 0, 1)); handle.sizeDelta = new Vector2(18, 0);
            var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = Paper;
            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage;
            slider.minValue = 0; slider.maxValue = 1; slider.SetValueWithoutNotify(initial);
            slider.onValueChanged.AddListener(value => { changed(value); valueLabel.text = Mathf.RoundToInt(value * 100) + "%"; });
        }

        private void HowToPlay()
        {
            var modal = Modal("玩法说明");
            Label(modal, "以牌型进攻，以弃牌防御", new Rect(.08f, .70f, .84f, .10f), 36, Gold, TextAlignmentOptions.Center);
            Label(modal, "从手牌中选择最多五张，组成扑克牌型后出牌。\n弃牌可以积累防御；遗物会改变你的战斗方式。\n\n从地图前往下一处高亮地点。商店可购买遗物和改造牌库，\n休息处恢复生命。完成七站，挑战暴怒与它的五只手。", new Rect(.10f, .22f, .80f, .44f), 27, Paper);
            Label(modal, "旅程自动保存，可以随时从主菜单继续。", new Rect(.10f, .10f, .80f, .07f), 22, Muted, TextAlignmentOptions.Center);
        }
    }
}
