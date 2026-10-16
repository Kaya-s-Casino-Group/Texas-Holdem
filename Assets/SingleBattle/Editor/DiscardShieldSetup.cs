using System;
using System.IO;
using System.Linq;
using CardArt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Editor
{
    public static class DiscardShieldSetup
    {
        public const string Folder = "Assets/SingleBattle/ShieldAssets";
        [MenuItem("Tools/Poker/Single Battle/Configure Discard Shields")]
        public static void ConfigureScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请先停止 Play。");
            var scene = SceneManager.GetActiveScene();
            var hosts = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleHost>(true)).ToArray();
            if (hosts.Length == 0) throw new InvalidOperationException("场景中没有单场战斗入口。");
            foreach (var host in hosts)
            {
                var surface = host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().CardPrefab.GetComponentInChildren<CardView>(true);
                PrepareAssets(surface.Atlas, out var mesh, out var material);
                foreach (var actor in host.Players) Configure(actor, mesh, material, surface.Atlas);
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("弃牌盾牌已配置：Actor 总表现入口、独立起点/落地区域、逐张 DOTween 飞入、格挡消耗溶解。");
        }
        [MenuItem("Tools/Poker/Single Battle/Configure Shield Landing Flash")]
        public static void ConfigureLandingFlash()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请先停止 Play。");
            const string path = "Assets/Art/VFX/HitFlashes/Prefabs/Sword Slash 14 Hit Flashes.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var effect = prefab != null ? prefab.GetComponent<ParticleSystem>() : null;
            if (effect == null) throw new InvalidOperationException("落地特效 Prefab 缺少根粒子系统：" + path);
            var scene = SceneManager.GetActiveScene();
            var views = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DiscardShieldPresenter>(true)).ToArray();
            if (views.Length == 0) throw new InvalidOperationException("当前场景没有弃牌盾牌容器。");
            foreach (var view in views)
            {
                Undo.RecordObject(view, "配置盾牌落地闪光");
                view.LandingEffect = effect; view.LandingEffectScale = .2f;
                EditorUtility.SetDirty(view); PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("盾牌落地已绑定 Sword Slash 14 Hit Flashes，缩放随卡牌高度变化，倍率 0.2。");
        }

        public static void PrepareAssets(CardAtlasData atlas, out Mesh mesh, out Material material)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/SingleBattle", "ShieldAssets");
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/ShieldCard.asset");
            if (mesh == null)
            {
                mesh = new Mesh { name = "Shield Card XY" };
                mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                mesh.triangles = new[] { 0,1,2,0,2,3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, Folder + "/ShieldCard.asset");
            }
            string noisePath = Folder + "/ShieldPerlin.png";
            if (!File.Exists(noisePath))
            {
                const int size = 128; var noise = new Texture2D(size,size,TextureFormat.RGBA32,false,true);
                var pixels = new Color[size*size];
                for (int y=0;y<size;y++) for (int x=0;x<size;x++)
                {
                    float u=x/(float)(size-1),v=y/(float)(size-1), px=u*5,py=v*5;
                    float a=Mathf.Lerp(Mathf.PerlinNoise(17+px,29+py),Mathf.PerlinNoise(12+px,29+py),u);
                    float b=Mathf.Lerp(Mathf.PerlinNoise(17+px,24+py),Mathf.PerlinNoise(12+px,24+py),u);
                    float n=Mathf.Lerp(a,b,v);pixels[y*size+x]=new Color(n,n,n,1);
                }
                noise.SetPixels(pixels);noise.Apply();File.WriteAllBytes(noisePath,noise.EncodeToPNG());UnityEngine.Object.DestroyImmediate(noise);
                AssetDatabase.ImportAsset(noisePath);
                var importer=(TextureImporter)AssetImporter.GetAtPath(noisePath);importer.sRGBTexture=false;importer.wrapMode=TextureWrapMode.Repeat;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            string atlasId=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(atlas));
            string materialPath=Folder+"/Shield-"+atlasId+".mat";
            material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null)
            {
                var shader=Shader.Find("CardArt/Shield Deferred");if(shader==null)throw new InvalidOperationException("Shield Shader 未导入。");
                material=new Material(shader){name="Discard Shield",enableInstancing=true};
                material.SetTexture("_BaseMap",atlas.texture);material.SetTexture("_NoiseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(noisePath));
                if(atlas.TryGetCard("card_back",out var back))material.SetVector("_BackUVRect",back.uv);
                AssetDatabase.CreateAsset(material,materialPath);
            }
        }
        private static void ConfigureRing(PlayerBattleActor actor, DiscardShieldPresenter view)
        {
            Undo.RecordObject(view, "配置 Actor 环形盾牌区域");
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var renderer in actor.Visual.GetComponentsInChildren<Renderer>(true))
            {
                var worldBounds = renderer.bounds;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            var point = view.transform.InverseTransformPoint(worldBounds.center + Vector3.Scale(worldBounds.extents, new Vector3(x, y, z)));
                            if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                            else bounds.Encapsulate(point);
                        }
            }
            // Visuals may be offset from the logical Actor origin; surround the visible model.
            float radius = Mathf.Max(view.CardSize.y, new Vector2(bounds.extents.x, bounds.extents.z).magnitude);
            view.RingCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            view.RingInnerRadius = radius + view.CardSize.y * .15f;
            view.RingOuterRadius = view.RingInnerRadius + view.CardSize.y * .35f;
            view.LandingHeightVariation = 0;
            view.RingLayoutVersion = 2;
            EditorUtility.SetDirty(view); PrefabUtility.RecordPrefabInstancePropertyModifications(view);
        }

        private static void Configure(PlayerBattleActor actor, Mesh mesh, Material material, CardAtlasData atlas)
        {
            if(actor.Visual==null)throw new InvalidOperationException("玩家需要绑定 Visual 节点。");
            var existing=actor.Presentation as BattleActorPresentationCoordinator;
            if(existing!=null && existing.Shields!=null)
            {
                if (existing.Shields.RingLayoutVersion < 2) ConfigureRing(actor, existing.Shields);
                existing.Shields.ValidateConfiguration(); return;
            }
            var body=actor.Presentation as MonoBehaviour;
            if(body==null)
            {
                var fallback=Undo.AddComponent<BattleActorPresenter>(actor.Visual.gameObject);fallback.Configure(actor.Visual);body=fallback;
            }
            var root=new GameObject("Discard Shields");Undo.RegisterCreatedObjectUndo(root,"添加弃牌盾牌");
            root.transform.SetParent(actor.transform,false);root.transform.position=actor.Visual.position;root.transform.rotation=actor.Visual.rotation;
            var view=Undo.AddComponent<DiscardShieldPresenter>(root);view.CardMesh=mesh;view.ShieldMaterial=material;view.Atlas=atlas;
            var renderers=actor.Visual.GetComponentsInChildren<Renderer>(true);var bounds=new Bounds(Vector3.zero,Vector3.zero);bool first=true;
            foreach(var renderer in renderers)
            {
                var b=renderer.bounds;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                { var point=root.transform.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point); }
            }
            float height=Mathf.Max(.08f,bounds.size.y*.65f),width=Mathf.Max(height,bounds.size.x);
            view.CardHeight=height;
            ConfigureRing(actor, view);
            view.SpawnCenter=view.RingCenter+new Vector3(0,height*1.9f,-height*1.8f);
            view.SpawnSize=new Vector3(width,height*.25f,height*.4f);view.ArcHeight=height*.5f;
            view.LandingSound=actor.Battle.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().DiscardSound;
            view.Haptics=actor.Battle.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().HapticSettings;
            var coordinator=Undo.AddComponent<BattleActorPresentationCoordinator>(root);coordinator.Configure(body,view);
            Undo.RecordObject(actor,"绑定总表现入口");actor.ConfigurePresentation(coordinator);
            EditorUtility.SetDirty(actor);EditorUtility.SetDirty(view);EditorUtility.SetDirty(coordinator);PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
            view.ValidateConfiguration();Selection.activeGameObject=root;
        }
    }
}
