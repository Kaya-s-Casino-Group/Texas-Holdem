using System.Collections.Generic;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Three painted depth planes and a horizontal floor with perspective-correct baked camera UVs.</summary>
    [ExecuteAlways]
    public sealed class PaintedBattleBackdrop : MonoBehaviour
    {
        [SerializeField] private Camera referenceCamera;
        [SerializeField] private Transform rear, middle, front;
        [SerializeField] private MeshFilter floor;
        [SerializeField] private float artworkAspect = 2442f / 1080f;
        [SerializeField, Range(.01f, .6f)] private float floorTop = .43f;
        [SerializeField] private float floorY;
        [SerializeField] private Vector3 projectionPosition;
        [SerializeField] private Quaternion projectionRotation = Quaternion.identity;
        private Mesh floorMesh;
        private float lastAspect, lastFov, lastSize;
        private bool lastOrthographic, invalid = true;

        public Mesh FloorMesh => floorMesh;
        public float FloorY => floorY;

        public void ConfigureImage(Camera camera, Transform image, float aspect)
        {
            referenceCamera = camera; rear = image; middle = front = null; floor = null;
            artworkAspect = aspect;
            projectionPosition = camera.transform.position; projectionRotation = camera.transform.rotation;
            Refresh(true);
        }

        public void Configure(Camera camera, Transform back, Transform mid, Transform near,
            MeshFilter ground, float groundTop, float groundY = 0)
        {
            referenceCamera = camera; rear = back; middle = mid; front = near; floor = ground;
            floorTop = groundTop; floorY = groundY;
            projectionPosition = camera.transform.position;
            projectionRotation = camera.transform.rotation;
            Refresh(true);
        }

        private void OnEnable() { invalid = true; Refresh(); }
        private void OnValidate() => invalid = true;
        private void LateUpdate() => Refresh();

        public void Refresh(bool force = false)
        {
            if (referenceCamera == null || rear == null) return;
            float aspect = referenceCamera.aspect;
            if (aspect <= 0) return;
            if (!Application.isPlaying)
            {
                invalid |= projectionPosition != referenceCamera.transform.position ||
                    projectionRotation != referenceCamera.transform.rotation;
                projectionPosition = referenceCamera.transform.position;
                projectionRotation = referenceCamera.transform.rotation;
            }
            if (!force && !invalid && Mathf.Approximately(aspect, lastAspect) &&
                Mathf.Approximately(lastFov, referenceCamera.fieldOfView) &&
                Mathf.Approximately(lastSize, referenceCamera.orthographicSize) &&
                lastOrthographic == referenceCamera.orthographic) return;
            lastAspect = aspect; lastFov = referenceCamera.fieldOfView;
            lastSize = referenceCamera.orthographicSize; lastOrthographic = referenceCamera.orthographic;
            invalid = false;
            PlaceLayer(rear, 200);
            if (middle != null) PlaceLayer(middle, 50);
            if (front != null) PlaceLayer(front, 18);
            if (floor != null) BuildFloor();
        }

        // Cover the viewport without stretching; every layer and the floor share this exact crop.
        public static Vector2 ImageUV(Vector2 viewport, float cameraAspect, float imageAspect)
        {
            var scale = new Vector2(Mathf.Min(1, cameraAspect / imageAspect),
                Mathf.Min(1, imageAspect / cameraAspect));
            return Vector2.Scale(viewport - Vector2.one * .5f, scale) + Vector2.one * .5f;
        }

        private float ViewHeight(float depth) => referenceCamera.orthographic ?
            2 * referenceCamera.orthographicSize : 2 * depth * Mathf.Tan(referenceCamera.fieldOfView * Mathf.Deg2Rad * .5f);

        private void PlaceLayer(Transform layer, float depth)
        {
            float height = ViewHeight(depth) * Mathf.Max(1, lastAspect / artworkAspect);
            layer.SetPositionAndRotation(projectionPosition + projectionRotation * (Vector3.forward * depth), projectionRotation);
            layer.localScale = new Vector3(height * artworkAspect, height, 1);
        }

        private void BuildFloor()
        {
            if (floorMesh == null)
            {
                floorMesh = new Mesh { name = "Camera Projected Floor (Runtime)", hideFlags = HideFlags.HideAndDontSave };
            }
            var vertices = new Vector3[4];
            var uv = new List<Vector3>(4);
            float uvScaleY = Mathf.Min(1, artworkAspect / lastAspect);
            float top = Mathf.Clamp01((floorTop - .5f) / uvScaleY + .5f);
            var corners = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, top), new Vector2(0, top) };
            var plane = new Plane(Vector3.up, new Vector3(0, floorY, 0));
            var forward = projectionRotation * Vector3.forward;
            for (int i = 0; i < corners.Length; ++i)
            {
                Vector2 screen = corners[i];
                float height = ViewHeight(1);
                var offset = new Vector3((screen.x - .5f) * height * lastAspect, (screen.y - .5f) * height, 0);
                var ray = referenceCamera.orthographic
                    ? new Ray(projectionPosition + projectionRotation * offset, forward)
                    : new Ray(projectionPosition, projectionRotation * (offset + Vector3.forward));
                if (!plane.Raycast(ray, out float distance))
                {
                    // A camera looking above the ground cannot project this plate onto a horizontal surface.
                    floorMesh.Clear(); floor.sharedMesh = floorMesh; return;
                }
                Vector3 world = ray.GetPoint(distance);
                vertices[i] = floor.transform.InverseTransformPoint(world);
                float depth = referenceCamera.orthographic ? 1 : Vector3.Dot(world - projectionPosition, forward);
                Vector2 image = ImageUV(screen, lastAspect, artworkAspect);
                uv.Add(new Vector3(image.x * depth, image.y * depth, depth));
            }
            floorMesh.Clear();
            floorMesh.vertices = vertices;
            floorMesh.SetUVs(0, uv);
            floorMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            floorMesh.RecalculateNormals(); floorMesh.RecalculateBounds();
            floor.sharedMesh = floorMesh;
        }

        private void OnDestroy()
        {
            if (floorMesh == null) return;
            if (Application.isPlaying) Destroy(floorMesh); else DestroyImmediate(floorMesh);
        }
    }
}
