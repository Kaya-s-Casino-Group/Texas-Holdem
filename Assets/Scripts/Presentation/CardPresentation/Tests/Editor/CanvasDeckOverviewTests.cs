#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using CardArt;
using CardInteraction;
using CardPresentation.Editor;
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    [TestFixture]
    public sealed class CanvasDeckOverviewTests
    {
        private static CardDefinition Definition(Suit suit, Rank rank) => StandardDeck.Cards().Single(card => card.Suit == suit && card.Rank == rank);
        private static CardZoneCardState Card(string id, Suit suit, Rank rank, string art, bool visible = true,
            CardEffects effects = CardEffects.None, bool disabled = false) => new CardZoneCardState(new CardInstance(
                new CardInstanceId(id), Definition(suit,rank).Id, effects: effects, isDisabled: disabled), art, visible, Color.white, default);
        private static DeckOverviewCard Entry(CardZoneCardState card, bool remaining) => new DeckOverviewCard(card.Project(),
            card.FaceUp ? StandardDeck.Cards().Single(definition => definition.Id == card.Card.DefinitionId) : null, remaining);
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene = EditorSceneManager.NewPreviewScene();
            public readonly DeckOverviewView View;
            public Fixture()
            {
                var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DeckOverviewSetup.PrefabPath));
                SceneManager.MoveGameObjectToScene(root,Scene); View = root.GetComponent<DeckOverviewView>();
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(1280,720); rect.localScale = Vector3.one;
                Canvas.ForceUpdateCanvases();
            }
            public void Dispose() => EditorSceneManager.ClosePreviewScene(Scene);
        }

        [Test] public void CanvasPrefabSamplesExistingAtlasWithSharedMaterialAndNoMeshRenderer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeckOverviewSetup.PrefabPath);
            Assert.IsNotNull(prefab); Assert.IsNotNull(prefab.GetComponent<GraphicRaycaster>());
            Assert.AreEqual((int)RenderMode.ScreenSpaceOverlay,new SerializedObject(prefab.GetComponent<Canvas>()).FindProperty("m_RenderMode").intValue);
            Assert.IsNull(prefab.GetComponent<Canvas>().worldCamera); Assert.IsEmpty(prefab.GetComponentsInChildren<MeshRenderer>(true));
            var view = prefab.GetComponent<DeckOverviewView>(); Assert.AreSame(AssetDatabase.LoadAssetAtPath<CardAtlasData>(DeckOverviewSetup.AtlasPath),view.Atlas);
            Assert.AreEqual("CardPresentation/Card Canvas",view.CardMaterial.shader.name);
            Assert.AreEqual(CardUIGraphic.RequiredChannels,prefab.GetComponent<Canvas>().additionalShaderChannels & CardUIGraphic.RequiredChannels);
            Assert.IsEmpty(ShaderUtil.GetShaderMessages(view.CardMaterial.shader).Where(message => message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error));
            Assert.IsEmpty(ShaderUtil.GetShaderMessages(Shader.Find("CardArt/Card Unlit Forward")).Where(message => message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error));
        }

        [Test] public void StatisticsCountRemainingInstancesIncludingDisabledAndIneffectiveCards()
        {
            var cards = new[] {
                Entry(Card("ace",Suit.Spades,Rank.Ace,"spades_a"),false),
                Entry(Card("number",Suit.Spades,Rank.Two,"spades_02",effects:CardEffects.Ineffective),true),
                Entry(Card("face",Suit.Spades,Rank.King,"spades_k",disabled:true),true),
                Entry(Card("copy",Suit.Spades,Rank.King,"spades_k"),true),
                Entry(Card("heart",Suit.Hearts,Rank.Ace,"hearts_a"),true) };
            var data = new DeckOverviewData(cards); var count = data.Counts(Suit.Spades);
            Assert.AreEqual(1,count.Numbers); Assert.AreEqual(2,count.Faces); Assert.AreEqual(0,count.Aces); Assert.AreEqual(3,count.Total);
            Assert.AreEqual(4,data.RemainingCount);
            CollectionAssert.AreEqual(new[] { "ace","face","copy","number" },data.Row(Suit.Spades).Select(card => card.Visual.ViewId));
            Assert.Throws<ArgumentException>(() => new DeckOverviewData(cards.Concat(new[] { cards[0] })));
        }

        [Test] public void UiVerticesKeepAtlasSamplingInsetHdrFoilAndOpacityIndependentOfRuleTint()
        {
            using (var f = new Fixture())
            {
                var state = Card("foil",Suit.Spades,Rank.Ace,"spades_a",effects:CardEffects.Laminated | CardEffects.Enhanced);
                f.View.Present(new DeckOverviewData(new[] { Entry(state,false) })); var graphic = f.View.Cards.Single().Graphic;
                var combinations = AssetDatabase.LoadAssetAtPath<CardArtProfile>("Assets/GameAssets/CardArt/Profiles/StandardCardArt.asset").Combinations;
                combinations.Single(c=>c.Id=="base").Parameters.Single(p=>p.Name=="_FoilColor").Value = new Color(1.2f,3.2f,4);
                var profile = ScriptableObject.CreateInstance<CardArtProfile>(); profile.Configure(graphic.Atlas, combinations);
                graphic.SetArtProfile(profile); graphic.Present(state.Project(),.28f,.65f);
                using (var helper = new VertexHelper())
                {
                    typeof(CardUIGraphic).GetMethod("OnPopulateMesh",BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(graphic,new object[] { helper });
                    Assert.AreEqual(4,helper.currentVertCount); var vertex = UIVertex.simpleVert; helper.PopulateUIVertex(ref vertex,0);
                    Assert.AreEqual(new Vector4(1.2f,3.2f,4,1.2f),vertex.uv1);
                    Assert.AreEqual(3,Mathf.FloorToInt(vertex.uv3.x/2)); Assert.AreEqual(.65f,vertex.uv3.x-6,.000001f);
                    Assert.AreEqual(.28f,graphic.Opacity); Assert.AreEqual(.28f,vertex.color.a/255f,1f/255);
                    Assert.AreEqual(Color.white,state.Tint);
                    Assert.IsTrue(graphic.Atlas.TryGetCard("spades_a",out var region));
                    Assert.AreEqual(region.uv.x,vertex.uv0.x); Assert.AreEqual(region.uv.y,vertex.uv0.y);
                    Assert.AreEqual(region.uv.z,vertex.uv3.y); Assert.AreEqual(region.uv.w,vertex.uv3.z);
                    Assert.AreEqual(Vector3.back,vertex.normal);
                    Assert.AreEqual(Vector2.zero,new Vector2(vertex.uv0.z,vertex.uv0.w));
                }
                var before = graphic.Data;
                Assert.Throws<ArgumentOutOfRangeException>(() => graphic.SetPresentation(float.NaN,0)); Assert.AreSame(before,graphic.Data); Object.DestroyImmediate(profile);
                Assert.Throws<ArgumentException>(() => graphic.Present(new CardVisualData("bad","missing"))); Assert.AreSame(before,graphic.Data);
            }
        }

        [Test] public void HiddenCardsDoNotDiscloseSuitsRanksOrShaderEffects()
        {
            var card = Card("hidden",Suit.Spades,Rank.Ace,"spades_a",visible:false,effects:CardEffects.Laminated,disabled:true);
            Assert.Throws<ArgumentException>(() => new DeckOverviewCard(card.Project(),Definition(Suit.Spades,Rank.Ace),true));
            var data = new DeckOverviewData(new[] { Entry(card,true) }); Assert.AreEqual(1,data.OtherCount); Assert.AreEqual(1,data.OtherRemainingCount);
            Assert.IsEmpty(data.Row(Suit.Spades)); Assert.AreEqual(0,data.Counts(Suit.Spades).Aces);
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DeckOverviewSetup.ItemPath));
            try
            {
                var graphic = root.GetComponent<CardUIItemView>().Graphic;
                graphic.Present(card.Project());
                using (var helper = new VertexHelper())
                {
                    typeof(CardUIGraphic).GetMethod("OnPopulateMesh",BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(graphic,new object[] { helper });
                    var vertex = UIVertex.simpleVert; helper.PopulateUIVertex(ref vertex,0); Assert.AreEqual(0,vertex.uv3.x);
                    Assert.IsNull(graphic.Data.FaceArtId); Assert.IsNull(graphic.Data.CardId);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void HoverAndSwayStayLocalAndRemainingChangeReusesTheSameCanvasCard()
        {
            using (var f = new Fixture())
            {
                var state = Card("card",Suit.Spades,Rank.Ace,"spades_a"); var entry = Entry(state,true);
                f.View.Present(new DeckOverviewData(new[] { entry })); var item = f.View.Cards.Single();
                item.SetHovered(true,false); Assert.IsTrue(item.Card.Interaction.IsHovered); Assert.IsFalse(entry.Visual.Interaction.IsHovered);
                Assert.IsFalse(state.Interaction.IsHovered); Assert.IsFalse(item.Interaction.IsSelected);
                Assert.Greater(item.Graphic.rectTransform.localScale.x,1);
                item.AdvanceMotion(.7f); Assert.LessOrEqual(Mathf.Abs(item.IdleYaw),1.5f);
                f.View.Present(new DeckOverviewData(new[] { Entry(state,false) })); Assert.AreSame(item,f.View.Cards.Single());
                Assert.IsTrue(item.Interaction.IsHovered); Assert.AreEqual(.48f,item.Graphic.Opacity);
                item.SetHovered(false,false); Assert.AreEqual(.28f,item.Graphic.Opacity); Assert.AreEqual(Vector3.one,item.Graphic.rectTransform.localScale);
                Assert.AreSame(f.View.CardMaterial,item.Graphic.material);
                f.View.CloseButton.onClick.Invoke(); Assert.IsFalse(f.View.gameObject.activeSelf);
            }
        }

        [Test] public void MissingArtworkRejectsTheWholeOverviewBeforeRemovingExistingCards()
        {
            using (var f = new Fixture())
            {
                var good = Entry(Card("good",Suit.Spades,Rank.Ace,"spades_a"),true);
                f.View.Present(new DeckOverviewData(new[] { good })); var before = f.View.Data; var item = f.View.Cards.Single();
                var bad = Entry(Card("bad",Suit.Hearts,Rank.King,"missing"),true);
                Assert.Throws<ArgumentException>(() => f.View.Present(new DeckOverviewData(new[] { bad })));
                Assert.AreSame(before,f.View.Data); Assert.AreSame(item,f.View.Cards.Single());
            }
        }

        [Test] public void FixedRowsFillSameBoundsWithShallowArcOverlapAndUprightCards()
        {
            using (var f = new Fixture())
            {
                var data = new DeckOverviewData(Enumerable.Range(0,13).Select(index =>
                    Entry(Card("spade/"+index,Suit.Spades,(Rank)(14-index),index == 0 ? "spades_a" : "spades_02"),true))
                    .Concat(Enumerable.Range(0,9).Select(index => Entry(Card("heart/"+index,Suit.Hearts,(Rank)(14-index),"hearts_a"),true))));
                f.View.Present(data);
                var spades = f.View.Cards.Where(card => card.Card.ViewId.StartsWith("spade/")).OrderBy(card => card.Card.ViewId.Split('/')[1],ComparerForInteger()).ToArray();
                var hearts = f.View.Cards.Where(card => card.Card.ViewId.StartsWith("heart/")).ToArray();
                var width = ((RectTransform)spades[0].transform.parent).rect.width;
                foreach (var group in new[] { spades,hearts })
                {
                    var row = (RectTransform)group[0].transform.parent;
                    var bounds = new Bounds(); bool first = true;
                    foreach (var item in group)
                    {
                        var rect = (RectTransform)item.transform;
                        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                        foreach (var corner in corners)
                        {
                            var point = row.InverseTransformPoint(corner);
                            Assert.LessOrEqual(Mathf.Abs(point.x),row.rect.width/2+.01f);
                            Assert.LessOrEqual(Mathf.Abs(point.y),row.rect.height/2+.01f);
                            if (first) { bounds = new Bounds(point,Vector3.zero); first = false; } else bounds.Encapsulate(point);
                        }
                        Assert.Greater(Vector3.Dot(rect.up,row.up),.99f);
                        Assert.AreEqual(f.View.CardHeight*row.rect.height/132,rect.rect.height,.01f);
                    }
                    Assert.Greater(bounds.size.x,width-8);
                }
                var left = (RectTransform)spades.First().transform; var right = (RectTransform)spades.Last().transform;
                var center = (RectTransform)spades[6].transform;
                Assert.Greater(center.anchoredPosition.y,left.anchoredPosition.y);
                Assert.Greater(left.localEulerAngles.z,0); Assert.Greater(right.localEulerAngles.z,350);
                Assert.Less(((RectTransform)spades[1].transform).anchoredPosition.x-left.anchoredPosition.x,left.rect.width);
                var originalPosition = left.anchoredPosition;
                Assert.Throws<ArgumentException>(() => f.View.SetRowLayout(1000,5));
                Assert.AreEqual(originalPosition,left.anchoredPosition);
                f.View.SetRowLayout(116,4); Assert.AreSame(spades[0],f.View.Cards.Single(card => card.Card.ViewId == "spade/0"));
            }
        }
        private static System.Collections.Generic.IComparer<string> ComparerForInteger() =>
            System.Collections.Generic.Comparer<string>.Create((a,b) => int.Parse(a).CompareTo(int.Parse(b)));

        [Test] public void VerticalOverlapKeepsHoveredCardAboveOtherRowsAndIndividualIdleTurnsContinue()
        {
            using (var f = new Fixture())
            {
                var a = Entry(Card("a",Suit.Spades,Rank.Ace,"spades_a"),true);
                var b = Entry(Card("b",Suit.Hearts,Rank.Ace,"hearts_a"),true);
                f.View.Present(new DeckOverviewData(new[] { a,b })); Canvas.ForceUpdateCanvases();
                var first = f.View.Cards.Single(card => card.Card.ViewId == "a"); var second = f.View.Cards.Single(card => card.Card.ViewId == "b");
                var row = (RectTransform)first.transform.parent; var next = (RectTransform)second.transform.parent;
                Assert.Greater(row.rect.height,Mathf.Abs(row.anchoredPosition.y-next.anchoredPosition.y));
                Assert.Greater(((RectTransform)first.transform).rect.height,
                    Mathf.Abs(row.localPosition.y-next.localPosition.y));
                first.SetHovered(true,false); Canvas.ForceUpdateCanvases();
                Assert.AreEqual(3,row.GetSiblingIndex());
                f.View.AdvanceMotion(.7f); float yaw = first.IdleYaw, roll = first.IdleRoll;
                Assert.AreNotEqual(first.IdleYaw,second.IdleYaw); Assert.AreNotEqual(first.IdleRoll,second.IdleRoll);
                f.View.AdvanceMotion(.7f); Assert.AreNotEqual(yaw,first.IdleYaw); Assert.AreNotEqual(roll,first.IdleRoll);
                Assert.Greater(first.Graphic.rectTransform.localScale.x,1);
                first.SetHovered(false,false); Canvas.ForceUpdateCanvases(); Assert.AreEqual(0,row.GetSiblingIndex());
                f.View.SetVerticalOverlap(0); Canvas.ForceUpdateCanvases();
                Assert.AreEqual(f.View.SuitRowsRoot.rect.height/4,row.rect.height,.001f);
                Assert.Throws<ArgumentOutOfRangeException>(() => f.View.SetVerticalOverlap(.5f));
                Assert.AreEqual(0,f.View.VerticalOverlap);
            }
        }

        [Test] public void SidebarCategoriesRanksAndGeneratedIconsFollowRemainingCards()
        {
            using (var f = new Fixture())
            {
                var ace = Entry(Card("ace",Suit.Spades,Rank.Ace,"spades_a"),true);
                var king = Entry(Card("king",Suit.Hearts,Rank.King,"hearts_k"),true);
                var number = Entry(Card("number",Suit.Clubs,Rank.Two,"clubs_02"),true);
                var drawn = Entry(Card("drawn",Suit.Spades,Rank.King,"spades_k"),false);
                f.View.Present(new DeckOverviewData(new[] { ace,king,number,drawn }));
                var panel = f.View.transform.Find("Frame/Panel/StatisticsPanel");
                foreach (string name in new[] { "A","人头牌","数字牌" })
                {
                    Assert.AreEqual("1",panel.Find("Total "+name).GetComponent<TMPro.TMP_Text>().text);
                    var image = panel.Find("Icon "+name).GetComponent<Image>(); Assert.IsNotNull(image.sprite);
                    Assert.AreEqual(FilterMode.Point,image.sprite.texture.filterMode); Assert.AreEqual(1,image.sprite.texture.mipmapCount);
                    Assert.IsFalse(image.raycastTarget);
                }
                Assert.AreEqual("1",f.View.transform.Find("Frame/Panel/Rank Count K").GetComponent<TMPro.TMP_Text>().text);
                f.View.Present(new DeckOverviewData(new[] { Entry(Card("ace",Suit.Spades,Rank.Ace,"spades_a"),false),king,number,drawn }));
                Assert.AreEqual("0",panel.Find("Total A").GetComponent<TMPro.TMP_Text>().text);
                Assert.AreEqual("0",f.View.transform.Find("Frame/Panel/Rank Count A").GetComponent<TMPro.TMP_Text>().text);
                Assert.IsTrue(f.View.GetComponent<Canvas>().pixelPerfect);
                Assert.IsTrue(f.View.CloseButton.targetGraphic.raycastTarget);
                Assert.AreEqual(FilterMode.Point,f.View.Atlas.texture.filterMode);
            }
        }

        [Test] public void SuitRowsParentResizesAllRowsAndReflowsExistingCardsBeforeCanvasRenders()
        {
            using (var f = new Fixture())
            {
                var data = new DeckOverviewData(DeckOverviewView.SuitOrder.SelectMany(suit =>
                    Enumerable.Range(0,13).Select(index => Entry(Card(suit+"/"+index,suit,(Rank)(14-index),"spades_a"),true))));
                f.View.Present(data); var area = f.View.SuitRowsRoot;
                Assert.IsNotNull(area); Assert.AreEqual("SuitRows",area.name);
                area.sizeDelta = new Vector2(680,528); Canvas.ForceUpdateCanvases();
                var originals = f.View.Cards.ToDictionary(card => card.Card.ViewId);
                var hovered = f.View.Cards.First(); hovered.SetHovered(true,false);
                var statistics = f.View.transform.Find("Frame/Panel/StatisticsPanel").GetComponent<RectTransform>().rect.size;
                foreach (var size in new[] { new Vector2(510,396),new Vector2(850,660) })
                {
                    area.sizeDelta = size; Canvas.ForceUpdateCanvases();
                    foreach (var item in f.View.Cards)
                    {
                        Assert.AreSame(originals[item.Card.ViewId],item);
                        var row = (RectTransform)item.transform.parent;
                        Assert.AreSame(area,row.parent); Assert.AreEqual(size.x,row.rect.width,.01f);
                        Assert.AreEqual(size.y/(4-3*f.View.VerticalOverlap),row.rect.height,.01f);
                        Assert.AreEqual(120*size.y/(4-3*f.View.VerticalOverlap)/132,((RectTransform)item.transform).rect.height,.01f);
                        Assert.Greater(Vector3.Dot(item.transform.up,row.up),.99f);
                    }
                    Assert.IsTrue(hovered.Interaction.IsHovered); Assert.AreSame(data,f.View.Data);
                    Assert.AreEqual(statistics,f.View.transform.Find("Frame/Panel/StatisticsPanel").GetComponent<RectTransform>().rect.size);
                }
                f.View.SetRowLayout(116,4);
                Assert.AreEqual(116*660/(4-3*f.View.VerticalOverlap)/132,((RectTransform)hovered.transform).rect.height,.01f);
                Assert.Throws<ArgumentException>(() => f.View.SetRowLayout(1000,5));
                Assert.AreEqual(116,f.View.CardHeight); Assert.IsTrue(hovered.Interaction.IsHovered);
            }
        }

        [Test] public void RefreshRemovesOrphanedEditorVisualsWithoutReplacingTrackedCards()
        {
            using (var f = new Fixture())
            {
                var entry = Entry(Card("good",Suit.Spades,Rank.Ace,"spades_a"),true);
                f.View.Present(new DeckOverviewData(new[] { entry })); var item = f.View.Cards.Single();
                var orphan = Object.Instantiate(item.gameObject,item.transform.parent,false);
                f.View.Present(new DeckOverviewData(new[] { entry }));
                Assert.IsTrue(orphan == null); Assert.AreSame(item,f.View.Cards.Single());
                Assert.AreEqual(1,item.transform.parent.GetComponentsInChildren<CardUIItemView>().Length);
            }
        }

        private sealed class ScoreAuthority : ICardMatchCommands, ICardScoreSource
        {
            public bool Allow = true;
            public CardInstanceId Queried;
            public bool CanInteract => false;
            public bool CanPlay(CardSelectionRequest request) => false;
            public bool CanDiscard(CardSelectionRequest request) => false;
            public int Play(CardSelectionRequest request, bool animate) => 0;
            public int Discard(CardSelectionRequest request, bool animate) => 0;
            public bool TryGetCardBasePoints(CardInstanceId card, out long points) { Queried = card; points = 123; return Allow; }
        }
        [Test] public void OverviewQueriesGameplayByCardIdentityAndDoesNotReplaceDeniedValuesWithDefaults()
        {
            using (var f = new Fixture())
            {
                var root = new GameObject("Match"); SceneManager.MoveGameObjectToScene(root, f.Scene);
                var match = root.AddComponent<CardMatchHost>(); match.ConfigureRuntimeManaged();
                var card = Card("actual-card", Suit.Spades, Rank.Ace, "spades_a");
                var seed = new CardTableState(new MatchId("overview"), new[] { card.Card }, new[] {
                    new CardZone(new ZoneId("deck"), CardZoneKinds.Deck, null, new[] { card.Card.Id }) });
                var source = new ScoreAuthority(); match.StartExternalMatch(source, seed, new[] { card });
                var presenter = f.View.gameObject.AddComponent<DeckOverviewPresenter>();
                presenter.Configure(match, f.View, new ZoneId("deck"), new[] { card.Card.Id }); presenter.Open(false);
                // View identity may differ from the authoritative card identity.
                f.View.Present(new DeckOverviewData(new[] { new DeckOverviewCard(
                    new CardVisualData("different-view", "spades_a", card.Card.Id), Definition(Suit.Spades, Rank.Ace), true) }));
                f.View.Cards.Single().SetHovered(true, false);
                Assert.IsTrue(presenter.TryGetHoveredCard(out var target));
                Assert.AreEqual(123, target.ChipPoints); Assert.AreEqual(card.Card.Id, source.Queried);
                source.Allow = false; Assert.IsFalse(presenter.TryGetHoveredCard(out _));
            }
        }
        [Test] public void PresenterReadsSharedMatchAndExplicitRosterWithoutChangingZoneOrderOrHandHover()
        {
            using (var f = new Fixture())
            {
                var root = new GameObject("Match"); SceneManager.MoveGameObjectToScene(root,f.Scene); var match = root.AddComponent<CardMatchHost>();
                var a = Card("a",Suit.Spades,Rank.Ace,"spades_a"); var b = Card("b",Suit.Hearts,Rank.King,"hearts_k");
                var other = Card("other",Suit.Clubs,Rank.Two,"clubs_02");
                var seed = new CardTableState(new MatchId("overview"),new[] { a.Card,b.Card,other.Card },new[] {
                    new CardZone(new ZoneId("deck"),CardZoneKinds.Deck,null,new[] { a.Card.Id,b.Card.Id }),
                    new CardZone(new ZoneId("hand"),CardZoneKinds.Hand,null,Array.Empty<CardInstanceId>()),
                    new CardZone(new ZoneId("other"),CardZoneKinds.Hand,null,new[] { other.Card.Id }) });
                match.StartMatch(seed,new[] { a,b,other });
                var presenter = f.View.gameObject.AddComponent<DeckOverviewPresenter>(); presenter.Configure(match,f.View,new ZoneId("deck"),new[] { a.Card.Id,b.Card.Id });
                Assert.AreEqual(2,f.View.Cards.Count); CollectionAssert.AreEqual(new[] { a.Card.Id,b.Card.Id },match.GetZone(new ZoneId("deck")).Cards);
                var change = match.PrepareDraw(new ZoneId("deck"),new ZoneId("hand"),1,5); match.Commit(change);
                match.SetInteraction(b.Card.Id,new CardInteractionState(isHovered:true,isSelected:true));
                presenter.Refresh(); Assert.AreEqual(1,f.View.Data.RemainingCount); Assert.AreEqual(2,f.View.Cards.Count);
                var item = f.View.Cards.Single(card => card.Card.ViewId == "b"); item.SetHovered(true,false);
                Assert.IsTrue(match.GetCards(new ZoneId("hand")).Single().Interaction.IsSelected);
                Assert.IsFalse(item.Interaction.IsSelected); Assert.AreEqual(1,match.State.Revision);
                match.RestartMatch(); presenter.Refresh(); Assert.AreEqual(2,f.View.Data.RemainingCount);
            }
        }
    }
}
#endif
