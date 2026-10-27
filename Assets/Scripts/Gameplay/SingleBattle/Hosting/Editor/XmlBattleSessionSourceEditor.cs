using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PokerEngine;
using UnityEditor;
using UnityEngine;

namespace Poker.BattleHosting.Editor
{
    [CustomEditor(typeof(XmlBattleSessionSource))]
    public sealed class XmlBattleSessionSourceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("绑定单局入口 XML 和 Encounter Id 后，收集 Import 依赖。依赖会序列化为 TextAsset，运行时无需文件系统。修改 Import 后请重新收集。", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                if (GUILayout.Button("收集并校验 XML 依赖"))
                {
                    try { CollectImports((XmlBattleSessionSource)target); Debug.Log("XML 依赖已收集并通过校验。", target); }
                    catch (Exception error) { Debug.LogError(error.Message, target); }
                }
        }

        public static void CollectImports(XmlBattleSessionSource source)
        {
            if (source.ContentXml == null) throw new InvalidOperationException("先绑定入口 XML。");
            string assetPath = AssetDatabase.GetAssetPath(source.ContentXml);
            if (string.IsNullOrEmpty(assetPath)) throw new InvalidOperationException("入口必须是项目中的 TextAsset。");
            string directory = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string entry = source.ContentXml.name + ".xml";
            var dependencies = new Dictionary<string, TextAsset>(StringComparer.Ordinal);
            var compiled = BattleContentCompiler.CompileFiles(entry, path =>
            {
                if (path == entry) return source.ContentXml.text;
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(directory + "/" + path);
                if (asset == null) return null;
                dependencies[path] = asset;
                return asset.text;
            });
            if (!compiled.Success) throw new InvalidOperationException(string.Join("\n", compiled.Diagnostics.Select(d =>
                d.Location.SourceName + ":" + d.Location.Line + " " + d.Code + " " + d.Message)));
            if (!compiled.Content.Encounters.ContainsKey(source.EncounterId))
                throw new InvalidOperationException("入口中不存在 Encounter：" + source.EncounterId);
            Undo.RecordObject(source, "Collect Battle XML Imports");
            source.Configure(source.ContentXml, source.EncounterId, dependencies.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new BattleXmlModule { Path = p.Key, Asset = p.Value }).ToArray());
            EditorUtility.SetDirty(source);
            PrefabUtility.RecordPrefabInstancePropertyModifications(source);
        }
    }
}
