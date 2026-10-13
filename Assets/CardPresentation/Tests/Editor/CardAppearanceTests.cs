using System;
using System.Linq;
using System.Reflection;
using CardArt;
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace CardPresentation.Tests
{
    public sealed class CardAppearanceTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene=EditorSceneManager.NewPreviewScene();
            public readonly CardArtProfile Profile;
            public readonly GameObject Prefab;
            public readonly HandZoneView Zone;
            public Fixture()
            {
                Profile=Object.Instantiate(AssetDatabase.LoadAssetAtPath<CardArtProfile>("Assets/CardArt/Profiles/StandardCardArt.asset"));
                Prefab=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab"));
                SceneManager.MoveGameObjectToScene(Prefab,Scene); Prefab.SetActive(false); Prefab.GetComponentInChildren<CardView>(true).SetArtProfile(Profile);
                var root=new GameObject("Art boundary test"); SceneManager.MoveGameObjectToScene(root,Scene); Zone=root.AddComponent<HandZoneView>();
                Zone.Configure(Prefab,CardZoneLayoutSettings.Default);
                var transition=CardZoneTransitionSettings.Default; transition.Enabled=false; Zone.SetTransitionSettings(transition);
            }
            public void Dispose() { EditorSceneManager.ClosePreviewScene(Scene); Object.DestroyImmediate(Profile); }
        }
        private static CardZoneViewData Data(params CardVisualData[] cards) => new CardZoneViewData(new ZoneId("hand"),cards);
        private static MaterialPropertyBlock Block(CardView view) { var block=new MaterialPropertyBlock(); view.GetComponent<MeshRenderer>().GetPropertyBlock(block); return block; }
        private static void Near(float expected,float actual) => Assert.That(actual,Is.EqualTo(expected).Within(.00001f));
        [Test] public void AllStateCombinationsMapToPresetsWithoutTouchingSharedMaterialOrNeighbors()
        {
            using(var f=new Fixture())
            {
                var material=f.Prefab.GetComponentInChildren<CardView>(true).GetComponent<MeshRenderer>().sharedMaterial;
                var original=EditorJsonUtility.ToJson(material); CardInstanceView kept=null;
                for(int bits=0;bits<8;bits++) for(int disabled=0;disabled<2;disabled++)
                {
                    f.Zone.Present(Data(new CardVisualData("card","hearts_q",effects:(CardEffects)bits,isDisabled:disabled==1),new CardVisualData("neighbor","hearts_q")));
                    var current=f.Zone.Views[0]; if(kept==null) kept=current; else Assert.AreSame(kept,current);
                    var block=Block(current.Surface);
                    Near((bits&1)!=0?1:0,block.GetFloat(CardShaderProperties.SealEnabled));
                    Near((bits&2)!=0?1:0,block.GetFloat(CardShaderProperties.FoilEnabled));
                    Near(disabled,block.GetFloat(CardShaderProperties.RedCrossEnabled));
                    Near((bits&4)!=0?.45f:1,block.GetColor(CardShaderProperties.ColorTint).r);
                    Near((bits&2)!=0?1.2f*((bits&4)!=0?.2f:1):0,block.GetFloat(CardShaderProperties.FoilStrength));
                    Near(0,Block(f.Zone.Views[1].Surface).GetFloat(CardShaderProperties.SealEnabled));
                    Assert.AreSame(material,current.Surface.GetComponent<MeshRenderer>().sharedMaterial);
                }
                Assert.AreEqual(original,EditorJsonUtility.ToJson(material));
            }
        }
        [Test] public void GameplayCanRemapEnhancedToAnyCombinationWithoutChangingArtOrShader()
        {
            using(var f=new Fixture())
            {
                var bindings=ScriptableObject.CreateInstance<CardStateArtBindings>();
                try
                {
                    bindings.Configure(new[]{new CardStateArtRule { Required=CardEffects.Enhanced,Combinations=new System.Collections.Generic.List<string>{"foil"} }});
                    f.Zone.Present(Data(new CardVisualData("x","spades_a",effects:CardEffects.Enhanced,artBindings:bindings)));
                    var block=Block(f.Zone.Views[0].Surface); Near(1,block.GetFloat(CardShaderProperties.FoilEnabled)); Near(0,block.GetFloat(CardShaderProperties.SealEnabled));
                    // Mapping excludes rule flags on hidden faces, including manually constructed projections.
                    f.Zone.Present(Data(new CardVisualData("x",effects:(CardEffects)7,isDisabled:true,artBindings:bindings)));
                    block=Block(f.Zone.Views[0].Surface); Near(0,block.GetFloat(CardShaderProperties.FoilEnabled)); Near(1,block.GetColor(CardShaderProperties.ColorTint).r);
                    Assert.AreEqual("card_back",f.Zone.Views[0].Surface.CardId);
                }
                finally { Object.DestroyImmediate(bindings); }
            }
        }
        [Test] public void MatchOwnedMappingSurvivesProjectionInteractionAndCardTransfer()
        {
            using(var f=new Fixture())
            {
                var bindings=ScriptableObject.CreateInstance<CardStateArtBindings>();
                try
                {
                    bindings.Configure(new[]{new CardStateArtRule { Required=CardEffects.Enhanced,Combinations=new System.Collections.Generic.List<string>{"foil"} }});
                    var root=new GameObject("Match"); SceneManager.MoveGameObjectToScene(root,f.Scene); var match=root.AddComponent<CardInteraction.CardMatchHost>(); match.ConfigureArtBindings(bindings);
                    var card=new CardInstance(new CardInstanceId("x"),new CardDefinitionId("definition"),effects:CardEffects.Enhanced);
                    var hand=new CardZone(new ZoneId("hand"),CardZoneKinds.Hand,null,new[]{card.Id});
                    var played=new CardZone(new ZoneId("played"),CardZoneKinds.Played,null,Array.Empty<CardInstanceId>());
                    var table=new CardTableState(new MatchId("test"),new[]{card},new[]{hand,played});
                    match.StartMatch(table,new[]{new CardInteraction.CardZoneCardState(card,"spades_a",true,Color.white,default)});
                    var visual=match.GetCards(hand.Id)[0].Project(); Assert.AreSame(bindings,visual.ArtBindings); CollectionAssert.AreEqual(new[]{"foil"},CardAppearanceResolver.Combinations(visual));
                    match.SetInteraction(card.Id,new CardInteractionState(isSelected:true));
                    var update=match.PreparePlay(hand.Id,played.Id,new[]{card.Id},5); match.Commit(update);
                    visual=match.GetCards(played.Id)[0].Project(); Assert.AreSame(bindings,visual.ArtBindings); CollectionAssert.AreEqual(new[]{"foil"},CardAppearanceResolver.Combinations(visual));
                }
                finally { Object.DestroyImmediate(bindings); }
            }
        }
        [Test] public void RunAndBattleShareMappingAndProfileRefreshesWorldAndCanvas()
        {
            using(var f=new Fixture())
            {
                var entry=new DeckEntry("run",new CardDefinitionId("standard"),(CardEffects)7,true);
                var card=new CardInstance(new CardInstanceId("battle"),entry.DefinitionId,entry.Id,entry.Effects,entry.IsDisabled);
                var run=CardVisualData.FromDeckEntry(entry,"run",true,_=>"hearts_q"); var battle=CardVisualData.FromCard(card,"battle",true,_=>"hearts_q");
                Assert.IsNull(run.CardId); CollectionAssert.AreEqual(CardAppearanceResolver.Combinations(run),CardAppearanceResolver.Combinations(battle));
                var hidden=CardVisualData.FromDeckEntry(entry,"hidden",false,_=>throw new Exception()); Assert.IsNull(hidden.FaceArtId); Assert.AreEqual(CardEffects.None,hidden.Effects);
                f.Zone.Present(Data(battle)); var world=f.Zone.Views[0];
                var uiRoot=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardPresentation/Prefabs/CardUIItem.prefab")); SceneManager.MoveGameObjectToScene(uiRoot,f.Scene);
                var ui=uiRoot.GetComponentInChildren<CardUIGraphic>(true); ui.SetArtProfile(f.Profile); ui.Present(run); var material=ui.material;
                var combinations=f.Profile.Combinations; combinations.Single(c=>c.Id=="dim").Parameters.Single(p=>p.Name=="_ColorTint").Value=new Color(.25f,.25f,.25f,1);
                f.Profile.SetCombinations(combinations); Near(.25f,Block(world.Surface).GetColor(CardShaderProperties.ColorTint).r);
                using(var vertices=new UnityEngine.UI.VertexHelper())
                {
                    typeof(CardUIGraphic).GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Invoke(ui,new object[]{vertices});
                    var vertex=UIVertex.simpleVert; vertices.PopulateUIVertex(ref vertex,0); Assert.That(((Color)vertex.color).r,Is.EqualTo(.25f).Within(.005f));
                }
                Assert.AreSame(run,ui.Data); Assert.AreSame(battle,world.Data); Assert.AreSame(material,ui.material);
            }
        }
        [Test] public void OrderedOverlaysAreDeterministicAndRemovalResetsCompleteInstanceBlock()
        {
            using(var f=new Fixture())
            {
                var a=f.Profile.Resolve(new[]{"foil","dim"},Color.white); var b=f.Profile.Resolve(new[]{"dim","foil"},Color.white);
                Near(.24f,a.GetFloat("_FoilStrength")); Near(1.2f,b.GetFloat("_FoilStrength"));
                f.Zone.Present(Data(new CardVisualData("x","hearts_q",effects:(CardEffects)7,isDisabled:true))); var view=f.Zone.Views[0];
                f.Zone.Present(Data(new CardVisualData("x","hearts_q"))); Assert.AreSame(view,f.Zone.Views[0]);
                Near(0,Block(view.Surface).GetFloat(CardShaderProperties.FoilEnabled)); Near(0,Block(view.Surface).GetFloat(CardShaderProperties.RedCrossEnabled)); Near(1,Block(view.Surface).GetColor(CardShaderProperties.ColorTint).r);
            }
        }
        [Test] public void InvalidCombinationsRejectBeforeExistingZoneChanges()
        {
            using(var f=new Fixture())
            {
                f.Zone.Present(Data(new CardVisualData("kept","spades_a"))); var data=f.Zone.Data; var view=f.Zone.Views[0];
                var combinations=f.Profile.Combinations; combinations.Single(c=>c.Id=="foil").Parameters.Add(CardShaderParameter.Float("_UndeclaredParameter",1));
                f.Profile.SetCombinations(combinations); // Not selected by the existing ordinary card.
                Assert.Throws<ArgumentException>(()=>f.Zone.Present(Data(new CardVisualData("kept","spades_a",effects:CardEffects.Laminated))));
                Assert.AreSame(data,f.Zone.Data); Assert.AreSame(view,f.Zone.Views[0]);
                combinations=f.Profile.Combinations; combinations[0].Parameters[0].Value=new Vector4(float.NaN,1,1,1);
                Assert.Throws<ArgumentException>(()=>f.Profile.SetCombinations(combinations));
                Assert.Throws<ArgumentException>(()=>f.Profile.Resolve(new[]{"unknown"},Color.white));
            }
        }
        [Test] public void CanvasRejectsUnmappedParametersBeforeReplacingExistingCard()
        {
            using(var f=new Fixture())
            {
                var profile=Object.Instantiate(f.Profile); var bindings=ScriptableObject.CreateInstance<CardStateArtBindings>();
                try
                {
                    var combinations=profile.Combinations.ToList(); combinations.Add(new CardShaderCombination("custom_ui",CardShaderParameter.Float("_ArtistUnmapped",1))); profile.SetCombinations(combinations);
                    bindings.Configure(new[]{new CardStateArtRule { Required=CardEffects.Enhanced,Combinations=new System.Collections.Generic.List<string>{"custom_ui"} }});
                    var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardPresentation/Prefabs/CardUIItem.prefab")); SceneManager.MoveGameObjectToScene(root,f.Scene);
                    var ui=root.GetComponentInChildren<CardUIGraphic>(true); ui.SetArtProfile(profile);
                    var normal=new CardVisualData("normal","spades_a",artBindings:bindings); ui.Present(normal);
                    Assert.Throws<ArgumentException>(()=>ui.Present(new CardVisualData("custom","spades_a",effects:CardEffects.Enhanced,artBindings:bindings)));
                    Assert.AreSame(normal,ui.Data);
                }
                finally { Object.DestroyImmediate(profile); Object.DestroyImmediate(bindings); }
            }
        }
        [Test] public void TransferKeepsTheCardsOwnPresetProfile()
        {
            using(var f=new Fixture())
            {
                var alternate=Object.Instantiate(f.Profile);
                try
                {
                    var combinations=alternate.Combinations; combinations.Single(c=>c.Id=="foil").Parameters.Single(p=>p.Name=="_FoilStrength").Value=new Vector4(4,0,0,0); alternate.SetCombinations(combinations);
                    var root=new GameObject("destination"); SceneManager.MoveGameObjectToScene(root,f.Scene); var destination=root.AddComponent<PlayedZoneView>(); destination.Configure(f.Prefab,CardZoneLayoutSettings.Default);
                    var visual=new CardVisualData("moving","hearts_q",effects:CardEffects.Laminated); f.Zone.Present(Data(visual)); var kept=f.Zone.Views[0]; kept.Surface.SetArtProfile(alternate);
                    destination.Present(new CardZoneViewData(new ZoneId("played"),new CardVisualData[0]));
                    var route=root.AddComponent<CardZoneTransferPresenter>(); route.Configure(f.Zone,destination,CardZoneTransferSettings.Default);
                    route.PresentTransfer(Data(),new CardZoneViewData(new ZoneId("played"),new[]{visual}),new[]{visual.ViewId},false);
                    Assert.AreSame(kept,destination.Views[0]); Assert.AreSame(alternate,kept.Surface.ArtProfile); Near(4,kept.Surface.FoilStrength);
                }
                finally { Object.DestroyImmediate(alternate); }
            }
        }
    }
}
