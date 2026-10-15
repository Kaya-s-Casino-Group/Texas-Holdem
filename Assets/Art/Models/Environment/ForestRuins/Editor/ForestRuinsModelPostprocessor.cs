using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ForestRuins.Editor
{
    // Imported mesh bounds persist in builds; a renderer-only override does not.
    public class ForestRuinsModelPostprocessor : AssetPostprocessor
    {
        public override uint GetVersion() => 4;
        void OnPreprocessModel()
        {
            if (assetPath.StartsWith(ForestRuinsAssetImport.Folder + "/Models/") && ForestRuinsPackedUV.HasCache(assetPath))
            {
                context.DependsOnSourceAsset(ForestRuinsPackedUV.CachePath(assetPath));
                ((ModelImporter)assetImporter).generateSecondaryUV = false;
            }
        }
        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(ForestRuinsAssetImport.Folder + "/Models/")) return;
            ForestRuinsPackedUV.Apply(assetPath, root);
            var processed = new HashSet<Mesh>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.name.StartsWith("SM_FR_Bush") || !processed.Add(filter.sharedMesh)) continue;
                var bounds = filter.sharedMesh.bounds;
                bounds.Expand(.6f);
                filter.sharedMesh.bounds = bounds;
            }
        }
    }
}
