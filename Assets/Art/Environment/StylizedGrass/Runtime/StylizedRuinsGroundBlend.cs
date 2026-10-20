using UnityEngine;

namespace StylizedGrass
{
    /// <summary>Scene-local fixed-camera painting and surface-only atmosphere. Never changes background materials.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class StylizedRuinsGroundBlend : MonoBehaviour
    {
        public Camera referenceCamera;
        public Material groundMaterial;
        [Header("固定镜头地面投影")]
        [Tooltip("只控制地面上沿淡出，不压缩贴图高度。"), Range(.3f,1)] public float paintingScreenHeight=.64f;
        [Tooltip("等比例放大并裁切；1 为横向铺满，不拉伸。"), Range(1,3)] public float paintingZoom=1;
        [Tooltip("贴图 UV 裁切偏移。超出贴图范围会采样边缘。")]
        public Vector2 paintingOffset=Vector2.zero;
        [Range(.2f,2)] public float brightness=1;
        [Header("地面到远景的交接（相机深度 / 米）")]
        [Min(0)] public float blendStart=12;
        [Min(.1f)] public float blendEnd=24;
        [Range(0,3)] public float edgeBreakup=.8f;
        [Header("仅地面：深度与高度雾，不模糊背景")]
        public Color fogColor=new Color(.22f,.11f,.16f,1);
        [Min(0)] public float fogStart=7;
        [Min(.1f)] public float fogEnd=22;
        [Range(0,1)] public float fogStrength=.28f;
        public float fogBaseHeight=.6f;
        [Min(.01f)] public float fogHeightFalloff=.5f;
        [SerializeField, HideInInspector] Vector3 projectionPosition,projectionRight,projectionUp,projectionForward;
        [SerializeField, HideInInspector] float tanHalfFov=.45f,projectionAspect=1.7777778f;

        [ContextMenu("从主相机重新固定投影")]
        public void CaptureProjection()
        {
            if(!referenceCamera)referenceCamera=Camera.main;
            if(!referenceCamera)return;
            var t=referenceCamera.transform;
            projectionPosition=t.position;projectionRight=t.right;projectionUp=t.up;projectionForward=t.forward;
            tanHalfFov=Mathf.Tan(referenceCamera.fieldOfView*Mathf.Deg2Rad*.5f);
            projectionAspect=Mathf.Max(16f/9f,referenceCamera.aspect);
            Apply();
        }
        void OnEnable(){Apply();}
        void LateUpdate(){Apply();}
        void OnValidate()
        {
            blendEnd=Mathf.Max(blendStart+.1f,blendEnd);fogEnd=Mathf.Max(fogStart+.1f,fogEnd);Apply();
        }
        public void Apply()
        {
            if(!groundMaterial || projectionForward.sqrMagnitude<.5f)return;
            groundMaterial.SetVector("_PaintingOrigin",projectionPosition);
            groundMaterial.SetVector("_PaintingRight",projectionRight);
            groundMaterial.SetVector("_PaintingUp",projectionUp);
            groundMaterial.SetVector("_PaintingForward",projectionForward);
            groundMaterial.SetVector("_PaintingProjection",new Vector4(tanHalfFov,projectionAspect,paintingScreenHeight,brightness));
            var texture=groundMaterial.GetTexture("_GroundPainting");
            float aspect=texture?(float)texture.width/texture.height:16f/9f;
            // Cover horizontally and crop vertically. Equal source-pixel scale in both axes.
            float zoom=Mathf.Max(1,paintingZoom);
            groundMaterial.SetVector("_PaintingCrop",new Vector4(1/zoom,aspect/projectionAspect/zoom,paintingOffset.x,paintingOffset.y));
            groundMaterial.SetVector("_GroundBlend",new Vector4(blendStart,blendEnd,edgeBreakup,0));
            groundMaterial.SetVector("_GroundFog",new Vector4(fogStart,fogEnd,fogStrength,fogBaseHeight));
            groundMaterial.SetFloat("_GroundFogFalloff",fogHeightFalloff);
            groundMaterial.SetColor("_GroundFogColor",fogColor);
        }
    }
}
