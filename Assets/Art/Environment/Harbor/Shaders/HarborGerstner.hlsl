#ifndef HARBOR_GERSTNER_INCLUDED
#define HARBOR_GERSTNER_INCLUDED
void HarborWave(float2 d,float wavelength,float amplitude,float steepness,float period,
                float3 rest,float fade,inout float3 p,inout float3 tangent,inout float3 binormal)
{
    d=normalize(d);
    float k=6.2831853/wavelength;
    float phase=k*dot(d,rest.xz)-6.2831853*_Time.y*_WaveSpeed/period;
    float sn,cs;sincos(phase,sn,cs);
    float a=amplitude*_WaveStrength*fade,qa=a*steepness;
    p+=float3(d.x*qa*cs,a*sn,d.y*qa*cs);
    tangent+=float3(-d.x*d.x*qa*k*sn,d.x*a*k*cs,-d.x*d.y*qa*k*sn);
    binormal+=float3(-d.x*d.y*qa*k*sn,d.y*a*k*cs,-d.y*d.y*qa*k*sn);
}
void HarborWaterVertex(inout float3 p,out float3 normal)
{
    float3 rest=p,t=float3(1,0,0),b=float3(0,0,1);
    float f=saturate((rest.z-7)/3);f=f*f*(3-2*f);
    HarborWave(float2(-.28,-.96),8,.045,.65,2.5,rest,f,p,t,b);
    HarborWave(float2(.76,-.65),4.2,.023,.5,1.6666667,rest,f,p,t,b);
    HarborWave(float2(-.9,-.44),12,.025,.55,3,rest,f,p,t,b);
    normal=normalize(cross(b,t));
}
float3 HarborHardStep(float3 color)
{
    float lum=max(dot(color,float3(.2126,.7152,.0722)),.0001);
    float stepped=lum<.03?.012:lum<.085?.05:lum<.19?.12:lum<.38?.26:.48;
    return color*(stepped/lum);
}
#endif
