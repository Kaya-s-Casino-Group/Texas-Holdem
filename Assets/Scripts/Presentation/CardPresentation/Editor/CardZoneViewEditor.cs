#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CardPresentation.Editor
{
    [CustomEditor(typeof(CardZoneView), true), CanEditMultipleObjects]
    public sealed class CardZoneViewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cardPrefab"));
            var layout = serializedObject.FindProperty("layout");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("牌区尺寸", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(layout.FindPropertyRelative("Size"), new GUIContent("区域大小（宽 / 高）"));
            EditorGUILayout.PropertyField(layout.FindPropertyRelative("FitCardsToZone"), new GUIContent("卡牌随区域缩放"));
            EditorGUILayout.HelpBox("开启后，调整区域高度即可等比缩放卡牌，宽度控制排布。Scene 中可拖动青色边缘手柄；出牌区自动适配五个槽位。", MessageType.None);
            EditorGUILayout.PropertyField(layout.FindPropertyRelative("Padding"));
            EditorGUILayout.PropertyField(layout.FindPropertyRelative("PreferredGap"));
            EditorGUILayout.PropertyField(layout.FindPropertyRelative("OverlapLift"));
            EditorGUILayout.PropertyField(layout.FindPropertyRelative("ArcHeight"));
            EditorGUILayout.PropertyField(layout.FindPropertyRelative("Alignment"));
            DrawPropertiesExcluding(serializedObject, "m_Script", "cardPrefab", "layout");
            if (serializedObject.ApplyModifiedProperties())
            {
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
        }

        private void OnSceneGUI()
        {
            var zone = target as CardZoneView;
            if (zone == null) return;
            var size = zone.Layout.Size;
            using (new Handles.DrawingScope(Color.cyan, zone.transform.localToWorldMatrix))
            {
                EditorGUI.BeginChangeCheck();
                float width = DragEdge(Vector3.right, size.x);
                float height = DragEdge(Vector3.forward, size.y);
                if (EditorGUI.EndChangeCheck())
                {
                    // Scene GUI runs once per target; keep handle edits scoped to that zone.
                    using (var zoneObject = new SerializedObject(zone))
                    {
                        zoneObject.FindProperty("layout").FindPropertyRelative("Size").vector2Value = new Vector2(width, height);
                        zoneObject.ApplyModifiedProperties();
                    }
                    EditorApplication.QueuePlayerLoopUpdate();
                }
            }
        }

        private static float DragEdge(Vector3 axis, float extent)
        {
            var edge = axis * (extent * .5f);
            var moved = Handles.Slider(edge, axis, HandleUtility.GetHandleSize(edge) * .06f, Handles.CubeHandleCap, 0);
            return Mathf.Max(.0001f, Vector3.Dot(moved, axis) * 2);
        }
    }
}
#endif
