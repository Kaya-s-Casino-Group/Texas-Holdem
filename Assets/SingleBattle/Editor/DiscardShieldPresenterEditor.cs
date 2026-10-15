using UnityEditor;
using UnityEngine;

namespace Poker.SingleBattle.Editor
{
    [CustomEditor(typeof(DiscardShieldPresenter))]
    public sealed class DiscardShieldPresenterEditor : UnityEditor.Editor
    {
        private readonly BoxBoundsHandleWithLabel spawn = new BoxBoundsHandleWithLabel(Color.cyan, "飞入起点");
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var size = ((DiscardShieldPresenter)target).CardSize;
            EditorGUILayout.LabelField("实际尺寸（宽 × 高，图集比例）", $"{size.x:0.###} × {size.y:0.###}");
            EditorGUILayout.HelpBox("青色为实例化起点，黄色为围绕 Actor 的圆弧落地区域，表示卡牌底部位置。内外半径可拖动；Arc Angle 控制弧长角度，Arc Center Angle 控制方向，默认前方 60 度；卡牌随机朝内或朝外。尺寸为该区域局部单位；位于角色根节点下，不跟随 Visual 的前冲和受击晃动。每张落地触发一次闪光及位于卡牌底部的可选粒子；只有实际播放音效才触发震动。", MessageType.Info);
            if (!Application.isPlaying)
            {
                var view = (DiscardShieldPresenter)target;
                if (GUILayout.Button("预览三张盾牌（不保存实例）"))
                    view.Snap(new ShieldSnapshot(new[] { new ShieldBatch("preview", 24, 24, new[] {
                        new ShieldCard("preview1", "hearts_q"), new ShieldCard("preview2", "spades_a"), new ShieldCard("preview3", "diamonds_k") }) }));
                if (GUILayout.Button("清除预览")) view.Snap(ShieldSnapshot.Empty);
            }
        }
        private void OnSceneGUI()
        {
            var view = (DiscardShieldPresenter)target;
            using (new Handles.DrawingScope(view.transform.localToWorldMatrix))
            {
                spawn.Draw(view, ref view.SpawnCenter, ref view.SpawnSize);
                Handles.color = Color.yellow;
                Handles.Label(view.RingCenter, "圆弧落地区域（卡牌底部）");
                EditorGUI.BeginChangeCheck();
                var center = Handles.PositionHandle(view.RingCenter, Quaternion.identity);
                var direction = Quaternion.Euler(0, view.ArcCenterAngle, 0) * Vector3.forward;
                float inner = DrawRadius(view, center, view.RingInnerRadius, direction);
                float outer = DrawRadius(view, center, view.RingOuterRadius, direction);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(view, "调整盾牌圆弧区域");
                    view.RingCenter = center;
                    view.RingInnerRadius = Mathf.Max(0, inner);
                    view.RingOuterRadius = Mathf.Max(view.RingInnerRadius, outer);
                    EditorUtility.SetDirty(view); PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                }
                Handles.color = Color.white;
                var bottom = view.RingCenter + direction * view.RingOuterRadius;
                Handles.DrawWireCube(bottom + Vector3.up * view.CardSize.y * .5f, new Vector3(view.CardSize.x, view.CardSize.y, 0));
            }
        }
        private static float DrawRadius(DiscardShieldPresenter view, Vector3 center, float radius, Vector3 direction)
        {
            var start = Quaternion.Euler(0, view.ArcCenterAngle - view.ArcAngle * .5f, 0) * Vector3.forward;
            Handles.DrawWireArc(center, Vector3.up, start, view.ArcAngle, radius);
            var point = center + direction * radius;
            var moved = Handles.Slider(point, direction, HandleUtility.GetHandleSize(point) * .06f, Handles.DotHandleCap, 0);
            return Mathf.Max(0, Vector3.Dot(moved - center, direction));
        }
        private sealed class BoxBoundsHandleWithLabel
        {
            private readonly UnityEditor.IMGUI.Controls.BoxBoundsHandle handle = new UnityEditor.IMGUI.Controls.BoxBoundsHandle();
            private readonly Color color; private readonly string label;
            public BoxBoundsHandleWithLabel(Color color, string label) { this.color = color; this.label = label; }
            public void Draw(DiscardShieldPresenter view, ref Vector3 center, ref Vector3 size)
            {
                Handles.color = color; Handles.Label(center, label);
                EditorGUI.BeginChangeCheck(); var position = Handles.PositionHandle(center, Quaternion.identity);
                handle.center = position; handle.size = size; handle.SetColor(color); handle.DrawHandle();
                if (!EditorGUI.EndChangeCheck()) return;
                Undo.RecordObject(view, "调整盾牌区域"); center = handle.center; size = handle.size;
                EditorUtility.SetDirty(view); PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            }
        }
    }
}
