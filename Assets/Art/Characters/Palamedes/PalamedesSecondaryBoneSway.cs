using UnityEngine;

[DefaultExecutionOrder(110)]
public sealed class PalamedesSecondaryBoneSway : MonoBehaviour
{
    public string partName;
    public Transform characterRoot;
    public Transform motionSource;
    public PalamedesCapeBoneSway windSource;
    public Transform[] bones;
    public Quaternion[] restLocalRotations;
    public Vector3 localSwingAxis = Vector3.right;
    [Range(0,15)] public float maximumBend = 6;
    [Min(0)] public float windResponse = .22f;
    [Tooltip("Catch the shared gust strength from either side while keeping the configured outward swing direction.")]
    public bool catchWindFromEitherSide;
    [Tooltip("Use the pinned attachment orientation for the swing plane, so turning the pelvis also turns panel contact.")]
    public bool followAttachmentRotation;
    public Transform SwingFrame => followAttachmentRotation&&bones!=null&&bones.Length>0&&bones[0]?bones[0]:characterRoot;
    [Min(0)] public float movementResponse = .3f;
    [Min(0)] public float rotationResponse = .025f;
    [Min(.05f)] public float dampingTime = .3f;
    [Header("Optional front panel leg contact")]
    public SkinnedMeshRenderer collisionRenderer;
    public CapsuleCollider[] legColliders;
    [Min(0)] public float collisionClearance = .004f;
    [Tooltip("Ignore shallow capsule overlap, preserving the original motion until there is a definite contact.")]
    [Min(0)] public float contactActivationDepth = .006f;
    [Range(0,30)] public float maximumContactBend = 12;
    [Range(0,15)] public float maximumAnchorContactBend = 10;
    [Min(1)] public float contactReturnSpeed = 25;
    public float LastLegPenetration => legContact == null ? 0 : legContact.Penetration;
    public float CurrentContactBend => legContact == null ? 0 : legContact.TotalBend;
    public Vector3 WorstContactPoint => legContact == null ? Vector3.zero : legContact.WorstPoint;
    PalamedesPanelLegContact legContact;
    public float CurrentBend { get; private set; }
    float[] angles,velocities;
    Vector3 lastPosition,lastVelocity,filteredAcceleration,filteredAngularVelocity;
    Quaternion lastRotation;
    void OnEnable(){ResetSimulation();}
    public void ResetSimulation()
    {
        int count=bones==null?0:bones.Length;
        angles=new float[count];velocities=new float[count];
        if(motionSource){lastPosition=motionSource.position;lastRotation=motionSource.rotation;}
        lastVelocity=Vector3.zero;filteredAcceleration=Vector3.zero;filteredAngularVelocity=Vector3.zero;
        legContact=collisionRenderer&&legColliders!=null&&legColliders.Length>0?new PalamedesPanelLegContact(this):null;
    }
    void LateUpdate(){Simulate(Time.deltaTime);}
    public void Simulate(float dt)
    {
        if(!characterRoot||!motionSource||bones==null||restLocalRotations==null||bones.Length!=restLocalRotations.Length)return;
        if(angles==null||angles.Length!=bones.Length)ResetSimulation();
        if(dt<=0)return;
        Vector3 displacement=motionSource.position-lastPosition;
        if(displacement.magnitude>.5f||dt>.2f){ResetSimulation();return;}
        var velocity=displacement/dt;
        var acceleration=Vector3.ClampMagnitude((velocity-lastVelocity)/dt,30);
        Quaternion delta=motionSource.rotation*Quaternion.Inverse(lastRotation);
        delta.ToAngleAxis(out float angle,out Vector3 axis);
        if(angle>180)angle-=360;
        var angular=angle*angle>.000001f?axis*(angle/dt):Vector3.zero;
        float blend=1-Mathf.Exp(-dt*10);
        filteredAcceleration=Vector3.Lerp(filteredAcceleration,acceleration,blend);
        filteredAngularVelocity=Vector3.Lerp(filteredAngularVelocity,Vector3.ClampMagnitude(angular,360),blend);
        lastPosition=motionSource.position;lastRotation=motionSource.rotation;lastVelocity=velocity;
        var worldAxis=SwingFrame.TransformDirection(localSwingAxis).normalized;
        var outward=SwingFrame.TransformDirection(Vector3.Cross(localSwingAxis.normalized,Vector3.down)).normalized;
        float speed=windSource&&windSource.enabled?windSource.CurrentWindSpeed:0;
        float windProjection=windSource?Vector3.Dot(characterRoot.TransformDirection(windSource.localWindDirection).normalized,outward):0;
        windProjection=catchWindFromEitherSide?Mathf.Abs(windProjection):Mathf.Max(0,windProjection);
        float wind=speed*speed*windResponse*windProjection;
        float inertia=-Vector3.Dot(filteredAcceleration,outward)*movementResponse-Vector3.Dot(filteredAngularVelocity,worldAxis)*rotationResponse;
        CurrentBend=Mathf.Clamp(wind+inertia,-maximumBend*.35f,maximumBend);
        float sum=bones.Length*(bones.Length-1)*.5f;
        for(int i=0;i<bones.Length;i++)
        {
            if(!bones[i])continue;
            if(i==0){bones[i].localRotation=restLocalRotations[i];continue;}
            float target=CurrentBend*i/Mathf.Max(1,sum);
            angles[i]=Mathf.SmoothDamp(angles[i],target,ref velocities[i],dampingTime*(1+i*.1f),Mathf.Infinity,dt);
            var restWorld=bones[i].parent.rotation*restLocalRotations[i];
            bones[i].localRotation=restLocalRotations[i]*Quaternion.AngleAxis(angles[i],Quaternion.Inverse(restWorld)*worldAxis);
        }
        if(legContact!=null)legContact.Solve(dt,angles,worldAxis);
    }
    void OnDisable()
    {
        if(bones==null||restLocalRotations==null)return;
        for(int i=0;i<Mathf.Min(bones.Length,restLocalRotations.Length);i++)if(bones[i])bones[i].localRotation=restLocalRotations[i];
        CurrentBend=0;
    }
}
