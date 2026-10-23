using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Poker.EditorTools
{
    public static class PokerBuildAssetOptimizer
    {
        public const string AdventureScenePath = "Assets/Scenes/SingleChapterAdventure.unity";
        private static readonly string[] EliteLayers = { "StylizedRuins Backdrop", "StylizedRuins Midground",
            "StylizedRuins Foreground", "StylizedRuins Pillars", "StylizedRuins Ground Card" };
        private static readonly string[] MinionLayers = { "Rear", "Middle", "Foreground", "Floor - Camera Projection" };

        [Serializable] public sealed class TextureState
        {
            public int width, height, configuredMaxSize, mipCount;
            public string format, configuredFormat, npotScale;
            public bool readable, standaloneOverride, mipmapEnabled;
        }
        [Serializable] public sealed class TextureChange
        {
            public string path, outcome;
            public bool settingsChanged;
            public int maxSize;
            public TextureState before, after;
        }
        [Serializable] public sealed class TextureReport
        {
            public string target, generatedUtc;
            public List<TextureChange> textures = new List<TextureChange>();
        }

        [MenuItem("工具/构建发布/macOS/优化发布资源")]
        public static void OptimizeFromMenu()
        {
            var scene = SceneManager.GetSceneByPath(AdventureScenePath);
            RequireAdventureScene(scene);
            string reportPath = "output/build-asset-optimization/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json";
            var removed = RemoveRetiredBackgrounds(scene);
            OptimizeStandaloneTextures(reportPath);
            if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save adventure scene.");
            Debug.Log("Release resources optimized; removed old background roots: " + removed.Count + "; report: " + reportPath);
        }

        public static TextureReport OptimizeStandaloneTextures(string reportPath)
        {
            RequireEditMode();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX &&
                EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 &&
                EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneLinux64)
                throw new InvalidOperationException("Select a Standalone build target before validating imported desktop textures.");
            var report = new TextureReport { target = EditorUserBuildSettings.activeBuildTarget.ToString(),
                generatedUtc = DateTime.UtcNow.ToString("O") };
            if (string.IsNullOrWhiteSpace(reportPath)) throw new ArgumentException("A report path is required.", nameof(reportPath));
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            try
            {
                foreach (var path in PokerBuildAssetPolicy.Paths.OrderBy(p => p, StringComparer.Ordinal))
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) throw new InvalidOperationException("Reviewed artwork is missing: " + path);
                    PokerBuildAssetPolicy.TryGetRule(path, out var rule);
                    var entry = new TextureChange { path = path, maxSize = rule.MaxSize, before = Capture(path) };
                    report.textures.Add(entry);
                    entry.settingsChanged = PokerBuildAssetPolicy.ApplyStandalone(importer);
                    if (entry.settingsChanged) importer.SaveAndReimport();
                    entry.after = Capture(path);
                    if (entry.after.width > rule.MaxSize || entry.after.height > rule.MaxSize)
                        throw new InvalidOperationException("Texture max size was not applied: " + path);
                    if (entry.before.readable != entry.after.readable)
                        throw new InvalidOperationException("CPU texture access changed: " + path);
                    if (rule.DisableMipmaps && (entry.after.mipmapEnabled || entry.after.mipCount != 1 ||
                        entry.after.format != TextureFormat.BC7.ToString()))
                        throw new InvalidOperationException("Measured no-mip artwork must import as actual BC7 without a mip chain: " + path);
                    // Unity may retain RGBA32 for NPOT sprites. Record the actual outcome, never claim BC7 by settings alone.
                    entry.outcome = entry.after.format == TextureFormat.BC7.ToString() ? "BC7" :
                        "Size limit applied; actual format " + entry.after.format + " (NPOT/format fallback; source aspect preserved)";
                }
            }
            finally { File.WriteAllText(reportPath, JsonUtility.ToJson(report, true)); }
            return report;
        }

        private static TextureState Capture(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("Texture could not be loaded: " + path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var platform = importer.GetPlatformTextureSettings("Standalone");
            return new TextureState { width = texture.width, height = texture.height,
                format = texture.format.ToString(), readable = texture.isReadable,
                configuredMaxSize = platform.maxTextureSize, configuredFormat = platform.format.ToString(),
                standaloneOverride = platform.overridden, mipCount = texture.mipmapCount,
                mipmapEnabled = importer.mipmapEnabled, npotScale = importer.npotScale.ToString() };
        }

        // This modifies only the loaded release scene. Source materials/images and other scenes remain intact.
        // The caller controls saving; Undo can restore the removed scene objects during an interactive run.
        public static List<string> RemoveRetiredBackgrounds(Scene scene)
        {
            RequireAdventureScene(scene);
            var backgrounds = scene.GetRootGameObjects().SingleOrDefault(root => root.name == "Battle Backgrounds");
            if (backgrounds == null) throw new InvalidOperationException("Missing Battle Backgrounds root.");
            var targets = CollectRetiredBackgrounds(backgrounds.transform);
            ValidateNoExternalReferences(scene, targets);
            var paths = targets.Select(t => "Battle Backgrounds/" + t.parent.name + "/" + t.name).ToList();
            foreach (var target in targets) Undo.DestroyObjectImmediate(target.gameObject);
            if (targets.Count != 0) EditorSceneManager.MarkSceneDirty(scene);
            return paths;
        }

        public static List<Transform> CollectRetiredBackgrounds(Transform backgrounds)
        {
            if (backgrounds == null || backgrounds.name != "Battle Backgrounds")
                throw new InvalidOperationException("Expected the authored Battle Backgrounds root.");
            var targets = new List<Transform>();
            Collect("Elite - Existing Ruins", EliteLayers);
            Collect("Minion - Red Wastes", MinionLayers);
            return targets;

            void Collect(string parentName, string[] knownLayers)
            {
                var root = backgrounds.Find(parentName);
                if (root == null) throw new InvalidOperationException("Missing background group: " + parentName);
                var replacement = root.Find("Updated Artwork");
                if (replacement == null || !replacement.gameObject.activeSelf || replacement.GetComponent<Renderer>() == null)
                    throw new InvalidOperationException("Keep old background until replacement artwork is ready: " + parentName);
                foreach (var name in knownLayers)
                {
                    var child = root.Find(name);
                    if (child == null) continue; // Already cleaned.
                    if (child.gameObject.activeSelf)
                        throw new InvalidOperationException("Refusing to remove an active background layer: " + parentName + "/" + name);
                    targets.Add(child);
                }
            }
        }

        private static void ValidateNoExternalReferences(Scene scene, List<Transform> targets)
        {
            var removed = new HashSet<Object>();
            foreach (var target in targets)
                foreach (var transform in target.GetComponentsInChildren<Transform>(true))
                {
                    removed.Add(transform.gameObject);
                    foreach (var component in transform.GetComponents<Component>()) if (component != null) removed.Add(component);
                }
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component is Transform || removed.Contains(component)) continue;
                    using var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference &&
                            property.objectReferenceValue != null && removed.Contains(property.objectReferenceValue))
                            throw new InvalidOperationException("Background is still referenced by " + component.name + "/" + property.propertyPath);
                }
        }

        private static void RequireAdventureScene(Scene scene)
        {
            RequireEditMode();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != AdventureScenePath)
                throw new InvalidOperationException("Open SingleChapterAdventure in Edit Mode first.");
        }
        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Exit Play Mode and wait for compilation before optimizing build resources.");
        }
    }
}
