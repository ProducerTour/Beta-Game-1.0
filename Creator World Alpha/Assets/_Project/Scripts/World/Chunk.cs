using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace CreatorWorld.World
{
    /// <summary>
    /// Individual terrain chunk with mesh, collision, and decorations.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class Chunk : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private Vector2Int coordinate;
        [SerializeField] private int currentLOD = 0;
        [SerializeField] private bool showDebugGizmos = false;

        // Components
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;

        // Data
        private int size;
        private int seed;
        private float[,] heightmap;
        private Mesh[] lodMeshes;

        // Decoration
        private GameObject decorationContainer;
        private GameObject[] treePrefabs;
        private float treeYOffset;
        private GameObject[] rockPrefabs;
        private float rockYOffset;

        // Debug data for visualization
        private List<Vector3> debugTreePositions = new List<Vector3>();
        private List<Vector3> debugRaycastHits = new List<Vector3>();

        public Vector2Int Coordinate => coordinate;
        public int CurrentLOD => currentLOD;

        // Event fired when chunk mesh is ready for grass generation
        public System.Action<Chunk> OnChunkReady;

        public void Initialize(Vector2Int coord, int chunkSize, int worldSeed, Material material, GameObject[] trees = null, float treeOffset = 0f, GameObject[] rocks = null, float rockOffset = 0f)
        {
            coordinate = coord;
            size = chunkSize;
            seed = worldSeed;
            treePrefabs = trees;
            treeYOffset = treeOffset;
            rockPrefabs = rocks;
            rockYOffset = rockOffset;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();

            // Set material
            meshRenderer.material = material;

            // Generate terrain
            GenerateHeightmap();
            GenerateMeshLODs();
            SetLOD(0);

            // Notify listeners that chunk mesh is ready (for grass generation)
            OnChunkReady?.Invoke(this);

            // Generate decorations after physics sync (coroutine waits for collider to be ready)
            StartCoroutine(GenerateDecorationsDelayed());
        }

        private void GenerateHeightmap()
        {
            int resolution = size + 1; // +1 for edge vertices
            heightmap = new float[resolution, resolution];

            Vector3 worldPos = transform.position;

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = worldPos.x + x;
                    float worldZ = worldPos.z + z;
                    // Use river-carved height for terrain mesh
                    heightmap[x, z] = TerrainGenerator.GetHeightWithRivers(worldX, worldZ, seed);
                }
            }
        }

        private void GenerateMeshLODs()
        {
            lodMeshes = new Mesh[3];

            // LOD 0: Full resolution (every vertex)
            lodMeshes[0] = GenerateMesh(1);

            // LOD 1: Half resolution (every 2nd vertex)
            lodMeshes[1] = GenerateMesh(2);

            // LOD 2: Quarter resolution (every 4th vertex)
            lodMeshes[2] = GenerateMesh(4);
        }

        private Mesh GenerateMesh(int step)
        {
            int resolution = (size / step) + 1;
            Vector3[] vertices = new Vector3[resolution * resolution];
            Vector2[] uvs = new Vector2[resolution * resolution];
            Color[] colors = new Color[resolution * resolution]; // Biome weights: R=sand, G=grass, B=rock, A=snow
            int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];

            Vector3 worldPos = transform.position;

            // Generate vertices with biome colors
            int vertIndex = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int heightX = Mathf.Min(x * step, size);
                    int heightZ = Mathf.Min(z * step, size);

                    float height = heightmap[heightX, heightZ];
                    vertices[vertIndex] = new Vector3(x * step, height, z * step);
                    uvs[vertIndex] = new Vector2((float)x / (resolution - 1), (float)z / (resolution - 1));

                    // Calculate world position for biome sampling
                    float wx = worldPos.x + heightX;
                    float wz = worldPos.z + heightZ;

                    // Get biome weights from unified TerrainGenerator
                    colors[vertIndex] = TerrainGenerator.GetBiomeWeights(wx, wz, seed);

                    vertIndex++;
                }
            }

            // Generate triangles
            int triIndex = 0;
            for (int z = 0; z < resolution - 1; z++)
            {
                for (int x = 0; x < resolution - 1; x++)
                {
                    int topLeft = z * resolution + x;
                    int topRight = topLeft + 1;
                    int bottomLeft = (z + 1) * resolution + x;
                    int bottomRight = bottomLeft + 1;

                    // First triangle
                    triangles[triIndex++] = topLeft;
                    triangles[triIndex++] = bottomLeft;
                    triangles[triIndex++] = topRight;

                    // Second triangle
                    triangles[triIndex++] = topRight;
                    triangles[triIndex++] = bottomLeft;
                    triangles[triIndex++] = bottomRight;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = $"Chunk_{coordinate.x}_{coordinate.y}_LOD{step}";
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents(); // Needed for normal mapping
            mesh.RecalculateBounds();

            return mesh;
        }

        public void SetLOD(int lod)
        {
            lod = Mathf.Clamp(lod, 0, lodMeshes.Length - 1);

            if (lod == currentLOD && meshFilter.sharedMesh != null) return;

            currentLOD = lod;
            meshFilter.sharedMesh = lodMeshes[lod];

            // Only use high-detail collision for LOD 0
            if (lod == 0)
            {
                meshCollider.sharedMesh = lodMeshes[0];
            }
        }

        public float GetHeightAt(Vector3 worldPosition)
        {
            // Convert world position to local heightmap position
            Vector3 localPos = worldPosition - transform.position;
            int x = Mathf.Clamp(Mathf.FloorToInt(localPos.x), 0, size - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(localPos.z), 0, size - 1);

            // Bilinear interpolation for smooth height
            float xFrac = localPos.x - x;
            float zFrac = localPos.z - z;

            int x1 = Mathf.Min(x + 1, size);
            int z1 = Mathf.Min(z + 1, size);

            float h00 = heightmap[x, z];
            float h10 = heightmap[x1, z];
            float h01 = heightmap[x, z1];
            float h11 = heightmap[x1, z1];

            float h0 = Mathf.Lerp(h00, h10, xFrac);
            float h1 = Mathf.Lerp(h01, h11, xFrac);

            return Mathf.Lerp(h0, h1, zFrac);
        }

        /// <summary>
        /// Sample heightmap using local coordinates (0 to size)
        /// </summary>
        private float SampleHeightmap(float localX, float localZ)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(localX), 0, size - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(localZ), 0, size - 1);

            float xFrac = localX - x;
            float zFrac = localZ - z;

            int x1 = Mathf.Min(x + 1, size);
            int z1 = Mathf.Min(z + 1, size);

            float h00 = heightmap[x, z];
            float h10 = heightmap[x1, z];
            float h01 = heightmap[x, z1];
            float h11 = heightmap[x1, z1];

            float h0 = Mathf.Lerp(h00, h10, xFrac);
            float h1 = Mathf.Lerp(h01, h11, xFrac);

            return Mathf.Lerp(h0, h1, zFrac);
        }

        /// <summary>
        /// Coroutine that waits for physics to sync before placing decorations.
        /// This ensures the mesh collider is ready for raycasting.
        /// </summary>
        private IEnumerator GenerateDecorationsDelayed()
        {
            // Wait for physics to process the mesh collider
            // This is CRITICAL - raycasting won't work until physics syncs
            yield return new WaitForFixedUpdate();

            // Force physics transforms to sync immediately
            Physics.SyncTransforms();

            GenerateDecorations();
        }

        private void GenerateDecorations()
        {
            decorationContainer = new GameObject("Decorations");
            decorationContainer.transform.parent = transform;
            decorationContainer.transform.localPosition = Vector3.zero;

            // Use seeded random for consistent decoration placement
            System.Random rng = new System.Random(seed + coordinate.x * 1000 + coordinate.y);

            // Clear debug data
            debugTreePositions.Clear();
            debugRaycastHits.Clear();

            // Spawn trees using biome-aware placement
            // More attempts with density-based filtering for natural distribution
            int treeAttempts = 30;
            int treesPlaced = 0;
            int maxTrees = 15;

            for (int i = 0; i < treeAttempts && treesPlaced < maxTrees; i++)
            {
                float localX = (float)rng.NextDouble() * size;
                float localZ = (float)rng.NextDouble() * size;

                // Convert to world position
                float worldX = transform.position.x + localX;
                float worldZ = transform.position.z + localZ;

                // Use biome-aware tree density (returns 0-1)
                float treeDensity = TerrainGenerator.GetTreeDensity(worldX, worldZ, seed);

                // Probability check based on density
                if ((float)rng.NextDouble() > treeDensity)
                {
                    continue; // Skip - not enough density here
                }

                // Raycast from high above to find ground
                Vector3 rayOrigin = new Vector3(worldX, 200f, worldZ);

                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 400f))
                {
                    // Only place on our own chunk's collider
                    if (hit.collider == meshCollider)
                    {
                        float groundHeight = hit.point.y;

                        // Store debug info
                        debugRaycastHits.Add(hit.point);

                        GameObject tree;
                        if (treePrefabs != null && treePrefabs.Length > 0)
                        {
                            // Randomly select a tree prefab from the array
                            GameObject selectedPrefab = treePrefabs[rng.Next(treePrefabs.Length)];
                            tree = Instantiate(selectedPrefab, decorationContainer.transform);
                            tree.name = "Tree";

                            // Use world position from raycast hit
                            Vector3 treeWorldPos = new Vector3(hit.point.x, groundHeight + treeYOffset, hit.point.z);
                            tree.transform.position = treeWorldPos;

                            debugTreePositions.Add(treeWorldPos);

                            // Random rotation for variety
                            float randomYaw = (float)rng.NextDouble() * 360f;
                            tree.transform.rotation = Quaternion.Euler(0, randomYaw, 0);

                            // Random scale variation (80% to 120%)
                            float scaleVariation = 0.8f + (float)rng.NextDouble() * 0.4f;
                            tree.transform.localScale = Vector3.one * scaleVariation;
                        }
                        else
                        {
                            tree = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                            tree.name = "Tree_Placeholder";
                            tree.transform.parent = decorationContainer.transform;
                            tree.transform.position = new Vector3(hit.point.x, groundHeight + treeYOffset, hit.point.z);
                            tree.transform.localScale = new Vector3(1f, 3f, 1f);
                            Destroy(tree.GetComponent<Collider>());

                            debugTreePositions.Add(tree.transform.position);
                        }

                        treesPlaced++;
                    }
                }
            }

            // Spawn rocks using biome-aware placement
            int rockAttempts = 20;
            int rocksPlaced = 0;
            int maxRocks = 10;

            for (int i = 0; i < rockAttempts && rocksPlaced < maxRocks; i++)
            {
                float localX = (float)rng.NextDouble() * size;
                float localZ = (float)rng.NextDouble() * size;

                // Convert to world position
                float worldX = transform.position.x + localX;
                float worldZ = transform.position.z + localZ;

                // Use biome-aware rock density (returns 0-1)
                float rockDensity = TerrainGenerator.GetRockDensity(worldX, worldZ, seed);

                // Probability check based on density
                if ((float)rng.NextDouble() > rockDensity)
                {
                    continue; // Skip - not enough density here
                }

                Vector3 rayOrigin = new Vector3(worldX, 200f, worldZ);

                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 400f))
                {
                    if (hit.collider == meshCollider && hit.point.y > 1f)
                    {
                        GameObject rock;
                        if (rockPrefabs != null && rockPrefabs.Length > 0)
                        {
                            // Randomly select a rock prefab from the array
                            GameObject selectedPrefab = rockPrefabs[rng.Next(rockPrefabs.Length)];
                            rock = Instantiate(selectedPrefab, decorationContainer.transform);
                            rock.name = "Rock";
                            rock.transform.position = new Vector3(hit.point.x, hit.point.y + rockYOffset, hit.point.z);

                            // Random rotation for variety
                            float randomYaw = (float)rng.NextDouble() * 360f;
                            rock.transform.rotation = Quaternion.Euler(0, randomYaw, 0);

                            // Keep prefab's baked scale - don't override
                        }
                        else
                        {
                            // Fallback to primitive sphere
                            rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            rock.name = "Rock_Placeholder";
                            rock.transform.parent = decorationContainer.transform;
                            rock.transform.position = hit.point;
                            float scale = 0.5f + (float)rng.NextDouble() * 1.5f;
                            rock.transform.localScale = new Vector3(scale, scale * 0.6f, scale);
                            Destroy(rock.GetComponent<Collider>());
                        }

                        rocksPlaced++;
                    }
                }
            }
        }

        private void OnDestroy()
        {
            // Clean up meshes
            if (lodMeshes != null)
            {
                foreach (var mesh in lodMeshes)
                {
                    if (mesh != null) Destroy(mesh);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;

            // Draw raycast hit points (green spheres on ground)
            Gizmos.color = Color.green;
            foreach (var hitPos in debugRaycastHits)
            {
                Gizmos.DrawWireSphere(hitPos, 0.5f);
            }

            // Draw tree positions (yellow spheres slightly above)
            Gizmos.color = Color.yellow;
            foreach (var treePos in debugTreePositions)
            {
                Gizmos.DrawWireSphere(treePos, 0.3f);
                // Draw line from ground to tree position
                Gizmos.DrawLine(new Vector3(treePos.x, treePos.y - 5f, treePos.z), treePos);
            }

            // Draw chunk bounds
            if (size > 0)
            {
                Gizmos.color = Color.cyan;
                Vector3 center = transform.position + new Vector3(size / 2f, 50f, size / 2f);
                Gizmos.DrawWireCube(center, new Vector3(size, 100f, size));
            }
        }
    }
}
