using System;
using System.Collections.Generic;
using UnityEngine;

// Coupled spring chains drive the cape skin. Each folded section stays coherent,
// while its orientation follows the delayed centreline and body contact.
[DefaultExecutionOrder(100), DisallowMultipleComponent, AddComponentMenu("角色视觉/布料动力学")]
public sealed class PalamedesClothDynamics : MonoBehaviour
{
    [Min(0)]
    public float startupBlendTime = .4f;
    [Range(0, 32)]
    public int startupSettleSteps = 12;
    public bool capeEnabled = true;
    public PalamedesPanelSettings[] panels = Array.Empty<PalamedesPanelSettings>();
    public Transform characterRoot, motionSource;
    public SkinnedMeshRenderer capeRenderer;
    [Tooltip("跟随躯干相对初始姿态的朝向，披风在重力作用下保持自然下垂。")]
    public bool followAttachmentHeading = true;
    [Tooltip("启用披风与身体碰撞；关闭后可观察纯骨骼动力学效果。")]
    public bool enableBodyCollision = true;
    public CapsuleCollider[] bodyColliders;
    public Transform[] bones;
    public Quaternion[] restLocalRotations, restRotationsInAttachment;
    public Vector3[] restLocalPositions, restInAttachment, restInRoot;
    [Min(2)]
    public int columns = 3, rows = 7;
    [Min(0)]
    public float shapeFrequency = .45f, drag = 4, gravity = 2f;
    [Range(0, 1)]
    public float bendStiffness = .025f, foldStiffness = .85f;
    [Range(2, 16)]
    public int solverIterations = 12;
    [Range(0, .02f)]
    public float bodyClearance = .003f;
    [Range(0, .01f)]
    public float contactSlop = .001f;
    [Range(0, 1)]
    public float contactFriction = .3f;
    [Min(.1f)]
    public float maximumSpeed = 1.5f;
    [Tooltip("让下垂的静态形状略向后抬起，挂点保持固定。")]
    [Range(0, 12)]
    public float restingBackLift = 0;
    [Tooltip("每个 60 Hz 模拟步对初始形状的拉回比例；越小，裙摆拖尾越明显。")]
    [Range(0, 1)]
    public float chainElasticity = .015f;
    [Tooltip("立即传递给自由骨骼的挂点位移比例；越小，移动滞后越明显。")]
    [Range(0, 1)]
    public float motionInertia = .1f;
    [Tooltip("褶皱横截面跟随模拟中心线弯曲的旋转比例。")]
    [Range(0, 1)]
    public float sectionRotation = .85f;
    [Range(0, 80)]
    public float maximumSectionAngle = 65;
    public float LastBodyPenetration { get; private set; }
    public float MaximumParticleSpeed { get; private set; }

    Vector3[] positions, velocities, targets, startPositions, contactNormals, contactOffsets;
    bool[] hasContactOffsets;
    Vector3[] sectionCenters, restSectionCenters;
    Quaternion[] sectionSwings;
    Vector3 lastAttachmentPosition;
    Quaternion lastAttachmentRotation, hangingRotation;
    struct SurfaceSample
    {
        public Vector3 vertex;
        public BoneWeight weights;
    }

    SurfaceSample[] samples;
    Transform[] skinBones;
    Matrix4x4[] bindposes, skinMatrices;
    int[] particleForSkinBone;
    struct Capsule
    {
        public Vector3 center, axis, min, max;
        public float half, radius;
    }

    Capsule[] capsules, oldCapsules, activeCapsules;
    int capsuleCount;
    bool ready;
    static readonly Unity.Profiling.ProfilerMarker Marker = new Unity.Profiling.ProfilerMarker("Palamedes.CapeClothCage");
    bool initializationPending = true;
    float startupElapsed, dynamicsWeight = 1;
    Vector3 previousRootPosition, previousRootScale;
    Quaternion previousRootRotation;

    // OnEnable precedes the Animator's first evaluation and may precede spawn placement.
    // Capture the animated pose in the first LateUpdate instead of injecting that jump as velocity.
    void OnEnable() => initializationPending = true;
    public void ResetSimulation()
    {
        initializationPending = false;
        startupElapsed = 0;
        dynamicsWeight = startupBlendTime > 0 ? 0 : 1;
        if (capeEnabled) ResetCapeSimulation();
        else RestoreCapePose();
        if (panels != null)
            foreach (var panel in panels)
            {
                if (panel == null) continue;
                if (!panel.characterRoot) panel.characterRoot = characterRoot;
                panel.RestorePose();
                if (panel.enabled) panel.ResetSimulation();
            }

        // Resolve the current pose's resting contacts before rendering. Do not turn
        // these initial collision corrections into an outward launch velocity.
        for (int i = 0; i < Mathf.Clamp(startupSettleSteps, 0, 32); i++)
        {
            if (capeEnabled && ready)
            {
                SimulateCape(1f / 120);
                Array.Clear(velocities, 0, velocities.Length);
            }
            if (panels != null)
                foreach (var panel in panels)
                    if (panel != null && panel.enabled) panel.Simulate(1f / 120, 0);
        }
        MaximumParticleSpeed = 0;
        RememberRootPose();
    }

    public void Simulate(float deltaTime)
    {
        if (deltaTime <= 0) return;
        var root = characterRoot ? characterRoot : transform;
        bool reset = initializationPending || deltaTime > .2f
            || (root.position - previousRootPosition).sqrMagnitude > .25f
            || Quaternion.Angle(root.rotation, previousRootRotation) > 75
            || (root.lossyScale - previousRootScale).sqrMagnitude > .0001f;
        if (reset)
        {
            ResetSimulation();
            deltaTime = Mathf.Min(deltaTime, 1f / 60);
        }
        startupElapsed += deltaTime;
        dynamicsWeight = startupBlendTime > 0 ? Mathf.SmoothStep(0, 1, startupElapsed / startupBlendTime) : 1;
        if (capeEnabled) SimulateCape(deltaTime);
        if (panels != null)
            foreach (var panel in panels)
                if (panel != null && panel.enabled) panel.Simulate(deltaTime, dynamicsWeight);
        RememberRootPose();
    }

    void RememberRootPose()
    {
        var root = characterRoot ? characterRoot : transform;
        previousRootPosition = root.position;
        previousRootRotation = root.rotation;
        previousRootScale = root.lossyScale;
    }

    void OnDisable()
    {
        RestoreCapePose();
        if (panels == null) return;
        foreach (var panel in panels) panel?.RestorePose();
    }

    void ResetCapeSimulation()
    {
        ready = false;
        LastBodyPenetration = MaximumParticleSpeed = 0;
        int count = columns * rows;
        if (columns < 2 || rows < 2 || !characterRoot || !motionSource || !capeRenderer || !capeRenderer.sharedMesh || bones == null || bones.Length != count || restInAttachment == null || restInAttachment.Length != count || restRotationsInAttachment == null || restRotationsInAttachment.Length != count)
            return;
        if (restInRoot == null || restInRoot.Length != count)
            return;
        for (int i = 0; i < count; i++)
            if (!bones[i])
                return;
        sectionCenters = new Vector3[rows];
        restSectionCenters = new Vector3[rows];
        sectionSwings = new Quaternion[rows];
        for (int row = 0; row < rows; row++)
        {
            for (int c = 0; c < columns; c++)
                restSectionCenters[row] += restInRoot[row * columns + c] / columns;
            sectionSwings[row] = Quaternion.identity;
        }

        positions = new Vector3[count];
        velocities = new Vector3[count];
        targets = new Vector3[count];
        startPositions = new Vector3[count];
        contactNormals = new Vector3[count];
        contactOffsets = new Vector3[count];
        hasContactOffsets = new bool[count];
        hangingRotation = HangingRotation(motionSource.rotation);
        for (int i = 0; i < count; i++)
        {
            positions[i] = targets[i] = motionSource.TransformPoint(restInAttachment[i]);
            if (followAttachmentHeading && i >= columns)
            {
                var offset = Quaternion.AngleAxis(restingBackLift, Vector3.right) * (restInRoot[i] - restInRoot[i % columns]);
                positions[i] = targets[i] = Vector3.Lerp(targets[i], targets[i % columns] + HangingVector(offset), Mathf.Clamp01((float)(i / columns) / 2));
            }
        }
        var mesh = capeRenderer.sharedMesh;
        skinBones = capeRenderer.bones;
        bindposes = mesh.bindposes;
        if (bindposes.Length != skinBones.Length)
            return;
        particleForSkinBone = new int[skinBones.Length];
        skinMatrices = new Matrix4x4[skinBones.Length];
        for (int i = 0; i < skinBones.Length; i++)
            particleForSkinBone[i] = Array.IndexOf(bones, skinBones[i]);
        var vertices = mesh.vertices;
        var weights = mesh.boneWeights;
        var surface = new List<SurfaceSample>();
        for (int i = 0; i < vertices.Length; i += 2)
        {
            var w = weights[i];
            float Free(int b, float f) => particleForSkinBone[b] >= columns ? f : 0;
            float free = Free(w.boneIndex0, w.weight0) + Free(w.boneIndex1, w.weight1) + Free(w.boneIndex2, w.weight2) + Free(w.boneIndex3, w.weight3);
            if (free > .5f)
                surface.Add(new SurfaceSample { vertex = vertices[i], weights = w });
        }

        samples = surface.ToArray();
        capsules = new Capsule[bodyColliders == null ? 0 : bodyColliders.Length];
        oldCapsules = new Capsule[capsules.Length];
        activeCapsules = new Capsule[capsules.Length];
        ReadBodyCapsules();
        Array.Copy(capsules, oldCapsules, capsules.Length);
        lastAttachmentPosition = motionSource.position;
        lastAttachmentRotation = motionSource.rotation;
        ready = true;
        PreserveFoldSections();
        ApplyBonePose();
    }

    void LateUpdate() => Simulate(Time.deltaTime);
    void SimulateCape(float dt)
    {
        if (!ready || bones == null || positions.Length != bones.Length || capsules.Length != (bodyColliders == null ? 0 : bodyColliders.Length))
            ResetCapeSimulation();
        if (!ready || dt <= 0)
            return;
        using (Marker.Auto())
        {
            if ((motionSource.position - lastAttachmentPosition).sqrMagnitude > 1 || dt > .25f)
            {
                ResetCapeSimulation();
                return;
            }

            ReadBodyCapsules();
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt * 120), 1, 12);
            float h = Mathf.Min(dt, .1f) / steps;
            var nextPosition = motionSource.position;
            var nextRotation = motionSource.rotation;
            for (int step = 0; step < steps; step++)
            {
                float t = (step + 1f) / steps;
                var attachmentPosition = Vector3.Lerp(lastAttachmentPosition, nextPosition, t);
                var attachmentRotation = Quaternion.Slerp(lastAttachmentRotation, nextRotation, t);
                hangingRotation = HangingRotation(attachmentRotation);
                var attachmentMove = (nextPosition - lastAttachmentPosition) / steps;
                PredictParticles(h, attachmentPosition, attachmentRotation, nextPosition, nextRotation, attachmentMove);
                SolveConstraints(t);
                UpdateVelocities(h);
            }

            lastAttachmentPosition = nextPosition;
            lastAttachmentRotation = nextRotation;
            Array.Copy(capsules, oldCapsules, capsules.Length);
            ApplyBonePose();
        }
    }

    Quaternion HangingRotation(Quaternion attachmentRotation)
    {
        if (!followAttachmentHeading)
            return characterRoot.rotation;
        // Rest cape bone axes match character-root axes. Remove the attachment's
        // bind rotation before extracting heading; keep gravity independent of torso lean.
        var forward = Vector3.ProjectOnPlane(attachmentRotation * restRotationsInAttachment[0] * Vector3.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-8f)
            forward = Vector3.ProjectOnPlane(characterRoot.forward, Vector3.up);
        return forward.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(forward, Vector3.up) : characterRoot.rotation;
    }

    Vector3 HangingVector(Vector3 localOffset)
    {
        return hangingRotation * Quaternion.Inverse(characterRoot.rotation) * characterRoot.TransformVector(localOffset);
    }

    void PredictParticles(float h, Vector3 attachmentPosition, Quaternion attachmentRotation, Vector3 nextPosition, Quaternion nextRotation, Vector3 attachmentMove)
    {
        float elasticity = 1 - Mathf.Pow(1 - chainElasticity, h * 60);
        for (int i = 0; i < bones.Length; i++)
        {
            var offset = Quaternion.Inverse(nextRotation) * (motionSource.TransformPoint(restInAttachment[i]) - nextPosition);
            targets[i] = attachmentPosition + attachmentRotation * offset;
            startPositions[i] = positions[i];
            contactNormals[i] = Vector3.zero;
            if (i < columns)
            {
                positions[i] = targets[i];
                velocities[i] = Vector3.zero;
                continue;
            }

            if (restInRoot != null && restInRoot.Length == bones.Length)
            {
                int anchor = i % columns;
                var offsetFromTop = Quaternion.AngleAxis(restingBackLift, Vector3.right) * (restInRoot[i] - restInRoot[anchor]);
                var hanging = targets[anchor] + HangingVector(offsetFromTop);
                targets[i] = Vector3.Lerp(targets[i], hanging, Mathf.Clamp01((float)(i / columns) / 2));
            }

            // Carry only a small fraction of root movement: the remaining motion
            // propagates down the chain rather than snapping the whole cape to it.
            var carried = attachmentMove * Mathf.Lerp(1, motionInertia, dynamicsWeight);
            startPositions[i] += carried;
            positions[i] += carried + velocities[i] * Mathf.Exp(-(drag + (1 - dynamicsWeight) * 20) * h) * h + Vector3.down * (gravity * h * h);
        }

        for (int i = columns; i < bones.Length; i++)
        {
            int parent = i - columns;
            float along = (float)(i / columns) / (rows - 1);
            var relativeRest = targets[i] - targets[parent];
            var restTarget = positions[parent] + relativeRest;
            // Per-link elasticity lets motion reach the hem later than the shoulder.
            positions[i] += (restTarget - positions[i]) * elasticity * Mathf.Lerp(1, .55f, along);
            float omega = 2 * Mathf.PI * shapeFrequency;
            positions[i] += (targets[i] - positions[i]) * (omega * omega * h * h * Mathf.Lerp(.25f, .04f, along));
        }
    }

    void SolveConstraints(float substepFraction)
    {
        for (int iteration = 0; iteration < solverIterations; iteration++)
        {
            ConstrainCenterline();
            PreserveFoldSections();
            if (iteration == solverIterations / 2 || iteration == solverIterations - 1)
            {
                ApplyBonePose();
                SolveBodyContacts(substepFraction);
                PreserveFoldSections();
            }
        }
    }

    void UpdateVelocities(float h)
    {
        MaximumParticleSpeed = 0;
        for (int row = 1; row < rows; row++)
        {
            var displacement = Vector3.zero;
            var normal = Vector3.zero;
            for (int c = 0; c < columns; c++)
            {
                int i = row * columns + c;
                displacement += positions[i] - startPositions[i];
                normal += contactNormals[i];
            }

            // Only centre translation contributes velocity. Counting the derived
            // cross-section rotation again injects energy into the folded mesh.
            var velocity = displacement / (columns * h);
            if (normal.sqrMagnitude > 1e-10f)
                velocity = Vector3.ProjectOnPlane(velocity, normal.normalized) * (1 - contactFriction);
            velocity = Vector3.ClampMagnitude(velocity, maximumSpeed);
            MaximumParticleSpeed = Mathf.Max(MaximumParticleSpeed, velocity.magnitude);
            for (int c = 0; c < columns; c++)
                velocities[row * columns + c] = velocity;
        }
    }

    void UpdateSectionCenters()
    {
        for (int row = 0; row < rows; row++)
        {
            sectionCenters[row] = Vector3.zero;
            for (int c = 0; c < columns; c++)
                sectionCenters[row] += positions[row * columns + c] / columns;
        }
    }

    void ConstrainCenterline()
    {
        UpdateSectionCenters();
        for (int row = 1; row < rows; row++)
        {
            var rest = HangingVector(restSectionCenters[row] - restSectionCenters[row - 1]);
            var delta = sectionCenters[row] - sectionCenters[row - 1];
            float maxDeviation = rest.magnitude * (1 - bendStiffness) * 2;
            delta = rest + Vector3.ClampMagnitude(delta - rest, maxDeviation);
            // A single length constraint per section avoids conflicting stretch
            // constraints between the inner and outer edges of a curved fold.
            var direction = delta.sqrMagnitude > 1e-10f ? delta.normalized : rest.normalized;
            var desired = sectionCenters[row - 1] + direction * rest.magnitude;
            var shift = desired - sectionCenters[row];
            sectionCenters[row] = desired;
            for (int c = 0; c < columns; c++)
                positions[row * columns + c] += shift;
        }
    }

    void ApplyBonePose()
    {
        for (int i = 0; i < bones.Length; i++)
        {
            if (!bones[i])
                continue;
            int row = i / columns;
            var rotation = row == 0 ? motionSource.rotation * restRotationsInAttachment[i] : sectionSwings[row] * hangingRotation;
            bones[i].SetPositionAndRotation(positions[i], rotation);
        }
    }

    void PreserveFoldSections()
    {
        // Keep the authored folds within each row, but rotate that row with the
        // centreline. This allows bending without letting the three columns cross.
        UpdateSectionCenters();
        var down = hangingRotation * Vector3.down;
        for (int row = 1; row < rows; row++)
        {
            float minimum = (restSectionCenters[row - 1].y - restSectionCenters[row].y) * .2f * characterRoot.lossyScale.y;
            float deficit = minimum - Vector3.Dot(sectionCenters[row] - sectionCenters[row - 1], down);
            if (deficit > 0)
                sectionCenters[row] += down * deficit;
        }

        for (int row = 1; row < rows; row++)
        {
            int above = row - 1, below = Mathf.Min(rows - 1, row + 1);
            var restDirection = HangingVector(restSectionCenters[below] - restSectionCenters[above]);
            var direction = sectionCenters[below] - sectionCenters[above];
            var swing = restDirection.sqrMagnitude > 1e-10f && direction.sqrMagnitude > 1e-10f ? Quaternion.FromToRotation(restDirection, direction) : Quaternion.identity;
            swing = Quaternion.RotateTowards(Quaternion.identity, swing, maximumSectionAngle);
            sectionSwings[row] = Quaternion.Slerp(Quaternion.identity, swing, sectionRotation);
            for (int c = 0; c < columns; c++)
            {
                int i = row * columns + c;
                var offset = HangingVector(restInRoot[i] - restSectionCenters[row]);
                positions[i] = Vector3.Lerp(positions[i], sectionCenters[row] + sectionSwings[row] * offset, foldStiffness);
            }
        }
    }

    void ReadBodyCapsules()
    {
        capsuleCount = 0;
        if (!enableBodyCollision || bodyColliders == null)
            return;
        foreach (var c in bodyColliders)
        {
            if (!c)
                continue;
            var scale = c.transform.lossyScale;
            int d = c.direction;
            float radial = d == 0 ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)) : d == 1 ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            capsules[capsuleCount++] = new Capsule
            {
                center = c.transform.TransformPoint(c.center),
                axis = c.transform.TransformDirection(d == 0 ? Vector3.right : d == 1 ? Vector3.up : Vector3.forward).normalized,
                half = Mathf.Max(0, c.height * Mathf.Abs(scale[d]) * .5f - c.radius * radial),
                radius = c.radius * radial + bodyClearance
            };
        }
    }

    void SolveBodyContacts(float substepFraction)
    {
        LastBodyPenetration = 0;
        if (capsuleCount == 0)
            return;
        Array.Clear(contactOffsets, 0, contactOffsets.Length);
        Array.Clear(hasContactOffsets, 0, hasContactOffsets.Length);
        for (int c = 0; c < capsuleCount; c++)
        {
            var shape = capsules[c];
            shape.center = Vector3.Lerp(oldCapsules[c].center, shape.center, substepFraction);
            shape.axis = Vector3.Slerp(oldCapsules[c].axis, shape.axis, substepFraction);
            var extent = new Vector3(Mathf.Abs(shape.axis.x), Mathf.Abs(shape.axis.y), Mathf.Abs(shape.axis.z)) * shape.half + Vector3.one * shape.radius;
            shape.min = shape.center - extent;
            shape.max = shape.center + extent;
            activeCapsules[c] = shape;
        }

        for (int i = 0; i < skinBones.Length; i++)
            skinMatrices[i] = skinBones[i].localToWorldMatrix * bindposes[i];
        foreach (var sample in samples)
        {
            var w = sample.weights;
            var v = sample.vertex;
            var point = skinMatrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0;
            if (w.weight1 > 0)
                point += skinMatrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1;
            if (w.weight2 > 0)
                point += skinMatrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2;
            if (w.weight3 > 0)
                point += skinMatrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
            Vector3 correction = Vector3.zero;
            for (int c = 0; c < capsuleCount; c++)
            {
                var shape = activeCapsules[c];
                var p = point + correction;
                if (p.x < shape.min.x || p.x > shape.max.x || p.y < shape.min.y || p.y > shape.max.y || p.z < shape.min.z || p.z > shape.max.z)
                    continue;
                var nearest = shape.center + shape.axis * Mathf.Clamp(Vector3.Dot(p - shape.center, shape.axis), -shape.half, shape.half);
                var delta = p - nearest;
                float distance = delta.magnitude;
                float depth = shape.radius - distance;
                LastBodyPenetration = Mathf.Max(LastBodyPenetration, depth);
                if (depth <= contactSlop)
                    continue;
                var normal = hangingRotation * Vector3.back;
                // Preserve the side of the body occupied by the cape. A moving foot
                // can otherwise overtake a sample and expel it through the front.
                float along = Vector3.Dot(delta, normal);
                depth = -along + Mathf.Sqrt(Mathf.Max(0, along * along + shape.radius * shape.radius - distance * distance));
                correction += normal * Mathf.Max(0, depth - contactSlop);
            }

            if (correction.sqrMagnitude < 1e-12f)
                continue;
            float denominator = 0;
            void Sum(int b, float weight)
            {
                if (particleForSkinBone[b] >= columns)
                    denominator += weight * weight;
            }

            Sum(w.boneIndex0, w.weight0);
            Sum(w.boneIndex1, w.weight1);
            Sum(w.boneIndex2, w.weight2);
            Sum(w.boneIndex3, w.weight3);
            void Push(int b, float weight)
            {
                int p = particleForSkinBone[b];
                if (p < columns || weight <= 0)
                    return;
                var push = correction * weight / Mathf.Max(.01f, denominator);
                if (push.sqrMagnitude > contactOffsets[p].sqrMagnitude)
                {
                    contactOffsets[p] = push;
                    hasContactOffsets[p] = true;
                }

                contactNormals[p] += correction.normalized * weight;
            }

            Push(w.boneIndex0, w.weight0);
            Push(w.boneIndex1, w.weight1);
            Push(w.boneIndex2, w.weight2);
            Push(w.boneIndex3, w.weight3);
        }

        for (int row = 1; row < rows; row++)
        {
            int first = row * columns;
            var push = Vector3.zero;
            var normal = Vector3.zero;
            for (int c = 0; c < columns; c++)
            {
                int i = first + c;
                if (hasContactOffsets[i] && contactOffsets[i].sqrMagnitude > push.sqrMagnitude)
                    push = contactOffsets[i];
                normal += contactNormals[i];
            }

            push = Vector3.ClampMagnitude(push, .012f);
            for (int c = 0; c < columns; c++)
            {
                positions[first + c] += push;
                contactNormals[first + c] = normal;
            }
        }
    }

    void RestoreCapePose()
    {
        if (bones == null)
            return;
        for (int i = 0; i < bones.Length; i++)
            if (bones[i])
            {
                if (restLocalPositions != null && i < restLocalPositions.Length)
                    bones[i].localPosition = restLocalPositions[i];
                if (restLocalRotations != null && i < restLocalRotations.Length)
                    bones[i].localRotation = restLocalRotations[i];
            }

        ready = false;
    }
}


[Serializable]
public sealed class PalamedesPanelSettings
{
    public bool enabled = true;
    public bool enableLegCollision = true;
    public string partName;
    public Transform characterRoot;
    public Transform motionSource;
    public Transform[] bones;
    public Quaternion[] restLocalRotations;
    public Vector3 localSwingAxis = Vector3.right;
    [Range(0, 15)]
    public float maximumBend = 6;
    [Tooltip("摆动平面跟随固定挂点，骨盆转动时碰撞避让方向也随之转动。")]
    public bool followAttachmentRotation;
    public Transform SwingFrame => followAttachmentRotation && bones != null && bones.Length > 0 && bones[0] ? bones[0] : characterRoot;

    [Min(0)]
    public float movementResponse = .3f;
    [Min(0)]
    public float rotationResponse = .025f;
    [Min(.05f)]
    public float dampingTime = .3f;
    public SkinnedMeshRenderer collisionRenderer;
    public CapsuleCollider[] legColliders;
    [Min(0)]
    public float collisionClearance = .004f;
    [Tooltip("忽略轻微胶囊重叠，达到接触深度后再触发避让，减少抖动。")]
    [Min(0)]
    public float contactActivationDepth = .006f;
    [Range(0, 30)]
    public float maximumContactBend = 12;
    [Range(0, 15)]
    public float maximumAnchorContactBend = 10;
    [Min(1)]
    public float contactReturnSpeed = 25;
    public float LastLegPenetration => legContact == null ? 0 : legContact.Penetration;
    public float CurrentContactBend => legContact == null ? 0 : legContact.TotalBend;
    public Vector3 WorstContactPoint => legContact == null ? Vector3.zero : legContact.WorstPoint;

    PanelLegContact legContact;
    public float CurrentBend { get; private set; }

    float[] angles, velocities;
    Vector3 lastPosition, lastVelocity, filteredAcceleration, filteredAngularVelocity;
    Quaternion lastRotation;

    public void ResetSimulation()
    {
        int count = bones == null ? 0 : bones.Length;
        angles = new float[count];
        velocities = new float[count];
        if (motionSource)
        {
            lastPosition = motionSource.position;
            lastRotation = motionSource.rotation;
        }

        lastVelocity = Vector3.zero;
        filteredAcceleration = Vector3.zero;
        filteredAngularVelocity = Vector3.zero;
        legContact = enableLegCollision && collisionRenderer && legColliders != null && legColliders.Length > 0 ? new PanelLegContact(this) : null;
    }


    public void Simulate(float dt, float inertiaWeight = 1)
    {
        if (!characterRoot || !motionSource || bones == null || restLocalRotations == null || bones.Length != restLocalRotations.Length)
            return;
        if (angles == null || angles.Length != bones.Length)
            ResetSimulation();
        if (dt <= 0)
            return;
        Vector3 displacement = motionSource.position - lastPosition;
        if (displacement.magnitude > .5f || dt > .2f)
        {
            ResetSimulation();
            return;
        }

        var velocity = displacement / dt;
        var acceleration = Vector3.ClampMagnitude((velocity - lastVelocity) / dt, 30);
        Quaternion delta = motionSource.rotation * Quaternion.Inverse(lastRotation);
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180)
            angle -= 360;
        var angular = angle * angle > .000001f ? axis * (angle / dt) : Vector3.zero;
        float blend = 1 - Mathf.Exp(-dt * 10);
        filteredAcceleration = Vector3.Lerp(filteredAcceleration, acceleration, blend);
        filteredAngularVelocity = Vector3.Lerp(filteredAngularVelocity, Vector3.ClampMagnitude(angular, 360), blend);
        lastPosition = motionSource.position;
        lastRotation = motionSource.rotation;
        lastVelocity = velocity;
        var worldAxis = SwingFrame.TransformDirection(localSwingAxis).normalized;
        var outward = SwingFrame.TransformDirection(Vector3.Cross(localSwingAxis.normalized, Vector3.down)).normalized;
        float inertia = -Vector3.Dot(filteredAcceleration, outward) * movementResponse - Vector3.Dot(filteredAngularVelocity, worldAxis) * rotationResponse;
        CurrentBend = Mathf.Clamp(inertia, -maximumBend * .35f, maximumBend) * Mathf.Clamp01(inertiaWeight);
        float sum = bones.Length * (bones.Length - 1) * .5f;
        for (int i = 0; i < bones.Length; i++)
        {
            if (!bones[i])
                continue;
            if (i == 0)
            {
                bones[i].localRotation = restLocalRotations[i];
                continue;
            }

            float target = CurrentBend * i / Mathf.Max(1, sum);
            angles[i] = Mathf.SmoothDamp(angles[i], target, ref velocities[i], dampingTime * (1 + i * .1f), Mathf.Infinity, dt);
            var restWorld = bones[i].parent.rotation * restLocalRotations[i];
            bones[i].localRotation = restLocalRotations[i] * Quaternion.AngleAxis(angles[i], Quaternion.Inverse(restWorld) * worldAxis);
        }

        if (enableLegCollision && legContact != null)
            legContact.Solve(dt, angles, worldAxis);
    }

    public void RestorePose()
    {
        if (bones == null || restLocalRotations == null)
            return;
        for (int i = 0; i < Mathf.Min(bones.Length, restLocalRotations.Length); i++)
            if (bones[i])
                bones[i].localRotation = restLocalRotations[i];
        CurrentBend = 0;
    }
}


// Direct capsule contact only. Preserve the original panel skin and inertial motion whenever
// there is no overlap; limit contact response to small rotations of the existing rig.
internal sealed class PanelLegContact
{
    readonly PalamedesPanelSettings owner;
    struct Capsule
    {
        public Vector3 a, b, min, max;
        public float radius;
    }

    readonly Vector3[] vertices, skinned;
    readonly BoneWeight[] weights;
    readonly int[] triangles;
    readonly Transform[] skinBones;
    readonly Matrix4x4[] bindposes, matrices;
    readonly float[] correction;
    readonly bool[] contactSamples;
    readonly Capsule[] capsules;
    int capsuleCount;
    Vector3 contactAxis;
    public float Penetration { get; private set; }
    public float TotalBend { get; private set; }
    public Vector3 WorstPoint { get; private set; }

    static readonly Unity.Profiling.ProfilerMarker Marker = new Unity.Profiling.ProfilerMarker("Palamedes.SmallPanelLegContact");
    public PanelLegContact(PalamedesPanelSettings owner)
    {
        this.owner = owner;
        var mesh = owner.collisionRenderer.sharedMesh;
        bindposes = mesh.bindposes;
        skinBones = owner.collisionRenderer.bones;
        matrices = new Matrix4x4[skinBones.Length];
        correction = new float[owner.bones.Length];
        capsules = new Capsule[owner.legColliders.Length];
        vertices = mesh.vertices;
        weights = mesh.boneWeights;
        triangles = mesh.triangles;
        skinned = new Vector3[vertices.Length];
        float top = float.NegativeInfinity, bottom = float.PositiveInfinity;
        foreach (var vertex in vertices)
        {
            top = Mathf.Max(top, vertex.y);
            bottom = Mathf.Min(bottom, vertex.y);
        }

        float contactTop = top - (top - bottom) * .15f;
        contactSamples = new bool[vertices.Length + triangles.Length / 3];
        for (int i = 0; i < vertices.Length; i++)
            contactSamples[i] = vertices[i].y < contactTop;
        for (int i = 0; i < triangles.Length; i += 3)
            contactSamples[vertices.Length + i / 3] = (vertices[triangles[i]].y + vertices[triangles[i + 1]].y + vertices[triangles[i + 2]].y) / 3 < contactTop;
    }

    public void Solve(float dt, float[] swayAngles, Vector3 axis)
    {
        if (owner.bones.Length != 3 || bindposes.Length != skinBones.Length)
            return;
        using (Marker.Auto())
        {
            contactAxis = owner.bones[0].parent.rotation * owner.restLocalRotations[0] * owner.localSwingAxis.normalized;
            UpdateCapsules();
            for (int i = 0; i < correction.Length; i++)
                correction[i] = Mathf.MoveTowards(correction[i], 0, owner.contactReturnSpeed * dt);
            // Test the original sway pose, not a projected exclusion zone. A dead band
            // prevents small proxy/skin differences from disturbing otherwise good motion.
            float c0 = correction[0], c1 = correction[1], c2 = correction[2];
            correction[0] = correction[1] = correction[2] = 0;
            Pose(swayAngles, axis);
            Error(float.PositiveInfinity, out float rawDepth);
            correction[0] = c0;
            correction[1] = c1;
            correction[2] = c2;
            Pose(swayAngles, axis);
            for (int pass = 0; rawDepth > owner.contactActivationDepth && pass < 8; pass++)
            {
                float best = Error(float.PositiveInfinity, out float depth);
                if (depth <= .001f)
                    break;
                int joint = -1;
                float chosen = 0;
                for (int j = 0; j < correction.Length; j++)
                {
                    float original = correction[j];
                    float limit = j == 0 ? owner.maximumAnchorContactBend : owner.maximumContactBend;
                    for (int step = 0; step < 6; step++)
                    {
                        float amount = step % 3 == 0 ? .5f : step % 3 == 1 ? 2 : 6;
                        float candidate = Mathf.Clamp(original + amount * (step < 3 ? 1 : -1), -limit * .5f, limit);
                        if (candidate == original)
                            continue;
                        float total = Mathf.Abs(candidate);
                        for (int k = 0; k < correction.Length; k++)
                            if (k != j)
                                total += Mathf.Abs(correction[k]);
                        if (total > 22)
                            continue;
                        correction[j] = candidate;
                        Pose(swayAngles, axis);
                        float error = Error(best, out _);
                        if (error < best - 1e-10f)
                        {
                            best = error;
                            joint = j;
                            chosen = candidate;
                        }
                    }

                    correction[j] = original;
                }

                if (joint < 0)
                {
                    Pose(swayAngles, axis);
                    break;
                }

                correction[joint] = chosen;
                Pose(swayAngles, axis);
            }

            Error(float.PositiveInfinity, out float finalDepth);
            Penetration = finalDepth;
            TotalBend = Mathf.Abs(correction[0]) + Mathf.Abs(correction[1]) + Mathf.Abs(correction[2]);
        }
    }

    void Pose(float[] angles, Vector3 axis)
    {
        // The waist pivot position stays fixed. Contact can rotate the original pinned
        // section as one piece, rather than stretching it or moving the waistband sideways.
        for (int i = 0; i < owner.bones.Length; i++)
        {
            var bone = owner.bones[i];
            var rest = owner.restLocalRotations[i];
            var inverse = Quaternion.Inverse(bone.parent.rotation * rest);
            bone.localRotation = rest * Quaternion.AngleAxis(i == 0 ? 0 : angles[i], inverse * axis) * Quaternion.AngleAxis(correction[i], inverse * contactAxis);
        }

        for (int i = 0; i < matrices.Length; i++)
            matrices[i] = skinBones[i].localToWorldMatrix * bindposes[i];
    }

    void UpdateCapsules()
    {
        capsuleCount = 0;
        foreach (var collider in owner.legColliders)
        {
            if (!collider)
                continue;
            var scale = collider.transform.lossyScale;
            var direction = collider.direction == 0 ? Vector3.right : collider.direction == 1 ? Vector3.up : Vector3.forward;
            float radial = collider.direction == 0 ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)) : collider.direction == 1 ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            float radius = collider.radius * radial;
            float half = Mathf.Max(0, collider.height * Mathf.Abs(scale[collider.direction]) * .5f - radius);
            var center = collider.transform.TransformPoint(collider.center);
            var axis = collider.transform.TransformDirection(direction).normalized;
            var a = center - axis * half;
            var b = center + axis * half;
            radius += owner.collisionClearance;
            capsules[capsuleCount++] = new Capsule
            {
                a = a,
                b = b,
                radius = radius,
                min = Vector3.Min(a, b) - Vector3.one * radius,
                max = Vector3.Max(a, b) + Vector3.one * radius
            };
        }
    }

    float Error(float cutoff, out float deepest)
    {
        float error = 0;
        deepest = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            var w = weights[i];
            var v = vertices[i];
            var p = matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0;
            if (w.weight1 > 0)
                p += matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1;
            if (w.weight2 > 0)
                p += matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2;
            if (w.weight3 > 0)
                p += matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
            skinned[i] = p;
        }

        for (int i = 0; i < vertices.Length + triangles.Length / 3; i++)
        {
            // The waist seam is above the leg-contact region; rounded thigh caps near
            // the hip must not change the established idle/flying silhouette.
            if (!contactSamples[i])
                continue;
            Vector3 p;
            if (i < vertices.Length)
                p = skinned[i];
            else
            {
                int triangle = (i - vertices.Length) * 3;
                p = (skinned[triangles[triangle]] + skinned[triangles[triangle + 1]] + skinned[triangles[triangle + 2]]) / 3;
            }

            float depth = 0;
            for (int c = 0; c < capsuleCount; c++)
            {
                var capsule = capsules[c];
                if (p.x < capsule.min.x || p.x > capsule.max.x || p.y < capsule.min.y || p.y > capsule.max.y || p.z < capsule.min.z || p.z > capsule.max.z)
                    continue;
                var segment = capsule.b - capsule.a;
                float t = Mathf.Clamp01(Vector3.Dot(p - capsule.a, segment) / Mathf.Max(1e-12f, segment.sqrMagnitude));
                float distance2 = (p - (capsule.a + segment * t)).sqrMagnitude;
                if (distance2 < capsule.radius * capsule.radius)
                    depth = Mathf.Max(depth, capsule.radius - Mathf.Sqrt(distance2));
            }

            if (depth > deepest)
            {
                deepest = depth;
                if (float.IsPositiveInfinity(cutoff))
                    WorstPoint = owner.characterRoot.InverseTransformPoint(p);
            }

            error += depth * depth;
            if (error >= cutoff)
                return error;
        }

        return error;
    }
}
