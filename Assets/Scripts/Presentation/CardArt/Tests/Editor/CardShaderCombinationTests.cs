using System;
using System.IO;
using System.Linq;
using CardArt.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace CardArt.Tests
{
    public sealed class CardShaderCombinationTests
    {
        [Test] public void GenericCombinationsHaveNoGameplayStateNamesAndPreserveOrderedTypedValues()
        {
            var profile=ScriptableObject.CreateInstance<CardArtProfile>();
            try
            {
                var atlas=AssetDatabase.LoadAssetAtPath<CardAtlasData>(CardRenderingValidation.AtlasPath);
                profile.Configure(atlas,new[]{
                    new CardShaderCombination("base",CardShaderParameter.Float("_ArtistAmount",2),CardShaderParameter.Color("_ArtistColor",new Color(4,2,1,1))),
                    new CardShaderCombination("variant",CardShaderParameter.Float("_ArtistAmount",3,CardShaderParameterOperation.Multiply),new CardShaderParameter("_ArtistDirection",CardShaderValueKind.Vector,new Vector4(1,2,3,4))) });
                var output=profile.Resolve(new[]{"variant"},Color.white); Assert.AreEqual(6,output.GetFloat("_ArtistAmount"));
                Assert.AreEqual(new Color(4,2,1,1),output.GetColor("_ArtistColor",Color.white)); Assert.AreEqual(new Vector4(1,2,3,4),output.GetVector("_ArtistDirection",Vector4.zero));
                var copy=profile.Combinations; copy[0].Parameters[0].Value=Vector4.zero;
                Assert.AreEqual(6,profile.Resolve(new[]{"variant"},Color.white).GetFloat("_ArtistAmount"));
                Assert.Throws<ArgumentException>(()=>CardShaderParameter.Float("_FrontUVRect",1));
                Assert.Throws<ArgumentException>(()=>CardShaderParameter.Float("_ArtistAmount",float.NaN));
                Assert.Throws<ArgumentException>(()=>profile.Resolve(new[]{"missing"},Color.white));
            }
            finally { Object.DestroyImmediate(profile); }
        }
        [Test] public void CustomParameterNamesReachRealInstanceShaderAndResetWithoutChangingMinimumContract()
        {
            string folder="Assets/Scripts/Presentation/CardArt/Tests/GenericShaderTemp_"+Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            Material material=null; CardArtProfile profile=null; GameObject root=null;
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                // Rename optional foil inputs throughout the shader family. Atlas contract remains identical.
                foreach(var path in Directory.GetFiles("Assets/GameAssets/CardArt/Shaders","*.hlsl"))
                    File.WriteAllText(folder+"/"+Path.GetFileName(path),File.ReadAllText(path).Replace("_FoilEnabled","_ArtistGlintOn").Replace("_FoilStrength","_ArtistGlintAmount"));
                string source=File.ReadAllText("Assets/GameAssets/CardArt/Shaders/CardUnlit.shader").Replace("CardArt/Card Unlit Forward","Hidden/CardArt/Tests/Generic"+Guid.NewGuid().ToString("N"))
                    .Replace("_FoilEnabled","_ArtistGlintOn").Replace("_FoilStrength","_ArtistGlintAmount").Replace("[PerRendererData]","");
                File.WriteAllText(folder+"/Generic.shader",source); AssetDatabase.ImportAsset(folder+"/Generic.shader",ImportAssetOptions.ForceSynchronousImport);
                material=new Material(AssetDatabase.LoadAssetAtPath<Shader>(folder+"/Generic.shader"));
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"); var original=prefab.GetComponentInChildren<CardView>(true);
                CardShaderProperties.ConfigureSkin(material,original.Atlas);
                var errors=new System.Collections.Generic.List<string>(); CardPrefabExporter.ValidateShader(material.shader,errors); Assert.IsEmpty(errors,string.Join("\n",errors));
                profile=ScriptableObject.CreateInstance<CardArtProfile>();
                profile.Configure(original.Atlas,new[]{new CardShaderCombination("base",CardShaderParameter.Float("_ArtistGlintOn",0),CardShaderParameter.Float("_ArtistGlintAmount",0)),
                    new CardShaderCombination("artist_custom",CardShaderParameter.Float("_ArtistGlintOn",1),CardShaderParameter.Float("_ArtistGlintAmount",2))});
                CardPrefabExporter.ValidateProfileParameters(profile,material);
                root=new GameObject("Generic shader instance"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene); var view=root.AddComponent<CardView>();
                view.Bind(original.GetComponent<MeshFilter>().sharedMesh,material,original.Atlas,"spades_a"); view.SetArtProfile(profile);
                var before=EditorJsonUtility.ToJson(material); view.PresentArtwork(new[]{"artist_custom"},Color.white);
                var block=new MaterialPropertyBlock(); view.GetComponent<MeshRenderer>().GetPropertyBlock(block); Assert.AreEqual(2,block.GetFloat("_ArtistGlintAmount"));
                view.PresentArtwork(Array.Empty<string>(),Color.white); view.GetComponent<MeshRenderer>().GetPropertyBlock(block); Assert.AreEqual(0,block.GetFloat("_ArtistGlintAmount"));
                Assert.AreSame(material,view.GetComponent<MeshRenderer>().sharedMaterial); Assert.AreEqual(before,EditorJsonUtility.ToJson(material));
                Object.DestroyImmediate(root); root=null;
                var combinations=profile.Combinations; combinations[0].Parameters.Add(CardShaderParameter.Float("_FoilSpeed",1)); profile.SetCombinations(combinations);
                Assert.Throws<ArgumentException>(()=>CardPrefabExporter.ValidateProfileParameters(profile,material)); // Shared uniforms cannot become per-card inputs.
            }
            finally
            { if(root!=null) Object.DestroyImmediate(root); if(profile!=null) Object.DestroyImmediate(profile); if(material!=null) Object.DestroyImmediate(material); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); AssetDatabase.DeleteAsset(folder); }
        }
    }
}
