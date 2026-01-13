using UnityEngine;
using CreatorWorld.Config;
using System;

namespace CreatorWorld.World
{
    /// <summary>
    /// Manages the day/night cycle and sun/moon positioning.
    /// Singleton that persists across scenes.
    /// </summary>
    public class TimeOfDayManager : MonoBehaviour
    {
        public static TimeOfDayManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private TimeOfDaySettings settings;

        [Header("Lights")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Light moonLight;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] [Range(0f, 1f)] private float debugTimeOverride = 0.5f;
        [SerializeField] private bool useDebugTime = false;

        // Current state
        private float currentTime;
        private bool wasDay = true;

        // Events
        public static event Action<float> OnTimeChanged;
        public static event Action OnSunrise;
        public static event Action OnSunset;
        public static event Action<bool> OnDayNightChanged;

        // Properties
        public float CurrentTime => currentTime;
        public bool IsDay => settings != null && settings.IsDaytime(currentTime);
        public bool IsSunrise => settings != null && settings.IsSunrise(currentTime);
        public bool IsSunset => settings != null && settings.IsSunset(currentTime);
        public float NightFactor => settings != null ? settings.GetNightFactor(currentTime) : 0f;
        public string TimeString => settings != null ? settings.GetTimeString(currentTime) : "00:00";
        public float HourOfDay => settings != null ? settings.GetHourOfDay(currentTime) : 0f;
        public TimeOfDaySettings Settings => settings;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (settings != null)
            {
                currentTime = settings.startTimeNormalized;
            }
        }

        private void Start()
        {
            // Initial update
            wasDay = IsDay;
            UpdateSunMoonPositions();
            UpdateLighting();
        }

        private void Update()
        {
            if (settings == null) return;

            // Use debug time if enabled
            if (useDebugTime)
            {
                currentTime = debugTimeOverride;
            }
            else if (!settings.pauseTime)
            {
                // Progress time
                float daySeconds = settings.dayDurationMinutes * 60f;
                currentTime += Time.deltaTime / daySeconds;

                // Wrap around
                if (currentTime >= 1f)
                {
                    currentTime -= 1f;
                }
            }

            // Update systems
            UpdateSunMoonPositions();
            UpdateLighting();

            // Fire events
            OnTimeChanged?.Invoke(currentTime);

            // Check for day/night transitions
            bool isDay = IsDay;
            if (isDay != wasDay)
            {
                if (isDay)
                {
                    OnSunrise?.Invoke();
                }
                else
                {
                    OnSunset?.Invoke();
                }
                OnDayNightChanged?.Invoke(isDay);
                wasDay = isDay;
            }
        }

        private void UpdateSunMoonPositions()
        {
            if (sunLight == null || settings == null) return;

            // Calculate sun position
            // Sun rises in east (90), peaks at south (0), sets in west (-90)
            float sunAngle = CalculateSunAngle();

            // Apply rotation - sun rotates around X axis for altitude, Y for azimuth
            sunLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);

            // Moon is opposite to sun
            if (moonLight != null)
            {
                moonLight.transform.rotation = Quaternion.Euler(sunAngle + 180f, 170f, 0f);
            }
        }

        private float CalculateSunAngle()
        {
            // Map time to sun angle
            // Sunrise (0.25) = 0 degrees (horizon)
            // Noon (0.5) = 90 degrees (zenith)
            // Sunset (0.75) = 180 degrees (horizon)
            // Night = below horizon (negative angles)

            if (currentTime < settings.sunriseTime)
            {
                // Night before sunrise
                float nightProgress = currentTime / settings.sunriseTime;
                return Mathf.Lerp(-90f, 0f, nightProgress);
            }
            else if (currentTime <= settings.sunsetTime)
            {
                // Daytime
                float dayProgress = Mathf.InverseLerp(settings.sunriseTime, settings.sunsetTime, currentTime);
                return Mathf.Sin(dayProgress * Mathf.PI) * 90f;
            }
            else
            {
                // Night after sunset
                float nightProgress = Mathf.InverseLerp(settings.sunsetTime, 1f, currentTime);
                return Mathf.Lerp(0f, -90f, nightProgress);
            }
        }

        private void UpdateLighting()
        {
            if (settings == null) return;

            // Update sun light
            if (sunLight != null)
            {
                float sunAltitude = settings.GetSunAltitudeFactor(currentTime);
                float intensityCurve = settings.sunIntensityCurve.Evaluate(sunAltitude);

                sunLight.intensity = Mathf.Lerp(
                    settings.minSunIntensity,
                    settings.maxSunIntensity,
                    intensityCurve
                );

                // Sun color from gradient
                sunLight.color = settings.sunDiscColor.Evaluate(currentTime);

                // Disable sun at night
                sunLight.enabled = IsDay || IsSunrise || IsSunset;
            }

            // Update moon light
            if (moonLight != null)
            {
                float nightFactor = NightFactor;
                moonLight.intensity = settings.moonIntensity * nightFactor;
                moonLight.color = settings.moonColor;
                moonLight.enabled = nightFactor > 0.1f;
            }

            // Update ambient lighting
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = settings.ambientSkyColor.Evaluate(currentTime);
            RenderSettings.ambientEquatorColor = settings.ambientEquatorColor.Evaluate(currentTime);
            RenderSettings.ambientGroundColor = settings.ambientGroundColor.Evaluate(currentTime);
        }

        /// <summary>
        /// Set the current time of day (0-1 normalized)
        /// </summary>
        public void SetTime(float normalizedTime)
        {
            currentTime = Mathf.Clamp01(normalizedTime);
            UpdateSunMoonPositions();
            UpdateLighting();
            OnTimeChanged?.Invoke(currentTime);
        }

        /// <summary>
        /// Set time by hour (0-24)
        /// </summary>
        public void SetTimeByHour(float hour)
        {
            SetTime(hour / 24f);
        }

        /// <summary>
        /// Skip to next sunrise
        /// </summary>
        public void SkipToSunrise()
        {
            if (settings != null) SetTime(settings.sunriseTime);
        }

        /// <summary>
        /// Skip to next sunset
        /// </summary>
        public void SkipToSunset()
        {
            if (settings != null) SetTime(settings.sunsetTime);
        }

        /// <summary>
        /// Skip to noon
        /// </summary>
        public void SkipToNoon()
        {
            SetTime(0.5f);
        }

        /// <summary>
        /// Skip to midnight
        /// </summary>
        public void SkipToMidnight()
        {
            SetTime(0f);
        }

        /// <summary>
        /// Get the current sun direction (normalized)
        /// </summary>
        public Vector3 GetSunDirection()
        {
            if (sunLight != null)
            {
                return -sunLight.transform.forward;
            }
            // Calculate from time if no sun light assigned
            float sunAngle = CalculateSunAngleForDirection();
            return Quaternion.Euler(sunAngle, 170f, 0f) * Vector3.forward;
        }

        /// <summary>
        /// Get the current moon direction (normalized)
        /// </summary>
        public Vector3 GetMoonDirection()
        {
            if (moonLight != null)
            {
                return -moonLight.transform.forward;
            }
            // Moon is opposite to sun - calculate from sun position
            float sunAngle = CalculateSunAngleForDirection();
            return Quaternion.Euler(sunAngle + 180f, 170f, 0f) * Vector3.forward;
        }

        private float CalculateSunAngleForDirection()
        {
            if (settings == null) return 45f;

            if (currentTime < settings.sunriseTime)
            {
                float nightProgress = currentTime / settings.sunriseTime;
                return Mathf.Lerp(-90f, 0f, nightProgress);
            }
            else if (currentTime <= settings.sunsetTime)
            {
                float dayProgress = Mathf.InverseLerp(settings.sunriseTime, settings.sunsetTime, currentTime);
                return Mathf.Sin(dayProgress * Mathf.PI) * 90f;
            }
            else
            {
                float nightProgress = Mathf.InverseLerp(settings.sunsetTime, 1f, currentTime);
                return Mathf.Lerp(0f, -90f, nightProgress);
            }
        }

        private void OnGUI()
        {
            if (!showDebugInfo || settings == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 200, 150));
            GUILayout.Label($"Time: {TimeString}");
            GUILayout.Label($"Normalized: {currentTime:F3}");
            GUILayout.Label($"Is Day: {IsDay}");
            GUILayout.Label($"Night Factor: {NightFactor:F2}");
            GUILayout.Label($"Sun Altitude: {settings.GetSunAltitudeFactor(currentTime):F2}");
            GUILayout.EndArea();
        }

        private void OnValidate()
        {
            if (useDebugTime && Application.isPlaying && settings != null)
            {
                currentTime = debugTimeOverride;
                UpdateSunMoonPositions();
                UpdateLighting();
            }
        }
    }
}
