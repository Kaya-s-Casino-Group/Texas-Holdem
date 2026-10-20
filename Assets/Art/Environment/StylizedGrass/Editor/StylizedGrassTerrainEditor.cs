using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StylizedGrass.Editor
{
    [InitializeOnLoad]
    public static class StylizedGrassTerrainEditing
    {
        static StylizedGrassTerrainEditing(){Undo.undoRedoPerformed+=AfterUndo;}
        static void AfterUndo()
        {
            if(Application.isPlaying)return;
            foreach(var manager in UnityEngine.Object.FindObjectsOfType<StylizedGrassManager>())
            {
                if(!manager.groundRenderer || !manager.field)continue;
                if(manager.terrainData)manager.terrainData.Invalidate();
                Sync(manager);Save(manager);
            }
        }
        public static string Problem(StylizedGrassManager manager)
        {
            if(!manager || !manager.groundRenderer || !manager.field)return "请选择带有地面和草场的 StylizedGrass 管理器。";
            if(Application.isPlaying)return "请退出 Play Mode 后编辑。";
            var filter=manager.groundRenderer.GetComponent<MeshFilter>();
            if(!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable)return "地面需要可读 MeshFilter 网格。";
            if(!manager.groundRenderer.GetComponent<MeshCollider>())return "地面需要 MeshCollider，用于定位画笔。";
            if(manager.field.transform.localToWorldMatrix!=manager.groundRenderer.transform.localToWorldMatrix)
                return "草场与地面需使用相同的 Transform。可一起移动或缩放父级 StylizedGrass。";
            return null;
        }
        public static void Install(StylizedGrassManager manager)
        {
            string problem=Problem(manager);if(problem!=null)throw new InvalidOperationException(problem);
            if(manager.terrainData && manager.terrainData.IsInitialized)return;
            if(!manager.terrainData)
            {
                const string folder="Assets/Art/Environment/StylizedGrass/TerrainData";
                Directory.CreateDirectory(folder);AssetDatabase.Refresh();
                var data=ScriptableObject.CreateInstance<StylizedGrassTerrainData>();
                data.Initialize(new Vector2(manager.groundRenderer.GetComponent<MeshFilter>().sharedMesh.bounds.size.x,
                    manager.groundRenderer.GetComponent<MeshFilter>().sharedMesh.bounds.size.z));
                AssetDatabase.CreateAsset(data,AssetDatabase.GenerateUniqueAssetPath(folder+"/StylizedGrassTerrain.asset"));
                Undo.RecordObject(manager,"接入草地地形编辑器");manager.terrainData=data;
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(manager.terrainData,"初始化地形数据");
                var bounds=manager.groundRenderer.GetComponent<MeshFilter>().sharedMesh.bounds;
                manager.terrainData.Initialize(new Vector2(bounds.size.x,bounds.size.z));
            }
            manager.Apply();EditorUtility.SetDirty(manager);EditorUtility.SetDirty(manager.field);
            Save(manager);EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            EditorSceneManager.SaveScene(manager.gameObject.scene);
        }
        public static void Sync(StylizedGrassManager manager)
        {
            if(Problem(manager)!=null)return;
            manager.Apply();
            var filter=manager.groundRenderer.GetComponent<MeshFilter>();var mesh=filter.sharedMesh;
            var vertices=mesh.vertices;var colors=mesh.colors;
            if(colors.Length!=vertices.Length)colors=new Color[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var p=vertices[i];p.y=manager.field.SurfaceHeight(p.x,p.z);vertices[i]=p;
                colors[i]=new Color(manager.field.SurfaceCoverage(p.x,p.z),0,0,1);
            }
            mesh.vertices=vertices;mesh.colors=colors;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            mesh.UploadMeshData(false);EditorUtility.SetDirty(mesh);
            var collider=manager.groundRenderer.GetComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=mesh;
            manager.field.Rebuild();SceneView.RepaintAll();EditorApplication.QueuePlayerLoopUpdate();
        }
        public static void Save(StylizedGrassManager manager)
        {
            if(!manager)return;
            if(manager.terrainData){EditorUtility.SetDirty(manager.terrainData);AssetDatabase.SaveAssetIfDirty(manager.terrainData);}
            if(manager.groundRenderer)
            {
                var filter=manager.groundRenderer.GetComponent<MeshFilter>();
                if(filter && filter.sharedMesh)AssetDatabase.SaveAssetIfDirty(filter.sharedMesh);
            }
        }
    }

    public sealed class StylizedGrassTerrainEditor : EditorWindow
    {
        [SerializeField] StylizedGrassManager manager;
        [SerializeField] GrassTerrainBrush brush;
        [SerializeField] float radius=1.5f,strength=.5f,hardness=.25f,flattenHeight=2.8f;
        bool painting,stroke,pending;
        int strokeUndo=-1,control;
        double lastSync;
        Vector2 lastPoint;
        static readonly string[] Names={"抬高","压低","平滑","定高","加草","擦草","恢复原貌"};

        [MenuItem("Tools/Scene Art/StylizedGrass/Terrain Editor")]
        public static void OpenMenu(){Open(UnityEngine.Object.FindObjectOfType<StylizedGrassManager>());}
        public static void Open(StylizedGrassManager target)
        {
            var window=GetWindow<StylizedGrassTerrainEditor>("草地地形编辑器");
            window.Finish();window.manager=target;window.minSize=new Vector2(320,430);window.Show();
        }
        [MenuItem("Tools/Scene Art/StylizedGrass/Install Terrain Editor")]
        public static void InstallCurrent()
        {
            var target=UnityEngine.Object.FindObjectOfType<StylizedGrassManager>();
            StylizedGrassTerrainEditing.Install(target);Open(target);
            Debug.Log("草地地形编辑器已接入：Scene 画笔、独立数据资产、撤销/重做、地形/草/碰撞同步。当前外观未重置。");
        }
        void OnEnable()
        {
            SceneView.duringSceneGui+=SceneGUI;EditorApplication.update+=Tick;
            AssemblyReloadEvents.beforeAssemblyReload+=Finish;
        }
        void OnDisable()
        {
            Finish();SceneView.duringSceneGui-=SceneGUI;EditorApplication.update-=Tick;
            AssemblyReloadEvents.beforeAssemblyReload-=Finish;
        }
        void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            var next=(StylizedGrassManager)EditorGUILayout.ObjectField("草地管理器",manager,typeof(StylizedGrassManager),true);
            if(EditorGUI.EndChangeCheck()){Finish();manager=next;painting=false;}
            string problem=StylizedGrassTerrainEditing.Problem(manager);
            if(problem!=null){EditorGUILayout.HelpBox(problem,MessageType.Info);return;}
            if(!manager.terrainData || !manager.terrainData.IsInitialized)
            {
                EditorGUILayout.HelpBox("首次接入会创建独立笔刷数据，保留当前草地参数、地形基底和材质。",MessageType.Info);
                if(GUILayout.Button("为此草地启用编辑"))StylizedGrassTerrainEditing.Install(manager);
                return;
            }
            using(new EditorGUI.DisabledScope(true))EditorGUILayout.ObjectField("笔刷数据",manager.terrainData,typeof(StylizedGrassTerrainData),false);
            EditorGUILayout.LabelField("画笔",EditorStyles.boldLabel);
            brush=(GrassTerrainBrush)GUILayout.SelectionGrid((int)brush,Names,3);
            radius=EditorGUILayout.Slider("半径（地形局部米）",radius,.15f,6);
            strength=EditorGUILayout.Slider("力度",strength,.05f,3);
            hardness=EditorGUILayout.Slider("硬度",hardness,0,.95f);
            if(brush==GrassTerrainBrush.Flatten)flattenHeight=EditorGUILayout.FloatField("目标高度（局部 Y）",flattenHeight);
            EditorGUI.BeginChangeCheck();painting=GUILayout.Toggle(painting,painting?"画笔已启用 · 点击暂停":"启用 Scene 画笔","Button",GUILayout.Height(32));
            if(EditorGUI.EndChangeCheck()){if(!painting)Finish();SceneView.RepaintAll();}
            EditorGUILayout.HelpBox("在 Scene 视图按住左键拖动涂刷。\nShift：反向抬高/压低、加草/擦草。\nCtrl / ⌘ + 左键：采样定高高度。\nAlt：保持视角操作。Esc：退出画笔。\n⌘Z / Ctrl+Z 撤销；重做也会同步地面和草。",MessageType.Info);
            EditorGUILayout.LabelField("加草 / 擦草同步改变草皮与裸土混合；主相机 LOD 和 GPU Instancing 保持生效。",EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();
            if(GUILayout.Button("保存地形与场景"))
            {
                Finish();StylizedGrassTerrainEditing.Save(manager);
                EditorSceneManager.SaveScene(manager.gameObject.scene);ShowNotification(new GUIContent("地形与场景已保存"));
            }
            if(GUILayout.Button("定位地形"))
            {
                Selection.activeGameObject=manager.groundRenderer.gameObject;
                if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.Frame(manager.groundRenderer.bounds,false);
            }
        }
        void Tick()
        {
            if(pending && EditorApplication.timeSinceStartup-lastSync>.12)
            {StylizedGrassTerrainEditing.Sync(manager);pending=false;lastSync=EditorApplication.timeSinceStartup;}
            if(stroke && (!manager || !painting || Application.isPlaying))Finish();
        }
        void Finish()
        {
            if(pending && manager)StylizedGrassTerrainEditing.Sync(manager);
            if(stroke)
            {
                StylizedGrassTerrainEditing.Save(manager);
                if(strokeUndo>=0)Undo.CollapseUndoOperations(strokeUndo);
            }
            if(control!=0 && GUIUtility.hotControl==control)GUIUtility.hotControl=0;
            stroke=false;pending=false;strokeUndo=-1;
        }
        void SceneGUI(SceneView view)
        {
            if(!painting || StylizedGrassTerrainEditing.Problem(manager)!=null || !manager.terrainData)return;
            Event e=Event.current;
            control=GUIUtility.GetControlID("StylizedGrassTerrainBrush".GetHashCode(),FocusType.Passive);
            if(e.type==EventType.KeyDown && e.keyCode==KeyCode.Escape){Finish();painting=false;e.Use();Repaint();return;}
            if(e.rawType==EventType.MouseUp && e.button==0 && stroke){Finish();e.Use();return;}
            if(e.alt || e.button==1 || e.button==2){if(stroke)Finish();return;}
            if(e.type==EventType.Layout)HandleUtility.AddDefaultControl(control);
            var collider=manager.groundRenderer.GetComponent<MeshCollider>();
            if(!collider.Raycast(HandleUtility.GUIPointToWorldRay(e.mousePosition),out var hit,10000))return;
            Transform ground=manager.groundRenderer.transform;Vector3 local=ground.InverseTransformPoint(hit.point);
            using(new Handles.DrawingScope(new Color(.25f,1,.6f,1),ground.localToWorldMatrix))
            {
                Handles.DrawWireDisc(local+Vector3.up*.02f,Vector3.up,radius);
                Handles.color=new Color(.25f,1,.6f,.4f);Handles.DrawWireDisc(local+Vector3.up*.02f,Vector3.up,radius*hardness);
            }
            if(e.type==EventType.MouseMove)view.Repaint();
            if(e.type!=EventType.MouseDown && e.type!=EventType.MouseDrag || e.button!=0)return;
            if(e.type==EventType.MouseDown && EditorGUI.actionKey)
            {flattenHeight=local.y;brush=GrassTerrainBrush.Flatten;e.Use();Repaint();return;}
            if(e.type==EventType.MouseDown)
            {
                Undo.IncrementCurrentGroup();strokeUndo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("草地地形 · "+Names[(int)brush]);
                Undo.RegisterCompleteObjectUndo(manager.terrainData,"草地地形画笔");stroke=true;GUIUtility.hotControl=control;
                lastPoint=new Vector2(local.x,local.z);Stamp(lastPoint,e.shift,1);
            }
            else if(stroke)
            {
                Vector2 point=new Vector2(local.x,local.z);float distance=Vector2.Distance(lastPoint,point);
                float step=Mathf.Max(.04f,radius*.15f);
                if(distance>.005f)
                {
                    int count=Mathf.Clamp(Mathf.CeilToInt(distance/step),1,64);
                    for(int i=1;i<=count;i++)Stamp(Vector2.Lerp(lastPoint,point,i/(float)count),e.shift,Mathf.Min(distance/step/count,1));
                    lastPoint=point;
                }
            }
            e.Use();view.Repaint();
        }
        void Stamp(Vector2 point,bool reverse,float amount)
        {
            var mode=brush;
            if(reverse)
            {
                if(mode==GrassTerrainBrush.Raise)mode=GrassTerrainBrush.Lower;else if(mode==GrassTerrainBrush.Lower)mode=GrassTerrainBrush.Raise;
                else if(mode==GrassTerrainBrush.AddGrass)mode=GrassTerrainBrush.EraseGrass;else if(mode==GrassTerrainBrush.EraseGrass)mode=GrassTerrainBrush.AddGrass;
            }
            if(manager.terrainData.Paint(point,radius,strength*.2f*amount,hardness,mode,flattenHeight))
            {EditorUtility.SetDirty(manager.terrainData);pending=true;}
        }
    }
}
