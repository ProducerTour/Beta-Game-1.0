using UnityEngine;

namespace CreatorWorld.Config
{
    /// <summary>
    /// Configuration for the time of day and sky system.
    /// Create via: Right-click > Create > Config > Time Of Day Settings
    /// </summary>
    [CreateAssetMenu(fileName = "TimeOfDaySettings", menuName = "Config/Time Of Day Settings")]
    public class TimeOfDaySettings : ScriptableObject
    {
        [Header("Time Progression")]
        [Tooltip("Real-time minutes for one full in-game day cycle")]
        [Range(1f, 60f)]
        public float dayDurationMinutes = 20f;

        [Tooltip("Starting time of day (0=midnight, 0.25=6am, 0.5=noon, 0.75=6pm)")]
        [Range(0f, 1f)]
        public float startTimeNormalized = 0.25f;

        [Tooltip("Pause time progression")]
        public bool pauseTime = false;

        [Header("Sun/Moon Timing")]
        [Tooltip("Time when sun rises (0-1 normalized, 0.25 = 6:00 AM)")]
        [Range(0f, 0.5f)]
        public float sunriseTime = 0.25f;

        [Tooltip("Time when sun sets (0-1 normalized, 0.75 = 6:00 PM)")]
        [Range(0.5f, 1f)]
        public float sunsetTime = 0.75f;

        [Header("Sun Light")]
        [Tooltip("Maximum sun intensity at noon")]
        [Range(0f, 2f)]
        public float maxSunIntensity = 1.2f;

        [Tooltip("Minimum sun intensity at horizon")]
        [Range(0f, 0.5f)]
        public float minSunIntensity = 0.1f;

        [Tooltip("Sun intensity curve over the day (0=sunrise, 0.5=noon, 1=sunset)")]
        public AnimationCurve sunIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Moon Light")]
        [Tooltip("Moon intensity at night")]
        [Range(0f, 0.5f)]
        public float moonIntensity = 0.15f;

        [Tooltip("Moon light color")]
        public Color moonColor = new Color(0.6f, 0.7f, 0.9f);

        [Header("Sky Colors - Day")]
        [Tooltip("Sky zenith (top) color gradient over 24h")]
        public Gradient skyTopColor;

        [Tooltip("Sky horizon color gradient over 24h")]
        public Gradient skyHorizonColor;

        [Tooltip("Sun disc color gradient over 24h")]
        public Gradient sunDiscColor;

        [Header("Sky Colors - Sunset/Sunrise")]
        [Tooltip("Sunset/sunrise glow color")]
        public Color sunsetGlowColor = new Color(1f, 0.5f, 0.2f);

        [Tooltip("Duration of sunset/sunrise transition (normalized, 0.05 = ~1.2 hours)")]
        [Range(0.02f, 0.15f)]
        public float sunsetDuration = 0.08f;

        [Header("Ambient Lighting")]
        [Tooltip("Ambient sky color gradient over 24h")]
        public Gradient ambientSkyColor;

        [Tooltip("Ambient equator color gradient over 24h")]
        public Gradient ambientEquatorColor;

        [Tooltip("Ambient ground color gradient over 24h")]
        public Gradient ambientGroundColor;

        [Header("Stars")]
        [Tooltip("Star visibility intensity at night")]
        [Range(0f, 1f)]
        public float starIntensity = 0.8f;

        [Tooltip("Time offset for stars to appear before full night")]
        [Range(0f, 0.1f)]
        public float starFadeOffset = 0.05f;

        [Header("Sun Disc")]
        [Tooltip("Size of the sun disc in the sky")]
        [Range(0.01f, 0.15f)]
        public float sunSize = 0.05f;

        [Tooltip("Size of the moon disc in the sky")]
        [Range(0.01f, 0.1f)]
        public float moonSize = 0.03f;

        /// <summary>
        /// Returns true if the given time is during daylight hours
        /// </summary>
        public bool IsDaytime(float normalizedTime)
        {
            return normalizedTime >= sunriseTime && normalizedTime <= sunsetTime;
        }

        /// <summary>
        /// Returns true if the given time is during sunrise transition
        /// </summary>
        public bool IsSunrise(float normalizedTime)
        {
            return normalizedTime >= sunriseTime && normalizedTime <= sunriseTime + sunsetDuration;
        }

        /// <summary>
        /// Returns true if the given time is during sunset transition
        /// </summary>
        public bool IsSunset(float normalizedTime)
        {
            return normalizedTime >= sunsetTime - sunsetDuration && normalizedTime <= sunsetTime;
        }

        /// <summary>
        /// Calculate sun altitude factor (0 at horizon, 1 at zenith)
        /// </summary>
        public float GetSunAltitudeFactor(float normalizedTime)
        {
            if (!IsDaytime(normalizedTime)) return 0f;

            float dayProgress = Mathf.InverseLerp(sunriseTime, sunsetTime, normalizedTime);
            return Mathf.Sin(dayProgress * Mathf.PI);
        }

        /// <summary>
        /// Calculate night factor (0 = full day, 1 = full night)
        /// </summary>
        public float GetNightFactor(float normalizedTime)
        {
            if (normalizedTime < sunriseTime - starFadeOffset)
                return 1f;
            if (normalizedTime < sunriseTime)
                return Mathf.InverseLerp(sunriseTime, sunriseTime - starFadeOffset, normalizedTime);
            if (normalizedTime < sunsetTime)
                return 0f;
            if (normalizedTime < sunsetTime + starFadeOffset)
                return Mathf.InverseLerp(sunsetTime, sunsetTime + starFadeOffset, normalizedTime);
            return 1f;
        }

        /// <summary>
        /// Convert normalized time to hour of day (0-24)
        /// </summary>
        public float GetHourOfDay(float normalizedTime)
        {
            return normalizedTime * 24f;
        }

        /// <summary>
        /// Convert normalized time to formatted time string (HH:MM)
        /// </summary>
        public string GetTimeString(float normalizedTime)
        {
            float hours = normalizedTime * 24f;
            int hour = Mathf.FloorToInt(hours);
            int minute = Mathf.FloorToInt((hours - hour) * 60f);
            return $"{hour:D2}:{minute:D2}";
        }

        private void OnValidate()
        {
            // Ensure sunset is after sunrise
            if (sunsetTime <= sunriseTime)
            {
                sunsetTime = sunriseTime + 0.1f;
            }

            // Initialize gradients with sensible defaults if empty
            InitializeDefaultGradients();
        }

        private void OnEnable()
        {
            InitializeDefaultGradients();
        }

        private void InitializeDefaultGradients()
        {
            // Sky top color (zenith)
            if (skyTopColor == null || skyTopColor.colorKeys.Length == 0)
            {
                skyTopColor = new Gradient();
                skyTopColor.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(0.02f, 0.02f, 0.05f), 0f),      // Midnight - dark blue
                        new GradientColorKey(new Color(0.1f, 0.15f, 0.3f), 0.2f),      // Pre-dawn
                        new GradientColorKey(new Color(0.4f, 0.6f, 0.9f), 0.3f),       // Morning
                        new GradientColorKey(new Color(0.3f, 0.5f, 0.9f), 0.5f),       // Noon - bright blue
                        new GradientColorKey(new Color(0.4f, 0.5f, 0.8f), 0.7f),       // Afternoon
                        new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 0.8f),       // Dusk
                        new GradientColorKey(new Color(0.02f, 0.02f, 0.05f), 1f)       // Night
                    },
                    new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                );
            }

            // Sky horizon color
            if (skyHorizonColor == null || skyHorizonColor.colorKeys.Length == 0)
            {
                skyHorizonColor = new Gradient();
                skyHorizonColor.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(0.05f, 0.05f, 0.1f), 0f),       // Midnight
                        new GradientColorKey(new Color(0.8f, 0.4f, 0.2f), 0.25f),      // Sunrise - orange
                        new GradientColorKey(new Color(0.7f, 0.8f, 0.95f), 0.35f),     // Morning - light blue
                        new GradientColorKey(new Color(0.8f, 0.9f, 1f), 0.5f),         // Noon - pale
                        new GradientColorKey(new Color(0.7f, 0.8f, 0.95f), 0.65f),     // Afternoon
                        new GradientColorKey(new Color(1f, 0.5f, 0.3f), 0.75f),        // Sunset - orange
                        new GradientColorKey(new Color(0.05f, 0.05f, 0.1f), 0.85f)     // Night
                    },
                    new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                );
            }

            // Sun disc color
            if (sunDiscColor == null || sunDiscColor.colorKeys.Length == 0)
            {
                sunDiscColor = new Gradient();
                sunDiscColor.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(1f, 0.3f, 0.1f), 0f),           // Not visible
                        new GradientColorKey(new Color(1f, 0.5f, 0.2f), 0.25f),        // Sunrise - orange
                        new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0.4f),        // Morning - warm white
                        new GradientColorKey(new Color(1f, 1f, 0.95f), 0.5f),          // Noon - white
                        new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0.6f),        // Afternoon
                        new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.75f),        // Sunset - deep orange
                        new GradientColorKey(new Color(1f, 0.3f, 0.1f), 1f)            // Not visible
                    },
                    new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                );
            }

            // Ambient colors
            if (ambientSkyColor == null || ambientSkyColor.colorKeys.Length == 0)
            {
                ambientSkyColor = new Gradient();
                ambientSkyColor.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(0.05f, 0.05f, 0.1f), 0f),
                        new GradientColorKey(new Color(0.3f, 0.35f, 0.4f), 0.3f),
                        new GradientColorKey(new Color(0.4f, 0.45f, 0.5f), 0.5f),
                        new GradientColorKey(new Color(0.3f, 0.35f, 0.4f), 0.7f),
                        new GradientColorKey(new Color(0.05f, 0.05f, 0.1f), 1f)
                    },
                    new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                );
            }

            if (ambientEquatorColor == null || ambientEquatorColor.colorKeys.Length == 0)
            {
                ambientEquatorColor = new Gradient();
                ambientEquatorColor.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(0.03f, 0.03f, 0.05f), 0f),
                        new GradientColorKey(new Color(0.2f, 0.22f, 0.25f), 0.3f),
                        new GradientColorKey(new Color(0.25f, 0.27f, 0.3f), 0.5f),
                        new GradientColorKey(new Color(0.2f, 0.22f, 0.25f), 0.7f),
                        new GradientColorKey(new Color(0.03f, 0.03f, 0.05f), 1f)
                    },
                    new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                );
            }

            if (ambientGroundColor == null || ambientGroundColor.colorKeys.Length == 0)
            {
                ambientGroundColor = new Gradient();
                ambientGroundColor.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(0.02f, 0.02f, 0.03f), 0f),
                        new GradientColorKey(new Color(0.1f, 0.09f, 0.08f), 0.3f),
                        new GradientColorKey(new Color(0.12f, 0.11f, 0.1f), 0.5f),
                        new GradientColorKey(new Color(0.1f, 0.09f, 0.08f), 0.7f),
                        new GradientColorKey(new Color(0.02f, 0.02f, 0.03f), 1f)
                    },
                    new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                );
            }
        }
    }
}
