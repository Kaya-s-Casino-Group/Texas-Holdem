using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class PalamedesAnimationLibrary
{
    internal const string SourceFolder="Assets/AnimationPack/Animations";
    internal static bool IsInplace(AnimationClip clip) => AssetDatabase.GetAssetPath(clip).IndexOf("/Inplace/",StringComparison.OrdinalIgnoreCase)>=0;
    internal static string Label(AnimationClip clip) => clip.name+(IsInplace(clip)?" · Inplace":"");
    internal static AnimationClip DefaultClip(AnimationClip[] clips) => clips.FirstOrDefault(c=>c.name=="move_Ground_walk_front"&&IsInplace(c))
        ??clips.FirstOrDefault(c=>c.name.Contains("walk_front"))??clips.FirstOrDefault();
    internal static AnimationClip[] FindClips(string folder)
    {
        if(!AssetDatabase.IsValidFolder(folder))return Array.Empty<AnimationClip>();
        return AssetDatabase.FindAssets("t:AnimationClip",new[]{folder})
            .Select(AssetDatabase.GUIDToAssetPath).Distinct()
            .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>()
            .Where(c=>!c.name.StartsWith("__preview__",StringComparison.Ordinal)&&c.isHumanMotion)
            .OrderBy(c=>c.name,StringComparer.OrdinalIgnoreCase).ThenBy(AssetDatabase.GetAssetPath).ToArray();
    }
    internal static Animator FindTarget()
    {
        var selected=Selection.activeGameObject;
        var candidate=selected?selected.GetComponentInParent<Animator>():null;
        if(candidate&&!EditorUtility.IsPersistent(candidate)&&candidate.avatar&&candidate.avatar.isHuman)return candidate;
        return UnityEngine.Object.FindObjectsOfType<Animator>().FirstOrDefault(a=>a.name=="Palamedes"&&(a.hideFlags&HideFlags.DontSave)==0);
    }
}
