Shader "Apex/Marina Road Paint" {
    Properties { [MainTexture] _MainTex ("Paint mask", 2D) = "white" {} }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass {
            ZWrite Off
            Offset -1, -1
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            struct In { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Out { float4 clip:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; half3 color:COLOR; };
            Out Vert(In v) {
                Out o; o.world=TransformObjectToWorld(v.positionOS.xyz);
                o.clip=TransformWorldToHClip(o.world); o.uv=v.uv; o.color=v.color.rgb;
                return o;
            }
            half4 Frag(Out i):SV_Target {
                half mask=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a;
                float2 grainUV=i.world.xz*18;
                float close=1-saturate(max(fwidth(grainUV.x),fwidth(grainUV.y)));
                half grain=frac(sin(dot(floor(grainUV),float2(12.99,78.21)))*4375.53);
                half3 ink=i.color*(.91h+.07h*close*(grain-.5h));
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    ink=SRGBToLinear(ink);
                #endif
                return half4(ink,mask*.94h);
            }
            ENDHLSL
        }
    }
}
