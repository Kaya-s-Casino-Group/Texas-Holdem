using System;
using System.Collections.Generic;
using System.Linq;
using CardArt;
using CardPresentation;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

namespace Poker.SingleBattle
{
    /// <summary>A persistent shield container in actor space. Only DOTween operations are transient.</summary>
    [DisallowMultipleComponent]
    public sealed class DiscardShieldPresenter : MonoBehaviour, ISerializationCallbackReceiver
    {
        [Header("共享渲染资源")]
        public Mesh CardMesh;
        public Material ShieldMaterial;
        public CardAtlasData Atlas;
        [Header("飞入起点区域（局部坐标）")]
        public Vector3 SpawnCenter = new Vector3(0, .25f, -.15f);
        public Vector3 SpawnSize = new Vector3(.18f, .04f, .04f);
        [Header("圆弧落地区域（卡牌底部，局部坐标）")]
        public Vector3 RingCenter;
        [Min(0)] public float RingInnerRadius = .15f;
        [Min(0)] public float RingOuterRadius = .22f;
        [Tooltip("圆弧中心朝向，0 度为容器前方（Visual.forward）。")]
        public float ArcCenterAngle;
        [Range(1, 360)] public float ArcAngle = 60;
        [Min(0)] public float LandingHeightVariation = .02f;
        [HideInInspector] public int RingLayoutVersion;

        [Header("卡牌等比尺寸")]
        [Tooltip("卡牌高度（容器局部单位），宽度自动采用图集原始牌面比例。")]
        [Min(.001f)] public float CardHeight = .10f;
        public Vector2 CardSize => new Vector2(CardHeight *
            (Atlas != null && Atlas.cardSize.y > 0 ? (float)Atlas.cardSize.x / Atlas.cardSize.y : 2f / 3f), CardHeight);
        [Header("落地随机角度（度，X 为最小值 / Y 为最大值）")]
        [Tooltip("绕卡牌自身 X 轴的前后俯仰范围。")]
        public Vector2 PitchRange = new Vector2(-8, 8);
        [Tooltip("相对朝内 / 朝外方向，绕自身 Y 轴的偏转范围。")]
        public Vector2 YawRange = new Vector2(-12, 12);
        [Tooltip("绕卡牌自身 Z 轴的左右倾斜范围。")]
        public Vector2 RollRange = new Vector2(-20, 20);
        [Tooltip("0 全部朝内，1 全部朝外，0.5 各半；随后叠加上述角度。")]
        [Range(0, 1)] public float OutwardChance = .5f;
        public int Seed = 173;
        [FormerlySerializedAs("CardSize"), SerializeField, HideInInspector] private Vector2 legacyCardSize;
        [FormerlySerializedAs("RotationJitter"), SerializeField, HideInInspector] private Vector3 legacyRotationJitter = new Vector3(-1, -1, -1);
        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() => MigrateLegacySettings();
        private void OnValidate() => MigrateLegacySettings();
        private void MigrateLegacySettings()
        {
            if (legacyCardSize.y > 0) { CardHeight = legacyCardSize.y; legacyCardSize = Vector2.zero; }
            if (legacyRotationJitter.x >= 0 && legacyRotationJitter.y >= 0 && legacyRotationJitter.z >= 0)
            {
                PitchRange = new Vector2(-legacyRotationJitter.x, legacyRotationJitter.x);
                YawRange = new Vector2(-legacyRotationJitter.y, legacyRotationJitter.y);
                RollRange = new Vector2(-legacyRotationJitter.z, legacyRotationJitter.z);
                legacyRotationJitter = new Vector3(-1, -1, -1);
            }
        }
        [Header("DOTween 时间")]
        [Min(.01f)] public float FlightSeconds = .35f;
        [Min(0)] public float CardInterval = .08f;
        [Min(0)] public float ArcHeight = .08f;
        [Min(.01f)] public float DissolveSeconds = .24f;
        [Min(0)] public float DissolveInterval = .06f;
        [Header("落地反馈（逐张）")]
        public ParticleSystem LandingEffect;
        public AudioClip LandingSound;
        [Range(0, 1)] public float LandingVolume = .65f;
        public AudioClip BlockHitSound;
        [Range(0, 1)] public float BlockHitVolume = .65f;
        public CardHapticSettings Haptics = CardHapticSettings.Default;
        public int VisibleCardCount => cards.Count;
        public event Action<string> CardLanded;
        private sealed class Visual
        {
            public string Key, Id;
            public GameObject Root;
            public MeshRenderer Renderer;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector4 Rect, Noise;
            public float Dissolve, Flash;
            public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
            public void Apply()
            {
                if (Renderer == null) return;
                Properties.SetVector("_FrontUVRect", Rect); Properties.SetVector("_NoiseOffset", Noise);
                Properties.SetFloat("_Dissolve", Dissolve); Properties.SetFloat("_LandingFlash", Flash);
                Renderer.SetPropertyBlock(Properties);
            }
        }
        private readonly Dictionary<string, Visual> cards = new Dictionary<string, Visual>();
        private readonly List<GameObject> effects = new List<GameObject>();
        private ShieldSnapshot target = ShieldSnapshot.Empty;
        private Sequence sequence;
        private BattlePresentationOperation operation;
        private AudioSource audioSource;
        private Vector3 Scale => new Vector3(CardSize.x, CardSize.y, 1);

        public void ValidateConfiguration()
        {
            if (CardMesh == null || ShieldMaterial == null || Atlas == null || Atlas.texture == null ||
                ShieldMaterial.shader == null || ShieldMaterial.shader.name != "CardArt/Shield Deferred" ||
                ShieldMaterial.GetTexture("_BaseMap") != Atlas.texture || !ShieldMaterial.enableInstancing ||
                Atlas.cardSize.x <= 0 || Atlas.cardSize.y <= 0)
                throw new InvalidOperationException("盾牌需要绑定 Mesh、图集与启用 Instancing 的 Shield Deferred 共享材质。");
            if (!Finite(CardSize.x) || !Finite(CardSize.y) || CardSize.x <= 0 || CardSize.y <= 0 ||
                !Finite(FlightSeconds) || FlightSeconds < .01f || !Finite(CardInterval) || CardInterval < 0 ||
                !Finite(DissolveSeconds) || DissolveSeconds < .01f || !Finite(DissolveInterval) || DissolveInterval < 0 ||
                !Finite(ArcHeight) || ArcHeight < 0 || !Valid(SpawnCenter) || !Valid(RingCenter) ||
                !Positive(SpawnSize) || !ValidAngleRange(PitchRange) || !ValidAngleRange(YawRange) || !ValidAngleRange(RollRange) ||
                !Finite(OutwardChance) || OutwardChance < 0 || OutwardChance > 1 ||
                !Finite(RingInnerRadius) || RingInnerRadius < 0 || !Finite(RingOuterRadius) || RingOuterRadius < RingInnerRadius ||
                !Finite(LandingHeightVariation) || LandingHeightVariation < 0 ||
                !Finite(ArcCenterAngle) || !Finite(ArcAngle) || ArcAngle < 1 || ArcAngle > 360)
                throw new InvalidOperationException("盾牌区域、尺寸与动画参数必须为有效有限值。");
            Haptics.Validate();
        }
        private static bool ValidAngleRange(Vector2 range) => Finite(range.x) && Finite(range.y) && range.x <= range.y;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Valid(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Positive(Vector3 value) => Valid(value) && value.x >= 0 && value.y >= 0 && value.z >= 0;
        public BattlePresentationOperation Present(ShieldSnapshot snapshot, bool animate, bool hit = false)
        {
            ValidateConfiguration(); ValidateArtwork(snapshot);
            CancelPresentation(); target = snapshot ?? ShieldSnapshot.Empty;
            if (!animate || !Application.isPlaying || !isActiveAndEnabled) { Snap(target); return BattlePresentationOperation.Completed(); }
            operation = new BattlePresentationOperation(); var pending = operation;
            sequence = DOTween.Sequence().SetUpdate(true).SetAutoKill(true).Pause();
            var desired = Desired(target); int removed = 0, added = 0;
            foreach (var visual in cards.Values.ToArray())
                if (!desired.ContainsKey(visual.Key))
                {
                    float delay = removed++ * DissolveInterval;
                    sequence.Insert(delay, DOTween.To(() => visual.Dissolve, value => { visual.Dissolve = value; visual.Apply(); }, 1, DissolveSeconds).SetEase(Ease.Linear));
                }
            foreach (var pair in desired)
            {
                if (cards.ContainsKey(pair.Key)) continue;
                var visual = Create(pair.Key, pair.Value); var start = SpawnCenter + RandomOffset(pair.Key + "/spawn", SpawnSize);
                visual.Root.transform.localPosition = start; visual.Renderer.enabled = false;
                float delay = added++ * CardInterval, progress = 0;
                sequence.InsertCallback(delay, () => { if (pending.keepWaiting && visual.Renderer != null) visual.Renderer.enabled = true; });
                sequence.Insert(delay, DOTween.To(() => progress, value => {
                    progress = value;
                    if (visual.Root == null) return;
                    visual.Root.transform.localPosition = Vector3.LerpUnclamped(start, visual.Position, value) + Vector3.up * (4 * ArcHeight * value * (1 - value));
                    visual.Root.transform.localRotation = Quaternion.Slerp(Quaternion.Euler(25, 0, 0), visual.Rotation, value);
                }, 1, FlightSeconds).SetEase(Ease.OutCubic));
                sequence.InsertCallback(delay + FlightSeconds, () => { if (pending.keepWaiting && visual.Root != null) Land(visual); });
                sequence.Insert(delay + FlightSeconds, DOTween.To(() => visual.Flash, value => { visual.Flash = value; visual.Apply(); }, 0, .14f).From(1, false).SetEase(Ease.OutQuad));
            }
            if (hit)
            {
                foreach (var visual in cards.Values)
                    sequence.Insert(0, visual.Root.transform.DOPunchRotation(new Vector3(-5, 0, 0), .12f, 3, .3f));
                PlaySound(BlockHitSound, BlockHitVolume);
            }
            if (sequence.Duration() <= 0) { sequence.Kill(); sequence = null; FinishTarget(); pending.Complete(); return pending; }
            sequence.OnComplete(() => { sequence = null; FinishTarget(); pending.Complete(); })
                .OnKill(() => { if (pending.keepWaiting) pending.Fail(new InvalidOperationException("Shield animation was interrupted.")); });
            sequence.Play(); return pending;
        }
        private Dictionary<string, ShieldCard> Desired(ShieldSnapshot snapshot)
        {
            var result = new Dictionary<string, ShieldCard>();
            foreach (var batch in snapshot.Batches)
                for (int i = batch.DissolvedCount; i < batch.Cards.Count; i++) result.Add(batch.Id + "/" + i, batch.Cards[i]);
            return result;
        }
        private void ValidateArtwork(ShieldSnapshot snapshot)
        {
            if (snapshot == null) return;
            foreach (var card in snapshot.Batches.SelectMany(batch => batch.Cards))
                if (!Atlas.TryGetCard(card.ArtId, out var region) || region.clipped) throw new ArgumentException("盾牌缺少完整牌面：" + card.ArtId);
        }
        private Visual Create(string key, ShieldCard card)
        {
            Atlas.TryGetCard(card.ArtId, out var region);
            var root = new GameObject("Shield " + card.Id) { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
            root.transform.SetParent(transform, false); root.transform.localScale = Scale;
            root.AddComponent<MeshFilter>().sharedMesh = CardMesh;
            var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = ShieldMaterial;
            var visual = new Visual { Key = key, Id = card.Id, Root = root, Renderer = renderer, Rect = region.uv,
                Noise = RandomOffset(key + "/noise", Vector3.one * 10) };
            SetLandingPose(visual);
            root.transform.localPosition = visual.Position; root.transform.localRotation = visual.Rotation;
            visual.Apply(); cards.Add(key, visual); return visual;
        }
        private void SetLandingPose(Visual visual)
        {
            var sample = RandomOffset(visual.Key + "/ring", Vector3.one) + Vector3.one * .5f;
            float angle = (ArcCenterAngle + (sample.x - .5f) * ArcAngle) * Mathf.Deg2Rad;
            // Square-root sampling distributes positions uniformly across the annular sector area.
            float radius = Mathf.Sqrt(Mathf.Lerp(RingInnerRadius * RingInnerRadius, RingOuterRadius * RingOuterRadius, sample.y));
            var radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            var bottom = RingCenter + radial * radius + Vector3.up * ((sample.z - .5f) * LandingHeightVariation);
            bool faceOut = RandomOffset(visual.Key + "/facing", Vector3.one).x + .5f >= 1 - OutwardChance;
            var angles = RandomOffset(visual.Key + "/angle", Vector3.one) + Vector3.one * .5f;
            visual.Rotation = Quaternion.LookRotation(faceOut ? radial : -radial, Vector3.up) *
                Quaternion.Euler(Mathf.Lerp(PitchRange.x, PitchRange.y, angles.x),
                    Mathf.Lerp(YawRange.x, YawRange.y, angles.y), Mathf.Lerp(RollRange.x, RollRange.y, angles.z));
            visual.Position = bottom + visual.Rotation * (Vector3.up * CardSize.y * .5f);
        }
        public static Vector3 CardBottomPosition(Transform card) => card.TransformPoint(new Vector3(0, -.5f, 0));

        private Vector3 RandomOffset(string key, Vector3 size)
        {
            int hash = Seed; unchecked { foreach (char character in key) hash = hash * 31 + character; }
            var random = new System.Random(hash);
            return Vector3.Scale(new Vector3((float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f), size);
        }
        private void Land(Visual visual)
        {
            // Callback and tween may share a frame; anchor feedback to the exact final pose.
            visual.Root.transform.localPosition = visual.Position;
            visual.Root.transform.localRotation = visual.Rotation;
            visual.Flash = 1; visual.Apply();
            if (LandingEffect != null)
            {
                var effect = Instantiate(LandingEffect, CardBottomPosition(visual.Root.transform), visual.Root.transform.rotation, transform);
                effects.Add(effect.gameObject); effect.Play();
                Destroy(effect.gameObject, Mathf.Max(.1f, effect.main.duration + effect.main.startLifetime.constantMax));
            }
            PlaySound(LandingSound, LandingVolume); CardLanded?.Invoke(visual.Id);
        }
        private void PlaySound(AudioClip clip, float volume)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || clip == null || volume <= 0) return;
            if (audioSource == null)
            {
                var child = new GameObject("Shield Audio") { hideFlags = HideFlags.DontSave }; child.transform.SetParent(transform, false);
                audioSource = child.AddComponent<AudioSource>(); audioSource.playOnAwake = false; audioSource.spatialBlend = 0;
            }
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume)); CardHapticsDriver.Request(this, Haptics);
        }
        public void Snap(ShieldSnapshot snapshot)
        {
            CancelPresentation(); target = snapshot ?? ShieldSnapshot.Empty;
            if (CardMesh == null || ShieldMaterial == null || Atlas == null) { ClearVisuals(); return; }
            ValidateArtwork(target); FinishTarget();
        }
        private void FinishTarget()
        {
            var desired = Desired(target);
            foreach (var key in cards.Keys.Where(key => !desired.ContainsKey(key)).ToArray()) { Release(cards[key].Root); cards.Remove(key); }
            foreach (var pair in desired)
            {
                var visual = cards.TryGetValue(pair.Key, out var existing) ? existing : Create(pair.Key, pair.Value);
                SetLandingPose(visual);
                visual.Dissolve = 0; visual.Flash = 0; visual.Renderer.enabled = true; visual.Apply();
                visual.Root.transform.localPosition = visual.Position; visual.Root.transform.localRotation = visual.Rotation; visual.Root.transform.localScale = Scale;
            }
        }
        public void CancelPresentation()
        {
            operation?.Cancel(); operation = null; sequence?.Kill(); sequence = null;
            if (audioSource != null) audioSource.Stop(); CardHapticsDriver.StopFor(this);
            foreach (var effect in effects) if (effect != null) Release(effect); effects.Clear();
        }
        private void ClearVisuals() { foreach (var visual in cards.Values) Release(visual.Root); cards.Clear(); }
        private static void Release(GameObject value) { if (value == null) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnDisable() { CancelPresentation(); ClearVisuals(); }
        private void OnDestroy() { CancelPresentation(); ClearVisuals(); }
        private void DrawRing(Vector3 center, float radius)
        {
            const int segments = 64;
            for (int i = 0; i < segments; i++)
            {
                float a = (ArcCenterAngle - ArcAngle * .5f + i * ArcAngle / segments) * Mathf.Deg2Rad;
                float b = (ArcCenterAngle - ArcAngle * .5f + (i + 1) * ArcAngle / segments) * Mathf.Deg2Rad;
                Gizmos.DrawLine(center + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius,
                    center + new Vector3(Mathf.Sin(b), 0, Mathf.Cos(b)) * radius);
            }
        }
        private void OnDrawGizmosSelected()
        {
            var old = Gizmos.matrix; Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.cyan; Gizmos.DrawWireCube(SpawnCenter, SpawnSize);
            Gizmos.color = Color.yellow;
            DrawRing(RingCenter, RingInnerRadius); DrawRing(RingCenter, RingOuterRadius);
            if (LandingHeightVariation > 0)
            {
                DrawRing(RingCenter + Vector3.up * LandingHeightVariation * .5f, RingOuterRadius);
                DrawRing(RingCenter - Vector3.up * LandingHeightVariation * .5f, RingOuterRadius);
            }
            foreach (float edge in new[] { -.5f, .5f })
            {
                var radial = Quaternion.Euler(0, ArcCenterAngle + ArcAngle * edge, 0) * Vector3.forward;
                Gizmos.DrawLine(RingCenter + radial * RingInnerRadius, RingCenter + radial * RingOuterRadius);
            }
            var middle = Quaternion.Euler(0, ArcCenterAngle, 0) * Vector3.forward;
            Gizmos.DrawLine(SpawnCenter, RingCenter + middle * ((RingInnerRadius + RingOuterRadius) * .5f));
            Gizmos.matrix = old;
        }
    }
}
