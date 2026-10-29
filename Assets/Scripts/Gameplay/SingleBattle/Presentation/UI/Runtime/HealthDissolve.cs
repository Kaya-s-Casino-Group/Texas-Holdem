using UnityEngine;
using UnityEngine.UI;

namespace Poker.BattlePresentation
{
    [ExecuteAlways, RequireComponent(typeof(Image))]
    public sealed class HealthDissolve : BaseMeshEffect
    {
        [SerializeField, Range(0,1)] private float amount = 1;
        [SerializeField, Range(.01f,.2f)] private float edgeWidth = .08f;
        [SerializeField, Min(0)] private float transitionSeconds = .25f;
        private float visibleAmount = 1;
        public float Amount => amount;
        public void SetAmount(float value, bool immediate = false)
        {
            amount = Mathf.Clamp01(value);
            if (immediate || !Application.isPlaying) visibleAmount = amount;
            graphic.SetVerticesDirty();
        }
        protected override void OnEnable()
        {
            base.OnEnable(); visibleAmount = amount;
            if (graphic.canvas != null) graphic.canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }
        private void Update()
        {
            if (Mathf.Approximately(visibleAmount, amount)) return;
            visibleAmount = transitionSeconds <= 0 || !Application.isPlaying ? amount :
                Mathf.MoveTowards(visibleAmount, amount, Time.unscaledDeltaTime / transitionSeconds);
            graphic.SetVerticesDirty();
        }
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            var rect = graphic.rectTransform.rect;
            var vertex = new UIVertex();
            for (int i=0; i<mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex,i);
                vertex.uv1 = new Vector4(Mathf.InverseLerp(rect.xMin,rect.xMax,vertex.position.x),
                    Mathf.InverseLerp(rect.yMin,rect.yMax,vertex.position.y),visibleAmount,edgeWidth);
                mesh.SetUIVertex(vertex,i);
            }
        }
    }
}
