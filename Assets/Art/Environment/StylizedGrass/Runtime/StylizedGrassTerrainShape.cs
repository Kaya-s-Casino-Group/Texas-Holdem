using UnityEngine;

namespace StylizedGrass
{
    /// <summary>Overlapping, irregular landforms without a road. Geometry and grass share this surface.</summary>
    public static class StylizedGrassTerrainShape
    {
        // Fixed design coordinates align the landforms to the existing composition, not to a moving camera.
        static readonly Vector2 Origin=new Vector2(7.83f,-9.89f);
        static readonly Vector2 Forward=new Vector2(-.485643f,.874157f);
        static readonly Vector2 Right=new Vector2(.874157f,.485643f);

        static void Coordinates(float x,float z,out float u,out float d)
        {
            Vector2 p=new Vector2(x,z)-Origin;u=Vector2.Dot(p,Right);d=Vector2.Dot(p,Forward);
        }
        static float Ridge(float value,float width)
        {
            float t=Mathf.Clamp01(1-Mathf.Abs(value)/width);
            return t*t*(2-t);
        }
        static float Mound(float u,float d,float centerU,float centerD,float width,float depth,float skew)
        {
            return Ridge(u-centerU,width)*Ridge(d-centerD-skew*(u-centerU),depth);
        }
        public static float Height(float x,float z)
        {
            Coordinates(x,z,out float u,out float d);
            // Finite oblique forms overlap across the whole image, including its center.
            // No path-shaped mask is applied to height or coverage.
            float front=.46f*Mound(u,d,-2.6f,3.4f,5.2f,2.8f,.28f)
                       +.34f*Mound(u,d,2.8f,5.3f,4.4f,2.6f,-.32f)
                       +.28f*Mound(u,d,.1f,7.1f,3.8f,2.3f,.18f);
            float middle=.69f*Mound(u,d,-3.5f,10.5f,5.3f,3.2f,-.22f)
                        +.72f*Mound(u,d,2.6f,12.6f,5.8f,3.1f,.3f)
                        +.36f*Mound(u,d,-.4f,14.8f,3.4f,2.3f,-.38f);
            float back=.98f*Mound(u,d,-2.7f,18.6f,7.2f,3.9f,.18f)
                      +.86f*Mound(u,d,4.8f,20.8f,5.6f,3.4f,-.24f);
            float broken=(Mathf.PerlinNoise(x*.39f+29,z*.43f+7)-.5f)*.24f;
            float fine=(Mathf.PerlinNoise(x*1.1f+8,z*1.1f+3)-.5f)*.035f;
            float rearRise=.52f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(15,25,d));
            float floor=2.23f+.016f*Mathf.Clamp(d,-3,26)+rearRise;
            // Lift the foreground shelf gently, tapering into the existing middle ground.
            float foregroundLift=.28f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2,10,d)));
            return floor+front+middle+back+broken+fine+foregroundLift;
        }
        public static float Coverage(float x,float z)
        {
            Coordinates(x,z,out float u,out float d);
            float foreground=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(4,12,d));
            float patches=Mathf.SmoothStep(.24f,.78f,Mathf.PerlinNoise(x*.48f+5,z*.48f+31));
            float clearings=Mathf.SmoothStep(.64f,.84f,Mathf.PerlinNoise(x*.72f+44,z*.72f+9));
            // Denser foreground clusters with small isolated gaps, never a connected bare strip.
            return Mathf.Clamp01(Mathf.Lerp(.3f,.64f,foreground)+patches*Mathf.Lerp(.48f,.32f,foreground)-clearings*.4f);
        }
        public static float GrassScale(float x,float z)
        {
            Coordinates(x,z,out _,out float d);
            return Mathf.Lerp(1.3f,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,10,d)));
        }
    }
}
