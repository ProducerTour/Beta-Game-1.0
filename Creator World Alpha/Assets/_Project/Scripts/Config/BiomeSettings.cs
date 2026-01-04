using UnityEngine;

namespace CreatorWorld.Config
{
    /// <summary>
    /// Configurable biome and terrain generation settings.
    /// Create assets via: Right-click > Create > Config > Biome Settings
    /// </summary>
    [CreateAssetMenu(fileName = "BiomeSettings", menuName = "Config/Biome Settings")]
    public class BiomeSettings : ScriptableObject
    {
        [Header("World Size")]
        [Tooltip("Total map size in meters (square map)")]
        public float MapSize = 2048f;

        [Header("Island Falloff")]
        [Tooltip("Radius from center where land is fully intact")]
        public float IslandFalloffStart = 800f;
        [Tooltip("Radius from center where ocean begins")]
        public float IslandFalloffEnd = 1024f;
        [Tooltip("How deep below water level at map edges")]
        public float IslandFalloffDepth = 50f;

        [Header("Biome Zones (Z-axis latitude)")]
        [Tooltip("Z coordinate where pure desert zone ends (south of map)")]
        public float DesertZoneEnd = 0f;
        [Tooltip("Z coordinate where desert-to-grass transition ends")]
        public float DesertTransitionEnd = 128f;
        [Tooltip("Z coordinate where grassland zone ends")]
        public float GrassZoneEnd = 1400f;
        [Tooltip("Z coordinate where grass-to-snow transition ends (snow beyond this)")]
        public float GrassToSnowEnd = 1600f;

        [Header("Altitude Snow Override")]
        [Tooltip("Height where snow starts appearing on mountains regardless of latitude")]
        public float SnowAltitudeThreshold = 60f;
        [Tooltip("Height where full snow coverage applies on mountains")]
        public float SnowAltitudeFullThreshold = 80f;

        [Header("Terrain Heights")]
        [Tooltip("Y coordinate of water surface")]
        public float WaterLevel = 0f;
        [Tooltip("Base height offset to raise terrain above water")]
        public float HeightOffset = 15f;

        [Header("Noise Scales")]
        [Tooltip("Scale for base terrain (large rolling hills)")]
        [Range(0.001f, 0.05f)]
        public float BaseScale = 0.005f;
        [Tooltip("Scale for detail noise (small bumps)")]
        [Range(0.005f, 0.1f)]
        public float DetailScale = 0.02f;
        [Tooltip("Scale for mountain regions")]
        [Range(0.001f, 0.01f)]
        public float MountainScale = 0.002f;
        [Tooltip("Scale for ridge noise (sharp peaks)")]
        [Range(0.002f, 0.02f)]
        public float RidgeScale = 0.008f;

        [Header("Amplitude Settings")]
        [Tooltip("Height amplitude for base terrain")]
        [Range(10f, 100f)]
        public float BaseAmplitude = 30f;
        [Tooltip("Height amplitude for detail noise")]
        [Range(1f, 20f)]
        public float DetailAmplitude = 5f;
        [Tooltip("Height amplitude for mountains")]
        [Range(50f, 200f)]
        public float MountainAmplitude = 100f;
        [Tooltip("Height amplitude for ridges")]
        [Range(10f, 80f)]
        public float RidgeAmplitude = 40f;

        [Header("Rock Blending")]
        [Tooltip("Height where rock starts blending in")]
        public float RockBlendStart = 20f;
        [Tooltip("Height where rock blend reaches full strength")]
        public float RockBlendEnd = 50f;

        /// <summary>
        /// Map center X coordinate (calculated from MapSize)
        /// </summary>
        public float MapCenterX => MapSize / 2f;

        /// <summary>
        /// Map center Z coordinate (calculated from MapSize)
        /// </summary>
        public float MapCenterZ => MapSize / 2f;
    }
}
