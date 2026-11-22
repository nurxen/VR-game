// Stylized Biomes — bark, rock and ground: a painted albedo with its normal map, moss (or sand, ash...) on surfaces that
// face up, snow in winter, the plant's wind on bark, soft wrapped light.
Shader "Stylized Worlds Sampler/Nature/Surface"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        [NoScaleOffset][Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1
        _MacroStrength ("Large Patches (ground)", Range(0, 1)) = 0
        _MacroScale ("Large Patch Size (m)", Float) = 40

        [Header(Cover on top surfaces)]
        _CoverColor ("Cover (moss)", Color) = (0.24, 0.40, 0.12, 1)
        _CoverAmount ("Cover Amount", Range(0, 1)) = 0
        _CoverSharpness ("Cover Edge", Range(1, 12)) = 5

        [Header(Season)]
        [Enum(Spring,0,Summer,1,Autumn,2,Winter,3)] _Season ("Season (without a Season Controller)", Float) = 1
        [Toggle] _FixedSeason ("Fixed Season (ignore the Season Controller)", Float) = 0
        _SnowColor ("Snow", Color) = (0.90, 0.93, 0.98, 1)
        _SnowCover ("Snow Cover in Winter", Range(0, 1)) = 0.75
        _SpringTint ("Spring Tint", Color) = (1, 1, 1, 1)
        _SummerTint ("Summer Tint", Color) = (1, 1, 1, 1)
        _AutumnTint ("Autumn Tint", Color) = (1, 1, 1, 1)
        _WinterTint ("Winter Tint", Color) = (1, 1, 1, 1)

        [Header(Look)]
        _Occlusion ("Inner Shade (vertex)", Range(0, 1)) = 0.6
        _Wrap ("Soft Light", Range(0, 1)) = 0.35
        _WindAmount ("Wind Amount (bark)", Range(0, 2)) = 0
        _Variation ("Variety Between Copies", Range(0, 1)) = 0.5
        _VertexVariety ("Variety Between Parts (vertex G)", Range(0, 1)) = 0
        [Header(Glow (albedo alpha))]
        [HDR] _GlowColor ("Glow (embers, hot seams)", Color) = (1, 0.35, 0.05, 1)
        _GlowStrength ("Glow Strength", Range(0, 10)) = 0
        [Header(Ice)]
        _IceGlow ("Ice: light through edges", Range(0, 2)) = 0
        _IceColor ("Ice Colour", Color) = (0.45, 0.80, 1.0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "NatureCommon.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _Tint, _CoverColor, _SnowColor, _SpringTint, _SummerTint, _AutumnTint, _WinterTint;
            half _MacroStrength, _MacroScale;
            half _BumpScale, _CoverAmount, _CoverSharpness, _Season, _FixedSeason, _SnowCover;
            half _Occlusion, _Wrap, _WindAmount, _Variation, _GlowStrength, _VertexVariety;
            half4 _GlowColor, _IceColor;
            half _IceGlow;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
            float2 uv1 : TEXCOORD1;
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

        Varyings SurfaceVert(Attributes v)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
            float3 p = TransformObjectToWorld(v.positionOS.xyz);
            if (_WindAmount > 0)
                p = SB_Wind(p, v.positionOS.xyz, n.normalWS, v.color, v.uv1, _WindAmount);
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
            #pragma vertex SurfaceVert
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
                albedo *= _SpringTint.rgb * w.x + _SummerTint.rgb * w.y + _AutumnTint.rgb * w.z + _WinterTint.rgb * w.w;
                if (_MacroStrength > 0)
                {   // large soft patches so a big field isn't one flat colour: the same texture, sampled huge, two scales
                    half m1 = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.positionWS.xz / _MacroScale).rgb, half3(0.3, 0.55, 0.15));
                    half m2 = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.positionWS.xz / (_MacroScale * 0.37) + 0.31).rgb, half3(0.3, 0.55, 0.15));
                    half m = (m1 + m2) * 0.5;
                    albedo *= lerp(1.0, saturate(0.55 + (m - 0.35) * 2.2), _MacroStrength);
                }
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                half3 bitangent = i.tangentWS.w * cross(i.normalWS, i.tangentWS.xyz);
                half3 N = normalize(mul(nTS, half3x3(i.tangentWS.xyz, bitangent, i.normalWS)));
                albedo = SB_Vary(albedo, i.positionWS, _Variation);
                albedo *= 1 + (i.color.g - 0.5) * _VertexVariety;
                half lum = dot(albedo, half3(0.3, 0.55, 0.15));
                // cover (moss) on top surfaces; its edge follows the painted texture so it doesn't look stencilled
                half up = N.y + (lum - 0.35) * 0.6;
                half cover = saturate((up - (1.0 - _CoverAmount)) * _CoverSharpness) * step(0.001, _CoverAmount) * (1.0 - w.w);
                // in patches, not a carpet: a second, larger look at the painted texture breaks the cover up
                half patch = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv * 0.37 + 0.5).rgb, half3(0.3, 0.55, 0.15));
                cover *= smoothstep(0.12, 0.24, patch + (N.y - 0.8) * 0.3);       // (colours are linear here: mid grey ~0.2)
                albedo = lerp(albedo, _CoverColor.rgb * (0.75 + lum), cover);
                half snow = SB_Snow(w) * saturate((up - (1.0 - _SnowCover)) * 6.0);
                albedo = lerp(albedo, _SnowColor.rgb * (0.9 + 0.2 * lum), snow);
                // inside a crown bark is shaded, less so once the leaves have fallen
                half occ = lerp(1.0, lerp(1.0 - _Occlusion, 1.0, i.color.a), 1.0 - w.w * 0.6);
                half wet = SB_Wet(albedo, saturate(N.y + 0.6) * (1 - snow));     // rain darkens, tops most
                half streak = SB_RainStreaks(i.positionWS, N) * (1 - snow);      // water running down the sides
                albedo *= 1 - streak * 0.45;
                wet = max(wet, streak * 0.3);
                half3 colour = SB_Shade(albedo * occ, N, i.positionWS, i.positionCS, _Wrap, 0.0, 1.0);
                colour += SB_WetGloss(wet, N, i.positionWS);
                if (_IceGlow > 0)
                {   // ice: light scattered inside shows at thin edges and grazing angles, a deep blue core
                    half3 Vv = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                    half rim = pow(1 - saturate(dot(N, Vv)), 2.5);
                    Light s0 = GetMainLight();
                    half back = pow(saturate(dot(Vv, -s0.direction) * 0.5 + 0.5), 3);
                    colour = lerp(colour, colour * _IceColor.rgb * 1.3, 0.35 * _IceGlow);
                    colour += _IceColor.rgb * (rim * 0.55 + back * 0.35) * _IceGlow * s0.color;
                }
                if (_GlowStrength > 0)                                          // embers breathe slowly
                {
                    half breathe = 0.7 + 0.3 * sin(_Time.y * 1.3 + SB_Noise(i.positionWS.xz * 0.7) * 6.28);
                    colour += _GlowColor.rgb * baseTex.a * _GlowStrength * breathe * (1 - snow);
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
                Varyings o = SurfaceVert(v);
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
            #pragma vertex SurfaceVert
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
            #pragma vertex SurfaceVert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            half4 Frag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0.0); }
            ENDHLSL
        }
    }
    FallBack Off
}
