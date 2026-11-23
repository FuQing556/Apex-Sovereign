Shader "Apex/Tyre print" {
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" "RenderType"="Transparent" }
        Pass {
            Blend DstColor Zero
            ColorMask RGB
            Cull Off ZWrite Off Offset -1,-1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float4 position:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Surface { float4 pos:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            Surface vert(Input v) {
                Surface o; o.pos=TransformObjectToHClip(v.position.xyz); o.color=v.color; o.uv=v.uv; return o;
            }
            half4 frag(Surface i):SV_Target {
                float u=i.uv.x, v=i.uv.y;
                float grain=sin(v*19.4+sin(v*6.3))*sin(v*3.7);
                float edge=smoothstep(0,.11,min(u,1-u)+.018*grain);
                float grooves=abs(frac(u*4+.025*sin(v*12))-.5);
                float aa=clamp(fwidth(u)*4,.014,.35);
                float tread=lerp(.62,1,smoothstep(.055-aa,.055+aa,grooves));
                float alpha=saturate(i.color.a*edge*tread*(.94+.06*grain));
                return half4(lerp(half3(1,1,1),i.color.rgb,alpha),1);
            }
            ENDHLSL
        }
    }
}
