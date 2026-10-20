using System;
using UnityEngine;

namespace StylizedGrass
{
    public enum GrassTerrainBrush { Raise, Lower, Smooth, Flatten, AddGrass, EraseGrass, Restore }

    /// <summary>Scene-owned editable offsets, sampled identically by mesh generation and instanced grass.</summary>
    [CreateAssetMenu(menuName="StylizedGrass/Terrain Paint Data")]
    public sealed class StylizedGrassTerrainData : ScriptableObject
    {
        [SerializeField, HideInInspector] int resolution;
        [SerializeField, HideInInspector] Vector2 size;
        [SerializeField, HideInInspector] float[] heightOffsets, coverageOffsets;
        [NonSerialized] int revision;
        public int Resolution => resolution;
        public Vector2 Size => size;
        public int Revision => revision;
        public bool IsInitialized => resolution>=2 && heightOffsets!=null && coverageOffsets!=null
            && heightOffsets.Length==resolution*resolution && coverageOffsets.Length==heightOffsets.Length;

        public void Initialize(Vector2 dimensions,int samples=129)
        {
            if(IsInitialized)throw new InvalidOperationException("Terrain paint data is already initialized.");
            size=new Vector2(Mathf.Max(.1f,dimensions.x),Mathf.Max(.1f,dimensions.y));
            resolution=Mathf.Clamp(samples,17,513);
            heightOffsets=new float[resolution*resolution];coverageOffsets=new float[heightOffsets.Length];revision++;
        }
        void OnValidate(){revision++;}
        public void Invalidate(){revision++;}
        public Vector2 GridPoint(int x,int z)=>new Vector2((x/(float)(resolution-1)-.5f)*size.x,(z/(float)(resolution-1)-.5f)*size.y);
        float Sample(float[] values,float x,float z)
        {
            if(!IsInitialized)return 0;
            // Jittered grass roots and normal samples can extend slightly past the mesh boundary.
            float gx=Mathf.Clamp01(x/size.x+.5f)*(resolution-1),gz=Mathf.Clamp01(z/size.y+.5f)*(resolution-1);
            int ix=Mathf.Min(Mathf.FloorToInt(gx),resolution-2),iz=Mathf.Min(Mathf.FloorToInt(gz),resolution-2);
            int i=iz*resolution+ix;
            return Mathf.Lerp(Mathf.Lerp(values[i],values[i+1],gx-ix),Mathf.Lerp(values[i+resolution],values[i+resolution+1],gx-ix),gz-iz);
        }
        public float Height(float x,float z)=>StylizedGrassTerrainShape.Height(x,z)+Sample(heightOffsets,x,z);
        public float Coverage(float x,float z)
        {
            float basis=StylizedGrassTerrainShape.Coverage(x,z),mask=Sample(coverageOffsets,x,z);
            // Signed influence reaches exactly empty/full between samples while zero preserves the procedural base.
            return mask>=0?Mathf.Lerp(basis,1,mask):basis*(1+mask);
        }

        public bool Paint(Vector2 center,float radius,float amount,float hardness,GrassTerrainBrush brush,float flattenHeight=0)
        {
            if(!IsInitialized || radius<=0 || amount<=0)return false;
            hardness=Mathf.Clamp(hardness,0,.95f);
            int x0=Mathf.Clamp(Mathf.FloorToInt(((center.x-radius)/size.x+.5f)*(resolution-1)),0,resolution-1);
            int x1=Mathf.Clamp(Mathf.CeilToInt(((center.x+radius)/size.x+.5f)*(resolution-1)),0,resolution-1);
            int z0=Mathf.Clamp(Mathf.FloorToInt(((center.y-radius)/size.y+.5f)*(resolution-1)),0,resolution-1);
            int z1=Mathf.Clamp(Mathf.CeilToInt(((center.y+radius)/size.y+.5f)*(resolution-1)),0,resolution-1);
            // Snapshot the smooth source so its result never depends on traversal direction.
            float[] source=brush==GrassTerrainBrush.Smooth?(float[])heightOffsets.Clone():heightOffsets;
            bool changed=false;
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
            {
                Vector2 p=GridPoint(x,z);float distance=Vector2.Distance(center,p)/radius;
                if(distance>=1)continue;
                float weight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(hardness,1,distance));
                int i=z*resolution+x;float a=amount*weight,oldH=heightOffsets[i],oldC=coverageOffsets[i];
                float baseHeight=StylizedGrassTerrainShape.Height(p.x,p.y),baseCoverage=StylizedGrassTerrainShape.Coverage(p.x,p.y);
                float currentCoverage=oldC>=0?Mathf.Lerp(baseCoverage,1,oldC):baseCoverage*(1+oldC);
                switch(brush)
                {
                    case GrassTerrainBrush.Raise:heightOffsets[i]+=a;break;
                    case GrassTerrainBrush.Lower:heightOffsets[i]-=a;break;
                    case GrassTerrainBrush.Flatten:heightOffsets[i]=Mathf.Lerp(oldH,flattenHeight-baseHeight,Mathf.Clamp01(a));break;
                    case GrassTerrainBrush.Smooth:
                        float sum=0;int count=0;
                        for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                        {
                            int sx=Mathf.Clamp(x+dx,0,resolution-1),sz=Mathf.Clamp(z+dz,0,resolution-1);
                            Vector2 q=GridPoint(sx,sz);sum+=StylizedGrassTerrainShape.Height(q.x,q.y)+source[sz*resolution+sx];count++;
                        }
                        heightOffsets[i]=Mathf.Lerp(oldH,sum/count-baseHeight,Mathf.Clamp01(a));break;
                    case GrassTerrainBrush.AddGrass:
                    case GrassTerrainBrush.EraseGrass:
                        float target=Mathf.Clamp01(currentCoverage+(brush==GrassTerrainBrush.AddGrass?a:-a));
                        coverageOffsets[i]=target<=0?-1:target>=1?1:target>=baseCoverage?(target-baseCoverage)/Mathf.Max(.00001f,1-baseCoverage)
                            :(target-baseCoverage)/Mathf.Max(.00001f,baseCoverage);
                        break;
                    case GrassTerrainBrush.Restore:
                        heightOffsets[i]=Mathf.MoveTowards(oldH,0,a);coverageOffsets[i]=Mathf.MoveTowards(oldC,0,a);break;
                }
                changed|=heightOffsets[i]!=oldH || coverageOffsets[i]!=oldC;
            }
            if(changed)revision++;return changed;
        }
    }
}
