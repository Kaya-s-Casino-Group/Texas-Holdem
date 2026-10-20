using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StylizedGrass
{
    /// <summary>Deterministic opaque grass with spatial instancing and main-camera LOD.</summary>
    [ExecuteAlways]
    public sealed class StylizedGrassField : MonoBehaviour
    {
        [HideInInspector] public StylizedGrassManager manager;
        [HideInInspector] public StylizedGrassTerrainData terrainData;
        public Mesh nearMesh, farMesh;
        public Material material;
        public Vector2 size = new Vector2(22, 24);
        public int seed = 7319;
        [Range(.08f, .6f)] public float spacing = .18f;
        public Vector2 heightRange = new Vector2(.2f, .46f);
        public float nearDistance = 11;
        public bool drawGrass = true;
        public bool castNearShadows = true;
        [Serializable] public struct RenderStatistics
        {
            public string camera, lodCamera;
            public int sourceInstances, instances, draws, nearInstances, midInstances, farInstances;
            public int nearBatches, distanceCulledBatches, triangles, fullDetailTriangles, lodSignature;
        }
        public RenderStatistics MainCameraStatistics { get; private set; }
        public RenderStatistics LastRenderStatistics { get; private set; }
        public int LastInstances => MainCameraStatistics.instances;
        public int LastDrawCalls => MainCameraStatistics.draws;
        public int LastNearBatches => MainCameraStatistics.nearBatches;
        public int TotalInstances { get; private set; }

        sealed class Batch { public Matrix4x4[] matrices; public Bounds bounds; public int lod=-1, firstId; }
        readonly List<Batch> batches = new List<Batch>();
        readonly Dictionary<int,string> cameraNames = new Dictionary<int,string>(4);
        readonly Plane[] frustum = new Plane[6];
        bool rebuild = true;
        Matrix4x4 builtTransform;
        int nearTriangles,farTriangles;
        int builtTerrainRevision=-1;

        void OnEnable()
        {
            rebuild=true;cameraNames.Clear();
            if(!manager)manager=GetComponentInParent<StylizedGrassManager>();
            RenderPipelineManager.beginCameraRendering+=Render;
        }
        void OnDisable() { RenderPipelineManager.beginCameraRendering-=Render; }
        void OnValidate() { spacing=Mathf.Clamp(spacing,.08f,.6f);rebuild=true; }
        public void Rebuild() { rebuild=true; }

        string CameraName(Camera camera)
        {
            // Unity's native Object.name getter returns a managed string; cache it outside the steady-state draw loop.
            int id=camera.GetInstanceID();
            if(!cameraNames.TryGetValue(id,out string name))
            {
                name=camera.name;cameraNames.Add(id,name);
            }
            return name;
        }

        public static float Height(float x,float z) => StylizedGrassTerrainShape.Height(x,z);
        public static float Coverage(float x,float z) => StylizedGrassTerrainShape.Coverage(x,z);
        public float SurfaceHeight(float x,float z) => terrainData?terrainData.Height(x,z):Height(x,z);
        public float SurfaceCoverage(float x,float z) => terrainData?terrainData.Coverage(x,z):Coverage(x,z);
        Vector3 SurfaceNormal(float x,float z)
        {
            const float e=.04f;
            return new Vector3(SurfaceHeight(x-e,z)-SurfaceHeight(x+e,z),2*e,SurfaceHeight(x,z-e)-SurfaceHeight(x,z+e)).normalized;
        }
        public static Vector3 Normal(float x, float z)
        {
            const float e = .04f;
            return new Vector3(Height(x-e,z)-Height(x+e,z), 2*e, Height(x,z-e)-Height(x,z+e)).normalized;
        }
        void Build()
        {
            batches.Clear(); TotalInstances = 0;
            nearTriangles=nearMesh?(int)nearMesh.GetIndexCount(0)/3:0;
            farTriangles=farMesh?(int)farMesh.GetIndexCount(0)/3:0;
            var random = new System.Random(seed);
            var shuffle = new System.Random(seed ^ 0x41a72b);
            float Next() => (float)random.NextDouble();
            const float tile = 3;
            var matrices = new List<Matrix4x4>(512);
            float minX=-size.x*.5f, maxX=size.x*.5f;
            float minZ=-size.y*.5f, maxZ=size.y*.5f;
            for (float tz = minZ; tz < maxZ; tz += tile)
            for (float tx = minX; tx < maxX; tx += tile)
            {
                matrices.Clear();
                for (float z = tz; z < Mathf.Min(tz+tile,maxZ); z += Mathf.Max(.08f,spacing))
                for (float x = tx; x < Mathf.Min(tx+tile,maxX); x += Mathf.Max(.08f,spacing))
                {
                    float px=x+(Next()-.5f)*spacing, pz=z+(Next()-.5f)*spacing;
                    float acceptance=Next(),baseCoverage=Coverage(px,pz);
                    // Consume the original distribution's random stream even when paint removes a tuft.
                    // Added tufts use their own stream, so brushing one patch cannot move every later tuft.
                    bool originallyPresent=acceptance<=baseCoverage*.94f && baseCoverage>=.08f;
                    uint extra=unchecked((uint)(x.GetHashCode()^(z.GetHashCode()*397)^seed));
                    float Extra(){extra=extra*1664525u+1013904223u;return (extra&0x00ffffff)/16777216f;}
                    float heightRandom=originallyPresent?Next():Extra(),widthRandom=originallyPresent?Next():Extra(),rotationRandom=originallyPresent?Next():Extra();
                    float coverage=SurfaceCoverage(px,pz);
                    if (acceptance > coverage*.94f || coverage < .08f) continue;
                    float patch=Mathf.PerlinNoise(px*.45f+72,pz*.45f+17);
                    float h=Mathf.Lerp(heightRange.x,heightRange.y, heightRandom*.5f+patch*.5f);
                    h *= Mathf.Lerp(.45f,1,coverage)*StylizedGrassTerrainShape.GrassScale(px,pz);
                    float w=h*Mathf.Lerp(.95f,1.6f,widthRandom);
                    var rot=Quaternion.FromToRotation(Vector3.up,Vector3.Lerp(Vector3.up,SurfaceNormal(px,pz),.6f)) * Quaternion.Euler(0,rotationRandom*360,0);
                    matrices.Add(transform.localToWorldMatrix * Matrix4x4.TRS(new Vector3(px,SurfaceHeight(px,pz)-.025f,pz),rot,new Vector3(w,h,w)));
                }
                // Split dense cells to respect the 1023 instances per draw limit.
                for(int offset=0; offset<matrices.Count; offset+=1023)
                {
                    int count=Mathf.Min(1023,matrices.Count-offset);
                    var array=matrices.GetRange(offset,count).ToArray();
                    // The same shuffled prefix is retained at every density, preventing rows/stripes.
                    for(int i=array.Length-1;i>0;i--)
                    {
                        int j=shuffle.Next(i+1);var swap=array[i];array[i]=array[j];array[j]=swap;
                    }
                    var bounds=InstanceBounds(array[0]);
                    foreach(var m in array)bounds.Encapsulate(InstanceBounds(m));
                    bounds.Expand(1); // World-space wind allowance, including non-uniform field scale.
                    batches.Add(new Batch { matrices=array, bounds=bounds, firstId=TotalInstances }); TotalInstances += count;
                }
            }
            builtTransform=transform.localToWorldMatrix;builtTerrainRevision=terrainData?terrainData.Revision:-1;rebuild=false;
        }
        Bounds InstanceBounds(Matrix4x4 matrix)
        {
            var local=nearMesh.bounds;local.Encapsulate(farMesh.bounds);
            Vector3 e=local.extents;
            Vector3 x=matrix.MultiplyVector(new Vector3(e.x,0,0));
            Vector3 y=matrix.MultiplyVector(new Vector3(0,e.y,0));
            Vector3 z=matrix.MultiplyVector(new Vector3(0,0,e.z));
            var worldExtent=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),
                Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
            return new Bounds(matrix.MultiplyPoint3x4(local.center),worldExtent*2);
        }
        void Render(ScriptableRenderContext context, Camera camera)
        {
            if(camera.cameraType==CameraType.Preview)return;
            var owner=manager && manager.isActiveAndEnabled?manager:null;
            if(owner)owner.Apply();
            // Deliberately never fall back to the render camera: Scene view must not drive LOD.
            Camera reference=owner?owner.LodCamera:Camera.main;
            var stats=new RenderStatistics{camera=CameraName(camera),lodCamera=reference?CameraName(reference):"None (full detail)"};
            if(!drawGrass || !material || !nearMesh || !farMesh || !SystemInfo.supportsInstancing || (camera.cullingMask & (1<<gameObject.layer))==0)
            { Publish(camera,reference,stats);return; }
            if(rebuild || builtTransform!=transform.localToWorldMatrix || builtTerrainRevision!=(terrainData?terrainData.Revision:-1))Build();
            GeometryUtility.CalculateFrustumPlanes(camera,frustum);
            foreach(var batch in batches)
            {
                float distance=reference?Vector3.Distance(reference.transform.position,batch.bounds.center):0;
                int level=owner && reference?owner.SelectLod(distance,batch.lod):(distance<nearDistance?0:1);
                if(!reference)level=0;
                batch.lod=level;
                float density=owner && reference?owner.DensityAt(distance):1;
                int count=level==3?0:Mathf.Clamp(Mathf.RoundToInt(batch.matrices.Length*density),0,batch.matrices.Length);
                unchecked{stats.lodSignature=stats.lodSignature*31+level*1024+count;}
                if(count==0)stats.distanceCulledBatches++;
                if(!GeometryUtility.TestPlanesAABB(frustum,batch.bounds))continue;
                stats.sourceInstances+=batch.matrices.Length;
                stats.fullDetailTriangles+=batch.matrices.Length*nearTriangles;
                if(count==0)continue;
                bool near=level==0;
                Graphics.DrawMeshInstanced(near?nearMesh:farMesh,0,material,batch.matrices,count,owner?owner.GrassProperties:null,
                    near && castNearShadows?ShadowCastingMode.On:ShadowCastingMode.Off,true,gameObject.layer,camera,LightProbeUsage.BlendProbes);
                stats.draws++;stats.instances+=count;stats.triangles+=count*(near?nearTriangles:farTriangles);
                if(near){stats.nearInstances+=count;stats.nearBatches++;}
                else if(level==1)stats.midInstances+=count;
                else stats.farInstances+=count;
            }
            Publish(camera,reference,stats);
        }
        void Publish(Camera camera,Camera reference,RenderStatistics stats)
        {
            LastRenderStatistics=stats;
            if(camera==reference || (!reference && camera.cameraType==CameraType.Game))MainCameraStatistics=stats;
        }
    }
}
