using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RetroRendering
{
    /// <summary>World-space emissive bodies and opt-in volumetric point lights inside a local box.</summary>
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("Rendering/Firefly Volume")]
    public sealed class FireflyVolume : MonoBehaviour
    {
        [Header("区域 / 生成")]
        public Vector3 center;
        public Vector3 size = new Vector3(2, 1.2f, 1.5f);
        [Range(0, 64), Tooltip("体积光实际参与数量仍受 Volumetric Light Feature 的上限约束。")]
        public int count = 12;
        public int seed = 1337;
        public bool previewInEditor = true;
        [Header("实体亮点（世界单位）")]
        public Mesh bodyMesh;
        public Material bodyMaterial;
        [ColorUsage(false, true)] public Color emission = new Color(4, 6, 1.2f);
        [Min(.001f)] public float diameter = .015f;
        [Range(0, 1)] public float pulseAmount = .35f;
        [Min(0)] public float pulseFrequency = .5f;
        [Header("点光源（默认不投射阴影）")]
        public Color lightColor = new Color(.75f, 1, .25f);
        [Min(0)] public float lightIntensity = .12f;
        [Min(.001f)] public float lightRange = .25f;
        public LightShadows shadows = LightShadows.None;
        [Header("体积光（统一覆盖每只的 VolumetricLight）")]
        [Min(0)] public float scattering = .15f;
        [Range(-.95f, .95f)] public float anisotropy = .1f;
        [Min(.001f)] public float lightRadius = .025f;
        [Header("游动 / Mesh 避障")]
        [Min(0)] public float speed = .12f;
        [Min(.01f)] public float turnSpeed = 3;
        [Min(.1f)] public float wanderInterval = 2;
        [Min(0)] public float clearance = .025f;
        public LayerMask obstacleLayers = ~0;
        [Min(.1f), Tooltip("重新发现新增/删除 Mesh 的间隔；已缓存 Renderer 的 bounds 每帧更新。")]
        public float obstacleRefreshInterval = 1;

        sealed class Fly
        {
            public Transform transform;
            public MeshRenderer renderer;
            public Light light;
            public VolumetricLight volume;
            public Vector3 direction, target;
            public float nextTurn, phase, speedFactor;
        }
        readonly List<Fly> flies = new List<Fly>();
        readonly List<Bounds> obstacles = new List<Bounds>();
        Renderer[] sources = Array.Empty<Renderer>();
        System.Random random;
        Transform generated;
        MaterialPropertyBlock properties;
        bool rebuild = true;
        double previousTime;
        float elapsed, nextRefresh;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public int SpawnedCount => flies.Count;
        public static readonly HashSet<FireflyVolume> ActiveVolumes = new HashSet<FireflyVolume>();

        void OnEnable() { ActiveVolumes.Add(this); rebuild = true; previousTime = Time.realtimeSinceStartupAsDouble; }
        void OnDisable() { ActiveVolumes.Remove(this); Clear(); }
        void OnValidate() { rebuild = true; }
        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float dt = Application.isPlaying ? Time.deltaTime : (float)(now - previousTime);
            previousTime = now;
            if (!Application.isPlaying && !previewInEditor) { if (generated != null) Clear(); return; }
            if (rebuild) Regenerate();
            Tick(Mathf.Min(dt, .1f));
        }

        [ContextMenu("重新生成 / 刷新障碍物")]
        public void Regenerate()
        {
            Clear(); rebuild = false;
            if (!isActiveAndEnabled || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded ||
                bodyMesh == null || bodyMaterial == null || (!Application.isPlaying && !previewInEditor)) return;
            random = new System.Random(seed); elapsed = 0; nextRefresh = 0;
            RefreshObstacles();
            var root = new GameObject("Fireflies (Generated)") { hideFlags = HideFlags.DontSave };
            generated = root.transform; generated.SetParent(transform, false);
            properties = new MaterialPropertyBlock();
            for (int i = 0; i < Mathf.Clamp(count, 0, 64); i++)
            {
                if (!TryPosition(out var position)) break;
                var go = new GameObject($"Firefly {i + 1:00}") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
                go.transform.SetParent(generated, false);
                go.transform.position = position;
                go.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = bodyMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var fly = new Fly { transform = go.transform, renderer = renderer,
                    light = go.AddComponent<Light>(), volume = go.AddComponent<VolumetricLight>(),
                    phase = Random01() * Mathf.PI * 2, speedFactor = Mathf.Lerp(.7f, 1.3f, Random01()) };
                fly.light.type = LightType.Point;
#if UNITY_EDITOR
                // Newly created player lights are realtime; this authoring property is editor-only.
                fly.light.lightmapBakeType = LightmapBakeType.Realtime;
#endif
                ChooseTarget(fly); flies.Add(fly); Apply(fly);
            }
            if (flies.Count < count)
                Debug.LogWarning($"{name}: 区域可用空间不足，生成 {flies.Count}/{count} 只萤火虫。请移动/扩大区域或调整障碍物 Layer。", this);
        }

        /// <summary>Explicit step also used by editor preview and deterministic movement checks.</summary>
        public void Tick(float deltaTime)
        {
            if (generated == null || deltaTime <= 0) return;
            float dt = Mathf.Min(deltaTime, .1f); elapsed += dt;
            if (elapsed >= nextRefresh) RefreshObstacles(); else UpdateBounds();
            foreach (var fly in flies)
            {
                if (fly.transform == null) { rebuild = true; continue; }
                var position = fly.transform.position;
                if (!Free(position))
                {
                    // A moved obstacle/volume must not leave a glowing body embedded in geometry.
                    bool found = TryPosition(out position);
                    fly.transform.gameObject.SetActive(found);
                    if (!found) continue;
                    fly.transform.position = position; ChooseTarget(fly);
                }
                else fly.transform.gameObject.SetActive(true);
                if (elapsed >= fly.nextTurn || Vector3.Distance(position, fly.target) < .04f) ChooseTarget(fly);
                var desired = (fly.target - position).normalized;
                fly.direction = Vector3.Lerp(fly.direction, desired, 1 - Mathf.Exp(-Mathf.Max(.01f, turnSpeed) * dt)).normalized;
                var next = position + fly.direction * (Mathf.Max(0, speed) * fly.speedFactor * dt);
                if (Free(next) && PathFree(position, next)) fly.transform.position = next;
                else { fly.direction = Vector3.zero; ChooseTarget(fly); }
                Apply(fly);
            }
        }

        void Apply(Fly fly)
        {
            var scale = transform.lossyScale;
            float d = Mathf.Max(.001f, diameter);
            fly.transform.localScale = new Vector3(d / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                d / Mathf.Max(.0001f, Mathf.Abs(scale.y)), d / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
            float pulse = 1 - Mathf.Clamp01(pulseAmount) * (.5f + .5f * Mathf.Sin(elapsed * Mathf.Max(0, pulseFrequency) * 2 * Mathf.PI + fly.phase));
            properties.SetColor(BaseColor, emission * pulse); fly.renderer.SetPropertyBlock(properties);
            fly.light.color = lightColor; fly.light.intensity = Mathf.Max(0, lightIntensity) * pulse;
            fly.light.range = Mathf.Max(.001f, lightRange); fly.light.shadows = shadows;
            fly.volume.scattering = Mathf.Max(0, scattering);
            fly.volume.anisotropy = Mathf.Clamp(anisotropy, -.95f, .95f);
            fly.volume.radius = Mathf.Max(.001f, lightRadius);
        }
        void ChooseTarget(Fly fly)
        {
            var start = fly.transform.position;
            fly.target = start;
            for (int i = 0; i < 32; i++)
            {
                var candidate = RandomPosition();
                if (!Free(candidate) || !PathFree(start, candidate)) continue;
                fly.target = candidate; break;
            }
            fly.nextTurn = elapsed + Mathf.Max(.1f, wanderInterval) * Mathf.Lerp(.6f, 1.4f, Random01());
        }
        float Random01() => (float)random.NextDouble();
        Vector3 RandomPosition() => transform.TransformPoint(center + Vector3.Scale(SafeSize(),
            new Vector3(Random01() - .5f, Random01() - .5f, Random01() - .5f)));
        Vector3 SafeSize() => new Vector3(Mathf.Max(.001f, Mathf.Abs(size.x)), Mathf.Max(.001f, Mathf.Abs(size.y)), Mathf.Max(.001f, Mathf.Abs(size.z)));
        bool TryPosition(out Vector3 position)
        {
            for (int i = 0; i < 256; i++) { position = RandomPosition(); if (Free(position)) return true; }
            position = transform.TransformPoint(center); return false;
        }
        public bool Contains(Vector3 worldPosition)
        {
            var local = transform.InverseTransformPoint(worldPosition) - center;
            var half = SafeSize() * .5f;
            var scale = transform.lossyScale; float radius = Mathf.Max(.001f, diameter) * .5f;
            return Mathf.Abs(local.x) + radius / Mathf.Max(.0001f, Mathf.Abs(scale.x)) <= half.x &&
                Mathf.Abs(local.y) + radius / Mathf.Max(.0001f, Mathf.Abs(scale.y)) <= half.y &&
                Mathf.Abs(local.z) + radius / Mathf.Max(.0001f, Mathf.Abs(scale.z)) <= half.z;
        }
        bool Free(Vector3 position)
        {
            if (!Contains(position)) return false;
            foreach (var b in obstacles) if (b.Contains(position)) return false;
            return true;
        }
        bool PathFree(Vector3 from, Vector3 to)
        {
            var delta = to - from; float length = delta.magnitude;
            if (length < .000001f) return true;
            var ray = new Ray(from, delta / length);
            foreach (var b in obstacles) if (b.IntersectRay(ray, out float distance) && distance <= length) return false;
            return true;
        }
        void RefreshObstacles()
        {
            sources = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            nextRefresh = elapsed + Mathf.Max(.1f, obstacleRefreshInterval);
            UpdateBounds();
        }
        void UpdateBounds()
        {
            obstacles.Clear();
            foreach (var source in sources)
            {
                if (source == null || !source.enabled || !source.gameObject.activeInHierarchy ||
                    source.gameObject.scene != gameObject.scene ||
                    !(source is MeshRenderer || source is SkinnedMeshRenderer) ||
                    (obstacleLayers.value & (1 << source.gameObject.layer)) == 0 ||
                    source.GetComponentInParent<FireflyVolume>() != null) continue;
                var b = source.bounds;
                b.Expand(2 * Mathf.Max(0, clearance) + Mathf.Max(.001f, diameter));
                obstacles.Add(b);
            }
        }
        void Clear()
        {
            flies.Clear();
            if (generated != null)
            {
                generated.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(generated.gameObject); else DestroyImmediate(generated.gameObject);
            }
            generated = null;
        }
        void OnDrawGizmosSelected()
        {
            var previous = Gizmos.matrix; Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(.8f, 1, .2f, .7f); Gizmos.DrawWireCube(center, SafeSize()); Gizmos.matrix = previous;
        }
    }
}
