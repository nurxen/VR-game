// Terreno, vegetación y cuevas del Mundo abierto: color por vértice × (luz del sol/luna + luz ambiente) + niebla.
// Sin texturas ni sombras en tiempo real: lo más barato para Quest 2 y con aspecto low poly.
// Textura del suelo (opcional): se repite cada «Tamaño» metros vista desde arriba y matiza el color del bioma
// (no lo cambia: la hierba en la pradera sale verde y en el desierto, color arena). Cuánta se ve en cada bioma lo dice
// su BiomeProfile (Ground Texture), que llega en el alfa del color de cada vértice. Cuesta una lectura de textura por píxel.
Shader "VRKit/TerrenoLowPoly"
{
    Properties
    {
        _BaseColor ("Tinte", Color) = (1, 1, 1, 1)
        _Ambient ("Fuerza de la luz ambiente", Range(0, 2)) = 1
        _Wrap ("Luz envolvente (suaviza las sombras)", Range(0, 1)) = 0.25
        [NoScaleOffset] _DetailMap ("Textura del suelo (opcional)", 2D) = "white" {}
        _DetailSize ("Tamaño de la textura (metros)", Float) = 4
        _DetailStrength ("Fuerza de la textura", Range(0, 1)) = 1
        [Toggle] _DetailColor ("Usar también sus colores (si no, solo luces y sombras)", Float) = 0
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
                half _DetailSize;
                half _DetailStrength;
                half _DetailColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; half fog : TEXCOORD0; float2 uv : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);

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
                o.color = half4(v.color.rgb * _BaseColor.rgb * light, v.color.a * _DetailStrength);
                o.uv = posWS.xz / max(_DetailSize, 0.01);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 col = i.color.rgb;
                if (i.color.a > 0.004h)
                {
                    half3 t = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, i.uv).rgb;
                    // El último mipmap es el color medio de la textura: así la textura matiza sin oscurecer ni aclarar
                    half3 avg = max(SAMPLE_TEXTURE2D_LOD(_DetailMap, sampler_DetailMap, i.uv, 12).rgb, half3(0.04h, 0.04h, 0.04h));
                    half lum = dot(t, half3(0.299h, 0.587h, 0.114h)) / max(dot(avg, half3(0.299h, 0.587h, 0.114h)), 0.04h);
                    half3 k = _DetailColor > 0.5h ? t / avg : half3(lum, lum, lum);
                    col *= lerp(half3(1.0h, 1.0h, 1.0h), k, i.color.a);
                }
                return half4(MixFog(col, i.fog), 1.0);
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
                half _DetailSize;
                half _DetailStrength;
                half _DetailColor;
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
