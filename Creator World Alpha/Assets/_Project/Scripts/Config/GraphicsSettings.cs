using UnityEngine;

namespace CreatorWorld.Config
{
    /// <summary>
    /// Graphics quality preset configuration.
    /// Create presets via: Right-click > Create > Config > Game Graphics Settings
    /// </summary>
    [CreateAssetMenu(fileName = "GameGraphicsSettings", menuName = "Config/Game Graphics Settings")]
    public class GameGraphicsSettings : ScriptableObject
    {
        [Header("Preset Info")]
        [Tooltip("Name displayed in UI")]
        public string presetName = "Custom";

        [Header("Grass Settings")]
        [Tooltip("Number of grass instances per terrain chunk")]
        [Range(8192, 524288)]
        public int grassInstancesPerChunk = 65536;

        [Tooltip("Maximum grass view distance in meters")]
        [Range(50f, 300f)]
        public float grassViewDistance = 100f;

        [Tooltip("Enable grass shadow casting (expensive)")]
        public bool grassShadows = false;

        [Tooltip("Enable grass occlusion culling")]
        public bool grassOcclusionCulling = true;

        [Header("Terrain Settings")]
        [Tooltip("View distance in chunks")]
        [Range(2, 6)]
        public int viewDistanceChunks = 3;

        [Tooltip("High detail terrain distance in chunks")]
        [Range(1, 3)]
        public int terrainHighDetailDistance = 1;

        [Tooltip("Medium detail terrain distance in chunks")]
        [Range(1, 4)]
        public int terrainMediumDetailDistance = 2;

        [Header("Shadow Settings")]
        [Tooltip("Shadow quality level (0=Off, 1=Low, 2=Medium, 3=High)")]
        [Range(0, 3)]
        public int shadowQuality = 2;

        [Tooltip("Shadow distance in meters")]
        [Range(20f, 150f)]
        public float shadowDistance = 50f;

        [Header("Rendering")]
        [Tooltip("Render scale (0.5 = half resolution, 1.0 = full)")]
        [Range(0.5f, 1.0f)]
        public float renderScale = 1.0f;

        [Tooltip("Enable Screen Space Ambient Occlusion")]
        public bool ssaoEnabled = true;

        [Tooltip("Enable Bloom post-processing")]
        public bool bloomEnabled = true;

        [Tooltip("Anti-aliasing mode (0=Off, 1=FXAA, 2=SMAA, 3=TAA)")]
        [Range(0, 3)]
        public int antiAliasing = 2;

        [Header("Effects")]
        [Tooltip("Enable fog")]
        public bool fogEnabled = true;

        [Tooltip("Enable ambient particles")]
        public bool ambientParticles = true;

        /// <summary>
        /// Create a copy of these settings for runtime modification
        /// </summary>
        public GameGraphicsSettings CreateRuntimeCopy()
        {
            var copy = CreateInstance<GameGraphicsSettings>();
            copy.presetName = presetName;
            copy.grassInstancesPerChunk = grassInstancesPerChunk;
            copy.grassViewDistance = grassViewDistance;
            copy.grassShadows = grassShadows;
            copy.grassOcclusionCulling = grassOcclusionCulling;
            copy.viewDistanceChunks = viewDistanceChunks;
            copy.terrainHighDetailDistance = terrainHighDetailDistance;
            copy.terrainMediumDetailDistance = terrainMediumDetailDistance;
            copy.shadowQuality = shadowQuality;
            copy.shadowDistance = shadowDistance;
            copy.renderScale = renderScale;
            copy.ssaoEnabled = ssaoEnabled;
            copy.bloomEnabled = bloomEnabled;
            copy.antiAliasing = antiAliasing;
            copy.fogEnabled = fogEnabled;
            copy.ambientParticles = ambientParticles;
            return copy;
        }

        /// <summary>
        /// Copy values from another settings object
        /// </summary>
        public void CopyFrom(GameGraphicsSettings other)
        {
            presetName = other.presetName;
            grassInstancesPerChunk = other.grassInstancesPerChunk;
            grassViewDistance = other.grassViewDistance;
            grassShadows = other.grassShadows;
            grassOcclusionCulling = other.grassOcclusionCulling;
            viewDistanceChunks = other.viewDistanceChunks;
            terrainHighDetailDistance = other.terrainHighDetailDistance;
            terrainMediumDetailDistance = other.terrainMediumDetailDistance;
            shadowQuality = other.shadowQuality;
            shadowDistance = other.shadowDistance;
            renderScale = other.renderScale;
            ssaoEnabled = other.ssaoEnabled;
            bloomEnabled = other.bloomEnabled;
            antiAliasing = other.antiAliasing;
            fogEnabled = other.fogEnabled;
            ambientParticles = other.ambientParticles;
        }
    }
}
