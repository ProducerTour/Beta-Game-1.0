Shader "CreatorWorld/ProceduralGrass"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.1, 0.3, 0.1, 1)
        _TipColor ("Tip Color", Color) = (0.4, 0.6, 0.2, 1)
        _BladeWidth ("Blade Width", Range(0.01, 0.1)) = 0.03
        _BladeHeight ("Blade Height", Range(0.1, 1.5)) = 0.5
        _BendAmount ("Bend Amount", Range(0, 1)) = 0.3
        _WindStrength ("Wind Strength", Range(0, 2)) = 1
        _AmbientOcclusion ("Ambient Occlusion", Range(0, 1)) = 0.5
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
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

            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Grass blade data from compute shader
            struct GrassData
            {
                float3 position;
                float height;
                float3 normal;
                float rotation;
                float bend;
                float2 wind;
                uint lodLevel;
            };

            StructuredBuffer<GrassData> _GrassDataBuffer;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float _BladeWidth;
                float _BladeHeight;
                float _BendAmount;
                float _WindStrength;
                float _AmbientOcclusion;
                float _Cutoff;
                float2 _WindDirection;
                float _WindFrequency;
            CBUFFER_END

            // GPU-side wind calculation using Perlin-like noise
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // Smoothstep

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float2 CalculateWind(float3 worldPos)
            {
                float2 windSample = worldPos.xz * _WindFrequency + _WindDirection * _Time.y * 0.5;
                float windNoise = noise(windSample);
                return normalize(_WindDirection) * windNoise * _WindStrength;
            }

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float ao : TEXCOORD4;
            };

            // Rotation matrix around Y axis
            float3x3 RotateY(float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float3x3(
                    c, 0, s,
                    0, 1, 0,
                    -s, 0, c
                );
            }

            // Rotation matrix around X axis (for bending)
            float3x3 RotateX(float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float3x3(
                    1, 0, 0,
                    0, c, -s,
                    0, s, c
                );
            }

            // Convert triangle vertex ID to strip vertex ID
            // 5 triangles = 15 vertices, maps to 7 unique positions
            static const uint triangleToStrip[15] = {
                0, 1, 2,  // Triangle 0
                2, 1, 3,  // Triangle 1
                2, 3, 4,  // Triangle 2
                4, 3, 5,  // Triangle 3
                4, 5, 6   // Triangle 4
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Get grass instance data
                GrassData grass = _GrassDataBuffer[IN.instanceID];

                // Convert from triangle vertex to strip vertex
                uint triVertIndex = IN.vertexID % 15;
                uint stripVertIndex = triangleToStrip[triVertIndex];

                // 7 unique vertices: 0-1 (bottom), 2-3, 4-5, 6 (top point)
                // heightPercent: 0=bottom, 1=top
                float heightPercent = float(stripVertIndex / 2) / 3.0; // 0, 0.33, 0.67, 1.0
                float side = (stripVertIndex % 2 == 0) ? -1.0 : 1.0;

                // Top vertex (6) is centered
                if (stripVertIndex == 6)
                {
                    side = 0.0;
                    heightPercent = 1.0;
                }

                // Base vertex position
                float width = _BladeWidth * (1.0 - heightPercent * 0.7); // Taper toward tip
                float height = grass.height * _BladeHeight * heightPercent;

                float3 localPos = float3(side * width, height, 0);

                // Apply bend (cubic bezier approximation)
                float bendFactor = pow(heightPercent, 2) * (grass.bend + _BendAmount);
                float3x3 bendMatrix = RotateX(bendFactor);
                localPos = mul(bendMatrix, localPos);

                // Apply wind (calculated on GPU for performance)
                float2 wind = CalculateWind(grass.position);
                float windEffect = heightPercent * heightPercent;
                localPos.x += wind.x * windEffect;
                localPos.z += wind.y * windEffect;

                // Apply rotation
                float3x3 rotMatrix = RotateY(grass.rotation);
                localPos = mul(rotMatrix, localPos);

                // World position
                float3 worldPos = grass.position + localPos;

                // Calculate normal (simplified - pointing outward from blade)
                float3 worldNormal = normalize(grass.normal + float3(side * 0.3, 0.5, 0));
                // Blend with terrain normal at base
                worldNormal = normalize(lerp(grass.normal, worldNormal, heightPercent));

                OUT.positionWS = worldPos;
                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.normalWS = worldNormal;
                OUT.uv = float2(side * 0.5 + 0.5, heightPercent);

                // Fog
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);

                // Ambient occlusion (darker at base)
                OUT.ao = lerp(1.0 - _AmbientOcclusion, 1.0, heightPercent);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Color gradient from base to tip
                half4 color = lerp(_BaseColor, _TipColor, IN.uv.y);

                // Apply ambient occlusion
                color.rgb *= IN.ao;

                // Simple lighting
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(IN.normalWS, mainLight.direction));
                float3 lighting = mainLight.color * NdotL + unity_AmbientSky.rgb;

                color.rgb *= lighting;

                // Apply fog
                color.rgb = MixFog(color.rgb, IN.fogFactor);

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
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct GrassData
            {
                float3 position;
                float height;
                float3 normal;
                float rotation;
                float bend;
                float2 wind;
                uint lodLevel;
            };

            StructuredBuffer<GrassData> _GrassDataBuffer;

            float _BladeWidth;
            float _BladeHeight;
            float _BendAmount;
            float _WindStrength;
            float2 _WindDirection;
            float _WindFrequency;
            float3 _LightDirection;

            // GPU-side wind (same as main pass)
            float hash_shadow(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise_shadow(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash_shadow(i);
                float b = hash_shadow(i + float2(1.0, 0.0));
                float c = hash_shadow(i + float2(0.0, 1.0));
                float d = hash_shadow(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float2 CalculateWindShadow(float3 worldPos)
            {
                float2 windSample = worldPos.xz * _WindFrequency + _WindDirection * _Time.y * 0.5;
                float windNoise = noise_shadow(windSample);
                return normalize(_WindDirection) * windNoise * _WindStrength;
            }

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float3x3 RotateY(float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float3x3(c, 0, s, 0, 1, 0, -s, 0, c);
            }

            float3x3 RotateX(float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float3x3(1, 0, 0, 0, c, -s, 0, s, c);
            }

            // Triangle to strip mapping (same as main pass)
            static const uint shadowTriToStrip[15] = {
                0, 1, 2, 2, 1, 3, 2, 3, 4, 4, 3, 5, 4, 5, 6
            };

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;

                GrassData grass = _GrassDataBuffer[IN.instanceID];

                // Convert from triangle vertex to strip vertex
                uint triVertIndex = IN.vertexID % 15;
                uint stripVertIndex = shadowTriToStrip[triVertIndex];

                float heightPercent = float(stripVertIndex / 2) / 3.0;
                float side = (stripVertIndex % 2 == 0) ? -1.0 : 1.0;

                if (stripVertIndex == 6)
                {
                    side = 0.0;
                    heightPercent = 1.0;
                }

                float width = _BladeWidth * (1.0 - heightPercent * 0.7);
                float height = grass.height * _BladeHeight * heightPercent;

                float3 localPos = float3(side * width, height, 0);

                float bendFactor = pow(heightPercent, 2) * (grass.bend + _BendAmount);
                localPos = mul(RotateX(bendFactor), localPos);

                // GPU wind calculation
                float2 wind = CalculateWindShadow(grass.position);
                float windEffect = heightPercent * heightPercent;
                localPos.x += wind.x * windEffect;
                localPos.z += wind.y * windEffect;

                localPos = mul(RotateY(grass.rotation), localPos);

                float3 worldPos = grass.position + localPos;

                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(worldPos, grass.normal, _LightDirection));

                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
