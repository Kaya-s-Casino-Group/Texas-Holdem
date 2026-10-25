using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Editor-owned, unsaved duplicate. The source Animator and bone transforms are never sampled.
internal sealed class PalamedesSceneAnimationPreview : IDisposable
{
    public GameObject Root { get; private set; }
    public Animator Animator { get; private set; }
    public AnimationClip Clip { get; private set; }
    public float Time { get; private set; }
    public float SimulationTime { get; private set; }
    public bool Dynamics = true;
    public bool FootIK;
    public readonly Animator Source;
    PlayableGraph graph;
    AnimationClipPlayable playable;
    readonly List<(Renderer renderer, bool hidden)> originals = new List<(Renderer, bool)>();
    struct Pose { public Transform transform; public Vector3 position, scale; public Quaternion rotation; }
    Pose[] rest;
    PalamedesCapeBoneSway cape;
    PalamedesSecondaryBoneSway[] secondary;

    public PalamedesSceneAnimationPreview(Animator source)
    {
        if(!source || !source.avatar || !source.avatar.isValid || !source.avatar.isHuman)
            throw new ArgumentException("目标角色需要有效的 Humanoid Avatar。");
        // Fail before hiding the real character if a saved skin has lost its rig references.
        foreach(var renderer in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if(!renderer.sharedMesh || renderer.sharedMesh.bindposes.Length==0)continue;
            var skinBones=renderer.bones;
            if(skinBones.Length<renderer.sharedMesh.bindposes.Length || skinBones.Any(b=>!b))
                throw new InvalidOperationException(renderer.name+" 的蒙皮骨骼引用不完整，请修复绑定后再预览。");
        }
        Source=source;
        try
        {
            Root=UnityEngine.Object.Instantiate(source.gameObject,source.transform.parent);
            Root.name="Palamedes Scene Animation Preview (temporary)";
            foreach(var t in Root.GetComponentsInChildren<Transform>(true))t.gameObject.hideFlags=HideFlags.HideAndDontSave;
            Root.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
            Root.transform.localScale=source.transform.localScale;
            // Prevent unrelated scripts and physics components from participating in preview.
            foreach(var c in Root.GetComponentsInChildren<MonoBehaviour>(true))c.enabled=false;
            foreach(var c in Root.GetComponentsInChildren<Collider>(true))c.enabled=false;
            foreach(var c in Root.GetComponentsInChildren<AudioSource>(true))c.enabled=false;
            Animator=Root.GetComponent<Animator>();
            Animator.runtimeAnimatorController=null;
            Animator.enabled=true;
            Animator.applyRootMotion=false;
            Animator.fireEvents=false;
            Animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            foreach(var r in Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))r.updateWhenOffscreen=true;
            cape=Root.GetComponent<PalamedesCapeBoneSway>();
            secondary=Root.GetComponents<PalamedesSecondaryBoneSway>();
            // These behaviours are not ExecuteAlways; explicit fixed steps drive them in edit mode.
            if(cape)cape.enabled=true;
            foreach(var c in secondary)c.enabled=true;
            rest=Root.GetComponentsInChildren<Transform>(true).Select(t=>new Pose{transform=t,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
            foreach(var r in source.GetComponentsInChildren<Renderer>(true))
            {originals.Add((r,r.forceRenderingOff));r.forceRenderingOff=true;}
        }
        catch { Dispose(); throw; }
    }

    public void SetClip(AnimationClip clip)
    {
        if(!clip || !clip.isHumanMotion)throw new ArgumentException("请选择 Humanoid 动画。");
        if(graph.IsValid())graph.Destroy();
        RestorePose();
        Clip=clip;
        graph=PlayableGraph.Create("Palamedes Scene Preview");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        playable=AnimationClipPlayable.Create(graph,clip);
        playable.SetSpeed(0);
        playable.SetApplyFootIK(FootIK);
        playable.SetApplyPlayableIK(false);
        var output=AnimationPlayableOutput.Create(graph,"Humanoid",Animator);
        output.SetSourcePlayable(playable);
        graph.Play();
        Sample(0,0,true);
    }

    public void Sample(float time,float deltaTime,bool resetDynamics)
    {
        if(!graph.IsValid() || !Clip || !Root)return;
        if(resetDynamics)RestorePose();
        Time=Mathf.Clamp(time,0,Clip.length);
        playable.SetApplyFootIK(FootIK);
        playable.SetTime(Time);
        graph.Evaluate(deltaTime);
        // Preview in place; translation within the Humanoid pose (e.g. crouching) remains.
        var rootPose=rest[0];
        Root.transform.localPosition=rootPose.position;
        Root.transform.localRotation=rootPose.rotation;
        if(resetDynamics)
        {
            SimulationTime=0;
            if(cape)cape.ResetSimulation();
            foreach(var c in secondary)if(c)c.ResetSimulation();
        }
        if(Dynamics && deltaTime>0)
        {
            SimulationTime+=deltaTime;
            if(cape)cape.Simulate(deltaTime,SimulationTime);
            foreach(var c in secondary)if(c)c.Simulate(deltaTime);
        }
    }

    void RestorePose()
    {
        if(rest==null)return;
        foreach(var p in rest)if(p.transform)
        {p.transform.localPosition=p.position;p.transform.localRotation=p.rotation;p.transform.localScale=p.scale;}
    }

    public void Dispose()
    {
        if(graph.IsValid())graph.Destroy();
        foreach(var pair in originals)if(pair.renderer)pair.renderer.forceRenderingOff=pair.hidden;
        originals.Clear();
        if(Root)UnityEngine.Object.DestroyImmediate(Root);
        Root=null;
    }
}
