Shader "The Visitor/Sink Water"
{
    Properties
    {
        _Tint ("Water Tint", Color) = (0.12, 0.25, 0.25, 0.82)
        _Reflection ("Reflection Tint", Color) = (0.58, 0.71, 0.64, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _Reflection;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y;
                float ripple = sin(uv.y * 45.0 + 1.2 * sin(uv.x * 8.0 - time * .55) - time * .85);
                float glint = smoothstep(.83, 1.0, ripple);
                float reflection = smoothstep(.12, .82, 1.0 - abs(uv.x - .63) * 2.0);
                float shade = sin(dot(uv, float2(12.0, 16.0)) + time * .37);
                half3 color = _Tint.rgb * (.92 + shade * .08);
                color = lerp(color, _Reflection.rgb, reflection * (.08 + glint * .20));
                return half4(color, _Tint.a);
            }
            ENDHLSL
        }
    }
}
