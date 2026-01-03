using UnityEngine;

namespace CreatorWorld.World
{
    /// <summary>
    /// Static terrain generation utilities using layered noise.
    /// Generates Rust-style terrain with beaches, grasslands, mountains.
    /// </summary>
    public static class TerrainGenerator
    {
        // Noise settings
        private const float BaseScale = 0.005f;
        private const float DetailScale = 0.02f;
        private const float MountainScale = 0.002f;
        private const float RidgeScale = 0.008f;

        // Height settings
        private const float BaseAmplitude = 30f;
        private const float DetailAmplitude = 5f;
        private const float MountainAmplitude = 100f;
        private const float RidgeAmplitude = 40f;

        // Water level
        public const float WaterLevel = 0f;

        /// <summary>
        /// Get terrain height at world position (base terrain only, no rivers)
        /// </summary>
        public static float GetHeightAt(float x, float z, int seed)
        {
            float height = 0f;

            // Seed offset for variation
            float seedOffsetX = seed * 0.1f;
            float seedOffsetZ = seed * 0.13f;

            // Base terrain (large rolling hills)
            height += SamplePerlin(x + seedOffsetX, z + seedOffsetZ, BaseScale) * BaseAmplitude;

            // Detail noise (small bumps)
            height += SamplePerlin(x + seedOffsetX * 2, z + seedOffsetZ * 2, DetailScale) * DetailAmplitude;

            // Mountain regions (using domain warping)
            // More permissive mask for taller mountains (threshold 0.1, power 1.5, multiplier 4)
            float mountainMask = SamplePerlin(x * MountainScale, z * MountainScale, 1f);
            mountainMask = Mathf.Pow(Mathf.Max(0, mountainMask - 0.1f), 1.5f) * 4f;
            height += mountainMask * MountainAmplitude;

            // Ridge noise for dramatic peaks
            float ridge = RidgeNoise(x + seedOffsetX, z + seedOffsetZ, RidgeScale);
            height += ridge * RidgeAmplitude * mountainMask;

            // Flatten beaches near water level
            if (height < 5f && height > -2f)
            {
                float beachFactor = Mathf.InverseLerp(-2f, 5f, height);
                height = Mathf.Lerp(-2f, 5f, Mathf.Pow(beachFactor, 0.5f));
            }

            return height;
        }

        /// <summary>
        /// Get terrain height with river carving applied.
        /// Use this for final terrain mesh generation.
        /// </summary>
        public static float GetHeightWithRivers(float x, float z, int seed)
        {
            float baseHeight = GetHeightAt(x, z, seed);

            // Apply river carving
            float riverCarve = RiverGenerator.GetRiverCarveDepth(x, z);
            if (riverCarve > 0f)
            {
                baseHeight -= riverCarve;

                // Ensure rivers don't go below water level
                baseHeight = Mathf.Max(baseHeight, WaterLevel - 2f);
            }

            return baseHeight;
        }

        /// <summary>
        /// Check if position is in a river channel
        /// </summary>
        public static bool IsInRiver(float x, float z)
        {
            return RiverGenerator.GetRiverCarveDepth(x, z) > 0.5f;
        }

        /// <summary>
        /// Get biome weights for texture splatting (R=sand, G=dirt/grass, B=rock, A=snow)
        /// Uses height-based blending with subtle noise for natural edge variation.
        /// Also applies sand/gravel along riverbanks.
        /// </summary>
        public static Color GetBiomeWeights(float x, float z, int seed)
        {
            float height = GetHeightAt(x, z, seed);

            // Subtle noise for natural edge variation (smaller scale = smoother transitions)
            float noise = Mathf.PerlinNoise(x * 0.008f + seed * 0.1f, z * 0.008f + seed * 0.13f);
            float noiseVariation = (noise - 0.5f) * 2f; // -1 to 1 range

            float sandWeight = 0f;
            float dirtWeight = 0f;  // This is the "grass" channel - now used for dirt
            float rockWeight = 0f;
            float snowWeight = 0f;

            // Check for river proximity (riverbanks get sand/gravel)
            float riverCarve = RiverGenerator.GetRiverCarveDepth(x, z);
            float riverbankBlend = 0f;
            if (riverCarve > 0.1f)
            {
                // In or near river - blend to sand/gravel
                riverbankBlend = Mathf.Clamp01(riverCarve / 2f); // Full sand in river center
            }

            // Fixed height thresholds (noise only affects blend, not thresholds)
            // Beach: 0-3m (right at water level)
            // Transition: 3-8m (sand fades to dirt)
            // Dirt/Grass zone: 8-70m (main playable area with procedural grass blades)
            // Rock blend: 55-90m (dirt fades to rock)
            // Snow blend: 90-130m (rock fades to snow)
            // Pure snow: 130m+

            if (height < 3f)
            {
                // Pure beach/sand near water
                sandWeight = 1f;
            }
            else if (height < 8f)
            {
                // Sand to dirt transition (tight band)
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 8f, height));
                // Add subtle noise to transition
                t = Mathf.Clamp01(t + noiseVariation * 0.15f);
                sandWeight = 1f - t;
                dirtWeight = t;
            }
            else if (height < 55f)
            {
                // Pure dirt zone (procedural grass grows here)
                dirtWeight = 1f;

                // Apply riverbank sand blend
                if (riverbankBlend > 0f)
                {
                    sandWeight = riverbankBlend;
                    dirtWeight = 1f - riverbankBlend;
                }
            }
            else if (height < 90f)
            {
                // Dirt to rock transition
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(55f, 90f, height));
                // Add noise for rocky outcrops
                t = Mathf.Clamp01(t + noiseVariation * 0.2f);
                dirtWeight = 1f - t;
                rockWeight = t;
            }
            else if (height < 130f)
            {
                // Rock to snow transition
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(90f, 130f, height));
                t = Mathf.Clamp01(t + noiseVariation * 0.1f);
                rockWeight = 1f - t;
                snowWeight = t;
            }
            else
            {
                // Pure snow peaks
                snowWeight = 1f;
            }

            return new Color(sandWeight, dirtWeight, rockWeight, snowWeight);
        }

        /// <summary>
        /// Get biome type at world position
        /// </summary>
        public static BiomeType GetBiomeAt(float x, float z, int seed)
        {
            float height = GetHeightAt(x, z, seed);
            float moisture = SamplePerlin(x * 0.003f, z * 0.003f, 1f);

            // Height-based biomes
            if (height < WaterLevel) return BiomeType.Ocean;
            if (height < 3f) return BiomeType.Beach;
            if (height > 120f) return BiomeType.Snow;
            if (height > 80f) return BiomeType.Mountain;

            // Moisture-based biomes for mid elevations
            if (moisture > 0.6f) return BiomeType.Forest;
            if (moisture < 0.3f) return BiomeType.Desert;

            return BiomeType.Grassland;
        }

        /// <summary>
        /// Get terrain normal at world position
        /// </summary>
        public static Vector3 GetNormalAt(float x, float z, int seed, float sampleDistance = 1f)
        {
            float hL = GetHeightAt(x - sampleDistance, z, seed);
            float hR = GetHeightAt(x + sampleDistance, z, seed);
            float hD = GetHeightAt(x, z - sampleDistance, seed);
            float hU = GetHeightAt(x, z + sampleDistance, seed);

            Vector3 normal = new Vector3(hL - hR, 2f * sampleDistance, hD - hU);
            return normal.normalized;
        }

        /// <summary>
        /// Get slope angle at world position (0-90 degrees)
        /// </summary>
        public static float GetSlopeAt(float x, float z, int seed)
        {
            Vector3 normal = GetNormalAt(x, z, seed);
            return Mathf.Acos(normal.y) * Mathf.Rad2Deg;
        }

        #region Noise Functions

        private static float SamplePerlin(float x, float z, float scale)
        {
            // Unity's Perlin returns 0-1, we want -1 to 1
            return Mathf.PerlinNoise(x * scale, z * scale) * 2f - 1f;
        }

        private static float RidgeNoise(float x, float z, float scale)
        {
            // Ridge noise: 1 - abs(noise) creates sharp ridges
            float noise = SamplePerlin(x, z, scale);
            return 1f - Mathf.Abs(noise);
        }

        private static float FBM(float x, float z, float scale, int octaves, float persistence, float lacunarity)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = scale;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                total += SamplePerlin(x * frequency, z * frequency, 1f) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return total / maxValue;
        }

        #endregion

        #region Decoration Placement

        /// <summary>
        /// Check if a tree can be placed at this position (uses unified biome weights)
        /// </summary>
        public static bool CanPlaceTree(float x, float z, int seed)
        {
            float height = GetHeightAt(x, z, seed);
            float slope = GetSlopeAt(x, z, seed);
            Color biomeWeights = GetBiomeWeights(x, z, seed);

            // Trees need flat-ish ground, above water, below snow
            if (height < 2f || height > 80f) return false;
            if (slope > 30f) return false;

            // No trees on sand/beach (R channel) or snow (A channel)
            if (biomeWeights.r > 0.5f) return false;
            if (biomeWeights.a > 0.3f) return false;

            // No trees in rivers
            if (IsInRiver(x, z)) return false;

            // Use noise for base density
            float density = SamplePerlin(x * 0.05f, z * 0.05f, 1f);

            // Boost density in grass/dirt areas (G channel)
            density += biomeWeights.g * 0.4f;

            // Reduce density in rocky areas (B channel)
            density -= biomeWeights.b * 0.3f;

            return density > 0.3f;
        }

        /// <summary>
        /// Get tree density multiplier at position (0-1) for biome-aware spawning
        /// </summary>
        public static float GetTreeDensity(float x, float z, int seed)
        {
            Color biomeWeights = GetBiomeWeights(x, z, seed);
            float slope = GetSlopeAt(x, z, seed);

            // Base density from noise
            float density = (SamplePerlin(x * 0.02f, z * 0.02f, 1f) + 1f) * 0.5f;

            // Multiply by grass weight (trees love grass areas)
            density *= biomeWeights.g;

            // Reduce on slopes
            density *= 1f - Mathf.Clamp01(slope / 45f);

            // No trees on sand, rock dominant, or snow
            if (biomeWeights.r > 0.6f) density = 0f;
            if (biomeWeights.b > 0.7f) density *= 0.2f;
            if (biomeWeights.a > 0.3f) density = 0f;

            // No trees in rivers
            if (IsInRiver(x, z)) density = 0f;

            return Mathf.Clamp01(density);
        }

        /// <summary>
        /// Check if a rock can be placed at this position (uses unified biome weights)
        /// </summary>
        public static bool CanPlaceRock(float x, float z, int seed)
        {
            float height = GetHeightAt(x, z, seed);
            Color biomeWeights = GetBiomeWeights(x, z, seed);

            // Rocks can be anywhere above water
            if (height < 1f) return false;

            // No rocks in rivers
            if (IsInRiver(x, z)) return false;

            float density = SamplePerlin(x * 0.03f + 100f, z * 0.03f + 100f, 1f);

            // Boost rock density in rocky/mountain areas (B channel)
            density += biomeWeights.b * 0.5f;

            // Some rocks on beaches
            if (biomeWeights.r > 0.5f) density += 0.1f;

            return density > 0.4f;
        }

        /// <summary>
        /// Get rock density multiplier at position (0-1) for biome-aware spawning
        /// </summary>
        public static float GetRockDensity(float x, float z, int seed)
        {
            Color biomeWeights = GetBiomeWeights(x, z, seed);

            // Base density from noise
            float density = (SamplePerlin(x * 0.03f + 100f, z * 0.03f + 100f, 1f) + 1f) * 0.5f;

            // Rocks love rocky terrain (B channel)
            density *= 0.3f + biomeWeights.b * 0.7f;

            // Some beach rocks
            density += biomeWeights.r * 0.15f;

            // Fewer rocks on pure grass
            if (biomeWeights.g > 0.8f) density *= 0.4f;

            // No rocks in rivers
            if (IsInRiver(x, z)) density = 0f;

            return Mathf.Clamp01(density);
        }

        #endregion

        #region Lake Detection

        /// <summary>
        /// Check if position is in a lake (terrain depression below water level)
        /// </summary>
        public static bool IsInLake(float x, float z, int seed, out float depth)
        {
            float height = GetHeightAt(x, z, seed);
            depth = 0f;

            // Lakes form in terrain depressions below water level
            if (height < WaterLevel)
            {
                // Check if this is an inland depression (not ocean)
                // Sample surrounding area to detect if inland
                float avgSurrounding = 0f;
                int samples = 0;
                float checkRadius = 50f;

                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 2f / 8f;
                    float sx = x + Mathf.Cos(angle) * checkRadius;
                    float sz = z + Mathf.Sin(angle) * checkRadius;
                    avgSurrounding += GetHeightAt(sx, sz, seed);
                    samples++;
                }
                avgSurrounding /= samples;

                // If surrounding terrain is above water, this is a lake
                if (avgSurrounding > WaterLevel + 5f)
                {
                    depth = WaterLevel - height;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if position is underwater (ocean, river, or lake)
        /// </summary>
        public static bool IsUnderwater(float x, float z, int seed)
        {
            float height = GetHeightWithRivers(x, z, seed);
            return height < WaterLevel;
        }

        #endregion
    }

    public enum BiomeType
    {
        Ocean,
        Beach,
        Grassland,
        Forest,
        Desert,
        Mountain,
        Snow
    }
}
