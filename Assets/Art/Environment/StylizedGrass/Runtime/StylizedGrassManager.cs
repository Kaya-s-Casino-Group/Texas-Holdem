using UnityEngine;

namespace StylizedGrass
{
    /// <summary>Scene-local appearance and main-camera LOD controls for one grass field.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("StylizedGrass/Manager")]
    public sealed class StylizedGrassManager : MonoBehaviour
    {
        [Header("资源与主相机")]
        public StylizedGrassField field;
        public MeshRenderer groundRenderer;
        public Material grassMaterial, groundMaterial;
        public Mesh nearMesh, farMesh;
        [Tooltip("Scene 画笔保存的高度和草密度数据。留空使用原始参考地形。")]
        public StylizedGrassTerrainData terrainData;
        [Tooltip("留空自动使用 Camera.main。Scene 视图只负责视锥裁剪，不改变 LOD。")]
        public Camera lodCamera;

        [Header("草分布")]
        [InspectorName("显示草")] public bool drawGrass = true;
        [InspectorName("随机种子")] public int seed = 7319;
        [InspectorName("铺设范围 XZ"), Tooltip("草的铺设范围；调整时应与已有地面网格匹配。")]
        public Vector2 areaSize = new Vector2(22,24);
        [InspectorName("草间距（米）"), Range(.08f,.6f)] public float spacing = .215f;
        [InspectorName("草高度范围")] public Vector2 heightRange = new Vector2(.22f,.48f);
        [InspectorName("近处投影")] public bool castNearShadows = false;

        [Header("主相机 LOD（世界距离 / 米）")]
        [InspectorName("启用距离降级")] public bool lodEnabled = true;
        [InspectorName("LOD0 完整模型距离"), Min(1)] public float nearDistance = 8;
        [InspectorName("LOD1 简模距离"), Min(2)] public float midDistance = 16;
        [InspectorName("草最大显示距离"), Min(3)] public float maxDistance = 28;
        [InspectorName("中景保留密度"), Range(.05f,1)] public float midDensity = .55f;
        [InspectorName("远景保留密度"), Range(.01f,1)] public float farDensity = .2f;
        [InspectorName("消隐过渡距离"), Min(.1f)] public float fadeDistance = 5;
        [InspectorName("切换缓冲距离"), Range(0,2)] public float hysteresis = .6f;

        [Header("倒伏")]
        [InspectorName("倒伏程度"), Range(0,1)] public float lodging = .95f;
        [InspectorName("倒伏方向（0=X / 90=Z）"), Range(0,360)] public float lodgingDirection = 25;
        [InspectorName("斑块变化"), Range(0,1)] public float lodgingVariation = .85f;
        [InspectorName("斑块尺寸（米）"), Range(.5f,12)] public float lodgingPatchSize = 3.5f;
        [Header("风")]
        [InspectorName("风方向 XZ")] public Vector2 windDirection = new Vector2(1,.35f);
        [InspectorName("风幅（米）"), Range(0,.5f)] public float windStrength = .065f;
        [InspectorName("风速"), Range(0,4)] public float windSpeed = .7f;
        [InspectorName("风波尺度"), Min(.01f)] public float windScale = 1.1f;
        [Header("草颜色与表面")]
        public Color rootColor, tipColor, dryColor;
        [Range(0,1)] public float colorVariation = .35f, grassSmoothness = .08f;
        [Range(0,1)] public float rootOcclusion = .32f, normalUp = .7f, grassFill = .12f;
        [Header("草皮与裸土")]
        public Color turfColor, soilColor;
        [Range(.5f,5)] public float blendSharpness = 1.6f;
        [Min(.01f)] public float turfTiling = 1.4f, soilTiling = 4.5f;
        [Range(0,2)] public float groundNormalStrength = .7f;
        [Range(0,1)] public float groundSmoothness = .13f, groundFill = .1f;

        MaterialPropertyBlock grassProperties, groundProperties;
        MeshRenderer appliedGround;
        public MaterialPropertyBlock GrassProperties => grassProperties;
        public Camera LodCamera => lodCamera ? lodCamera : Camera.main;

        void Reset() { ReadCurrentSettings(); }
        void OnEnable() { Apply(); }
        void Update() { Apply(); }
        void OnValidate()
        {
            spacing=Mathf.Clamp(spacing,.08f,.6f);
            areaSize=new Vector2(Mathf.Max(.1f,areaSize.x),Mathf.Max(.1f,areaSize.y));
            heightRange.x=Mathf.Max(.01f,heightRange.x);heightRange.y=Mathf.Max(heightRange.x,heightRange.y);
            nearDistance=Mathf.Max(1,nearDistance);midDistance=Mathf.Max(nearDistance+1,midDistance);
            maxDistance=Mathf.Max(midDistance+1,maxDistance);
            midDensity=Mathf.Clamp(midDensity,.05f,1);farDensity=Mathf.Clamp(farDensity,.01f,midDensity);
            fadeDistance=Mathf.Clamp(fadeDistance,.1f,maxDistance-midDistance);
        }
        void OnDisable()
        {
            if(appliedGround)appliedGround.SetPropertyBlock(null);
            appliedGround=null;
        }
        /// <summary>Import the existing look once, without resetting the scene or writing material assets.</summary>
        public void ReadCurrentSettings()
        {
            if(!field)field=GetComponentInChildren<StylizedGrassField>(true);
            if(!groundRenderer)groundRenderer=GetComponentInChildren<MeshRenderer>(true);
            if(field)
            {
                grassMaterial=field.material;nearMesh=field.nearMesh;farMesh=field.farMesh;
                seed=field.seed;spacing=field.spacing;heightRange=field.heightRange;areaSize=field.size;
                drawGrass=field.drawGrass;castNearShadows=field.castNearShadows;
            }
            if(groundRenderer)groundMaterial=groundRenderer.sharedMaterial;
            if(grassMaterial)
            {
                var m=grassMaterial;
                lodging=m.GetFloat("_Lodging");lodgingDirection=m.GetFloat("_LodgingDirection");
                lodgingVariation=m.GetFloat("_LodgingVariation");lodgingPatchSize=m.GetFloat("_LodgingPatchSize");
                Vector4 wind=m.GetVector("_WindDirection");windDirection=new Vector2(wind.x,wind.z);
                windStrength=m.GetFloat("_WindStrength");windSpeed=m.GetFloat("_WindSpeed");windScale=m.GetFloat("_WindScale");
                rootColor=m.GetColor("_RootColor");tipColor=m.GetColor("_TipColor");dryColor=m.GetColor("_DryColor");
                colorVariation=m.GetFloat("_ColorVariation");grassSmoothness=m.GetFloat("_Smoothness");
                rootOcclusion=m.GetFloat("_RootOcclusion");normalUp=m.GetFloat("_NormalUp");grassFill=m.GetFloat("_Fill");

            }
            if(groundMaterial)
            {
                var m=groundMaterial;
                turfColor=m.GetColor("_GrassColor");soilColor=m.GetColor("_DirtColor");
                blendSharpness=m.GetFloat("_BlendSharpness");turfTiling=m.GetFloat("_GrassTiling");soilTiling=m.GetFloat("_SoilTiling");
                groundNormalStrength=m.GetFloat("_NormalStrength");groundSmoothness=m.GetFloat("_Smoothness");groundFill=m.GetFloat("_Fill");
            }
        }
        public void Apply()
        {
            if(!isActiveAndEnabled)return;
            if(grassProperties==null)grassProperties=new MaterialPropertyBlock();
            if(groundProperties==null)groundProperties=new MaterialPropertyBlock();
            grassProperties.SetFloat("_Lodging",lodging);grassProperties.SetFloat("_LodgingDirection",lodgingDirection);
            grassProperties.SetFloat("_LodgingVariation",lodgingVariation);grassProperties.SetFloat("_LodgingPatchSize",lodgingPatchSize);
            grassProperties.SetVector("_WindDirection",new Vector4(windDirection.x,0,windDirection.y,0));
            grassProperties.SetFloat("_WindStrength",windStrength);grassProperties.SetFloat("_WindSpeed",windSpeed);grassProperties.SetFloat("_WindScale",windScale);
            grassProperties.SetColor("_RootColor",rootColor);grassProperties.SetColor("_TipColor",tipColor);grassProperties.SetColor("_DryColor",dryColor);
            grassProperties.SetFloat("_ColorVariation",colorVariation);grassProperties.SetFloat("_Smoothness",grassSmoothness);
            grassProperties.SetFloat("_RootOcclusion",rootOcclusion);grassProperties.SetFloat("_NormalUp",normalUp);grassProperties.SetFloat("_Fill",grassFill);
            if(field)
            {
                bool changed=field.seed!=seed || field.spacing!=spacing || field.heightRange!=heightRange || field.size!=areaSize || field.nearMesh!=nearMesh || field.farMesh!=farMesh || field.terrainData!=terrainData;
                field.terrainData=terrainData;
                field.manager=this;field.material=grassMaterial;field.nearMesh=nearMesh;field.farMesh=farMesh;
                field.seed=seed;field.spacing=spacing;field.heightRange=heightRange;field.size=areaSize;
                field.drawGrass=drawGrass;field.castNearShadows=castNearShadows;
                if(changed)field.Rebuild();
            }
            if(appliedGround && appliedGround!=groundRenderer)appliedGround.SetPropertyBlock(null);
            appliedGround=groundRenderer;
            if(groundRenderer)
            {
                if(groundRenderer.sharedMaterial!=groundMaterial)groundRenderer.sharedMaterial=groundMaterial;
                groundProperties.SetColor("_GrassColor",turfColor);groundProperties.SetColor("_DirtColor",soilColor);
                groundProperties.SetFloat("_BlendSharpness",blendSharpness);groundProperties.SetFloat("_GrassTiling",turfTiling);groundProperties.SetFloat("_SoilTiling",soilTiling);
                groundProperties.SetFloat("_NormalStrength",groundNormalStrength);groundProperties.SetFloat("_Smoothness",groundSmoothness);groundProperties.SetFloat("_Fill",groundFill);
                groundRenderer.SetPropertyBlock(groundProperties);
            }
        }
        public int SelectLod(float distance,int previous)
        {
            if(!lodEnabled)return 0;
            int level=previous<0?(distance<nearDistance?0:distance<midDistance?1:distance<maxDistance?2:3):previous;
            float margin=Mathf.Min(hysteresis,Mathf.Min(midDistance-nearDistance,maxDistance-midDistance)*.45f);
            while(level<3 && distance>Boundary(level)+margin)level++;
            while(level>0 && distance<Boundary(level-1)-margin)level--;
            return level;
        }
        float Boundary(int level) => level==0?nearDistance:level==1?midDistance:maxDistance;
        public float DensityAt(float distance)
        {
            if(!lodEnabled)return 1;
            float fadeStart=Mathf.Max(midDistance,maxDistance-fadeDistance);
            if(distance<=nearDistance)return 1;
            if(distance<midDistance)return Mathf.Lerp(1,midDensity,Mathf.InverseLerp(nearDistance,midDistance,distance));
            if(distance<fadeStart)return Mathf.Lerp(midDensity,farDensity,Mathf.InverseLerp(midDistance,fadeStart,distance));
            // A prefix of a shuffled stable set disappears one tuft at a time, without resampling.
            float startDensity=fadeStart==midDistance?midDensity:farDensity;
            return Mathf.Lerp(startDensity,0,Mathf.InverseLerp(fadeStart,maxDistance,distance));
        }
    }
}
