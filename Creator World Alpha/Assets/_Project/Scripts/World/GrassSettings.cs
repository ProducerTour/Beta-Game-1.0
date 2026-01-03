using UnityEngine;

namespace CreatorWorld.World
{
    /// <summary>
    /// ScriptableObject for grass rendering settings.
    /// Create presets for different biomes or quality levels.
    /// </summary>
    [CreateAssetMenu(fileName = "GrassSettings", menuName = "Creator World/World/Grass Settings")]
    public class GrassSettings : ScriptableObject
    {
        [Header("Density & Area")]
        [Tooltip("Grass blades per square meter")]
        [Range(1, 50)]
        public int grassPerMeter = 8;

        [Tooltip("Radius around camera to render grass")]
        [Range(10, 200)]
        public float renderRadius = 50f;

        [Tooltip("Maximum render distance")]
        [Range(20, 300)]
        public float maxRenderDistance = 100f;

        [Tooltip("Maximum grass blades to render")]
        public int maxGrassBlades = 500000;

        [Header("Terrain Integration")]
        [Tooltip("Seed for terrain generation (must match TerrainGenerator)")]
        public int terrainSeed = 12345;

        [Tooltip("Maximum slope angle for grass placement (degrees)")]
        [Range(0f, 90f)]
        public float maxSlope = 40f;

        [Header("Appearance")]
        [ColorUsage(false, false)]
        public Color baseColor = new Color(0.1f, 0.35f, 0.1f);

        [ColorUsage(false, false)]
        public Color tipColor = new Color(0.45f, 0.6f, 0.25f);

        [Range(0.01f, 0.1f)]
        public float bladeWidth = 0.04f;

        [Range(0.1f, 1.5f)]
        public float bladeHeight = 0.5f;

        [Range(0f, 1f)]
        public float bendAmount = 0.3f;

        [Header("Wind")]
        public Vector2 windDirection = new Vector2(1f, 0.3f);

        [Range(0f, 2f)]
        public float windStrength = 0.5f;

        [Range(0.01f, 1f)]
        public float windFrequency = 0.1f;

        /// <summary>
        /// Apply these settings to a grass renderer.
        /// </summary>
        public void ApplyTo(ProceduralGrassRenderer renderer)
        {
            if (renderer == null) return;

            renderer.SetWindDirection(windDirection);
            renderer.SetWindStrength(windStrength);
            renderer.SetGrassColors(baseColor, tipColor);
            renderer.SetRenderDistance(maxRenderDistance);
        }

        /// <summary>
        /// Get estimated grass count for these settings.
        /// </summary>
        public int GetEstimatedGrassCount()
        {
            float area = renderRadius * renderRadius * 4f; // Square area
            return Mathf.Min(Mathf.CeilToInt(area * grassPerMeter * grassPerMeter), maxGrassBlades);
        }

        /// <summary>
        /// Get estimated memory usage in MB.
        /// </summary>
        public float GetEstimatedMemoryMB()
        {
            int grassCount = GetEstimatedGrassCount();
            int bytesPerGrass = 52; // GrassData struct size (12 floats + 1 uint)
            return (grassCount * bytesPerGrass) / (1024f * 1024f);
        }
    }
}
