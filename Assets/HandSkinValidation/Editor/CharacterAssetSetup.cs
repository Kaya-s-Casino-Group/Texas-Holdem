using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CharacterAssetSetup
{
    const string Old = "Assets/Art/Models/Characters/HandSkinOptimized";
    const string Folder = "Assets/Art/Models/Characters/PokerCharacter";
    const string Prefab = Folder + "/PokerCharacter.prefab";

    static void Move(string from, string to)
    {
        if (!AssetDatabase.LoadMainAssetAtPath(from))
        {
            if (AssetDatabase.LoadMainAssetAtPath(to)) return;
            throw new Exception("Missing asset: " + from);
        }
        string guid = AssetDatabase.AssetPathToGUID(from);
        string error = AssetDatabase.MoveAsset(from, to);
        if (!string.IsNullOrEmpty(error)) throw new Exception(error);
        if (AssetDatabase.AssetPathToGUID(to) != guid) throw new Exception("GUID changed: " + to);
    }

    [MenuItem("Tools/Hand Skin Validation/10 Organize Assets and Replace Scene Character")]
    public static void Organize()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play mode before replacing scene objects");
        if (AssetDatabase.IsValidFolder(Old)) Move(Old, Folder);
        foreach (string dir in new[] { "Models", "Materials", "Textures" })
            if (!AssetDatabase.IsValidFolder(Folder + "/" + dir)) AssetDatabase.CreateFolder(Folder, dir);
        Move(Folder + "/Character_Standard.prefab", Prefab);
        Move(Folder + "/Character_HandOptimized.prefab", Folder + "/Models/PokerCharacter_Visual.prefab");
        Move(Folder + "/Character_HandOptimized.fbx", Folder + "/Models/PokerCharacter.fbx");
        Move(Folder + "/Character_URP.mat", Folder + "/Materials/PokerCharacter.mat");
        Move(Folder + "/Character_BaseColor.png", Folder + "/Textures/PokerCharacter_BaseColor_Review.png");
        const string textureSource = "/Character_HandOptimized.fbm/";
        Move(Folder + textureSource + "tripo_image_dc21eaf7-4c95-4807-9392-04d2cbe312ed_0_0.jpg", Folder + "/Textures/PokerCharacter_BaseColor.jpg");
        Move(Folder + textureSource + "tripo_image_dc21eaf7-4c95-4807-9392-04d2cbe312ed_0_3.jpg", Folder + "/Textures/PokerCharacter_Normal.jpg");
        Move(Folder + textureSource + "UnityCharacter_metallic.JPEG", Folder + "/Textures/PokerCharacter_Metallic.jpg");
        Move(Folder + textureSource + "UnityCharacter_roughness.JPEG", Folder + "/Textures/PokerCharacter_Roughness.jpg");
        string emptyFolder = Folder + "/Character_HandOptimized.fbm";
        if (AssetDatabase.IsValidFolder(emptyFolder) && !Directory.EnumerateFiles(emptyFolder).Any()) AssetDatabase.DeleteAsset(emptyFolder);
        RenamePrefab(Folder + "/Models/PokerCharacter_Visual.prefab", "PokerCharacter_Visual");
        RenamePrefab(Prefab, "PokerCharacter");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/PokerCharacter.mat");
        material.name = "PokerCharacter"; EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        Replace();
    }

    static void RenamePrefab(string path, string name)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = name;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                r.name = "BodyMesh";
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static string Key(Transform t, Transform root)
    {
        if (t == root) return "";
        string key = AnimationUtility.CalculateTransformPath(t, root);
        return key.Replace("tripo_node_dc21eaf7", "BodyMesh");
    }

    static void Replace()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        var sourceAvatar = asset.GetComponentInChildren<Animator>().avatar;
        if (!sourceAvatar || !sourceAvatar.isValid || !sourceAvatar.isHuman) throw new Exception("Renamed Avatar invalid");
        var candidates = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>(true)
            .Where(r => r.gameObject.scene.IsValid() && !EditorSceneManager.IsPreviewScene(r.gameObject.scene))
            .Where(r => r.sharedMesh && r.sharedMesh.name.Contains("tripo_node_dc21eaf7"))
            .Select(r => r.GetComponentInParent<Animator>()).Where(a => a)
            .Where(a => !a.transform.root.GetComponentsInChildren<Transform>(true).Any(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) == asset))
            .Distinct().ToArray();
        var report = new StringBuilder();
        report.AppendLine("Prefab=" + Prefab + "; Humanoid valid=" + sourceAvatar.isValid);
        var scenes = new HashSet<Scene>();
        var replacements = new List<GameObject>();
        int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Replace with PokerCharacter prefab");
        foreach (var oldAnimator in candidates)
        {
            var old = oldAnimator.gameObject;
            var scene = old.scene;
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            Undo.RegisterCreatedObjectUndo(replacement, "Create PokerCharacter prefab instance");
            replacement.transform.SetParent(old.transform.parent, false);
            replacement.transform.localPosition = old.transform.localPosition;
            replacement.transform.localRotation = old.transform.localRotation;
            replacement.transform.localScale = old.transform.localScale;
            replacement.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
            replacement.SetActive(old.activeSelf);
            replacement.tag = old.tag;
            var animator = replacement.GetComponentInChildren<Animator>(true);
            EditorUtility.CopySerialized(oldAnimator, animator);
            var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            var transforms = animator.GetComponentsInChildren<Transform>(true).ToDictionary(t => Key(t, animator.transform));
            foreach (var oldTransform in old.GetComponentsInChildren<Transform>(true))
            {
                if (!transforms.TryGetValue(Key(oldTransform, old.transform), out var target))
                    throw new Exception("Cannot preserve child reference: " + oldTransform.name);
                if (oldTransform != old.transform)
                {
                    // Keep the current review pose without changing prefab defaults.
                    target.localPosition = oldTransform.localPosition;
                    target.localRotation = oldTransform.localRotation;
                    target.localScale = oldTransform.localScale;
                }
                map[oldTransform] = oldTransform == old.transform ? replacement.transform : target;
                map[oldTransform.gameObject] = oldTransform == old.transform ? replacement : target.gameObject;
                foreach (var component in oldTransform.GetComponents<Component>().Where(c => c && !(c is Transform)))
                {
                    var dest = target.GetComponent(component.GetType());
                    if (!dest)
                    {
                        ComponentUtility.CopyComponent(component);
                        ComponentUtility.PasteComponentAsNew(target.gameObject);
                        dest = target.GetComponents(component.GetType()).Last();
                    }
                    map[component] = dest;
                }
            }
            // Preserve scene links to the character, its Animator, renderer and individual bones.
            foreach (var component in Resources.FindObjectsOfTypeAll<Component>())
            {
                if (!component || !component.gameObject.scene.IsValid() || EditorUtility.IsPersistent(component) || component.transform.IsChildOf(old.transform)) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator(); bool changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_GameObject" || property.name == "m_Script") continue;
                    var reference = property.objectReferenceValue;
                    if (!reference || !map.TryGetValue(reference, out var remapped)) continue;
                    if (!changed) Undo.RecordObject(component, "Reconnect character references");
                    property.objectReferenceValue = remapped; changed = true;
                }
                if (changed) { serialized.ApplyModifiedProperties(); EditorSceneManager.MarkSceneDirty(component.gameObject.scene); }
            }
            foreach (var t in replacement.GetComponentsInChildren<Transform>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            report.AppendLine("Replaced=" + old.name + "; scene=" + scene.path + "; position=" + replacement.transform.position + "; controller=" + animator.runtimeAnimatorController);
            Undo.DestroyObjectImmediate(old);
            scenes.Add(scene); replacements.Add(replacement);
        }
        Undo.CollapseUndoOperations(group);
        foreach (var scene in scenes)
        {
            if (string.IsNullOrEmpty(scene.path)) throw new Exception("Replacement is in an unsaved scene");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        foreach (var instance in replacements)
        {
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance) != Prefab) throw new Exception("Scene instance is not linked to new prefab");
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(instance) != 0) throw new Exception("Missing script on character");
        }
        report.AppendLine("Replaced instances=" + replacements.Count + "; scene references remapped; scene saved");
        Directory.CreateDirectory("Art/HandSkinReview/Unity");
        File.WriteAllText("Art/HandSkinReview/Unity/asset-organization.txt", report.ToString());
        Selection.objects = replacements.Cast<UnityEngine.Object>().ToArray();
        Debug.Log(report.ToString());
    }
}
