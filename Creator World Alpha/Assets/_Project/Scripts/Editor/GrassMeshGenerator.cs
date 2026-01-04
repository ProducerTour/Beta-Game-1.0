using UnityEngine;
using UnityEditor;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Generates simple billboard meshes for grass LOD system.
    /// Access via: Tools > Creator World > Generate Grass Meshes
    /// </summary>
    public static class GrassMeshGenerator
    {
        private const string MESH_PATH = "Assets/_Project/Meshes/Grass";

        [MenuItem("Tools/Creator World/Generate Grass Meshes", priority = 150)]
        public static void GenerateGrassMeshes()
        {
            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Meshes"))
                AssetDatabase.CreateFolder("Assets/_Project", "Meshes");
            if (!AssetDatabase.IsValidFolder(MESH_PATH))
                AssetDatabase.CreateFolder("Assets/_Project/Meshes", "Grass");

            // LOD0 - Full detail (cross billboard with 2 quads)
            Mesh lod0 = CreateCrossBillboard("GrassLOD0", 0.15f, 0.6f);
            AssetDatabase.CreateAsset(lod0, $"{MESH_PATH}/GrassLOD0.asset");

            // LOD1 - Medium detail (single quad)
            Mesh lod1 = CreateSingleBillboard("GrassLOD1", 0.12f, 0.5f);
            AssetDatabase.CreateAsset(lod1, $"{MESH_PATH}/GrassLOD1.asset");

            // LOD2 - Low detail (smaller quad)
            Mesh lod2 = CreateSingleBillboard("GrassLOD2", 0.1f, 0.35f);
            AssetDatabase.CreateAsset(lod2, $"{MESH_PATH}/GrassLOD2.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Try to assign to GrassSettings if it exists
            var grassSettings = AssetDatabase.LoadAssetAtPath<Config.GrassSettings>(
                "Assets/_Project/ScriptableObjects/DefaultGrassSettings.asset");

            if (grassSettings != null)
            {
                SerializedObject so = new SerializedObject(grassSettings);
                so.FindProperty("lod0Mesh").objectReferenceValue = lod0;
                so.FindProperty("lod1Mesh").objectReferenceValue = lod1;
                so.FindProperty("lod2Mesh").objectReferenceValue = lod2;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(grassSettings);
                AssetDatabase.SaveAssets();
            }

            Debug.Log("[Setup] Generated grass LOD meshes in " + MESH_PATH);
            EditorUtility.DisplayDialog("Grass Meshes Generated",
                "Created:\n• GrassLOD0 (cross billboard)\n• GrassLOD1 (single quad)\n• GrassLOD2 (small quad)\n\nLocation: " + MESH_PATH +
                (grassSettings != null ? "\n\n✓ Assigned to DefaultGrassSettings" : "\n\n⚠️ DefaultGrassSettings not found"),
                "OK");
        }

        /// <summary>
        /// Creates a cross-billboard mesh (2 quads at 90 degrees)
        /// </summary>
        private static Mesh CreateCrossBillboard(string name, float width, float height)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;

            float halfWidth = width / 2f;

            // 8 vertices (2 quads)
            Vector3[] vertices = new Vector3[]
            {
                // Quad 1 (facing +X/-X)
                new Vector3(-halfWidth, 0, 0),
                new Vector3(halfWidth, 0, 0),
                new Vector3(halfWidth, height, 0),
                new Vector3(-halfWidth, height, 0),

                // Quad 2 (facing +Z/-Z)
                new Vector3(0, 0, -halfWidth),
                new Vector3(0, 0, halfWidth),
                new Vector3(0, height, halfWidth),
                new Vector3(0, height, -halfWidth)
            };

            Vector2[] uvs = new Vector2[]
            {
                // Quad 1
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1),

                // Quad 2
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };

            // Normals for wind animation (pointing up at top, forward at bottom)
            Vector3[] normals = new Vector3[]
            {
                // Quad 1
                Vector3.back,
                Vector3.back,
                Vector3.up,
                Vector3.up,

                // Quad 2
                Vector3.left,
                Vector3.left,
                Vector3.up,
                Vector3.up
            };

            // Colors for vertex height (used in shader for wind)
            Color[] colors = new Color[]
            {
                new Color(0, 0, 0, 0), // Bottom
                new Color(0, 0, 0, 0),
                new Color(1, 1, 1, 1), // Top - full wind influence
                new Color(1, 1, 1, 1),

                new Color(0, 0, 0, 0),
                new Color(0, 0, 0, 0),
                new Color(1, 1, 1, 1),
                new Color(1, 1, 1, 1)
            };

            int[] triangles = new int[]
            {
                // Quad 1 front
                0, 2, 1,
                0, 3, 2,
                // Quad 1 back
                0, 1, 2,
                0, 2, 3,

                // Quad 2 front
                4, 6, 5,
                4, 7, 6,
                // Quad 2 back
                4, 5, 6,
                4, 6, 7
            };

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Creates a single billboard quad
        /// </summary>
        private static Mesh CreateSingleBillboard(string name, float width, float height)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;

            float halfWidth = width / 2f;

            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-halfWidth, 0, 0),
                new Vector3(halfWidth, 0, 0),
                new Vector3(halfWidth, height, 0),
                new Vector3(-halfWidth, height, 0)
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };

            Vector3[] normals = new Vector3[]
            {
                Vector3.back,
                Vector3.back,
                Vector3.up,
                Vector3.up
            };

            Color[] colors = new Color[]
            {
                new Color(0, 0, 0, 0), // Bottom - no wind
                new Color(0, 0, 0, 0),
                new Color(1, 1, 1, 1), // Top - full wind
                new Color(1, 1, 1, 1)
            };

            int[] triangles = new int[]
            {
                // Front face
                0, 2, 1,
                0, 3, 2,
                // Back face
                0, 1, 2,
                0, 2, 3
            };

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
