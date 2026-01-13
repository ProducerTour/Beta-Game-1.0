using UnityEngine;
using CreatorWorld.Config;
using System;
using System.Collections;

namespace CreatorWorld.World
{
    /// <summary>
    /// Manages weather states and transitions.
    /// Singleton that persists across scenes.
    /// </summary>
    public class WeatherManager : MonoBehaviour
    {
        public static WeatherManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private WeatherSettings settings;

        [Header("References")]
        [SerializeField] private SkyController skyController;
        [SerializeField] private ParticleSystem rainParticles;
        [SerializeField] private AudioSource rainAudio;
        [SerializeField] private WindZone windZone;

        [Header("Current State")]
        [SerializeField] private WeatherState currentWeather = WeatherState.Clear;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // Transition state
        private bool isTransitioning = false;
        private Coroutine transitionCoroutine;
        private WeatherStateConfig currentConfig;
        private float nextWeatherChangeTime;

        // CTI shader property IDs (cached for performance)
        private int CTIWindPID;
        private int CTITurbulencePID;

        // Base wind settings (used as multiplier base)
        private float baseWindMain = 1f;
        private float baseWindTurbulence = 0.5f;

        // Events
        public static event Action<WeatherState> OnWeatherChanged;
        public static event Action<WeatherState, WeatherState> OnWeatherTransitionStart;
        public static event Action<WeatherState> OnWeatherTransitionComplete;

        // Properties
        public WeatherState CurrentWeather => currentWeather;
        public bool IsTransitioning => isTransitioning;
        public WeatherSettings Settings => settings;
        public WeatherStateConfig CurrentConfig => currentConfig;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Cache CTI shader property IDs
            CTIWindPID = Shader.PropertyToID("_CTI_SRP_Wind");
            CTITurbulencePID = Shader.PropertyToID("_CTI_SRP_Turbulence");

            // Store base wind settings from WindZone if available
            if (windZone != null)
            {
                baseWindMain = windZone.windMain;
                baseWindTurbulence = windZone.windTurbulence;
            }

            if (settings != null)
            {
                currentConfig = settings.GetConfig(currentWeather);
            }
        }

        private void Start()
        {
            // Apply initial weather
            ApplyWeatherImmediate(currentWeather);

            // Schedule first random weather change if enabled
            if (settings != null && settings.enableRandomWeather)
            {
                ScheduleNextWeatherChange();
            }
        }

        private void Update()
        {
            // Update CTI wind shader globals each frame (for pulse animation)
            UpdateCTIWindShaderGlobals();

            // Check for random weather changes
            if (settings != null && settings.enableRandomWeather && !isTransitioning)
            {
                if (Time.time >= nextWeatherChangeTime)
                {
                    WeatherState newWeather = settings.GetRandomWeather();
                    // Avoid same weather twice in a row
                    while (newWeather == currentWeather)
                    {
                        newWeather = settings.GetRandomWeather();
                    }
                    SetWeather(newWeather);
                    ScheduleNextWeatherChange();
                }
            }
        }

        private void UpdateCTIWindShaderGlobals()
        {
            if (windZone == null) return;

            Vector3 windDirection = windZone.transform.forward;
            float windStrength = windZone.windMain;

            // Add pulse effect like CTI_URP_CustomWind does
            windStrength += windZone.windPulseMagnitude *
                (1.0f + Mathf.Sin(Time.time * windZone.windPulseFrequency) +
                 1.0f + Mathf.Sin(Time.time * windZone.windPulseFrequency * 3.0f)) * 0.5f;

            float turbulence = windZone.windTurbulence * windZone.windMain;

            Shader.SetGlobalVector(CTIWindPID, new Vector4(windDirection.x, windDirection.y, windDirection.z, windStrength));
            Shader.SetGlobalFloat(CTITurbulencePID, turbulence);
        }

        private void ScheduleNextWeatherChange()
        {
            if (settings != null)
            {
                nextWeatherChangeTime = Time.time + settings.GetRandomWeatherDuration();
            }
        }

        /// <summary>
        /// Change to a new weather state with transition
        /// </summary>
        public void SetWeather(WeatherState newWeather)
        {
            if (settings == null) return;
            if (newWeather == currentWeather && !isTransitioning) return;

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            transitionCoroutine = StartCoroutine(TransitionWeather(newWeather));
        }

        /// <summary>
        /// Apply weather immediately without transition
        /// </summary>
        public void ApplyWeatherImmediate(WeatherState weather)
        {
            if (settings == null) return;

            currentWeather = weather;
            currentConfig = settings.GetConfig(weather);
            ApplyWeatherConfig(currentConfig);
            OnWeatherChanged?.Invoke(weather);
        }

        private IEnumerator TransitionWeather(WeatherState targetWeather)
        {
            isTransitioning = true;
            WeatherStateConfig fromConfig = settings.GetConfig(currentWeather);
            WeatherStateConfig toConfig = settings.GetConfig(targetWeather);

            OnWeatherTransitionStart?.Invoke(currentWeather, targetWeather);

            float elapsed = 0f;
            float duration = settings.transitionDuration;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float rawT = elapsed / duration;
                float t = settings.transitionCurve.Evaluate(rawT);

                // Interpolate weather config
                currentConfig = WeatherSettings.Lerp(fromConfig, toConfig, t);
                ApplyWeatherConfig(currentConfig);

                yield return null;
            }

            // Ensure final state is exact
            currentWeather = targetWeather;
            currentConfig = toConfig;
            ApplyWeatherConfig(currentConfig);

            isTransitioning = false;
            OnWeatherChanged?.Invoke(targetWeather);
            OnWeatherTransitionComplete?.Invoke(targetWeather);
        }

        private void ApplyWeatherConfig(WeatherStateConfig config)
        {
            if (config == null) return;

            // Update sky
            if (skyController != null)
            {
                skyController.SetCloudCoverage(config.cloudCoverage);
                skyController.SetCloudSpeed(config.cloudSpeed * 0.02f);
            }

            // Update fog
            RenderSettings.fog = config.fogEnabled;
            if (config.fogEnabled)
            {
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = config.fogDensity;
                RenderSettings.fogColor = config.fogColor;
            }

            // Update rain particles
            if (rainParticles != null)
            {
                var emission = rainParticles.emission;
                if (config.rainIntensity > 0)
                {
                    emission.rateOverTimeMultiplier = 500f * config.rainParticleRate * config.rainIntensity;
                    if (!rainParticles.isPlaying)
                    {
                        rainParticles.Play();
                    }
                }
                else
                {
                    emission.rateOverTimeMultiplier = 0f;
                    if (rainParticles.isPlaying)
                    {
                        rainParticles.Stop();
                    }
                }
            }

            // Update rain audio
            if (rainAudio != null)
            {
                rainAudio.volume = config.ambientVolume * config.rainIntensity;
                if (config.rainIntensity > 0 && !rainAudio.isPlaying)
                {
                    rainAudio.Play();
                }
                else if (config.rainIntensity <= 0 && rainAudio.isPlaying)
                {
                    rainAudio.Stop();
                }
            }

            // Update sun intensity if TimeOfDayManager exists
            if (TimeOfDayManager.Instance != null && TimeOfDayManager.Instance.Settings != null)
            {
                // Note: We modify the ambient intensity here
                // Sun intensity is handled by TimeOfDayManager
                float ambientMultiplier = config.ambientIntensity * config.skyBrightness;
                RenderSettings.ambientIntensity = ambientMultiplier;
            }

            // Update wind for grass and trees
            UpdateWind(config.windStrength);
        }

        private void UpdateWind(float windMultiplier)
        {
            // Update WindZone values - CTI shader globals are updated every frame in Update()
            if (windZone != null)
            {
                windZone.windMain = baseWindMain * windMultiplier;
                windZone.windTurbulence = baseWindTurbulence * windMultiplier;
            }
            else
            {
                // Fallback: Set CTI globals even without WindZone
                // Default wind direction is forward (0, 0, 1)
                Shader.SetGlobalVector(CTIWindPID, new Vector4(0f, 0f, 1f, windMultiplier));
                Shader.SetGlobalFloat(CTITurbulencePID, windMultiplier * 0.5f);
            }

            // Update grass shader wind
            Material grassMat = Shader.Find("Custom/GrassInstanced") != null
                ? Resources.Load<Material>("GrassInstanced")
                : null;

            if (grassMat != null)
            {
                float currentWind = grassMat.GetFloat("_WindStrength");
                grassMat.SetFloat("_WindStrength", currentWind * windMultiplier);
            }
        }

        /// <summary>
        /// Get the current interpolated fog density
        /// </summary>
        public float GetCurrentFogDensity()
        {
            return currentConfig?.fogDensity ?? 0f;
        }

        /// <summary>
        /// Get the current rain intensity (0-1)
        /// </summary>
        public float GetRainIntensity()
        {
            return currentConfig?.rainIntensity ?? 0f;
        }

        /// <summary>
        /// Check if it's currently raining
        /// </summary>
        public bool IsRaining()
        {
            return currentConfig != null && currentConfig.rainIntensity > 0.1f;
        }

        /// <summary>
        /// Check if it's currently foggy
        /// </summary>
        public bool IsFoggy()
        {
            return currentWeather == WeatherState.Fog ||
                   (currentConfig != null && currentConfig.fogDensity > 0.03f);
        }

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 160, 200, 150));
            GUILayout.Label($"Weather: {currentWeather}");
            GUILayout.Label($"Transitioning: {isTransitioning}");
            GUILayout.Label($"Cloud Coverage: {currentConfig?.cloudCoverage:F2}");
            GUILayout.Label($"Fog Density: {currentConfig?.fogDensity:F4}");
            GUILayout.Label($"Rain Intensity: {currentConfig?.rainIntensity:F2}");
            GUILayout.Label($"Wind Strength: {currentConfig?.windStrength:F2}");
            if (windZone != null)
                GUILayout.Label($"WindZone Main: {windZone.windMain:F2}");
            GUILayout.EndArea();
        }

        private void OnValidate()
        {
            if (Application.isPlaying && settings != null)
            {
                ApplyWeatherImmediate(currentWeather);
            }
        }
    }
}
