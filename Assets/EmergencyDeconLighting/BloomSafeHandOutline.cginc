// Preserve Meta hand animation, wrist fade and interaction feedback, but keep
// virtual hands below the HDR bloom range used by ceiling fixtures.
#define outlineFragment RadCareOriginalFragment
#include "Packages/com.meta.xr.sdk.interaction/Runtime/Shaders/OculusHandOutlineCG.cginc"
#undef outlineFragment
half4 outlineFragment(OutlineVertexOutput i) : SV_Target
{
    half4 color = RadCareOriginalFragment(i);
    color.rgb = saturate(color.rgb);
    return color;
}
