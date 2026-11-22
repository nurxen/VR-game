// Stylized Biomes — leaves and grass: colours from the season's palette, leaves that fall in autumn and grow back in
// spring, wind, soft wrapped light and light through the leaves. Leaf atlases are data (R painted shade, G random per
// leaf, A cut-out); grass (Grass Mode) is coloured from its blade gradient alone.
Shader "Stylized Worlds Sampler/Nature/Foliage"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Leaf Atlas (R shade, G variety, A cut-out)", 2D) = "white" {}
        [Toggle(_SB_GRASS)] _GrassMode ("Grass Mode (no atlas)", Float) = 0
        _Cutoff ("Alpha Cut-off", Range(0.05, 0.95)) = 0.5

        [Header(Season)]
        [Enum(Spring,0,Summer,1,Autumn,2,Winter,3)] _Season ("Season (without a Season Controller)", Float) = 1
        [Toggle] _FixedSeason ("Fixed Season (ignore the Season Controller)", Float) = 0
        _SpringDark ("Spring Dark", Color) = (0.16, 0.38, 0.10, 1)
        _SpringLight ("Spring Light", Color) = (0.66, 0.88, 0.32, 1)
        _SpringAlt ("Spring Variety", Color) = (0.95, 0.78, 0.84, 1)
        _SummerDark ("Summer Dark", Color) = (0.13, 0.33, 0.10, 1)
        _SummerLight ("Summer Light", Color) = (0.62, 0.82, 0.30, 1)
        _SummerAlt ("Summer Variety", Color) = (0.30, 0.52, 0.12, 1)
        _AutumnDark ("Autumn Dark", Color) = (0.42, 0.10, 0.03, 1)
        _AutumnLight ("Autumn Light", Color) = (1.00, 0.70, 0.22, 1)
        _AutumnAlt ("Autumn Variety", Color) = (0.80, 0.24, 0.06, 1)
        _WinterDark ("Winter Dark", Color) = (0.30, 0.22, 0.12, 1)
        _WinterLight ("Winter Light", Color) = (0.62, 0.48, 0.30, 1)
        _WinterAlt ("Winter Variety", Color) = (0.40, 0.30, 0.18, 1)
        _VarietyShare ("Variety Share (spring, summer, autumn, winter)", Vector) = (0.14, 0.30, 0.40, 0.30)
        _Fallen ("Leaves Fallen (spring, summer, autumn, winter)", Vector) = (0, 0, 0.22, 1.01)
        _SnowColor ("Snow", Color) = (0.90, 0.93, 0.98, 1)
        _PetalColor ("Petals (atlas B = 1)", Color) = (1, 1, 1, 1)
        _CenterColor ("Flower Centre (atlas B = 0.5)", Color) = (0.95, 0.75, 0.15, 1)
        _SnowReach ("Snow Reach (lower = snow on more of the crown)", Range(-1, 1)) = 0.25

        [Header(Look)]
        _Occlusion ("Inner Shade", Range(0, 1)) = 0.75
        _EdgeFade ("Hide Edge-on Cards", Range(0, 0.6)) = 0.3
        _Wrap ("Soft Light", Range(0, 1)) = 0.4
        _Translucency ("Light Through Leaves", Range(0, 2)) = 0.6
        _WindAmount ("Wind Amount", Range(0, 2)) = 0.6
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "NatureCommon.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            half _Cutoff, _Season, _FixedSeason;
            half4 _SpringDark, _SpringLight, _SpringAlt, _SummerDark, _SummerLight, _SummerAlt;
            half4 _AutumnDark, _AutumnLight, _AutumnAlt, _WinterDark, _WinterLight, _WinterAlt;
            half4 _VarietyShare, _Fallen, _SnowColor;
            half _SnowReach, _EdgeFade;
            half4 _PetalColor, _CenterColor;
            half _Occlusion, _Wrap, _Translucency, _WindAmount, _GrassMode, _Cull;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
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
            half4 color : TEXCOORD3;
            half fog : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float4 _SB_DetailFade;

        float3 WindPosition(Attributes v, out half3 normalWS)
        {
            normalWS = TransformObjectToWorldNormal(v.normalOS);
            float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
        #if defined(_SB_GRASS)
            // under snow the blades lie down: only their tips show above it
            float flatten = SB_Snow(SB_SeasonWeights(SB_Season(_Season, _FixedSeason))) * 0.72;
            float baseY = UNITY_MATRIX_M._m13;
            flatten = max(flatten, SB_Trail(float3(UNITY_MATRIX_M._m03, baseY, UNITY_MATRIX_M._m23)) * 0.65);   // trodden paths lie flat
            positionWS.y = baseY + (positionWS.y - baseY) * (1.0 - flatten);
            // far grass shrinks into the ground before the terrain stops drawing it: a block of grass coming into
            // range grows in instead of popping (_SB_DetailFade: start, end metres, set by SeasonTerrain)
            if (_SB_DetailFade.y > 0)
            {
                float3 root = float3(UNITY_MATRIX_M._m03, baseY, UNITY_MATRIX_M._m23);
                float grow = 1.0 - smoothstep(_SB_DetailFade.x, _SB_DetailFade.y, distance(root.xz, _WorldSpaceCameraPos.xz));
                positionWS = root + (positionWS - root) * grow;
            }
        #endif
            float3 p = SB_Wind(positionWS, v.positionOS.xyz, normalWS, v.color, v.uv1, _WindAmount);
            return SB_Interact(p, v.color.r);                 // grass, flowers, ferns part around the player
        }

        // Cards seen edge-on show as streaks: fade them out (dithered) by how much the card's own plane (from screen-space
        // derivatives; the vertex normals are the crown's) faces the camera. Not for grass: blades are meant to be thin.
        void EdgeFade(float3 positionWS, float4 positionCS)
        {
        #if !defined(_SB_GRASS)
            float3 plane = normalize(cross(ddy(positionWS), ddx(positionWS)));
            half facing = abs(dot(plane, SafeNormalize(GetWorldSpaceViewDir(positionWS))));
            clip(facing - _EdgeFade);                     // a clean cut: a dithered fade left specks
        #endif
        }

        // cut-out, then the leaves the season has dropped (each card falls at its own random value)
        half4 FoliageSample(float2 uv, half4 color, out half4 weights, float4 positionCS)
        {
        #ifdef LOD_FADE_CROSSFADE
            LODFadeCrossFade(positionCS);
        #endif
            weights = SB_SeasonWeights(SB_Season(_Season, _FixedSeason));
        #if defined(_SB_GRASS)
            half4 data = half4(color.a, color.g, 0, 1);
        #else
            half4 data = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
            clip(color.g - dot(weights, _Fallen) + 0.0001);
        #endif
            clip(data.a - _Cutoff);
            return data;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _SB_GRASS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                half3 n;
                o.positionWS = WindPosition(v, n);
                o.normalWS = n;
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.color = v.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 w;
                half4 data = FoliageSample(i.uv, i.color, w, i.positionCS);
                EdgeFade(i.positionWS, i.positionCS);
                half3 dark = _SpringDark.rgb * w.x + _SummerDark.rgb * w.y + _AutumnDark.rgb * w.z + _WinterDark.rgb * w.w;
                half3 light = _SpringLight.rgb * w.x + _SummerLight.rgb * w.y + _AutumnLight.rgb * w.z + _WinterLight.rgb * w.w;
                half3 alt = _SpringAlt.rgb * w.x + _SummerAlt.rgb * w.y + _AutumnAlt.rgb * w.z + _WinterAlt.rgb * w.w;
                half share = dot(w, _VarietyShare);
                half3 albedo = lerp(dark, light, data.r);
            #if defined(_SB_GRASS)
                half pick = step(1.0 - share * 0.6, i.color.g) * 0.5;
            #else
                half pick = step(1.0 - share, i.color.g * 0.85 + data.g * 0.15);   // whole clusters turn, not single leaves
            #endif
                albedo = lerp(albedo, lerp(alt * 0.55, alt, data.r * 0.6 + 0.4), pick);
            #if !defined(_SB_GRASS)
                // flower atlases: petals where B = 1, the flower's centre where B = 0.5 (leaf atlases have B = 0)
                half petal = saturate(data.b * 2.0 - 1.0);
                half centre = saturate(1.0 - abs(data.b - 0.5) * 4.0);
                albedo = lerp(albedo, _PetalColor.rgb * (0.35 + 0.85 * data.r) * (0.88 + 0.24 * i.color.g), petal);
                albedo = lerp(albedo, _CenterColor.rgb * (0.6 + 0.5 * data.r), centre);
            #endif
                albedo = SB_Vary(albedo, i.positionWS, 1.0);              // per-object and field variety
            #if defined(_SB_GRASS)
                {   // a gust rolling over the meadow: bent blades show their lighter sides
                    float3 wd = _SB_UseGlobal > 0.5 ? _SB_WindDir.xyz : normalize(float3(1, 0, 0.35));
                    float g = SB_Gust(i.positionWS, wd, _Time.y * (_SB_UseGlobal > 0.5 ? _SB_WindSpeed : 1.0));
                    albedo *= 1.0 + saturate(g - 0.6) * 0.22 * i.color.a * (_SB_UseGlobal > 0.5 ? saturate(_SB_WindStrength) : 0.5);
                }
            #endif
                half3 N = normalize(i.normalWS);
                // snow settles on the lit, upper parts of each spray, in patches that follow the painted shade
                half snow = SB_Snow(w) * saturate((N.y - _SnowReach) * 2.0) * smoothstep(0.35, 0.7, data.r + 0.25 * N.y);
            #if defined(_SB_GRASS)
                snow *= i.color.a;
                half occ = lerp(1.0 - _Occlusion * 0.5, 1.0, i.color.a);
            #else
                half occ = lerp(1.0 - _Occlusion, 1.0, i.color.a);
            #endif
                albedo = lerp(albedo, _SnowColor.rgb, snow * 0.85);
                SB_Wet(albedo, 0.5 * (1 - snow));                               // wet leaves: a little darker
                half3 colour = SB_Shade(albedo * occ, N, i.positionWS, i.positionCS, _Wrap, _Translucency, 1.0);
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
            #pragma shader_feature_local _SB_GRASS
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                half3 n;
                float3 p = WindPosition(v, n);
                o.positionCS = SB_ShadowPositionCS(p, n);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 w;
                FoliageSample(i.uv, i.color, w, i.positionCS);
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
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _SB_GRASS
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                half3 n;
                o.positionWS = WindPosition(v, n);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 w;
                FoliageSample(i.uv, i.color, w, i.positionCS);
                EdgeFade(i.positionWS, i.positionCS);
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
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _SB_GRASS
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                half3 n;
                o.positionWS = WindPosition(v, n);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = n;
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 w;
                FoliageSample(i.uv, i.color, w, i.positionCS);
                EdgeFade(i.positionWS, i.positionCS);
                return half4(NormalizeNormalPerPixel(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
