Shader "GP1/Cenote Sky"
{
    Properties
    {
        _ZenithColor ("Sky blue", Color) = (0.22, 0.43, 0.68, 1)
        _HorizonColor ("Horizon haze", Color) = (0.70, 0.78, 0.80, 1)
        _GroundColor ("Lower hemisphere", Color) = (0.30, 0.34, 0.32, 1)
        _CloudAmount ("Cloud amount", Range(0,1)) = 0.28
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _ZenithColor, _HorizonColor, _GroundColor;
            float _CloudAmount;
            struct v2f { float4 pos:SV_POSITION; float3 ray:TEXCOORD0; };
            v2f vert(float4 p:POSITION)
            {
                v2f o; o.pos=UnityObjectToClipPos(p); o.ray=p.xyz; return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),
                    lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            half4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.ray);
                float3 c=lerp(_HorizonColor.rgb,_ZenithColor.rgb,pow(saturate(d.y),.6));
                c=lerp(c,_GroundColor.rgb,smoothstep(0,.5,-d.y));
                float2 p=d.xz/max(.18,d.y)*1.8;
                float n=noise(p)*.57+noise(p*2.1)*.28+noise(p*4.3)*.15;
                float cloud=smoothstep(.55,.78,n)*smoothstep(.04,.25,d.y)*_CloudAmount;
                c=lerp(c,float3(.92,.94,.94),cloud);
                float3 sunDirection=normalize(float3(.3,.7,.4));
                float facing=saturate(dot(d,sunDirection));
                float glow=pow(facing,32)*.10+pow(facing,1000)*.55;
                return half4(c+float3(1,.93,.8)*glow*smoothstep(0,.08,d.y),1);
            }
            ENDHLSL
        }
    }
}
