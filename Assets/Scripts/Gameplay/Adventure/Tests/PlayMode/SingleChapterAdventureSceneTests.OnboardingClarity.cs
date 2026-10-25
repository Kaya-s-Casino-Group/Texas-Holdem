#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using CardArt;
using CardPresentation;
using NUnit.Framework;
using Poker.SingleBattle;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Poker.Adventure.Tests
{
    public sealed partial class SingleChapterAdventureSceneTests
    {
        [UnityTest]
        public IEnumerator FirstBattleTeachingCanBeSkippedWithoutAdvancingTheRealBattleAndThenAllowsDiscard()
        {
            PlayerPrefs.DeleteKey(TeachingPreference);
            yield return ReachBattle("battle-1");
            var view = host.BattleView;
            Assert.IsTrue(VisibleText("首次战斗"));
            Assert.IsTrue(view.IsHandGuideOpen);
            Assert.IsFalse(view.Player.CanInteract);
            long revision = host.Revision;
            Assert.IsFalse(view.Player.EndTurn(false));
            Assert.AreEqual(revision, host.Revision);
            yield return Capture("clarity-first-battle-teaching");
            yield return Click("跳过教学");
            Assert.IsFalse(view.IsHandGuideOpen);
            Assert.IsTrue(view.Player.CanInteract);
            Assert.AreEqual(revision, host.Revision);
            screen.Refresh(); yield return null;
            Assert.IsFalse(VisibleText("首次战斗"));

            yield return InformationHand("standard:1:13", "standard:2:12", "standard:3:11");
            long block = view.State.Duel.Combat.Get(host.Adventure.PlayerId).Block;
            yield return ClickButton(view.Actions.Panel.DiscardButton);
            yield return Idle();
            Assert.AreEqual(block + 36, view.State.Duel.Combat.Get(host.Adventure.PlayerId).Block);
            Assert.AreEqual(3, view.Actions.Hand.Cards.Count, "The controlled three-card hand must receive exactly three replacements.");
            yield return EndAdventureRound();
            Assert.AreEqual(0, view.State.Duel.Combat.Get(host.Adventure.PlayerId).Block,
                "Remaining default block must expire at the next player turn start.");
            Assert.IsFalse(VisibleText("首次战斗"));
            yield return Capture("clarity-next-player-turn");
        }

        [UnityTest]
        public IEnumerator MenuHelpAndFaceSuitCornersRenderThroughTheExistingGuideAndHand()
        {
            yield return Click("玩法说明");
            Assert.IsTrue(screen.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("格挡")));
            yield return Capture("clarity-menu-help");
            yield return Click("关闭");
            yield return ReachBattle("battle-1");
            yield return InformationHand("standard:1:11", "standard:2:12", "standard:3:13", "standard:4:11", "standard:1:12");
            var cards = host.BattleView.Actions.Hand.GetComponentsInChildren<CardView>();
            Assert.AreEqual(5, cards.Length);
            foreach (var card in cards)
            {
                Assert.AreEqual("FourBeastsAtlas", card.Atlas.name);
                Assert.IsTrue(card.GetComponentsInChildren<MeshRenderer>().Any(renderer => renderer.gameObject != card.gameObject && renderer.enabled),
                    "Rendered face cards must have the readable corner layer.");
            }
            yield return Capture("clarity-face-suits");
            var hud = Object.FindObjectOfType<SingleBattleHud>();
            hud.OpenHandGuide(); yield return null; Canvas.ForceUpdateCanvases();
            Assert.IsTrue(host.BattleView.IsHandGuideOpen);
            var straight = hud.GetComponentsInChildren<Button>().Single(button =>
                button.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "顺子"));
            yield return ClickButton(straight);
            float deadline = Time.realtimeSinceStartup + 10;
            while (!hud.GetComponentsInChildren<CardUIGraphic>().Any() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(hud.GetComponentsInChildren<CardUIGraphic>(true).Any(graphic =>
                graphic.GetComponentsInChildren<Graphic>(true).Any(child => child != graphic && !child.raycastTarget)),
                "Direct guide card graphics must share the readable corner layer.");
            yield return Capture("clarity-hand-guide");
            hud.CloseHandGuide(); yield return null;
            Assert.IsFalse(host.BattleView.IsHandGuideOpen);
            Assert.IsTrue(host.BattleView.Player.CanInteract);
        }

        [UnityTest]
        public IEnumerator RealBattleChestAndLocationInterestNameTheirGoldSourcesWithoutReapplyingIncome()
        {
            yield return ReachBattle("battle-1");
            yield return WinBattleWithCard(50);
            AssertGoldReceipt("战斗奖励", "+50", host.Adventure.Gold);
            yield return Capture("clarity-battle-gold");
            yield return Click("继续旅程");
            Assert.IsTrue(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, "chest")), host.Message);
            yield return Idle();
            AssertGoldReceipt("宝箱奖励", "+100", host.Adventure.Gold);
            yield return Capture("clarity-chest-gold");
            while (host.Adventure.PendingRelicRewards.Count > 0)
                Assert.IsTrue(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.SkipRelicReward)), host.Message);
            Assert.IsTrue(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.Continue)), host.Message);
            Assert.IsTrue(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, "battle-2")), host.Message);
            yield return Idle();
            Assert.IsTrue(host.BattleView.Playback.ApplyAdmin(host.Revision,
                new BattleAdminPatch(relics: new[] { new BattleAdminRelics(host.Adventure.PlayerId, new[] { new BattleAdminRelic("11003") }) })).Accepted);
            yield return Idle();
            yield return WinBattleWithCard(60);
            AssertGoldReceipt("战斗奖励", "+60", host.Adventure.Gold);
            long before = host.Adventure.Gold;
            yield return Click("继续旅程");
            long interest = before / 10;
            Assert.AreEqual(before + interest, host.Adventure.Gold);
            AssertGoldReceipt("存折", "+" + interest, host.Adventure.Gold);
            yield return Capture("clarity-location-interest");
            string receipt = ClarityGoldReceipt();
            string snapshot = host.Adventure.Export();
            for (int i = 0; i < 3; i++) { screen.Refresh(); yield return null; }
            Assert.AreEqual(snapshot, host.Adventure.Export());
            Assert.AreEqual(receipt, ClarityGoldReceipt());
            host.ReturnToMenu(); yield return null;
            Assert.IsTrue(host.Resume()); yield return Idle();
            Assert.AreEqual(snapshot, host.Adventure.Export());
            Assert.IsEmpty(ClarityGoldReceipt(), "Resume must not announce the last interest as fresh income.");

            Assert.IsTrue(host.ExecuteAdventure(new AdventureCommand(AdventureCommandKind.EnterNode, "shop-1")), host.Message);
            yield return Idle();
            var service = screen.GetComponentsInChildren<Button>().Single(button => button.name == "Service DuplicateCard");
            yield return ClickButton(service);
            var thumbnails = screen.GetComponentsInChildren<RitualPicture>().Where(picture => picture.name == "FourBeasts Card").ToArray();
            Assert.AreEqual(52, thumbnails.Length);
            Assert.IsTrue(thumbnails.All(picture => picture.GetComponentInChildren<CardSuitCornerGraphic>() != null),
                "The shop's actual deck-service thumbnails must use the same readable suits as the hand and guide.");
            Canvas.ForceUpdateCanvases();
            foreach (var picture in thumbnails)
            {
                var suit = picture.GetComponentInChildren<CardSuitCornerGraphic>();
                var bounds = ((RectTransform)suit.transform).rect;
                Assert.That(bounds.width / bounds.height, Is.EqualTo(2f / 3f).Within(.01f),
                    "The suit layer must follow the contained card artwork, not its wider thumbnail cell.");
            }
            yield return new WaitForEndOfFrame();
            float shaderDeadline = Time.realtimeSinceStartup + 20;
            while (ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < shaderDeadline) yield return null;
            Assert.IsFalse(ShaderUtil.anythingCompiling, "The shop shader variant did not finish compiling.");
            yield return new WaitForEndOfFrame();
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                // FourBeasts cards contain black/red ink and paper. Check the
                // actual clipped shop grid, not just the presence of its layer.
                int cyan = 0;
                foreach (var picture in thumbnails.Take(10))
                {
                    var corners = new Vector3[4]; ((RectTransform)picture.transform).GetWorldCorners(corners);
                    var bottom = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                    var top = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                    int left = Mathf.Clamp(Mathf.CeilToInt(bottom.x), 0, frame.width - 1);
                    int lower = Mathf.Clamp(Mathf.CeilToInt(bottom.y), 0, frame.height - 1);
                    int width = Mathf.Clamp(Mathf.FloorToInt(top.x) - left, 1, frame.width - left);
                    int height = Mathf.Clamp(Mathf.FloorToInt(top.y) - lower, 1, frame.height - lower);
                    cyan += frame.GetPixels(left, lower, width, height).Count(pixel => pixel.r < .15f && pixel.g > .85f && pixel.b > .85f);
                }
                Assert.That(cyan, Is.Zero, "Readable suits must not produce cyan rectangles in the actual shop scroll grid.");
            }
            finally { Object.Destroy(frame); }
            yield return Capture("clarity-shop-deck-suits");
        }

        private string ClarityGoldReceipt()
        {
            var rows = screen.GetComponentsInChildren<Transform>().Where(item => item.name == "Gold Income Feedback").ToArray();
            Assert.LessOrEqual(rows.Length, 1, "Refresh cannot accumulate duplicate receipts.");
            return string.Join("\n", rows.SelectMany(row => row.GetComponentsInChildren<TMP_Text>()).Select(text => text.text));
        }

        private void AssertGoldReceipt(string source, string amount, long balance)
        {
            string receipt = ClarityGoldReceipt();
            StringAssert.Contains(source, receipt);
            StringAssert.Contains(amount, receipt);
            StringAssert.Contains("余额 " + balance, receipt);
        }
    }
}
#endif
