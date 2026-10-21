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
            bool rest = !host.IsAtMenu && host.Adventure?.Phase == AdventurePhase.Rest;
            bool map = !host.IsAtMenu && host.Adventure?.Phase == AdventurePhase.Map;
            sceneArt.Configure(rest ? Resources.Load<Texture2D>("RitualUI/Campfire") : map ? Resources.Load<Texture2D>("RitualUI/Map") : homeArt, true);
            sceneArt.color = rest ? new Color(.85f, .85f, .85f) : map ? new Color(.68f, .62f, .55f) : new Color(.32f, .28f, .32f);
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
            RitualSkin.Picture(page, "The Wanderer", new Rect(.48f, -.13f, .53f, 1.18f), heroArt);
            Label(page, "JOKER’S PACT", new Rect(.025f, .73f, .57f, .19f), 68, Paper);
            Label(page, "命运在牌中 · 第一章", new Rect(.03f, .63f, .49f, .09f), 29, Gold);
            Label(page, "七处地点，一副牌。\n穿过破碎王庭，挑战最后的守门人。", new Rect(.03f, .47f, .43f, .13f), 25, Muted);
            Button(page, "开始新旅程", new Rect(.03f, .31f, .33f, .105f), () => { setupPage = "role"; Refresh(); }, !host.HasUnfinishedRun, true);
            Button(page, host.HasUnfinishedRun ? "继续旅程" : "查看上局结算", new Rect(.03f, .175f, .33f, .105f), () => host.Resume(), host.HasSavedRun);
            Button(page, "塔罗图鉴", new Rect(.03f, .04f, .33f, .105f), () => { setupPage = "collection"; Refresh(); });
            if (host.HasUnfinishedRun) Button(footer, "放弃当前旅程", new Rect(.70f, .3f, .30f, .55f), ConfirmAbandon);
            Label(footer, "单章七站  /  自动保存  /  扑克牌构筑", new Rect(0, .38f, .60f, .40f), 21, Muted);
        }
        private void RoleSelection()
        {
            Title(page, "选择角色", "以牌型决定进攻，以弃牌积累防御。");
            var card = Box(page, "Character", new Rect(.08f, .04f, .84f, .68f), Panel);
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
                var tile = Box(page, "Tarot " + tarot.Id, new Rect(start + index % 3 * .32f, .19f, width, .54f), selectedTarot == tarot.Id ? Accent : Panel);
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
            Title(page, "旅途商店", "查看遗物效果后购买；这里也能永久改造你的牌库。");
            for (int i = 0; i < run.ShopItems.Count; i++)
            {
                var item = run.ShopItems[i]; bool bought = run.PurchasedItemIds.Contains(item.Id);
                int quality = host.Content.Relics[item.RelicId].Definition.Quality;
                var tile = Box(page, "Stock " + i, new Rect(.01f + i * .25f, .36f, .23f, .38f), Panel);
                RelicIcon(tile, item.RelicId, new Rect(.29f, .43f, .42f, .36f));
                Label(tile, "品质 " + quality + (quality == 5 ? " · 高风险" : ""), new Rect(.07f, .84f, .86f, .14f), 18, Gold, TextAlignmentOptions.Center);
                Label(tile, host.ItemName(item.RelicId), new Rect(.07f, .25f, .86f, .16f), 28, Paper, TextAlignmentOptions.Center);
                Button(tile, bought ? "已售出" : item.Price + " 金币 · 查看", new Rect(.07f, .06f, .86f, .17f), () => ItemDetail(item), !bought);
            }
            Label(page, "牌库改造", new Rect(.01f, .29f, .28f, .055f), 25, Gold);
            var kinds = new[] { AdventureCommandKind.DeleteCard, AdventureCommandKind.DuplicateCard, AdventureCommandKind.ChangeSuit };
            for (int i = 0; i < kinds.Length; i++)
            {
                var kind = kinds[i]; long price = run.ServicePrice(kind);
                Button(page, ServiceName(kind) + " · " + price + " 金币\n已使用 " + run.ServiceUses(kind) + " 次", new Rect(.01f + i * .333f, .16f, .31f, .11f), () => OpenService(kind), run.Gold >= price);
            }
            Inventory(page, new Rect(.01f, 0, .98f, .13f), true); ContinueButton("离开商店");
        }
        private void Rest()
        {
            var run = host.Adventure;
            Title(page, "篝火休息", "已恢复 " + run.Definition.RestHeal + " 点生命 · 当前 " + run.Health + " / " + run.MaxHealth);
            var body = Box(page, "Camp", new Rect(.61f, .08f, .38f, .64f), Panel);
            RitualSkin.Icon(body, "Campfire Emblem", new Rect(.37f, .68f, .26f, .24f), RitualSkin.Symbol(4));
            Label(body, run.RestLotteryUsed ? "命运已揭晓" : "火光中的机遇", new Rect(.07f, .53f, .86f, .13f), 34, Gold, TextAlignmentOptions.Center);
            Label(body, run.RestLotteryUsed ? RewardText() : "免费抽奖一次\n\n道具或金币可能增加，也可能减少。\n三类结果等概率；也可以安心离开。", new Rect(.08f, .18f, .84f, .31f), 24, Paper, TextAlignmentOptions.Center);
            if (!run.RestLotteryUsed) Button(body, "免费抽奖", new Rect(.13f, .045f, .74f, .11f), () => host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.DrawRestReward)), run.CanDrawRestReward, true);
            ContinueButton(run.RestLotteryUsed ? "继续旅程" : "不抽奖，继续旅程");
        }
        private void Chest()
        {
            Title(page, "发现宝箱", "宝箱已开启。收下这份馈赠，继续前往王庭。");
            var body = Box(page, "Treasure", new Rect(.14f, .13f, .72f, .59f), Panel);
            RitualSkin.Icon(body, "Treasure Emblem", new Rect(.04f, .13f, .35f, .74f), RitualSkin.Symbol(2));
            Label(body, RewardText(), new Rect(.45f, .22f, .49f, .55f), 34, Gold);
            ContinueButton("收好奖励，继续旅程");
        }
        private void Reward()
        {
            Title(page, "战斗胜利", "前路已清。带着这次战斗的收获，继续你的旅程。");
            var body = Box(page, "Victory Reward", new Rect(.18f, .15f, .64f, .57f), Panel);
            RitualSkin.Icon(body, "Victory Emblem", new Rect(.39f, .59f, .22f, .30f), RitualSkin.Symbol(1));
            Label(body, host.Adventure.LastRewardGold > 0 ? "+ " + host.Adventure.LastRewardGold + " 金币" : "最后的阻碍已被击败", new Rect(.08f, .30f, .84f, .23f), 48, Gold, TextAlignmentOptions.Center);
            Label(body, "当前生命 " + host.Adventure.Health + " / " + host.Adventure.MaxHealth, new Rect(.08f, .12f, .84f, .12f), 27, Paper, TextAlignmentOptions.Center);
            ContinueButton(host.Adventure.CurrentNode.NextIds.Count == 0 ? "完成旅程" : "继续旅程");
        }
        private void Result()
        {
            bool won = host.Adventure.Phase == AdventurePhase.Completed;
            Title(page, won ? "旅程完成" : "旅程结束", won ? "你穿越了破碎王庭，兑现了与命运的契约。" : "命运尚未结束。整理收获，准备下一次出发。");
            var body = Box(page, "Run Results", new Rect(.16f, .12f, .68f, .60f), Panel);
            RitualSkin.Icon(body, "Crown", new Rect(.06f, .19f, .30f, .65f), RitualSkin.Symbol(won ? 5 : 0));
            Label(body, "已完成 " + host.Adventure.VisitedNodeIds.Count + " / 7 站\n金币 " + host.Adventure.Gold + "   ·   牌组 " + host.Adventure.Deck.Count + " 张\n携带道具 " + host.Adventure.Relics.Count + " 件", new Rect(.43f, .23f, .51f, .55f), 31, Paper);
            Button(footer, "返回主菜单", new Rect(.68f, .35f, .32f, .6f), host.ReturnToMenu, true, true);
        }
    }
}
