using UnityEngine;
using System.Collections.Generic;

namespace CreatorWorld.World
{
    /// <summary>
    /// Detects terrain depressions and generates lake water surfaces.
    /// Lakes form in areas below water level that are surrounded by higher terrain.
    /// </summary>
    public class LakeGenerator : MonoBehaviour
    {
        [Header("Lake Detection")]
        [SerializeField] private int worldSeed = 12345;
        [SerializeField] private float searchRadius = 500f;
        [SerializeField] private int maxLakes = 10;
        [SerializeField] private float minLakeSize = 20f;
        [SerializeField] private float sampleSpacing = 10f;

        [Header("Lake Rendering")]
        [SerializeField] private Material lakeMaterial;
        [SerializeField] private float waterSurfaceOffset = 0.05f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool generateOnStart = true;

        // Generated lake data
        private List<LakeData> lakes = new List<LakeData>();
        private List<GameObject> lakeMeshes = new List<GameObject>();
        private static LakeGenerator instance;

        public static LakeGenerator Instance => instance;
        public List<LakeData> Lakes => lakes;

        private void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            if (generateOnStart)
            {
                GenerateLakes();
            }
        }

        /// <summary>
        /// Detect and generate all lakes
        /// </summary>
        [ContextMenu("Generate Lakes")]
        public void GenerateLakes()
        {
            // Clear existing
            lakes.Clear();
            foreach (var mesh in lakeMeshes)
            {
                if (mesh != null) Destroy(mesh);
            }
            lakeMeshes.Clear();

            System.Random rng = new System.Random(worldSeed);

            // Search for terrain depressions
            List<Vector2> depressions = FindDepressions(rng);
            Debug.Log($"[LakeGenerator] Found {depressions.Count} potential lake locations");

            // Create lakes from valid depressions
            foreach (var center in depressions)
            {
                LakeData lake = AnalyzeDepression(center);
                if (lake != null && lake.Radius >= minLakeSize)
                {
                    lakes.Add(lake);
                    GameObject lakeMesh = CreateLakeMesh(lake);
                    if (lakeMesh != null)
                    {
                        lakeMesh.transform.parent = transform;
                        lakeMeshes.Add(lakeMesh);
                    }
                }
            }

            Debug.Log($"[LakeGenerator] Generated {lakes.Count} lakes");
        }

        /// <summary>
        /// Find terrain points that are below water level and surrounded by higher terrain
        /// </summary>
        private List<Vector2> FindDepressions(System.Random rng)
        {
            List<Vector2> depressions = new List<Vector2>();
            int gridSize = Mathf.CeilToInt(searchRadius * 2 / sampleSpacing);

            for (int gx = 0; gx < gridSize && depressions.Count < maxLakes * 3; gx++)
            {
                for (int gz = 0; gz < gridSize && depressions.Count < maxLakes * 3; gz++)
                {
                    float x = -searchRadius + gx * sampleSpacing;
                    float z = -searchRadius + gz * sampleSpacing;

                    float height = TerrainGenerator.GetHeightAt(x, z, worldSeed);

                    // Must be below water level
                    if (height >= TerrainGenerator.WaterLevel) continue;

                    // Check if this is NOT part of the ocean (surrounded by higher terrain)
                    if (IsInlandDepression(x, z, height))
                    {
                        // Make sure not too close to existing depressions
                        bool tooClose = false;
                        foreach (var existing in depressions)
                        {
                            if (Vector2.Distance(new Vector2(x, z), existing) < minLakeSize * 2)
                            {
                                tooClose = true;
                                break;
                            }
                        }

                        if (!tooClose)
                        {
                            depressions.Add(new Vector2(x, z));
                        }
                    }
                }
            }

            return depressions;
        }

        /// <summary>
        /// Check if a below-water-level point is an inland depression (lake) vs ocean
        /// </summary>
        private bool IsInlandDepression(float x, float z, float height)
        {
            float checkRadius = 100f;
            int aboveWaterCount = 0;
            int totalSamples = 16;

            for (int i = 0; i < totalSamples; i++)
            {
                float angle = i * Mathf.PI * 2f / totalSamples;
                float sx = x + Mathf.Cos(angle) * checkRadius;
                float sz = z + Mathf.Sin(angle) * checkRadius;

                float surroundHeight = TerrainGenerator.GetHeightAt(sx, sz, worldSeed);
                if (surroundHeight > TerrainGenerator.WaterLevel + 3f)
                {
                    aboveWaterCount++;
                }
            }

            // Need most surrounding terrain to be above water for this to be a lake
            return aboveWaterCount >= totalSamples * 0.6f;
        }

        /// <summary>
        /// Analyze a depression to determine lake boundaries
        /// </summary>
        private LakeData AnalyzeDepression(Vector2 center)
        {
            // Find the extent of the depression (flood fill to water level)
            List<Vector2> lakePoints = new List<Vector2>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            Queue<Vector2> toVisit = new Queue<Vector2>();

            toVisit.Enqueue(center);
            float gridScale = 5f; // Sample resolution

            while (toVisit.Count > 0 && lakePoints.Count < 1000)
            {
                Vector2 current = toVisit.Dequeue();
                Vector2Int gridPos = new Vector2Int(
                    Mathf.RoundToInt(current.x / gridScale),
                    Mathf.RoundToInt(current.y / gridScale)
                );

                if (visited.Contains(gridPos)) continue;
                visited.Add(gridPos);

                float height = TerrainGenerator.GetHeightAt(current.x, current.y, worldSeed);
                if (height > TerrainGenerator.WaterLevel) continue;

                lakePoints.Add(current);

                // Check neighbors
                Vector2[] neighbors = new Vector2[]
                {
                    current + Vector2.right * gridScale,
                    current + Vector2.left * gridScale,
                    current + Vector2.up * gridScale,
                    current + Vector2.down * gridScale
                };

                foreach (var neighbor in neighbors)
                {
                    Vector2Int neighborGrid = new Vector2Int(
                        Mathf.RoundToInt(neighbor.x / gridScale),
                        Mathf.RoundToInt(neighbor.y / gridScale)
                    );
                    if (!visited.Contains(neighborGrid))
                    {
                        toVisit.Enqueue(neighbor);
                    }
                }
            }

            if (lakePoints.Count < 4) return null;

            // Calculate lake center and radius
            Vector2 avgCenter = Vector2.zero;
            foreach (var p in lakePoints) avgCenter += p;
            avgCenter /= lakePoints.Count;

            float maxDist = 0f;
            float minDepth = 0f;
            foreach (var p in lakePoints)
            {
                float dist = Vector2.Distance(p, avgCenter);
                if (dist > maxDist) maxDist = dist;

                float depth = TerrainGenerator.WaterLevel - TerrainGenerator.GetHeightAt(p.x, p.y, worldSeed);
                if (depth > minDepth) minDepth = depth;
            }

            return new LakeData
            {
                Center = new Vector3(avgCenter.x, TerrainGenerator.WaterLevel, avgCenter.y),
                Radius = maxDist,
                MaxDepth = minDepth,
                Points = lakePoints
            };
        }

        /// <summary>
        /// Create a water surface mesh for a lake
        /// </summary>
        private GameObject CreateLakeMesh(LakeData lake)
        {
            if (lake.Points.Count < 4) return null;

            // Create a simple circular mesh for the lake
            int segments = 32;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            // Center vertex
            vertices.Add(new Vector3(0, waterSurfaceOffset, 0));
            uvs.Add(new Vector2(0.5f, 0.5f));

            // Edge vertices
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle) * lake.Radius;
                float z = Mathf.Sin(angle) * lake.Radius;
                vertices.Add(new Vector3(x, waterSurfaceOffset, z));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
            }

            // Triangles (fan from center)
            for (int i = 1; i <= segments; i++)
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add(i + 1);
            }

            Mesh mesh = new Mesh();
            mesh.name = "Lake";
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject lakeObj = new GameObject($"Lake_{lakes.Count}");
            lakeObj.transform.position = lake.Center;

            MeshFilter filter = lakeObj.AddComponent<MeshFilter>();
            MeshRenderer renderer = lakeObj.AddComponent<MeshRenderer>();

            filter.mesh = mesh;
            renderer.material = lakeMaterial != null ? lakeMaterial : CreateDefaultLakeMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return lakeObj;
        }

        /// <summary>
        /// Create a simple water material if none assigned
        /// </summary>
        private Material CreateDefaultLakeMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.name = "LakeWater";

            // Transparent settings
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;

            // Lake color (slightly different from ocean)
            Color lakeColor = new Color(0.1f, 0.4f, 0.5f, 0.8f);
            mat.SetColor("_BaseColor", lakeColor);
            mat.color = lakeColor;
            mat.SetFloat("_Smoothness", 0.9f);

            return mat;
        }

        /// <summary>
        /// Check if a world position is in a lake
        /// </summary>
        public static bool IsInLake(float x, float z, out float depth)
        {
            depth = 0f;
            if (instance == null || instance.lakes == null) return false;

            Vector2 pos = new Vector2(x, z);
            foreach (var lake in instance.lakes)
            {
                float dist = Vector2.Distance(pos, new Vector2(lake.Center.x, lake.Center.z));
                if (dist <= lake.Radius)
                {
                    depth = lake.MaxDepth * (1f - dist / lake.Radius);
                    return true;
                }
            }

            return false;
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || lakes == null) return;

            foreach (var lake in lakes)
            {
                Gizmos.color = new Color(0, 0.5f, 1f, 0.5f);
                Gizmos.DrawWireSphere(lake.Center, lake.Radius);

                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(lake.Center, 3f);
            }
        }

        private void OnDestroy()
        {
            foreach (var mesh in lakeMeshes)
            {
                if (mesh != null) Destroy(mesh);
            }
        }
    }

    /// <summary>
    /// Data for a single lake
    /// </summary>
    [System.Serializable]
    public class LakeData
    {
        public Vector3 Center;
        public float Radius;
        public float MaxDepth;
        public List<Vector2> Points;
    }
}
