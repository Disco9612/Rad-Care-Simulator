// Preserve Meta hand animation, wrist fade and interaction feedback, but keep
// virtual hands below the HDR bloom range used by ceiling fixtures.
#define baseFragment RadCareOriginalFragment
#include "Packages/com.meta.xr.sdk.interaction/Runtime/Shaders/OculusHandFillCG.cginc"
#undef baseFragment
half4 baseFragment(VertexOutput i) : SV_Target
{
    half4 color = RadCareOriginalFragment(i);
    color.rgb = saturate(color.rgb);
    return color;
}
