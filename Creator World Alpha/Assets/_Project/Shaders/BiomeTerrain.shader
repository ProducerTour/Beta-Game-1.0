Shader "CreatorWorld/BiomeTerrain"
{
    Properties
    {
        // Texture maps
        [Header(Sand Beach)]
        _SandAlbedo ("Sand Albedo", 2D) = "white" {}
        _SandNormal ("Sand Normal", 2D) = "bump" {}
        _SandRoughness ("Sand Roughness", 2D) = "gray" {}
        _SandTiling ("Sand Tiling", Float) = 4.0

        [Header(Grass)]
        _GrassAlbedo ("Grass Albedo", 2D) = "white" {}
        _GrassNormal ("Grass Normal", 2D) = "bump" {}
        _GrassRoughness ("Grass Roughness", 2D) = "gray" {}
        _GrassTiling ("Grass Tiling", Float) = 4.0
        _GrassColor ("Grass Tint", Color) = (0.3, 0.5, 0.2, 1)

        [Header(Rock Mountain)]
        _RockAlbedo ("Rock Albedo", 2D) = "gray" {}
        _RockNormal ("Rock Normal", 2D) = "bump" {}
        _RockRoughness ("Rock Roughness", 2D) = "gray" {}
        _RockTiling ("Rock Tiling", Float) = 2.0

        [Header(Snow)]
        _SnowAlbedo ("Snow Albedo", 2D) = "white" {}
        _SnowNormal ("Snow Normal", 2D) = "bump" {}
        _SnowTiling ("Snow Tiling", Float) = 4.0
        _SnowColor ("Snow Tint", Color) = (0.95, 0.97, 1.0, 1)

        [Header(Blending)]
        _BlendSharpness ("Blend Sharpness", Range(0.1, 10)) = 2.0
        _SlopeThreshold ("Slope Rock Threshold", Range(0, 1)) = 0.5
        _SlopeBlend ("Slope Blend Range", Range(0.01, 0.5)) = 0.1

        [Header(Triplanar)]
        _TriplanarSharpness ("Triplanar Sharpness", Range(1, 8)) = 4.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Textures
            TEXTURE2D(_SandAlbedo); SAMPLER(sampler_SandAlbedo);
            TEXTURE2D(_SandNormal); SAMPLER(sampler_SandNormal);
            TEXTURE2D(_SandRoughness); SAMPLER(sampler_SandRoughness);

            TEXTURE2D(_GrassAlbedo); SAMPLER(sampler_GrassAlbedo);
            TEXTURE2D(_GrassNormal); SAMPLER(sampler_GrassNormal);
            TEXTURE2D(_GrassRoughness); SAMPLER(sampler_GrassRoughness);

            TEXTURE2D(_RockAlbedo); SAMPLER(sampler_RockAlbedo);
            TEXTURE2D(_RockNormal); SAMPLER(sampler_RockNormal);
            TEXTURE2D(_RockRoughness); SAMPLER(sampler_RockRoughness);

            TEXTURE2D(_SnowAlbedo); SAMPLER(sampler_SnowAlbedo);
            TEXTURE2D(_SnowNormal); SAMPLER(sampler_SnowNormal);

            CBUFFER_START(UnityPerMaterial)
                float _SandTiling;
                float _GrassTiling;
                float _RockTiling;
                float _SnowTiling;
                float4 _GrassColor;
                float4 _SnowColor;
                float _BlendSharpness;
                float _SlopeThreshold;
                float _SlopeBlend;
                float _TriplanarSharpness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; // Biome weights: R=sand, G=grass, B=rock, A=snow
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float4 biomeWeights : TEXCOORD4;
                float fogFactor : TEXCOORD5;
                float4 shadowCoord : TEXCOORD6;
            };

            // Triplanar sampling for steep slopes
            float3 SampleTriplanar(TEXTURE2D_PARAM(tex, samp), float3 worldPos, float3 worldNormal, float tiling)
            {
                float3 blending = pow(abs(worldNormal), _TriplanarSharpness);
                blending /= dot(blending, 1.0);

                float3 xProj = SAMPLE_TEXTURE2D(tex, samp, worldPos.zy * tiling).rgb * blending.x;
                float3 yProj = SAMPLE_TEXTURE2D(tex, samp, worldPos.xz * tiling).rgb * blending.y;
                float3 zProj = SAMPLE_TEXTURE2D(tex, samp, worldPos.xy * tiling).rgb * blending.z;

                return xProj + yProj + zProj;
            }

            float3 SampleTriplanarNormal(TEXTURE2D_PARAM(tex, samp), float3 worldPos, float3 worldNormal, float tiling)
            {
                float3 blending = pow(abs(worldNormal), _TriplanarSharpness);
                blending /= dot(blending, 1.0);

                float3 xNorm = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, worldPos.zy * tiling));
                float3 yNorm = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, worldPos.xz * tiling));
                float3 zNorm = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, worldPos.xy * tiling));

                // Swizzle normals for each projection
                xNorm = float3(xNorm.xy + worldNormal.zy, abs(xNorm.z) * worldNormal.x);
                yNorm = float3(yNorm.xy + worldNormal.xz, abs(yNorm.z) * worldNormal.y);
                zNorm = float3(zNorm.xy + worldNormal.xy, abs(zNorm.z) * worldNormal.z);

                return normalize(xNorm * blending.x + yNorm * blending.y + zNorm * blending.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w);
                output.uv = input.uv;
                output.biomeWeights = input.color;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                output.shadowCoord = GetShadowCoord(posInputs);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Normalize biome weights
                float4 weights = input.biomeWeights;
                float totalWeight = weights.r + weights.g + weights.b + weights.a;
                if (totalWeight > 0.001)
                    weights /= totalWeight;
                else
                    weights = float4(0, 1, 0, 0); // Default to grass

                // Calculate slope factor for rock blending on steep terrain
                float slope = 1.0 - saturate(dot(input.normalWS, float3(0, 1, 0)));
                float slopeRock = smoothstep(_SlopeThreshold - _SlopeBlend, _SlopeThreshold + _SlopeBlend, slope);

                // Adjust weights based on slope (steep = more rock)
                float originalRock = weights.b;
                weights.b = lerp(weights.b, 1.0, slopeRock * 0.7);
                float rockIncrease = weights.b - originalRock;
                float sandGrassSum = max(weights.r + weights.g, 0.001);
                weights.r -= rockIncrease * (weights.r / sandGrassSum);
                weights.g -= rockIncrease * (weights.g / sandGrassSum);
                weights = saturate(weights);

                // Sample textures using triplanar for rock, UV for others
                float3 worldPos = input.positionWS * 0.1; // Scale world pos for tiling

                // Sand (beach)
                float3 sandAlbedo = SAMPLE_TEXTURE2D(_SandAlbedo, sampler_SandAlbedo, input.uv * _SandTiling).rgb;
                float3 sandNormal = UnpackNormal(SAMPLE_TEXTURE2D(_SandNormal, sampler_SandNormal, input.uv * _SandTiling));
                float sandRoughness = SAMPLE_TEXTURE2D(_SandRoughness, sampler_SandRoughness, input.uv * _SandTiling).r;

                // Grass
                float3 grassAlbedo = SAMPLE_TEXTURE2D(_GrassAlbedo, sampler_GrassAlbedo, input.uv * _GrassTiling).rgb * _GrassColor.rgb;
                float3 grassNormal = UnpackNormal(SAMPLE_TEXTURE2D(_GrassNormal, sampler_GrassNormal, input.uv * _GrassTiling));
                float grassRoughness = SAMPLE_TEXTURE2D(_GrassRoughness, sampler_GrassRoughness, input.uv * _GrassTiling).r;

                // Rock (triplanar for cliffs)
                float3 rockAlbedo = SampleTriplanar(TEXTURE2D_ARGS(_RockAlbedo, sampler_RockAlbedo), input.positionWS, input.normalWS, _RockTiling * 0.1);
                float3 rockNormal = SampleTriplanarNormal(TEXTURE2D_ARGS(_RockNormal, sampler_RockNormal), input.positionWS, input.normalWS, _RockTiling * 0.1);
                float rockRoughness = SampleTriplanar(TEXTURE2D_ARGS(_RockRoughness, sampler_RockRoughness), input.positionWS, input.normalWS, _RockTiling * 0.1).r;

                // Snow
                float3 snowAlbedo = SAMPLE_TEXTURE2D(_SnowAlbedo, sampler_SnowAlbedo, input.uv * _SnowTiling).rgb * _SnowColor.rgb;
                float3 snowNormal = UnpackNormal(SAMPLE_TEXTURE2D(_SnowNormal, sampler_SnowNormal, input.uv * _SnowTiling));
                float snowRoughness = 0.3; // Snow is smooth-ish

                // Blend all layers
                float3 finalAlbedo = sandAlbedo * weights.r + grassAlbedo * weights.g + rockAlbedo * weights.b + snowAlbedo * weights.a;
                float3 finalNormal = normalize(sandNormal * weights.r + grassNormal * weights.g + rockNormal * weights.b + snowNormal * weights.a);
                float finalRoughness = sandRoughness * weights.r + grassRoughness * weights.g + rockRoughness * weights.b + snowRoughness * weights.a;

                // Build TBN matrix for normal mapping
                float3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                float3x3 TBN = float3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                float3 worldNormal = normalize(mul(finalNormal, TBN));

                // Lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = worldNormal;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(worldNormal);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalAlbedo;
                surfaceData.metallic = 0;
                surfaceData.specular = 0.04;
                surfaceData.smoothness = 1.0 - finalRoughness;
                surfaceData.normalTS = float3(0, 0, 1);
                surfaceData.occlusion = 1;
                surfaceData.alpha = 1;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
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

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
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

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
