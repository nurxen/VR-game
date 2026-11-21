// Agua del Mundo abierto: color transparente con ondas suaves (los vértices suben y bajan) y más brillo al mirar
// de lado. Se oscurece de noche con la luz ambiente. Barato: sin reflejos ni refracción.
Shader "VRKit/Agua"
{
    Properties
    {
        _ShallowColor ("Color", Color) = (0.25, 0.55, 0.75, 0.65)
        _EdgeColor ("Color al mirar de lado", Color) = (0.7, 0.85, 0.95, 0.9)
        _WaveHeight ("Altura de las ondas", Range(0, 0.5)) = 0.08
        _WaveSpeed ("Velocidad de las ondas", Range(0, 3)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _EdgeColor;
                half _WaveHeight;
                half _WaveSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; half fog : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float t = _Time.y * _WaveSpeed;
                p.y += (sin(p.x * 0.35 + t) + sin(p.z * 0.27 + t * 1.3)) * _WaveHeight;
                float3 viewDir = normalize(GetWorldSpaceViewDir(p));
                half fresnel = pow(1.0 - saturate(viewDir.y), 3.0);
                half4 c = lerp(_ShallowColor, _EdgeColor, fresnel);
                Light sun = GetMainLight();
                c.rgb *= sun.color * 0.6 + unity_AmbientSky.rgb;
                o.positionCS = TransformWorldToHClip(p);
                o.color = c;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return half4(MixFog(i.color.rgb, i.fog), i.color.a);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
