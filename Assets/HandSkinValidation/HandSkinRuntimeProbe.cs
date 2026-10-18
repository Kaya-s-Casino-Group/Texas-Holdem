using System;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed class HandSkinRuntimeProbe : MonoBehaviour
{
    Animator target;
    Transform[] fingers;
    Quaternion[] first;
    float[] angles;
    float elapsed;
    int frames;
    SkinWeights previousWeights;
    bool finished;
    void Start()
    {
        target = GetComponent<Animator>();
        fingers = Enumerable.Range((int)HumanBodyBones.LeftThumbProximal, 30).Select(i => target.GetBoneTransform((HumanBodyBones)i)).ToArray();
        first = fingers.Select(t => t.localRotation).ToArray(); angles = new float[30];
        previousWeights = QualitySettings.skinWeights; QualitySettings.skinWeights = SkinWeights.Unlimited;
    }
    void LateUpdate()
    {
        if (finished || fingers == null) return;
        elapsed += Time.unscaledDeltaTime; frames++;
        for (int i = 0; i < 30; i++) angles[i] = Mathf.Max(angles[i], Quaternion.Angle(first[i], fingers[i].localRotation));
        if (elapsed < 8.2f) return;
        finished = true;
        string report = "PlayMode elapsed=" + elapsed + "; frames=" + frames + "; isHuman=" + target.isHuman +
            "; moving finger joints=" + angles.Count(a => a > 1) + "/30\n" +
            string.Join("\n", fingers.Select((t, i) => t.name + ": " + angles[i].ToString("F2") + " degrees"));
        #if UNITY_EDITOR
        Directory.CreateDirectory("Art/HandSkinReview/Unity");
        File.WriteAllText("Art/HandSkinReview/Unity/playmode-validation.txt", report);
        #endif
        Debug.Log("HandSkin runtime completed: " + report);
    }
    void OnDestroy() { if (fingers != null) QualitySettings.skinWeights = previousWeights; }
}
