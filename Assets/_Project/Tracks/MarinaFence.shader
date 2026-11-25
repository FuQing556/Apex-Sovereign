Shader "Apex/Marina Catch Fence" {
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            struct Input { float4 pos:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 clip:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; half3 color:COLOR; };
            Varyings Vert(Input v){
                Varyings o;
                o.world=TransformObjectToWorld(v.pos.xyz);
                o.clip=TransformWorldToHClip(o.world);
                o.uv=v.uv;
                o.color=v.color.rgb;
                return o;
            }
            // Integral of one periodic wire pattern. Differences give the fraction
            // of a pixel occupied by wire, including when many diamonds fit in a pixel.
            float2 WireIntegral(float2 phase,float thickness){
                return floor(phase)*thickness+min(frac(phase),thickness);
            }
            half4 Frag(Varyings i):SV_Target {
                float2 grid=float2(i.uv.x*5+i.uv.y*3,i.uv.x*5-i.uv.y*3);
                const float thickness=.13;
                float2 footprint=clamp(fwidth(grid),.001,3);
                float2 phase=frac(grid+thickness*.5);
                float2 coverage=(WireIntegral(phase+.5*footprint,thickness)
                    -WireIntegral(phase-.5*footprint,thickness))/footprint;
                coverage=lerp(saturate(coverage),thickness,smoothstep(.8,2.5,footprint));
                float opacity=1-(1-coverage.x)*(1-coverage.y);
                float dist=distance(i.world,_WorldSpaceCameraPos);
                float3 wire=lerp(i.color*.87,float3(.095,.128,.207),1-exp(-dist*.00022));
                #if !defined(UNITY_COLORSPACE_GAMMA)
                wire=SRGBToLinear(wire);
                #endif
                return half4(wire,opacity*lerp(.95,.70,smoothstep(300,850,dist)));
            }
            ENDHLSL
        }
    }
}
