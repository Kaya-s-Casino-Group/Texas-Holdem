using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CardPresentation
{
    /// <summary>Uses the same Input System pointer identity for UI hover and touch inspection.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EventSystem))]
    public sealed class CardUIInput : MonoBehaviour
    {
        public static bool IsTouch(PointerEventData data) => data is ExtendedPointerEventData extended &&
            extended.pointerType == UIPointerType.Touch;

        private InputSystemUIInputModule inputModule;
        private StandaloneInputModule legacyModule;

        public static void EnsureEventSystem()
        {
            var system = EventSystem.current;
            if (system == null) system = Object.FindObjectOfType<EventSystem>();
            if (system == null) system = new GameObject("Card UI EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            var router = system.GetComponent<CardUIInput>();
            if (router == null) router = system.gameObject.AddComponent<CardUIInput>();
            router.RefreshModules();
        }

        private void OnEnable() => RefreshModules();

        public void RefreshModules()
        {
            if (inputModule == null) inputModule = GetComponent<InputSystemUIInputModule>();
            if (legacyModule == null) legacyModule = GetComponent<StandaloneInputModule>();
            // Retire serialized modules in older scenes; never use the legacy input backend.
            if (legacyModule != null) legacyModule.enabled = false;
            if (inputModule == null) inputModule = gameObject.AddComponent<InputSystemUIInputModule>();
            inputModule.enabled = true;
        }
    }
}
