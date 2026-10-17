using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit editor commands only; no runtime behaviour or automatic scene changes.
public static class TabletopCombatBlockout
{
    const string RootName = "TabletopCombat_Blockout";
    const string MaterialFolder = "Assets/SceneBlockouts/TabletopCombat/Materials";
    const string OutputFolder = "Blender/Combat_Layout_20261004";
    static readonly Dictionary<Object, string> OriginalObjects = new Dictionary<Object, string>();
    static Camera originalCamera;
    static string originalCameraJson;
    static Quaternion originalRotation;
    static Vector3 originalScale;
    static Vector3 originalPosition;
    static float originalFov;
    static int[] originalRootIds;

    [MenuItem("工具/场景美术/桌面白模/在当前场景搭建")]
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Build in Edit mode.");
        if (scene.path != "Assets/Scenes/CardRenderingValidation.unity")
            throw new InvalidOperationException("The inspected current scene has changed.");
        if (scene.GetRootGameObjects().Any(o => o.name == RootName))
            throw new InvalidOperationException("The blockout already exists. Edit its children instead.");
        var camera = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Camera>(true))
            .Single(c => c.CompareTag("MainCamera") && c.enabled);
        if (camera.orthographic) throw new InvalidOperationException("Expected the existing perspective camera.");
        Snapshot(scene, camera);
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add tabletop combat blockout");
        try
        {
            var wood = Mat("Wood", new Color(.39f, .235f, .13f));
            var plank = Mat("Planks", new Color(.46f, .30f, .18f));
            var edge = Mat("DarkWood", new Color(.19f, .105f, .062f));
            var gold = Mat("Ochre", new Color(.68f, .48f, .23f));
            var red = Mat("Player_Red", new Color(.54f, .17f, .085f));
            var blue = Mat("Enemy_Blue", new Color(.15f, .28f, .31f));
            var ivory = Mat("Ivory", new Color(.78f, .67f, .48f));
            var slots = Mat("PlaySlots", new Color(.085f, .065f, .048f));

            var root = Group(RootName, null);
            root.localPosition = new Vector3(.021f, 0, .35f);
            var table = Group("Table", root);
            Part("Tabletop", PrimitiveType.Cube, table, V(0,.022f,0), V(.82f,.032f,.36f), wood);
            for (int i=0; i<6; ++i)
                Part("Plank_"+(i+1), PrimitiveType.Cube, table,
                    V(0,.0395f,-.145f+i*.058f), V(.794f,.003f,.057f), i%2==0 ? plank : wood);
            foreach (float x in new[]{-.405f,.405f})
                Part(x<0 ? "Rail_Left" : "Rail_Right", PrimitiveType.Cube, table,
                    V(x,.042f,0), V(.012f,.010f,.36f), edge);
            foreach (float z in new[]{-.174f,.174f})
                Part(z<0 ? "Rail_Front" : "Rail_Back", PrimitiveType.Cube, table,
                    V(0,.042f,z), V(.80f,.010f,.012f), edge);
            Part("Front_Inlay", PrimitiveType.Cube, table, V(0,.022f,-.1808f), V(.78f,.003f,.001f), gold);

            var play = Group("PlayArea_5Slots", root);
            for (int i=0; i<5; ++i)
            {
                float x = (i-2)*.066f;
                Part("Slot_"+(i+1)+"_Border", PrimitiveType.Cube, play,
                    V(x,.0417f,-.090f), V(.056f,.001f,.084f), gold);
                Part("Slot_"+(i+1), PrimitiveType.Cube, play,
                    V(x,.0424f,-.090f), V(.052f,.001f,.080f), slots);
            }
            Pawn("Player", root, -.225f, red, ivory, gold);
            Pawn("Enemy", root, .225f, blue, ivory, gold);

            Undo.RecordObject(camera.transform, "Frame tabletop with existing camera");
            Undo.RecordObject(camera, "Set tabletop field of view");
            // Keep the existing orientation, clipping planes, renderer and all other settings.
            var target = new Vector3(.021f,.040f,.230f) - camera.transform.up*.020f;
            camera.transform.position = target - camera.transform.forward*.79f;
            camera.fieldOfView = 44;
            EditorUtility.SetDirty(camera);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root.gameObject;
            Undo.CollapseUndoOperations(undo);
            Validate();
            Debug.Log("Tabletop blockout created: " + root.GetComponentsInChildren<Renderer>().Length +
                      " primitive renderers; all under " + RootName + ". Existing camera position and FOV only.");
        }
        catch
        {
            Undo.RevertAllDownToGroup(undo);
            throw;
        }
    }

    static Transform Group(string name, Transform parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create blockout group");
        if (parent != null) go.transform.SetParent(parent, false);
        return go.transform;
    }

    static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        Undo.RegisterCreatedObjectUndo(go, "Create blockout primitive");
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        // Visual blockout should not intercept the existing card input raycasts.
        Undo.DestroyObjectImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void Pawn(string name, Transform parent, float x, Material body, Material ivory, Material gold)
    {
        var pawn = Group(name, parent);
        pawn.localPosition = V(x,0,.055f);
        Part("Base", PrimitiveType.Cylinder, pawn, V(0,.047f,0), V(.104f,.006f,.104f), gold);
        Part("Base_Top", PrimitiveType.Cylinder, pawn, V(0,.054f,0), V(.088f,.002f,.088f), body);
        Part("Body", PrimitiveType.Capsule, pawn, V(0,.099f,0), V(.062f,.043f,.062f), body);
        Part("Collar", PrimitiveType.Cylinder, pawn, V(0,.131f,0), V(.068f,.004f,.068f), ivory);
        Part("Head", PrimitiveType.Sphere, pawn, V(0,.154f,0), V(.052f,.054f,.052f), body);
        Part("Crown_Ring", PrimitiveType.Cylinder, pawn, V(0,.178f,0), V(.055f,.004f,.055f), gold);
        for (int i=0; i<5; ++i)
        {
            float angle = i*Mathf.PI*2/5;
            Part("Crown_Tooth_"+(i+1), PrimitiveType.Cube, pawn,
                V(Mathf.Cos(angle)*.022f,.186f,Mathf.Sin(angle)*.022f), V(.007f,.013f,.007f), gold);
        }
        float sign=x<0 ? 1 : -1;
        Part("Arm", PrimitiveType.Cube, pawn, V(sign*.035f,.116f,-.01f), V(.043f,.016f,.021f), body);
        Part("Staff", PrimitiveType.Cylinder, pawn, V(sign*.066f,.108f,-.015f), V(.006f,.052f,.006f), gold);
        Part("Staff_Top", PrimitiveType.Sphere, pawn, V(sign*.066f,.165f,-.015f), V(.016f,.018f,.016f), ivory);
    }

    static Vector3 V(float x,float y,float z) => new Vector3(x,y,z);

    static Material Mat(string name, Color color)
    {
        EnsureFolder(MaterialFolder);
        var path=MaterialFolder+"/"+name+".mat";
        var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit")) {name=name};
        material.SetColor("_BaseColor",color);
        material.SetFloat("_Smoothness",.12f);
        material.SetFloat("_Metallic",0);
        AssetDatabase.CreateAsset(material,path);
        return material;
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        var parent=Path.GetDirectoryName(folder).Replace('\\','/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent,Path.GetFileName(folder));
    }

    static void Snapshot(Scene scene, Camera camera)
    {
        OriginalObjects.Clear();
        originalRootIds=scene.GetRootGameObjects().Select(o=>o.GetInstanceID()).ToArray();
        originalCamera=camera;
        originalCameraJson=CameraWithoutFov(camera);
        originalPosition=camera.transform.position;
        originalRotation=camera.transform.rotation;
        originalScale=camera.transform.localScale;
        originalFov=camera.fieldOfView;
        foreach(var root in scene.GetRootGameObjects())
            foreach(var tr in root.GetComponentsInChildren<Transform>(true))
            {
                // Card preview's animated DontSave objects are managed by its existing editor window.
                if ((tr.gameObject.hideFlags & HideFlags.DontSaveInEditor)!=0) continue;
                OriginalObjects[tr.gameObject]=EditorJsonUtility.ToJson(tr.gameObject);
                foreach(var component in tr.GetComponents<Component>())
                    if(component!=null && component!=camera && component!=camera.transform)
                        OriginalObjects[component]=EditorJsonUtility.ToJson(component);
            }
    }

    static string CameraWithoutFov(Camera camera) => Regex.Replace(EditorJsonUtility.ToJson(camera),
        "\\\"field of view\\\":\\s*[-+0-9.eE]+", "\"field of view\":0");

    [MenuItem("工具/场景美术/桌面白模/验证原场景保留")]
    public static void Validate()
    {
        if(originalCamera==null) throw new InvalidOperationException("No build snapshot in this editor session.");
        var changed=OriginalObjects.Where(pair=>pair.Key==null || EditorJsonUtility.ToJson(pair.Key)!=pair.Value)
            .Select(pair=>pair.Key==null ? "deleted object" : pair.Key.name+" ("+pair.Key.GetType().Name+")").ToArray();
        if(changed.Length>0) throw new InvalidOperationException("Existing objects changed: "+string.Join(", ",changed));
        if(CameraWithoutFov(originalCamera)!=originalCameraJson || originalCamera.transform.rotation!=originalRotation ||
           originalCamera.transform.localScale!=originalScale)
            throw new InvalidOperationException("An unapproved camera property changed.");
        var roots=SceneManager.GetActiveScene().GetRootGameObjects();
        var added=roots.Where(o=>!originalRootIds.Contains(o.GetInstanceID())).ToArray();
        if(added.Length!=1 || added[0].name!=RootName) throw new InvalidOperationException("Expected exactly one new root.");
        var all=added[0].GetComponentsInChildren<Component>(true);
        if(all.Any(c=>c is Camera || c is Light || c is Canvas || c is Collider))
            throw new InvalidOperationException("Blockout contains an unexpected component.");
        Directory.CreateDirectory(OutputFolder);
        File.WriteAllText(OutputFolder+"/unity_blockout_validation.txt",
            "Scene: "+SceneManager.GetActiveScene().path+"\n"+
            "Verified unchanged existing objects/components: "+OriginalObjects.Count+"\n"+
            "New root: "+RootName+"\n"+
            "Only camera position and FOV changed. Rotation, clipping, renderer, lights and existing objects preserved.\n"+
            "Previous camera position: "+originalPosition.ToString("F6")+"; FOV: "+originalFov+"\n"+
            "Current camera position: "+originalCamera.transform.position.ToString("F6")+"; FOV: "+originalCamera.fieldOfView);
        Debug.Log("Tabletop preservation check passed: "+OriginalObjects.Count+" existing objects/components unchanged.");
    }

    [MenuItem("工具/场景美术/桌面白模/捕获当前相机")]
    public static void Capture()
    {
        var camera=Camera.main;
        if(camera==null) throw new InvalidOperationException("No existing main camera.");
        Directory.CreateDirectory(OutputFolder);
        var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32) {antiAliasing=1,filterMode=FilterMode.Point};
        var pixels=new Texture2D(1920,1080,TextureFormat.RGBA32,false);
        var previous=RenderTexture.active;
        try
        {
            rt.Create();
            var request=new UniversalRenderPipeline.SingleCameraRequest {destination=rt};
            if(!RenderPipeline.SupportsRenderRequest(camera,request)) throw new InvalidOperationException("Render capture unsupported.");
            RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture.active=rt;
            pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);
            pixels.Apply();
            File.WriteAllBytes(OutputFolder+"/unity_tabletop_16x9.png",pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(pixels);
        }
        Debug.Log("Captured current camera to "+OutputFolder+"/unity_tabletop_16x9.png");
    }
}
