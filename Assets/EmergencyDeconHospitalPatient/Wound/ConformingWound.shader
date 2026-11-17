Shader "RadCare/ConformingWound" {
 Properties { _BaseMap("Blood texture",2D)="white"{} _Opacity("Opacity",Range(0,1))=1 }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
 Pass { Tags {"LightMode"="UniversalForward"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma multi_compile_instancing
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseMap_ST; float _Opacity;
 CBUFFER_END
 struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
 struct V {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float2 uv:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
 V vert(A i){V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;return o;}
 half4 frag(V i):SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 half4 t=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,float2(i.uv.x,1-i.uv.y));
 float2 p=(i.uv-.5)*2;float edge=1-smoothstep(.65,1,length(p));
 float stain=saturate((.12-t.g)*10);float a=edge*stain*_Opacity*.86;
 Light l=GetMainLight();half3 lighting=max(SampleSH(normalize(i.normalWS)),half3(.22,.22,.22))+l.color*saturate(dot(normalize(i.normalWS),l.direction))*.65;
 return half4(t.rgb*half3(.65,.45,.45)*lighting,a);
 }
 ENDHLSL
 }} }


