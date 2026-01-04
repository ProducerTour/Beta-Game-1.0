using UnityEngine;
using UnityEditor;

namespace CreatorWorld.World.Editor
{
    /// <summary>
    /// Editor utility to help set up GrassManager with correct references.
    /// </summary>
    public class GrassManagerSetup : EditorWindow
    {
        [MenuItem("CreatorWorld/Setup Grass Manager")]
        public static void ShowWindow()
        {
            GetWindow<GrassManagerSetup>("Grass Manager Setup");
        }

        private void OnGUI()
        {
            GUILayout.Label("Grass Manager Setup", EditorStyles.boldLabel);
            GUILayout.Space(10);

            if (GUILayout.Button("Create & Configure Grass Manager", GUILayout.Height(40)))
            {
                SetupGrassManager();
            }

            GUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "This will:\n" +
                "1. Create a GrassManager GameObject (or find existing)\n" +
                "2. Find and assign ChunkManager reference\n" +
                "3. Find and assign Main Camera\n" +
                "4. Find and assign grass_blade mesh\n" +
                "5. Create grass material with correct shader",
                MessageType.Info);
        }

        private static void SetupGrassManager()
        {
            // Find or create GrassManager
            GrassManager grassManager = Object.FindFirstObjectByType<GrassManager>();
            if (grassManager == null)
            {
                GameObject go = new GameObject("GrassManager");
                grassManager = go.AddComponent<GrassManager>();
                Debug.Log("[GrassSetup] Created GrassManager GameObject");
            }

            SerializedObject serializedGrass = new SerializedObject(grassManager);

            // Find ChunkManager
            ChunkManager chunkManager = Object.FindFirstObjectByType<ChunkManager>();
            if (chunkManager != null)
            {
                serializedGrass.FindProperty("chunkManager").objectReferenceValue = chunkManager;
                Debug.Log("[GrassSetup] Assigned ChunkManager");

                // Wire GrassManager into ChunkManager
                SerializedObject serializedChunk = new SerializedObject(chunkManager);
                serializedChunk.FindProperty("grassManager").objectReferenceValue = grassManager;
                serializedChunk.ApplyModifiedProperties();
                Debug.Log("[GrassSetup] Wired GrassManager into ChunkManager");
            }
            else
            {
                Debug.LogWarning("[GrassSetup] ChunkManager not found in scene!");
            }

            // Find Main Camera
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                serializedGrass.FindProperty("mainCamera").objectReferenceValue = mainCam;
                Debug.Log("[GrassSetup] Assigned Main Camera");
            }
            else
            {
                Debug.LogWarning("[GrassSetup] Main Camera not found!");
            }

            // Find grass blade mesh
            string[] meshGuids = AssetDatabase.FindAssets("grass_blade t:Mesh");
            if (meshGuids.Length == 0)
            {
                // Try finding FBX and getting the mesh from it
                string[] fbxGuids = AssetDatabase.FindAssets("grass_blade t:Model");
                if (fbxGuids.Length > 0)
                {
                    string fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuids[0]);
                    GameObject fbxObj = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                    if (fbxObj != null)
                    {
                        MeshFilter mf = fbxObj.GetComponentInChildren<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null)
                        {
                            serializedGrass.FindProperty("grassBladeMesh").objectReferenceValue = mf.sharedMesh;
                            Debug.Log($"[GrassSetup] Assigned grass blade mesh from: {fbxPath}");
                        }
                    }
                }
            }
            else
            {
                string meshPath = AssetDatabase.GUIDToAssetPath(meshGuids[0]);
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (mesh != null)
                {
                    serializedGrass.FindProperty("grassBladeMesh").objectReferenceValue = mesh;
                    Debug.Log($"[GrassSetup] Assigned grass blade mesh: {meshPath}");
                }
            }

            // Find or create grass material
            string materialPath = "Assets/_Project/Materials/GrassInstanced.mat";
            Material grassMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (grassMaterial == null)
            {
                // Try to find the shader
                Shader grassShader = Shader.Find("CreatorWorld/GrassInstanced");
                if (grassShader != null)
                {
                    grassMaterial = new Material(grassShader);
                    grassMaterial.SetColor("_BaseColor", new Color(0.2f, 0.6f, 0.1f, 1f));
                    grassMaterial.SetColor("_TipColor", new Color(0.4f, 0.8f, 0.2f, 1f));
                    grassMaterial.SetColor("_AOColor", new Color(0.1f, 0.2f, 0.05f, 1f));
                    grassMaterial.SetFloat("_WindStrength", 0.5f);
                    grassMaterial.SetFloat("_WindSpeed", 1.0f);
                    grassMaterial.SetFloat("_WindNoiseScale", 0.1f);

                    AssetDatabase.CreateAsset(grassMaterial, materialPath);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[GrassSetup] Created grass material at: {materialPath}");
                }
                else
                {
                    Debug.LogWarning("[GrassSetup] GrassInstanced shader not found! Make sure the shader compiled successfully.");
                }
            }

            if (grassMaterial != null)
            {
                serializedGrass.FindProperty("grassMaterial").objectReferenceValue = grassMaterial;
                Debug.Log("[GrassSetup] Assigned grass material");
            }

            serializedGrass.ApplyModifiedProperties();

            // Select the GrassManager
            Selection.activeGameObject = grassManager.gameObject;
            EditorGUIUtility.PingObject(grassManager.gameObject);

            Debug.Log("[GrassSetup] Setup complete! Check the GrassManager component in the Inspector.");
        }
    }
}
