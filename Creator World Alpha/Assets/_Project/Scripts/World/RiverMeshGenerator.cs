using UnityEngine;
using System.Collections.Generic;

namespace CreatorWorld.World
{
    /// <summary>
    /// Generates water surface meshes for rivers.
    /// Creates a mesh strip that follows each river path.
    /// </summary>
    public class RiverMeshGenerator : MonoBehaviour
    {
        [Header("River Rendering")]
        [SerializeField] private Material riverMaterial;
        [SerializeField] private float waterSurfaceOffset = 0.1f;
        [SerializeField] private int segmentsPerPoint = 2;

        [Header("References")]
        [SerializeField] private RiverGenerator riverGenerator;

        private List<GameObject> riverMeshes = new List<GameObject>();

        private void Start()
        {
            if (riverGenerator == null)
            {
                riverGenerator = FindFirstObjectByType<RiverGenerator>();
            }

            // Generate river meshes after rivers are created
            Invoke(nameof(GenerateAllRiverMeshes), 0.5f);
        }

        /// <summary>
        /// Generate water meshes for all rivers
        /// </summary>
        [ContextMenu("Generate River Meshes")]
        public void GenerateAllRiverMeshes()
        {
            // Clear existing meshes
            foreach (var mesh in riverMeshes)
            {
                if (mesh != null) Destroy(mesh);
            }
            riverMeshes.Clear();

            if (riverGenerator == null || riverGenerator.Rivers == null)
            {
                Debug.LogWarning("[RiverMeshGenerator] No river generator or rivers found");
                return;
            }

            // Create material if not assigned
            if (riverMaterial == null)
            {
                riverMaterial = CreateDefaultRiverMaterial();
            }

            // Generate mesh for each river
            int riverIndex = 0;
            foreach (var river in riverGenerator.Rivers)
            {
                if (river.Points.Count < 2) continue;

                GameObject riverObj = GenerateRiverMesh(river, riverIndex);
                if (riverObj != null)
                {
                    riverObj.transform.parent = transform;
                    riverMeshes.Add(riverObj);
                }
                riverIndex++;
            }

            Debug.Log($"[RiverMeshGenerator] Generated {riverMeshes.Count} river meshes");
        }

        /// <summary>
        /// Generate a mesh for a single river
        /// </summary>
        private GameObject GenerateRiverMesh(RiverPath river, int index)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            float totalLength = 0f;

            for (int i = 0; i < river.Points.Count; i++)
            {
                var point = river.Points[i];

                // Calculate perpendicular direction for width
                Vector3 right;
                if (i < river.Points.Count - 1)
                {
                    Vector3 forward = river.Points[i + 1].Position - point.Position;
                    right = Vector3.Cross(Vector3.up, forward).normalized;
                }
                else
                {
                    Vector3 forward = point.Position - river.Points[i - 1].Position;
                    right = Vector3.Cross(Vector3.up, forward).normalized;
                }

                float halfWidth = point.Width * 0.5f;

                // Water surface position (slightly above carved riverbed)
                Vector3 waterPos = point.Position + Vector3.up * waterSurfaceOffset;

                // Add left and right vertices
                vertices.Add(waterPos - right * halfWidth);
                vertices.Add(waterPos + right * halfWidth);

                // UV based on river length
                float u = totalLength / river.TotalLength;
                uvs.Add(new Vector2(0f, u));
                uvs.Add(new Vector2(1f, u));

                // Add triangles (except for first point)
                if (i > 0)
                {
                    int baseIndex = (i - 1) * 2;
                    // First triangle
                    triangles.Add(baseIndex);
                    triangles.Add(baseIndex + 2);
                    triangles.Add(baseIndex + 1);
                    // Second triangle
                    triangles.Add(baseIndex + 1);
                    triangles.Add(baseIndex + 2);
                    triangles.Add(baseIndex + 3);
                }

                // Update total length for UV calculation
                if (i < river.Points.Count - 1)
                {
                    totalLength += Vector3.Distance(point.Position, river.Points[i + 1].Position);
                }
            }

            // Create mesh
            Mesh mesh = new Mesh();
            mesh.name = $"River_{index}";
            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Create GameObject
            GameObject riverObj = new GameObject($"River_{index}");
            MeshFilter filter = riverObj.AddComponent<MeshFilter>();
            MeshRenderer renderer = riverObj.AddComponent<MeshRenderer>();

            filter.mesh = mesh;
            renderer.material = riverMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return riverObj;
        }

        /// <summary>
        /// Create a simple water material
        /// </summary>
        private Material CreateDefaultRiverMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material mat = new Material(shader);
            mat.name = "RiverWater";

            // Set up for transparency
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0); // Alpha blend
            mat.SetFloat("_AlphaClip", 0);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;

            // River water color (slightly more blue/green than ocean)
            Color riverColor = new Color(0.15f, 0.5f, 0.55f, 0.75f);
            mat.SetColor("_BaseColor", riverColor);
            mat.color = riverColor;

            // High smoothness for reflections
            mat.SetFloat("_Smoothness", 0.9f);

            return mat;
        }

        private void OnDestroy()
        {
            foreach (var mesh in riverMeshes)
            {
                if (mesh != null) Destroy(mesh);
            }
        }
    }
}
