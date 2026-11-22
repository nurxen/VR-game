// Stylized Biomes — shared code of the nature shaders: wind, seasons, snow and the soft stylised lighting.
//
// Mesh data (written by our generators):
//   color.r  sway weight (0 at the ground, 1 at the far tips)      color.g  random value per leaf card / blade
//   color.b  1 on leaves and grass, 0 on bark and stone            color.a  occlusion (inside of a crown darker)
//   uv1.x    phase of the main branch a part belongs to
// Globals (set by SeasonController; without one the materials use their own Season and a light default breeze):
//   _SB_UseGlobal (1 when a controller is active), _SB_Season (0 spring, 1 summer, 2 autumn, 3 winter, 4 = spring
//   again; fractions blend), _SB_WindDir (xyz, normalised), _SB_WindStrength, _SB_WindSpeed, _SB_SnowBoost.
#ifndef STYLIZED_BIOMES_NATURE_COMMON
#define STYLIZED_BIOMES_NATURE_COMMON

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

// A value 0..1 per placed object (from its position): copies of one prefab get slightly different colours.
float SB_InstanceRandom()
{
    float3 o = UNITY_MATRIX_M._m03_m13_m23;
    return frac(sin(dot(floor(o * 3.1), float3(12.9898, 78.233, 37.719))) * 43758.5453);
}

// Smooth value noise in 2D (world-space patches of colour across a field).
float SB_Noise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = frac(sin(dot(i, float2(127.1, 311.7))) * 43758.5453);
    float b = frac(sin(dot(i + float2(1, 0), float2(127.1, 311.7))) * 43758.5453);
    float c = frac(sin(dot(i + float2(0, 1), float2(127.1, 311.7))) * 43758.5453);
    float d = frac(sin(dot(i + float2(1, 1), float2(127.1, 311.7))) * 43758.5453);
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// Colour variety: per object (v) and across the world in large patches (field), both subtle.
half3 SB_Vary(half3 albedo, float3 positionWS, half amount)
{
    float r = SB_InstanceRandom();
    float field = SB_Noise(positionWS.xz / 23.0) * 0.6 + SB_Noise(positionWS.xz / 7.0) * 0.4;
    half bright = lerp(0.90, 1.10, r) * lerp(0.92, 1.08, field);
    half3 warm = albedo * half3(1.10, 1.04, 0.78);                 // a touch yellower / drier
    albedo = lerp(albedo, warm, saturate((r * 0.5 + field * 0.8 - 0.45)) * 0.7);
    return lerp(albedo, albedo * bright, amount);
}

float _SB_UseGlobal;
float _SB_Season;
float4 _SB_WindDir;
float _SB_WindStrength;
float _SB_WindSpeed;
float _SB_SnowBoost;
float _SB_Wetness;          // 0 dry .. 1 soaked (WeatherController)

// Wet surfaces: darker and a little cooler; returns how wet (for a glossy highlight).
half SB_Wet(inout half3 albedo, half exposure)
{
    half w = saturate(_SB_Wetness) * exposure;
    albedo *= lerp(1.0, 0.62, w);
    return w;
}

// weather and time of day (set by WeatherController and DayNightController)
float _SB_UseWeather, _SB_CloudCover, _SB_Storm, _SB_Night;

// Tracks (TrailMap): 0 untouched .. 1 trodden. Rect: world x, z of the map's corner, its size, on (w > 0).
TEXTURE2D(_SB_TrailMap); SAMPLER(sampler_SB_TrailMap);
float4 _SB_TrailRect;
half SB_Trail(float3 p)
{
    if (_SB_TrailRect.w <= 0) return 0;
    float2 uv = (p.xz - _SB_TrailRect.xy) / _SB_TrailRect.z;
    if (any(uv < 0) || any(uv > 1)) return 0;
    return SAMPLE_TEXTURE2D_LOD(_SB_TrailMap, sampler_SB_TrailMap, uv, 0).r;
}

// Both channels: x trodden, y the ground the snow was pushed onto (berms).
half2 SB_Trail2(float3 p)
{
    if (_SB_TrailRect.w <= 0) return 0;
    float2 uv = (p.xz - _SB_TrailRect.xy) / _SB_TrailRect.z;
    if (any(uv < 0) || any(uv > 1)) return 0;
    return SAMPLE_TEXTURE2D_LOD(_SB_TrailMap, sampler_SB_TrailMap, uv, 0).rg;
}

// Glints: a jittered grid of tiny mirror facets (sand grains, ice crystals in snow), each with its own random tilt;
// one flashes when it reflects the light into the eye. Cells are a few cm, faded out with distance so they never shimmer.
half SB_Glints(float3 p, half3 N, half3 V, half3 L, float cellsPerMetre, half density)
{
    float3 cell = floor(p * cellsPerMetre);
    float h = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
    float3 rn = frac(float3(h * 13.17, h * 71.73, h * 29.31)) - 0.5;
    half3 H = normalize(L + V);
    // crystal facets face every way (not just the ground's way), so they sparkle under any light; only facets on the
    // side the ground faces count
    float3 facet = normalize(rn + N * 0.25);
    float3 local = frac(p * cellsPerMetre) - 0.5 - rn * 0.5;                // a tiny round grain somewhere in its cell
    half grain = smoothstep(0.22, 0.05, length(local));
    half g = pow(saturate(dot(facet, H)), 160) * step(1 - density, frac(h * 7.31)) * saturate(dot(N, V) * 4) * grain;
    float dist = distance(p, _WorldSpaceCameraPos);
    return g * saturate(1.3 - dist / (cellsPerMetre > 50 ? 14.0 : 35.0));   // fine grains only close up
}

// Shadows of the clouds drifting over the land with the wind: as many and as big as the sky's cover.
half SB_CloudShadow(float3 positionWS)
{
    float cover = _SB_UseWeather > 0.5 ? _SB_CloudCover : 0.3;
    float2 wind = (_SB_UseGlobal > 0.5 ? _SB_WindDir.xz : float2(1, 0.35)) * _Time.y * 2.4;
    float3 L = _MainLightPosition.xyz;                       // follow the shadow along the sun's rays to the cloud layer
    float2 along = positionWS.xz + L.xz / max(L.y, 0.15) * (300.0 - positionWS.y);       // (so cliffs get patches, not bands)
    float2 p = (along + wind) / 150.0;
    float n = SB_Noise(p) * 0.65 + SB_Noise(p * 2.3 + 7.1) * 0.35;
    float th = lerp(0.74, 0.34, cover);
    return 1 - smoothstep(th, th + 0.14, n) * 0.6 * (1 - _SB_Storm * 0.7);   // (under a storm it is all shade anyway)
}

float _SB_Rain;             // how hard it is raining now, 0..1 (WeatherController)

// Raindrop rings on standing water and wet ground: each cell of two offset grids gets a drop at its own random spot
// and time; its ring spreads and fades. Returns the slope to add to the surface normal (xz) - 0 when it isn't raining.
float2 SB_RainRipples(float2 xz, float strength)
{
    if (_SB_Rain <= 0.01 || strength <= 0) return 0;
    float2 slope = 0;
    [unroll] for (int layer = 0; layer < 2; layer++)
    {
        float2 p = xz / 0.42 + layer * float2(0.37, 0.71);
        float2 cell = floor(p), f = frac(p);
        [unroll] for (int oy = -1; oy <= 1; oy++)
        [unroll] for (int ox = -1; ox <= 1; ox++)
        {
            float2 c = cell + float2(ox, oy);
            float h = frac(sin(dot(c, float2(127.1, 311.7)) + layer * 17.0) * 43758.5453);
            float2 drop = float2(ox, oy) + float2(frac(h * 13.1), frac(h * 71.3)) * 0.8 + 0.1;   // where in its cell
            float t = frac(_Time.y * (0.9 + h * 0.4) + h * 7.0);                          // its age, 0..1
            float2 d = f - drop;
            float r = length(d);
            float ring = sin(clamp((r - t * 0.9) * 40.0, -3.14159, 3.14159));             // one wave travelling out
            float fade = (1 - t) * (1 - t) * step(h, _SB_Rain * 0.9 + 0.1);               // more drops in harder rain
            slope += d / max(r, 1e-3) * ring * fade * smoothstep(0.0, 0.05, r);
        }
    }
    return slope * 0.35 * strength;
}

// Water running down wet steep faces (rock, bark, cliffs): thin streaks sliding down, a little darker and shinier.
half SB_RainStreaks(float3 positionWS, half3 N)
{
    if (_SB_Wetness <= 0.01) return 0;
    half steep = saturate((0.8 - abs(N.y)) * 2.0);
    float2 q = float2((positionWS.x + positionWS.z) * 11.0, positionWS.y * 1.2 + _Time.y * 1.6);   // thin runnels
    half s = smoothstep(0.62, 0.8, SB_Noise(q)) * smoothstep(0.3, 0.6, SB_Noise(q * float2(0.25, 0.4) + 9.1));
    return s * steep * saturate(_SB_Wetness * 1.5);
}

half3 SB_WetGloss(half wet, half3 N, float3 positionWS)
{
    if (wet <= 0.001) return 0;
    half3 V = SafeNormalize(GetWorldSpaceViewDir(positionWS));
    half3 H = SafeNormalize(_MainLightPosition.xyz + V);
    half overcast = _SB_UseWeather > 0.5 ? saturate(1.0 - _SB_CloudCover * 0.9) : 1.0;   // no sharp sun glints under rain cloud
    return pow(saturate(dot(N, H)), 48) * wet * 0.6 * _MainLightColor.rgb * overcast * SB_CloudShadow(positionWS);
}

// The season this material shows: the controller's, unless the material has its own fixed season.
float SB_Season(float materialSeason, float fixedSeason)
{
    return (_SB_UseGlobal > 0.5 && fixedSeason < 0.5) ? _SB_Season : materialSeason;
}

// Weights of spring, summer, autumn, winter. A season holds for the first half of its slot and blends into the next
// over the second half, so a slider or a clock moves smoothly round the year.
float4 SB_SeasonWeights(float s)
{
    s = fmod(fmod(s, 4.0) + 4.0, 4.0);
    float i = floor(s);
    float t = smoothstep(0.5, 1.0, s - i);
    float4 slot = float4(0, 1, 2, 3);                       // (no dynamic vector indexing: Metal can't)
    float4 now = 1.0 - saturate(abs(slot - i));
    float4 next = 1.0 - saturate(abs(slot - fmod(i + 1.0, 4.0)));
    return now * (1.0 - t) + next * t;
}

float SB_Snow(float4 w)
{
    return saturate(w.w + _SB_SnowBoost);
}

// Gusts: a slow wind field that travels across the world in the wind's direction, so waves of wind roll over a
// meadow and through a forest (0 calm .. ~1.6 in a gust). Shared by every plant and the grass.
float SB_Gust(float3 positionWS, float3 dir, float t)
{
    float2 p = positionWS.xz;
    float2 along = dir.xz * t * 3.2;                         // the gust front moves with the wind (~3 m/s per unit speed)
    float g = SB_Noise((p - along) / 22.0) * 0.65 + SB_Noise((p - along * 1.4) / 7.5) * 0.35;
    return smoothstep(0.30, 0.85, g) * 1.4 + 0.25;
}

// Wind, in three layers (after the Crysis vegetation method, in our own form):
//   main bend  - the whole plant leans downwind, more with height (squared), swaying with the gusts;
//   branches   - each main branch (uv1.x phase) swings on its own, more toward the tips (color.r);
//   leaves     - fast flutter, each card/blade with its own phase (color.g), stronger in gusts (color.b = leaf).
// The ground (sway 0) never moves; bending tips travel on an arc (they dip as they bend).
// Something walking through the grass (GrassInteractor sets it): xyz its feet, w the radius it parts (0 = none).
float4 _SB_Interactor;

// Blades within the radius are pushed out and down, more toward their tips (weight 0 at the root, 1 at the tip).
float3 SB_Interact(float3 p, float weight)
{
    if (_SB_Interactor.w <= 0) return p;
    float2 d = p.xz - _SB_Interactor.xz;
    float dist = length(d);
    float k = saturate(1 - dist / _SB_Interactor.w);
    k = k * k * saturate(weight) * saturate(1.5 - abs(p.y - _SB_Interactor.y) * 0.5);
    p.xz += d / (dist + 1e-4) * k * _SB_Interactor.w * 0.5;
    p.y -= k * 0.3;
    return p;
}

float3 SB_Wind(float3 positionWS, float3 positionOS, float3 normalWS, float4 color, float2 uv1, float windAmount)
{
    float3 dir = _SB_UseGlobal > 0.5 ? _SB_WindDir.xyz : normalize(float3(1.0, 0.0, 0.35));
    float strength = _SB_UseGlobal > 0.5 ? _SB_WindStrength : 0.5;
    float speed = _SB_UseGlobal > 0.5 ? _SB_WindSpeed : 1.0;
    float3 origin = UNITY_MATRIX_M._m03_m13_m23;
    float objPhase = dot(origin, float3(0.21, 0.0, 0.17));
    float t = _Time.y * speed;
    float gust = SB_Gust(origin, dir, t);
    float height = max(positionOS.y, 0.0);
    float bendW = color.r * color.r;
    // main bend: a steady lean that grows in gusts, plus a slow swing
    float lean = strength * windAmount * gust * (0.10 + 0.035 * height);
    float swing = sin(t * 0.9 + objPhase) * 0.35 + sin(t * 2.1 + objPhase * 1.7) * 0.12;
    float3 offset = dir * (lean * (1.0 + swing)) * bendW;
    // branches: their own swing, sideways as well as along the wind
    float3 side = float3(-dir.z, 0, dir.x);
    float bp = uv1.x * 6.2832 + objPhase;
    offset += (dir * sin(t * 1.7 + bp) + side * sin(t * 1.3 + bp * 1.3) * 0.6) * 0.06 * strength * windAmount * gust * color.r * (1 - color.b * 0.3);
    // leaves: quick flutter, mostly up/down across the wind
    float fl = sin(t * 7.0 + color.g * 31.0 + objPhase) * 0.6 + sin(t * 11.3 + color.g * 17.0) * 0.4;
    offset += normalize(float3(side.x, 0.8, side.z)) * fl * 0.028 * strength * (0.5 + gust) * color.r * color.b;
    positionWS += offset;
    positionWS.y -= dot(offset.xz, offset.xz) * 0.18;
    return positionWS;
}

// Soft stylised light: wrapped diffuse (no hard terminator), light shining through thin leaves from behind, ambient
// from the scene's sky. `wrap` 0 is plain Lambert, 0.5 is soft.
half3 SB_Light(Light light, half3 albedo, half3 N, half3 V, half wrap, half translucency)
{
    half ndl = dot(N, light.direction);
    half diff = saturate((ndl + wrap) / (1.0 + wrap));
    diff = diff * diff * (3.0 - 2.0 * diff) * 0.85 + diff * 0.15;
    half atten = light.shadowAttenuation * light.distanceAttenuation;
    half back = pow(saturate(dot(V, -light.direction)), 4.0) * translucency * (0.35 + 0.65 * atten) * light.distanceAttenuation;
    return albedo * light.color * (diff * atten + back);
}

half3 SB_Shade(half3 albedo, half3 N, float3 positionWS, float4 positionCS, half wrap, half translucency, half ambientOcclusion)
{
    half3 V = SafeNormalize(GetWorldSpaceViewDir(positionWS));
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = N;
    inputData.viewDirectionWS = V;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
    half4 shadowMask = half4(1, 1, 1, 1);
    Light mainLight = GetMainLight(inputData.shadowCoord, positionWS, shadowMask);
    mainLight.shadowAttenuation *= SB_CloudShadow(positionWS);
    half indirectAO = 1, directAO = 1;
#if defined(_SCREEN_SPACE_OCCLUSION)
    AmbientOcclusionFactor ssao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    indirectAO = ssao.indirectAmbientOcclusion; directAO = ssao.directAmbientOcclusion;
    mainLight.color *= directAO;
#endif
    half3 colour = SampleSH(N) * albedo * ambientOcclusion * indirectAO;
    colour += SB_Light(mainLight, albedo, N, V, wrap, translucency);
#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
        colour += SB_Light(light, albedo, N, V, wrap, translucency);
    }
    #endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
        colour += SB_Light(light, albedo, N, V, wrap, translucency);
    LIGHT_LOOP_END
#endif
    return colour;
}

// ---------------------------------------------------------------- atmosphere
// Height fog with sun glow (WeatherController sets these; without it Unity's own fog is used):
//   _SB_Atmo (1 on), _SB_FogDensity (per metre at the base height), _SB_FogFalloff (how fast it thins with height),
//   _SB_FogBase (world height of the densest fog), _SB_FogColor, _SB_FogSun (colour of the glow toward the sun).
float _SB_Atmo, _SB_FogDensity, _SB_FogFalloff, _SB_FogBase, _SB_Aerial;
float4 _SB_FogColor, _SB_FogSun;

half SB_FogAmount(float3 positionWS)
{
    float3 cam = _WorldSpaceCameraPos;
    float3 ray = positionWS - cam;
    float dist = length(ray);
    float k = _SB_FogFalloff;
    float h0 = cam.y - _SB_FogBase, dy = ray.y;
    // density integrated along the ray through exponential height fog
    float integral = abs(dy * k) > 1e-3 ? (exp(-k * h0) - exp(-k * (h0 + dy))) / (k * dy) : exp(-k * h0);
    float heightFog = _SB_FogDensity * dist * max(integral, 0.0);
    float aerial = _SB_Aerial * dist;                       // thin air everywhere: far things turn blue at any height
    return saturate(1.0 - exp(-(heightFog + aerial)));
}

half3 SB_FogColour(float3 viewDir)
{
    half sun = pow(saturate(dot(viewDir, _MainLightPosition.xyz)), 6.0);
    return lerp(_SB_FogColor.rgb, _SB_FogSun.rgb, sun);
}

half3 SB_Fog(half3 colour, float3 positionWS, half unityFog)
{
    if (_SB_Atmo < 0.5) return MixFog(colour, unityFog);
    float3 v = normalize(positionWS - _WorldSpaceCameraPos);
    return lerp(colour, SB_FogColour(v), SB_FogAmount(positionWS));
}

// Shadow caster position (the same bias rules as URP's own Lit shader).
float3 _LightDirection;
float3 _LightPosition;

float4 SB_ShadowPositionCS(float3 positionWS, float3 normalWS)
{
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    return ApplyShadowClamping(positionCS);
}

#endif
