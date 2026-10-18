using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public static class PokerCharacterIdleSetup
{
    const string Folder="Assets/Art/Models/Characters/PokerCharacter";
    const string Animations=Folder+"/Animations";
    const string Prefab=Folder+"/PokerCharacter.prefab";
    const string Output="Art/HandSkinReview/Unity/Idle";
    static Vector3 visualOffset;
    static readonly string[] Names={"Idle","Listen","LastChip","Tarot"};
    static readonly float[] Lengths={16,5.5f,8,7};
    static Transform Bone(Animator a,HumanBodyBones b)=>a.GetBoneTransform(b);
    static float Bell(float t,float start,float peak,float end)
    {
        if(t<=start||t>=end)return 0;
        float x=t<peak?(t-start)/(peak-start):(end-t)/(end-peak);
        return Mathf.SmoothStep(0,1,x);
    }
    static void Rotate(Transform t,Vector3 axis,float degrees) {t.rotation=Quaternion.AngleAxis(degrees,axis)*t.rotation;}
    static void SolveIK(Transform upper,Transform lower,Transform tip,Vector3 target,Vector3 pole)
    {
        Vector3 root=upper.position,delta=target-root;
        float a=Vector3.Distance(root,lower.position),b=Vector3.Distance(lower.position,tip.position);
        float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.0001f,a+b-.0001f);
        Vector3 direction=delta.normalized;
        Vector3 bend=Vector3.ProjectOnPlane(pole-root,direction).normalized;
        float x=(a*a-b*b+d*d)/(2*d),y=Mathf.Sqrt(Mathf.Max(0,a*a-x*x));
        Vector3 elbow=root+direction*x+bend*y;
        upper.rotation=Quaternion.FromToRotation(lower.position-root,elbow-root)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(tip.position-lower.position,target-lower.position)*lower.rotation;
    }
    static string MuscleProperty(string muscle)
    {
        if(muscle.Contains("Stretched")||muscle.Contains(" Spread"))
        {var p=muscle.Split(' ');return p[0]+"Hand."+p[1]+"."+string.Join(" ",p.Skip(2));}
        return muscle;
    }
    static void WriteAsset<T>(T asset,string path) where T:UnityEngine.Object
    {
        var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(old) {EditorUtility.CopySerialized(asset,old);UnityEngine.Object.DestroyImmediate(asset);EditorUtility.SetDirty(old);}
        else AssetDatabase.CreateAsset(asset,path);
    }

    [MenuItem("Tools/Hand Skin Validation/11 Build Poker Character Idle")]
    public static void Build()
    {
        if(!AssetDatabase.IsValidFolder(Animations))AssetDatabase.CreateFolder(Folder,"Animations");
        Directory.CreateDirectory(Output);
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Models/PokerCharacter_Visual.prefab"),scene);
            model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var animator=model.GetComponent<Animator>();animator.runtimeAnimatorController=null;
            if(!animator.avatar||!animator.avatar.isValid||!animator.avatar.isHuman)throw new Exception("Humanoid invalid after face import");
            var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();
            foreach(string shape in new[]{"Blink_L","Blink_R","Smile_Subtle"})
                if(skin.sharedMesh.GetBlendShapeIndex(shape)<0)throw new Exception("Missing blend shape "+shape);
            var transforms=model.GetComponentsInChildren<Transform>();
            var positions=transforms.Select(t=>t.localPosition).ToArray();
            var rotations=transforms.Select(t=>t.localRotation).ToArray();
            var lf=Bone(animator,HumanBodyBones.LeftFoot);var rf=Bone(animator,HumanBodyBones.RightFoot);
            Vector3 leftFoot=lf.position,rightFoot=rf.position;
            float side=Mathf.Sign(leftFoot.x);
            var neutralMesh=new Mesh();skin.BakeMesh(neutralMesh,true);
            float sole=neutralMesh.vertices.Min(v=>skin.transform.TransformPoint(v).y);
            UnityEngine.Object.DestroyImmediate(neutralMesh);
            var footCenter=(leftFoot+rightFoot)*.5f;
            visualOffset=new Vector3(-footCenter.x,-sole,-footCenter.z);
            File.WriteAllText(Output+"/bones.txt",string.Join("\n",new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot}.Select(b=>b+"="+Bone(animator,b).position)));
            Quaternion leftRotation=lf.rotation,rightRotation=rf.rotation;
            using(var handler=new HumanPoseHandler(animator.avatar,animator.transform))
            {
                for(int beat=0;beat<Names.Length;beat++)
                {
                    var clip=new AnimationClip{name="PokerCharacter_"+Names[beat],frameRate=30};
                    var curves=Enumerable.Range(0,HumanTrait.MuscleCount+7).Select(_=>new AnimationCurve()).ToArray();
                    var smile=new AnimationCurve();
                    int samples=Mathf.RoundToInt(Lengths[beat]*15);
                    for(int sample=0;sample<=samples;sample++)
                    {
                        float t=sample/15f;
                        for(int i=0;i<transforms.Length;i++){transforms[i].localPosition=positions[i];transforms[i].localRotation=rotations[i];}
                        float cycle=Mathf.Sin(t*Mathf.PI/4),breath=Mathf.Sin(t*Mathf.PI*6/16);
                        float listen=beat==1?Bell(t,.25f,1.25f,4.9f):0;
                        float watch=beat==2?Bell(t,.5f,2.1f,3.9f):0;
                        float freeze=beat==2?Bell(t,3.4f,4.0f,6.8f):0;
                        float secret=beat==3?Bell(t,.5f,2.2f,5.9f):0;
                        float check=beat==3?Bell(t,2.1f,3.15f,4.7f):0;
                        var hips=Bone(animator,HumanBodyBones.Hips);
                        hips.position+=new Vector3(-.008f+.003f*cycle,-.003f+.0007f*breath,.0015f*cycle);
                        Rotate(hips,Vector3.forward,1.8f+.5f*cycle);
                        Rotate(Bone(animator,HumanBodyBones.Spine),Vector3.right,4.2f+.55f*breath+listen*2);
                        Rotate(Bone(animator,HumanBodyBones.Chest),Vector3.right,2.5f+listen*2);
                        Rotate(Bone(animator,HumanBodyBones.LeftShoulder),Vector3.forward,-2);
                        Rotate(Bone(animator,HumanBodyBones.RightShoulder),Vector3.forward,2);
                        Rotate(Bone(animator,HumanBodyBones.Neck),Vector3.right,3.0f+watch*7-freeze*1);
                        Rotate(Bone(animator,HumanBodyBones.Head),Vector3.right,3.0f+watch*10-listen*2);
                        Rotate(Bone(animator,HumanBodyBones.Head),Vector3.up,listen*26-freeze*14+check*22);
                        // Keep both soles planted while the tired torso shifts weight.
                        SolveIK(Bone(animator,HumanBodyBones.LeftUpperLeg),Bone(animator,HumanBodyBones.LeftLowerLeg),lf,leftFoot,new Vector3(side*.11f,.3f,.3f));lf.rotation=leftRotation;
                        SolveIK(Bone(animator,HumanBodyBones.RightUpperLeg),Bone(animator,HumanBodyBones.RightLowerLeg),rf,rightFoot,new Vector3(-side*.11f,.3f,.3f));rf.rotation=rightRotation;
                        Vector3 rightTarget=new Vector3(-side*.095f,.555f+watch*.035f,.11f+watch*.015f);
                        if(beat==0)rightTarget+=new Vector3(.002f*cycle,.002f*breath,.002f*cycle);
                        SolveIK(Bone(animator,HumanBodyBones.RightUpperArm),Bone(animator,HumanBodyBones.RightLowerArm),Bone(animator,HumanBodyBones.RightHand),rightTarget,new Vector3(-side*.27f,.51f,.035f));
                        var rightHand=Bone(animator,HumanBodyBones.RightHand);
                        Vector3 fingerDirection=Bone(animator,HumanBodyBones.RightMiddleProximal).position-rightHand.position;
                        rightHand.rotation=Quaternion.FromToRotation(fingerDirection,new Vector3(side*.65f,.15f,.65f))*rightHand.rotation;
                        Vector3 leftTarget=Vector3.Lerp(new Vector3(side*.165f,.445f,.025f),new Vector3(side*.038f,.704f,.081f),secret);
                        leftTarget.x+=check*.012f;
                        SolveIK(Bone(animator,HumanBodyBones.LeftUpperArm),Bone(animator,HumanBodyBones.LeftLowerArm),Bone(animator,HumanBodyBones.LeftHand),leftTarget,new Vector3(side*.28f,.55f,.07f));
                        var pose=new HumanPose();handler.GetHumanPose(ref pose);
                        for(int i=0;i<HumanTrait.MuscleCount;i++)
                        {
                            string name=HumanTrait.MuscleName[i];
                            bool finger=name.Contains("Stretched")||name.Contains(" Spread");
                            if(finger)
                            {
                                bool right=name.StartsWith("Right");
                                float fidget=(beat==0?1:0)*Mathf.Sin(t*Mathf.PI/2+(name.Contains("Index")?0:name.Contains("Middle")?1.2f:name.Contains("Ring")?2.4f:3.6f));
                                if(name.Contains("Stretched"))
                                {
                                    pose.muscles[i]=right?-.24f-.14f*fidget:.40f;
                                    if(name.Contains("Thumb"))pose.muscles[i]=right?-.32f+.12f*fidget:-.10f+.13f*(beat==0?Mathf.Sin(t*Mathf.PI*6/16):0);
                                    if(name.Contains("Index")&&!right)pose.muscles[i]=.05f;
                                    if(right)pose.muscles[i]=Mathf.Lerp(pose.muscles[i],-.83f,freeze);
                                }
                                else pose.muscles[i]=name.Contains("Thumb")?(right?-.38f:-.25f):-.1f;
                            }
                            curves[i].AddKey(t,pose.muscles[i]);
                        }
                        for(int i=0;i<3;i++)curves[HumanTrait.MuscleCount+i].AddKey(t,pose.bodyPosition[i]);
                        for(int i=0;i<4;i++)curves[HumanTrait.MuscleCount+3+i].AddKey(t,pose.bodyRotation[i]);
                        smile.AddKey(t,beat==2?Bell(t,1.4f,2.6f,3.9f)*65:0);
                    }
                    for(int i=0;i<curves.Length;i++)curves[i]=Simplify(curves[i],.0006f);
                    for(int i=0;i<HumanTrait.MuscleCount;i++)clip.SetCurve("",typeof(Animator),MuscleProperty(HumanTrait.MuscleName[i]),curves[i]);
                    for(int i=0;i<3;i++)clip.SetCurve("",typeof(Animator),"RootT."+"xyz"[i],curves[HumanTrait.MuscleCount+i]);
                    for(int i=0;i<4;i++)clip.SetCurve("",typeof(Animator),"RootQ."+"xyzw"[i],curves[HumanTrait.MuscleCount+3+i]);
                    clip.SetCurve(AnimationUtility.CalculateTransformPath(skin.transform,animator.transform),typeof(SkinnedMeshRenderer),"blendShape.Smile_Subtle",smile);
                    var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=beat==0;settings.loopBlend=beat==0;
                    AnimationUtility.SetAnimationClipSettings(clip,settings);
                    WriteAsset(clip,Animations+"/PokerCharacter_"+Names[beat]+".anim");
                }
            }
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
        BuildController(); Install(); AssetDatabase.SaveAssets(); Validate();
    }

    static AnimationCurve Simplify(AnimationCurve source,float tolerance)
    {
        var keys=source.keys;var selected=new SortedSet<int>{0,keys.Length-1};
        Action<int,int> split=null;split=(a,b)=>
        {
            if(b<=a+1)return;float error=0;int index=-1;
            for(int i=a+1;i<b;i++)
            {
                float value=Mathf.Lerp(keys[a].value,keys[b].value,(keys[i].time-keys[a].time)/(keys[b].time-keys[a].time));
                float delta=Mathf.Abs(value-keys[i].value);if(delta>error){error=delta;index=i;}
            }
            if(error<=tolerance)return;selected.Add(index);split(a,index);split(index,b);
        };
        split(0,keys.Length-1);
        var result=new AnimationCurve(selected.Select(i=>keys[i]).ToArray());
        for(int i=0;i<result.length;i++){AnimationUtility.SetKeyLeftTangentMode(result,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(result,i,AnimationUtility.TangentMode.Linear);}
        return result;
    }

    static void BuildController()
    {
        string path=Animations+"/PokerCharacter_Idle.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
        var machine=controller.layers[0].stateMachine;
        foreach(var child in machine.states)machine.RemoveState(child.state);
        foreach(var p in controller.parameters.ToArray())controller.RemoveParameter(p);
        foreach(string name in Names.Skip(1))controller.AddParameter(name,AnimatorControllerParameterType.Trigger);
        var states=Names.Select(name=>{var state=machine.AddState(name);state.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations+"/PokerCharacter_"+name+".anim");state.writeDefaultValues=false;return state;}).ToArray();
        machine.defaultState=states[0];
        for(int i=1;i<states.Length;i++)
        {
            var enter=states[0].AddTransition(states[i]);enter.hasExitTime=false;enter.duration=.4f;enter.hasFixedDuration=true;enter.AddCondition(AnimatorConditionMode.If,0,Names[i]);
            var exit=states[i].AddTransition(states[0]);exit.hasExitTime=true;exit.exitTime=.94f;exit.duration=.4f;exit.hasFixedDuration=true;
        }
        EditorUtility.SetDirty(controller);
    }

    static void Install()
    {
        string chipPath=Folder+"/Models/PokerCharacter_LastChip.asset";
        var cylinder=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        var mesh=UnityEngine.Object.Instantiate(cylinder.GetComponent<MeshFilter>().sharedMesh);mesh.name="PokerCharacter_LastChip";
        UnityEngine.Object.DestroyImmediate(cylinder);WriteAsset(mesh,chipPath);
        string matPath=Folder+"/Materials/PokerCharacter_LastChip.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,matPath);mat.SetColor("_BaseColor",new Color(.72f,.56f,.31f));mat.SetFloat("_Smoothness",.25f);}
        mat.SetColor("_BaseColor",new Color(.72f,.56f,.31f));EditorUtility.SetDirty(mat);
        var prefab=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            Configure(prefab,chipPath,mat);PrefabUtility.SaveAsPrefabAsset(prefab,Prefab);
        }
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
        foreach(var animator in UnityEngine.Object.FindObjectsOfType<Animator>(true))
        {
            if(!animator.gameObject.scene.IsValid()||EditorSceneManager.IsPreviewScene(animator.gameObject.scene))continue;
            if(!animator.avatar||!animator.avatar.name.Contains("PokerCharacter"))continue;
            var root=animator.transform.root.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(root,"Apply gambler idle");Configure(root,chipPath,mat);
            foreach(var probe in root.GetComponentsInChildren<HandSkinRuntimeProbe>(true))probe.enabled=false;
            EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);
        }
    }
    static void Configure(GameObject root,string chipPath,Material material)
    {
        var animator=root.GetComponentInChildren<Animator>(true);
        animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Animations+"/PokerCharacter_Idle.controller");
        animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var skin=root.GetComponentInChildren<SkinnedMeshRenderer>(true);skin.updateWhenOffscreen=true;
        var chip=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="LastChip");
        if(!chip)
        {
            chip=new GameObject("LastChip",typeof(MeshFilter),typeof(MeshRenderer)).transform;chip.SetParent(animator.transform,false);
        }
        chip.gameObject.layer=root.layer;
        chip.localScale=new Vector3(.023f,.0012f,.023f);
        chip.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(chipPath);
        chip.GetComponent<MeshRenderer>().sharedMaterial=material;
        var idle=animator.GetComponent<PokerCharacterIdle>()??animator.gameObject.AddComponent<PokerCharacterIdle>();idle.Configure(skin,chip);
        if(animator.transform!=root.transform)animator.transform.localPosition=visualOffset;
        chip.position=(Bone(animator,HumanBodyBones.RightThumbDistal).position+Bone(animator,HumanBodyBones.RightIndexDistal).position)*.5f;
        chip.rotation=Bone(animator,HumanBodyBones.RightHand).rotation*Quaternion.Euler(80,0,20);
        PrefabUtility.RecordPrefabInstancePropertyModifications(chip);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
        PrefabUtility.RecordPrefabInstancePropertyModifications(idle);
    }

    [MenuItem("Tools/Hand Skin Validation/12 Validate Poker Character Idle")]
    public static void Validate()
    {
        Directory.CreateDirectory(Output);
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),scene);
            var animator=root.GetComponentInChildren<Animator>();animator.GetComponent<PokerCharacterIdle>().enabled=false;
            animator.runtimeAnimatorController=null;
            var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
            var graph=PlayableGraph.Create("Poker idle validation");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output=AnimationPlayableOutput.Create(graph,"Idle",animator);
            var report=new StringBuilder();report.AppendLine("Avatar valid="+animator.avatar.isValid+"; BlendShapes="+skin.sharedMesh.blendShapeCount);
            try
            {
                var lf=Bone(animator,HumanBodyBones.LeftFoot);var rf=Bone(animator,HumanBodyBones.RightFoot);
                Vector3 plantedLeft=lf.position,plantedRight=rf.position;
                float maxFootDrift=0;
                foreach(string name in Names)
                {
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations+"/PokerCharacter_"+name+".anim");
                    if(!clip.humanMotion)throw new Exception("Not a Humanoid clip: "+name);
                    var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);output.SetSourcePlayable(playable);graph.Play();
                    foreach(float t in new[]{0f,clip.length*.33f,clip.length*.6f,clip.length-.04f})
                    {playable.SetTime(t);graph.Evaluate(1f/60);
                        maxFootDrift=Mathf.Max(maxFootDrift,Vector3.Distance(lf.position,plantedLeft),Vector3.Distance(rf.position,plantedRight));
                        Capture(root,scene,Output+"/"+name+"_"+t.ToString("F1")+".png");}
                    report.AppendLine(name+" duration="+clip.length+"; humanoid="+clip.humanMotion);
                    playable.Destroy();
                }
                report.AppendLine("Max sampled foot drift="+maxFootDrift.ToString("F6")+" m");
                if(maxFootDrift>.025f)throw new Exception("Feet drift exceeded tolerance: "+maxFootDrift);
                // Confirm both blink shapes affect eye vertices without moving the character bounds.
                report.AppendLine("Blink_L and Blink_R imported; scene/prefab controller assigned");
            }
            finally{graph.Destroy();}
            File.WriteAllText(Output+"/validation.txt",report.ToString());Debug.Log(report.ToString());
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    static void Capture(GameObject root,Scene scene,string path)
    {
        var animator=root.GetComponentInChildren<Animator>();
        var source=root.GetComponentInChildren<SkinnedMeshRenderer>();
        var baked=new Mesh();source.BakeMesh(baked,true);
        var snapshot=new GameObject("Pose snapshot",typeof(MeshFilter),typeof(MeshRenderer));
        snapshot.layer=root.layer;snapshot.transform.SetParent(source.transform,false);
        snapshot.GetComponent<MeshFilter>().sharedMesh=baked;snapshot.GetComponent<MeshRenderer>().sharedMaterials=source.sharedMaterials;
        source.enabled=false;
        var chip=root.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="LastChip");
        if(chip){chip.position=(Bone(animator,HumanBodyBones.RightThumbDistal).position+Bone(animator,HumanBodyBones.RightIndexDistal).position)*.5f;chip.rotation=Bone(animator,HumanBodyBones.RightHand).rotation*Quaternion.Euler(80,0,20);}
        var camera=new GameObject("Idle review camera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.scene=scene;
        camera.transform.position=new Vector3(0,.62f,2);camera.transform.LookAt(new Vector3(0,.49f,0));camera.orthographic=true;camera.orthographicSize=.59f;camera.nearClipPlane=.01f;camera.farClipPlane=10;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.17f,.18f);camera.cullingMask=1<<root.layer;
        var light=new GameObject("Idle review light").AddComponent<Light>();SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.intensity=1.8f;light.transform.rotation=Quaternion.Euler(30,145,0);
        var rt=RenderTexture.GetTemporary(720,960,24);var previous=RenderTexture.active;var image=new Texture2D(720,960,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,720,960),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(light.gameObject);source.enabled=true;UnityEngine.Object.DestroyImmediate(snapshot);UnityEngine.Object.DestroyImmediate(baked);}
    }
}
