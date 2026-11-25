Shader "Apex/Marina Sky" {
    SubShader {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct In { float4 vertex:POSITION; };
            struct Out { float4 clip:SV_POSITION; float3 dir:TEXCOORD0; };
            Out Vert(In i) { Out o; o.clip=TransformObjectToHClip(i.vertex.xyz); o.dir=i.vertex.xyz; return o; }
            half4 Frag(Out i):SV_Target {
                float3 d=normalize(i.dir);
                float a=saturate(d.y);
                float3 col=lerp(float3(.14,.165,.265),float3(.018,.028,.084),sqrt(a));
                float moon=distance(d,normalize(float3(-.50,.32,-.70)));
                col+=float3(.75,.84,.92)*(1-smoothstep(.012,.014,moon))+.07*exp(-moon*24);
                float2 coord=d.xz/(max(.1,d.y)+.38)*320;
                float star=frac(sin(dot(floor(coord),float2(19.51,42.36)))*3575.7);
                col+=smoothstep(.992,1,star)*saturate(1-length(frac(coord)-.5)*4)*.35*smoothstep(.08,.4,a);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                col=SRGBToLinear(saturate(col));
                #endif
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
