using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace StylizedGrass.Editor
{
    public static class StylizedGrassSetup
    {
        const string Root="Assets/Art/Environment/StylizedGrass";
        const string ScenePath=Root+"/Scenes/StylizedGrassTest.unity";
        const string Output="ArtSource/StylizedGrass";

        [MenuItem("Tools/Scene Art/StylizedGrass/Build in Current Scene")]
        public static void Build()
        {
            if(Application.isPlaying) throw new InvalidOperationException("Exit Play mode before building.");
            var scene=SceneManager.GetActiveScene();
            if(scene.GetRootGameObjects().Any(o=>o.name!="Main Camera" && o.name!="Directional Light" && o.name!="StylizedGrass Test" && o.name!="StylizedGrass"))
                throw new InvalidOperationException("This builder expects the requested empty scene or its own test scene.");
            AssetDatabase.Refresh();
            ConfigureSource();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Source/Models/SM_StylizedGrass.fbx");
            var filters=source.GetComponentsInChildren<MeshFilter>().OrderByDescending(f=>f.sharedMesh.triangles.Length).ToArray();
            var near=BakeMesh(filters[0],"StylizedGrassNear",true);
            var far=BakeMesh(filters[filters.Length-1],"StylizedGrassFar",true);
            var grass=Material("M_StylizedGrass","StylizedGrass/Opaque Grass");
            grass.SetColor("_RootColor",ColorHex("291823"));
            grass.SetColor("_TipColor",ColorHex("8E293F"));
            grass.SetColor("_DryColor",ColorHex("95605C"));
            grass.SetFloat("_WindStrength",.065f);grass.SetFloat("_WindSpeed",.7f);
            grass.SetFloat("_Smoothness",.23f);grass.SetFloat("_RootOcclusion",.55f);
            grass.SetFloat("_NormalUp",.5f);grass.SetFloat("_Fill",.1f);
            grass.SetFloat("_WindTime",-1);grass.SetFloat("_Cull",0);grass.enableInstancing=true;
            EditorUtility.SetDirty(grass);
            var ground=Material("M_StylizedGrassGround","StylizedGrass/Grass Soil Blend");
            ground.SetTexture("_GrassMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Source/Textures/T_StylizedGrass_Color.png"));
            ground.SetTexture("_GrassHeight",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Source/Textures/T_StylizedGrass_Height.PNG"));
            ground.SetTexture("_GrassNormal",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Source/Textures/T_StylizedGrass_Normal.png"));
            ground.SetColor("_GrassColor",ColorHex("502332"));
            ground.SetColor("_DirtColor",ColorHex("853C44"));
            ground.SetFloat("_NormalStrength",.7f);ground.SetFloat("_GrassTiling",1.4f);
            ground.SetFloat("_SoilTiling",4.5f);ground.SetFloat("_BlendSharpness",1.6f);
            ground.SetFloat("_Fill",.1f);ground.SetFloat("_Smoothness",.13f);ground.SetFloat("_Cull",2);
            ground.enableInstancing=true;EditorUtility.SetDirty(ground);
            var old=GameObject.Find("StylizedGrass")??GameObject.Find("StylizedGrass Test");if(old) UnityEngine.Object.DestroyImmediate(old);
            var root=new GameObject("StylizedGrass");
            var terrain=new GameObject("StylizedGrass Ground");terrain.transform.SetParent(root.transform);
            Mesh terrainMesh=SaveMesh(GroundMesh(22,24,180,196),"StylizedGrassGround");
            terrain.AddComponent<MeshFilter>().sharedMesh=terrainMesh;
            terrain.AddComponent<MeshRenderer>().sharedMaterial=ground;
            terrain.AddComponent<MeshCollider>().sharedMesh=terrainMesh;
            var fieldGO=new GameObject("StylizedGrass Instances");fieldGO.transform.SetParent(root.transform);
            var field=fieldGO.AddComponent<StylizedGrassField>();field.nearMesh=near;field.farMesh=far;field.material=grass;
            field.size=new Vector2(22,24);field.spacing=.17f;field.heightRange=new Vector2(.18f,.43f);field.nearDistance=11.5f;field.Rebuild();
            var camera=Camera.main;
            if(!camera) {var go=new GameObject("Main Camera");go.tag="MainCamera";camera=go.AddComponent<Camera>();go.AddComponent<AudioListener>();}
            camera.transform.position=new Vector3(7.7f,8.3f,-12.7f);
            camera.transform.LookAt(new Vector3(-.4f,2.8f,1.6f));
            camera.fieldOfView=49;camera.nearClipPlane=.1f;camera.farClipPlane=80;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ColorHex("180F1B");
            camera.allowHDR=true;camera.allowMSAA=false;
            var data=camera.GetUniversalAdditionalCameraData();data.SetRenderer(-1);data.renderPostProcessing=false;
            data.antialiasing=AntialiasingMode.None;
            var keyGO=GameObject.Find("Directional Light");if(!keyGO)keyGO=new GameObject("Directional Light");
            var key=keyGO.GetComponent<Light>()??keyGO.AddComponent<Light>();key.type=LightType.Directional;
            key.color=ColorHex("FF9DAD");key.intensity=1.65f;key.shadows=LightShadows.Soft;
            key.shadowBias=.03f;key.shadowNormalBias=.25f;key.transform.rotation=Quaternion.Euler(24,-52,0);
            RenderSettings.sun=key;
            Light fill=Light("StylizedGrass Sky Fill",root.transform,LightType.Directional,ColorHex("929BCB"),.45f);
            fill.transform.rotation=Quaternion.Euler(52,135,0);
            Light rim=Light("StylizedGrass Grazing Light",root.transform,LightType.Point,ColorHex("FF244B"),14);
            rim.transform.position=new Vector3(-5,4.1f,3);rim.range=11;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=ColorHex("837080");RenderSettings.ambientEquatorColor=ColorHex("433C50");
            RenderSettings.ambientGroundColor=ColorHex("25151D");RenderSettings.ambientIntensity=1;
            RenderSettings.skybox=null;RenderSettings.fog=false;
            DynamicGI.UpdateEnvironment();
            Tune();
            var manager=root.AddComponent<StylizedGrassManager>();manager.ReadCurrentSettings();
            manager.Apply();
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);
            Selection.activeGameObject=root;
            if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(0,2.8f,0),camera.transform.rotation,15);
            SceneView.RepaintAll();
            Debug.Log($"StylizedGrass built in {Application.dataPath}: source meshes {near.triangles.Length/3}/{far.triangles.Length/3} triangles, instancing enabled. Scene: {ScenePath}");
        }
        static Color ColorHex(string hex) {ColorUtility.TryParseHtmlString("#"+hex,out var color);return color;}
        [MenuItem("Tools/Scene Art/StylizedGrass/Preview Patchy Lodging")]
        public static void PreviewLodging()
        {
            var field=UnityEngine.Object.FindObjectOfType<StylizedGrassField>();
            if(!field)throw new InvalidOperationException("Open the StylizedGrass test first.");
            var material=field.material;
            if(field.manager && field.manager.isActiveAndEnabled)
            {
                var manager=field.manager;Undo.RecordObject(manager,"Preview grass lodging");
                manager.lodging=.95f;manager.lodgingDirection=25;manager.lodgingVariation=.85f;manager.lodgingPatchSize=3.5f;
                manager.Apply();EditorUtility.SetDirty(manager);EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            }
            else
            {
                Undo.RecordObject(material,"Preview grass lodging");
                material.SetFloat("_Lodging",.95f);material.SetFloat("_LodgingDirection",25);
                material.SetFloat("_LodgingVariation",.85f);material.SetFloat("_LodgingPatchSize",3.5f);
            }
            foreach(var mesh in new[]{field.nearMesh,field.farMesh})
            {
                Undo.RecordObject(mesh,"Expand grass deformation bounds");
                ExpandGrassBounds(mesh);EditorUtility.SetDirty(mesh);
            }
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
            Selection.activeObject=field.manager?(UnityEngine.Object)field.manager.gameObject:material;SceneView.RepaintAll();
        }
        static void ExpandGrassBounds(Mesh mesh)
        {
            mesh.RecalculateBounds();var bounds=mesh.bounds;
            // Include the sideways arc and wind in the instanced draw's culling bounds.
            bounds.Expand(new Vector3(2.2f,.4f,2.2f));mesh.bounds=bounds;
        }
        [MenuItem("Tools/Scene Art/StylizedGrass/Tune Dark Red Palette")]
        public static void Tune()
        {
            var field=UnityEngine.Object.FindObjectOfType<StylizedGrassField>();if(!field)return;
            var grass=field.material;
            grass.SetColor("_RootColor",ColorHex("342133"));grass.SetColor("_TipColor",ColorHex("812D44"));
            grass.SetColor("_DryColor",ColorHex("794650"));grass.SetFloat("_Fill",.12f);
            grass.SetFloat("_Smoothness",.08f);grass.SetFloat("_NormalUp",.7f);grass.SetFloat("_RootOcclusion",.32f);
            field.spacing=.21f;field.heightRange=new Vector2(.22f,.48f);field.Rebuild();
            var ground=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/M_StylizedGrassGround.mat");
            ground.SetColor("_GrassColor",ColorHex("472536"));ground.SetColor("_DirtColor",ColorHex("713140"));ground.SetFloat("_Fill",.1f);
            var key=GameObject.Find("Directional Light").GetComponent<Light>();key.color=ColorHex("FFE1E4");key.intensity=1.4f;
            var rim=GameObject.Find("StylizedGrass Grazing Light").GetComponent<Light>();rim.color=ColorHex("EE6E89");rim.intensity=3;
            RenderSettings.ambientSkyColor=ColorHex("A191A1");RenderSettings.ambientEquatorColor=ColorHex("6D607A");
            RenderSettings.ambientGroundColor=ColorHex("49313C");
            var camera=Camera.main;camera.backgroundColor=ColorHex("241320");
            camera.transform.position=new Vector3(4.5f,8.3f,-8.5f);camera.transform.LookAt(new Vector3(-.5f,2.8f,.5f));camera.fieldOfView=49;
            if(field.manager){field.manager.ReadCurrentSettings();field.manager.Apply();EditorUtility.SetDirty(field.manager);}
            EditorUtility.SetDirty(grass);EditorUtility.SetDirty(ground);EditorUtility.SetDirty(field);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if(!string.IsNullOrEmpty(SceneManager.GetActiveScene().path))EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        [MenuItem("Tools/Scene Art/StylizedGrass/Use Project Renderer")]
        public static void UseProjectRenderer()
        {
            if(Application.isPlaying) throw new InvalidOperationException("Exit Play mode before saving renderer settings.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=ScenePath) throw new InvalidOperationException("Open the StylizedGrass test first.");
            var camera=Camera.main;
            camera.GetUniversalAdditionalCameraData().SetRenderer(-1);
            EditorUtility.SetDirty(camera.GetUniversalAdditionalCameraData());
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();SceneView.RepaintAll();
            Debug.Log("StylizedGrass now uses the project's default renderer.");
        }
        static Material Material(string name,string shaderName)
        {
            var shader=Shader.Find(shaderName);if(!shader)throw new InvalidOperationException("Missing shader "+shaderName);
            string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(shader){name=name};AssetDatabase.CreateAsset(m,path);}else m.shader=shader;
            return m;
        }
        static Light Light(string name,Transform parent,LightType type,Color color,float intensity)
        {
            var go=new GameObject(name);go.transform.SetParent(parent);var l=go.AddComponent<Light>();
            l.type=type;l.color=color;l.intensity=intensity;l.shadows=LightShadows.None;return l;
        }
        static void ConfigureSource()
        {
            foreach(string f in new[]{"T_StylizedGrass_Color.png","T_StylizedGrass_Height.PNG","T_StylizedGrass_Normal.png"})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(Root+"/Source/Textures/"+f);
                importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.anisoLevel=4;
                importer.sRGBTexture=f=="T_StylizedGrass_Color.png";
                if(f=="T_StylizedGrass_Normal.png")importer.textureType=TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }
        internal static Mesh BakeMesh(MeshFilter filter,string name,bool grass)
        {
            var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name=name;
            var vertices=mesh.vertices;var normals=mesh.normals;var tangents=mesh.tangents;
            var matrix=filter.transform.localToWorldMatrix;
            for(int i=0;i<vertices.Length;i++)vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);
            var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);
            float scale=1/Mathf.Max(.001f,bounds.size.y);
            var origin=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);var colors=new Color[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                vertices[i]=(vertices[i]-origin)*scale;
                if(normals.Length==vertices.Length)normals[i]=matrix.inverse.transpose.MultiplyVector(normals[i]).normalized;
                if(tangents.Length==vertices.Length){Vector3 t=matrix.MultiplyVector(tangents[i]).normalized;tangents[i]=new Vector4(t.x,t.y,t.z,tangents[i].w);}
                colors[i]=new Color(grass?Mathf.Clamp01(vertices[i].y):0,0,0,1);
            }
            mesh.vertices=vertices;mesh.normals=normals;if(tangents.Length==vertices.Length)mesh.tangents=tangents;
            mesh.colors=colors;mesh.RecalculateBounds();if(normals.Length==0)mesh.RecalculateNormals();
            if(grass)ExpandGrassBounds(mesh);
            return SaveMesh(mesh,name);
        }
        internal static Mesh SaveMesh(Mesh mesh,string name)
        {
            string path=Root+"/Meshes/"+name+".asset";var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(previous)
            {
                previous.Clear();previous.indexFormat=mesh.indexFormat;previous.vertices=mesh.vertices;previous.normals=mesh.normals;
                previous.uv=mesh.uv;previous.colors=mesh.colors;previous.tangents=mesh.tangents;previous.triangles=mesh.triangles;
                previous.bounds=mesh.bounds;previous.UploadMeshData(false);EditorUtility.SetDirty(previous);
                UnityEngine.Object.DestroyImmediate(mesh);return previous;
            }
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        internal static Mesh GroundMesh(float width,float depth,int nx,int nz,StylizedGrassTerrainData data=null)
        {
            var mesh=new Mesh{name="StylizedGrassGround",indexFormat=IndexFormat.UInt32};
            var vertices=new Vector3[(nx+1)*(nz+1)];var uv=new Vector2[vertices.Length];var colors=new Color[vertices.Length];
            var triangles=new int[nx*nz*6];int t=0;
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                int i=z*(nx+1)+x;float px=(x/(float)nx-.5f)*width,pz=(z/(float)nz-.5f)*depth;
                vertices[i]=new Vector3(px,data?data.Height(px,pz):StylizedGrassField.Height(px,pz),pz);uv[i]=new Vector2(px,pz);
                colors[i]=new Color(data?data.Coverage(px,pz):StylizedGrassField.Coverage(px,pz),0,0,1);
                if(x<nx && z<nz){triangles[t++]=i;triangles[t++]=i+nx+1;triangles[t++]=i+1;triangles[t++]=i+1;triangles[t++]=i+nx+1;triangles[t++]=i+nx+2;}
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        [MenuItem("Tools/Scene Art/StylizedGrass/Validate and Capture")]
        public static void ValidateAndCapture()
        {
            Directory.CreateDirectory(Output);
            var camera=Camera.main;var field=UnityEngine.Object.FindObjectOfType<StylizedGrassField>();
            if(!camera||!field)throw new InvalidOperationException("Open the StylizedGrass test first.");
            Capture(camera,Output+"/StylizedGrass_Overview.png");
            var renderer=camera.GetUniversalAdditionalCameraData().scriptableRenderer;
            var cameraData=new SerializedObject(camera.GetUniversalAdditionalCameraData());
            int rendererIndex=cameraData.FindProperty("m_RendererIndex").intValue;
            var pipelineData=new SerializedObject(GraphicsSettings.currentRenderPipeline);
            int effectiveIndex=rendererIndex<0?pipelineData.FindProperty("m_DefaultRendererIndex").intValue:rendererIndex;
            var rendererData=pipelineData.FindProperty("m_RendererDataList").GetArrayElementAtIndex(effectiveIndex).objectReferenceValue;
            string mode=renderer.GetType().GetProperty("renderingModeActual",BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(renderer)?.ToString();
            var errors=new System.Collections.Generic.List<string>();
            foreach(string shaderName in new[]{"StylizedGrass/Opaque Grass","StylizedGrass/Grass Soil Blend"})
            {
                var shader=Shader.Find(shaderName);errors.AddRange(ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity.ToString()=="Error").Select(m=>shaderName+": "+m.message));
                var m=new Material(shader);
                foreach(string pass in new[]{"GBuffer","Forward","ShadowCaster","DepthOnly","DepthNormals","Meta"})
                    if(m.FindPass(pass)<0)errors.Add(shaderName+": missing "+pass);
                UnityEngine.Object.DestroyImmediate(m);
            }
            if(mode!="Deferred")errors.Add("Camera renderer is "+mode+", expected Deferred.");
            if(!field.material.enableInstancing || field.LastInstances==0)errors.Add("Instanced grass did not submit any visible instances.");
            File.WriteAllText(Output+"/validation.json",JsonUtility.ToJson(new Report{device=SystemInfo.graphicsDeviceType.ToString(),renderer=mode,
                pipelineAsset=AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline),rendererAsset=AssetDatabase.GetAssetPath(rendererData),cameraRendererIndex=rendererIndex,
                totalGrass=field.TotalInstances,visibleGrass=field.LastInstances,instancedDraws=field.LastDrawCalls,nearBatches=field.LastNearBatches,
                instancing=field.material.enableInstancing,isPlaying=Application.isPlaying,nearTriangles=field.nearMesh.triangles.Length/3,farTriangles=field.farMesh.triangles.Length/3,
                grassLod=field.MainCameraStatistics,frame=((UniversalRenderer)renderer).lastCameraRenderTargetInfo,errors=errors.ToArray()},true));
            if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            Debug.Log($"StylizedGrass validation passed: {mode}, {field.LastInstances} visible grass instances / {field.LastDrawCalls} instanced draws. Capture: {Output}");
        }
        [MenuItem("Tools/Scene Art/StylizedGrass/Capture Closeup")]
        public static void CaptureCloseup()
        {
            Directory.CreateDirectory(Output);var camera=Camera.main;
            Vector3 p=camera.transform.position;Quaternion r=camera.transform.rotation;float f=camera.fieldOfView;
            try
            {
                camera.transform.position=new Vector3(3.1f,4.05f,-4.5f);camera.transform.LookAt(new Vector3(-.3f,2.65f,-.6f));camera.fieldOfView=48;
                Capture(camera,Output+"/StylizedGrass_Closeup.png");
            }
            finally{camera.transform.SetPositionAndRotation(p,r);camera.fieldOfView=f;}
        }
        [MenuItem("Tools/Scene Art/StylizedGrass/Verify Grass and Wind Pixels")]
        public static void VerifyGrassAndWind()
        {
            var field=UnityEngine.Object.FindObjectOfType<StylizedGrassField>();var camera=Camera.main;
            if(!field || !camera)throw new InvalidOperationException("Open the turf test scene.");
            var manager=field.manager;
            bool oldDraw=manager?manager.drawGrass:field.drawGrass;float oldTime=field.material.GetFloat("_WindTime");
            void SetDraw(bool draw){if(manager){manager.drawGrass=draw;manager.Apply();}else field.drawGrass=draw;}
            Color32[] bare=null,still=null;int stage=0;
            Directory.CreateDirectory(Output);
            SetDraw(false);
            void Tick()
            {
                try
                {
                    if(stage==0)
                    {
                        bare=Capture(camera,Output+"/Validation_BareGround.png");
                        SetDraw(true);field.material.SetFloat("_WindTime",0);stage++;
                        EditorApplication.delayCall+=Tick;
                    }
                    else if(stage==1)
                    {
                        still=Capture(camera,Output+"/Validation_Wind0.png");
                        field.material.SetFloat("_WindTime",2.4f);stage++;EditorApplication.delayCall+=Tick;
                    }
                    else
                    {
                        var moving=Capture(camera,Output+"/Validation_Wind1.png");
                        int grassPixels=Changed(bare,still),windPixels=Changed(still,moving);
                        File.WriteAllText(Output+"/pixel-validation.json","{\"grassChangedPixels\":"+grassPixels+",\"windChangedPixels\":"+windPixels+",\"totalPixels\":"+moving.Length+"}");
                        if(grassPixels<10000 || windPixels<1000)throw new InvalidOperationException("Grass/wind rasterization check failed.");
                        SetDraw(oldDraw);field.material.SetFloat("_WindTime",oldTime);
                        Debug.Log($"Grass rasterization and wind verified: {grassPixels} grass pixels, {windPixels} wind pixels changed.");
                    }
                }
                catch(Exception e){SetDraw(oldDraw);field.material.SetFloat("_WindTime",oldTime);Debug.LogException(e);}
            }
            EditorApplication.delayCall+=Tick;
        }
        static int Changed(Color32[] a,Color32[] b)
        {
            int n=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>12)n++;return n;
        }
        [Serializable]sealed class Report
        {
            public string grassRenderPath="Opaque Deferred",device,renderer,pipelineAsset,rendererAsset;public int cameraRendererIndex,totalGrass,visibleGrass,instancedDraws,nearBatches,nearTriangles,farTriangles;
            public bool instancing,isPlaying;public StylizedGrassField.RenderStatistics grassLod;public CameraRenderTargetInfo frame;public string[] errors;
        }
        internal static Color32[] Capture(Camera camera,string path)
        {
            var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32){antiAliasing=1,filterMode=FilterMode.Point};
            var image=new Texture2D(1600,900,TextureFormat.RGBA32,false);var previous=RenderTexture.active;float aspect=camera.aspect;
            try
            {
                target.Create();camera.aspect=16f/9;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally{camera.aspect=aspect;RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
