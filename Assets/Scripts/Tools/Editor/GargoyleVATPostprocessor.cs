using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// The Blender manifest is the commit marker. Preparing assets keeps their GUIDs,
// updates resident mesh buffers, and leaves the scene/player settings alone.
public sealed class GargoyleVATPostprocessor : AssetPostprocessor
{
    const string ToggleMenu = "工具/场景美术/石像鬼/顶点动画/自动导入 Blender 导出";
    const string Preference = "GargoyleVAT.AutoImportBlenderExports";
    static bool pending, importing;
    static readonly HashSet<string> manifests = new HashSet<string>();

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (!EditorPrefs.GetBool(Preference, true)) return;
        foreach (string path in imported.Where(p=>p.EndsWith("_vat.json")))
        {
            try { if ((string)JObject.Parse(File.ReadAllText(path))["schema"]=="statue-rigid-vat-v1") manifests.Add(path); }
            catch (Exception) { /* A JSON import elsewhere is not necessarily our VAT schema. */ }
        }
        if (pending || manifests.Count==0) return;
        pending = true;
        EditorApplication.delayCall += ImportWhenReady;
    }

    static void ImportWhenReady()
    {
        if (importing || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += ImportWhenReady;
            return;
        }
        pending = false;
        if (!EditorPrefs.GetBool(Preference, true)) return;
        importing = true;
        var work=manifests.ToArray();manifests.Clear();
        try
        {
            foreach(string manifest in work)
            {
                try { RigidVATAssetImport.Configure(manifest); }
                catch (Exception exception) { Debug.LogError("Blender VAT auto import failed for "+manifest+": "+exception.Message); }
            }
        }
        finally { importing = false; }
    }

    [MenuItem(ToggleMenu)]
    static void Toggle() => EditorPrefs.SetBool(Preference, !EditorPrefs.GetBool(Preference, true));

    [MenuItem(ToggleMenu, true)]
    static bool ShowToggle()
    {
        Menu.SetChecked(ToggleMenu, EditorPrefs.GetBool(Preference, true));
        return true;
    }

    [MenuItem("工具/场景美术/雕像顶点动画/自动导入 Blender 导出")]
    static void ToggleGeneric() => Toggle();

    [MenuItem("工具/场景美术/雕像顶点动画/自动导入 Blender 导出",true)]
    static bool ShowGenericToggle()
    {
        Menu.SetChecked("工具/场景美术/雕像顶点动画/自动导入 Blender 导出",EditorPrefs.GetBool(Preference,true));
        return true;
    }
}
