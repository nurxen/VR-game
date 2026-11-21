// Terreno, vegetación y cuevas del Mundo abierto: color por vértice × (luz del sol/luna + luz ambiente) + niebla.
// Sin texturas ni sombras en tiempo real: lo más barato para Quest 2 y con aspecto low poly.
// Para un estilo con texturas, cambiad el material del OpenWorld por otro (el color por vértice se puede ignorar).
Shader "VRKit/TerrenoLowPoly"
{
    Properties
    {
        _BaseColor ("Tinte", Color) = (1, 1, 1, 1)
        _Ambient ("Fuerza de la luz ambiente", Range(0, 2)) = 1
        _Wrap ("Luz envolvente (suaviza las sombras)", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Ambient;
                half _Wrap;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half3 color : COLOR; half fog : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                half3 n = normalize(TransformObjectToWorldNormal(v.normalOS));
                Light sun = GetMainLight();
                half ndl = saturate((dot(n, sun.direction) + _Wrap) / (1.0 + _Wrap));
                half3 light = sun.color * ndl + unity_AmbientSky.rgb * _Ambient;
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; ++i)
                {
                    Light l = GetAdditionalLight(i, posWS);
                    light += l.color * l.distanceAttenuation * saturate(dot(n, l.direction));
                }
                #endif
                o.positionCS = TransformWorldToHClip(posWS);
                o.color = v.color.rgb * _BaseColor.rgb * light;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return half4(MixFog(i.color, i.fog), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Ambient;
                half _Wrap;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert (Attributes v) { Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half frag (Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
