#ifndef HARBOR_GERSTNER_INCLUDED
#define HARBOR_GERSTNER_INCLUDED
void HarborWave(float2 d,float wavelength,float amplitude,float steepness,float period,
                float strength,float speed,float3 rest,float fade,inout float3 p,inout float3 tangent,inout float3 binormal)
{
    d=dot(d,d)>0.0001?normalize(d):float2(0,1);
    float k=6.2831853/max(wavelength,.1);
    float phase=k*dot(d,rest.xz)-6.2831853*_Time.y*speed/period;
    float sn,cs;sincos(phase,sn,cs);
    float a=amplitude*strength*fade,qa=a*steepness;
    p+=float3(d.x*qa*cs,a*sn,d.y*qa*cs);
    tangent+=float3(-d.x*d.x*qa*k*sn,d.x*a*k*cs,-d.x*d.y*qa*k*sn);
    binormal+=float3(-d.x*d.y*qa*k*sn,d.y*a*k*cs,-d.y*d.y*qa*k*sn);
}
float HarborShoreFade(float3 rest)
{
    float f=saturate((rest.z-7)/3);return f*f*(3-2*f);
}
void HarborWaterVertex(inout float3 p)
{
    float3 rest=p,t=float3(1,0,0),b=float3(0,0,1);
    // Layer 1 displaces geometry; its long wavelength fits the graded far mesh.
    HarborWave(_SwellDirection.xy,_SwellLength,.065,.6,3,
        _WaveStrength,_WaveSpeed,rest,HarborShoreFade(rest),p,t,b);
}
float3 HarborWaterNormal(float3 rest, float detailFilter)
{
    float3 p=rest,t=float3(1,0,0),b=float3(0,0,1);
    float f=HarborShoreFade(rest);
    HarborWave(_SwellDirection.xy,_SwellLength,.065,.6,3,
        _WaveStrength,_WaveSpeed,rest,f,p,t,b);
    // Layer 2 supplies analytic ripple normals without adding mesh subdivisions.
    HarborWave(_RippleDirection.xy,_RippleLength,.018,.45,1.6666667,
        _RippleStrength,_RippleSpeed,rest,f*detailFilter,p,t,b);
    return normalize(cross(b,t));
}
float3 HarborHardStep(float3 color)
{
    float lum=max(dot(color,float3(.2126,.7152,.0722)),.0001);
    float stepped=lum<.03?.012:lum<.085?.05:lum<.19?.12:lum<.38?.26:.48;
    return color*(stepped/lum);
}
#endif
