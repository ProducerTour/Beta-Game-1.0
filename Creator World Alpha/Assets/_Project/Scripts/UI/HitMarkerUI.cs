using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using CreatorWorld.Interfaces;

namespace CreatorWorld.UI
{
    /// <summary>
    /// Displays hit markers (X shape) when hitting enemies.
    /// AAA Pattern: Visual feedback confirms hits without damage numbers.
    /// Used in CoD, Battlefield, Destiny, etc.
    /// </summary>
    public class HitMarkerUI : MonoBehaviour
    {
        [Header("Appearance")]
        [SerializeField] private float markerSize = 24f;
        [SerializeField] private float lineThickness = 3f;
        [SerializeField] private float lineLength = 10f;
        [SerializeField] private float gapFromCenter = 4f;

        [Header("Colors")]
        [SerializeField] private Color normalHitColor = Color.white;
        [SerializeField] private Color headshotColor = new Color(1f, 0.9f, 0f); // Yellow
        [SerializeField] private Color killColor = Color.red;

        [Header("Animation")]
        [SerializeField] private float displayDuration = 0.15f;
        [SerializeField] private float fadeOutDuration = 0.1f;
        [SerializeField] private float headshotScale = 1.3f;
        [SerializeField] private float killScale = 1.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip hitSound;
        [SerializeField] private AudioClip headshotSound;
        [SerializeField] private AudioClip killSound;
        [SerializeField] [Range(0f, 1f)] private float soundVolume = 0.5f;

        // UI Elements
        private Canvas canvas;
        private RectTransform markerContainer;
        private Image[] markerLines = new Image[4]; // 4 lines make an X
        private CanvasGroup canvasGroup;
        private AudioSource audioSource;

        // State
        private Coroutine fadeCoroutine;
        private float baseScale = 1f;

        private void Awake()
        {
            CreateHitMarker();
            SetupAudio();
        }

        private void CreateHitMarker()
        {
            // Find or create canvas
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("HitMarkerCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 101; // Above crosshair

                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();

                transform.SetParent(canvasGO.transform, false);
            }

            // Create container for the marker
            GameObject containerGO = new GameObject("HitMarkerContainer");
            containerGO.transform.SetParent(transform, false);

            markerContainer = containerGO.AddComponent<RectTransform>();
            markerContainer.anchorMin = new Vector2(0.5f, 0.5f);
            markerContainer.anchorMax = new Vector2(0.5f, 0.5f);
            markerContainer.pivot = new Vector2(0.5f, 0.5f);
            markerContainer.anchoredPosition = Vector2.zero;
            markerContainer.sizeDelta = new Vector2(markerSize, markerSize);

            canvasGroup = containerGO.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f; // Start hidden

            // Create the 4 lines of the X
            // Line positions: top-left, top-right, bottom-left, bottom-right
            // Each line goes from gap distance outward
            float[] rotations = { 45f, -45f, 135f, -135f };

            for (int i = 0; i < 4; i++)
            {
                GameObject lineGO = new GameObject($"Line_{i}");
                lineGO.transform.SetParent(markerContainer, false);

                markerLines[i] = lineGO.AddComponent<Image>();
                markerLines[i].color = normalHitColor;

                RectTransform lineRect = lineGO.GetComponent<RectTransform>();
                lineRect.anchorMin = new Vector2(0.5f, 0.5f);
                lineRect.anchorMax = new Vector2(0.5f, 0.5f);
                lineRect.pivot = new Vector2(0.5f, 0f); // Pivot at bottom center
                lineRect.sizeDelta = new Vector2(lineThickness, lineLength);
                lineRect.localRotation = Quaternion.Euler(0, 0, rotations[i]);

                // Position outward from center
                float angle = rotations[i] * Mathf.Deg2Rad;
                float offsetX = Mathf.Sin(angle) * gapFromCenter;
                float offsetY = Mathf.Cos(angle) * gapFromCenter;
                lineRect.anchoredPosition = new Vector2(offsetX, offsetY);
            }

            Debug.Log("[HitMarkerUI] Hit marker created successfully");
        }

        private void SetupAudio()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound
        }

        /// <summary>
        /// Show the hit marker with appropriate style.
        /// </summary>
        public void ShowHitMarker(HitFeedbackType type)
        {
            // Stop any existing fade
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }

            // Set color and scale based on type
            Color color;
            float scale;
            AudioClip sound;

            switch (type)
            {
                case HitFeedbackType.Kill:
                    color = killColor;
                    scale = killScale;
                    sound = killSound ?? headshotSound ?? hitSound;
                    break;
                case HitFeedbackType.Headshot:
                    color = headshotColor;
                    scale = headshotScale;
                    sound = headshotSound ?? hitSound;
                    break;
                default:
                    color = normalHitColor;
                    scale = baseScale;
                    sound = hitSound;
                    break;
            }

            // Apply color to all lines
            foreach (var line in markerLines)
            {
                if (line != null)
                {
                    line.color = color;
                }
            }

            // Apply scale
            markerContainer.localScale = Vector3.one * scale;

            // Show immediately
            canvasGroup.alpha = 1f;

            // Play sound
            if (sound != null && audioSource != null)
            {
                audioSource.PlayOneShot(sound, soundVolume);
            }

            // Start fade out
            fadeCoroutine = StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            // Hold at full opacity
            yield return new WaitForSeconds(displayDuration);

            // Fade out
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutDuration;
                canvasGroup.alpha = 1f - t;

                // Optional: shrink slightly during fade
                float shrinkScale = Mathf.Lerp(markerContainer.localScale.x, baseScale * 0.8f, t * 0.5f);
                markerContainer.localScale = Vector3.one * shrinkScale;

                yield return null;
            }

            canvasGroup.alpha = 0f;
            markerContainer.localScale = Vector3.one * baseScale;
            fadeCoroutine = null;
        }

        /// <summary>
        /// Set custom colors for hit markers.
        /// </summary>
        public void SetColors(Color normal, Color headshot, Color kill)
        {
            normalHitColor = normal;
            headshotColor = headshot;
            killColor = kill;
        }

        /// <summary>
        /// Set custom sounds for hit markers.
        /// </summary>
        public void SetSounds(AudioClip hit, AudioClip headshot, AudioClip kill)
        {
            hitSound = hit;
            headshotSound = headshot;
            killSound = kill;
        }
    }
}
