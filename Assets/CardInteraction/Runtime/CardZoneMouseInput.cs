using System;
using CardPresentation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CardInteraction
{
    /// <summary>Runtime legacy-input mouse adapter. Bind a host-owned card source and republish on StateChanged.</summary>
    [DisallowMultipleComponent]
    public sealed class CardZoneMouseInput : MonoBehaviour
    {
        [SerializeField] private Camera inputCamera;
        private CardZonePointerController controller;
        public CardZonePointerController Controller => controller;
        public event Action StateChanged;

        public void Bind(CardZoneView zone, ICardInteractionSource cards, Camera camera = null)
        {
            var next = new CardZonePointerController(zone, cards);
            Unbind(); inputCamera = camera; controller = next; controller.StateChanged += OnStateChanged;
        }

        public void Unbind()
        {
            if (controller == null) return;
            controller.CancelPointer(); controller.StateChanged -= OnStateChanged; controller = null;
        }

        private void OnStateChanged() => StateChanged?.Invoke();
        private void Update()
        {
            if (controller == null) return;
            var camera = inputCamera != null ? inputCamera : Camera.main;
            bool blocked = camera == null || !camera.isActiveAndEnabled || !Input.mousePresent || !Application.isFocused ||
                !camera.pixelRect.Contains(Input.mousePosition) || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
#if UNITY_EDITOR
            // Input Manager can retain Game coordinates while the editor pointer is over Scene/Inspector.
            var editorWindow = UnityEditor.EditorWindow.mouseOverWindow;
            blocked |= editorWindow == null || editorWindow.GetType().FullName != "UnityEditor.GameView";
#endif
            if (blocked) { controller.CancelPointer(); return; }
            controller.ProcessPointer(camera.ScreenPointToRay(Input.mousePosition), Input.GetMouseButtonDown(0), Input.GetMouseButtonUp(0));
        }

        private void OnApplicationFocus(bool focused) { if (!focused) controller?.CancelPointer(); }
        private void OnDisable() => controller?.CancelPointer();
        private void OnDestroy() => Unbind();
    }
}
