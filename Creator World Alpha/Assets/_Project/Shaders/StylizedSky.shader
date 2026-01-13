Shader "Custom/StylizedSky"
{
    Properties
    {
        [Header(Sky Colors)]
        _SkyTopColor ("Sky Top Color", Color) = (0.3, 0.5, 0.9, 1)
        _SkyHorizonColor ("Sky Horizon Color", Color) = (0.8, 0.9, 1, 1)
        _HorizonSharpness ("Horizon Sharpness", Range(0.1, 10)) = 2.0
        _HorizonOffset ("Horizon Offset", Range(-0.5, 0.5)) = 0.0

        [Header(Sun)]
        _SunColor ("Sun Color", Color) = (1, 0.95, 0.8, 1)
        _SunDirection ("Sun Direction", Vector) = (0, 1, 0, 0)
        _SunSize ("Sun Size", Range(0.001, 0.2)) = 0.05
        _SunBloom ("Sun Bloom", Range(0, 2)) = 0.5
        _SunIntensity ("Sun Intensity", Range(0, 5)) = 1.0

        [Header(Moon)]
        _MoonColor ("Moon Color", Color) = (0.9, 0.95, 1, 1)
        _MoonDirection ("Moon Direction", Vector) = (0, -1, 0, 0)
        _MoonSize ("Moon Size", Range(0.001, 0.15)) = 0.045
        _MoonIntensity ("Moon Intensity", Range(0, 3)) = 1.2

        [Header(Stars)]
        _StarsDensity ("Stars Density", Range(0, 500)) = 300
        _StarsIntensity ("Stars Intensity", Range(0, 3)) = 1.5
        _StarsTwinkleSpeed ("Stars Twinkle Speed", Range(0, 5)) = 1.0
        _StarsThreshold ("Stars Threshold", Range(0, 1)) = 0.5
        _NightFactor ("Night Factor", Range(0, 1)) = 0

        [Header(Clouds)]
        _CloudColor ("Cloud Color", Color) = (1, 1, 1, 1)
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.3
        _CloudSpeed ("Cloud Speed", Range(0, 1)) = 0.02
        _CloudScale ("Cloud Scale", Range(0.1, 10)) = 2.0
        _CloudSharpness ("Cloud Sharpness", Range(0.1, 5)) = 1.5
        _CloudOpacity ("Cloud Opacity", Range(0, 1)) = 0.8

        [Header(Night Sky)]
        _NightSkyColor ("Night Sky Color", Color) = (0.01, 0.01, 0.025, 1)
        _NightHorizonColor ("Night Horizon Color", Color) = (0.02, 0.02, 0.04, 1)

        [Header(Atmosphere)]
        _AtmosphereDensity ("Atmosphere Density", Range(0, 2)) = 0.5
        _SunsetGlow ("Sunset Glow Color", Color) = (1, 0.5, 0.2, 1)
        _SunsetGlowIntensity ("Sunset Glow Intensity", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            Name "StylizedSky"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Sky
            float4 _SkyTopColor;
            float4 _SkyHorizonColor;
            float _HorizonSharpness;
            float _HorizonOffset;

            // Sun
            float4 _SunColor;
            float4 _SunDirection;
            float _SunSize;
            float _SunBloom;
            float _SunIntensity;

            // Moon
            float4 _MoonColor;
            float4 _MoonDirection;
            float _MoonSize;
            float _MoonIntensity;

            // Stars
            float _StarsDensity;
            float _StarsIntensity;
            float _StarsTwinkleSpeed;
            float _StarsThreshold;
            float _NightFactor;

            // Clouds
            float4 _CloudColor;
            float _CloudCoverage;
            float _CloudSpeed;
            float _CloudScale;
            float _CloudSharpness;
            float _CloudOpacity;

            // Night sky
            float4 _NightSkyColor;
            float4 _NightHorizonColor;

            // Atmosphere
            float _AtmosphereDensity;
            float4 _SunsetGlow;
            float _SunsetGlowIntensity;

            // Hash function for procedural noise
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // 2D value noise
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Fractal Brownian Motion for clouds
            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * noise(p * frequency);
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }

                return value;
            }

            // Star field generation
            float stars(float3 dir)
            {
                // Project direction to 2D for star placement
                float2 uv = float2(atan2(dir.x, dir.z) / PI * 0.5 + 0.5, asin(dir.y) / PI + 0.5);
                uv *= _StarsDensity;

                // Create star grid
                float2 gridUV = frac(uv) - 0.5;
                float2 id = floor(uv);

                // Random star position within cell
                float2 offset = float2(hash(id), hash(id + 100.0)) - 0.5;
                float2 starPos = gridUV - offset * 0.8;

                // Star brightness with twinkle
                float brightness = hash(id + 200.0);
                float twinkle = sin(_Time.y * _StarsTwinkleSpeed * (brightness * 2.0 + 1.0)) * 0.3 + 0.7;

                // Star shape (sharp point) - vary size with brightness
                float starSize = 0.03 + brightness * 0.04;
                float star = 1.0 - smoothstep(0.0, starSize, length(starPos));
                star *= brightness * twinkle;

                // Show stars above threshold (lower = more stars)
                return star * step(_StarsThreshold, brightness);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.viewDir = input.positionOS.xyz;

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(input.viewDir);

                // ===== SKY GRADIENT =====
                float horizonFactor = pow(1.0 - saturate(viewDir.y + _HorizonOffset), _HorizonSharpness);

                // Day sky colors
                float3 daySkyColor = lerp(_SkyTopColor.rgb, _SkyHorizonColor.rgb, horizonFactor);

                // Night sky colors - darker gradient
                float3 nightSkyGradient = lerp(_NightSkyColor.rgb, _NightHorizonColor.rgb, horizonFactor);

                // Blend between day and night sky based on night factor
                float3 skyColor = lerp(daySkyColor, nightSkyGradient, _NightFactor);

                // ===== SUN =====
                float3 sunDir = normalize(_SunDirection.xyz);
                float sunDot = dot(viewDir, sunDir);

                // Sun disc
                float sunDisc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.5, sunDot);

                // Sun bloom/glow
                float sunBloom = pow(saturate(sunDot), 8.0 / max(_SunBloom, 0.01)) * _SunBloom;

                // Fade sun out at night
                float dayFactor = 1.0 - _NightFactor;
                float3 sunContribution = (_SunColor.rgb * sunDisc + _SunColor.rgb * sunBloom * 0.3) * _SunIntensity * dayFactor;

                // ===== MOON =====
                float3 moonDir = normalize(_MoonDirection.xyz);
                float moonDot = dot(viewDir, moonDir);
                float moonDisc = smoothstep(1.0 - _MoonSize, 1.0 - _MoonSize * 0.2, moonDot);
                // Add subtle glow around moon
                float moonGlow = pow(saturate(moonDot), 16.0) * 0.3;
                float3 moonContribution = _MoonColor.rgb * (moonDisc + moonGlow) * _MoonIntensity * _NightFactor;

                // ===== STARS =====
                float starField = stars(viewDir) * _StarsIntensity * _NightFactor;
                // Gentler horizon fade for stars
                starField *= saturate(viewDir.y * 2.0 + 0.1);
                float3 starsContribution = float3(1, 1, 1) * starField;

                // ===== CLOUDS =====
                // Project view direction onto dome for cloud UVs
                float2 cloudUV = viewDir.xz / (viewDir.y + 0.5) * _CloudScale;
                cloudUV += _Time.y * _CloudSpeed * float2(1, 0.3);

                float cloudNoise = fbm(cloudUV);
                float clouds = smoothstep(_CloudCoverage, _CloudCoverage + 0.3 / _CloudSharpness, cloudNoise);
                clouds *= saturate(viewDir.y * 4.0); // Fade at horizon

                float3 cloudContribution = _CloudColor.rgb * clouds * _CloudOpacity;
                // Tint clouds with sun color during sunset
                cloudContribution = lerp(cloudContribution, cloudContribution * _SunColor.rgb, sunBloom * 0.5 * dayFactor);
                // Darken clouds at night
                cloudContribution *= lerp(0.15, 1.0, dayFactor);

                // ===== SUNSET GLOW =====
                float horizonGlow = pow(1.0 - abs(viewDir.y), 4.0);
                float sunsetFactor = pow(saturate(dot(viewDir, sunDir)), 2.0) * horizonGlow;
                float3 sunsetContribution = _SunsetGlow.rgb * sunsetFactor * _SunsetGlowIntensity * dayFactor;

                // ===== ATMOSPHERIC SCATTERING (simplified) =====
                float3 atmosphereColor = lerp(_SkyHorizonColor.rgb, _SunsetGlow.rgb, sunsetFactor);
                float atmosphereFactor = horizonGlow * _AtmosphereDensity * dayFactor;

                // ===== COMBINE =====
                float3 finalColor = skyColor;
                finalColor += sunContribution;
                finalColor += moonContribution;
                finalColor += starsContribution;
                finalColor = lerp(finalColor, cloudContribution + finalColor * (1.0 - clouds * _CloudOpacity), clouds * _CloudOpacity);
                finalColor += sunsetContribution;
                finalColor = lerp(finalColor, atmosphereColor, atmosphereFactor * 0.3);

                // Ensure no negative colors
                finalColor = max(finalColor, 0.0);

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
