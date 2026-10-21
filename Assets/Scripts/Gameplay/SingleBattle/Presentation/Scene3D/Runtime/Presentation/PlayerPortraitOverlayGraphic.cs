using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>Projects the source quad verbatim, including attack motion, pose fades and death rotation.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PlayerPortraitOverlayGraphic : MaskableGraphic
    {
        private Camera viewCamera;
        private MeshRenderer source;
        private Vector3[] vertices;
        private Vector2[] uv;
        private int[] triangles;
        private Texture texture;
        private MaterialPropertyBlock properties;
        public override Texture mainTexture => texture != null ? texture : Texture2D.whiteTexture;

        public void Configure(Camera camera, MeshRenderer renderer)
        {
            viewCamera = camera; source = renderer; raycastTarget = false; maskable = false;
            var mesh = source.GetComponent<MeshFilter>().sharedMesh;
            vertices = mesh.vertices; uv = mesh.uv; triangles = mesh.triangles;
            Refresh();
        }

        public void Refresh()
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            source.GetPropertyBlock(properties);
            var next = properties.GetTexture("_BaseMap") ?? source.sharedMaterial.GetTexture("_BaseMap");
            if (texture != next) { texture = next; SetMaterialDirty(); }
            color = properties.HasColor("_BaseColor") ? properties.GetColor("_BaseColor") : source.sharedMaterial.GetColor("_BaseColor");
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (source == null || vertices == null || viewCamera == null) return;
            for (int i = 0; i < vertices.Length; i++)
            {
                var screen = viewCamera.WorldToScreenPoint(source.transform.TransformPoint(vertices[i]));
                if (screen.z <= 0) { mesh.Clear(); return; }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, null, out var point);
                mesh.AddVert(point, color, uv[i]);
            }
            for (int i = 0; i < triangles.Length; i += 3) mesh.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
        }
    }
}
