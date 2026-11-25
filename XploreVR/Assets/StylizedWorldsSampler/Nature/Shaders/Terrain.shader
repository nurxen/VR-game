// Stylized Biomes — Unity Terrain with seasons: up to four painted terrain layers, season tints on the layers that
// grow (grass), snow on flat ground in winter, large soft patches so wide fields aren't one flat colour, the soft
// stylised light of the other nature shaders. Works with Draw Instanced on or off.
// Use: Terrain > Settings > Material = a material with this shader. Keep "Base Map Dist." high (the SeasonTerrain
// component sets it): beyond it Unity draws URP's own distance base map, which has no seasons.
Shader "Stylized Worlds Sampler/Nature/Terrain"
{
    Properties
    {
        [Header(Seasons)]
        [Enum(Spring,0,Summer,1,Autumn,2,Winter,3)] _Season ("Season (without a Season Controller)", Float) = 1
        [Toggle] _FixedSeason ("Fixed Season (ignore the Season Controller)", Float) = 0
        _SeasonLayers ("Layers That Change With Seasons (layer 1-4, 0..1)", Vector) = (1, 0, 0.4, 0)
        _SpringTint ("Spring Tint", Color) = (1.06, 1.10, 0.90, 1)
        _SummerTint ("Summer Tint", Color) = (1, 1, 1, 1)
        _AutumnTint ("Autumn Tint", Color) = (1.14, 0.96, 0.58, 1)
        _WinterTint ("Winter Tint (under the snow)", Color) = (0.95, 0.88, 0.70, 1)
        _SnowColor ("Snow", Color) = (0.90, 0.93, 0.98, 1)
        _SnowCover ("Snow Cover in Winter", Range(0, 1)) = 0.92
        _SnowLine ("Snow Line (world height; snow all year above)", Float) = 100000
        _SnowLineBlend ("Snow Line Blend (m)", Float) = 12

        [Header(Look)]
        _MacroStrength ("Large Patches", Range(0, 1)) = 0.6
        _MacroScale ("Large Patch Size (m)", Float) = 45
        _Wrap ("Soft Light", Range(0, 1)) = 0.2

        [Header(Cliffs (rock on steep slopes))]
        [NoScaleOffset] _RockTex ("Cliff Rock", 2D) = "grey" {}
        [NoScaleOffset] _RockNormal ("Cliff Rock Normal", 2D) = "bump" {}
        _RockTint ("Cliff Tint", Color) = (1, 1, 1, 1)
        _RockSlope ("Rock From Slope (normal Y)", Range(0.3, 0.95)) = 0.74
        _RockScale ("Rock Tile (m)", Float) = 8
        _Sastrugi ("Wind Ripples In Snow", Range(0, 1)) = 0.6
        _SnowGlints ("Snow Sparkle", Range(0, 2)) = 1
        _SnowLayers ("Snow Layers (layer 1-4, 0..1)", Vector) = (0, 0, 0, 0)

        [Header(Sand)]
        _SandLayers ("Sand Layers (layer 1-4, 0..1)", Vector) = (0, 0, 0, 0)
        _SandRipples ("Wind Ripples", Range(0, 1)) = 0.7
        _SandRippleSize ("Ripple Spacing (m)", Float) = 0.32
        _SandSheen ("Sheen (broad soft shine)", Range(0, 1)) = 0.35
        _SandGlitter ("Glitter", Range(0, 2)) = 1
        _SandRim ("Rim Light on Crests", Range(0, 1)) = 0.35

        [Header(Shore)]
        _WetLevel ("Water Level (world Y; wet ground below + band)", Float) = -1000
        _WetBand ("Wet Band Above Water (m)", Float) = 0.6
        _WetDarken ("Wet Darkening", Range(0, 1)) = 0.42

        [Header(Glow (layer alpha))]
        _GlowLayers ("Layers That Glow (alpha = hot seams), 0..1", Vector) = (0, 0, 0, 0)
        [HDR] _GlowColor ("Glow", Color) = (1, 0.32, 0.04, 1)
        _GlowStrength ("Glow Strength", Range(0, 10)) = 3
        _AntiTile ("Hide Tiling", Range(0, 1)) = 0.65
        _HeightBlend ("Height Blend (crisp natural edges between layers; 0 = plain crossfade)", Range(0, 0.5)) = 0.12

        // set by the Terrain
        [HideInInspector] _Control ("Control (RGBA)", 2D) = "red" {}
        [HideInInspector] _Splat0 ("Layer 0", 2D) = "grey" {}
        [HideInInspector] _Splat1 ("Layer 1", 2D) = "grey" {}
        [HideInInspector] _Splat2 ("Layer 2", 2D) = "grey" {}
        [HideInInspector] _Splat3 ("Layer 3", 2D) = "grey" {}
        [HideInInspector] _Normal0 ("Normal 0", 2D) = "bump" {}
        [HideInInspector] _Normal1 ("Normal 1", 2D) = "bump" {}
        [HideInInspector] _Normal2 ("Normal 2", 2D) = "bump" {}
        [HideInInspector] _Normal3 ("Normal 3", 2D) = "bump" {}
        [HideInInspector] _TerrainHolesTexture ("Holes Map (RGB)", 2D) = "white" {}
        [HideInInspector] _MainTex ("BaseMap (RGB)", 2D) = "grey" {}
        [HideInInspector] _BaseColor ("Main Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Geometry-100" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "TerrainCompatible" = "True" }

        HLSLINCLUDE
        // Unity's own terrain inputs (URP): the layer textures and cbuffers the Terrain fills, and TerrainInstancing
        #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitInput.hlsl"
        #include "NatureCommon.hlsl"

        // our material properties (the Terrain owns UnityPerMaterial)
        float _SnowLine, _SnowLineBlend;
        float _Season, _FixedSeason, _SnowCover, _MacroStrength, _MacroScale, _Wrap, _AntiTile, _HeightBlend, _WetLevel, _WetBand, _WetDarken, _GlowStrength;
        float4 _GlowLayers, _GlowColor, _RockTint;
        float _RockSlope, _RockScale, _Sastrugi, _SnowGlints, _SandRipples, _SandRippleSize, _SandSheen, _SandGlitter, _SandRim;
        float4 _SandLayers, _SnowLayers;
        TEXTURE2D(_RockTex); SAMPLER(sampler_RockTex); TEXTURE2D(_RockNormal);
        float4 _SeasonLayers, _SpringTint, _SummerTint, _AutumnTint, _WinterTint, _SnowColor;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 texcoord : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
            half fog : TEXCOORD3;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings TerrainVert(Attributes v)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            TerrainInstancing(v.positionOS, v.normalOS, v.texcoord);
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.uv = v.texcoord;
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
            #pragma vertex TerrainVert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
            #pragma shader_feature_local _TERRAIN_INSTANCED_PERPIXEL_NORMAL
            #pragma multi_compile_local_fragment __ _ALPHATEST_ON

            half4 Frag(Varyings i) : SV_Target
            {
            #ifdef _ALPHATEST_ON
                ClipHoles(i.uv);
            #endif
                half4 control = SAMPLE_TEXTURE2D(_Control, sampler_Control, i.uv);
                control /= max(dot(control, 1.0h), 1e-3h);
                float2 uv0 = TRANSFORM_TEX(i.uv, _Splat0), uv1 = TRANSFORM_TEX(i.uv, _Splat1);
                float2 uv2 = TRANSFORM_TEX(i.uv, _Splat2), uv3 = TRANSFORM_TEX(i.uv, _Splat3);
                // anti-tiling: each layer is also sampled 2.7x larger and turned; the two are mixed by a slow noise
                // (the second, larger texture) and more of the large one in the distance, so no grid of tiles shows
                float2x2 turn = float2x2(0.8, -0.6, 0.6, 0.8);
                half far = saturate(distance(i.positionWS, _WorldSpaceCameraPos) / 60.0);
                half mixer = saturate(0.3 + 0.5 * far + (SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, i.positionWS.xz / 97.0).g - 0.2) * 1.5);
                #define SB_LAYER(n, uvn) lerp(SAMPLE_TEXTURE2D(_Splat##n, sampler_Splat0, uvn).rgb, SAMPLE_TEXTURE2D(_Splat##n, sampler_Splat0, mul(turn, uvn) * 0.37 + 0.13).rgb, mixer * _AntiTile) * _DiffuseRemapScale##n.rgb
                half3 c0 = SB_LAYER(0, uv0);
                half3 c1 = SB_LAYER(1, uv1);
                half3 c2 = SB_LAYER(2, uv2);
                half3 c3 = SB_LAYER(3, uv3);
                if (_HeightBlend > 0)
                {   // height blend: where layers meet, the "taller" one (its bright bits: stones, tufts) wins with a
                    // crisp natural edge instead of a muddy crossfade
                    half4 hgt = half4(Luminance(c0), Luminance(c1), Luminance(c2), Luminance(c3));
                    half4 hw = control * (hgt + 0.3h);
                    half mx = max(max(hw.r, hw.g), max(hw.b, hw.a));
                    half4 hb = max(hw - mx + _HeightBlend, 0.0h);
                    control = hb / max(dot(hb, 1.0h), 1e-4h);
                }
                half4 w = SB_SeasonWeights(SB_Season(_Season, _FixedSeason));
                half3 tint = _SpringTint.rgb * w.x + _SummerTint.rgb * w.y + _AutumnTint.rgb * w.z + _WinterTint.rgb * w.w;
                half4 seasonal = control * _SeasonLayers;
                half3 albedo = c0 * control.r + c1 * control.g + c2 * control.b + c3 * control.a;
                albedo *= lerp(1.0h, tint, dot(seasonal, 1.0h));
                if (_MacroStrength > 0)
                {   // large soft patches: the first layer's texture sampled huge, at two scales
                    half m1 = dot(SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, i.positionWS.xz / _MacroScale).rgb, half3(0.3, 0.55, 0.15));
                    half m2 = dot(SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, i.positionWS.xz / (_MacroScale * 0.37) + 0.31).rgb, half3(0.3, 0.55, 0.15));
                    albedo *= lerp(1.0h, saturate(0.55h + ((m1 + m2) * 0.5h - 0.18h) * 2.2h), _MacroStrength);
                }
                // normal: the layers' normal maps on the terrain's surface
                half3 N = normalize(i.normalWS);
            #ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
                N = normalize(TransformObjectToWorldNormal(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, i.uv).rgb * 2 - 1));
            #endif
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal0, sampler_Normal0, uv0), _NormalScale0) * control.r
                          + UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal1, sampler_Normal0, uv1), _NormalScale1) * control.g
                          + UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal2, sampler_Normal0, uv2), _NormalScale2) * control.b
                          + UnpackNormalScale(SAMPLE_TEXTURE2D(_Normal3, sampler_Normal0, uv3), _NormalScale3) * control.a;
                half3 T = normalize(cross(UNITY_MATRIX_M._13_23_33, N));
                half3 B = cross(N, T);
                half3 Nd = normalize(T * nTS.x + B * nTS.y + N * max(nTS.z, 1e-3h));
                // cliffs: bare rock wherever the ground is steep, mapped from three sides (no stretching), strata level
                half rockW = saturate((_RockSlope - N.y) / 0.10 + (SB_Noise(i.positionWS.xz / 6.0) - 0.5) * 0.9);
                if (rockW > 0.001)
                {
                    float3 bw = pow(abs(N), 4); bw /= dot(bw, 1);
                    float3 wp = i.positionWS;
                    wp.y += (SB_Noise(wp.xz / 9.0) - 0.5) * 2.4;                 // strata wander instead of running level
                    float3 p = wp / _RockScale;
                    float3 q = float3(wp.x * 0.8 + wp.z * 0.6, wp.y, wp.z * 0.8 - wp.x * 0.6) / (_RockScale * 2.7) + 0.37;
                    half mixS = smoothstep(0.3, 0.7, SB_Noise(float2(wp.x + wp.z * 0.7, wp.y * 1.4) / 37.0));   // (along the wall and up it: blotches, not stripes)   // which scale shows where
                    half3 rx = lerp(SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, p.zy).rgb, SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, q.zy).rgb, mixS);
                    half3 rz = lerp(SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, p.xy).rgb, SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, q.xy).rgb, mixS);
                    half3 ry = lerp(SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, p.xz).rgb, SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, q.xz).rgb, mixS);
                    half3 rock = (rx * bw.x + rz * bw.z + ry * bw.y) * _RockTint.rgb;
                    half big = SB_Noise(float2(wp.x * 0.8 + wp.z * 0.6, wp.y * 1.3) / 23.0), warmth = SB_Noise(float2(wp.z - wp.x * 0.5, wp.y) / 61.0);
                    rock *= 0.78 + 0.42 * big;                                   // broad light and dark patches across a face
                    rock *= lerp(half3(0.94, 0.97, 1.04), half3(1.06, 1.0, 0.92), warmth);   // cool and warm stone
                    half3 nx = UnpackNormal(lerp(SAMPLE_TEXTURE2D(_RockNormal, sampler_RockTex, p.zy), SAMPLE_TEXTURE2D(_RockNormal, sampler_RockTex, q.zy), mixS));
                    half3 nz = UnpackNormal(lerp(SAMPLE_TEXTURE2D(_RockNormal, sampler_RockTex, p.xy), SAMPLE_TEXTURE2D(_RockNormal, sampler_RockTex, q.xy), mixS));
                    half3 rn = normalize(N + (float3(0, nx.y, nx.x) * bw.x + float3(nz.x, nz.y, 0) * bw.z) * 0.8);
                    albedo = lerp(albedo, rock, rockW);
                    Nd = normalize(lerp(Nd, rn, rockW));
                }
                // snow lies on flat ground; its edge follows the painted textures so it isn't a clean slope line
                half lum = dot(albedo, half3(0.3, 0.55, 0.15));
                half snowAmt = max(SB_Snow(w), saturate((i.positionWS.y - _SnowLine) / max(_SnowLineBlend, 0.1) + (SB_Noise(i.positionWS.xz / 9.0) - 0.5) * 1.2));
                half snow = snowAmt * saturate((N.y + (lum - 0.15h) * 0.8h - (1.0h - _SnowCover)) * 6.0h);   // flat parts first, rock shows on steep
                albedo = lerp(albedo, _SnowColor.rgb * (0.92h + 0.3h * lum), snow);
                // snow surfaces: fallen snow and painted snow layers both get the snow treatment
                half snowSurf = max(snow, saturate(dot(control, _SnowLayers)));
                albedo *= lerp(1.0h, 0.88h, snowSurf);                          // headroom: shading and sparkle can read
                if (snowSurf > 0.01)
                {   // soft wind-packed lumps (two noise slopes), fading with distance
                    float2 pp = i.positionWS.xz;
                    float n0 = SB_Noise(pp / 2.2), nx = SB_Noise((pp + float2(0.15, 0)) / 2.2), nz = SB_Noise((pp + float2(0, 0.15)) / 2.2);
                    float m0 = SB_Noise(pp / 0.7 + 3.1), mx = SB_Noise((pp + float2(0.05, 0)) / 0.7 + 3.1), mz = SB_Noise((pp + float2(0, 0.05)) / 0.7 + 3.1);
                    float fadeL = saturate(1.3 - distance(i.positionWS, _WorldSpaceCameraPos) / 60.0);
                    float3 lump = float3(n0 - nx, 0, n0 - nz) * 9.0 + float3(m0 - mx, 0, m0 - mz) * 4.0;
                    Nd = normalize(Nd + lump * snowSurf * fadeL);
                }
                if (snowSurf > 0.01 && _Sastrugi > 0)
                {   // wind-carved ripples across the snow (sastrugi): long ridges across the wind
                    float3 wd = _SB_UseGlobal > 0.5 ? _SB_WindDir.xyz : normalize(float3(1, 0, 0.35));
                    float2 q = float2(dot(i.positionWS.xz, wd.xz), dot(i.positionWS.xz, float2(-wd.z, wd.x)));
                    float r0 = SB_Noise(float2(q.x * 0.9, q.y * 0.12)), r1 = SB_Noise(float2(q.x * 0.9 + 0.05, q.y * 0.12));
                    float3 g = float3(wd.x, 0, wd.z) * (r1 - r0) * 18.0 * _Sastrugi;
                    Nd = normalize(lerp(Nd, normalize(Nd + g), snowSurf));
                }
                // wet ground at the shore: under the water and in a band above it that the swash keeps wetting
                // (same timing as the Water shader's swash), darker and a little cooler; a glint of sky on it
                float swashT = frac(_Time.y * 0.12);
                float reach = _WetLevel + _WetBand * (0.55 + 0.45 * sin(swashT * PI));
                half wet = saturate((reach - i.positionWS.y) / max(_WetBand * 0.35, 0.01));
                albedo = lerp(albedo, albedo * (1 - _WetDarken) * half3(0.92, 0.97, 1.05), wet * (1 - snow));
                // rain: wet ground, and puddles in flat hollows (low, flat, where the painted texture is darkest)
                half rainWet = SB_Wet(albedo, 1 - snow);
                half puddle = 0;
                if (rainWet > 0.01)
                {
                    half low = SB_Noise(i.positionWS.xz / 6.0) * 0.6 + SB_Noise(i.positionWS.xz / 2.1) * 0.4;
                    float grow = saturate(_SB_Wetness) * 0.08;                  // the longer it rains, the bigger the puddles
                    puddle = smoothstep(0.64 - grow, 0.68 - grow, low + (1 - lum * 2.5) * 0.08) * smoothstep(0.96, 0.995, N.y) * rainWet
                           * saturate(1 - dot(seasonal, 1.0h) * 1.5);              // on bare ground, not in the grass
                    albedo = lerp(albedo, albedo * 0.35 + half3(0.05, 0.07, 0.09), puddle);
                    Nd = normalize(lerp(Nd, N, puddle));
                    float2 rip = SB_RainRipples(i.positionWS.xz, puddle + rainWet * 0.25 * smoothstep(0.9, 0.99, N.y));
                    Nd = normalize(Nd + float3(rip.x, 0, rip.y));
                    half streak = SB_RainStreaks(i.positionWS, N) * rockW;     // water running down the cliffs
                    albedo *= 1 - streak * 0.5;                                 // wet stone: darker runnels, only a thin shine
                    rainWet = max(rainWet, streak * 0.3);
                }
                // sand: wind ripples - gentle windward side, sharp lee edge - running across the wind on flat sand and
                // turning to follow the slope on dune faces; they fade with distance (no shimmer)
                half sandW = saturate(dot(control, _SandLayers)) * (1 - snow) * (1 - puddle);
                half3 Vv = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                if (sandW > 0.01 && _SandRipples > 0)
                {
                    float3 wd = _SB_UseGlobal > 0.5 ? _SB_WindDir.xyz : normalize(float3(1, 0, 0.35));
                    float steep = saturate((1 - N.y) * 3.5);
                    float2 rd = normalize(lerp(wd.xz, normalize(N.xz + 1e-4), steep * 0.35));   // turn only a little on slopes (no contour lines)
                    float q = dot(i.positionWS.xz, rd) / _SandRippleSize + SB_Noise(i.positionWS.xz * 0.35) * 3.0
                            + SB_Noise(i.positionWS.xz * 1.7) * 0.6;
                    float f = frac(q);
                    // a soft asymmetric profile: long gentle rise, shorter fall, rounded at both turns
                    float slopeR = smoothstep(0.0, 0.12, f) * smoothstep(0.75, 0.62, f) * 1.2 - smoothstep(0.66, 0.8, f) * smoothstep(1.0, 0.9, f) * 2.2;
                    float fadeR = saturate(1.3 - distance(i.positionWS, _WorldSpaceCameraPos) / 45.0);
                    Nd = normalize(Nd - float3(rd.x, 0, rd.y) * slopeR * 0.05 * _SandRipples * sandW * fadeR);
                }
                // footprints in snow and sand: a dent (its slope from the map's gradient), darker inside, a lit rim
                half soft = saturate(snowSurf + sandW);
                if (soft > 0.01 && _SB_TrailRect.w > 0)
                {
                    float e = 0.025;
                    half t0 = SB_Trail(i.positionWS), tx = SB_Trail(i.positionWS + float3(e, 0, 0)), tz = SB_Trail(i.positionWS + float3(0, 0, e));
                    float3 g = float3(tx - t0, 0, tz - t0) / e;
                    Nd = normalize(Nd + g * lerp(0.12, 0.22, snowSurf) * soft);   // the walls of the print (deeper in snow)
                    half floor_ = smoothstep(0.35, 0.9, t0);
                    half rim = saturate(length(g) * 0.12) * (1 - floor_);       // the pushed-up rim round it
                    albedo *= 1 - floor_ * soft * lerp(0.30h, 0.32h, snowSurf); // packed, shaded floor
                    albedo = lerp(albedo, albedo * half3(0.62, 0.80, 1.15), floor_ * snowSurf);   // blue in snow prints: light scattered deep in them
                    albedo *= 1 + rim * soft * 0.07;
                }
                half3 colour = SB_Shade(albedo, normalize(lerp(Nd, N, snow * 0.7h)), i.positionWS, i.positionCS, _Wrap, 0.0, 1.0);
                Light sunL = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half sunLit = sunL.shadowAttenuation * SB_CloudShadow(i.positionWS);
                if (sandW > 0.01)
                {   // a broad soft shine like water ("ocean" sheen), grain glitter, and a bright rim along the crests
                    half3 H = normalize(sunL.direction + Vv);
                    colour += sunL.color * sunLit * sandW * _SandSheen * 0.35 * pow(saturate(dot(Nd, H)), 14);
                    colour += sunL.color * sandW * _SandGlitter * 1.4 * SB_Glints(i.positionWS, Nd, Vv, sunL.direction, 70.0, 0.3) * (0.25 + 0.75 * sunLit);
                    colour += sunL.color * sunLit * sandW * _SandRim * pow(1 - saturate(dot(N, Vv)), 4) * 0.5;
                }
                if (snowSurf > 0.01)
                {   // snow: cool blue in the shade (light scattered inside it), and ice crystals sparkling in the sun
                    colour = lerp(colour, colour * half3(0.78, 0.89, 1.16), snowSurf * (1 - sunLit) * 0.75);
                    colour += sunL.color * snowSurf * _SnowGlints * 5.0 * SB_Glints(i.positionWS, Nd, Vv, sunL.direction, 30.0, 0.5) * (0.15 + 0.85 * sunLit);
                }
                colour += SB_WetGloss(rainWet * 0.6 + puddle, Nd, i.positionWS);
                colour = lerp(colour, SampleSH(reflect(-SafeNormalize(GetWorldSpaceViewDir(i.positionWS)), N)) * 0.9, puddle * 0.55);   // puddles mirror the sky
                colour += wet * (1 - snow) * 0.12 * pow(saturate(dot(reflect(-SafeNormalize(GetWorldSpaceViewDir(i.positionWS)), N), _MainLightPosition.xyz)), 24) * _MainLightColor.rgb;
                if (dot(_GlowLayers, 1) > 0)                                    // hot seams in the ground (lava fields)
                {
                    half4 ga = half4(SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, uv0).a, SAMPLE_TEXTURE2D(_Splat1, sampler_Splat0, uv1).a,
                                     SAMPLE_TEXTURE2D(_Splat2, sampler_Splat0, uv2).a, SAMPLE_TEXTURE2D(_Splat3, sampler_Splat0, uv3).a);
                    half g = dot(ga * control, _GlowLayers);
                    half breathe = 0.7 + 0.3 * sin(_Time.y * 0.9 + SB_Noise(i.positionWS.xz * 0.2) * 6.28);
                    colour += _GlowColor.rgb * g * _GlowStrength * breathe * (1 - snow);
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
            ZWrite On ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_local _ _ALPHATEST_ON

            Varyings Vert(Attributes v)
            {
                Varyings o = TerrainVert(v);
                o.positionCS = SB_ShadowPositionCS(o.positionWS, o.normalWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
            #ifdef _ALPHATEST_ON
                ClipHoles(i.uv);
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
            #pragma vertex TerrainVert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
            #pragma multi_compile_local _ _ALPHATEST_ON

            half4 Frag(Varyings i) : SV_Target
            {
            #ifdef _ALPHATEST_ON
                ClipHoles(i.uv);
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
            #pragma vertex TerrainVert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
            #pragma multi_compile_local _ _ALPHATEST_ON

            half4 Frag(Varyings i) : SV_Target
            {
            #ifdef _ALPHATEST_ON
                ClipHoles(i.uv);
            #endif
                return half4(NormalizeNormalPerPixel(i.normalWS), 0.0);
            }
            ENDHLSL
        }

        UsePass "Hidden/Nature/Terrain/Utilities/PICKING"
    }
    Dependency "AddPassShader" = "Hidden/Universal Render Pipeline/Terrain/Lit (Add Pass)"
    Dependency "BaseMapShader" = "Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)"
    Dependency "BaseMapGenShader" = "Hidden/Universal Render Pipeline/Terrain/Lit (Basemap Gen)"
    FallBack Off
}
