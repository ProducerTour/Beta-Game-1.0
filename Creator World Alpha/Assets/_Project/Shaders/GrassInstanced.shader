Shader "CreatorWorld/GrassInstanced"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.2, 0.6, 0.1, 1)
        _TipColor ("Tip Color", Color) = (0.4, 0.8, 0.2, 1)
        _AOColor ("Ambient Occlusion Color", Color) = (0.1, 0.2, 0.05, 1)
        _WindStrength ("Wind Strength", Float) = 0.5
        _WindSpeed ("Wind Speed", Float) = 1.0
        _WindNoiseScale ("Wind Noise Scale", Float) = 0.1
        _WindDirection ("Wind Direction", Vector) = (1, 0.5, 0, 0)
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _MaxViewDistance ("Max View Distance", Float) = 150
        _FadeStart ("Fade Start", Range(0, 1)) = 0.7
        _FadeEnd ("Fade End", Range(0, 1)) = 1.0
        _MinBrightness ("Min Brightness", Range(0, 1)) = 0.3
        _ShadowBrightness ("Shadow Brightness", Range(0, 1)) = 0.2
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
            };

            // Instance data buffer (with density)
            StructuredBuffer<GrassData> _TransformBuffer;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float4 _AOColor;
                float _WindStrength;
                float _WindSpeed;
                float _WindNoiseScale;
                float4 _WindDirection;
                float _AlphaCutoff;
                float _MaxViewDistance;
                float _FadeStart;
                float _FadeEnd;
                float _MinBrightness;
                float _ShadowBrightness;
                float3 _CameraPosition;
                float _LOD1Threshold;
                float _LOD2Threshold;
                float _DistanceRatio;
                int _LODIndex;
            CBUFFER_END

            // Improved noise function for wind
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

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

            float2 Unity_SimpleNoise(float2 UV, float Scale)
            {
                float2 p = UV * Scale;
                float n = noise(p);
                float n2 = noise(p * 1.3 + 100.0);
                return float2(n, n2);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Get instance data
                GrassData instanceData = _TransformBuffer[input.instanceID];
                float4x4 instanceMatrix = instanceData.trs;
                float density = instanceData.density;

                // Apply instance transform
                float4 positionWS = mul(instanceMatrix, float4(input.positionOS.xyz, 1.0));

                // Calculate height gradient (0 at base, 1 at tip)
                float heightGradient = saturate(input.positionOS.y);
                output.heightGradient = heightGradient;
                output.density = density;

                // Get instance scale from matrix
                float3 scale = float3(
                    length(instanceMatrix._m00_m10_m20),
                    length(instanceMatrix._m01_m11_m21),
                    length(instanceMatrix._m02_m12_m22)
                );

                // Wind animation (only for LOD0)
                if (_LODIndex == 0)
                {
                    // Height-based deformation with smoothstep
                    float deformHeight = input.positionOS.y * scale.y + 0.2;
                    float smoothDeformation = smoothstep(0.0, 1.0, deformHeight);

                    // Sample wind noise
                    float2 windUV = positionWS.xz + _WindDirection.xy * _Time.y * _WindSpeed;
                    float2 windNoise = Unity_SimpleNoise(windUV, _WindNoiseScale);
                    windNoise = windNoise * 2.0 - 1.0; // Remap to -1 to 1

                    // Apply wind distortion
                    float2 windDir = normalize(_WindDirection.xy);
                    float distortion = smoothDeformation * windNoise.x;
                    positionWS.xz += distortion * _WindStrength * windDir;
                    positionWS.y += abs(distortion) * _WindStrength * 0.1; // Slight vertical movement
                }

                // Transform normal
                float3x3 normalMatrix = (float3x3)instanceMatrix;
                float3 normalWS = normalize(mul(normalMatrix, input.normalOS));

                // Calculate distance fade
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

                // Fog
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Distance fade alpha test
                clip(input.fadeAlpha - 0.01);

                // Gradient color from base to tip
                // AO at very bottom, then base color blending to tip
                float aoFactor = saturate(input.heightGradient * 4.0); // AO fades quickly
                float3 baseToTip = lerp(_BaseColor.rgb, _TipColor.rgb, input.heightGradient);
                float3 grassColor = lerp(_AOColor.rgb, baseToTip, aoFactor);

                // Density-based color variation (denser = slightly darker/richer)
                grassColor *= lerp(0.9, 1.1, input.density);

                // Lighting
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                float3 normal = normalize(input.normalWS);

                // Wrap lighting for softer grass look
                float NdotL = dot(normal, lightDir) * 0.5 + 0.5;
                NdotL = NdotL * NdotL;

                // Ambient and diffuse
                float3 ambient = SampleSH(normal);
                float3 diffuse = mainLight.color * NdotL;

                // Combine lighting with minimum brightness
                float3 lighting = ambient + diffuse;
                lighting = max(lighting, _MinBrightness);

                // Apply shadow (if enabled)
                #ifdef _MAIN_LIGHT_SHADOWS
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                    float shadow = MainLightRealtimeShadow(shadowCoord);
                    shadow = lerp(_ShadowBrightness, 1.0, shadow);
                    lighting *= shadow;
                #endif

                float3 finalColor = grassColor * lighting;

                // Apply fog
                finalColor = MixFog(finalColor, input.fogFactor);

                // Output with fade alpha for soft edges
                return half4(finalColor, input.fadeAlpha);
            }
            ENDHLSL
        }

        // Shadow caster pass (LOD0 only)
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

                // Skip shadow rendering for LOD1 and LOD2
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

        // Depth pass for proper depth sorting
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
