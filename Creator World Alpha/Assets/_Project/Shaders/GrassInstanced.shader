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
            };

            StructuredBuffer<GrassData> _TransformBuffer;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float4 _AOColor;
                float4 _DryColor;
                float _ColorVariation;
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

                // Apply instance transform
                float4 positionWS = mul(instanceMatrix, float4(input.positionOS.xyz, 1.0));

                // Height gradient (0 at base, 1 at tip)
                float heightGradient = saturate(input.positionOS.y);
                output.heightGradient = heightGradient;
                output.density = density;
                output.colorVar = bladeHash;

                // Get instance scale
                float3 scale = float3(
                    length(instanceMatrix._m00_m10_m20),
                    length(instanceMatrix._m01_m11_m21),
                    length(instanceMatrix._m02_m12_m22)
                );

                // Wind animation (LOD0 gets full animation, others simplified)
                float windMultiplier = _LODIndex == 0 ? 1.0 : (_LODIndex == 1 ? 0.5 : 0.0);

                if (windMultiplier > 0)
                {
                    float time = _Time.y * _WindSpeed;
                    float2 windDir = normalize(_WindDirection.xy);

                    // Height-based deformation curve (stronger at tip)
                    float deformHeight = input.positionOS.y * scale.y;
                    float deformCurve = pow(saturate(deformHeight), 2.0);

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

                    // Perpendicular sway for more natural motion
                    float2 perpDir = float2(-windDir.y, windDir.x);
                    float perpSway = sin(time * 1.5 + bladeHash * 4.0) * 0.3 * deformCurve * windMultiplier;
                    positionWS.xz += perpSway * perpDir * _WindStrength;

                    // Slight vertical compression when bent
                    positionWS.y -= abs(totalWind) * 0.1;
                }

                // Transform normal
                float3x3 normalMatrix = (float3x3)instanceMatrix;
                float3 normalWS = normalize(mul(normalMatrix, input.normalOS));

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
                output.normalWS = normalWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Distance fade
                clip(input.fadeAlpha - 0.01);

                // === Color Calculation ===
                // AO at base fades quickly
                float aoFactor = saturate(input.heightGradient * 5.0);
                aoFactor = smoothstep(0.0, 1.0, aoFactor);

                // Base to tip gradient with smooth curve
                float tipBlend = smoothstep(0.0, 1.0, input.heightGradient);
                float3 baseToTip = lerp(_BaseColor.rgb, _TipColor.rgb, tipBlend);

                // Apply AO at base
                float3 grassColor = lerp(_AOColor.rgb, baseToTip, aoFactor);

                // Per-blade color variation
                float3 dryTint = lerp(grassColor, _DryColor.rgb * grassColor, input.colorVar * _ColorVariation);
                grassColor = lerp(grassColor, dryTint, step(0.5, input.colorVar) * _ColorVariation * 2.0);

                // Density-based variation
                grassColor *= lerp(0.85, 1.15, input.density);

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

                // === Specular Highlight ===
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normal, halfDir));
                float specular = pow(NdotH, _SpecularPower) * _SpecularStrength * input.heightGradient;
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
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            StructuredBuffer<GrassData> _TransformBuffer;

            float3 _LightDirection;
            int _LODIndex;

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
                float4 positionWS = mul(instanceMatrix, float4(input.positionOS.xyz, 1.0));

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
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            StructuredBuffer<GrassData> _TransformBuffer;

            Varyings DepthVert(Attributes input)
            {
                Varyings output;

                GrassData instanceData = _TransformBuffer[input.instanceID];
                float4x4 instanceMatrix = instanceData.trs;
                float4 positionWS = mul(instanceMatrix, float4(input.positionOS.xyz, 1.0));

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

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
