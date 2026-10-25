using UnityEngine;

// Direct capsule contact only. Preserve the original panel skin and wind motion whenever
// there is no overlap; limit contact response to small rotations of the existing rig.
internal sealed class PalamedesPanelLegContact
{
    readonly PalamedesSecondaryBoneSway owner;
    struct Capsule { public Vector3 a,b,min,max; public float radius; }
    readonly Vector3[] vertices,skinned;
    readonly BoneWeight[] weights;
    readonly int[] triangles;
    readonly Transform[] skinBones;
    readonly Matrix4x4[] bindposes,matrices;
    readonly float[] correction;
    readonly bool[] contactSamples;
    readonly Capsule[] capsules;
    int capsuleCount;
    Vector3 contactAxis;
    public float Penetration { get; private set; }
    public float TotalBend { get; private set; }
    public Vector3 WorstPoint { get; private set; }
    static readonly Unity.Profiling.ProfilerMarker Marker=new Unity.Profiling.ProfilerMarker("Palamedes.SmallPanelLegContact");

    public PalamedesPanelLegContact(PalamedesSecondaryBoneSway owner)
    {
        this.owner=owner;var mesh=owner.collisionRenderer.sharedMesh;
        bindposes=mesh.bindposes;skinBones=owner.collisionRenderer.bones;matrices=new Matrix4x4[skinBones.Length];correction=new float[owner.bones.Length];
        capsules=new Capsule[owner.legColliders.Length];
        vertices=mesh.vertices;weights=mesh.boneWeights;triangles=mesh.triangles;
        skinned=new Vector3[vertices.Length];
        float top=float.NegativeInfinity,bottom=float.PositiveInfinity;
        foreach(var vertex in vertices){top=Mathf.Max(top,vertex.y);bottom=Mathf.Min(bottom,vertex.y);}
        float contactTop=top-(top-bottom)*.15f;
        contactSamples=new bool[vertices.Length+triangles.Length/3];
        for(int i=0;i<vertices.Length;i++)contactSamples[i]=vertices[i].y<contactTop;
        for(int i=0;i<triangles.Length;i+=3)contactSamples[vertices.Length+i/3]=(vertices[triangles[i]].y+vertices[triangles[i+1]].y+vertices[triangles[i+2]].y)/3<contactTop;
    }
    public void Solve(float dt,float[] windAngles,Vector3 axis)
    {
        if(owner.bones.Length!=3||bindposes.Length!=skinBones.Length)return;
        using(Marker.Auto())
        {
            contactAxis=owner.bones[0].parent.rotation*owner.restLocalRotations[0]*owner.localSwingAxis.normalized;
            UpdateCapsules();
            for(int i=0;i<correction.Length;i++)correction[i]=Mathf.MoveTowards(correction[i],0,owner.contactReturnSpeed*dt);
            // Test the original wind pose, not a projected exclusion zone. A dead band
            // prevents small proxy/skin differences from disturbing otherwise good motion.
            float c0=correction[0],c1=correction[1],c2=correction[2];
            correction[0]=correction[1]=correction[2]=0;Pose(windAngles,axis);
            Error(float.PositiveInfinity,out float rawDepth);
            correction[0]=c0;correction[1]=c1;correction[2]=c2;
            Pose(windAngles,axis);
            for(int pass=0;rawDepth>owner.contactActivationDepth&&pass<8;pass++)
            {
                float best=Error(float.PositiveInfinity,out float depth);if(depth<=.001f)break;
                int joint=-1;float chosen=0;
                for(int j=0;j<correction.Length;j++)
                {
                    float original=correction[j];float limit=j==0?owner.maximumAnchorContactBend:owner.maximumContactBend;
                    for(int step=0;step<6;step++)
                    {
                        float amount=step%3==0?.5f:step%3==1?2:6;
                        float candidate=Mathf.Clamp(original+amount*(step<3?1:-1),-limit*.5f,limit);
                        if(candidate==original)continue;
                        float total=Mathf.Abs(candidate);for(int k=0;k<correction.Length;k++)if(k!=j)total+=Mathf.Abs(correction[k]);
                        if(total>22)continue;
                        correction[j]=candidate;Pose(windAngles,axis);
                        float error=Error(best,out _);
                        if(error<best-1e-10f){best=error;joint=j;chosen=candidate;}
                    }
                    correction[j]=original;
                }
                if(joint<0){Pose(windAngles,axis);break;}
                correction[joint]=chosen;Pose(windAngles,axis);
            }
            Error(float.PositiveInfinity,out float finalDepth);Penetration=finalDepth;TotalBend=Mathf.Abs(correction[0])+Mathf.Abs(correction[1])+Mathf.Abs(correction[2]);
        }
    }
    void Pose(float[] angles,Vector3 axis)
    {
        // The waist pivot position stays fixed. Contact can rotate the original pinned
        // section as one piece, rather than stretching it or moving the waistband sideways.
        for(int i=0;i<owner.bones.Length;i++)
        {
            var bone=owner.bones[i];var rest=owner.restLocalRotations[i];
            var inverse=Quaternion.Inverse(bone.parent.rotation*rest);
            bone.localRotation=rest*Quaternion.AngleAxis(i==0?0:angles[i],inverse*axis)*Quaternion.AngleAxis(correction[i],inverse*contactAxis);
        }
        for(int i=0;i<matrices.Length;i++)matrices[i]=skinBones[i].localToWorldMatrix*bindposes[i];
    }
    void UpdateCapsules()
    {
        capsuleCount=0;
        foreach(var collider in owner.legColliders)
        {
            if(!collider)continue;
            var scale=collider.transform.lossyScale;var direction=collider.direction==0?Vector3.right:collider.direction==1?Vector3.up:Vector3.forward;
            float radial=collider.direction==0?Mathf.Max(Mathf.Abs(scale.y),Mathf.Abs(scale.z)):collider.direction==1?Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z)):Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y));
            float radius=collider.radius*radial;float half=Mathf.Max(0,collider.height*Mathf.Abs(scale[collider.direction])*.5f-radius);
            var center=collider.transform.TransformPoint(collider.center);var axis=collider.transform.TransformDirection(direction).normalized;
            var a=center-axis*half;var b=center+axis*half;radius+=owner.collisionClearance;
            capsules[capsuleCount++]=new Capsule{a=a,b=b,radius=radius,min=Vector3.Min(a,b)-Vector3.one*radius,max=Vector3.Max(a,b)+Vector3.one*radius};
        }
    }
    float Error(float cutoff,out float deepest)
    {
        float error=0;deepest=0;
        for(int i=0;i<vertices.Length;i++)
        {
            var w=weights[i];var v=vertices[i];var p=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0;
            if(w.weight1>0)p+=matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1;
            if(w.weight2>0)p+=matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2;
            if(w.weight3>0)p+=matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
            skinned[i]=p;
        }
        for(int i=0;i<vertices.Length+triangles.Length/3;i++)
        {
            // The waist seam is above the leg-contact region; rounded thigh caps near
            // the hip must not change the established idle/flying silhouette.
            if(!contactSamples[i])continue;
            Vector3 p;
            if(i<vertices.Length)p=skinned[i];
            else{int triangle=(i-vertices.Length)*3;p=(skinned[triangles[triangle]]+skinned[triangles[triangle+1]]+skinned[triangles[triangle+2]])/3;}
            float depth=0;
            for(int c=0;c<capsuleCount;c++)
            {
                var capsule=capsules[c];if(p.x<capsule.min.x||p.x>capsule.max.x||p.y<capsule.min.y||p.y>capsule.max.y||p.z<capsule.min.z||p.z>capsule.max.z)continue;
                var segment=capsule.b-capsule.a;float t=Mathf.Clamp01(Vector3.Dot(p-capsule.a,segment)/Mathf.Max(1e-12f,segment.sqrMagnitude));
                float distance2=(p-(capsule.a+segment*t)).sqrMagnitude;
                if(distance2<capsule.radius*capsule.radius)depth=Mathf.Max(depth,capsule.radius-Mathf.Sqrt(distance2));
            }
            if(depth>deepest){deepest=depth;if(float.IsPositiveInfinity(cutoff))WorstPoint=owner.characterRoot.InverseTransformPoint(p);}
            error+=depth*depth;
            if(error>=cutoff)return error;
        }
        return error;
    }
}
