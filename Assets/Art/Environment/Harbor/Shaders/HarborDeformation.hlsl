#ifndef HARBOR_DEFORMATION_INCLUDED
#define HARBOR_DEFORMATION_INCLUDED
// Exported vertex colors: R=pin falloff, G=distance from suspension, B=across cloth.
void HarborDeform(inout float3 p, inout float3 normal, half4 data)
{
    if (_DeformMode < .5) return;
    float phase=data.g*6.2831853+data.b*1.3;
    float time=_Time.y*_WindSpeed*1.5707963;
    float a=_WindStrength*data.r;
    p += _FlagDirection.xyz*a*(sin(phase+time)+.23*sin(phase*2+time*2));
    p += _FlagSide.xyz*a*.16*cos(phase+time);
    // Low-amplitude cloth bending in the shading frame, also used in depth/shadow passes.
    normal=normalize(normal+float3(0,-a*3*cos(phase+time),0));
}
#endif
