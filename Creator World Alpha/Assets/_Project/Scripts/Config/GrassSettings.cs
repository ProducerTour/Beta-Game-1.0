using UnityEngine;

namespace CreatorWorld.Config
{
    /// <summary>
    /// Configuration for the advanced grass rendering system.
    /// Create via: Right-click > Create > Config > Grass Settings
    /// </summary>
    [CreateAssetMenu(fileName = "GrassSettings", menuName = "Config/Grass Settings")]
    public class GrassSettings : ScriptableObject
    {
        [Header("Density")]
        [Tooltip("Maximum grass instances per terrain chunk")]
        [Range(8192, 524288)]
        public int instancesPerChunk = 262144;

        [Tooltip("Minimum terrain density value to spawn grass (0-1)")]
        [Range(0f, 0.5f)]
        public float minDensityThreshold = 0.1f;

        [Tooltip("Size of sub-chunks for culling (meters)")]
        [Range(8, 64)]
        public int subChunkSize = 16;

        [Header("Scale")]
        [Tooltip("Minimum grass blade scale")]
        public Vector3 scaleMin = new Vector3(0.8f, 0.5f, 0.8f);

        [Tooltip("Maximum grass blade scale")]
        public Vector3 scaleMax = new Vector3(1.2f, 1.5f, 1.2f);

        [Tooltip("Scale multiplier based on density (denser = taller)")]
        [Range(0f, 1f)]
        public float densityScaleInfluence = 0.3f;

        [Header("LOD Distances")]
        [Tooltip("Maximum view distance for grass (meters)")]
        [Range(50f, 300f)]
        public float maxViewDistance = 150f;

        [Tooltip("Distance ratio for LOD1 switch (0-1)")]
        [Range(0.1f, 0.5f)]
        public float lod1Threshold = 0.25f;

        [Tooltip("Distance ratio for LOD2 switch (0-1)")]
        [Range(0.3f, 0.8f)]
        public float lod2Threshold = 0.5f;

        [Header("Distance Fade")]
        [Tooltip("Distance ratio where fade starts (0-1)")]
        [Range(0.3f, 0.9f)]
        public float fadeStart = 0.7f;

        [Tooltip("Distance ratio where fade ends (0-1)")]
        [Range(0.5f, 1f)]
        public float fadeEnd = 1.0f;

        [Header("Wind Animation")]
        [Tooltip("Wind strength (amplitude of sway)")]
        [Range(0f, 2f)]
        public float windStrength = 0.5f;

        [Tooltip("Wind animation speed")]
        [Range(0f, 5f)]
        public float windSpeed = 1.0f;

        [Tooltip("Wind direction (normalized XZ)")]
        public Vector2 windDirection = new Vector2(1f, 0.5f);

        [Tooltip("Noise scale for wind variation")]
        [Range(0.01f, 1f)]
        public float windNoiseScale = 0.1f;

        [Tooltip("Minimum height for wind deformation (base of blade)")]
        [Range(0f, 0.5f)]
        public float windDeformationMin = 0f;

        [Tooltip("Maximum height for wind deformation (tip of blade)")]
        [Range(0.5f, 2f)]
        public float windDeformationMax = 1.0f;

        [Header("Appearance")]
        [Tooltip("Color at the base of grass blades")]
        public Color baseColor = new Color(0.1f, 0.2f, 0.05f);

        [Tooltip("Color at the tip of grass blades")]
        public Color tipColor = new Color(0.4f, 0.8f, 0.2f);

        [Tooltip("Ambient occlusion tint color")]
        public Color aoColor = new Color(0.1f, 0.15f, 0.05f);

        [Tooltip("Alpha cutoff for transparency")]
        [Range(0.1f, 0.9f)]
        public float alphaCutoff = 0.5f;

        [Tooltip("Minimum brightness (prevents pure black)")]
        [Range(0f, 1f)]
        public float minBrightness = 0.3f;

        [Tooltip("Shadow brightness multiplier")]
        [Range(0f, 1f)]
        public float shadowBrightness = 0.2f;

        [Header("Performance")]
        [Tooltip("Enable shadow casting for grass")]
        public bool castShadows = false;

        [Tooltip("Enable occlusion culling using depth buffer")]
        public bool useOcclusionCulling = true;

        [Tooltip("Depth bias for occlusion culling")]
        [Range(0.00001f, 0.001f)]
        public float occlusionDepthBias = 0.0001f;

        [Header("Meshes")]
        [Tooltip("High detail grass mesh (LOD0)")]
        public Mesh lod0Mesh;

        [Tooltip("Medium detail grass mesh (LOD1)")]
        public Mesh lod1Mesh;

        [Tooltip("Low detail grass mesh (LOD2)")]
        public Mesh lod2Mesh;

        /// <summary>
        /// Calculated number of sub-chunks per chunk side
        /// </summary>
        public int SubChunksPerSide(int chunkSize) => Mathf.CeilToInt((float)chunkSize / subChunkSize);

        /// <summary>
        /// Total sub-chunks per terrain chunk
        /// </summary>
        public int TotalSubChunks(int chunkSize)
        {
            int perSide = SubChunksPerSide(chunkSize);
            return perSide * perSide;
        }

        /// <summary>
        /// Get normalized wind direction
        /// </summary>
        public Vector2 NormalizedWindDirection => windDirection.normalized;

        /// <summary>
        /// Calculate LOD index based on distance ratio
        /// </summary>
        public int GetLODIndex(float distanceRatio)
        {
            if (distanceRatio < lod1Threshold) return 0;
            if (distanceRatio < lod2Threshold) return 1;
            return 2;
        }

        /// <summary>
        /// Calculate fade alpha based on distance ratio
        /// </summary>
        public float GetFadeAlpha(float distanceRatio)
        {
            if (distanceRatio < fadeStart) return 1f;
            if (distanceRatio > fadeEnd) return 0f;
            return 1f - Mathf.InverseLerp(fadeStart, fadeEnd, distanceRatio);
        }
    }
}
