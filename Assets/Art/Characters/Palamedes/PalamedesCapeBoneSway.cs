using System;
using System.Collections.Generic;
using UnityEngine;

// A folded cloth cage drives ordinary skin bones. Contacts move nearby particles,
// rather than exciting an angular spring shared by the entire cape.
[DefaultExecutionOrder(100), DisallowMultipleComponent]
public sealed class PalamedesCapeBoneSway : MonoBehaviour
{
    public Transform characterRoot, motionSource;
    public SkinnedMeshRenderer capeRenderer;
    public CapsuleCollider[] bodyColliders;
    public Transform[] bones;
    public Quaternion[] restLocalRotations, restRotationsInAttachment;
    public Vector3[] restLocalPositions, restInAttachment,restInRoot;
    [Min(2)] public int columns=3,rows=7;
    [Min(0)] public float shapeFrequency=1.2f,drag=4,gravity=2;
    [Range(0,1)] public float bendStiffness=.25f,foldStiffness=.85f;
    [Range(2,16)] public int solverIterations=8;
    [Range(0,.02f)] public float bodyClearance=.003f;
    [Range(0,.01f)] public float contactSlop=.001f;
    [Range(0,1)] public float contactFriction=.2f;
    [Min(.1f)] public float maximumSpeed=1.5f;
    [Tooltip("Small rearward lift of the hanging rest pose; the attachment stays fixed.")]
    [Range(0,12)] public float restingBackLift=4;
    public Vector3 localWindDirection=Vector3.back;
    public float CurrentWindSpeed=>0;
    public float LastBodyPenetration {get;private set;}
    public float MaximumParticleSpeed {get;private set;}
    Vector3[] positions,velocities,targets,startPositions,contactNormals,contactOffsets;
    float[] contactCounts;
    Vector3 lastAttachmentPosition;
    Quaternion lastAttachmentRotation;
    struct Edge {public int a,b;public float length,stiffness;}
    Edge[] edges;
    struct SurfaceSample {public Vector3 vertex;public BoneWeight weights;}
    SurfaceSample[] samples;
    Transform[] skinBones;
    Matrix4x4[] bindposes,skinMatrices;
    int[] particleForSkinBone;
    struct Capsule {public Vector3 center,axis,min,max;public float half,radius;}
    Capsule[] capsules,oldCapsules,activeCapsules;
    int capsuleCount;
    bool ready;
    static readonly Unity.Profiling.ProfilerMarker Marker=new Unity.Profiling.ProfilerMarker("Palamedes.CapeClothCage");
    void OnEnable()=>ResetSimulation();
    public void ResetSimulation()
    {
        ready=false;LastBodyPenetration=MaximumParticleSpeed=0;int count=columns*rows;
        if(!motionSource||!capeRenderer||!capeRenderer.sharedMesh||bones==null||bones.Length!=count||restInAttachment==null||restInAttachment.Length!=count||restRotationsInAttachment==null||restRotationsInAttachment.Length!=count)return;
        positions=new Vector3[count];velocities=new Vector3[count];targets=new Vector3[count];startPositions=new Vector3[count];contactNormals=new Vector3[count];contactOffsets=new Vector3[count];contactCounts=new float[count];
        for(int i=0;i<count;i++)positions[i]=targets[i]=motionSource.TransformPoint(restInAttachment[i]);
        var list=new List<Edge>();
        void Add(int a,int b,float stiffness){list.Add(new Edge{a=a,b=b,length=Vector3.Distance(positions[a],positions[b]),stiffness=stiffness});}
        for(int r=0;r<rows;r++)for(int c=0;c<columns;c++)
        {
            int i=r*columns+c;
            if(r+1<rows)Add(i,i+columns,1);
            if(c+1<columns)Add(i,i+1,foldStiffness);
            if(r+1<rows&&c+1<columns){Add(i,i+columns+1,foldStiffness*.12f);Add(i+1,i+columns,foldStiffness*.12f);}
            if(r+2<rows)Add(i,i+2*columns,bendStiffness);
            if(c+2<columns)Add(i,i+2,foldStiffness);
        }
        edges=list.ToArray();var mesh=capeRenderer.sharedMesh;skinBones=capeRenderer.bones;bindposes=mesh.bindposes;
        if(bindposes.Length!=skinBones.Length)return;
        particleForSkinBone=new int[skinBones.Length];skinMatrices=new Matrix4x4[skinBones.Length];
        for(int i=0;i<skinBones.Length;i++)particleForSkinBone[i]=Array.IndexOf(bones,skinBones[i]);
        var vertices=mesh.vertices;var weights=mesh.boneWeights;var surface=new List<SurfaceSample>();
        for(int i=0;i<vertices.Length;i+=2)
        {
            var w=weights[i];float Free(int b,float f)=>particleForSkinBone[b]>=columns?f:0;
            float free=Free(w.boneIndex0,w.weight0)+Free(w.boneIndex1,w.weight1)+Free(w.boneIndex2,w.weight2)+Free(w.boneIndex3,w.weight3);
            if(free>.5f)surface.Add(new SurfaceSample{vertex=vertices[i],weights=w});
        }
        samples=surface.ToArray();capsules=new Capsule[bodyColliders==null?0:bodyColliders.Length];oldCapsules=new Capsule[capsules.Length];activeCapsules=new Capsule[capsules.Length];
        ReadCapsules();Array.Copy(capsules,oldCapsules,capsules.Length);
        lastAttachmentPosition=motionSource.position;lastAttachmentRotation=motionSource.rotation;ready=true;PreserveFoldSections();Pose();
    }
    void LateUpdate()=>Simulate(Time.deltaTime,Time.time);
    public void Simulate(float dt,float time)
    {
        if(!ready||bones==null||positions.Length!=bones.Length||capsules.Length!=(bodyColliders==null?0:bodyColliders.Length))ResetSimulation();
        if(!ready||dt<=0)return;
        using(Marker.Auto())
        {
            if((motionSource.position-lastAttachmentPosition).sqrMagnitude>1||dt>.25f){ResetSimulation();return;}
            ReadCapsules();int steps=Mathf.Clamp(Mathf.CeilToInt(dt*120),1,12);float h=Mathf.Min(dt,.1f)/steps;
            var nextPosition=motionSource.position;var nextRotation=motionSource.rotation;
            for(int step=0;step<steps;step++)
            {
                float t=(step+1f)/steps;
                var attachmentPosition=Vector3.Lerp(lastAttachmentPosition,nextPosition,t);var attachmentRotation=Quaternion.Slerp(lastAttachmentRotation,nextRotation,t);
                for(int i=0;i<bones.Length;i++)
                {
                    var offset=Quaternion.Inverse(nextRotation)*(motionSource.TransformPoint(restInAttachment[i])-nextPosition);
                    targets[i]=attachmentPosition+attachmentRotation*offset;startPositions[i]=positions[i];contactNormals[i]=Vector3.zero;
                    if(i<columns){positions[i]=targets[i];velocities[i]=Vector3.zero;continue;}
                    if(restInRoot!=null&&restInRoot.Length==bones.Length)
                    {
                        int anchor=i%columns;
                        var offsetFromTop=Quaternion.AngleAxis(restingBackLift,Vector3.right)*(restInRoot[i]-restInRoot[anchor]);
                        var hanging=targets[anchor]+characterRoot.TransformVector(offsetFromTop);
                        targets[i]=Vector3.Lerp(targets[i],hanging,Mathf.Clamp01((float)(i/columns)/2));
                    }
                    var velocity=velocities[i]*Mathf.Exp(-drag*h);
                    float along=(float)(i/columns)/(rows-1);float omega=2*Mathf.PI*shapeFrequency;
                    var acceleration=(targets[i]-positions[i])*(omega*omega*Mathf.Lerp(1,.22f,along))+Vector3.down*gravity;
                    positions[i]+=velocity*h+acceleration*h*h;
                }
                for(int iteration=0;iteration<solverIterations;iteration++)
                {
                    foreach(var edge in edges)
                    {
                        var delta=positions[edge.b]-positions[edge.a];float length=delta.magnitude;if(length<1e-6f)continue;
                        float wa=edge.a<columns?0:1,wb=edge.b<columns?0:1;if(wa+wb==0)continue;
                        var correction=delta*((length-edge.length)/length*edge.stiffness/(wa+wb));positions[edge.a]+=correction*wa;positions[edge.b]-=correction*wb;
                    }
                    if(iteration==solverIterations/2||iteration==solverIterations-1){Pose();Contact(t);}
                    PreserveFoldSections();
                }
                MaximumParticleSpeed=0;
                for(int i=columns;i<positions.Length;i++)
                {
                    var velocity=(positions[i]-startPositions[i])/h;
                    if(contactNormals[i].sqrMagnitude>1e-10f)
                    {
                        // Inelastic contact removes normal velocity, including energy
                        // introduced by positional correction of moving body capsules.
                        velocity=Vector3.ProjectOnPlane(velocity,contactNormals[i].normalized)*(1-contactFriction);
                    }
                    velocity=Vector3.ClampMagnitude(velocity,maximumSpeed);MaximumParticleSpeed=Mathf.Max(MaximumParticleSpeed,velocity.magnitude);
                    velocities[i]=velocity;
                }
            }
            lastAttachmentPosition=nextPosition;lastAttachmentRotation=nextRotation;Array.Copy(capsules,oldCapsules,capsules.Length);Pose();
        }
    }
    void Pose()
    {
        for(int i=0;i<bones.Length;i++)
        {
            if(!bones[i])continue;
            // Translational cage skinning preserves the authored cross-section.
            // Rotating a section toward its neighbour would move offset fold vertices
            // a second time, invalidating the local contact projection.
            var rotation=i<columns?motionSource.rotation*restRotationsInAttachment[i]:characterRoot.rotation;
            bones[i].SetPositionAndRotation(positions[i],rotation);
        }
    }
    void PreserveFoldSections()
    {
        if(restInRoot==null||restInRoot.Length!=positions.Length)return;
        // A folded cross-section moves together. Independent contact offsets on
        // its columns can invert the narrow folds even when wall weights match.
        for(int row=1;row<rows;row++)
        {
            int first=row*columns;var displacement=Vector3.zero;
            for(int c=0;c<columns;c++)displacement+=positions[first+c]-characterRoot.TransformPoint(restInRoot[first+c]);
            displacement/=columns;
            for(int c=0;c<columns;c++)positions[first+c]=characterRoot.TransformPoint(restInRoot[first+c])+displacement;
        }
        // Keep consecutive strips in order while permitting the hem to lift.
        var down=-characterRoot.up;
        for(int row=1;row<rows;row++)
        {
            int first=row*columns,previous=first-columns;var center=Vector3.zero;var above=Vector3.zero;
            for(int c=0;c<columns;c++){center+=positions[first+c];above+=positions[previous+c];}
            center/=columns;above/=columns;
            float minimum=(restInRoot[previous].y-restInRoot[first].y)*.35f*characterRoot.lossyScale.y;
            float deficit=minimum-Vector3.Dot(center-above,down);
            if(deficit>0)for(int c=0;c<columns;c++)positions[first+c]+=down*deficit;
        }
    }
    void ReadCapsules()
    {
        capsuleCount=0;if(bodyColliders==null)return;
        foreach(var c in bodyColliders)
        {
            if(!c)continue;var scale=c.transform.lossyScale;int d=c.direction;
            float radial=d==0?Mathf.Max(Mathf.Abs(scale.y),Mathf.Abs(scale.z)):d==1?Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z)):Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y));
            capsules[capsuleCount++]=new Capsule{center=c.transform.TransformPoint(c.center),axis=c.transform.TransformDirection(d==0?Vector3.right:d==1?Vector3.up:Vector3.forward).normalized,half=Mathf.Max(0,c.height*Mathf.Abs(scale[d])*.5f-c.radius*radial),radius=c.radius*radial+bodyClearance};
        }
    }
    void Contact(float time)
    {
        LastBodyPenetration=0;if(capsuleCount==0)return;
        Array.Clear(contactOffsets,0,contactOffsets.Length);Array.Clear(contactCounts,0,contactCounts.Length);
        for(int c=0;c<capsuleCount;c++)
        {
            var shape=capsules[c];shape.center=Vector3.Lerp(oldCapsules[c].center,shape.center,time);shape.axis=Vector3.Slerp(oldCapsules[c].axis,shape.axis,time);
            var extent=new Vector3(Mathf.Abs(shape.axis.x),Mathf.Abs(shape.axis.y),Mathf.Abs(shape.axis.z))*shape.half+Vector3.one*shape.radius;shape.min=shape.center-extent;shape.max=shape.center+extent;activeCapsules[c]=shape;
        }
        for(int i=0;i<skinBones.Length;i++)skinMatrices[i]=skinBones[i].localToWorldMatrix*bindposes[i];
        foreach(var sample in samples)
        {
            var w=sample.weights;var v=sample.vertex;var point=skinMatrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0;
            if(w.weight1>0)point+=skinMatrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1;
            if(w.weight2>0)point+=skinMatrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2;
            if(w.weight3>0)point+=skinMatrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
            Vector3 correction=Vector3.zero;
            for(int c=0;c<capsuleCount;c++)
            {
                var shape=activeCapsules[c];var p=point+correction;
                if(p.x<shape.min.x||p.x>shape.max.x||p.y<shape.min.y||p.y>shape.max.y||p.z<shape.min.z||p.z>shape.max.z)continue;
                var nearest=shape.center+shape.axis*Mathf.Clamp(Vector3.Dot(p-shape.center,shape.axis),-shape.half,shape.half);var delta=p-nearest;float distance=delta.magnitude;
                float depth=shape.radius-distance;LastBodyPenetration=Mathf.Max(LastBodyPenetration,depth);if(depth<=contactSlop)continue;
                var normal=characterRoot.TransformDirection(Vector3.back);
                // Preserve the side of the body occupied by the cape. A moving foot
                // can otherwise overtake a sample and expel it through the front.
                float along=Vector3.Dot(delta,normal);
                depth=-along+Mathf.Sqrt(Mathf.Max(0,along*along+shape.radius*shape.radius-distance*distance));
                correction+=normal*Mathf.Max(0,depth-contactSlop);
            }
            if(correction.sqrMagnitude<1e-12f)continue;float denominator=0;
            void Sum(int b,float weight){if(particleForSkinBone[b]>=columns)denominator+=weight*weight;}
            Sum(w.boneIndex0,w.weight0);Sum(w.boneIndex1,w.weight1);Sum(w.boneIndex2,w.weight2);Sum(w.boneIndex3,w.weight3);
            void Push(int b,float weight){int p=particleForSkinBone[b];if(p<columns||weight<=0)return;var push=correction*weight/Mathf.Max(.01f,denominator);if(push.sqrMagnitude>contactOffsets[p].sqrMagnitude){contactOffsets[p]=push;contactCounts[p]=1;}contactNormals[p]+=correction.normalized*weight;}
            Push(w.boneIndex0,w.weight0);Push(w.boneIndex1,w.weight1);Push(w.boneIndex2,w.weight2);Push(w.boneIndex3,w.weight3);
        }
        for(int row=1;row<rows;row++)
        {
            int first=row*columns;var push=Vector3.zero;var normal=Vector3.zero;
            for(int c=0;c<columns;c++)
            {
                int i=first+c;if(contactCounts[i]>0&&contactOffsets[i].sqrMagnitude>push.sqrMagnitude)push=contactOffsets[i];
                normal+=contactNormals[i];
            }
            push=Vector3.ClampMagnitude(push,.012f);
            for(int c=0;c<columns;c++){positions[first+c]+=push;contactNormals[first+c]=normal;}
        }
    }
    void OnDisable()
    {
        if(bones==null)return;
        for(int i=0;i<bones.Length;i++)if(bones[i])
        {
            if(restLocalPositions!=null&&i<restLocalPositions.Length)bones[i].localPosition=restLocalPositions[i];
            if(restLocalRotations!=null&&i<restLocalRotations.Length)bones[i].localRotation=restLocalRotations[i];
        }
        ready=false;
    }
}
