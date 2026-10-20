using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Tests
{
    public sealed class BattleBackgroundTests
    {
        [Test]
        public void SwitchingActivatesExactlyOneEnvironmentAndPreservesItsObjects()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Backgrounds"); SceneManager.MoveGameObjectToScene(root, scene);
                var choices = new[] { new GameObject("Elite"), new GameObject("Minion"), new GameObject("Boss") };
                foreach (var choice in choices) choice.transform.SetParent(root.transform, false);
                var switcher = root.AddComponent<BattleBackgroundSwitcher>();
                switcher.Configure(choices[0], choices[1], choices[2]);
                foreach (BattleBackground value in new[] { BattleBackground.Boss, BattleBackground.Minion, BattleBackground.Elite })
                {
                    switcher.Select(value);
                    Assert.That(switcher.Current, Is.EqualTo(value));
                    for (int i = 0; i < choices.Length; i++)
                        Assert.That(choices[i].activeSelf, Is.EqualTo(i == (int)value));
                    Assert.That(root.transform.childCount, Is.EqualTo(3));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(1.777778f, .43f)]
        [TestCase(2.261111f, .19f)]
        [TestCase(2.5f, .43f)]
        public void FloorIsHorizontalAndInteriorUvsMatchTheReferenceCamera(float aspect, float top)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Background"); SceneManager.MoveGameObjectToScene(root, scene);
                Transform Child(string name)
                {
                    var go = new GameObject(name); go.transform.SetParent(root.transform, false); return go.transform;
                }
                var camera = Child("Camera").gameObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(5.3f, 5.93f, -11.91f);
                camera.transform.rotation = Quaternion.Euler(7.58107f, -29.0546f, 0);
                camera.fieldOfView = 49; camera.aspect = aspect;
                var floor = Child("Floor").gameObject.AddComponent<MeshFilter>();
                var backdrop = root.AddComponent<PaintedBattleBackdrop>();
                backdrop.Configure(camera, Child("Rear"), Child("Mid"), Child("Front"), floor, top);
                Assert.That(backdrop.FloorMesh.vertexCount, Is.EqualTo(4));
                var vertices = backdrop.FloorMesh.vertices;
                var uv = new List<Vector3>(); backdrop.FloorMesh.GetUVs(0, uv);
                foreach (var point in vertices)
                    Assert.That(floor.transform.TransformPoint(point).y, Is.EqualTo(0).Within(.0001f));
                // Check inside the triangle as well as at its corners: affine UVs would pass corners but warp here.
                Vector3 world = floor.transform.TransformPoint(vertices[0] * .2f + vertices[1] * .3f + vertices[2] * .5f);
                Vector3 homogeneous = uv[0] * .2f + uv[1] * .3f + uv[2] * .5f;
                Vector2 projected = new Vector2(homogeneous.x, homogeneous.y) / homogeneous.z;
                Vector2 expected = PaintedBattleBackdrop.ImageUV(camera.WorldToViewportPoint(world), aspect, 2442f / 1080);
                Assert.That(Vector2.Distance(projected, expected), Is.LessThan(.0001f));
                camera.aspect = 1.6f; backdrop.Refresh();
                Assert.That(backdrop.FloorMesh.vertexCount, Is.EqualTo(4), "Resize keeps the projected floor valid.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
