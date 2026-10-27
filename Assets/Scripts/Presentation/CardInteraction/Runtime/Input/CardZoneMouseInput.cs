using System;
using System.Collections.Generic;
using CardPresentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace CardInteraction
{
    /// <summary>Input System mouse/touch adapter. The component name preserves existing scene bindings.</summary>
    [DisallowMultipleComponent]
    public sealed class CardZoneMouseInput : MonoBehaviour
    {
        [SerializeField] private Camera inputCamera;
        [SerializeField, Min(.05f)] private float longPressSeconds = .35f;
        [SerializeField, Min(1)] private float pressMoveTolerance = 12;
        private CardZonePointerController controller;
        private TouchControl activeTouch;
        private int touchId;
        private bool touchSequence;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private PointerEventData pointerData;
        private EventSystem pointerSystem;
        public CardZonePointerController Controller => controller;
        public event Action StateChanged;

        public void Bind(CardZoneView zone, ICardInteractionSource cards, Camera camera = null)
        {
            var next = new CardZonePointerController(zone, cards) { LongPressSeconds = longPressSeconds, PressMoveTolerance = pressMoveTolerance };
            Unbind(); inputCamera = camera; controller = next; controller.StateChanged += OnStateChanged;
        }
        public void Unbind()
        {
            CancelGesture();
            if (controller == null) return;
            controller.StateChanged -= OnStateChanged; controller = null;
        }
        private void OnStateChanged() => StateChanged?.Invoke();
        private bool Blocked(Camera camera, Vector2 position)
        {
            if (camera == null || !camera.isActiveAndEnabled || !Application.isFocused || !camera.pixelRect.Contains(position)) return true;
            var system = EventSystem.current;
            if (system == null) return false;
            if (pointerSystem != system) { pointerSystem = system; pointerData = new PointerEventData(system); }
            pointerData.Reset(); pointerData.position = position;
            // Raycast the current position: EventSystem's cached pointer state can be one frame behind Update.
            uiHits.Clear(); system.RaycastAll(pointerData, uiHits);
            foreach (var hit in uiHits) if (hit.module is GraphicRaycaster) return true;
            return false;
        }
        private void Update()
        {
            if (controller == null) return;
            var camera = inputCamera != null ? inputCamera : Camera.main;
            if (!Application.isFocused) { CancelGesture(); return; }
            var screen = Touchscreen.current;
            if (activeTouch != null)
            {
                if (!activeTouch.device.added || activeTouch.touchId.ReadValue() != touchId) { CancelGesture(); return; }
                ProcessTouch(camera); return;
            }
            if (screen != null)
            {
                bool anyTouch = false;
                foreach (var touch in screen.touches)
                {
                    anyTouch |= touch.isInProgress || touch.press.wasReleasedThisFrame;
                    if (touchSequence || !touch.press.wasPressedThisFrame) continue;
                    activeTouch = touch; touchId = touch.touchId.ReadValue();
                    touchSequence = true; controller.CancelPointer();
                    ProcessTouch(camera, true); return;
                }
                if (anyTouch) { controller.CancelPointer(); return; }
            }
            // Do not hand an unfinished multi-touch gesture to another finger or a synthetic mouse.
            if (touchSequence) { touchSequence = false; controller.CancelPointer(); return; }
            var mouse = Mouse.current;
            if (mouse == null) { controller.CancelPointer(); return; }
            ProcessMouse(camera, mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame);
        }
        private void ProcessMouse(Camera camera, Vector2 position, bool pressed, bool released)
        {
            if (Blocked(camera, position)) { controller.CancelPointer(); return; }
#if UNITY_EDITOR
            var editorWindow = UnityEditor.EditorWindow.mouseOverWindow;
            if (editorWindow == null || editorWindow.GetType().FullName != "UnityEditor.GameView") { controller.CancelPointer(); return; }
#endif
            controller.ProcessPointer(camera.ScreenPointToRay(position), pressed, released, screenPosition: position);
        }
        private void ProcessTouch(Camera camera, bool began = false)
        {
            var position = activeTouch.position.ReadValue();
            var phase = activeTouch.phase.ReadValue();
            bool blocked = phase == TouchPhase.Canceled || phase == TouchPhase.None || Blocked(camera, position);
            if (blocked) controller.CancelPointer();
            else controller.ProcessPointer(camera.ScreenPointToRay(position), began,
                activeTouch.press.wasReleasedThisFrame || phase == TouchPhase.Ended, screenPosition: position);
            if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled || phase == TouchPhase.None)
            { controller.CancelPointer(); activeTouch = null; }
        }
        private void CancelGesture()
        {
            controller?.CancelPointer(); activeTouch = null;
            // Resume only after all fingers are lifted.
            touchSequence = true;
        }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelGesture(); }
        private void OnApplicationPause(bool paused) { if (paused) CancelGesture(); }
        private void OnDisable() => CancelGesture();
        private void OnDestroy() => Unbind();
    }
}
