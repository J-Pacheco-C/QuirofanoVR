// Dibuja un color con sombreado simple siempre por encima del vidrio (para que el balin del flujometro se vea)
Shader "Quirofano/ZAlways"
{
    Properties { _Color ("Color", Color) = (1, 0.45, 0.05, 1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ZAlways"
            Tags { "LightMode"="UniversalForward" }
            ZTest Always
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 nWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.nWS = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half l = saturate(dot(normalize(i.nWS), normalize(float3(0.35, 0.8, 0.5)))) * 0.55 + 0.45;
                return half4(_Color.rgb * l, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
