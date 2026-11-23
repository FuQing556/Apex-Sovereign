Shader "Apex/Ghost body" {
    Properties {
        _Tint ("Tint", Color) = (.24,.96,.78,1)
        _Opacity ("Coverage", Range(0,1)) = .75
        _Shade ("Panel value", Float) = 1
    }
    SubShader {
        Tags { "Queue"="AlphaTest+10" "RenderType"="TransparentCutout" }
        Pass {
            Cull Back ZWrite On
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            half4 _Tint;
            float _Opacity, _Shade;
            struct Vertex { float4 pos:POSITION; float3 normal:NORMAL; };
            struct Surface { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 world:TEXCOORD1; };
            Surface vert(Vertex v){
                Surface o; o.world=mul(unity_ObjectToWorld,v.pos).xyz;o.pos=UnityObjectToClipPos(v.pos);
                o.normal=UnityObjectToWorldNormal(v.normal);return o;
            }
            half4 frag(Surface s):SV_Target {
                uint2 p=(uint2)s.pos.xy & 3u;
                static const float thresholds[16]={0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5};
                clip(_Opacity-(thresholds[p.y*4+p.x]+.5)/16);
                float3 n=normalize(s.normal);
                float lit=.48+.48*saturate(dot(n,normalize(float3(.4,.85,.3))));
                float rim=pow(1-saturate(dot(n,normalize(_WorldSpaceCameraPos-s.world))),3);
                return half4(_Tint.rgb*(_Shade*lit+.38*rim),1);
            }
            ENDCG
        }
    }
}
