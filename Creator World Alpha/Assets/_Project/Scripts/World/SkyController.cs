using UnityEngine;
using CreatorWorld.Config;

namespace CreatorWorld.World
{
    /// <summary>
    /// Updates the stylized sky shader based on time of day.
    /// Attach to any GameObject in the scene.
    /// </summary>
    public class SkyController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Material skyMaterial;
        [SerializeField] private TimeOfDaySettings timeSettings;

        [Header("Cloud Settings")]
        [SerializeField] [Range(0f, 1f)] private float baseClouds = 0.2f;
        [SerializeField] private float cloudSpeed = 0.02f;

        [Header("Star Settings")]
        [SerializeField] [Range(0f, 1f)] private float starsThreshold = 0.4f;
        [SerializeField] [Range(100f, 500f)] private float starsDensity = 300f;

        // Shader property IDs for performance
        private static readonly int SkyTopColorID = Shader.PropertyToID("_SkyTopColor");
        private static readonly int SkyHorizonColorID = Shader.PropertyToID("_SkyHorizonColor");
        private static readonly int SunColorID = Shader.PropertyToID("_SunColor");
        private static readonly int SunDirectionID = Shader.PropertyToID("_SunDirection");
        private static readonly int SunIntensityID = Shader.PropertyToID("_SunIntensity");
        private static readonly int MoonDirectionID = Shader.PropertyToID("_MoonDirection");
        private static readonly int MoonIntensityID = Shader.PropertyToID("_MoonIntensity");
        private static readonly int NightFactorID = Shader.PropertyToID("_NightFactor");
        private static readonly int StarsIntensityID = Shader.PropertyToID("_StarsIntensity");
        private static readonly int CloudCoverageID = Shader.PropertyToID("_CloudCoverage");
        private static readonly int CloudSpeedID = Shader.PropertyToID("_CloudSpeed");
        private static readonly int SunsetGlowIntensityID = Shader.PropertyToID("_SunsetGlowIntensity");
        private static readonly int StarsThresholdID = Shader.PropertyToID("_StarsThreshold");
        private static readonly int StarsDensityID = Shader.PropertyToID("_StarsDensity");

        private float targetCloudCoverage;
        private float currentCloudCoverage;

        // Throttle DynamicGI updates - very expensive operation
        private float lastGIUpdateTime;
        private const float GI_UPDATE_INTERVAL = 2f; // Only update GI every 2 seconds

        private void OnEnable()
        {
            TimeOfDayManager.OnTimeChanged += UpdateSky;
            targetCloudCoverage = baseClouds;
            currentCloudCoverage = baseClouds;
        }

        private void OnDisable()
        {
            TimeOfDayManager.OnTimeChanged -= UpdateSky;
        }

        private void Start()
        {
            // Set skybox material
            if (skyMaterial != null)
            {
                RenderSettings.skybox = skyMaterial;

                // Set initial star settings
                skyMaterial.SetFloat(StarsThresholdID, starsThreshold);
                skyMaterial.SetFloat(StarsDensityID, starsDensity);
            }

            // Initial update if TimeOfDayManager exists
            if (TimeOfDayManager.Instance != null)
            {
                UpdateSky(TimeOfDayManager.Instance.CurrentTime);
            }
        }

        private void Update()
        {
            // Smoothly interpolate cloud coverage
            currentCloudCoverage = Mathf.Lerp(currentCloudCoverage, targetCloudCoverage, Time.deltaTime * 0.5f);

            if (skyMaterial != null)
            {
                skyMaterial.SetFloat(CloudCoverageID, currentCloudCoverage);
                skyMaterial.SetFloat(CloudSpeedID, cloudSpeed);
            }
        }

        private void UpdateSky(float normalizedTime)
        {
            if (skyMaterial == null || timeSettings == null) return;

            // Sky colors from gradients
            Color skyTop = timeSettings.skyTopColor.Evaluate(normalizedTime);
            Color skyHorizon = timeSettings.skyHorizonColor.Evaluate(normalizedTime);
            Color sunColor = timeSettings.sunDiscColor.Evaluate(normalizedTime);

            skyMaterial.SetColor(SkyTopColorID, skyTop);
            skyMaterial.SetColor(SkyHorizonColorID, skyHorizon);
            skyMaterial.SetColor(SunColorID, sunColor);

            // Sun direction from TimeOfDayManager
            if (TimeOfDayManager.Instance != null)
            {
                Vector3 sunDir = TimeOfDayManager.Instance.GetSunDirection();
                skyMaterial.SetVector(SunDirectionID, new Vector4(sunDir.x, sunDir.y, sunDir.z, 0));

                Vector3 moonDir = TimeOfDayManager.Instance.GetMoonDirection();
                skyMaterial.SetVector(MoonDirectionID, new Vector4(moonDir.x, moonDir.y, moonDir.z, 0));
            }

            // Night factor for stars and moon
            float nightFactor = timeSettings.GetNightFactor(normalizedTime);
            skyMaterial.SetFloat(NightFactorID, nightFactor);
            skyMaterial.SetFloat(StarsIntensityID, timeSettings.starIntensity * nightFactor);
            skyMaterial.SetFloat(MoonIntensityID, nightFactor);

            // Sun intensity (reduced at sunrise/sunset)
            float sunAltitude = timeSettings.GetSunAltitudeFactor(normalizedTime);
            float sunIntensity = Mathf.Lerp(0.3f, 1f, sunAltitude);
            skyMaterial.SetFloat(SunIntensityID, sunIntensity);

            // Sunset glow intensity
            float sunsetGlow = 0f;
            if (timeSettings.IsSunrise(normalizedTime) || timeSettings.IsSunset(normalizedTime))
            {
                sunsetGlow = 1f - sunAltitude;
            }
            skyMaterial.SetFloat(SunsetGlowIntensityID, sunsetGlow);

            // Throttle DynamicGI updates - this is VERY expensive and should not run every frame
            if (Time.time - lastGIUpdateTime > GI_UPDATE_INTERVAL)
            {
                lastGIUpdateTime = Time.time;
                DynamicGI.UpdateEnvironment();
            }
        }

        /// <summary>
        /// Set cloud coverage (used by WeatherManager)
        /// </summary>
        public void SetCloudCoverage(float coverage)
        {
            targetCloudCoverage = Mathf.Clamp01(coverage);
        }

        /// <summary>
        /// Get current cloud coverage
        /// </summary>
        public float GetCloudCoverage()
        {
            return currentCloudCoverage;
        }

        /// <summary>
        /// Set cloud speed
        /// </summary>
        public void SetCloudSpeed(float speed)
        {
            cloudSpeed = speed;
        }

        private void OnValidate()
        {
            if (Application.isPlaying && skyMaterial != null)
            {
                skyMaterial.SetFloat(CloudCoverageID, baseClouds);
                skyMaterial.SetFloat(CloudSpeedID, cloudSpeed);
                skyMaterial.SetFloat(StarsThresholdID, starsThreshold);
                skyMaterial.SetFloat(StarsDensityID, starsDensity);
            }
        }
    }
}
