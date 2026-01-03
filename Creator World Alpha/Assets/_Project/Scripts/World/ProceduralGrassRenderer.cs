using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using System.Collections;

namespace CreatorWorld.World
{
    /// <summary>
    /// GPU-instanced procedural grass renderer.
    /// Based on Ghost of Tsushima GDC talk techniques.
    /// Automatically follows camera and integrates with TerrainGenerator.
    /// </summary>
    public class ProceduralGrassRenderer : MonoBehaviour
    {
        [Header("Grass Settings")]
        [SerializeField] private Material grassMaterial;
        [SerializeField] private int grassPerMeter = 8;
        [SerializeField] private float renderRadius = 50f;
        [SerializeField] private float maxRenderDistance = 100f;

        [Header("Grass Appearance")]
        [SerializeField] private Color baseColor = new Color(0.1f, 0.35f, 0.1f);
        [SerializeField] private Color tipColor = new Color(0.45f, 0.6f, 0.25f);
        [SerializeField] private float bladeWidth = 0.04f;
        [SerializeField] private float bladeHeight = 0.5f;
        [SerializeField] private float bendAmount = 0.3f;

        [Header("Wind")]
        [SerializeField] private Vector2 windDirection = new Vector2(1f, 0.3f);
        [SerializeField] private float windStrength = 0.5f;
        [SerializeField] private float windFrequency = 0.1f;

        [Header("Terrain Integration")]
        [Tooltip("Leave empty to auto-detect from ChunkManager. Only set manually for testing.")]
        [SerializeField] private int terrainSeedOverride = 0;
        [SerializeField] private float maxSlope = 40f;
        [Tooltip("Minimum grass biome weight to place any grass (0-1)")]
        [SerializeField] private float minGrassWeight = 0.05f;  // Lowered to catch biome edges
        [Tooltip("Grass weight at full density")]
        [SerializeField] private float fullDensityWeight = 0.3f;  // Lowered for fuller coverage

        [Header("Slope-Rock Blending (matches BiomeTerrain shader)")]
        [Tooltip("Slope threshold where rock starts to dominate (matches shader _SlopeThreshold)")]
        [SerializeField] private float slopeRockThreshold = 0.5f;
        [Tooltip("Blend range around threshold (matches shader _SlopeBlend)")]
        [SerializeField] private float slopeRockBlend = 0.1f;

        [Header("Natural Variation")]
        [Tooltip("Noise scale for subtle density variation (doesn't create bare patches)")]
        [SerializeField] private float variationNoiseScale = 0.03f;
        [Tooltip("How much noise affects density (0=none, 1=full)")]
        [SerializeField] private float variationStrength = 0.2f;
        [Tooltip("Height variation scale")]
        [SerializeField] private float heightNoiseScale = 0.05f;
        [Tooltip("Height variation strength")]
        [SerializeField] private float heightVariation = 0.3f;

        // Cached seed from ChunkManager
        private int activeSeed;
        private ChunkManager chunkManager;

        // Noise offsets for variation
        private float noiseOffsetX;
        private float noiseOffsetZ;

        [Header("Camera Following")]
        [Tooltip("Automatically follow the main camera")]
        [SerializeField] private bool followCamera = true;
        [Tooltip("Distance camera must move before regenerating grass (higher = less regeneration)")]
        [SerializeField] private float regenerateDistance = 30f;  // Increased for less frequent regeneration

        [Header("Performance")]
        [SerializeField] private int maxGrassBlades = 500000;
        [SerializeField] private ComputeShader cullingShader;
        [SerializeField] private bool enableGPUCulling = true;

        [Header("LOD Distances")]
        [SerializeField] private float lod0Distance = 30f;
        [SerializeField] private float lod1Distance = 60f;

        [Header("Debug")]
        [SerializeField] private bool showDebug = false;

        // Grass data structure (must match shader)
        private struct GrassData
        {
            public Vector3 position;   // 3 floats = 12 bytes
            public float height;       // 1 float = 4 bytes
            public Vector3 normal;     // 3 floats = 12 bytes
            public float rotation;     // 1 float = 4 bytes
            public float bend;         // 1 float = 4 bytes
            public Vector2 wind;       // 2 floats = 8 bytes
            public uint lodLevel;      // 1 uint = 4 bytes
                                       // Total: 11 floats + 1 uint = 48 bytes

            public static int Size => sizeof(float) * 11 + sizeof(uint); // 48 bytes
        }

        // Buffers
        private ComputeBuffer grassDataBuffer;
        private ComputeBuffer argsBuffer;
        private List<GrassData> grassDataList;

        // GPU Culling buffers
        private ComputeBuffer visibleGrassBuffer;
        private ComputeBuffer visibleArgsBuffer;
        private ComputeBuffer counterBuffer;
        private int cullingKernel;
        private bool cullingInitialized;

        // Async generation
        private bool isGenerating;
        private Coroutine activeGeneration;
        private List<GrassData> pendingGrassData;

        // State
        private int currentGrassCount;
        private bool isInitialized;
        private Camera mainCamera;
        private Bounds renderBounds;
        private Vector3 lastGenerationCenter;
        private Vector3 grassCenter;

        // Shader property IDs (cached for performance)
        private static readonly int GrassDataBufferID = Shader.PropertyToID("_GrassDataBuffer");
        private static readonly int WindDirectionID = Shader.PropertyToID("_WindDirection");
        private static readonly int WindFrequencyID = Shader.PropertyToID("_WindFrequency");

        // Frustum planes for GPU culling
        private Plane[] frustumPlanes = new Plane[6];
        private Vector4[] frustumPlanesVector = new Vector4[6];

        private void Start()
        {
            mainCamera = Camera.main;
            grassDataList = new List<GrassData>();

            // Find ChunkManager to get the world seed
            chunkManager = FindFirstObjectByType<ChunkManager>();
            if (chunkManager != null)
            {
                activeSeed = chunkManager.WorldSeed;
                Debug.Log($"[GrassRenderer] Using seed {activeSeed} from ChunkManager");
            }
            else if (terrainSeedOverride != 0)
            {
                activeSeed = terrainSeedOverride;
                Debug.Log($"[GrassRenderer] Using override seed {activeSeed}");
            }
            else
            {
                activeSeed = 12345; // Default fallback
                Debug.LogWarning("[GrassRenderer] No ChunkManager found and no seed override set. Using default seed 12345");
            }

            // Initialize noise offsets based on seed for consistent variation
            System.Random seedRng = new System.Random(activeSeed);
            noiseOffsetX = (float)seedRng.NextDouble() * 10000f;
            noiseOffsetZ = (float)seedRng.NextDouble() * 10000f;

            // Wait a frame for camera to be positioned, then initialize
            StartCoroutine(DelayedInitialize());
        }

        private IEnumerator DelayedInitialize()
        {
            // Wait for end of frame to ensure all other Start() methods have run
            // and camera is in its correct position
            yield return new WaitForEndOfFrame();
            yield return null; // Extra frame for safety

            // Re-acquire camera in case it wasn't ready
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            // Initialize grass center based on camera position
            if (followCamera && mainCamera != null)
            {
                grassCenter = new Vector3(mainCamera.transform.position.x, 0, mainCamera.transform.position.z);
                Debug.Log($"[GrassRenderer] Camera found at {mainCamera.transform.position}, grass center: {grassCenter}");
            }
            else
            {
                grassCenter = transform.position;
                Debug.Log($"[GrassRenderer] No camera, using transform position: {grassCenter}");
            }

            lastGenerationCenter = grassCenter;
            Initialize();
        }

        private void Initialize()
        {
            if (grassMaterial == null)
            {
                Debug.LogError("[GrassRenderer] Missing grass material!");
                return;
            }

            // Generate grass data on CPU using TerrainGenerator
            GenerateGrassData();

            if (grassDataList.Count == 0)
            {
                Debug.LogWarning("[GrassRenderer] No grass generated at center " + grassCenter + ". Check biome at this location.");
                return;
            }

            // Create GPU buffer
            grassDataBuffer = new ComputeBuffer(grassDataList.Count, GrassData.Size);
            grassDataBuffer.SetData(grassDataList);

            // Indirect args buffer
            argsBuffer = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
            uint[] args = new uint[] { 15, (uint)grassDataList.Count, 0, 0 };
            argsBuffer.SetData(args);

            currentGrassCount = grassDataList.Count;
            renderBounds = new Bounds(grassCenter, Vector3.one * maxRenderDistance * 2);

            Debug.Log($"[GrassRenderer] Initialized with {currentGrassCount:N0} grass blades around {grassCenter}");
            isInitialized = true;

            // Initialize GPU culling if available
            InitializeGPUCulling();
        }

        private void InitializeGPUCulling()
        {
            if (!enableGPUCulling || cullingShader == null)
            {
                cullingInitialized = false;
                if (enableGPUCulling && cullingShader == null)
                {
                    Debug.LogWarning("[GrassRenderer] GPU culling enabled but no compute shader assigned. Using CPU path.");
                }
                return;
            }

            cullingKernel = cullingShader.FindKernel("CullGrass");

            // Create visible grass buffer (AppendStructuredBuffer)
            visibleGrassBuffer = new ComputeBuffer(maxGrassBlades, GrassData.Size, ComputeBufferType.Append);

            // Create args buffer for DrawProceduralIndirect with visible count
            visibleArgsBuffer = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);

            cullingInitialized = true;
            Debug.Log("[GrassRenderer] GPU frustum culling initialized");
        }

        private void GenerateGrassData()
        {
            grassDataList.Clear();

            Vector3 center = grassCenter;
            float spacing = 1f / grassPerMeter;
            int gridSize = Mathf.CeilToInt(renderRadius * 2 * grassPerMeter);

            // Debug counters
            int totalChecked = 0;
            int skippedBiome = 0;
            int skippedDensity = 0;
            int skippedSlope = 0;
            int skippedWater = 0;
            int skippedRiver = 0;
            int skippedLake = 0;

            for (int z = 0; z < gridSize && grassDataList.Count < maxGrassBlades; z++)
            {
                for (int x = 0; x < gridSize && grassDataList.Count < maxGrassBlades; x++)
                {
                    totalChecked++;

                    // Base grid position
                    float baseX = center.x - renderRadius + x * spacing;
                    float baseZ = center.z - renderRadius + z * spacing;

                    // Position-based deterministic randomness (consistent per world location)
                    float posHash = PositionHash(baseX, baseZ);

                    // Natural jitter using layered noise for organic distribution
                    float jitterX = (SampleNoise(baseX * 0.5f, baseZ * 0.5f, 100f) - 0.5f) * spacing * 0.9f;
                    float jitterZ = (SampleNoise(baseX * 0.5f + 50f, baseZ * 0.5f + 50f, 100f) - 0.5f) * spacing * 0.9f;

                    float worldX = baseX + jitterX;
                    float worldZ = baseZ + jitterZ;

                    // Sample terrain height using TerrainGenerator
                    float terrainHeight = TerrainGenerator.GetHeightAt(worldX, worldZ, activeSeed);

                    // Skip if below water
                    if (terrainHeight < TerrainGenerator.WaterLevel + 0.5f)
                    {
                        skippedWater++;
                        continue;
                    }

                    // Skip if in river
                    if (TerrainGenerator.IsInRiver(worldX, worldZ))
                    {
                        skippedRiver++;
                        continue;
                    }

                    // Skip if in lake
                    if (LakeGenerator.IsInLake(worldX, worldZ, out _))
                    {
                        skippedLake++;
                        continue;
                    }

                    // Get biome weights for gradual density fade
                    Color biomeWeights = TerrainGenerator.GetBiomeWeights(worldX, worldZ, activeSeed);
                    float grassWeight = biomeWeights.g; // Green channel = grass weight

                    // Skip if grass weight is below minimum threshold
                    if (grassWeight < minGrassWeight)
                    {
                        skippedBiome++;
                        continue;
                    }

                    // Get terrain normal for slope calculation
                    Vector3 normal = TerrainGenerator.GetNormalAt(worldX, worldZ, activeSeed);
                    float slope = TerrainGenerator.GetSlopeAt(worldX, worldZ, activeSeed);

                    // Skip if slope exceeds maximum
                    if (slope > maxSlope)
                    {
                        skippedSlope++;
                        continue;
                    }

                    // === SHADER-MATCHED SLOPE-ROCK BLENDING ===
                    // Match BiomeTerrain shader: steep slopes get more rock, less grass
                    float slopeNormalized = 1f - Mathf.Clamp01(normal.y); // 0=flat, 1=vertical
                    float slopeRockFactor = Mathf.SmoothStep(
                        slopeRockThreshold - slopeRockBlend,
                        slopeRockThreshold + slopeRockBlend,
                        slopeNormalized
                    );
                    // Reduce effective grass weight on steep slopes (matches shader behavior)
                    float effectiveGrassWeight = grassWeight * (1f - slopeRockFactor * 0.7f);

                    // Skip if effective grass weight is too low after slope adjustment
                    if (effectiveGrassWeight < minGrassWeight)
                    {
                        skippedSlope++;
                        continue;
                    }

                    // === SIMPLIFIED DENSITY CALCULATION ===
                    // Primary driver: biome weight (ensures grass fills the dirt/grass texture area)
                    float biomeDensity = Mathf.InverseLerp(minGrassWeight, fullDensityWeight, effectiveGrassWeight);
                    biomeDensity = Mathf.Clamp01(biomeDensity);

                    // Subtle noise variation (additive, not multiplicative - avoids bare patches)
                    float noiseVariation = (SampleNoise(worldX * variationNoiseScale, worldZ * variationNoiseScale, noiseOffsetX) - 0.5f) * 2f;
                    float finalDensity = Mathf.Clamp01(biomeDensity + noiseVariation * variationStrength * 0.3f);

                    // Gentle slope reduction (not aggressive)
                    float slopeFactor = 1f - Mathf.Pow(slope / maxSlope, 3f); // Cubic for gentler falloff
                    finalDensity *= slopeFactor;

                    // Probability check using position-based hash for consistency
                    if (posHash > finalDensity)
                    {
                        skippedDensity++;
                        continue;
                    }

                    // === GRASS PROPERTIES ===
                    float baseGrassHeight = bladeHeight;

                    // Height variation from noise (subtle, natural looking)
                    float heightNoise = SampleNoise(worldX * heightNoiseScale, worldZ * heightNoiseScale, noiseOffsetZ);
                    float heightMod = 1f + (heightNoise - 0.5f) * heightVariation;

                    // Shorter grass on slopes and biome edges
                    float edgeFactor = Mathf.Lerp(0.7f, 1f, biomeDensity);
                    float slopeHeightFactor = Mathf.Lerp(0.6f, 1f, slopeFactor);

                    // Individual variation using position hash
                    float individualVariation = 0.8f + posHash * 0.4f; // 0.8 to 1.2

                    float finalHeight = baseGrassHeight * heightMod * edgeFactor * slopeHeightFactor * individualVariation;

                    // Rotation - cluster grass blades in similar directions for natural look
                    float rotationNoise = SampleNoise(worldX * 0.1f, worldZ * 0.1f, 200f);
                    float baseRotation = rotationNoise * Mathf.PI * 2f;
                    // Add individual variation
                    float rotationOffset = (posHash - 0.5f) * Mathf.PI * 0.5f;
                    float finalRotation = baseRotation + rotationOffset;

                    // Bend amount with subtle variation
                    float bendNoise = SampleNoise(worldX * 0.15f, worldZ * 0.15f, 300f);
                    float finalBend = bendAmount * (0.7f + bendNoise * 0.6f);

                    // Create grass blade data
                    GrassData grass = new GrassData
                    {
                        position = new Vector3(worldX, terrainHeight, worldZ),
                        height = finalHeight,
                        normal = normal,
                        rotation = finalRotation,
                        bend = finalBend,
                        wind = Vector2.zero, // Updated per frame in shader
                        lodLevel = 0
                    };

                    grassDataList.Add(grass);
                }
            }

            Debug.Log($"[GrassRenderer] Generated {grassDataList.Count:N0} grass blades from {totalChecked:N0} checked positions");
            Debug.Log($"[GrassRenderer] Skipped - Biome: {skippedBiome:N0}, Density: {skippedDensity:N0}, Slope: {skippedSlope:N0}, Water: {skippedWater:N0}, River: {skippedRiver:N0}, Lake: {skippedLake:N0}");
            Debug.Log($"[GrassRenderer] Center: {center}, Radius: {renderRadius}, Seed: {activeSeed}");

            if (grassDataList.Count > 0)
            {
                var first = grassDataList[0];
                Debug.Log($"[GrassRenderer] Sample grass at: {first.position}, height: {first.height:F2}");
            }
        }

        /// <summary>
        /// Position-based hash for deterministic randomness (0-1 range)
        /// Same world position always returns same value regardless of when generated
        /// </summary>
        private float PositionHash(float x, float z)
        {
            // Use prime multipliers for good distribution
            int ix = Mathf.FloorToInt(x * 100f);
            int iz = Mathf.FloorToInt(z * 100f);
            int hash = ix * 73856093 ^ iz * 19349663 ^ activeSeed * 83492791;
            hash = (hash >> 13) ^ hash;
            hash = hash * (hash * hash * 15731 + 789221) + 1376312589;
            return (hash & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }

        /// <summary>
        /// Sample Perlin noise with offset (returns 0-1)
        /// </summary>
        private float SampleNoise(float x, float z, float offset)
        {
            return Mathf.PerlinNoise(x + offset + noiseOffsetX, z + offset + noiseOffsetZ);
        }

        private void Update()
        {
            // Check if we need to regenerate grass due to camera movement
            if (followCamera && mainCamera != null)
            {
                Vector3 cameraXZ = new Vector3(mainCamera.transform.position.x, 0, mainCamera.transform.position.z);
                float distFromLastGen = Vector3.Distance(cameraXZ, lastGenerationCenter);

                // Regenerate if moved enough, OR if no grass and we're in a valid area
                bool shouldRegenerate = distFromLastGen > regenerateDistance;

                // If no grass, check if we're now in a valid grass area
                if (!shouldRegenerate && currentGrassCount == 0 && !isGenerating)
                {
                    float terrainHeight = TerrainGenerator.GetHeightAt(cameraXZ.x, cameraXZ.z, activeSeed);
                    Color biomeWeights = TerrainGenerator.GetBiomeWeights(cameraXZ.x, cameraXZ.z, activeSeed);

                    // If we're above water and in a grass biome, try regenerating
                    if (terrainHeight > TerrainGenerator.WaterLevel + 1f && biomeWeights.g > minGrassWeight)
                    {
                        shouldRegenerate = true;
                        Debug.Log($"[GrassRenderer] No grass but valid area detected at height {terrainHeight:F1}, grassWeight {biomeWeights.g:F2}. Regenerating...");
                    }
                }

                if (shouldRegenerate)
                {
                    grassCenter = cameraXZ;
                    lastGenerationCenter = cameraXZ;
                    RegenerateGrass();
                }
            }

            if (!isInitialized) return;

            // Perform GPU culling if enabled
            if (cullingInitialized)
            {
                PerformGPUCulling();
            }

            // Render grass (wind is calculated on GPU for performance)
            RenderGrass();
        }

        private void PerformGPUCulling()
        {
            if (mainCamera == null || grassDataBuffer == null) return;

            // Get camera frustum planes
            GeometryUtility.CalculateFrustumPlanes(mainCamera, frustumPlanes);

            // Convert to Vector4 format for shader
            for (int i = 0; i < 6; i++)
            {
                frustumPlanesVector[i] = new Vector4(
                    frustumPlanes[i].normal.x,
                    frustumPlanes[i].normal.y,
                    frustumPlanes[i].normal.z,
                    frustumPlanes[i].distance
                );
            }

            // Reset visible buffer counter
            visibleGrassBuffer.SetCounterValue(0);

            // Set compute shader parameters
            cullingShader.SetBuffer(cullingKernel, "_InputGrass", grassDataBuffer);
            cullingShader.SetBuffer(cullingKernel, "_VisibleGrass", visibleGrassBuffer);
            cullingShader.SetVectorArray("_FrustumPlanes", frustumPlanesVector);
            cullingShader.SetVector("_CameraPosition", mainCamera.transform.position);
            cullingShader.SetFloat("_LOD0Distance", lod0Distance);
            cullingShader.SetFloat("_LOD1Distance", lod1Distance);
            cullingShader.SetInt("_GrassCount", currentGrassCount);

            // Dispatch compute shader
            int threadGroups = Mathf.CeilToInt(currentGrassCount / 256f);
            cullingShader.Dispatch(cullingKernel, threadGroups, 1, 1);

            // Copy visible count to indirect args
            ComputeBuffer.CopyCount(visibleGrassBuffer, visibleArgsBuffer, 4); // Copy to instanceCount position

            // Set base vertex count (15 vertices per grass blade)
            uint[] args = new uint[] { 15, 0, 0, 0 };
            visibleArgsBuffer.SetData(args);
            ComputeBuffer.CopyCount(visibleGrassBuffer, visibleArgsBuffer, 4);
        }

        private void RegenerateGrass()
        {
            // Don't start new generation if one is in progress
            if (isGenerating) return;

            // Start async generation
            if (activeGeneration != null)
            {
                StopCoroutine(activeGeneration);
            }
            activeGeneration = StartCoroutine(GenerateGrassAsync());
        }

        private IEnumerator GenerateGrassAsync()
        {
            isGenerating = true;

            // Use a temporary list to build new grass data
            pendingGrassData = new List<GrassData>();

            Vector3 center = grassCenter;
            float spacing = 1f / grassPerMeter;
            int gridSize = Mathf.CeilToInt(renderRadius * 2 * grassPerMeter);

            int batchSize = 10000; // Process this many per frame
            int processed = 0;

            for (int z = 0; z < gridSize && pendingGrassData.Count < maxGrassBlades; z++)
            {
                for (int x = 0; x < gridSize && pendingGrassData.Count < maxGrassBlades; x++)
                {
                    // Base grid position
                    float baseX = center.x - renderRadius + x * spacing;
                    float baseZ = center.z - renderRadius + z * spacing;

                    // Position-based deterministic randomness
                    float posHash = PositionHash(baseX, baseZ);

                    // Natural jitter using layered noise
                    float jitterX = (SampleNoise(baseX * 0.5f, baseZ * 0.5f, 100f) - 0.5f) * spacing * 0.9f;
                    float jitterZ = (SampleNoise(baseX * 0.5f + 50f, baseZ * 0.5f + 50f, 100f) - 0.5f) * spacing * 0.9f;

                    float worldX = baseX + jitterX;
                    float worldZ = baseZ + jitterZ;

                    // Sample terrain height
                    float terrainHeight = TerrainGenerator.GetHeightAt(worldX, worldZ, activeSeed);

                    // Skip if below water
                    if (terrainHeight < TerrainGenerator.WaterLevel + 0.5f)
                    {
                        continue;
                    }

                    // Skip if in river or lake
                    if (TerrainGenerator.IsInRiver(worldX, worldZ))
                    {
                        continue;
                    }
                    if (LakeGenerator.IsInLake(worldX, worldZ, out _))
                    {
                        continue;
                    }

                    // Get biome weights
                    Color biomeWeights = TerrainGenerator.GetBiomeWeights(worldX, worldZ, activeSeed);
                    float grassWeight = biomeWeights.g;

                    // Skip if below threshold
                    if (grassWeight < minGrassWeight)
                    {
                        continue;
                    }

                    // Get terrain normal for slope calculation
                    Vector3 normal = TerrainGenerator.GetNormalAt(worldX, worldZ, activeSeed);
                    float slope = TerrainGenerator.GetSlopeAt(worldX, worldZ, activeSeed);

                    // Skip if slope exceeds maximum
                    if (slope > maxSlope)
                    {
                        continue;
                    }

                    // === SHADER-MATCHED SLOPE-ROCK BLENDING ===
                    float slopeNormalized = 1f - Mathf.Clamp01(normal.y);
                    float slopeRockFactor = Mathf.SmoothStep(
                        slopeRockThreshold - slopeRockBlend,
                        slopeRockThreshold + slopeRockBlend,
                        slopeNormalized
                    );
                    float effectiveGrassWeight = grassWeight * (1f - slopeRockFactor * 0.7f);

                    if (effectiveGrassWeight < minGrassWeight)
                    {
                        continue;
                    }

                    // === SIMPLIFIED DENSITY CALCULATION ===
                    float biomeDensity = Mathf.InverseLerp(minGrassWeight, fullDensityWeight, effectiveGrassWeight);
                    biomeDensity = Mathf.Clamp01(biomeDensity);

                    float noiseVariation = (SampleNoise(worldX * variationNoiseScale, worldZ * variationNoiseScale, noiseOffsetX) - 0.5f) * 2f;
                    float finalDensity = Mathf.Clamp01(biomeDensity + noiseVariation * variationStrength * 0.3f);

                    float slopeFactor = 1f - Mathf.Pow(slope / maxSlope, 3f);
                    finalDensity *= slopeFactor;

                    if (posHash > finalDensity)
                    {
                        continue;
                    }

                    // === GRASS PROPERTIES ===
                    float baseGrassHeight = bladeHeight;

                    float heightNoise = SampleNoise(worldX * heightNoiseScale, worldZ * heightNoiseScale, noiseOffsetZ);
                    float heightMod = 1f + (heightNoise - 0.5f) * heightVariation;

                    float edgeFactor = Mathf.Lerp(0.7f, 1f, biomeDensity);
                    float slopeHeightFactor = Mathf.Lerp(0.6f, 1f, slopeFactor);
                    float individualVariation = 0.8f + posHash * 0.4f;
                    float finalHeight = baseGrassHeight * heightMod * edgeFactor * slopeHeightFactor * individualVariation;

                    float rotationNoise = SampleNoise(worldX * 0.1f, worldZ * 0.1f, 200f);
                    float baseRotation = rotationNoise * Mathf.PI * 2f;
                    float rotationOffset = (posHash - 0.5f) * Mathf.PI * 0.5f;
                    float finalRotation = baseRotation + rotationOffset;

                    float bendNoise = SampleNoise(worldX * 0.15f, worldZ * 0.15f, 300f);
                    float finalBend = bendAmount * (0.7f + bendNoise * 0.6f);

                    GrassData grass = new GrassData
                    {
                        position = new Vector3(worldX, terrainHeight, worldZ),
                        height = finalHeight,
                        normal = normal,
                        rotation = finalRotation,
                        bend = finalBend,
                        wind = Vector2.zero,
                        lodLevel = 0
                    };

                    pendingGrassData.Add(grass);

                    processed++;

                    // Yield every batchSize to prevent frame drops
                    if (processed >= batchSize)
                    {
                        processed = 0;
                        yield return null;
                    }
                }
            }

            // Apply the new data
            ApplyGeneratedGrass();
            isGenerating = false;
        }

        private void ApplyGeneratedGrass()
        {
            if (pendingGrassData == null || pendingGrassData.Count == 0)
            {
                isInitialized = false;
                return;
            }

            // Copy to main list
            grassDataList = pendingGrassData;
            pendingGrassData = null;

            // Resize buffer if needed
            if (grassDataBuffer == null || grassDataBuffer.count < grassDataList.Count)
            {
                grassDataBuffer?.Release();
                grassDataBuffer = new ComputeBuffer(grassDataList.Count, GrassData.Size);
            }

            grassDataBuffer.SetData(grassDataList);

            // Update args
            if (argsBuffer == null)
            {
                argsBuffer = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
            }
            uint[] args = new uint[] { 15, (uint)grassDataList.Count, 0, 0 };
            argsBuffer.SetData(args);

            currentGrassCount = grassDataList.Count;
            renderBounds = new Bounds(grassCenter, Vector3.one * maxRenderDistance * 2);
            isInitialized = true;

            Debug.Log($"[GrassRenderer] Async generation complete: {currentGrassCount:N0} blades");
        }

        private void RenderGrass()
        {
            if (grassMaterial == null || grassDataBuffer == null || currentGrassCount == 0)
                return;

            // Update material properties (wind is calculated on GPU)
            grassMaterial.SetColor("_BaseColor", baseColor);
            grassMaterial.SetColor("_TipColor", tipColor);
            grassMaterial.SetFloat("_BladeWidth", bladeWidth);
            grassMaterial.SetFloat("_BladeHeight", bladeHeight);
            grassMaterial.SetFloat("_BendAmount", bendAmount);
            grassMaterial.SetFloat("_WindStrength", windStrength);
            grassMaterial.SetVector(WindDirectionID, windDirection);
            grassMaterial.SetFloat(WindFrequencyID, windFrequency);

            // Choose buffer based on culling mode
            ComputeBuffer bufferToRender = cullingInitialized ? visibleGrassBuffer : grassDataBuffer;
            ComputeBuffer argsToUse = cullingInitialized ? visibleArgsBuffer : argsBuffer;

            grassMaterial.SetBuffer(GrassDataBufferID, bufferToRender);

            // Draw grass
            Graphics.DrawProceduralIndirect(
                grassMaterial,
                renderBounds,
                MeshTopology.Triangles,
                argsToUse,
                0,
                null,
                null,
                ShadowCastingMode.Off,
                true,
                gameObject.layer
            );
        }

        private void OnDestroy()
        {
            grassDataBuffer?.Release();
            argsBuffer?.Release();
            visibleGrassBuffer?.Release();
            visibleArgsBuffer?.Release();
            counterBuffer?.Release();
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebug) return;

            Vector3 center = Application.isPlaying ? grassCenter : transform.position;

            // Draw render area
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(center, new Vector3(renderRadius * 2, 10f, renderRadius * 2));

            // Draw max distance
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, maxRenderDistance);

            // Draw sample grass positions (limit to avoid performance issues)
            if (grassDataList != null && grassDataList.Count > 0)
            {
                Gizmos.color = Color.cyan;
                int step = Mathf.Max(1, grassDataList.Count / 500); // Show max 500 gizmos
                for (int i = 0; i < grassDataList.Count; i += step)
                {
                    var grass = grassDataList[i];
                    Gizmos.DrawLine(grass.position, grass.position + Vector3.up * grass.height * 0.5f);
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            // Show grass generation center
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(grassCenter, 2f);
        }

        // Public methods for runtime adjustment
        public void SetWindDirection(Vector2 direction) => windDirection = direction;
        public void SetWindStrength(float strength) => windStrength = strength;
        public void SetGrassColors(Color baseCol, Color tipCol) { baseColor = baseCol; tipColor = tipCol; }
        public void SetRenderDistance(float distance) { maxRenderDistance = distance; renderBounds = new Bounds(grassCenter, Vector3.one * maxRenderDistance * 2); }
        public void ForceRegenerate() => RegenerateGrass();
        public Vector3 GetGrassCenter() => grassCenter;
        public int GetCurrentGrassCount() => currentGrassCount;
        public bool IsGenerating() => isGenerating;
        public bool IsGPUCullingEnabled() => cullingInitialized;
    }
}
