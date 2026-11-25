// Stylized Worlds: Structures — walls, roofs, timber, stone, glass: a painted albedo with its normal map, moss (or dust,
// sand...) on surfaces that face up, snow settling on roofs and ledges in winter, rain darkening and streaking the walls,
// splash dirt at the foot of the walls, windows that light up at night, soft wrapped light.
Shader "Stylized Worlds Sampler/Structures/Structure"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        [NoScaleOffset][Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1

        [Header(Cover on top surfaces)]
        _CoverColor ("Cover (moss, dust)", Color) = (0.24, 0.40, 0.12, 1)
        _CoverAmount ("Cover Amount", Range(0, 1)) = 0
        _CoverSharpness ("Cover Edge", Range(1, 12)) = 5

        [Header(Season)]
        [Enum(Spring,0,Summer,1,Autumn,2,Winter,3)] _Season ("Season (without a Season Controller)", Float) = 1
        [Toggle] _FixedSeason ("Fixed Season (ignore the Season Controller)", Float) = 0
        _SnowColor ("Snow", Color) = (0.90, 0.93, 0.98, 1)
        _SnowCover ("Snow Cover in Winter", Range(0, 1)) = 0.75

        [Header(Weathering)]
        _DirtColor ("Splash Dirt", Color) = (0.42, 0.36, 0.28, 1)
        _DirtHeight ("Splash Dirt Height (m)", Range(0, 2)) = 0.7
        _Occlusion ("Inner Shade (vertex)", Range(0, 1)) = 0.6
        _Wrap ("Soft Light", Range(0, 1)) = 0.3
        _Variation ("Variety Between Copies", Range(0, 1)) = 0.35
        _VertexVariety ("Variety Between Parts", Range(0, 1)) = 0.25

        [Header(Lit windows (albedo alpha))]
        [HDR] _GlowColor ("Window Light", Color) = (1.6, 0.85, 0.35, 1)
        _GlowStrength ("Window Light Strength", Range(0, 10)) = 0
        _LightsOn ("Lights On (without a Day Night controller)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "StructureCommon.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _Tint, _CoverColor, _SnowColor, _DirtColor, _GlowColor;
            half _BumpScale, _CoverAmount, _CoverSharpness, _Season, _FixedSeason, _SnowCover;
            half _DirtHeight, _Occlusion, _Wrap, _Variation, _VertexVariety, _GlowStrength, _LightsOn;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
            half4 tangentWS : TEXCOORD3;
            half4 color : TEXCOORD4;
            half fog : TEXCOORD5;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings StructVert(Attributes v)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
            float3 p = TransformObjectToWorld(v.positionOS.xyz);
            o.positionWS = p;
            o.positionCS = TransformWorldToHClip(p);
            o.normalWS = n.normalWS;
            o.tangentWS = half4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
            o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
            o.color = v.color;
            o.fog = ComputeFogFactor(o.positionCS.z);
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex StructVert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(i.positionCS);
            #endif
                half4 w = SB_SeasonWeights(SB_Season(_Season, _FixedSeason));
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 albedo = baseTex.rgb * _Tint.rgb;
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                half3 bitangent = i.tangentWS.w * cross(i.normalWS, i.tangentWS.xyz);
                half3 N = normalize(mul(nTS, half3x3(i.tangentWS.xyz, bitangent, i.normalWS)));
                albedo = SB_Vary(albedo, i.positionWS, _Variation);
                albedo *= 1 + (i.color.g - 0.5) * _VertexVariety;
                half lum = dot(albedo, half3(0.3, 0.55, 0.15));
                // splash dirt at the foot of the walls (height above the object's origin = its ground floor)
                float baseY = UNITY_MATRIX_M._m13;
                half dirt = (1 - saturate((i.positionWS.y - baseY) / max(_DirtHeight, 0.01))) * saturate(1 - abs(N.y) * 1.5);
                dirt *= 0.55 + 0.45 * SB_Noise(i.positionWS.xz * 2.3 + i.positionWS.y);
                albedo = lerp(albedo, albedo * _DirtColor.rgb * 1.6, dirt);
                // cover (moss, dust) on top surfaces, in patches that follow the painted texture
                half up = N.y + (lum - 0.35) * 0.6;
                half open = 1.0 - i.color.r;                                  // 0 under a roof (indoors, under the eaves): stays dry
                half cover = saturate((up - (1.0 - _CoverAmount)) * _CoverSharpness) * step(0.001, _CoverAmount) * (1.0 - w.w) * open;
                half patch = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv * 0.37 + 0.5).rgb, half3(0.3, 0.55, 0.15));
                cover *= smoothstep(0.12, 0.24, patch + (N.y - 0.8) * 0.3);
                albedo = lerp(albedo, _CoverColor.rgb * (0.75 + lum), cover);
                // snow settles on roofs and ledges (a roof's slope still holds it), thinner on the steepest
                half snow = SB_Snow(w) * saturate((up - (1.0 - _SnowCover)) * 5.0) * open;
                albedo = lerp(albedo, _SnowColor.rgb * (0.9 + 0.2 * lum), snow);
                half occ = lerp(1.0 - _Occlusion, 1.0, i.color.a);
                half wet = SB_Wet(albedo, saturate(N.y + 0.6) * (1 - snow)) * open;
                half streak = SB_RainStreaks(i.positionWS, N) * (1 - snow) * open;
                albedo *= 1 - streak * 0.45;
                wet = max(wet, streak * 0.3);
                half3 colour = SB_Shade(albedo * occ, N, i.positionWS, i.positionCS, _Wrap, 0.0, 1.0);
                colour += SB_WetGloss(wet, N, i.positionWS);
                if (_GlowStrength > 0)
                {   // lit windows: the controller's night, or the material's own switch; a faint candle flicker
                    half night = max(_LightsOn, smoothstep(0.35, 0.8, _SB_Night));
                    half flicker = 0.9 + 0.1 * sin(_Time.y * 7.1 + SB_InstanceRandom() * 40) * sin(_Time.y * 3.3 + i.positionWS.x);
                    colour = lerp(colour, _GlowColor.rgb * baseTex.a * _GlowStrength * flicker, night * baseTex.a);
                }
                colour = SB_Fog(colour, i.positionWS, i.fog);
                return half4(colour, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            Varyings Vert(Attributes v)
            {
                Varyings o = StructVert(v);
                o.positionCS = SB_ShadowPositionCS(o.positionWS, o.normalWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(i.positionCS);
            #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex StructVert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            half4 Frag(Varyings i) : SV_Target
            {
            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(i.positionCS);
            #endif
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex StructVert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            half4 Frag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0.0); }
            ENDHLSL
        }
    }
    FallBack Off
}
