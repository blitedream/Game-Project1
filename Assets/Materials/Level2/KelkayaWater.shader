Shader "GP1/Level2/Kelkaya Water"
{
    Properties
    {
        _ShallowColor("Shallow Color", Color) = (0.08, 0.45, 0.5, 0.16)
        _DeepColor("Deep Color", Color) = (0.015, 0.12, 0.18, 0.42)
        _FresnelColor("Fresnel Color", Color) = (0.55, 0.96, 1, 1)
        _WaveScale("Wave Scale", Float) = 0.26
        _WaveSpeed("Wave Speed", Float) = 0.65
        _WaveHeight("Wave Height", Float) = 0.09
        _Smoothness("Smoothness", Range(0,1)) = 0.92
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardWater"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float wave : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FresnelColor;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveHeight;
                float _Smoothness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float t = _Time.y * _WaveSpeed;
                float waveA = sin((world.x + world.z * 0.72) * _WaveScale + t);
                float waveB = cos((world.z - world.x * 0.46) * (_WaveScale * 1.37) - t * 0.83);
                float wave = (waveA + waveB) * 0.5;
                world.y += wave * _WaveHeight;

                float dx = cos((world.x + world.z * 0.72) * _WaveScale + t) * _WaveScale * _WaveHeight;
                float dz = -sin((world.z - world.x * 0.46) * (_WaveScale * 1.37) - t * 0.83)
                    * (_WaveScale * 1.37) * _WaveHeight;

                output.positionWS = world;
                output.positionCS = TransformWorldToHClip(world);
                output.normalWS = normalize(float3(-dx, 1.0, -dz));
                output.wave = wave;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 viewDir = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                float facing = saturate(abs(dot(viewDir, input.normalWS)));
                float fresnel = pow(1.0 - facing, 2.7);
                float variation = saturate(input.wave * 0.18 + 0.62);
                half4 water = lerp(_DeepColor, _ShallowColor, variation);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceEye = -TransformWorldToView(input.positionWS).z;
                float thickness = max(0, sceneEye - surfaceEye);
                float absorption = 1 - exp(-thickness * .045);
                water = lerp(_ShallowColor, _DeepColor, absorption);
                Light sun = GetMainLight();
                float3 normal = viewDir.y >= 0 ? input.normalWS : -input.normalWS;
                float highlight = pow(saturate(dot(normal, SafeNormalize(viewDir + sun.direction))), 160);
                water.rgb = lerp(water.rgb, _FresnelColor.rgb, fresnel * .55);
                water.rgb += sun.color * highlight * .35;
                water.a = lerp(water.a, .64, fresnel);
                // From below the surface stays transmissive, rather than a cyan ceiling.
                if(viewDir.y < 0) water.a *= .65;
                water.rgb = MixFog(water.rgb, ComputeFogFactor(TransformWorldToHClip(input.positionWS).z));
                return water;
            }
            ENDHLSL
        }
    }
}
