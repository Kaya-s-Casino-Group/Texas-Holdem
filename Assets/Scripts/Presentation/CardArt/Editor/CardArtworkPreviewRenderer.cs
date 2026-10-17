using System;
using UnityEditor;
using UnityEngine;

namespace CardArt.Editor
{
    /// <summary>Real renderers, shared assets and per-instance property blocks in an isolated preview scene.</summary>
    public sealed class CardArtworkPreviewRenderer : IDisposable
    {
        private readonly PreviewRenderUtility preview;
        public CardView Reference { get; private set; }
        public CardView Current { get; private set; }
        public bool InstancingEnabled => Current.GetComponent<MeshRenderer>().sharedMaterial.enableInstancing;

        public CardArtworkPreviewRenderer(GameObject prefab)
        {
            preview = new PreviewRenderUtility();
            try
            {
                Reference = Create(prefab); Current = Create(prefab);
                preview.camera.orthographic = true;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.035f, .04f, .045f);
                preview.ambientColor = Color.gray * .5f;
                preview.lights[0].intensity = 1.5f;
                preview.lights[0].color = Color.white;
                preview.lights[0].transform.rotation = Quaternion.Euler(45, -35, 0);
                preview.lights[1].intensity = .5f;
                preview.lights[1].color = Color.white;
            }
            catch { Dispose(); throw; }
        }

        private CardView Create(GameObject prefab)
        {
            var root = UnityEngine.Object.Instantiate(prefab);
            root.hideFlags = HideFlags.HideAndDontSave;
            preview.AddSingleGO(root);
            var card = root.GetComponentInChildren<CardView>(true);
            if (card == null) throw new ArgumentException("Prefab 中没有 CardView。");
            root.SetActive(true); card.gameObject.SetActive(true);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // Atlas front-vertex flags and UV direction establish the surface orientation.
            card.transform.rotation = CardRenderingValidation.SurfaceOrientation(card.GetComponent<MeshFilter>().sharedMesh);
            return card;
        }

        public void Present(CardArtProfile profile, string face, System.Collections.Generic.IEnumerable<string> combinations, bool faceUp = true)
        {
            foreach (var card in new[] { Reference, Current }) card.SetArtProfile(profile);
            Reference.SetCard(face); Reference.PresentArtwork(System.Array.Empty<string>(),Color.white);
            Current.SetCard(faceUp ? face : "card_back"); Current.PresentArtwork(faceUp ? combinations : System.Array.Empty<string>(),Color.white);
            var width = Mathf.Max(Reference.GetComponent<MeshRenderer>().bounds.size.x, .001f);
            Reference.transform.root.position = Vector3.right * width * .65f;
            Current.transform.root.position = Vector3.left * width * .65f;
        }

        public void ConfigureLighting(float keyIntensity, float fillIntensity, float ambientBrightness, Vector2 direction)
        {
            preview.lights[0].intensity = keyIntensity;
            preview.lights[1].intensity = fillIntensity;
            preview.ambientColor = Color.white * ambientBrightness;
            preview.lights[0].transform.rotation = Quaternion.Euler(direction.x, direction.y, 0);
        }

        public Texture Render(Rect rect, float tilt)
        {
            Texture result = null;
            preview.BeginPreview(rect, GUIStyle.none);
            try
            {
                var bounds = Reference.GetComponent<MeshRenderer>().bounds;
                bounds.Encapsulate(Current.GetComponent<MeshRenderer>().bounds);
                var radius = Mathf.Max(bounds.extents.magnitude, .001f);
                var direction = Quaternion.AngleAxis(tilt, Vector3.right) * Vector3.up;
                preview.camera.transform.position = bounds.center + direction * radius * 4;
                preview.camera.transform.LookAt(bounds.center, Vector3.forward);
                preview.camera.nearClipPlane = radius * .01f;
                preview.camera.farClipPlane = radius * 10;
                preview.camera.aspect = rect.width / Mathf.Max(rect.height, 1);
                preview.camera.orthographicSize = Mathf.Max(bounds.extents.z, bounds.extents.x / preview.camera.aspect) * 1.25f;
                preview.Render(allowScriptableRenderPipeline: true);
            }
            finally { result = preview.EndPreview(); }
            return result;
        }

        public void Dispose() { preview?.Cleanup(); Reference = null; Current = null; }
    }
}
