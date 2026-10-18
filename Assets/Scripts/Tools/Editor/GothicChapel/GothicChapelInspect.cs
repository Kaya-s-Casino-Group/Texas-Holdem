using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GothicChapel.Editor
{
    public static class GothicChapelInspect
    {
        [MenuItem("Tools/Scene Art/Gothic Chapel/Report Shader Errors")]
        public static void ShaderErrors()
        {
            var shader = Shader.Find(GothicChapelImport.ShaderName);
            var messages = shader ? string.Join("\n", ShaderUtil.GetShaderMessages(shader).Select(m => m.severity + " " + m.file + ":" + m.line + " " + m.message)) : "Shader not found";
            File.WriteAllText(GothicChapelImport.ReportFolder + "/shader-messages.txt", messages);
            Debug.Log(messages);
        }
        [MenuItem("Tools/Scene Art/Gothic Chapel/Inspect and Back Up Current Scene")]
        public static void Inspect()
        {
            const string output = "output/gothic-scene/unity-export";
            Directory.CreateDirectory(output);
            var scene = SceneManager.GetActiveScene();
            // Save a copy of the live scene, including unsaved user changes.
            var backup = output + "/BeforeGothicImport_" + scene.name + ".unity";
            if (!File.Exists(backup) && !EditorSceneManager.SaveScene(scene, backup, true))
                throw new System.IO.IOException("Could not back up the live scene.");
            var pipeline = GraphicsSettings.currentRenderPipeline;
            var cameras = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Camera>(true));
            var details = string.Join("\n", cameras.Select(c => c.name + " renderer=" + c.GetUniversalAdditionalCameraData().scriptableRenderer.GetType().Name));
            File.WriteAllText(output + "/pipeline.txt", "Scene: " + scene.path + "\nPipeline: " + AssetDatabase.GetAssetPath(pipeline) + "\n" + details);
            Debug.Log("Gothic Chapel: backed up live scene; pipeline=" + AssetDatabase.GetAssetPath(pipeline) + "\n" + details);
        }
    }
}
