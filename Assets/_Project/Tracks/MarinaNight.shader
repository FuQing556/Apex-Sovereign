Shader "Apex/Marina Night" {
    Properties { _Surface ("Surface (0 city, 1 road, 2 bay, 3 display, 5 paving)", Float) = 0 }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct In { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Out { float4 clip:SV_POSITION; float3 world:TEXCOORD0; half4 color:COLOR; float2 uv:TEXCOORD1; half shade:TEXCOORD2; };
            CBUFFER_START(UnityPerMaterial)
            float _Surface;
            CBUFFER_END
            Out Vert(In v) {
                Out o;
                o.world=TransformObjectToWorld(v.positionOS.xyz);
                o.clip=TransformWorldToHClip(o.world);
                o.color=v.color; o.uv=v.uv;
                float3 n=TransformObjectToWorldNormal(v.normalOS);
                o.shade=0.78+0.22*dot(n,normalize(float3(-0.45,0.72,0.52)));
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(fmod(p,4096.0),float2(41.13,79.73)))*4375.53); }
            half4 Frag(Out i):SV_Target {
                float3 col=i.color.rgb*i.shade;
                float dist=distance(i.world,_WorldSpaceCameraPos);
                float emission=0;
                if (_Surface>4.5) {
                    float2 grid=i.world.xz/3.2;
                    float2 joint=abs(frac(grid+.5)-.5)/max(fwidth(grid),.001);
                    col*=1-.09*saturate(1-min(joint.x,joint.y))*saturate(1-dist/115);
                    col+=(Hash(floor(i.world.xz*7))-.5)*.024*saturate(1-dist/80);
                } else if (_Surface>2.5) {
                    float2 p=i.uv-.5; p.x*=2.25;
                    float scan=.76+.24*sin((i.uv.y*105-_Time.y*2)*6.283);
                    float orbit=length(float2(p.x+.28,p.y)*float2(.94,1.27));
                    float logo=1-smoothstep(.005,.021,abs(orbit-.29));
                    float wave=abs(p.y-.10*sin(p.x*8-_Time.y*1.6)-.05*sin(p.x*20+_Time.y));
                    float pulse=1-smoothstep(.003,.024,wave);
                    float bars=step(.55,frac(i.uv.x*35))*step(abs(p.y),.06+.11*abs(sin(p.x*5+_Time.y)))*step(.03,p.x);
                    col=(float3(.04,.17,.36)+float3(.07,.69,.94)*(logo+pulse+bars)*scan);
                    emission=(logo+pulse+bars)*.95;
                } else if (_Surface>1.5) {
                    float2 p=i.world.xz;
                    float wave=sin(p.y*1.7+sin(p.x*0.11+_Time.y)*2.8+_Time.y*1.8);
                    float stripes=pow(saturate(sin(p.x*0.037+sin(p.y*0.05)*0.6)),8);
                    float shore=exp(-min(i.uv.x,1-i.uv.x)*4.2);
                    float3 lights=lerp(float3(.96,.48,.19),float3(.26,.79,.98),.5+.5*sin(p.x*.058));
                    col+=lights*stripes*shore*(.12+.62*saturate(wave));
                } else if (_Surface>0.5) {
                    float light=0.5+0.5*cos(i.uv.y*(6.283185/32.0));
                    col*=0.69+0.36*light;
                    col+=.055*(smoothstep(.32,.5,abs(i.uv.x-.5)))*float3(.9,.83,.81);
                    col+=(Hash(floor(i.world.xz*13))-0.5)*0.035*saturate(1-dist/85);
                } else {
                    float bright=max(i.color.r,max(i.color.g,i.color.b));
                    emission=i.color.a*smoothstep(.68,.99,bright)*(.55+saturate(bright-min(i.color.r,min(i.color.g,i.color.b))));
                    col=lerp(col,i.color.rgb,saturate(emission));
                }
                col=lerp(col,float3(0.095,0.128,0.207),saturate(1-exp(-dist*0.00022)));
                #if !defined(UNITY_COLORSPACE_GAMMA)
                col=SRGBToLinear(saturate(col));
                #endif
                col*=1+emission*1.8;
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
