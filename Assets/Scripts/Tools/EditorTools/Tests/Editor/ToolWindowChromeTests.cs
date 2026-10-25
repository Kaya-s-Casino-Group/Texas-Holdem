using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Poker.EditorTools.Tests
{
    public sealed class ChromeTestWindow : EditorWindow
    {
        public readonly ToolWindowChrome Chrome = new ToolWindowChrome();
        public string EventTrace;
        private void OnGUI()
        {
            var evt = Event.current;
            bool trace = evt.isMouse || evt.type == EventType.KeyDown;
            if (trace) EventTrace += $"{evt.type} at {evt.mousePosition}, screen {GUIUtility.GUIToScreenPoint(evt.mousePosition)}, window {position}, docked {docked}, maximized {maximized}, hot {GUIUtility.hotControl}\n";
            Chrome.Draw(this);
            if (trace) EventTrace += $"after: {evt.type}, window {position}, hot {GUIUtility.hotControl}\n";
        }
        private void OnLostFocus() => Chrome.Release();
        private void OnDisable() => Chrome.Release();
    }

    public sealed class ToolWindowChromeTests
    {
        private ChromeTestWindow window;

        [SetUp]
        public void OpenWindow()
        {
            window = ScriptableObject.CreateInstance<ChromeTestWindow>();
            window.titleContent = new GUIContent("Window drag test");
            window.minSize = new Vector2(360, 260);
            window.maxSize = new Vector2(700, 600);
            window.Show();
            window.position = new Rect(180, 180, 440, 320);
            window.Focus();
        }

        [TearDown]
        public void CloseWindow()
        {
            if (window != null) window.Close();
        }

        [UnityTest]
        public IEnumerator ResizeChangesBothAxesAndHonorsLimits()
        {
            yield return null;
            yield return null;
            var origin = window.position.position;
            var initialSize = window.position.size;
            Resize(new Vector2(100, 80));
            AssertSize(initialSize + new Vector2(100, 80));
            Resize(new Vector2(-1000, -1000));
            AssertSize(window.minSize);
            Resize(new Vector2(1000, 1000));
            AssertSize(window.maxSize);
            Assert.AreEqual(origin, window.position.position);
        }

        [UnityTest]
        public IEnumerator MoveDoesNotAccumulateLocalCoordinateDriftAndEscapeRestores()
        {
            yield return null;
            yield return null;
            var original = window.position;
            var start = new Vector2(100, original.height - 11);
            Send(EventType.MouseDown, start);
            Send(EventType.MouseDrag, start + new Vector2(80, 40));
            yield return null;
            Assert.AreEqual(original.position + new Vector2(80, 40), window.position.position, window.EventTrace);
            // After moving the host, the same physical pointer is back at local 'start'.
            Send(EventType.MouseDrag, start + new Vector2(20, 10));
            yield return null;
            Assert.AreEqual(original.position + new Vector2(100, 50), window.position.position);
            Assert.AreEqual(original.size, window.position.size);
            window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
            yield return null;
            Assert.AreEqual(original, window.position);
            Send(EventType.MouseDrag, start + new Vector2(30, 30));
            Assert.AreEqual(original, window.position);
        }

        [UnityTest]
        public IEnumerator MouseUpAndFocusReleaseStopDragging()
        {
            yield return null;
            yield return null;
            var initialSize = window.position.size;
            var start = new Vector2(window.position.width - 14, window.position.height - 11);
            Send(EventType.MouseDown, start);
            Send(EventType.MouseUp, start);
            Send(EventType.MouseDrag, start + new Vector2(60, 60));
            Assert.AreEqual(initialSize, window.position.size);
            Send(EventType.MouseDown, start);
            window.Chrome.Release();
            Send(EventType.MouseDrag, start + new Vector2(60, 60));
            Assert.AreEqual(initialSize, window.position.size);
        }

        [UnityTest]
        public IEnumerator ContentDragDoesNotMoveOrResizeWindow()
        {
            yield return null;
            yield return null;
            var original = window.position;
            Send(EventType.MouseDown, new Vector2(40, 40));
            Send(EventType.MouseDrag, new Vector2(140, 140));
            Send(EventType.MouseUp, new Vector2(140, 140));
            Assert.AreEqual(original, window.position);
        }

        private void AssertSize(Vector2 expected)
        {
            // Native windows round logical points to physical pixels on high-DPI displays.
            float tolerance = 1f / EditorGUIUtility.pixelsPerPoint;
            Assert.That(window.position.width, Is.EqualTo(expected.x).Within(tolerance), window.EventTrace);
            Assert.That(window.position.height, Is.EqualTo(expected.y).Within(tolerance), window.EventTrace);
        }

        private void Resize(Vector2 delta)
        {
            var start = new Vector2(window.position.width - 14, window.position.height - 11);
            Send(EventType.MouseDown, start);
            Send(EventType.MouseDrag, start + delta);
            Send(EventType.MouseUp, start + delta);
        }

        private void Send(EventType type, Vector2 mouse)
        {
            // SendEvent targets the host view; its dock tab sits above the client area.
            window.SendEvent(new Event { type = type, mousePosition = mouse + window.rootVisualElement.worldBound.position, button = 0 });
        }
    }
}
