Shader "CreatorWorld/GrassInstanced"
{
    Properties
    {
        [Header(Colors)]
        _BaseColor ("Base Color", Color) = (0.15, 0.35, 0.08, 1)
        _TipColor ("Tip Color", Color) = (0.5, 0.85, 0.25, 1)
        _AOColor ("Ambient Occlusion Color", Color) = (0.08, 0.12, 0.04, 1)
        _DryColor ("Dry Grass Tint", Color) = (0.6, 0.55, 0.2, 1)
        _ColorVariation ("Color Variation", Range(0, 0.5)) = 0.15

        [Header(Blade Shape)]
        _TaperAmount ("Taper Amount", Range(0, 1)) = 0.98
        _TaperPower ("Taper Sharpness", Range(1, 4)) = 2.5
        _BladeCurve ("Blade Curve", Range(0, 0.5)) = 0.15
        _BladeWidth ("Blade Width Multiplier", Range(0.5, 2)) = 1.0
        _ViewThickness ("View Thickness (GoT)", Range(0, 0.1)) = 0.03

        [Header(Wind)]
        _WindStrength ("Wind Strength", Range(0, 2)) = 0.6
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.2
        _WindNoiseScale ("Wind Noise Scale", Range(0.01, 0.5)) = 0.08
        _WindDirection ("Wind Direction", Vector) = (1, 0.3, 0, 0)
        _GustStrength ("Gust Strength", Range(0, 2)) = 0.4
        _GustFrequency ("Gust Frequency", Range(0.1, 2)) = 0.3
        _TipWobble ("Tip Wobble", Range(0, 1)) = 0.3
        _TipWobbleFreq ("Tip Wobble Frequency", Range(1, 10)) = 4

        [Header(Lighting)]
        _Translucency ("Translucency (SSS)", Range(0, 1)) = 0.4
        _TranslucencyPower ("Translucency Power", Range(1, 10)) = 3
        _SpecularStrength ("Specular Strength", Range(0, 1)) = 0.15
        _SpecularPower ("Specular Power", Range(1, 64)) = 16
        _NormalRoundness ("Normal Roundness", Range(0, 1)) = 0.5
        _DensityAO ("Density AO Strength", Range(0, 1)) = 0.3
        _MinBrightness ("Min Brightness", Range(0, 1)) = 0.25
        _ShadowBrightness ("Shadow Brightness", Range(0, 1)) = 0.3

        [Header(Distance)]
        _MaxViewDistance ("Max View Distance", Float) = 150
        _FadeStart ("Fade Start", Range(0, 1)) = 0.7
        _FadeEnd ("Fade End", Range(0, 1)) = 1.0
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct GrassData
            {
                float4x4 trs;
                float density;
            };

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float heightGradient : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                float fadeAlpha : TEXCOORD5;
                float density : TEXCOORD6;
                float colorVar : TEXCOORD7;
                float3 bladeUp : TEXCOORD8;
            };

            StructuredBuffer<GrassData> _TransformBuffer;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float4 _AOColor;
                float4 _DryColor;
                float _ColorVariation;
                float _TaperAmount;
                float _TaperPower;
                float _BladeCurve;
                float _BladeWidth;
                float _ViewThickness;
                float _WindStrength;
                float _WindSpeed;
                float _WindNoiseScale;
                float4 _WindDirection;
                float _GustStrength;
                float _GustFrequency;
                float _TipWobble;
                float _TipWobbleFreq;
                float _Translucency;
                float _TranslucencyPower;
                float _SpecularStrength;
                float _SpecularPower;
                float _NormalRoundness;
                float _DensityAO;
                float _MinBrightness;
                float _ShadowBrightness;
                float _MaxViewDistance;
                float _FadeStart;
                float _FadeEnd;
                float _AlphaCutoff;
                float3 _CameraPosition;
                float _LOD1Threshold;
                float _LOD2Threshold;
                float _DistanceRatio;
                int _LODIndex;
            CBUFFER_END

            // Hash functions for noise
            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Smooth noise
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Fractal brownian motion for natural wind
            float fbm(float2 p, int octaves)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                for (int i = 0; i < octaves; i++)
                {
                    value += amplitude * noise(p * frequency);
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                GrassData instanceData = _TransformBuffer[input.instanceID];
                float4x4 instanceMatrix = instanceData.trs;
                float density = instanceData.density;

                // Extract instance position for per-blade variation
                float3 instancePos = float3(instanceMatrix._m03, instanceMatrix._m13, instanceMatrix._m23);
                float bladeHash = hash21(instancePos.xz);

                // === PROCEDURAL BLADE TAPERING ===
                // Use UV.y (0-1) instead of positionOS.y (0-0.6) for proper tapering
                float heightGradient = input.uv.y;

                // Taper: Shrink width towards tip
                float taperCurve = 1.0 - pow(heightGradient, _TaperPower) * _TaperAmount;

                // Apply taper to X position (width)
                float3 shapedPos = input.positionOS.xyz;
                shapedPos.x *= taperCurve * _BladeWidth;
                shapedPos.z *= taperCurve * _BladeWidth;

                // Add natural curve to the blade (S-curve bend)
                float curveDir = (bladeHash - 0.5) * 2.0;
                float curveFactor = sin(heightGradient * 3.14159) * _BladeCurve * curveDir;
                shapedPos.x += curveFactor * heightGradient;

                // Slight forward lean based on height
                float lean = heightGradient * heightGradient * 0.1 * (bladeHash * 0.5 + 0.5);
                shapedPos.z += lean;

                // Apply instance transform to shaped position
                float4 positionWS = mul(instanceMatrix, float4(shapedPos, 1.0));

                // Store blade up direction for lighting
                float3x3 normalMatrix = (float3x3)instanceMatrix;
                float3 bladeUp = normalize(mul(normalMatrix, float3(0, 1, 0)));
                output.bladeUp = bladeUp;

                output.heightGradient = heightGradient;
                output.density = density;
                output.colorVar = bladeHash;

                // Wind animation (LOD0 gets full animation, others simplified)
                float windMultiplier = _LODIndex == 0 ? 1.0 : (_LODIndex == 1 ? 0.5 : 0.0);

                if (windMultiplier > 0)
                {
                    float time = _Time.y * _WindSpeed;
                    float2 windDir = normalize(_WindDirection.xy);

                    // Height-based deformation curve (stronger at tip)
                    float deformCurve = pow(heightGradient, 2.0);

                    // === Main Wind Layer ===
                    float2 windUV = positionWS.xz * _WindNoiseScale + windDir * time;
                    float mainWind = fbm(windUV, 3) * 2.0 - 1.0;

                    // Per-blade phase offset for variation
                    float phaseOffset = bladeHash * 6.28;
                    mainWind += sin(time * 2.0 + phaseOffset) * 0.2;

                    // === Wind Gusts ===
                    float gustTime = time * _GustFrequency;
                    float gustNoise = noise(positionWS.xz * 0.02 + gustTime);
                    float gust = smoothstep(0.6, 1.0, gustNoise) * _GustStrength;

                    // === Secondary Tip Wobble ===
                    float tipWobble = 0.0;
                    if (heightGradient > 0.5)
                    {
                        float tipFactor = (heightGradient - 0.5) * 2.0;
                        tipWobble = sin(time * _TipWobbleFreq + bladeHash * 10.0) * _TipWobble * tipFactor;
                    }

                    // Combine wind forces
                    float totalWind = (mainWind * _WindStrength + gust + tipWobble) * deformCurve * windMultiplier;

                    // Apply wind displacement
                    positionWS.xz += totalWind * windDir;

                    // Perpendicular sway
                    float2 perpDir = float2(-windDir.y, windDir.x);
                    float perpSway = sin(time * 1.5 + bladeHash * 4.0) * 0.3 * deformCurve * windMultiplier;
                    positionWS.xz += perpSway * perpDir * _WindStrength;

                    // Slight vertical compression when bent
                    positionWS.y -= abs(totalWind) * 0.1;
                }

                // === GHOST OF TSUSHIMA TRICK: VIEW-SPACE THICKENING ===
                // Blades expand perpendicular to view when seen edge-on
                float3 viewDir = normalize(_CameraPosition - positionWS.xyz);
                float3 bladeNormal = normalize(mul(normalMatrix, input.normalOS));

                // How much are we viewing edge-on? (0 = facing camera, 1 = edge-on)
                float edgeFactor = 1.0 - abs(dot(viewDir, bladeNormal));
                edgeFactor = saturate(edgeFactor);

                // Calculate view-perpendicular direction
                float3 viewRight = normalize(cross(viewDir, float3(0, 1, 0)));

                // Expand in view space based on edge factor and height
                float thicknessExpand = edgeFactor * _ViewThickness * heightGradient;
                float side = sign(input.positionOS.x); // Which side of blade
                positionWS.xyz += viewRight * thicknessExpand * side;

                // === ROUNDED NORMALS for better specular ===
                // Blend between flat normal and rounded (cylindrical) normal
                float3 flatNormal = bladeNormal;
                // Create a rounded normal that curves around the blade
                float3 sideDir = normalize(cross(bladeUp, viewDir));
                float normalCurve = input.positionOS.x * 10.0; // X position determines curve
                float3 roundedNormal = normalize(lerp(flatNormal, sideDir * normalCurve + bladeUp * 0.5, _NormalRoundness));
                // Blend more to up at tip for better light catch
                roundedNormal = normalize(lerp(roundedNormal, bladeUp, heightGradient * 0.3));

                output.normalWS = roundedNormal;

                // Distance fade
                float distance = length(positionWS.xyz - _CameraPosition);
                float distanceRatio = distance / _MaxViewDistance;
                float fadeAlpha = 1.0;
                if (distanceRatio > _FadeStart)
                {
                    fadeAlpha = 1.0 - saturate((distanceRatio - _FadeStart) / (_FadeEnd - _FadeStart));
                }
                output.fadeAlpha = fadeAlpha;

                output.positionWS = positionWS.xyz;
                output.positionCS = TransformWorldToHClip(positionWS.xyz);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Distance fade
                clip(input.fadeAlpha - 0.01);

                // === Color Calculation ===
                // Height-based AO (darker at base)
                float heightAO = saturate(input.heightGradient * 4.0);
                heightAO = smoothstep(0.0, 1.0, heightAO);

                // === GHOST OF TSUSHIMA: DENSITY-BASED AO ===
                // Denser grass = darker at base (light blocked)
                float densityAO = lerp(1.0, 1.0 - _DensityAO, input.density);
                float combinedAO = heightAO * densityAO;

                // Base to tip gradient with smooth curve
                float tipBlend = smoothstep(0.0, 1.0, input.heightGradient);
                float3 baseToTip = lerp(_BaseColor.rgb, _TipColor.rgb, tipBlend);

                // Apply combined AO at base
                float3 grassColor = lerp(_AOColor.rgb, baseToTip, combinedAO);

                // Per-blade color variation
                float3 dryTint = lerp(grassColor, _DryColor.rgb * grassColor, input.colorVar * _ColorVariation);
                grassColor = lerp(grassColor, dryTint, step(0.5, input.colorVar) * _ColorVariation * 2.0);

                // Density-based brightness variation (denser = slightly darker overall)
                grassColor *= lerp(1.0, 0.9, input.density * 0.5);

                // === Lighting ===
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                float3 viewDir = normalize(_CameraPosition - input.positionWS);
                float3 normal = normalize(input.normalWS);

                // Wrap lighting for soft grass look
                float NdotL = dot(normal, lightDir);
                float wrappedNdotL = NdotL * 0.5 + 0.5;
                wrappedNdotL = wrappedNdotL * wrappedNdotL;

                // === Subsurface Scattering (Translucency) ===
                float3 backLitDir = normalize(lightDir + normal * 0.5);
                float VdotL = saturate(dot(viewDir, -backLitDir));
                float translucency = pow(VdotL, _TranslucencyPower) * _Translucency;
                float3 sssColor = mainLight.color * translucency * _TipColor.rgb * input.heightGradient;

                // === GHOST OF TSUSHIMA: SPECULAR ALONG BLADE ===
                // Specular highlight that travels along the blade
                float3 halfDir = normalize(lightDir + viewDir);

                // Use blade up direction for anisotropic-like specular
                float3 bladeDir = normalize(input.bladeUp);
                float NdotH = saturate(dot(normal, halfDir));
                float BdotH = dot(bladeDir, halfDir);

                // Anisotropic specular approximation
                float specAniso = exp(-BdotH * BdotH * 10.0);
                float specular = pow(NdotH, _SpecularPower) * _SpecularStrength;
                specular *= lerp(1.0, specAniso, 0.5); // Blend anisotropic
                specular *= input.heightGradient; // Stronger at tip
                float3 specularColor = mainLight.color * specular;

                // Ambient
                float3 ambient = SampleSH(normal);

                // Diffuse
                float3 diffuse = mainLight.color * wrappedNdotL;

                // Combine lighting
                float3 lighting = ambient + diffuse + sssColor + specularColor;
                lighting = max(lighting, _MinBrightness);

                // Shadows
                #ifdef _MAIN_LIGHT_SHADOWS
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                    float shadow = MainLightRealtimeShadow(shadowCoord);
                    shadow = lerp(_ShadowBrightness, 1.0, shadow);
                    // SSS still shows through shadows slightly
                    lighting = lighting * shadow + sssColor * 0.3 * (1.0 - shadow);
                #endif

                float3 finalColor = grassColor * lighting;

                // Fog
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, input.fadeAlpha);
            }
            ENDHLSL
        }

        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct GrassData
            {
                float4x4 trs;
                float density;
            };

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            StructuredBuffer<GrassData> _TransformBuffer;

            float3 _LightDirection;
            int _LODIndex;
            float _TaperAmount;
            float _TaperPower;
            float _BladeCurve;
            float _BladeWidth;

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;

                // Skip shadows for LOD1+
                if (_LODIndex > 0)
                {
                    output.positionCS = float4(0, 0, 0, 0);
                    return output;
                }

                GrassData instanceData = _TransformBuffer[input.instanceID];
                float4x4 instanceMatrix = instanceData.trs;

                float3 instancePos = float3(instanceMatrix._m03, instanceMatrix._m13, instanceMatrix._m23);
                float bladeHash = hash21(instancePos.xz);

                // Use UV.y (0-1) for proper tapering
                float heightGradient = input.uv.y;
                float taperCurve = 1.0 - pow(heightGradient, _TaperPower) * _TaperAmount;

                float3 shapedPos = input.positionOS.xyz;
                shapedPos.x *= taperCurve * _BladeWidth;
                shapedPos.z *= taperCurve * _BladeWidth;

                float curveDir = (bladeHash - 0.5) * 2.0;
                float curveFactor = sin(heightGradient * 3.14159) * _BladeCurve * curveDir;
                shapedPos.x += curveFactor * heightGradient;

                float4 positionWS = mul(instanceMatrix, float4(shapedPos, 1.0));

                float3x3 normalMatrix = (float3x3)instanceMatrix;
                float3 normalWS = normalize(mul(normalMatrix, input.normalOS));

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS.xyz, normalWS, _LightDirection));

                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // Depth pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct GrassData
            {
                float4x4 trs;
                float density;
            };

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            StructuredBuffer<GrassData> _TransformBuffer;

            float _TaperAmount;
            float _TaperPower;
            float _BladeCurve;
            float _BladeWidth;

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings DepthVert(Attributes input)
            {
                Varyings output;

                GrassData instanceData = _TransformBuffer[input.instanceID];
                float4x4 instanceMatrix = instanceData.trs;

                float3 instancePos = float3(instanceMatrix._m03, instanceMatrix._m13, instanceMatrix._m23);
                float bladeHash = hash21(instancePos.xz);

                // Use UV.y (0-1) for proper tapering
                float heightGradient = input.uv.y;
                float taperCurve = 1.0 - pow(heightGradient, _TaperPower) * _TaperAmount;

                float3 shapedPos = input.positionOS.xyz;
                shapedPos.x *= taperCurve * _BladeWidth;
                shapedPos.z *= taperCurve * _BladeWidth;

                float curveDir = (bladeHash - 0.5) * 2.0;
                float curveFactor = sin(heightGradient * 3.14159) * _BladeCurve * curveDir;
                shapedPos.x += curveFactor * heightGradient;

                float4 positionWS = mul(instanceMatrix, float4(shapedPos, 1.0));

                output.positionCS = TransformWorldToHClip(positionWS.xyz);

                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
