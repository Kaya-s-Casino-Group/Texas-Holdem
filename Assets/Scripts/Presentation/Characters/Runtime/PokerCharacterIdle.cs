using UnityEngine;

/// <summary>Schedules the gambler's idle beats; call NotifyChipsHeard from the game's sound/event logic.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class PokerCharacterIdle : MonoBehaviour
{
    [SerializeField] SkinnedMeshRenderer face;
    [SerializeField] Transform lastChip;
    [SerializeField] bool automaticVariations = true;
    [SerializeField] Vector2 variationInterval = new Vector2(13, 23);
    [SerializeField, Range(0, 35)] float restingEyelids = 12;
    [SerializeField] int randomSeed = 173;
    Animator animator;
    Transform thumb, index, hand;
    System.Random random;
    float nextBeat, nextBlink, blinkStart = -10, elapsed;
    int blinkL = -1, blinkR = -1;
    int previousBeat;
    const float BlinkDuration = .24f;

    void OnEnable() { if(Application.isPlaying) Initialize(); }
    void Initialize()
    {
        elapsed=0; blinkStart=-10;
        animator = GetComponent<Animator>(); random = new System.Random(randomSeed ^ GetInstanceID());
        if (!face) face = GetComponentInChildren<SkinnedMeshRenderer>();
        if (face && face.sharedMesh)
        { blinkL = face.sharedMesh.GetBlendShapeIndex("Blink_L"); blinkR = face.sharedMesh.GetBlendShapeIndex("Blink_R"); }
        thumb = animator.GetBoneTransform(HumanBodyBones.RightThumbDistal);
        index = animator.GetBoneTransform(HumanBodyBones.RightIndexDistal);
        hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if(thumb && thumb.childCount>0)thumb=thumb.GetChild(0);
        if(index && index.childCount>0)index=index.GetChild(0);
        nextBlink = Range(2.5f, 5.5f); nextBeat = Range(variationInterval.x, variationInterval.y);
    }
    float Range(float min, float max)
    {
        if(random==null)random=new System.Random(randomSeed ^ GetInstanceID());
        return min + (float)random.NextDouble()*(max-min);
    }
    void Update()
    {
        if(!Application.isPlaying)return;
        if(!animator || random==null)Initialize();
        elapsed += Time.deltaTime;
        if (elapsed >= nextBlink)
        { blinkStart = elapsed; nextBlink = elapsed + Range(3.2f, 6.7f); }
        if (!automaticVariations || elapsed < nextBeat || animator.IsInTransition(0)) return;
        if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")) return;
        // Collar/secret is deliberately much rarer than the other two beats.
        int roll = random.Next(100);
        int beat = roll < 48 ? 1 : roll < 88 ? 2 : 3;
        if (beat == previousBeat) beat = beat == 1 ? 2 : 1;
        previousBeat = beat;
        animator.SetTrigger(beat == 1 ? "Listen" : beat == 2 ? "LastChip" : "Tarot");
        nextBeat = elapsed + Range(variationInterval.x, variationInterval.y) + (beat == 3 ? 9 : 6);
    }
    void LateUpdate()
    {
        float t = (elapsed-blinkStart)/BlinkDuration;
        float blink = t >= 0 && t <= 1 ? Mathf.Sin(Mathf.PI*t) : 0;
        float weight = Mathf.Lerp(restingEyelids,100,blink);
        if (face)
        {
            if (blinkL >= 0) face.SetBlendShapeWeight(blinkL,weight);
            if (blinkR >= 0) face.SetBlendShapeWeight(blinkR,weight);
        }
        if (!lastChip || !thumb || !index || !hand) return;
        // Follow the fingers after animation evaluation so the chip cannot lag behind the grip.
        var state=animator.GetCurrentAnimatorStateInfo(0);
        bool stopped = state.IsName("Listen") || state.IsName("LastChip");
        float phase = stopped ? 0 : Mathf.Sin(elapsed*1.65f)*16;
        Vector3 fingerPoint=(thumb.position+index.position)*.5f;
        Vector3 normal=Vector3.Cross(index.position-hand.position,thumb.position-hand.position).normalized;
        lastChip.position=fingerPoint+normal*.002f;
        lastChip.rotation=hand.rotation*Quaternion.Euler(80,phase,20);
    }
    public void NotifyChipsHeard()
    {
        if (!animator || !animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")) return;
        animator.SetTrigger("Listen"); nextBeat=elapsed+Range(18,28);
    }
    public void PlayLastChipBeat() { if (animator) animator.SetTrigger("LastChip"); }
    public void PlaySecretBeat() { if (animator) animator.SetTrigger("Tarot"); }
    public void Configure(SkinnedMeshRenderer renderer, Transform chip) { face=renderer; lastChip=chip; }
    void OnDisable()
    {
        if (!face) return;
        if (blinkL >= 0) face.SetBlendShapeWeight(blinkL,0);
        if (blinkR >= 0) face.SetBlendShapeWeight(blinkR,0);
    }
}
