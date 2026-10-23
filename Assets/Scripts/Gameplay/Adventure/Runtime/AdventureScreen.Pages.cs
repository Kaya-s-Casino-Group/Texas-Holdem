using System;
using System.Linq;
using Poker.SingleBattle;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.Adventure
{
    public sealed partial class AdventureScreen
    {
        private void UpdateBackdrop()
        {
            var phase = host.Adventure?.Phase;
            string sprite = host.IsAtMenu ? "MenuBackground" : phase == AdventurePhase.Rest ? "RestBackground" :
                phase == AdventurePhase.Shop ? "ShopBackground" : "MapBackground";
            var art = DeskUISkin.Get(sprite);
            bool result = !host.IsAtMenu && (phase == AdventurePhase.Completed || phase == AdventurePhase.Failed || phase == AdventurePhase.Reward);
            sceneArt.gameObject.SetActive(!result);
            sceneArt.Configure(art != null ? art.texture : homeArt, false);
            sceneArt.color = host.IsAtMenu && setupPage != null ? new Color(.35f, .32f, .35f) : Color.white;
            backdrop.color = new Color32(16, 5, 6, 255);
        }
        private void RelicIcon(Transform parent, string id, Rect area)
        {
            var art = host.BattleView.GetComponentInChildren<BattleDemoRelicArt>(true);
            RitualSkin.Icon(parent, "Relic " + id, area, art != null ? art.Resolve(id) : BattleRelicArt.Get(id));
        }
        private void Menu()
        {
            if (setupPage == "role") { RoleSelection(); return; }
            if (setupPage == "tarot") { TarotSelection(false); return; }
            if (setupPage == "collection") { TarotSelection(true); return; }
            RitualSkin.Place(page, new Rect(0, 0, 1, 1));
            if (DeskUISkin.Get("MenuLogo") != null)
                DeskArt(page, "Shuffled Fate", DeskPixels(566, -8, 787, 439), "MenuLogo", true);
            else Label(page, "SHUFFLED\nFATE", new Rect(.35f, .69f, .31f, .25f), 76, Paper, TextAlignmentOptions.Center);
            const float x = .3562f, width = .2876f, top = .5375f, step = .1002f, height = .0762f;
            DeskButton(page, "开始新旅程", new Rect(x, top, width, height), () => { setupPage = "role"; Refresh(); }, !host.HasUnfinishedRun, "MenuButton", 29);
            DeskButton(page, host.HasUnfinishedRun ? "继续旅程" : "查看上局结算", new Rect(x, top - step, width, height), () => host.Resume(), host.HasSavedRun, "MenuButton", 29);
            DeskButton(page, "塔罗图鉴", new Rect(x, top - step * 2, width, height), () => { setupPage = "collection"; Refresh(); }, true, "MenuButton", 29);
            DeskButton(page, "设置", new Rect(x, top - step * 3, width, height), GameSettings, true, "MenuButton", 29);
            DeskButton(page, "玩法说明", new Rect(x, top - step * 4, width, height), HowToPlay, true, "MenuButton", 29);
            if (host.HasUnfinishedRun)
                DeskButton(page, "放弃当前旅程", new Rect(x, top - step * 5, width, height), ConfirmAbandon, true, "MenuButton", 29);
            else Label(page, "单章七站 · 自动保存", new Rect(x, .035f, width, .045f), 20, Muted, TextAlignmentOptions.Center);
        }
        private void RoleSelection()
        {
            Title(page, "选择角色", "以牌型决定进攻，以弃牌积累防御。");
            var card = DeskPanel(page, "Character", new Rect(.08f, .04f, .84f, .68f));
            RitualSkin.Picture(card, "Wanderer Portrait", new Rect(.015f, .015f, .43f, .97f), heroArt);
            Label(card, "愚者", new Rect(.49f, .70f, .44f, .16f), 50, Paper);
            Label(card, "孤身踏入王庭的持牌者", new Rect(.49f, .56f, .44f, .10f), 24, Gold);
            Label(card, "选择手牌，组成牌型。\n出牌造成伤害，弃牌获得防御。\n收集遗物，改造牌库，走完七站旅程。", new Rect(.49f, .25f, .44f, .27f), 25, Paper);
            Button(card, "选择愚者", new Rect(.49f, .07f, .43f, .13f), () => { setupPage = "tarot"; selectedTarot = null; Refresh(); }, true, true);
            BackToMenu();
        }
        private void TarotSelection(bool readOnly)
        {
            Title(page, readOnly ? "塔罗图鉴" : "选择开局天赋", readOnly ? "每张塔罗都改变旅程的起点。" : "选择一张已解锁塔罗，作为本次旅程的祝福。");
            var unlocked = host.AvailableTarots.Select(t => t.Id).ToArray();
            int count = host.Definition.Tarots.Count(), index = 0;
            foreach (var tarot in host.Definition.Tarots)
            {
                var current = tarot; bool enabled = unlocked.Contains(tarot.Id);
                float width = .29f, start = count == 1 ? .355f : .035f;
                var tile = DeskPanel(page, "Tarot " + tarot.Id, new Rect(start + index % 3 * .32f, .19f, width, .54f));
                tile.GetComponent<Image>().color = selectedTarot == tarot.Id ? new Color(1.18f, 1.02f, .86f) : Color.white;
                if (tarot.Id == "fool")
                    RitualSkin.Picture(tile, "The Fool Portrait", new Rect(.12f, .51f, .76f, .46f), heroArt);
                else if (cardAtlas != null && cardAtlas.TryGetCard("card_back", out var back))
                {
                    var picture = RitualSkin.Picture(tile, "Tarot Seal", new Rect(.32f, .51f, .36f, .40f), cardAtlas.texture);
                    picture.Configure(cardAtlas.texture, false, new Rect(back.uv.x, back.uv.y, back.uv.z, back.uv.w));
                }
                Label(tile, tarot.Name + (enabled ? "" : " · 未解锁"), new Rect(.08f, .39f, .84f, .13f), 31, Gold, TextAlignmentOptions.Center);
                Label(tile, tarot.Description, new Rect(.10f, .19f, .80f, .18f), 23, Paper, TextAlignmentOptions.Center);
                if (!readOnly) Button(tile, selectedTarot == tarot.Id ? "已选择" : "选择", new Rect(.12f, .04f, .76f, .12f), () => { selectedTarot = current.Id; Refresh(); }, enabled, selectedTarot == tarot.Id);
                index++;
            }
            if (!readOnly) Button(page, "踏上旅程", new Rect(.66f, .025f, .30f, .10f), () => { if (host.NewRun(selectedTarot)) setupPage = null; }, selectedTarot != null, true);
            BackToMenu();
        }
        private void Shop()
        {
            var run = host.Adventure;
            RitualSkin.Place(page, new Rect(0, 0, 1, 1));
            // The original shop background already contains its complete, empty cabinet frame.
            var cabinet = Rect(page, "Whisperer Shop", new Rect(0, 0, 1, 1));
            Label(cabinet, "吟唱者的商店", DeskPixels(1046, 143, 545, 69), 34, new Color32(74, 31, 114, 255), TextAlignmentOptions.Center);
            int columns = Math.Max(1, run.ShopItems.Count);
            float stockWidth = 1092f / columns;
            for (int i = 0; i < run.ShopItems.Count; i++)
            {
                var item = run.ShopItems[i]; bool bought = run.PurchasedItemIds.Contains(item.Id);
                int quality = host.Content.Relics[item.RelicId].Definition.Quality;
                var tile = DeskPanel(cabinet, "Stock " + i, DeskPixels(732 + i * stockWidth, 212, stockWidth - 7, 367), "ShopCard");
                RelicIcon(tile, item.RelicId, new Rect(.17f, .44f, .66f, .45f));
                Label(tile, host.ItemName(item.RelicId), new Rect(.07f, .29f, .86f, .13f), 26, Paper, TextAlignmentOptions.Center);
                Label(tile, "品质 " + quality + (quality == 5 ? " · 高风险" : ""), new Rect(.05f, .18f, .90f, .10f), 18, Muted, TextAlignmentOptions.Center);
                DeskButton(tile, bought ? "已售出" : item.Price + " 金币", new Rect(.10f, .04f, .80f, .13f), () => ItemDetail(item), !bought, "Button", 21);
            }
            var kinds = new[] { AdventureCommandKind.DuplicateCard, AdventureCommandKind.ChangeSuit, AdventureCommandKind.DeleteCard };
            var symbols = new[] { "ServiceDuplicate", "ServiceSuit", "ServiceDelete" };
            for (int i = 0; i < kinds.Length; i++)
            {
                var kind = kinds[i]; long price = run.ServicePrice(kind);
                var tile = DeskPanel(cabinet, "Service " + kind, DeskPixels(732 + i * 367, 592, 357, 335), "ShopServiceCard");
                if (DeskUISkin.Get(symbols[i]) != null) DeskArt(tile, "Card Service", new Rect(.33f, .54f, .34f, .38f), symbols[i], true);
                else RitualSkin.Icon(tile, "Card Service", new Rect(.36f, .57f, .28f, .33f), RitualSkin.Symbol(i == 0 ? 0 : i == 1 ? 1 : 2));
                Label(tile, ServiceName(kind), new Rect(.07f, .38f, .86f, .15f), 27, Paper, TextAlignmentOptions.Center);
                Label(tile, "已使用 " + run.ServiceUses(kind) + " 次", new Rect(.07f, .26f, .86f, .10f), 17, Muted, TextAlignmentOptions.Center);
                DeskButton(tile, price + " 金币", new Rect(.10f, .06f, .80f, .17f), () => OpenService(kind), run.Gold >= price, "Button", 22);
            }
            Inventory(footer, new Rect(0, .06f, .65f, .88f), true);
            ContinueButton("离开商店");
        }
        private void Rest()
        {
            var run = host.Adventure;
            RitualSkin.Place(page, new Rect(0, 0, 1, 1));
            var blessing = DeskPanel(page, "Camp Recovery", DeskPixels(656, 100, 608, 92), "MenuButton");
            Label(blessing, "篝火休息 · 已恢复 " + run.Definition.RestHeal + " 点生命", new Rect(.19f, .18f, .69f, .64f), 25, Gold, TextAlignmentOptions.Center);
            var health = DeskPanel(page, "Camp Health", DeskPixels(304, 233, 248, 211));
            Label(health, "生命\n" + run.Health + " / " + run.MaxHealth, new Rect(.10f, .17f, .80f, .66f), 29, Paper, TextAlignmentOptions.Center);
            var body = DeskPanel(page, "Camp", DeskPixels(914, 207, 300, 231));
            Label(body, run.RestLotteryUsed ? "命运已揭晓" : "火光中的机遇", new Rect(.08f, .68f, .84f, .22f), 27, Gold, TextAlignmentOptions.Center);
            Label(body, run.RestLotteryUsed ? RewardText() : "免费抽奖一次\n道具或金币可能增加，也可能减少。", new Rect(.09f, .17f, .82f, .48f), 22, Paper, TextAlignmentOptions.Center);
            if (!run.RestLotteryUsed)
                DeskButton(page, "免费抽奖", DeskPixels(944, 452, 240, 66), () => host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.DrawRestReward)), run.CanDrawRestReward, "Button", 25);
            ContinueButton(run.RestLotteryUsed ? "继续旅程" : "安心休息，继续旅程");
        }
        private void Chest()
        {
            Title(page, "发现宝箱", "宝箱已开启。收下这份馈赠，继续前往王庭。");
            var body = DeskPanel(page, "Treasure", new Rect(.14f, .13f, .72f, .59f));
            RitualSkin.Icon(body, "Treasure Emblem", new Rect(.04f, .13f, .35f, .74f), RitualSkin.Symbol(2));
            Label(body, RewardText(), new Rect(.45f, .22f, .49f, .55f), 34, Gold);
            ContinueButton("收好奖励，继续旅程");
        }
        private void Reward()
        {
            Clear(header);
            RitualSkin.Place(page, new Rect(0, .10f, 1, .80f));
            if (DeskUISkin.Get("VictoryLogo") != null)
                DeskArt(page, "Victory", new Rect(.30f, .47f, .40f, .39f), "VictoryLogo", true);
            else Label(page, "战斗胜利", new Rect(.25f, .60f, .50f, .20f), 82, Gold, TextAlignmentOptions.Center);
            Label(page, host.Adventure.LastRewardGold > 0 ? "+ " + host.Adventure.LastRewardGold + " 金币" : "最后的阻碍已被击败", new Rect(.25f, .30f, .50f, .10f), 38, Gold, TextAlignmentOptions.Center);
            Label(page, "当前生命 " + host.Adventure.Health + " / " + host.Adventure.MaxHealth, new Rect(.25f, .21f, .50f, .07f), 25, Paper, TextAlignmentOptions.Center);
            DeskButton(page, host.Adventure.CurrentNode.NextIds.Count == 0 ? "完成旅程" : "继续旅程", new Rect(.35f, .04f, .30f, .105f), () => host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.Continue)), host.Adventure.CanContinue, "Button", 30);
        }
        private void Result()
        {
            bool won = host.Adventure.Phase == AdventurePhase.Completed;
            Clear(header);
            RitualSkin.Place(page, new Rect(0, .10f, 1, .80f));
            string art = won ? "VictoryLogo" : "FailureLogo";
            if (DeskUISkin.Get(art) != null)
                DeskArt(page, won ? "Victory" : "Failure", new Rect(.28f, .46f, .44f, .42f), art, true);
            else Label(page, won ? "旅程完成" : "旅程结束", new Rect(.25f, .60f, .50f, .20f), 82, Gold, TextAlignmentOptions.Center);
            Label(page, "已完成 " + host.Adventure.VisitedNodeIds.Count + " / 7 站  ·  金币 " + host.Adventure.Gold + "\n牌组 " + host.Adventure.Deck.Count + " 张  ·  携带道具 " + host.Adventure.Relics.Count + " 件", new Rect(.25f, .24f, .50f, .13f), 27, Paper, TextAlignmentOptions.Center);
            DeskButton(page, "返回首页", new Rect(.35f, .05f, .30f, .105f), host.ReturnToMenu, true, "Button", 30);
        }

    }
}
