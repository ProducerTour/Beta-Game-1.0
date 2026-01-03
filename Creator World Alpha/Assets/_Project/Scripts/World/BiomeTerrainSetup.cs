using UnityEngine;

namespace CreatorWorld.World
{
    /// <summary>
    /// Helper component to set up biome terrain material with textures.
    /// Attach to ChunkManager and assign textures in the Inspector.
    /// </summary>
    [RequireComponent(typeof(ChunkManager))]
    public class BiomeTerrainSetup : MonoBehaviour
    {
        [Header("Sand/Beach Textures (Ground061 from AmbientCG)")]
        [Tooltip("Download from: https://ambientcg.com/view?id=Ground061")]
        public Texture2D sandAlbedo;
        public Texture2D sandNormal;
        public Texture2D sandRoughness;
        [Range(1f, 10f)] public float sandTiling = 4f;

        [Header("Dirt/Ground Textures (dirtwithrocks-bl)")]
        [Tooltip("Use dirt texture here - procedural grass blades grow on top")]
        public Texture2D grassAlbedo;  // Named 'grass' for shader compatibility
        public Texture2D grassNormal;
        public Texture2D grassRoughness;
        [Range(1f, 10f)] public float grassTiling = 4f;
        public Color grassTint = new Color(0.6f, 0.5f, 0.4f, 1f);  // Brownish dirt tint

        [Header("Rock/Mountain Textures")]
        public Texture2D rockAlbedo;
        public Texture2D rockNormal;
        public Texture2D rockRoughness;
        [Range(0.5f, 5f)] public float rockTiling = 2f;

        [Header("Snow Textures")]
        public Texture2D snowAlbedo;
        public Texture2D snowNormal;
        [Range(1f, 10f)] public float snowTiling = 4f;
        public Color snowTint = new Color(0.95f, 0.97f, 1f, 1f);

        [Header("Blending Settings")]
        [Range(0.1f, 10f)] public float blendSharpness = 2f;
        [Range(0f, 1f)] public float slopeRockThreshold = 0.5f;
        [Range(0.01f, 0.5f)] public float slopeBlendRange = 0.1f;

        [Header("Output")]
        [SerializeField] private Material generatedMaterial;

        private static readonly int SandAlbedoID = Shader.PropertyToID("_SandAlbedo");
        private static readonly int SandNormalID = Shader.PropertyToID("_SandNormal");
        private static readonly int SandRoughnessID = Shader.PropertyToID("_SandRoughness");
        private static readonly int SandTilingID = Shader.PropertyToID("_SandTiling");

        private static readonly int GrassAlbedoID = Shader.PropertyToID("_GrassAlbedo");
        private static readonly int GrassNormalID = Shader.PropertyToID("_GrassNormal");
        private static readonly int GrassRoughnessID = Shader.PropertyToID("_GrassRoughness");
        private static readonly int GrassTilingID = Shader.PropertyToID("_GrassTiling");
        private static readonly int GrassColorID = Shader.PropertyToID("_GrassColor");

        private static readonly int RockAlbedoID = Shader.PropertyToID("_RockAlbedo");
        private static readonly int RockNormalID = Shader.PropertyToID("_RockNormal");
        private static readonly int RockRoughnessID = Shader.PropertyToID("_RockRoughness");
        private static readonly int RockTilingID = Shader.PropertyToID("_RockTiling");

        private static readonly int SnowAlbedoID = Shader.PropertyToID("_SnowAlbedo");
        private static readonly int SnowNormalID = Shader.PropertyToID("_SnowNormal");
        private static readonly int SnowTilingID = Shader.PropertyToID("_SnowTiling");
        private static readonly int SnowColorID = Shader.PropertyToID("_SnowColor");

        private static readonly int BlendSharpnessID = Shader.PropertyToID("_BlendSharpness");
        private static readonly int SlopeThresholdID = Shader.PropertyToID("_SlopeThreshold");
        private static readonly int SlopeBlendID = Shader.PropertyToID("_SlopeBlend");

        /// <summary>
        /// Create or update the terrain material with current settings.
        /// Call this from Inspector context menu or at runtime.
        /// </summary>
        [ContextMenu("Generate Terrain Material")]
        public void GenerateTerrainMaterial()
        {
            Shader biomeShader = Shader.Find("CreatorWorld/BiomeTerrain");
            if (biomeShader == null)
            {
                Debug.LogError("BiomeTerrain shader not found! Make sure the shader is compiled.");
                return;
            }

            if (generatedMaterial == null)
            {
                generatedMaterial = new Material(biomeShader);
                generatedMaterial.name = "BiomeTerrainMaterial";
            }
            else
            {
                generatedMaterial.shader = biomeShader;
            }

            // Apply sand textures
            if (sandAlbedo != null) generatedMaterial.SetTexture(SandAlbedoID, sandAlbedo);
            if (sandNormal != null) generatedMaterial.SetTexture(SandNormalID, sandNormal);
            if (sandRoughness != null) generatedMaterial.SetTexture(SandRoughnessID, sandRoughness);
            generatedMaterial.SetFloat(SandTilingID, sandTiling);

            // Apply grass textures
            if (grassAlbedo != null) generatedMaterial.SetTexture(GrassAlbedoID, grassAlbedo);
            if (grassNormal != null) generatedMaterial.SetTexture(GrassNormalID, grassNormal);
            if (grassRoughness != null) generatedMaterial.SetTexture(GrassRoughnessID, grassRoughness);
            generatedMaterial.SetFloat(GrassTilingID, grassTiling);
            generatedMaterial.SetColor(GrassColorID, grassTint);

            // Apply rock textures
            if (rockAlbedo != null) generatedMaterial.SetTexture(RockAlbedoID, rockAlbedo);
            if (rockNormal != null) generatedMaterial.SetTexture(RockNormalID, rockNormal);
            if (rockRoughness != null) generatedMaterial.SetTexture(RockRoughnessID, rockRoughness);
            generatedMaterial.SetFloat(RockTilingID, rockTiling);

            // Apply snow textures
            if (snowAlbedo != null) generatedMaterial.SetTexture(SnowAlbedoID, snowAlbedo);
            if (snowNormal != null) generatedMaterial.SetTexture(SnowNormalID, snowNormal);
            generatedMaterial.SetFloat(SnowTilingID, snowTiling);
            generatedMaterial.SetColor(SnowColorID, snowTint);

            // Apply blend settings
            generatedMaterial.SetFloat(BlendSharpnessID, blendSharpness);
            generatedMaterial.SetFloat(SlopeThresholdID, slopeRockThreshold);
            generatedMaterial.SetFloat(SlopeBlendID, slopeBlendRange);

            Debug.Log("Terrain material generated successfully!");
        }

        /// <summary>
        /// Apply the generated material to the ChunkManager.
        /// </summary>
        [ContextMenu("Apply Material to ChunkManager")]
        public void ApplyToChunkManager()
        {
            if (generatedMaterial == null)
            {
                GenerateTerrainMaterial();
            }

            var chunkManager = GetComponent<ChunkManager>();
            if (chunkManager != null)
            {
                // Use reflection to set the private terrainMaterial field
                var field = typeof(ChunkManager).GetField("terrainMaterial",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    field.SetValue(chunkManager, generatedMaterial);
                    Debug.Log("Terrain material applied to ChunkManager!");
                }
            }
        }

        public Material GetMaterial()
        {
            if (generatedMaterial == null)
            {
                GenerateTerrainMaterial();
            }
            return generatedMaterial;
        }
    }
}
