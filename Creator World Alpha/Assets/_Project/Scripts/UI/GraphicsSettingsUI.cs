using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CreatorWorld.Core;
using CreatorWorld.Config;

namespace CreatorWorld.UI
{
    /// <summary>
    /// UI for adjusting graphics settings in the pause menu.
    /// </summary>
    public class GraphicsSettingsUI : MonoBehaviour
    {
        [Header("Preset Selection")]
        [SerializeField] private TMP_Dropdown presetDropdown;

        [Header("Grass Settings")]
        [SerializeField] private Slider grassDensitySlider;
        [SerializeField] private TMP_Text grassDensityLabel;
        [SerializeField] private Slider grassDistanceSlider;
        [SerializeField] private TMP_Text grassDistanceLabel;
        [SerializeField] private Toggle grassShadowsToggle;

        [Header("Terrain Settings")]
        [SerializeField] private Slider viewDistanceSlider;
        [SerializeField] private TMP_Text viewDistanceLabel;

        [Header("Rendering")]
        [SerializeField] private Slider renderScaleSlider;
        [SerializeField] private TMP_Text renderScaleLabel;
        [SerializeField] private TMP_Dropdown shadowQualityDropdown;
        [SerializeField] private Toggle ssaoToggle;
        [SerializeField] private Toggle bloomToggle;

        [Header("Buttons")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetButton;

        private bool isInitializing;

        private void OnEnable()
        {
            InitializeUI();
            LoadCurrentSettings();
        }

        private void InitializeUI()
        {
            isInitializing = true;

            // Preset dropdown
            if (presetDropdown != null)
            {
                presetDropdown.ClearOptions();
                presetDropdown.AddOptions(new System.Collections.Generic.List<string>
                {
                    "Low", "Medium", "High", "Ultra", "Custom"
                });
                presetDropdown.onValueChanged.AddListener(OnPresetChanged);
            }

            // Grass density slider (8k to 256k)
            if (grassDensitySlider != null)
            {
                grassDensitySlider.minValue = 0;
                grassDensitySlider.maxValue = 4;
                grassDensitySlider.wholeNumbers = true;
                grassDensitySlider.onValueChanged.AddListener(OnGrassDensityChanged);
            }

            // Grass distance slider
            if (grassDistanceSlider != null)
            {
                grassDistanceSlider.minValue = 50;
                grassDistanceSlider.maxValue = 300;
                grassDistanceSlider.onValueChanged.AddListener(OnGrassDistanceChanged);
            }

            // View distance slider
            if (viewDistanceSlider != null)
            {
                viewDistanceSlider.minValue = 2;
                viewDistanceSlider.maxValue = 6;
                viewDistanceSlider.wholeNumbers = true;
                viewDistanceSlider.onValueChanged.AddListener(OnViewDistanceChanged);
            }

            // Render scale slider
            if (renderScaleSlider != null)
            {
                renderScaleSlider.minValue = 0.5f;
                renderScaleSlider.maxValue = 1.0f;
                renderScaleSlider.onValueChanged.AddListener(OnRenderScaleChanged);
            }

            // Shadow quality dropdown
            if (shadowQualityDropdown != null)
            {
                shadowQualityDropdown.ClearOptions();
                shadowQualityDropdown.AddOptions(new System.Collections.Generic.List<string>
                {
                    "Off", "Low", "Medium", "High"
                });
                shadowQualityDropdown.onValueChanged.AddListener(OnShadowQualityChanged);
            }

            // Toggles
            if (grassShadowsToggle != null)
                grassShadowsToggle.onValueChanged.AddListener(OnGrassShadowsChanged);
            if (ssaoToggle != null)
                ssaoToggle.onValueChanged.AddListener(OnSSAOChanged);
            if (bloomToggle != null)
                bloomToggle.onValueChanged.AddListener(OnBloomChanged);

            // Buttons
            if (applyButton != null)
                applyButton.onClick.AddListener(ApplySettings);
            if (resetButton != null)
                resetButton.onClick.AddListener(ResetToDefaults);

            isInitializing = false;
        }

        private void LoadCurrentSettings()
        {
            if (GraphicsManager.Instance == null) return;

            isInitializing = true;

            var settings = GraphicsManager.Instance.CurrentSettings;
            if (settings == null) return;

            // Preset
            if (presetDropdown != null)
            {
                presetDropdown.value = GraphicsManager.Instance.GetCurrentPresetIndex();
            }

            // Grass density (convert to slider index)
            if (grassDensitySlider != null)
            {
                int index = GrassInstancesToSliderIndex(settings.grassInstancesPerChunk);
                grassDensitySlider.value = index;
                UpdateGrassDensityLabel(index);
            }

            // Grass distance
            if (grassDistanceSlider != null)
            {
                grassDistanceSlider.value = settings.grassViewDistance;
                UpdateGrassDistanceLabel(settings.grassViewDistance);
            }

            // Grass shadows
            if (grassShadowsToggle != null)
                grassShadowsToggle.isOn = settings.grassShadows;

            // View distance
            if (viewDistanceSlider != null)
            {
                viewDistanceSlider.value = settings.viewDistanceChunks;
                UpdateViewDistanceLabel(settings.viewDistanceChunks);
            }

            // Render scale
            if (renderScaleSlider != null)
            {
                renderScaleSlider.value = settings.renderScale;
                UpdateRenderScaleLabel(settings.renderScale);
            }

            // Shadow quality
            if (shadowQualityDropdown != null)
                shadowQualityDropdown.value = settings.shadowQuality;

            // Post processing
            if (ssaoToggle != null)
                ssaoToggle.isOn = settings.ssaoEnabled;
            if (bloomToggle != null)
                bloomToggle.isOn = settings.bloomEnabled;

            isInitializing = false;
        }

        private void OnPresetChanged(int index)
        {
            if (isInitializing || GraphicsManager.Instance == null) return;

            if (index < 4) // Not custom
            {
                GraphicsManager.Instance.ApplyPreset((GraphicsPreset)index);
                LoadCurrentSettings(); // Refresh UI to match preset
            }
        }

        private void OnGrassDensityChanged(float value)
        {
            int index = Mathf.RoundToInt(value);
            UpdateGrassDensityLabel(index);

            if (!isInitializing && GraphicsManager.Instance != null)
            {
                int instances = SliderIndexToGrassInstances(index);
                GraphicsManager.Instance.SetGrassQuality(instances);
                MarkAsCustom();
            }
        }

        private void OnGrassDistanceChanged(float value)
        {
            UpdateGrassDistanceLabel(value);

            if (!isInitializing && GraphicsManager.Instance != null)
            {
                GraphicsManager.Instance.SetGrassViewDistance(value);
                MarkAsCustom();
            }
        }

        private void OnGrassShadowsChanged(bool value)
        {
            if (!isInitializing && GraphicsManager.Instance != null)
            {
                GraphicsManager.Instance.CurrentSettings.grassShadows = value;
                MarkAsCustom();
            }
        }

        private void OnViewDistanceChanged(float value)
        {
            int chunks = Mathf.RoundToInt(value);
            UpdateViewDistanceLabel(chunks);

            if (!isInitializing && GraphicsManager.Instance != null)
            {
                GraphicsManager.Instance.SetViewDistance(chunks);
                MarkAsCustom();
            }
        }

        private void OnRenderScaleChanged(float value)
        {
            UpdateRenderScaleLabel(value);

            if (!isInitializing && GraphicsManager.Instance != null)
            {
                GraphicsManager.Instance.SetRenderScale(value);
                MarkAsCustom();
            }
        }

        private void OnShadowQualityChanged(int value)
        {
            if (!isInitializing && GraphicsManager.Instance != null)
            {
                GraphicsManager.Instance.SetShadowQuality(value);
                MarkAsCustom();
            }
        }

        private void OnSSAOChanged(bool value)
        {
            if (!isInitializing && GraphicsManager.Instance != null)
            {
                GraphicsManager.Instance.CurrentSettings.ssaoEnabled = value;
                GraphicsManager.Instance.ApplyAllSettings();
                MarkAsCustom();
            }
        }

        private void OnBloomChanged(bool value)
        {
            if (!isInitializing && GraphicsManager.Instance != null)
            {
                GraphicsManager.Instance.CurrentSettings.bloomEnabled = value;
                GraphicsManager.Instance.ApplyAllSettings();
                MarkAsCustom();
            }
        }

        private void MarkAsCustom()
        {
            if (presetDropdown != null)
            {
                isInitializing = true;
                presetDropdown.value = 4; // Custom
                isInitializing = false;
            }
        }

        public void ApplySettings()
        {
            if (GraphicsManager.Instance != null)
            {
                GraphicsManager.Instance.ApplyAllSettings();
            }
        }

        public void ResetToDefaults()
        {
            if (GraphicsManager.Instance != null)
            {
                GraphicsManager.Instance.ResetToDefaults();
                LoadCurrentSettings();
            }
        }

        #region Label Updates

        private void UpdateGrassDensityLabel(int index)
        {
            if (grassDensityLabel == null) return;

            string label = index switch
            {
                0 => "8K (Low)",
                1 => "32K (Medium)",
                2 => "65K (High)",
                3 => "150K (Very High)",
                4 => "256K (Ultra)",
                _ => "Unknown"
            };

            grassDensityLabel.text = label;
        }

        private void UpdateGrassDistanceLabel(float value)
        {
            if (grassDistanceLabel == null) return;
            grassDistanceLabel.text = $"{value:F0}m";
        }

        private void UpdateViewDistanceLabel(int chunks)
        {
            if (viewDistanceLabel == null) return;
            int meters = chunks * 64; // Assuming 64m chunks
            viewDistanceLabel.text = $"{chunks} chunks ({meters}m)";
        }

        private void UpdateRenderScaleLabel(float value)
        {
            if (renderScaleLabel == null) return;
            int percent = Mathf.RoundToInt(value * 100);
            renderScaleLabel.text = $"{percent}%";
        }

        #endregion

        #region Conversion Helpers

        private int GrassInstancesToSliderIndex(int instances)
        {
            if (instances <= 8192) return 0;
            if (instances <= 32768) return 1;
            if (instances <= 65536) return 2;
            if (instances <= 150000) return 3;
            return 4;
        }

        private int SliderIndexToGrassInstances(int index)
        {
            return index switch
            {
                0 => 8192,
                1 => 32768,
                2 => 65536,
                3 => 150000,
                4 => 262144,
                _ => 65536
            };
        }

        #endregion
    }
}
