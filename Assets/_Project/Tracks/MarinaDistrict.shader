Shader "Apex/Marina District" {
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Input {float4 pos:POSITION; float3 normal:NORMAL; half4 ink:COLOR; float2 uv:TEXCOORD0;};
            struct Vary {float4 clip:SV_POSITION; float3 world:TEXCOORD0; float2 facade:TEXCOORD1; half4 ink:COLOR; half2 style:TEXCOORD2;};
            Vary Vert(Input i){
                Vary o;o.world=TransformObjectToWorld(i.pos.xyz);o.clip=TransformWorldToHClip(o.world);
                float3 n=TransformObjectToWorldNormal(i.normal);
                o.facade=float2(dot(o.world.xz,float2(n.z,-n.x)),dot(o.world.xz,n.xz));
                o.style=half2(i.uv.y,.80+.19*dot(n,normalize(float3(-.4,.8,.5))));
                o.ink=i.ink;return o;
            }
            float Noise(float2 p){
                float3 h=frac(p.xyx*float3(.1031,.1030,.0973));
                h+=dot(h,h.yzx+33.33);return frac((h.x+h.y)*h.z);
            }
            half4 Frag(Vary i):SV_Target{
                float3 col=i.ink.rgb*i.style.y;
                float glow=i.ink.a*.65;
                if(i.style.x>.5){
                    // World-metre spacing keeps windows the same size on every length of facade.
                    bool sheds=i.style.x>3.5;bool glass=i.style.x<1.5;
                    float2 bay=float2(i.facade.x/(sheds?5.8:glass?3.3:3.65),i.world.y/(sheds?5.5:glass?3.8:3.25));
                    float2 fw=clamp(fwidth(bay),.008,.49);
                    float2 d=abs(frac(bay)-.5);
                    float2 win=1-smoothstep(float2(glass?.40:.32,sheds?.14:.28)-fw*.5,
                                             float2(glass?.40:.32,sheds?.14:.28)+fw*.5,d);
                    float seed=floor(i.facade.y*.23);
                    float rnd=Noise(floor(bay)+seed);
                    float lit=smoothstep(.49,.72,rnd);
                    float warm=Noise(float2(floor(bay.y*.34),seed));
                    float3 lamp=lerp(float3(.48,.69,.77),float3(.93,.76,.52),warm);
                    float3 pane=lerp(col*.58+float3(.035,.055,.09),lamp,lit);
                    float resolve=1-smoothstep(.24,1.2,max(fwidth(bay.x),fwidth(bay.y)));
                    col=lerp(col,pane,win.x*win.y*resolve);
                    col=lerp(col,lerp(i.ink.rgb*i.style.y,lamp,.15),1-resolve);
                    glow+=win.x*win.y*lit*resolve*.42;
                }
                float haze=1-exp(-distance(i.world,_WorldSpaceCameraPos)*.00025);
                col=lerp(col,float3(.105,.143,.213),haze);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    col=SRGBToLinear(saturate(col));
                #endif
                return half4(col*(1+glow),1);
            }
            ENDHLSL
        }
    }
}
