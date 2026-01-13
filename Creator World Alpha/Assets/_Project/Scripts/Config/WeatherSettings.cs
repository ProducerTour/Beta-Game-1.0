using UnityEngine;

namespace CreatorWorld.Config
{
    /// <summary>
    /// Weather states for the weather system
    /// </summary>
    public enum WeatherState
    {
        Clear,
        Cloudy,
        Rain,
        Fog
    }

    /// <summary>
    /// Configuration for a single weather state
    /// </summary>
    [System.Serializable]
    public class WeatherStateConfig
    {
        [Header("Identification")]
        public string name;
        public WeatherState state;

        [Header("Sky")]
        [Tooltip("Cloud coverage (0-1)")]
        [Range(0f, 1f)]
        public float cloudCoverage = 0.2f;

        [Tooltip("Cloud speed multiplier")]
        [Range(0f, 3f)]
        public float cloudSpeed = 1f;

        [Tooltip("Sky brightness multiplier")]
        [Range(0.3f, 1.5f)]
        public float skyBrightness = 1f;

        [Header("Fog")]
        [Tooltip("Enable fog for this weather")]
        public bool fogEnabled = false;

        [Tooltip("Fog density")]
        [Range(0f, 0.1f)]
        public float fogDensity = 0.01f;

        [Tooltip("Fog color")]
        public Color fogColor = new Color(0.7f, 0.75f, 0.8f);

        [Header("Precipitation")]
        [Tooltip("Rain intensity (0 = none, 1 = heavy)")]
        [Range(0f, 1f)]
        public float rainIntensity = 0f;

        [Tooltip("Rain particle emission rate multiplier")]
        [Range(0f, 5f)]
        public float rainParticleRate = 1f;

        [Header("Wind")]
        [Tooltip("Wind strength multiplier")]
        [Range(0f, 3f)]
        public float windStrength = 1f;

        [Header("Lighting")]
        [Tooltip("Sun intensity multiplier")]
        [Range(0f, 1.5f)]
        public float sunIntensity = 1f;

        [Tooltip("Ambient intensity multiplier")]
        [Range(0.3f, 1.5f)]
        public float ambientIntensity = 1f;

        [Header("Audio")]
        [Tooltip("Ambient audio volume (0-1)")]
        [Range(0f, 1f)]
        public float ambientVolume = 0f;
    }

    /// <summary>
    /// Configuration for the weather system.
    /// Create via: Right-click > Create > Config > Weather Settings
    /// </summary>
    [CreateAssetMenu(fileName = "WeatherSettings", menuName = "Config/Weather Settings")]
    public class WeatherSettings : ScriptableObject
    {
        [Header("Transitions")]
        [Tooltip("Duration of weather transitions in seconds")]
        [Range(1f, 30f)]
        public float transitionDuration = 8f;

        [Tooltip("Smoothing curve for transitions")]
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Weather States")]
        public WeatherStateConfig clearWeather = new WeatherStateConfig
        {
            name = "Clear",
            state = WeatherState.Clear,
            cloudCoverage = 0.15f,
            cloudSpeed = 0.8f,
            skyBrightness = 1f,
            fogEnabled = false,
            fogDensity = 0f,
            rainIntensity = 0f,
            windStrength = 0.5f,
            sunIntensity = 1f,
            ambientIntensity = 1f
        };

        public WeatherStateConfig cloudyWeather = new WeatherStateConfig
        {
            name = "Cloudy",
            state = WeatherState.Cloudy,
            cloudCoverage = 0.65f,
            cloudSpeed = 1.2f,
            skyBrightness = 0.85f,
            fogEnabled = false,
            fogDensity = 0.002f,
            rainIntensity = 0f,
            windStrength = 1f,
            sunIntensity = 0.7f,
            ambientIntensity = 0.9f
        };

        public WeatherStateConfig rainWeather = new WeatherStateConfig
        {
            name = "Rain",
            state = WeatherState.Rain,
            cloudCoverage = 0.9f,
            cloudSpeed = 1.5f,
            skyBrightness = 0.6f,
            fogEnabled = true,
            fogDensity = 0.015f,
            fogColor = new Color(0.5f, 0.55f, 0.6f),
            rainIntensity = 1f,
            rainParticleRate = 3f,
            windStrength = 1.5f,
            sunIntensity = 0.3f,
            ambientIntensity = 0.7f,
            ambientVolume = 0.8f
        };

        public WeatherStateConfig fogWeather = new WeatherStateConfig
        {
            name = "Fog",
            state = WeatherState.Fog,
            cloudCoverage = 1f,
            cloudSpeed = 0.3f,
            skyBrightness = 0.7f,
            fogEnabled = true,
            fogDensity = 0.05f,
            fogColor = new Color(0.7f, 0.75f, 0.8f),
            rainIntensity = 0f,
            windStrength = 0.2f,
            sunIntensity = 0.4f,
            ambientIntensity = 0.8f
        };

        [Header("Random Weather")]
        [Tooltip("Enable automatic random weather changes")]
        public bool enableRandomWeather = false;

        [Tooltip("Minimum time between weather changes (seconds)")]
        [Range(30f, 600f)]
        public float minWeatherDuration = 120f;

        [Tooltip("Maximum time between weather changes (seconds)")]
        [Range(60f, 1200f)]
        public float maxWeatherDuration = 300f;

        [Header("Weather Probabilities")]
        [Tooltip("Probability of clear weather (relative weight)")]
        [Range(0f, 10f)]
        public float clearProbability = 4f;

        [Tooltip("Probability of cloudy weather (relative weight)")]
        [Range(0f, 10f)]
        public float cloudyProbability = 3f;

        [Tooltip("Probability of rain (relative weight)")]
        [Range(0f, 10f)]
        public float rainProbability = 2f;

        [Tooltip("Probability of fog (relative weight)")]
        [Range(0f, 10f)]
        public float fogProbability = 1f;

        /// <summary>
        /// Get config for a weather state
        /// </summary>
        public WeatherStateConfig GetConfig(WeatherState state)
        {
            return state switch
            {
                WeatherState.Clear => clearWeather,
                WeatherState.Cloudy => cloudyWeather,
                WeatherState.Rain => rainWeather,
                WeatherState.Fog => fogWeather,
                _ => clearWeather
            };
        }

        /// <summary>
        /// Get a random weather state based on probabilities
        /// </summary>
        public WeatherState GetRandomWeather()
        {
            float total = clearProbability + cloudyProbability + rainProbability + fogProbability;
            float random = Random.value * total;

            if (random < clearProbability)
                return WeatherState.Clear;
            random -= clearProbability;

            if (random < cloudyProbability)
                return WeatherState.Cloudy;
            random -= cloudyProbability;

            if (random < rainProbability)
                return WeatherState.Rain;

            return WeatherState.Fog;
        }

        /// <summary>
        /// Get next random weather duration
        /// </summary>
        public float GetRandomWeatherDuration()
        {
            return Random.Range(minWeatherDuration, maxWeatherDuration);
        }

        /// <summary>
        /// Interpolate between two weather configs
        /// </summary>
        public static WeatherStateConfig Lerp(WeatherStateConfig from, WeatherStateConfig to, float t)
        {
            return new WeatherStateConfig
            {
                name = t < 0.5f ? from.name : to.name,
                state = t < 0.5f ? from.state : to.state,
                cloudCoverage = Mathf.Lerp(from.cloudCoverage, to.cloudCoverage, t),
                cloudSpeed = Mathf.Lerp(from.cloudSpeed, to.cloudSpeed, t),
                skyBrightness = Mathf.Lerp(from.skyBrightness, to.skyBrightness, t),
                fogEnabled = t > 0.5f ? to.fogEnabled : from.fogEnabled,
                fogDensity = Mathf.Lerp(from.fogDensity, to.fogDensity, t),
                fogColor = Color.Lerp(from.fogColor, to.fogColor, t),
                rainIntensity = Mathf.Lerp(from.rainIntensity, to.rainIntensity, t),
                rainParticleRate = Mathf.Lerp(from.rainParticleRate, to.rainParticleRate, t),
                windStrength = Mathf.Lerp(from.windStrength, to.windStrength, t),
                sunIntensity = Mathf.Lerp(from.sunIntensity, to.sunIntensity, t),
                ambientIntensity = Mathf.Lerp(from.ambientIntensity, to.ambientIntensity, t),
                ambientVolume = Mathf.Lerp(from.ambientVolume, to.ambientVolume, t)
            };
        }
    }
}
