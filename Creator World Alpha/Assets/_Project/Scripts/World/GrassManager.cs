using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using CreatorWorld.Config;
using CreatorWorld.Core;

namespace CreatorWorld.World
{
    /// <summary>
    /// Advanced grass rendering system with LOD, GPU culling, and biome integration.
    /// Generates grass per chunk using TerrainGenerator density queries.
    /// </summary>
    public class GrassManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ChunkManager chunkManager;
        [SerializeField] private Camera mainCamera;

        [Header("Settings")]
        [SerializeField] private GrassSettings settings;

        [Header("Rendering")]
        [SerializeField] private Material grassMaterial;
        [SerializeField] private int grassLayer = 0;

        [Header("Compute Shader")]
        [SerializeField] private ComputeShader cullComputeShader;

        // Runtime settings (can be modified by GraphicsManager)
        private int runtimeInstancesPerChunk;
        private float runtimeMaxViewDistance;
        private bool runtimeCastShadows;

        // Per-chunk grass data
        private Dictionary<Vector2Int, GrassChunkData> grassChunks = new Dictionary<Vector2Int, GrassChunkData>();

        // Shared buffers for LOD meshes
        private ComputeBuffer lod0VertexBuffer;
        private ComputeBuffer lod1VertexBuffer;
        private ComputeBuffer lod2VertexBuffer;

        // Material property block
        private MaterialPropertyBlock materialPropertyBlock;

        // Shader property IDs
        private static readonly int TransformBufferID = Shader.PropertyToID("_TransformBuffer");
        private static readonly int VisibleBufferID = Shader.PropertyToID("_VisibleBuffer");
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private static readonly int TipColorID = Shader.PropertyToID("_TipColor");
        private static readonly int AOColorID = Shader.PropertyToID("_AOColor");
        private static readonly int WindStrengthID = Shader.PropertyToID("_WindStrength");
        private static readonly int WindSpeedID = Shader.PropertyToID("_WindSpeed");
        private static readonly int WindDirectionID = Shader.PropertyToID("_WindDirection");
        private static readonly int WindNoiseScaleID = Shader.PropertyToID("_WindNoiseScale");
        private static readonly int MaxViewDistanceID = Shader.PropertyToID("_MaxViewDistance");
        private static readonly int FadeStartID = Shader.PropertyToID("_FadeStart");
        private static readonly int FadeEndID = Shader.PropertyToID("_FadeEnd");
        private static readonly int CameraPositionID = Shader.PropertyToID("_CameraPosition");
        private static readonly int LOD1ThresholdID = Shader.PropertyToID("_LOD1Threshold");
        private static readonly int LOD2ThresholdID = Shader.PropertyToID("_LOD2Threshold");

        // Grass instance data structure (matches compute shader)
        private struct GrassInstance
        {
            public Matrix4x4 trs;
            public float density;

            public static int Size => sizeof(float) * 17; // 16 floats for matrix + 1 for density
        }

        // Sub-chunk data for culling
        private struct SubChunk
        {
            public Vector3 center;
            public Vector3 extents;
            public uint startIndex;
            public uint count;

            public static int Size => sizeof(float) * 6 + sizeof(uint) * 2;
        }

        private class GrassChunkData
        {
            public Vector2Int Coordinate;
            public ComputeBuffer TransformBuffer;
            public ComputeBuffer ArgsBuffer;
            public ComputeBuffer SubChunkBuffer;
            public int InstanceCount;
            public int SubChunkCount;
            public Bounds RenderBounds;
            public bool IsReady;

            // LOD-specific args buffers
            public ComputeBuffer ArgsBufferLOD0;
            public ComputeBuffer ArgsBufferLOD1;
            public ComputeBuffer ArgsBufferLOD2;

            public void Release()
            {
                TransformBuffer?.Release();
                TransformBuffer?.Dispose();
                ArgsBuffer?.Release();
                ArgsBuffer?.Dispose();
                SubChunkBuffer?.Release();
                SubChunkBuffer?.Dispose();
                ArgsBufferLOD0?.Release();
                ArgsBufferLOD0?.Dispose();
                ArgsBufferLOD1?.Release();
                ArgsBufferLOD1?.Dispose();
                ArgsBufferLOD2?.Release();
                ArgsBufferLOD2?.Dispose();

                TransformBuffer = null;
                ArgsBuffer = null;
                SubChunkBuffer = null;
                ArgsBufferLOD0 = null;
                ArgsBufferLOD1 = null;
                ArgsBufferLOD2 = null;
                IsReady = false;
            }
        }

        private void Awake()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            materialPropertyBlock = new MaterialPropertyBlock();

            // Initialize runtime settings from GrassSettings
            if (settings != null)
            {
                runtimeInstancesPerChunk = settings.instancesPerChunk;
                runtimeMaxViewDistance = settings.maxViewDistance;
                runtimeCastShadows = settings.castShadows;
            }
        }

        private void Start()
        {
            // Subscribe to graphics settings changes
            GraphicsManager.OnGrassQualityChanged += OnGrassQualityChanged;
            GraphicsManager.OnSettingsChanged += OnGraphicsSettingsChanged;

            // Setup LOD vertex buffers
            SetupLODBuffers();
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            GraphicsManager.OnGrassQualityChanged -= OnGrassQualityChanged;
            GraphicsManager.OnSettingsChanged -= OnGraphicsSettingsChanged;

            // Release all chunk data
            foreach (var kvp in grassChunks)
            {
                kvp.Value.Release();
            }
            grassChunks.Clear();

            // Release LOD buffers
            lod0VertexBuffer?.Release();
            lod1VertexBuffer?.Release();
            lod2VertexBuffer?.Release();
        }

        private void Update()
        {
            if (mainCamera == null || grassMaterial == null || settings == null)
                return;

            UpdateShaderGlobals();
            RenderVisibleGrass();
        }

        private void SetupLODBuffers()
        {
            if (settings == null) return;

            // Upload mesh vertices to GPU buffers for LOD switching
            if (settings.lod0Mesh != null)
            {
                Vector3[] vertices = settings.lod0Mesh.vertices;
                lod0VertexBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
                lod0VertexBuffer.SetData(vertices);
            }

            if (settings.lod1Mesh != null)
            {
                Vector3[] vertices = settings.lod1Mesh.vertices;
                lod1VertexBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
                lod1VertexBuffer.SetData(vertices);
            }

            if (settings.lod2Mesh != null)
            {
                Vector3[] vertices = settings.lod2Mesh.vertices;
                lod2VertexBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
                lod2VertexBuffer.SetData(vertices);
            }
        }

        private void UpdateShaderGlobals()
        {
            if (settings == null) return;

            // Update material properties
            grassMaterial.SetColor(BaseColorID, settings.baseColor);
            grassMaterial.SetColor(TipColorID, settings.tipColor);
            grassMaterial.SetColor(AOColorID, settings.aoColor);
            grassMaterial.SetFloat(WindStrengthID, settings.windStrength);
            grassMaterial.SetFloat(WindSpeedID, settings.windSpeed);
            grassMaterial.SetVector(WindDirectionID, new Vector4(settings.windDirection.x, settings.windDirection.y, 0, 0));
            grassMaterial.SetFloat(WindNoiseScaleID, settings.windNoiseScale);
            grassMaterial.SetFloat(MaxViewDistanceID, runtimeMaxViewDistance);
            grassMaterial.SetFloat(FadeStartID, settings.fadeStart);
            grassMaterial.SetFloat(FadeEndID, settings.fadeEnd);
            grassMaterial.SetVector(CameraPositionID, mainCamera.transform.position);
            grassMaterial.SetFloat(LOD1ThresholdID, settings.lod1Threshold);
            grassMaterial.SetFloat(LOD2ThresholdID, settings.lod2Threshold);

            // Set global LOD buffers
            if (lod0VertexBuffer != null)
                Shader.SetGlobalBuffer("_LOD0Vertices", lod0VertexBuffer);
            if (lod1VertexBuffer != null)
                Shader.SetGlobalBuffer("_LOD1Vertices", lod1VertexBuffer);
            if (lod2VertexBuffer != null)
                Shader.SetGlobalBuffer("_LOD2Vertices", lod2VertexBuffer);
        }

        /// <summary>
        /// Generate grass for a chunk when it loads.
        /// Called by ChunkManager via OnChunkReady event.
        /// </summary>
        public void GenerateGrassForChunk(Chunk chunk)
        {
            if (chunk == null || settings == null) return;

            Vector2Int coord = chunk.Coordinate;

            // Skip if already generated
            if (grassChunks.ContainsKey(coord))
                return;

            int seed = chunkManager.WorldSeed;
            int chunkSize = chunkManager.ChunkSize;
            Vector3 chunkWorldPos = chunk.transform.position;

            // Use deterministic seeded RNG per chunk
            System.Random rng = new System.Random(seed + coord.x * 10007 + coord.y * 7919);

            // Calculate sub-chunk layout
            int subChunkSize = settings.subChunkSize;
            int subChunksPerSide = Mathf.CeilToInt((float)chunkSize / subChunkSize);
            int totalSubChunks = subChunksPerSide * subChunksPerSide;

            // Organize instances by sub-chunk
            List<GrassInstance>[] subChunkInstances = new List<GrassInstance>[totalSubChunks];
            for (int i = 0; i < totalSubChunks; i++)
            {
                subChunkInstances[i] = new List<GrassInstance>();
            }

            // Generate grass blade positions
            int targetInstances = runtimeInstancesPerChunk;
            for (int i = 0; i < targetInstances; i++)
            {
                float localX = (float)rng.NextDouble() * chunkSize;
                float localZ = (float)rng.NextDouble() * chunkSize;

                float worldX = chunkWorldPos.x + localX;
                float worldZ = chunkWorldPos.z + localZ;

                // Query grass density from terrain generator
                float density = TerrainGenerator.GetGrassDensity(worldX, worldZ, seed);

                // Skip if density is below threshold
                if (density < settings.minDensityThreshold)
                    continue;

                // Probability check based on density
                if ((float)rng.NextDouble() > density)
                    continue;

                // Get terrain height
                float height = TerrainGenerator.GetHeightWithRivers(worldX, worldZ, seed);

                // Skip if underwater
                if (height < TerrainGenerator.WaterLevel + 0.1f)
                    continue;

                Vector3 position = new Vector3(worldX, height, worldZ);

                // Random rotation around Y axis
                float rotationY = (float)rng.NextDouble() * 360f;
                Quaternion rotation = Quaternion.Euler(0, rotationY, 0);

                // Scale based on density
                float scaleT = density * (1f - settings.densityScaleInfluence) +
                               (float)rng.NextDouble() * settings.densityScaleInfluence;
                Vector3 scale = Vector3.Lerp(settings.scaleMin, settings.scaleMax, scaleT);

                // Align to terrain normal
                Vector3 normal = TerrainGenerator.GetNormalAt(worldX, worldZ, seed);
                Quaternion normalRotation = Quaternion.FromToRotation(Vector3.up, normal);
                rotation = normalRotation * rotation;

                // Determine which sub-chunk this instance belongs to
                int subX = Mathf.FloorToInt(localX / subChunkSize);
                int subZ = Mathf.FloorToInt(localZ / subChunkSize);
                subX = Mathf.Clamp(subX, 0, subChunksPerSide - 1);
                subZ = Mathf.Clamp(subZ, 0, subChunksPerSide - 1);
                int subIndex = subZ * subChunksPerSide + subX;

                GrassInstance instance = new GrassInstance
                {
                    trs = Matrix4x4.TRS(position, rotation, scale),
                    density = density
                };

                subChunkInstances[subIndex].Add(instance);
            }

            // Flatten instances and build sub-chunk metadata
            List<GrassInstance> allInstances = new List<GrassInstance>();
            List<SubChunk> subChunks = new List<SubChunk>();

            for (int sz = 0; sz < subChunksPerSide; sz++)
            {
                for (int sx = 0; sx < subChunksPerSide; sx++)
                {
                    int subIndex = sz * subChunksPerSide + sx;
                    List<GrassInstance> instances = subChunkInstances[subIndex];

                    // Calculate sub-chunk bounds
                    float subWorldX = chunkWorldPos.x + sx * subChunkSize + subChunkSize / 2f;
                    float subWorldZ = chunkWorldPos.z + sz * subChunkSize + subChunkSize / 2f;
                    float subWorldY = chunkWorldPos.y + 50f; // Approximate center height

                    SubChunk subChunk = new SubChunk
                    {
                        center = new Vector3(subWorldX, subWorldY, subWorldZ),
                        extents = new Vector3(subChunkSize / 2f, 50f, subChunkSize / 2f),
                        startIndex = (uint)allInstances.Count,
                        count = (uint)instances.Count
                    };

                    subChunks.Add(subChunk);
                    allInstances.AddRange(instances);
                }
            }

            // Skip empty chunks
            if (allInstances.Count == 0)
                return;

            // Create chunk data
            GrassChunkData chunkData = new GrassChunkData
            {
                Coordinate = coord,
                InstanceCount = allInstances.Count,
                SubChunkCount = subChunks.Count,
                RenderBounds = new Bounds(
                    chunkWorldPos + new Vector3(chunkSize / 2f, 50f, chunkSize / 2f),
                    new Vector3(chunkSize, 100f, chunkSize)
                )
            };

            // Create GPU buffers
            // Transform buffer
            chunkData.TransformBuffer = new ComputeBuffer(allInstances.Count, GrassInstance.Size);
            chunkData.TransformBuffer.SetData(allInstances.ToArray());

            // Sub-chunk buffer
            chunkData.SubChunkBuffer = new ComputeBuffer(subChunks.Count, SubChunk.Size);
            chunkData.SubChunkBuffer.SetData(subChunks.ToArray());

            // Create LOD args buffers
            Mesh lodMesh = GetLODMesh(0);
            if (lodMesh != null)
            {
                chunkData.ArgsBufferLOD0 = CreateArgsBuffer(lodMesh, allInstances.Count);
                chunkData.ArgsBufferLOD1 = CreateArgsBuffer(GetLODMesh(1) ?? lodMesh, allInstances.Count);
                chunkData.ArgsBufferLOD2 = CreateArgsBuffer(GetLODMesh(2) ?? lodMesh, allInstances.Count);
            }

            chunkData.IsReady = true;
            grassChunks[coord] = chunkData;
        }

        private ComputeBuffer CreateArgsBuffer(Mesh mesh, int instanceCount)
        {
            ComputeBuffer buffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            uint[] args = new uint[5]
            {
                mesh.GetIndexCount(0),
                (uint)instanceCount,
                mesh.GetIndexStart(0),
                mesh.GetBaseVertex(0),
                0
            };
            buffer.SetData(args);
            return buffer;
        }

        private Mesh GetLODMesh(int lod)
        {
            if (settings == null) return null;

            return lod switch
            {
                0 => settings.lod0Mesh,
                1 => settings.lod1Mesh ?? settings.lod0Mesh,
                2 => settings.lod2Mesh ?? settings.lod1Mesh ?? settings.lod0Mesh,
                _ => settings.lod0Mesh
            };
        }

        /// <summary>
        /// Remove grass when a chunk unloads.
        /// </summary>
        public void RemoveGrassForChunk(Vector2Int coord)
        {
            if (grassChunks.TryGetValue(coord, out GrassChunkData data))
            {
                data.Release();
                grassChunks.Remove(coord);
            }
        }

        private void RenderVisibleGrass()
        {
            if (grassChunks.Count == 0 || settings == null)
                return;

            Vector3 cameraPos = mainCamera.transform.position;
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);

            foreach (var kvp in grassChunks)
            {
                GrassChunkData data = kvp.Value;
                if (!data.IsReady || data.InstanceCount == 0)
                    continue;

                // Distance culling
                float distance = Vector3.Distance(cameraPos, data.RenderBounds.center);
                if (distance > runtimeMaxViewDistance)
                    continue;

                // Frustum culling
                if (!GeometryUtility.TestPlanesAABB(frustumPlanes, data.RenderBounds))
                    continue;

                // Determine LOD based on distance
                float distanceRatio = distance / runtimeMaxViewDistance;
                int lodIndex = settings.GetLODIndex(distanceRatio);
                Mesh lodMesh = GetLODMesh(lodIndex);
                ComputeBuffer argsBuffer = lodIndex switch
                {
                    0 => data.ArgsBufferLOD0,
                    1 => data.ArgsBufferLOD1,
                    2 => data.ArgsBufferLOD2,
                    _ => data.ArgsBufferLOD0
                };

                if (lodMesh == null || argsBuffer == null)
                    continue;

                // Set per-chunk properties
                materialPropertyBlock.SetBuffer(TransformBufferID, data.TransformBuffer);
                materialPropertyBlock.SetFloat("_DistanceRatio", distanceRatio);
                materialPropertyBlock.SetInt("_LODIndex", lodIndex);

                // Render grass
                ShadowCastingMode shadowMode = (runtimeCastShadows && lodIndex == 0)
                    ? ShadowCastingMode.On
                    : ShadowCastingMode.Off;

                Graphics.DrawMeshInstancedIndirect(
                    lodMesh,
                    0,
                    grassMaterial,
                    data.RenderBounds,
                    argsBuffer,
                    0,
                    materialPropertyBlock,
                    shadowMode,
                    true,
                    grassLayer
                );
            }
        }

        #region Graphics Settings Integration

        private void OnGrassQualityChanged(int newInstanceCount)
        {
            if (newInstanceCount != runtimeInstancesPerChunk)
            {
                runtimeInstancesPerChunk = newInstanceCount;
                RegenerateAllGrass();
            }
        }

        private void OnGraphicsSettingsChanged(GameGraphicsSettings graphicsSettings)
        {
            if (graphicsSettings == null) return;

            runtimeMaxViewDistance = graphicsSettings.grassViewDistance;
            runtimeCastShadows = graphicsSettings.grassShadows;
        }

        /// <summary>
        /// Force regeneration of all grass (called when quality changes)
        /// </summary>
        public void RegenerateAllGrass()
        {
            // Store current chunks
            List<Vector2Int> chunksToRegenerate = new List<Vector2Int>(grassChunks.Keys);

            // Release all current grass
            foreach (var coord in chunksToRegenerate)
            {
                RemoveGrassForChunk(coord);
            }

            // Note: Grass will be regenerated when chunks fire OnChunkReady again
            // For existing chunks, we need to manually trigger regeneration
            Debug.Log($"[GrassManager] Grass quality changed. Cleared {chunksToRegenerate.Count} chunks for regeneration.");
        }

        #endregion

        #region Debug

        private void OnDrawGizmosSelected()
        {
            if (grassChunks == null) return;

            foreach (var kvp in grassChunks)
            {
                if (kvp.Value.IsReady)
                {
                    // Draw chunk bounds
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(kvp.Value.RenderBounds.center, kvp.Value.RenderBounds.size);

                    // Draw instance count label
                    #if UNITY_EDITOR
                    UnityEditor.Handles.Label(
                        kvp.Value.RenderBounds.center + Vector3.up * 20f,
                        $"{kvp.Value.InstanceCount:N0} grass"
                    );
                    #endif
                }
            }
        }

        #endregion
    }
}
