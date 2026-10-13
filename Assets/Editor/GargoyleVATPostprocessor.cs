using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The Blender manifest is the commit marker. Preparing assets keeps their GUIDs,
// updates resident mesh buffers, and leaves the scene/player settings alone.
public sealed class GargoyleVATPostprocessor : AssetPostprocessor
{
    const string ToggleMenu = "Tools/Art Assets/Dragon Gargoyle VAT/Auto Import Blender Exports";
    const string Preference = "GargoyleVAT.AutoImportBlenderExports";
    static bool pending, importing;

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (importing || pending || !EditorPrefs.GetBool(Preference, true) ||
            !imported.Contains(GargoyleVATImport.Stem + "_vat.json")) return;
        pending = true;
        EditorApplication.delayCall += ImportWhenReady;
    }

    static void ImportWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += ImportWhenReady;
            return;
        }
        pending = false;
        if (!EditorPrefs.GetBool(Preference, true)) return;
        importing = true;
        try { GargoyleVATImport.Configure(); }
        catch (Exception exception) { Debug.LogError("Blender VAT auto import failed: " + exception.Message); }
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
}
