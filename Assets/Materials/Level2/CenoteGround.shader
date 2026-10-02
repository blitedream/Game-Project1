Shader "GP1/Cenote Ground"
{
 SubShader { Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
 Pass { HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 struct V{float4 p:POSITION;float3 n:NORMAL;};struct O{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;};
 O vert(V v){O o;o.w=TransformObjectToWorld(v.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(v.n);return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
 half4 frag(O i):SV_Target{float n=noise(i.w.xz*.16)*.65+noise(i.w.xz*.73)*.25+noise(i.w.xz*5)*.1;float3 c=lerp(float3(.16,.23,.10),float3(.43,.40,.31),smoothstep(.28,.76,n));Light l=GetMainLight();c*=.45+saturate(dot(normalize(i.n),l.direction))*.65;return half4(c,1);}
 ENDHLSL }
 }
}
