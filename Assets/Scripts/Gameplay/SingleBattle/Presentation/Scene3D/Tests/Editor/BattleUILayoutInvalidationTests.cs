using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle.Tests
{
    public sealed class BattleUILayoutInvalidationTests
    {
        private GameObject controller, page;
        private BattleAuthoredUILayout layout;
        private BattleUILayoutProfile profile;

        [SetUp]
        public void SetUp()
        {
            controller = new GameObject("Layout owner");
            page = new GameObject("DeckOverview", typeof(RectTransform), typeof(Canvas));
            page.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)page.transform).sizeDelta = new Vector2(640, 360);
            profile = ScriptableObject.CreateInstance<BattleUILayoutProfile>();
            profile.nodes = new[] { Node("Content", new Vector2(120, 240)) };
            layout = controller.AddComponent<BattleAuthoredUILayout>();
            layout.Configure(profile, new[] { new BattleAuthoredUILayout.Root { name = "DeckOverview", transform = page.transform } });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(controller); Object.DestroyImmediate(page); Object.DestroyImmediate(profile);
        }

        private static BattleUILayoutProfile.Node Node(string path, Vector2 size) => new BattleUILayoutProfile.Node
        {
            root = "DeckOverview",
            pose = new BattleLayoutProfile.RectPose { path = path, sizeDelta = size, scale = Vector3.one, rotation = Quaternion.identity }
        };

        private RectTransform AddContent()
        {
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(page.transform, false); return content;
        }

        [Test]
        public void LateControlsResizeAndReopeningInvalidateButIdleScrollingDoesNot()
        {
            Assert.IsTrue(layout.RefreshLayout());
            Assert.IsFalse(layout.RefreshLayout());
            var content = AddContent();
            Assert.IsTrue(layout.RefreshLayout(), "A control created after the initial layout was missed");
            Assert.That(content.sizeDelta, Is.EqualTo(new Vector2(120, 240)));

            // ScrollRect owns this transient position. Reading the unchanged page must not reset it.
            content.anchoredPosition = new Vector2(0, 73);
            for (int frame = 0; frame < 30; frame++) Assert.IsFalse(layout.RefreshLayout());
            Assert.That(content.anchoredPosition.y, Is.EqualTo(73));

            ((RectTransform)page.transform).sizeDelta = new Vector2(900, 500);
            Assert.IsTrue(layout.RefreshLayout(), "The page reference size changed");
            Assert.IsFalse(layout.RefreshLayout());

            page.SetActive(false); Assert.IsTrue(layout.RefreshLayout());
            content.sizeDelta = Vector2.zero;
            page.SetActive(true); Assert.IsTrue(layout.RefreshLayout(), "Reopened pooled pages need their authored geometry");
            Assert.That(content.sizeDelta, Is.EqualTo(new Vector2(120, 240)));
            Assert.IsFalse(layout.RefreshLayout());
        }

        [Test]
        public void ChangedProfileAndExplicitRebindingResolveCurrentControls()
        {
            var content = AddContent(); layout.RefreshLayout();
            profile.nodes = new[] { Node("Content", new Vector2(300, 400)) };
            Assert.IsTrue(layout.RefreshLayout());
            Assert.That(content.sizeDelta, Is.EqualTo(new Vector2(300, 400)));

            // Pool owners can replace an equal-sized subtree without changing hierarchyCount.
            Object.DestroyImmediate(content.gameObject);
            var replacement = AddContent(); layout.InvalidateLayout();
            Assert.IsTrue(layout.RefreshLayout());
            Assert.That(replacement.sizeDelta, Is.EqualTo(new Vector2(300, 400)));
            Assert.That(layout.Resolve("DeckOverview", "Content"), Is.SameAs(replacement));
            Assert.IsFalse(layout.RefreshLayout());
        }
    }
}
