using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CreatorWorld.Config;
using System;

namespace CreatorWorld.Core
{
    /// <summary>
    /// Manages graphics settings at runtime.
    /// Singleton that persists across scenes.
    /// </summary>
    public class GraphicsManager : MonoBehaviour
    {
        public static GraphicsManager Instance { get; private set; }

        [Header("Presets")]
        [SerializeField] private GameGraphicsSettings lowPreset;
        [SerializeField] private GameGraphicsSettings mediumPreset;
        [SerializeField] private GameGraphicsSettings highPreset;
        [SerializeField] private GameGraphicsSettings ultraPreset;

        [Header("Current Settings")]
        [SerializeField] private GameGraphicsSettings currentSettings;

        [Header("References")]
        [SerializeField] private UniversalRenderPipelineAsset urpAsset;
        [SerializeField] private VolumeProfile volumeProfile;

        // Events for systems to respond to settings changes
        public static event Action<GameGraphicsSettings> OnSettingsChanged;
        public static event Action<int> OnGrassQualityChanged;
        public static event Action<int> OnTerrainQualityChanged;

        // PlayerPrefs keys
        private const string PREFS_PRESET = "graphics_preset";
        private const string PREFS_GRASS_INSTANCES = "graphics_grass_instances";
        private const string PREFS_GRASS_DISTANCE = "graphics_grass_distance";
        private const string PREFS_VIEW_DISTANCE = "graphics_view_distance";
        private const string PREFS_SHADOW_QUALITY = "graphics_shadow_quality";
        private const string PREFS_RENDER_SCALE = "graphics_render_scale";

        public GameGraphicsSettings CurrentSettings => currentSettings;
        public GameGraphicsSettings LowPreset => lowPreset;
        public GameGraphicsSettings MediumPreset => mediumPreset;
        public GameGraphicsSettings HighPreset => highPreset;
        public GameGraphicsSettings UltraPreset => ultraPreset;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Create runtime settings copy
            if (currentSettings == null && mediumPreset != null)
            {
                currentSettings = mediumPreset.CreateRuntimeCopy();
            }

            LoadSettings();
        }

        private void Start()
        {
            ApplyAllSettings();
        }

        /// <summary>
        /// Apply a preset by name
        /// </summary>
        public void ApplyPreset(GraphicsPreset preset)
        {
            GameGraphicsSettings source = preset switch
            {
                GraphicsPreset.Low => lowPreset,
                GraphicsPreset.Medium => mediumPreset,
                GraphicsPreset.High => highPreset,
                GraphicsPreset.Ultra => ultraPreset,
                _ => mediumPreset
            };

            if (source != null && currentSettings != null)
            {
                currentSettings.CopyFrom(source);
                ApplyAllSettings();
                SaveSettings((int)preset);
            }
        }

        /// <summary>
        /// Apply all current settings to game systems
        /// </summary>
        public void ApplyAllSettings()
        {
            if (currentSettings == null) return;

            ApplyRenderSettings();
            ApplyShadowSettings();
            ApplyPostProcessing();

            // Notify all listeners
            OnSettingsChanged?.Invoke(currentSettings);
            OnGrassQualityChanged?.Invoke(currentSettings.grassInstancesPerChunk);
            OnTerrainQualityChanged?.Invoke(currentSettings.viewDistanceChunks);

            Debug.Log($"[GraphicsManager] Applied settings: {currentSettings.presetName}");
        }

        private void ApplyRenderSettings()
        {
            if (urpAsset == null)
            {
                urpAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            }

            if (urpAsset != null)
            {
                urpAsset.renderScale = currentSettings.renderScale;

                // Anti-aliasing
                urpAsset.msaaSampleCount = currentSettings.antiAliasing switch
                {
                    0 => 1,  // Off
                    1 => 2,  // 2x
                    2 => 4,  // 4x
                    3 => 8,  // 8x
                    _ => 4
                };
            }

            // Fog
            RenderSettings.fog = currentSettings.fogEnabled;
        }

        private void ApplyShadowSettings()
        {
            if (urpAsset == null) return;

            urpAsset.shadowDistance = currentSettings.shadowDistance;

            // Shadow resolution based on quality
            // Note: In newer URP versions, shadow settings are configured in the URP Asset directly
            // These quality settings affect shadow distance which is still modifiable at runtime
            switch (currentSettings.shadowQuality)
            {
                case 0: // Off - minimum shadow distance
                    urpAsset.shadowDistance = 0f;
                    break;
                case 1: // Low
                    urpAsset.shadowDistance = Mathf.Min(currentSettings.shadowDistance, 30f);
                    break;
                case 2: // Medium
                    urpAsset.shadowDistance = Mathf.Min(currentSettings.shadowDistance, 70f);
                    break;
                case 3: // High
                    urpAsset.shadowDistance = currentSettings.shadowDistance;
                    break;
            }
        }

        private void ApplyPostProcessing()
        {
            if (volumeProfile == null) return;

            // Note: SSAO in URP is a renderer feature, not a volume override
            // To toggle SSAO at runtime, you need to disable/enable the renderer feature
            // This is typically done through the Renderer Asset, not the Volume Profile

            // Bloom is a volume override
            if (volumeProfile.TryGet<Bloom>(out var bloom))
            {
                bloom.active = currentSettings.bloomEnabled;
            }
        }

        /// <summary>
        /// Set grass instances per chunk (runtime adjustment)
        /// </summary>
        public void SetGrassQuality(int instancesPerChunk)
        {
            currentSettings.grassInstancesPerChunk = Mathf.Clamp(instancesPerChunk, 8192, 524288);
            OnGrassQualityChanged?.Invoke(currentSettings.grassInstancesPerChunk);
            PlayerPrefs.SetInt(PREFS_GRASS_INSTANCES, currentSettings.grassInstancesPerChunk);
        }

        /// <summary>
        /// Set grass view distance (runtime adjustment)
        /// </summary>
        public void SetGrassViewDistance(float distance)
        {
            currentSettings.grassViewDistance = Mathf.Clamp(distance, 50f, 300f);
            OnSettingsChanged?.Invoke(currentSettings);
            PlayerPrefs.SetFloat(PREFS_GRASS_DISTANCE, currentSettings.grassViewDistance);
        }

        /// <summary>
        /// Set terrain view distance in chunks (runtime adjustment)
        /// </summary>
        public void SetViewDistance(int chunks)
        {
            currentSettings.viewDistanceChunks = Mathf.Clamp(chunks, 2, 6);
            OnTerrainQualityChanged?.Invoke(currentSettings.viewDistanceChunks);
            PlayerPrefs.SetInt(PREFS_VIEW_DISTANCE, currentSettings.viewDistanceChunks);
        }

        /// <summary>
        /// Set render scale (runtime adjustment)
        /// </summary>
        public void SetRenderScale(float scale)
        {
            currentSettings.renderScale = Mathf.Clamp(scale, 0.5f, 1.0f);
            ApplyRenderSettings();
            PlayerPrefs.SetFloat(PREFS_RENDER_SCALE, currentSettings.renderScale);
        }

        /// <summary>
        /// Set shadow quality (runtime adjustment)
        /// </summary>
        public void SetShadowQuality(int quality)
        {
            currentSettings.shadowQuality = Mathf.Clamp(quality, 0, 3);
            ApplyShadowSettings();
            PlayerPrefs.SetInt(PREFS_SHADOW_QUALITY, currentSettings.shadowQuality);
        }

        private void SaveSettings(int presetIndex = -1)
        {
            if (presetIndex >= 0)
            {
                PlayerPrefs.SetInt(PREFS_PRESET, presetIndex);
            }

            PlayerPrefs.SetInt(PREFS_GRASS_INSTANCES, currentSettings.grassInstancesPerChunk);
            PlayerPrefs.SetFloat(PREFS_GRASS_DISTANCE, currentSettings.grassViewDistance);
            PlayerPrefs.SetInt(PREFS_VIEW_DISTANCE, currentSettings.viewDistanceChunks);
            PlayerPrefs.SetInt(PREFS_SHADOW_QUALITY, currentSettings.shadowQuality);
            PlayerPrefs.SetFloat(PREFS_RENDER_SCALE, currentSettings.renderScale);

            PlayerPrefs.Save();
        }

        private void LoadSettings()
        {
            if (currentSettings == null) return;

            // Load preset first
            int presetIndex = PlayerPrefs.GetInt(PREFS_PRESET, 1); // Default to Medium
            GameGraphicsSettings basePreset = presetIndex switch
            {
                0 => lowPreset,
                1 => mediumPreset,
                2 => highPreset,
                3 => ultraPreset,
                _ => mediumPreset
            };

            if (basePreset != null)
            {
                currentSettings.CopyFrom(basePreset);
            }

            // Override with any custom values
            if (PlayerPrefs.HasKey(PREFS_GRASS_INSTANCES))
            {
                currentSettings.grassInstancesPerChunk = PlayerPrefs.GetInt(PREFS_GRASS_INSTANCES);
            }
            if (PlayerPrefs.HasKey(PREFS_GRASS_DISTANCE))
            {
                currentSettings.grassViewDistance = PlayerPrefs.GetFloat(PREFS_GRASS_DISTANCE);
            }
            if (PlayerPrefs.HasKey(PREFS_VIEW_DISTANCE))
            {
                currentSettings.viewDistanceChunks = PlayerPrefs.GetInt(PREFS_VIEW_DISTANCE);
            }
            if (PlayerPrefs.HasKey(PREFS_SHADOW_QUALITY))
            {
                currentSettings.shadowQuality = PlayerPrefs.GetInt(PREFS_SHADOW_QUALITY);
            }
            if (PlayerPrefs.HasKey(PREFS_RENDER_SCALE))
            {
                currentSettings.renderScale = PlayerPrefs.GetFloat(PREFS_RENDER_SCALE);
            }
        }

        /// <summary>
        /// Reset to default preset (Medium)
        /// </summary>
        public void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(PREFS_PRESET);
            PlayerPrefs.DeleteKey(PREFS_GRASS_INSTANCES);
            PlayerPrefs.DeleteKey(PREFS_GRASS_DISTANCE);
            PlayerPrefs.DeleteKey(PREFS_VIEW_DISTANCE);
            PlayerPrefs.DeleteKey(PREFS_SHADOW_QUALITY);
            PlayerPrefs.DeleteKey(PREFS_RENDER_SCALE);

            ApplyPreset(GraphicsPreset.Medium);
        }

        /// <summary>
        /// Get current preset index (0=Low, 1=Medium, 2=High, 3=Ultra, 4=Custom)
        /// </summary>
        public int GetCurrentPresetIndex()
        {
            return PlayerPrefs.GetInt(PREFS_PRESET, 1);
        }
    }

    public enum GraphicsPreset
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Ultra = 3,
        Custom = 4
    }
}
