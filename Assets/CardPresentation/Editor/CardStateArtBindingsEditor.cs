using System;
using CardArt;
using UnityEditor;
using UnityEngine;
using PokerEngine;

namespace CardPresentation.Editor
{
    [CustomEditor(typeof(CardStateArtBindings))]
    public sealed class CardStateArtBindingsEditor : UnityEditor.Editor
    {
        private CardEffects previewEffects;
        private bool previewDisabled;
        private CardArtProfile previewArt;
        [MenuItem("玩法配置/卡牌状态美术映射")]
        public static void Open() { Selection.activeObject = CardStateArtBindings.Standard; EditorGUIUtility.PingObject(Selection.activeObject); }
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("玩法侧决定卡牌状态对应哪些美术组合。所有匹配规则按列表顺序叠加；同名参数后续覆盖或乘算。隐藏牌只使用美术基础组合。",MessageType.Info);
            serializedObject.Update(); EditorGUILayout.PropertyField(serializedObject.FindProperty("rules"),true); serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space(); EditorGUILayout.LabelField("状态映射检查",EditorStyles.boldLabel);
            previewEffects=(CardEffects)EditorGUILayout.MaskField("卡牌效果",(int)previewEffects,new[]{"强化","塑封","失效"});
            previewDisabled=EditorGUILayout.Toggle("禁用",previewDisabled);
            if(previewArt==null) previewArt=AssetDatabase.LoadAssetAtPath<CardArtProfile>(CardArtworkSetup.ProfilePath);
            previewArt=(CardArtProfile)EditorGUILayout.ObjectField("美术组合配置",previewArt,typeof(CardArtProfile),false);
            try
            {
                var ids=((CardStateArtBindings)target).Select(new CardVisualData("preview","spades_a",effects:previewEffects,isDisabled:previewDisabled));
                EditorGUILayout.LabelField("叠加顺序",ids.Length==0?"基础组合":string.Join(" → ",ids));
                if(previewArt!=null) previewArt.Resolve(ids,Color.white);
            }
            catch(Exception exception) { EditorGUILayout.HelpBox(exception.Message,MessageType.Error); }
        }
    }
}
