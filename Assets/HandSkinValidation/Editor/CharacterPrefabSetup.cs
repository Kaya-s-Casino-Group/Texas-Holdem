using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CharacterPrefabSetup
{
    const string Folder = "Assets/Art/Models/Characters/PokerCharacter";
    const string Destination = Folder + "/PokerCharacter.prefab";
    const string Report = "Art/HandSkinReview/Unity/prefab-validation.txt";

    // Use baked vertices, not the importer's conservative animation bounds, to find the soles.
    static Bounds Geometry(GameObject model)
    {
        bool first = true;
        var bounds = new Bounds();
        foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh = new Mesh();
            try
            {
                renderer.BakeMesh(mesh, true);
                foreach (var vertex in mesh.vertices)
                {
                    var p = model.transform.root.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
        if (first) throw new Exception("Character has no skinned vertices");
        return bounds;
    }

    [MenuItem("Tools/Hand Skin Validation/8 Build Standard Character Prefab")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("PokerCharacter");
            SceneManager.MoveGameObjectToScene(root, scene);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Models/PokerCharacter_Visual.prefab"), scene);
            model.name = "Visual";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            var animator = model.GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid)
                throw new Exception("Source Humanoid Avatar is invalid");
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var footCenter = (left.position + right.position) * .5f;
            var before = Geometry(model);
            if (before.size.y < .2f || before.size.y > 3f) throw new Exception("Unexpected character height: " + before.size.y);
            model.transform.localPosition = new Vector3(-footCenter.x, -before.min.y, -footCenter.z);
            int layer = LayerMask.NameToLayer("Characters");
            if (layer < 0) throw new Exception("Missing Characters layer");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/PokerCharacter.mat");
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            { t.gameObject.layer = layer; t.gameObject.isStatic = false; }
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                renderer.updateWhenOffscreen = false;
                renderer.quality = SkinQuality.Auto;
            }
            PrefabUtility.SaveAsPrefabAsset(root, Destination);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        AssetDatabase.SaveAssets();
        Validate();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(Destination);
        EditorGUIUtility.PingObject(Selection.activeObject);
        Debug.Log("Standard character prefab saved: " + Destination);
    }

    [MenuItem("Tools/Hand Skin Validation/9 Validate Standard Character Prefab")]
    public static void Validate()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Destination), scene);
            var animator = root.GetComponentInChildren<Animator>();
            if (root.transform.position != Vector3.zero || root.transform.rotation != Quaternion.identity || root.transform.localScale != Vector3.one)
                throw new Exception("Root transform is not normalized");
            if (!animator.avatar.isValid || !animator.avatar.isHuman) throw new Exception("Invalid Humanoid");
            var bounds = Geometry(root);
            var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var center = (left.position + right.position) * .5f;
            if (Mathf.Abs(bounds.min.y) > .0001f || Mathf.Abs(center.x) > .0001f || Mathf.Abs(center.z) > .0001f)
                throw new Exception("Foot pivot alignment failed: " + bounds.min + ", foot center=" + center);
            int hands = Enumerable.Range((int)HumanBodyBones.LeftThumbProximal, 30).Count(i => animator.GetBoneTransform((HumanBodyBones)i));
            if (hands != 30) throw new Exception("Missing Humanoid finger joints: " + hands);
            var sb = new StringBuilder();
            sb.AppendLine("Prefab=" + Destination);
            sb.AppendLine("Root position=(0,0,0); rotation=(0,0,0); scale=(1,1,1)");
            sb.AppendLine("Up=+Y; Forward=+Z; Units=meters; original character size preserved");
            sb.AppendLine("Geometry bounds=" + bounds.ToString("F6"));
            sb.AppendLine("Sole height=" + bounds.min.y.ToString("F8") + "; foot midpoint=" + center.ToString("F6"));
            sb.AppendLine("Humanoid valid=" + animator.avatar.isValid + "; fingers=" + hands);
            // Retarget representative hand poses through the same Avatar used by the saved prefab.
            using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
            {
                var pose = new HumanPose(); handler.GetHumanPose(ref pose);
                var neutral = (float[])pose.muscles.Clone();
                foreach (float curl in new[] { -.65f, 0f, .65f })
                {
                    Array.Copy(neutral, pose.muscles, neutral.Length);
                    for (int i = 0; i < HumanTrait.MuscleCount; i++)
                        if (HumanTrait.MuscleName[i].Contains("Stretched")) pose.muscles[i] = curl;
                    handler.SetHumanPose(ref pose);
                    var posed = Geometry(root);
                    if (float.IsNaN(posed.size.x) || posed.size.magnitude > bounds.size.magnitude * 2)
                        throw new Exception("Humanoid pose produced invalid geometry");
                    sb.AppendLine("Finger curl=" + curl + "; baked geometry valid");
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, sb.ToString());
            Debug.Log(sb.ToString());
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
