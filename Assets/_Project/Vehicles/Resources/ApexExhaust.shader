Shader "Apex/Exhaust" {
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float4 position : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Varyings vert(Input v) { Varyings o; o.position=TransformObjectToHClip(v.position.xyz); o.uv=v.uv; o.color=v.color; return o; }
            half4 frag(Varyings i) : SV_Target {
                float edge=saturate(1-abs(2*i.uv.y-1));
                float core=edge*edge*(1-i.uv.x);
                return half4(lerp(i.color.rgb,half3(.85,1,1),core),edge*(1-i.uv.x)*i.color.a);
            }
            ENDHLSL
        }
    }
}
