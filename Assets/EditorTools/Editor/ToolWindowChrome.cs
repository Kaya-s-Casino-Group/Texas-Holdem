using UnityEditor;
using UnityEngine;

namespace Poker.EditorTools
{
    // An in-content alternative to the narrow native window edges on macOS.
    // Keep this outside scroll views and disabled controls so it is always reachable.
    public sealed class ToolWindowChrome
    {
        public const float FooterHeight = 22;
        private int activeControl;
        private bool resizing;
        private Vector2 startMouse;
        private Rect startPosition;

        public void Draw(EditorWindow window)
        {
            var footer = new Rect(0, Mathf.Max(0, window.position.height - FooterHeight),
                window.position.width, FooterHeight);
            var resizeRect = new Rect(Mathf.Max(0, footer.xMax - 28), footer.y, 28, FooterHeight);
            var moveRect = new Rect(footer.x, footer.y, Mathf.Max(0, footer.width - 28), FooterHeight);
            int control = GUIUtility.GetControlID("ToolWindowChrome".GetHashCode(), FocusType.Passive);
            bool canDrag = !window.docked && !window.maximized;

            GUI.Box(footer, GUIContent.none, EditorStyles.toolbar);
            if (canDrag)
            {
                GUI.Label(resizeRect, "◢", EditorStyles.centeredGreyMiniLabel);
                EditorGUIUtility.AddCursorRect(moveRect, MouseCursor.MoveArrow);
                EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeUpLeft);
            }

            var evt = Event.current;
            if (activeControl != 0 && (!canDrag || GUIUtility.hotControl != activeControl)) Release();
            switch (evt.GetTypeForControl(control))
            {
                case EventType.MouseDown:
                    if (!canDrag || evt.button != 0 || !footer.Contains(evt.mousePosition)) break;
                    activeControl = control;
                    GUIUtility.hotControl = control;
                    GUIUtility.keyboardControl = 0;
                    resizing = resizeRect.Contains(evt.mousePosition);
                    startMouse = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                    startPosition = window.position;
                    evt.Use();
                    break;
                case EventType.MouseDrag:
                    if (activeControl != control || GUIUtility.hotControl != control) break;
                    // Use screen points: local coordinates change while moving the window.
                    Vector2 delta = GUIUtility.GUIToScreenPoint(evt.mousePosition) - startMouse;
                    var next = startPosition;
                    if (resizing)
                    {
                        next.width = Mathf.Clamp(startPosition.width + delta.x, window.minSize.x, window.maxSize.x);
                        next.height = Mathf.Clamp(startPosition.height + delta.y, window.minSize.y, window.maxSize.y);
                    }
                    else next.position += delta;
                    window.position = next;
                    window.Repaint();
                    evt.Use();
                    break;
                case EventType.MouseUp:
                    if (activeControl != control || evt.button != 0) break;
                    Release();
                    evt.Use();
                    break;
                case EventType.KeyDown:
                    if (activeControl != control || evt.keyCode != KeyCode.Escape) break;
                    window.position = startPosition;
                    Release();
                    evt.Use();
                    break;
            }
        }

        public void Release()
        {
            if (activeControl != 0 && GUIUtility.hotControl == activeControl) GUIUtility.hotControl = 0;
            activeControl = 0;
        }
    }
}
