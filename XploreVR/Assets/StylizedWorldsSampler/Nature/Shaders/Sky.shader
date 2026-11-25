// Stylized Biomes — painted sky with clouds for the skybox. A gradient (zenith, horizon, below the horizon), a sun
// disc with a glow that follows the scene's main light, and two layers of soft cumulus drawn from noise on a dome,
// drifting with the wind, lit on their sun side with darker bases. The WeatherController drives cover, darkness and
// colour (_SB_CloudCover, _SB_Storm, _SB_SkyFlash, _SB_SkyTint); without one the material's own values are used.
Shader "Stylized Worlds Sampler/Nature/Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.30, 0.52, 0.86, 1)
        _Horizon ("Horizon", Color) = (0.74, 0.86, 0.94, 1)
        _Below ("Below Horizon", Color) = (0.42, 0.50, 0.55, 1)
        _SunColor ("Sun", Color) = (1, 0.95, 0.85, 1)
        _SunSize ("Sun Size", Range(0.00005, 0.02)) = 0.0004
        _CloudCover ("Cloud Cover (without a Weather Controller)", Range(0, 1)) = 0.35
        _CloudColor ("Cloud Lit", Color) = (1, 1, 1, 1)
        _CloudShade ("Cloud Shade", Color) = (0.62, 0.68, 0.80, 1)
        _CloudScale ("Cloud Size", Float) = 1.6
        _CloudSpeed ("Cloud Speed", Float) = 0.006
        [Toggle] _Volumetric ("Volumetric Clouds (ray-marched)", Float) = 1
        [IntRange] _CloudSteps ("Volumetric Quality (steps)", Range(8, 96)) = 48
        _CloudBase ("Cloud Base (m)", Float) = 1500
        _CloudTop ("Cloud Top (m)", Float) = 3600
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "NatureCommon.hlsl"

            half4 _Zenith, _Horizon, _Below, _SunColor, _CloudColor, _CloudShade;
            float _SunSize, _CloudCover, _CloudScale, _CloudSpeed, _Volumetric, _CloudSteps, _CloudBase, _CloudTop;
            float _SB_SkyFlash;
            float4 _SB_MoonDir;
            float4 _SB_SkyTint;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float Fbm(float2 p)
            {
                float s = 0, a = 0.5;
                for (int i = 0; i < 5; i++) { s += a * SB_Noise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return s;
            }

            // ---- volumetric clouds: a layer of cloud between _CloudBase and _CloudTop, density from 3D noise shaped by
            // the weather's cover (flat bases, billowy tops), ray-marched with a short march toward the sun at each step
            float Hash3(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float Noise3(float3 x)
            {
                float3 i = floor(x), f = frac(x); f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash3(i), Hash3(i + float3(1, 0, 0)), f.x), lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x), lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }
            float CloudDensity(float3 p, float cover, float2 wind, bool cheap)
            {
                float h = saturate((p.y - _CloudBase) / (_CloudTop - _CloudBase));
                float shapeH = saturate(h * 8.0) * saturate((1 - h) * 2.0);           // flat base, rounded top
                float3 q = float3(p.x + wind.x, p.y, p.z + wind.y) / 1800.0;
                float base = SB_Noise(q.xz * 0.6) * 0.65 + SB_Noise(q.xz * 1.7 + 3.1) * 0.35;   // where clouds are (weather map)
                float billow = Noise3(q * 3.0) * 0.6 + Noise3(q * 6.5 + 1.7) * 0.3;
                if (!cheap) billow += Noise3(q * 15.0 + 4.3) * 0.12;
                float d = base * shapeH - (1.0 - cover) * 0.75 - h * 0.25;            // thinner toward the top
                d = d + (billow - 0.55) * 0.35 * shapeH;
                return saturate(d * 5.0);                                             // crisp edges
            }
            // returns cloud colour (rgb) and coverage (a)
            float4 VolumetricClouds(float3 d, float3 sunDir, float cover, float storm, float3 sunCol, float3 skyCol, float2 pixel)
            {
                if (d.y < 0.01) return 0;
                float3 cam = float3(0, max(_WorldSpaceCameraPos.y, 0), 0);
                float t0 = (_CloudBase - cam.y) / d.y, t1 = (_CloudTop - cam.y) / d.y;
                t1 = min(t1, t0 + 12000.0);
                int steps = (int)_CloudSteps;
                float dt = (t1 - t0) / steps;
                float2 wind = (_SB_UseGlobal > 0.5 ? _SB_WindDir.xz : float2(1, 0.35)) * _Time.y * _CloudSpeed * 2000.0;
                // a different start for every pixel (interleaved gradient noise): fine grain instead of stripes
                float jitter = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))) + _Time.y * 0.0);
                float trans = 1.0; float3 light = 0;
                float phase = 0.6 + 0.9 * pow(saturate(dot(d, sunDir)), 6);             // forward scattering: bright toward the sun
                [loop] for (int k = 0; k < steps; k++)
                {
                    float3 p = cam + d * (t0 + (k + jitter) * dt);
                    float den = CloudDensity(p, cover, wind, false);
                    if (den > 0.01)
                    {
                        float toSun = 0;                                                 // a short march toward the sun
                        [unroll] for (int j = 1; j <= 4; j++) toSun += CloudDensity(p + sunDir * j * 140.0, cover, wind, true);
                        float beer = exp(-toSun * 1.1);
                        float powder = 1 - exp(-den * 4.0);                              // dark edges into the sun, bright lit tops
                        float h = saturate((p.y - _CloudBase) / (_CloudTop - _CloudBase));
                        float3 amb = skyCol * (0.55 + 0.45 * h);                           // sky light: brighter on top
                        float3 lit = sunCol * beer * phase * lerp(1.0, powder, 0.6) * 1.6 + amb;
                        lit *= 1 - storm * 0.6;
                        float a = 1 - exp(-den * dt * 0.012);
                        light += trans * a * lit;
                        trans *= 1 - a;
                        if (trans < 0.02) break;
                    }
                }
                float fade = saturate(d.y * 8.0);                                        // dissolve into the horizon haze
                return float4(light, (1 - trans) * fade);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sunDir = _MainLightPosition.xyz;
                float cover = _SB_UseWeather > 0.5 ? _SB_CloudCover : _CloudCover;
                float storm = _SB_UseWeather > 0.5 ? _SB_Storm : 0;
                float3 tint = _SB_UseWeather > 0.5 ? _SB_SkyTint.rgb : float3(1, 1, 1);
                // gradient
                float up = d.y;
                half3 sky = lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(up), 0.55));
                sky = lerp(sky, _Below.rgb, saturate(-up * 6));
                float sunDot = dot(d, sunDir);
                sky += _SunColor.rgb * pow(saturate(sunDot), 64) * 0.35 * (1 - storm);          // glow round the sun
                sky += _SunColor.rgb * float3(1.0, 0.86, 0.62) * pow(saturate(sunDot), 4) * 0.28 * (1 - storm) * saturate(1 - up);   // warm wide glow low in the sun's half of the sky
                sky = lerp(sky, dot(sky, float3(0.3, 0.55, 0.15)) * float3(0.62, 0.66, 0.72), storm * 0.85);   // grey under a storm
                sky *= tint;
                half sun = smoothstep(1 - _SunSize, 1 - _SunSize * 0.5, sunDot) * saturate(up * 20 + 0.5);
                sky += _SunColor.rgb * pow(saturate(sunDot), 900) * 0.8 * (1 - storm);          // a tight bright halo round the disc
                // clouds on a dome: two layers, drifting with the wind
                half3 col = sky;
                if (_Volumetric > 0.5 && up > -0.05)
                {
                    float3 skyAmb = lerp(_Horizon.rgb, _Zenith.rgb, 0.5) * 1.1 * tint;
                    float3 sunC = _SunColor.rgb * (1 - _SB_Night * 0.95);
                    float4 vc = VolumetricClouds(d, sunDir, cover, storm, sunC, skyAmb, i.positionCS.xy);
                    vc.rgb *= lerp(half3(1, 1, 1), half3(0.07, 0.08, 0.13), _SB_Night);
                    col = col * (1 - vc.a) + vc.rgb;
                    sun *= 1 - vc.a;
                }
                else if (up > -0.05)
                {
                    float2 uv = d.xz / (up + 0.18) * _CloudScale;
                    float2 wind = (_SB_UseGlobal > 0.5 ? _SB_WindDir.xz : float2(1, 0.35)) * _Time.y * _CloudSpeed * 40;
                    float n1 = Fbm(uv + wind);
                    float n2 = Fbm(uv * 2.2 + wind * 1.6 + 5.3);
                    float shape = n1 * 0.75 + n2 * 0.25;
                    float threshold = lerp(0.68, 0.30, cover);
                    float c = smoothstep(threshold, threshold + 0.05 + storm * 0.2, shape);   // defined edges
                    // lit from the sun side: sample the shape a little toward the sun; denser = darker base
                    float toward = Fbm(uv + wind + normalize(sunDir.xz + 1e-4) * 0.08);
                    float lit = saturate(0.5 + (shape - toward) * 9.0 + (shape - threshold) * 1.5);   // bright lit tops, grey bases
                    half3 cloud = lerp(_CloudShade.rgb, _CloudColor.rgb, lit);
                    cloud = lerp(cloud, cloud * float3(0.42, 0.45, 0.52), storm);
                    cloud *= tint;
                    cloud *= lerp(half3(1, 1, 1), half3(0.07, 0.08, 0.13), _SB_Night);       // clouds at night: dark, faintly lit
                    cloud += _SunColor.rgb * pow(saturate(sunDot), 8) * 0.25 * (1 - c * 0.6) * (1 - storm);   // silver lining
                    float fade = saturate(up * 6);                                       // thin out at the horizon
                    col = lerp(sky, cloud, c * fade * (0.85 + 0.15 * cover));
                    sun *= 1 - c;
                }
                col += _SunColor.rgb * sun * 2 * (1 - storm);
                if (_SB_Rain > 0.05 && up > -0.05 && up < 0.3)
                {   // rain curtains: soft vertical grey shafts low on the sky, drifting with the wind
                    float az = atan2(d.z, d.x);
                    float shaft = smoothstep(0.45, 0.8, SB_Noise(float2(az * 9.0 + _Time.y * 0.01, 0.5)) * 0.7 + SB_Noise(float2(az * 31.0, up * 3.0 - _Time.y * 0.05)) * 0.3);
                    float band = smoothstep(0.3, 0.05, up) * smoothstep(-0.05, 0.02, up);
                    col = lerp(col, dot(col, float3(0.3, 0.55, 0.15)) * float3(0.70, 0.74, 0.80), shaft * band * _SB_Rain * 0.6);
                }
                // thick fog: the sky near the horizon goes into the fog's colour, so fogged land has no hard edge
                if (_SB_Atmo > 0.5)
                {   // the same height fog as the land, looking out along this direction: haze at the horizon
                    float3 far = _WorldSpaceCameraPos + d * 2500.0;
                    col = lerp(col, SB_FogColour(d), SB_FogAmount(far) * saturate(1.0 - up * 3.5));   // haze only low on the sky
                }
                else
                {
                    float fogRange = -1.0 / min(unity_FogParams.z, -1e-4);          // linear fog: end - start
                    float thick = saturate(1 - fogRange / 450.0);
                    col = lerp(col, unity_FogColor.rgb, thick * saturate(1 - up * (2.5 - thick * 1.5)));
                }
                if (_SB_Night > 0.01 && up > 0)
                {   // stars: one per cell of a grid on the sky, some bright, gently twinkling; hidden by cloud and haze
                    float3 sd = d * 220.0;
                    float3 cell = floor(sd);
                    float h = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                    float3 f = frac(sd) - 0.5;
                    float star = step(0.982, h) * smoothstep(0.16, 0.02, length(f)) * (0.5 + 0.5 * frac(h * 91.7));
                    star *= 0.75 + 0.25 * sin(_Time.y * (2 + h * 5) + h * 40);
                    float mw = smoothstep(0.75, 0.0, abs(dot(d, normalize(float3(0.3, 0.2, 1)))));   // a faint band of the galaxy
                    col += (star * 1.6 + mw * 0.03 * SB_Noise(d.xz * 9 + d.y * 4)) * _SB_Night * saturate(up * 4) * (1 - cover * 0.9);
                    float md = dot(d, normalize(_SB_MoonDir.xyz));                       // the moon
                    col += float3(0.85, 0.88, 1.0) * (smoothstep(0.99965, 0.9998, md) * 1.3 + pow(saturate(md), 600) * 0.2) * smoothstep(0.65, 0.95, _SB_Night);   // the moon only once it is dark
                }
                col += _SB_SkyFlash * float3(0.8, 0.85, 1.0);                           // lightning
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
