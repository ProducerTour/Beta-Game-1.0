using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Generates stylized grass blade meshes for the grass LOD system.
    /// Creates tapered, curved blades with natural-looking shapes.
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

            // LOD0 - Full detail (3-blade clump with curved, tapered blades)
            Mesh lod0 = CreateGrassClump("GrassLOD0", 3, 8, 0.08f, 0.6f);
            AssetDatabase.CreateAsset(lod0, $"{MESH_PATH}/GrassLOD0.asset");

            // LOD1 - Medium detail (2-blade with fewer segments)
            Mesh lod1 = CreateGrassClump("GrassLOD1", 2, 5, 0.07f, 0.5f);
            AssetDatabase.CreateAsset(lod1, $"{MESH_PATH}/GrassLOD1.asset");

            // LOD2 - Low detail (single simple blade)
            Mesh lod2 = CreateSimpleBlade("GrassLOD2", 3, 0.06f, 0.35f);
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

            Debug.Log("[Setup] Generated stylized grass LOD meshes in " + MESH_PATH);
            EditorUtility.DisplayDialog("Grass Meshes Generated",
                $"Created stylized grass blades:\n" +
                $"• GrassLOD0 (3-blade clump, {lod0.vertexCount} verts)\n" +
                $"• GrassLOD1 (2-blade clump, {lod1.vertexCount} verts)\n" +
                $"• GrassLOD2 (simple blade, {lod2.vertexCount} verts)\n\n" +
                $"Location: {MESH_PATH}" +
                (grassSettings != null ? "\n\n✓ Assigned to DefaultGrassSettings" : "\n\n⚠️ DefaultGrassSettings not found"),
                "OK");
        }

        /// <summary>
        /// Creates a clump of multiple grass blades at different angles
        /// </summary>
        private static Mesh CreateGrassClump(string name, int bladeCount, int segments, float baseWidth, float height)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();

            // Blade angles for clump variety
            float[] bladeAngles = bladeCount switch
            {
                1 => new[] { 0f },
                2 => new[] { -25f, 25f },
                3 => new[] { -30f, 0f, 30f },
                4 => new[] { -40f, -15f, 15f, 40f },
                _ => new[] { 0f }
            };

            // Height and width variations per blade
            float[] heightMults = bladeCount switch
            {
                1 => new[] { 1f },
                2 => new[] { 0.85f, 1f },
                3 => new[] { 0.75f, 1f, 0.9f },
                4 => new[] { 0.7f, 0.9f, 1f, 0.8f },
                _ => new[] { 1f }
            };

            // Curve directions (slight lean)
            float[] curveDirs = bladeCount switch
            {
                1 => new[] { 0f },
                2 => new[] { -0.1f, 0.1f },
                3 => new[] { -0.15f, 0.05f, 0.12f },
                4 => new[] { -0.2f, -0.05f, 0.08f, 0.18f },
                _ => new[] { 0f }
            };

            for (int b = 0; b < bladeCount && b < bladeAngles.Length; b++)
            {
                float angle = bladeAngles[b] * Mathf.Deg2Rad;
                float bladeHeight = height * heightMults[b];
                float curveDir = curveDirs[b];

                int baseIndex = vertices.Count;
                AddStylizedBlade(
                    vertices, uvs, normals, colors, triangles,
                    baseIndex, segments, baseWidth, bladeHeight, angle, curveDir
                );
            }

            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.normals = normals.ToArray();
            mesh.colors = colors.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }

        /// <summary>
        /// Adds a single stylized grass blade with taper and curve
        /// </summary>
        private static void AddStylizedBlade(
            List<Vector3> vertices, List<Vector2> uvs, List<Vector3> normals, List<Color> colors, List<int> triangles,
            int baseIndex, int segments, float baseWidth, float height, float rotationAngle, float curveAmount)
        {
            // Create rotation matrix for blade orientation
            float cosA = Mathf.Cos(rotationAngle);
            float sinA = Mathf.Sin(rotationAngle);

            // Generate vertices along the blade
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float y = t * height;

                // Taper: width decreases towards tip (with slight curve for natural look)
                // Use a curve that tapers more aggressively at the tip
                float taperCurve = 1f - Mathf.Pow(t, 1.5f);
                float width = baseWidth * taperCurve;

                // Blade curve (bends outward slightly then back)
                // S-curve for natural grass look
                float curve = Mathf.Sin(t * Mathf.PI) * curveAmount * height;

                // Add slight random-looking variation based on height
                float microCurve = Mathf.Sin(t * Mathf.PI * 3f) * 0.01f * height;

                // Left and right edge positions (before rotation)
                float leftX = -width / 2f + curve + microCurve;
                float rightX = width / 2f + curve + microCurve;

                // Apply rotation around Y axis
                Vector3 leftPos = new Vector3(
                    leftX * cosA,
                    y,
                    leftX * sinA
                );
                Vector3 rightPos = new Vector3(
                    rightX * cosA,
                    y,
                    rightX * sinA
                );

                // For the tip (last segment), merge to a point
                if (i == segments)
                {
                    Vector3 tipPos = new Vector3(curve * cosA, y, curve * sinA);
                    vertices.Add(tipPos);
                    uvs.Add(new Vector2(0.5f, 1f));

                    // Tip normal points mostly up
                    normals.Add(Vector3.up);
                    colors.Add(new Color(1, 1, 1, 1)); // Full wind influence at tip
                }
                else
                {
                    vertices.Add(leftPos);
                    vertices.Add(rightPos);

                    uvs.Add(new Vector2(0f, t));
                    uvs.Add(new Vector2(1f, t));

                    // Normal calculation - face outward with slight upward tilt at top
                    Vector3 faceNormal = new Vector3(-sinA, t * 0.5f, cosA).normalized;
                    normals.Add(faceNormal);
                    normals.Add(faceNormal);

                    // Color stores height gradient for wind
                    float windInfluence = Mathf.Pow(t, 2f); // Quadratic for more realistic base stability
                    colors.Add(new Color(windInfluence, windInfluence, windInfluence, 1));
                    colors.Add(new Color(windInfluence, windInfluence, windInfluence, 1));
                }
            }

            // Generate triangles
            int vertsPerRow = 2;
            for (int i = 0; i < segments; i++)
            {
                int rowStart = baseIndex + i * vertsPerRow;

                if (i < segments - 1)
                {
                    // Regular quad section
                    int bl = rowStart;
                    int br = rowStart + 1;
                    int tl = rowStart + vertsPerRow;
                    int tr = rowStart + vertsPerRow + 1;

                    // Front face
                    triangles.Add(bl);
                    triangles.Add(tl);
                    triangles.Add(br);

                    triangles.Add(br);
                    triangles.Add(tl);
                    triangles.Add(tr);

                    // Back face
                    triangles.Add(bl);
                    triangles.Add(br);
                    triangles.Add(tl);

                    triangles.Add(br);
                    triangles.Add(tr);
                    triangles.Add(tl);
                }
                else
                {
                    // Tip triangle (connects to single tip vertex)
                    int bl = rowStart;
                    int br = rowStart + 1;
                    int tip = rowStart + vertsPerRow; // The single tip vertex

                    // Front face
                    triangles.Add(bl);
                    triangles.Add(tip);
                    triangles.Add(br);

                    // Back face
                    triangles.Add(bl);
                    triangles.Add(br);
                    triangles.Add(tip);
                }
            }
        }

        /// <summary>
        /// Creates a simple tapered blade for lowest LOD
        /// </summary>
        private static Mesh CreateSimpleBlade(string name, int segments, float baseWidth, float height)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();

            AddStylizedBlade(vertices, uvs, normals, colors, triangles, 0, segments, baseWidth, height, 0f, 0.05f);

            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.normals = normals.ToArray();
            mesh.colors = colors.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }

        [MenuItem("Tools/Creator World/Generate Grass Meshes (Variant: Thin)", priority = 151)]
        public static void GenerateGrassMeshesThin()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Meshes"))
                AssetDatabase.CreateFolder("Assets/_Project", "Meshes");
            if (!AssetDatabase.IsValidFolder(MESH_PATH))
                AssetDatabase.CreateFolder("Assets/_Project/Meshes", "Grass");

            // Thinner, taller grass
            Mesh lod0 = CreateGrassClump("GrassLOD0_Thin", 4, 10, 0.04f, 0.8f);
            AssetDatabase.CreateAsset(lod0, $"{MESH_PATH}/GrassLOD0_Thin.asset");

            Mesh lod1 = CreateGrassClump("GrassLOD1_Thin", 3, 6, 0.035f, 0.65f);
            AssetDatabase.CreateAsset(lod1, $"{MESH_PATH}/GrassLOD1_Thin.asset");

            Mesh lod2 = CreateSimpleBlade("GrassLOD2_Thin", 4, 0.03f, 0.45f);
            AssetDatabase.CreateAsset(lod2, $"{MESH_PATH}/GrassLOD2_Thin.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Setup] Generated thin grass variant meshes in " + MESH_PATH);
            EditorUtility.DisplayDialog("Thin Grass Meshes Generated",
                "Created thin grass variant. Manually assign to GrassSettings if desired.",
                "OK");
        }

        [MenuItem("Tools/Creator World/Generate Grass Meshes (Variant: Wild)", priority = 152)]
        public static void GenerateGrassMeshesWild()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Meshes"))
                AssetDatabase.CreateFolder("Assets/_Project", "Meshes");
            if (!AssetDatabase.IsValidFolder(MESH_PATH))
                AssetDatabase.CreateFolder("Assets/_Project/Meshes", "Grass");

            // Wider spread, wilder looking grass
            Mesh lod0 = CreateWildGrassClump("GrassLOD0_Wild", 5, 8, 0.06f, 0.7f);
            AssetDatabase.CreateAsset(lod0, $"{MESH_PATH}/GrassLOD0_Wild.asset");

            Mesh lod1 = CreateWildGrassClump("GrassLOD1_Wild", 3, 5, 0.05f, 0.55f);
            AssetDatabase.CreateAsset(lod1, $"{MESH_PATH}/GrassLOD1_Wild.asset");

            Mesh lod2 = CreateSimpleBlade("GrassLOD2_Wild", 3, 0.05f, 0.4f);
            AssetDatabase.CreateAsset(lod2, $"{MESH_PATH}/GrassLOD2_Wild.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Setup] Generated wild grass variant meshes in " + MESH_PATH);
            EditorUtility.DisplayDialog("Wild Grass Meshes Generated",
                "Created wild grass variant. Manually assign to GrassSettings if desired.",
                "OK");
        }

        /// <summary>
        /// Creates a wilder, more spread out grass clump
        /// </summary>
        private static Mesh CreateWildGrassClump(string name, int bladeCount, int segments, float baseWidth, float height)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();

            // More extreme angles for wild look
            float angleSpread = 50f;
            float heightVar = 0.4f;
            float curveVar = 0.25f;

            for (int b = 0; b < bladeCount; b++)
            {
                float t = (float)b / (bladeCount - 1) - 0.5f; // -0.5 to 0.5
                float angle = t * angleSpread * 2f * Mathf.Deg2Rad;

                // Pseudo-random variations based on blade index
                float heightMult = 1f - Mathf.Abs(Mathf.Sin(b * 2.7f)) * heightVar;
                float curveDir = Mathf.Sin(b * 1.3f) * curveVar;

                int baseIndex = vertices.Count;
                AddStylizedBlade(
                    vertices, uvs, normals, colors, triangles,
                    baseIndex, segments, baseWidth, height * heightMult, angle, curveDir
                );
            }

            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.normals = normals.ToArray();
            mesh.colors = colors.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }
    }
}
