using UnityEngine;
using CreatorWorld.Config;

namespace CreatorWorld.World
{
    /// <summary>
    /// Static terrain generation utilities using layered noise.
    /// Generates Rust-style terrain with beaches, grasslands, mountains.
    /// Call Initialize() with BiomeSettings before use.
    /// </summary>
    public static class TerrainGenerator
    {
        // Settings reference (set via Initialize)
        private static BiomeSettings _settings;

        /// <summary>
        /// Current biome settings. Returns default values if not initialized.
        /// </summary>
        public static BiomeSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    Debug.LogWarning("TerrainGenerator not initialized! Using default values.");
                    return null;
                }
                return _settings;
            }
        }

        /// <summary>
        /// Initialize the terrain generator with biome settings.
        /// Must be called before generating terrain.
        /// </summary>
        public static void Initialize(BiomeSettings settings)
        {
            _settings = settings;
            if (_settings != null)
            {
                Debug.Log($"TerrainGenerator initialized with settings: {settings.name}");
            }
            else
            {
                Debug.LogError("TerrainGenerator initialized with null settings!");
            }
        }

        /// <summary>
        /// Check if terrain generator has been initialized
        /// </summary>
        public static bool IsInitialized => _settings != null;

        // Public accessors for commonly used values (for external code compatibility)
        public static float WaterLevel => _settings != null ? _settings.WaterLevel : 0f;
        public static float MapSize => _settings != null ? _settings.MapSize : 2048f;
        public static float MapCenterX => _settings != null ? _settings.MapCenterX : 1024f;
        public static float MapCenterZ => _settings != null ? _settings.MapCenterZ : 1024f;

        /// <summary>
        /// Get terrain height at world position (base terrain only, no rivers)
        /// </summary>
        public static float GetHeightAt(float x, float z, int seed)
        {
            if (_settings == null) return 0f;

            float height = 0f;

            // Seed offset for variation
            float seedOffsetX = seed * 0.1f;
            float seedOffsetZ = seed * 0.13f;

            // Base terrain (large rolling hills)
            height += SamplePerlin(x + seedOffsetX, z + seedOffsetZ, _settings.BaseScale) * _settings.BaseAmplitude;

            // Detail noise (small bumps)
            height += SamplePerlin(x + seedOffsetX * 2, z + seedOffsetZ * 2, _settings.DetailScale) * _settings.DetailAmplitude;

            // Mountain regions (using domain warping)
            // More permissive mask for taller mountains (threshold 0.1, power 1.5, multiplier 4)
            float mountainMask = SamplePerlin(x * _settings.MountainScale, z * _settings.MountainScale, 1f);
            mountainMask = Mathf.Pow(Mathf.Max(0, mountainMask - 0.1f), 1.5f) * 4f;
            height += mountainMask * _settings.MountainAmplitude;

            // Ridge noise for dramatic peaks
            float ridge = RidgeNoise(x + seedOffsetX, z + seedOffsetZ, _settings.RidgeScale);
            height += ridge * _settings.RidgeAmplitude * mountainMask;

            // Apply height offset to raise terrain (more land above water)
            height += _settings.HeightOffset;

            // Apply island falloff (ocean at map edges)
            height = ApplyIslandFalloff(x, z, height);

            // Flatten beaches near water level
            if (height < 5f && height > -2f)
            {
                float beachFactor = Mathf.InverseLerp(-2f, 5f, height);
                height = Mathf.Lerp(-2f, 5f, Mathf.Pow(beachFactor, 0.5f));
            }

            return height;
        }

        /// <summary>
        /// Apply island falloff to create bounded island with ocean at edges.
        /// Uses distance from map center with noise for natural coastline.
        /// </summary>
        private static float ApplyIslandFalloff(float x, float z, float height)
        {
            if (_settings == null) return height;

            // Distance from map center
            float dx = x - _settings.MapCenterX;
            float dz = z - _settings.MapCenterZ;
            float distFromCenter = Mathf.Sqrt(dx * dx + dz * dz);

            // Add noise to break up the circular edge for natural coastline
            float edgeNoise = SamplePerlin(x * 0.003f, z * 0.003f, 1f) * 100f;
            float adjustedDist = distFromCenter + edgeNoise;

            if (adjustedDist < _settings.IslandFalloffStart)
            {
                // Inside core island - no falloff
                return height;
            }
            else if (adjustedDist > _settings.IslandFalloffEnd)
            {
                // Beyond edge - deep water
                return _settings.WaterLevel - _settings.IslandFalloffDepth;
            }
            else
            {
                // In falloff zone - smooth transition to ocean
                float t = Mathf.InverseLerp(_settings.IslandFalloffStart, _settings.IslandFalloffEnd, adjustedDist);
                t = Mathf.SmoothStep(0f, 1f, t);

                // Lerp from current height toward underwater
                float targetHeight = _settings.WaterLevel - _settings.IslandFalloffDepth * t;
                return Mathf.Lerp(height, targetHeight, t);
            }
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
        /// Uses LATITUDE (Z-axis) for biome zones plus altitude overrides.
        /// South = Desert (sand), Center = Grasslands, North = Snow.
        /// High altitude peaks get snow regardless of latitude.
        /// </summary>
        public static Color GetBiomeWeights(float x, float z, int seed)
        {
            if (_settings == null) return new Color(0f, 1f, 0f, 0f); // Default to grass

            float height = GetHeightAt(x, z, seed);

            // Subtle noise for natural edge variation
            float noise = Mathf.PerlinNoise(x * 0.008f + seed * 0.1f, z * 0.008f + seed * 0.13f);
            float noiseVariation = (noise - 0.5f) * 2f; // -1 to 1 range

            float sandWeight = 0f;
            float grassWeight = 0f;  // G channel - supports procedural grass
            float rockWeight = 0f;
            float snowWeight = 0f;

            // ============ BEACH OVERRIDE ============
            // Beaches at water level get sand regardless of latitude
            if (height < _settings.WaterLevel + 1.5f)
            {
                return ApplyRiverbankBlend(x, z, 1f, 0f, 0f, 0f);
            }

            // ============ ALTITUDE SNOW OVERRIDE ============
            // High mountains get snow regardless of latitude
            float altitudeSnowFactor = 0f;
            if (height > _settings.SnowAltitudeThreshold)
            {
                altitudeSnowFactor = Mathf.InverseLerp(_settings.SnowAltitudeThreshold, _settings.SnowAltitudeFullThreshold, height);
                altitudeSnowFactor = Mathf.SmoothStep(0f, 1f, altitudeSnowFactor);
            }

            // ============ LATITUDE-BASED BIOME CALCULATION ============
            float desertFactor = 0f;    // Desert influence (south)
            float grassFactor = 0f;     // Grassland influence (center)
            float snowFactor = 0f;      // Snow influence (north) - from latitude

            if (z < _settings.DesertZoneEnd)
            {
                // Pure desert zone (south)
                desertFactor = 1f;
            }
            else if (z < _settings.DesertTransitionEnd)
            {
                // Desert-to-grass transition
                float t = Mathf.InverseLerp(_settings.DesertZoneEnd, _settings.DesertTransitionEnd, z);
                t = Mathf.SmoothStep(0f, 1f, t);
                t = Mathf.Clamp01(t + noiseVariation * 0.15f);
                desertFactor = 1f - t;
                grassFactor = t;
            }
            else if (z < _settings.GrassZoneEnd)
            {
                // Pure grassland zone (center)
                grassFactor = 1f;
            }
            else if (z < _settings.GrassToSnowEnd)
            {
                // Grass-to-snow transition
                float t = Mathf.InverseLerp(_settings.GrassZoneEnd, _settings.GrassToSnowEnd, z);
                t = Mathf.SmoothStep(0f, 1f, t);
                t = Mathf.Clamp01(t + noiseVariation * 0.15f);
                grassFactor = 1f - t;
                snowFactor = t;
            }
            else
            {
                // Pure snow zone (north)
                snowFactor = 1f;
            }

            // ============ COMBINE ALTITUDE SNOW WITH LATITUDE ============
            // Altitude snow overrides latitude biomes
            snowFactor = Mathf.Max(snowFactor, altitudeSnowFactor);

            // Reduce other biomes proportionally when altitude snow kicks in
            if (altitudeSnowFactor > 0f)
            {
                desertFactor *= (1f - altitudeSnowFactor);
                grassFactor *= (1f - altitudeSnowFactor);
            }

            // ============ HEIGHT-BASED ROCK BLENDING ============
            // Higher elevations blend to rock (before snow altitude)
            float rockFromHeight = 0f;

            if (height > _settings.RockBlendStart && height < _settings.SnowAltitudeThreshold)
            {
                rockFromHeight = Mathf.InverseLerp(_settings.RockBlendStart, _settings.RockBlendEnd, height);
                rockFromHeight = Mathf.SmoothStep(0f, 1f, rockFromHeight);
                rockFromHeight = Mathf.Clamp01(rockFromHeight + noiseVariation * 0.2f);
            }

            // ============ APPLY BIOME WEIGHTS ============

            // DESERT: Sand + Rock (NO grass!)
            if (desertFactor > 0f)
            {
                float desertRockFactor = Mathf.Clamp01(height / 30f); // More rock at higher desert elevations
                desertRockFactor = Mathf.Max(desertRockFactor, rockFromHeight);

                sandWeight += desertFactor * (1f - desertRockFactor);
                rockWeight += desertFactor * desertRockFactor;
            }

            // GRASSLAND: Grass + Rock at height
            if (grassFactor > 0f)
            {
                grassWeight += grassFactor * (1f - rockFromHeight);
                rockWeight += grassFactor * rockFromHeight;
            }

            // SNOW: Snow + some rock at lower snow areas
            if (snowFactor > 0f)
            {
                float snowRockFactor = 0f;
                if (height < 40f)
                {
                    // Lower snow areas have more rock showing through
                    snowRockFactor = Mathf.InverseLerp(40f, 20f, height) * 0.3f;
                }
                snowWeight += snowFactor * (1f - snowRockFactor);
                rockWeight += snowFactor * snowRockFactor;
            }

            // ============ NORMALIZE WEIGHTS ============
            float total = sandWeight + grassWeight + rockWeight + snowWeight;
            if (total > 0.001f)
            {
                sandWeight /= total;
                grassWeight /= total;
                rockWeight /= total;
                snowWeight /= total;
            }

            return ApplyRiverbankBlend(x, z, sandWeight, grassWeight, rockWeight, snowWeight);
        }

        /// <summary>
        /// Apply riverbank sand/gravel blending to biome weights
        /// </summary>
        private static Color ApplyRiverbankBlend(float x, float z, float sand, float grass, float rock, float snow)
        {
            float riverCarve = RiverGenerator.GetRiverCarveDepth(x, z);
            if (riverCarve > 0.1f)
            {
                float riverbankBlend = Mathf.Clamp01(riverCarve / 2f);
                // Blend toward sand on riverbanks
                sand = Mathf.Lerp(sand, 1f, riverbankBlend);
                grass *= (1f - riverbankBlend);
                rock *= (1f - riverbankBlend);
                snow *= (1f - riverbankBlend);

                // Renormalize
                float total = sand + grass + rock + snow;
                if (total > 0.001f)
                {
                    sand /= total;
                    grass /= total;
                    rock /= total;
                    snow /= total;
                }
            }

            return new Color(sand, grass, rock, snow);
        }

        /// <summary>
        /// Get biome type at world position (updated for latitude system)
        /// </summary>
        public static BiomeType GetBiomeAt(float x, float z, int seed)
        {
            if (_settings == null) return BiomeType.Grassland;

            float height = GetHeightAt(x, z, seed);

            // Height-based overrides first
            if (height < _settings.WaterLevel) return BiomeType.Ocean;
            if (height < 3f) return BiomeType.Beach;
            if (height > _settings.SnowAltitudeFullThreshold) return BiomeType.Snow;
            if (height > 80f) return BiomeType.Mountain;

            // Latitude-based biomes (Z-axis)
            if (z < _settings.DesertZoneEnd) return BiomeType.Desert;
            if (z < _settings.DesertTransitionEnd)
            {
                // Transition zone - could be either
                float t = Mathf.InverseLerp(_settings.DesertZoneEnd, _settings.DesertTransitionEnd, z);
                return t < 0.5f ? BiomeType.Desert : BiomeType.Grassland;
            }
            if (z < _settings.GrassZoneEnd) return BiomeType.Grassland;
            if (z < _settings.GrassToSnowEnd)
            {
                float t = Mathf.InverseLerp(_settings.GrassZoneEnd, _settings.GrassToSnowEnd, z);
                return t < 0.5f ? BiomeType.Grassland : BiomeType.Snow;
            }

            return BiomeType.Snow;
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

            // No trees on sand/beach/desert (R channel) or snow (A channel)
            // Desert zones have high sand weight, so this catches them
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

        /// <summary>
        /// Get grass blade density at position (0-1) for procedural grass spawning.
        /// Used by GPU grass instancer for biome-aware placement.
        /// </summary>
        public static float GetGrassDensity(float x, float z, int seed)
        {
            if (_settings == null) return 0f;

            Color biomeWeights = GetBiomeWeights(x, z, seed);
            float slope = GetSlopeAt(x, z, seed);
            float height = GetHeightAt(x, z, seed);

            // Base density from grass biome weight
            float density = biomeWeights.g;

            // Add noise variation for natural clumping
            float noise = (SamplePerlin(x * 0.1f, z * 0.1f, 1f) + 1f) * 0.5f;
            density *= 0.5f + noise * 0.5f;

            // Reduce on slopes (grass doesn't grow well on steep terrain)
            density *= 1f - Mathf.Clamp01(slope / 35f);

            // Exclusions
            if (height < _settings.WaterLevel + 0.5f) density = 0f;  // No grass underwater/beaches
            if (biomeWeights.r > 0.5f) density = 0f;       // No grass on sand
            if (biomeWeights.b > 0.6f) density *= 0.2f;    // Less grass on rock
            if (biomeWeights.a > 0.2f) density = 0f;       // No grass on snow
            if (IsInRiver(x, z)) density = 0f;             // No grass in rivers

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
