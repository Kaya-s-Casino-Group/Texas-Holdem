using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ForestRuins.Editor
{
    public class ForestRuinsShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            EditorGUI.BeginChangeCheck();
            base.OnGUI(editor, properties);
            EditorGUILayout.HelpBox("Hand Painted Fill 只影响显示，不产生烘焙光。Physical Emission for Baking 才作为烘焙光源。Meta 使用静止网格，并保留 Alpha Cutoff 和叶片顶点色。", MessageType.Info);
            if (EditorGUI.EndChangeCheck())
                foreach (Material material in editor.targets) ValidateMaterial(material);
        }
        public override void ValidateMaterial(Material material)
        {
            bool alpha = material.GetFloat("_Cutoff") > 0;
            material.SetOverrideTag("RenderType", alpha ? "TransparentCutout" : "Opaque");
            material.renderQueue = alpha ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            material.doubleSidedGI = true;
            material.globalIlluminationFlags = material.GetFloat("_BakeEmission") > 0
                ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
    }
}
