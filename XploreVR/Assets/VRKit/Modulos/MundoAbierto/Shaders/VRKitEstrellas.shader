// Estrellas y luna del SkyCycle. Suman luz (aditivo), sin niebla, y solo se ven de noche (_VRKitNight global).
// Las estrellas parpadean un poco; la luna (uv.y > 1) es un disco que no parpadea.
Shader "VRKit/Estrellas"
{
    Properties
    {
        _Brightness ("Brillo", Range(0, 3)) = 1.3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Brightness;
            CBUFFER_END
            half _VRKitNight;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                bool isMoon = i.uv.y > 1.5;
                float2 p = float2(i.uv.x, frac(i.uv.y)) * 2.0 - 1.0;
                float d = dot(p, p);
                half shape = isMoon ? smoothstep(1.0, 0.85, d) : saturate(1.0 - d) * saturate(1.0 - d);
                half twinkle = isMoon ? 1.0 : 0.75 + 0.25 * sin(_Time.y * 2.3 + i.color.a * 6.2831);
                half3 c = i.color.rgb * shape * twinkle * _Brightness * _VRKitNight;
                return half4(c, 1.0);
            }
            ENDHLSL
        }
    }
}
