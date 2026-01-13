using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using CreatorWorld.Player;
using CreatorWorld.Interfaces;

namespace CreatorWorld.UI
{
    /// <summary>
    /// Rust/Tarkov-style survival HUD with health, hunger, thirst, and stamina bars.
    /// Positioned in the bottom-left corner with damage feedback effects.
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        [Header("Bar Settings")]
        [SerializeField] private Vector2 barSize = new Vector2(150f, 12f);
        [SerializeField] private float barSpacing = 6f;
        [SerializeField] private Vector2 screenMargin = new Vector2(20f, 20f);

        [Header("Health Colors")]
        [SerializeField] private Color healthHighColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color healthMidColor = new Color(0.9f, 0.9f, 0.2f);
        [SerializeField] private Color healthLowColor = new Color(0.9f, 0.2f, 0.2f);

        [Header("Stat Colors")]
        [SerializeField] private Color hungerColor = new Color(1f, 0.58f, 0f);
        [SerializeField] private Color thirstColor = new Color(0f, 0.83f, 1f);
        [SerializeField] private Color staminaColor = new Color(1f, 0.84f, 0f);

        [Header("Background")]
        [SerializeField] private Color backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.7f);
        [SerializeField] private Color borderColor = new Color(0f, 0f, 0f, 0.9f);

        [Header("Damage Flash")]
        [SerializeField] private bool enableDamageFlash = true;
        [SerializeField] private Color damageFlashColor = new Color(0.8f, 0f, 0f, 0.3f);
        [SerializeField] private float damageFlashDuration = 0.2f;

        [Header("Low Health Vignette")]
        [SerializeField] private bool enableVignette = true;
        [SerializeField] private float vignetteThreshold = 0.3f;
        [SerializeField] private float criticalThreshold = 0.15f;
        [SerializeField] private Color vignetteColor = new Color(0.4f, 0f, 0f, 0.6f);

        // UI References
        private Canvas canvas;
        private RectTransform containerRect;
        private Image healthFill;
        private Image hungerFill;
        private Image thirstFill;
        private Image staminaFill;
        private Image damageFlashOverlay;
        private Image vignetteOverlay;

        // Component references
        private PlayerHealth playerHealth;

        // State
        private Coroutine damageFlashCoroutine;
        private Coroutine vignettePulseCoroutine;
        private float currentHealthPercent = 1f;

        private void Start()
        {
            FindPlayerHealth();
            CreateCanvas();
            CreateBars();
            CreateDamageFlash();
            CreateVignette();
            SubscribeToEvents();
            InitializeValues();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void FindPlayerHealth()
        {
            playerHealth = FindObjectOfType<PlayerHealth>();
            if (playerHealth == null)
            {
                Debug.LogError("[HealthBarUI] PlayerHealth not found in scene!");
            }
        }

        private void CreateCanvas()
        {
            // Find or create canvas
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("HealthBarCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 99; // Below crosshair (100)

                var scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                canvasGO.AddComponent<GraphicRaycaster>();
                transform.SetParent(canvasGO.transform, false);
            }
        }

        private void CreateBars()
        {
            // Create container anchored to bottom-left
            GameObject containerGO = new GameObject("BarsContainer");
            containerGO.transform.SetParent(canvas.transform, false);

            containerRect = containerGO.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0, 0);
            containerRect.anchorMax = new Vector2(0, 0);
            containerRect.pivot = new Vector2(0, 0);
            containerRect.anchoredPosition = screenMargin;

            float totalHeight = (barSize.y + barSpacing) * 4 - barSpacing;
            containerRect.sizeDelta = new Vector2(barSize.x, totalHeight);

            // Create bars from bottom to top: Stamina, Thirst, Hunger, Health
            staminaFill = CreateBar(containerGO.transform, "Stamina", 0, staminaColor);
            thirstFill = CreateBar(containerGO.transform, "Thirst", 1, thirstColor);
            hungerFill = CreateBar(containerGO.transform, "Hunger", 2, hungerColor);
            healthFill = CreateBar(containerGO.transform, "Health", 3, healthHighColor);
        }

        private Image CreateBar(Transform parent, string name, int index, Color fillColor)
        {
            float yOffset = index * (barSize.y + barSpacing);

            // Background
            GameObject bgGO = new GameObject($"{name}Background");
            bgGO.transform.SetParent(parent, false);

            Image bgImage = bgGO.AddComponent<Image>();
            bgImage.color = backgroundColor;

            RectTransform bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0);
            bgRect.anchorMax = new Vector2(0, 0);
            bgRect.pivot = new Vector2(0, 0);
            bgRect.anchoredPosition = new Vector2(0, yOffset);
            bgRect.sizeDelta = barSize;

            // Border (outline effect via slightly larger dark rect behind)
            GameObject borderGO = new GameObject($"{name}Border");
            borderGO.transform.SetParent(parent, false);
            borderGO.transform.SetSiblingIndex(bgGO.transform.GetSiblingIndex());

            Image borderImage = borderGO.AddComponent<Image>();
            borderImage.color = borderColor;

            RectTransform borderRect = borderGO.GetComponent<RectTransform>();
            borderRect.anchorMin = new Vector2(0, 0);
            borderRect.anchorMax = new Vector2(0, 0);
            borderRect.pivot = new Vector2(0, 0);
            borderRect.anchoredPosition = new Vector2(-1, yOffset - 1);
            borderRect.sizeDelta = barSize + new Vector2(2, 2);

            // Fill (anchored left, scales width)
            GameObject fillGO = new GameObject($"{name}Fill");
            fillGO.transform.SetParent(bgGO.transform, false);

            Image fillImage = fillGO.AddComponent<Image>();
            fillImage.color = fillColor;

            RectTransform fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(barSize.x, 0); // Height from anchors

            return fillImage;
        }

        private void CreateDamageFlash()
        {
            if (!enableDamageFlash) return;

            GameObject flashGO = new GameObject("DamageFlash");
            flashGO.transform.SetParent(canvas.transform, false);

            damageFlashOverlay = flashGO.AddComponent<Image>();
            damageFlashOverlay.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, 0f);
            damageFlashOverlay.raycastTarget = false;

            RectTransform flashRect = flashGO.GetComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
        }

        private void CreateVignette()
        {
            if (!enableVignette) return;

            // Create a simple vignette using 4 gradient edges
            GameObject vignetteGO = new GameObject("Vignette");
            vignetteGO.transform.SetParent(canvas.transform, false);

            vignetteOverlay = vignetteGO.AddComponent<Image>();
            vignetteOverlay.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, 0f);
            vignetteOverlay.raycastTarget = false;

            // Use a radial gradient texture or simple color for now
            // For a proper vignette, you'd use a shader or gradient texture
            RectTransform vignetteRect = vignetteGO.GetComponent<RectTransform>();
            vignetteRect.anchorMin = Vector2.zero;
            vignetteRect.anchorMax = Vector2.one;
            vignetteRect.offsetMin = Vector2.zero;
            vignetteRect.offsetMax = Vector2.zero;
        }

        private void SubscribeToEvents()
        {
            if (playerHealth == null) return;

            playerHealth.OnHealthChanged += OnHealthChanged;
            playerHealth.OnHungerChanged += OnHungerChanged;
            playerHealth.OnThirstChanged += OnThirstChanged;
            playerHealth.OnStaminaChanged += OnStaminaChanged;
            playerHealth.OnDamaged += OnDamaged;
            playerHealth.OnDeath += OnPlayerDeath;
        }

        private void UnsubscribeFromEvents()
        {
            if (playerHealth == null) return;

            playerHealth.OnHealthChanged -= OnHealthChanged;
            playerHealth.OnHungerChanged -= OnHungerChanged;
            playerHealth.OnThirstChanged -= OnThirstChanged;
            playerHealth.OnStaminaChanged -= OnStaminaChanged;
            playerHealth.OnDamaged -= OnDamaged;
            playerHealth.OnDeath -= OnPlayerDeath;
        }

        private void InitializeValues()
        {
            if (playerHealth == null) return;

            UpdateBar(healthFill, playerHealth.HealthPercent, true);
            UpdateBar(hungerFill, playerHealth.HungerPercent, false);
            UpdateBar(thirstFill, playerHealth.ThirstPercent, false);
            UpdateBar(staminaFill, playerHealth.StaminaPercent, false);
        }

        #region Event Handlers

        private void OnHealthChanged(float current, float max)
        {
            float percent = max > 0 ? current / max : 0;
            currentHealthPercent = percent;
            UpdateBar(healthFill, percent, true);
            UpdateVignette(percent);
        }

        private void OnHungerChanged(float current, float max)
        {
            float percent = max > 0 ? current / max : 0;
            UpdateBar(hungerFill, percent, false);
        }

        private void OnThirstChanged(float current, float max)
        {
            float percent = max > 0 ? current / max : 0;
            UpdateBar(thirstFill, percent, false);
        }

        private void OnStaminaChanged(float current, float max)
        {
            float percent = max > 0 ? current / max : 0;
            UpdateBar(staminaFill, percent, false);
        }

        private void OnDamaged(float amount, DamageType type)
        {
            if (enableDamageFlash && damageFlashOverlay != null)
            {
                TriggerDamageFlash();
            }
        }

        private void OnPlayerDeath()
        {
            // Could show death overlay or hide HUD
        }

        #endregion

        #region Bar Updates

        private void UpdateBar(Image fillImage, float percent, bool isHealth)
        {
            if (fillImage == null) return;

            percent = Mathf.Clamp01(percent);

            // Update fill width
            RectTransform fillRect = fillImage.rectTransform;
            fillRect.sizeDelta = new Vector2(barSize.x * percent, fillRect.sizeDelta.y);

            // Update health bar color based on percent
            if (isHealth)
            {
                fillImage.color = GetHealthColor(percent);
            }
        }

        private Color GetHealthColor(float percent)
        {
            if (percent > 0.6f)
            {
                return healthHighColor;
            }
            else if (percent > 0.3f)
            {
                // Lerp between mid and high
                float t = (percent - 0.3f) / 0.3f;
                return Color.Lerp(healthMidColor, healthHighColor, t);
            }
            else
            {
                // Lerp between low and mid
                float t = percent / 0.3f;
                return Color.Lerp(healthLowColor, healthMidColor, t);
            }
        }

        #endregion

        #region Damage Flash

        private void TriggerDamageFlash()
        {
            if (damageFlashCoroutine != null)
            {
                StopCoroutine(damageFlashCoroutine);
            }
            damageFlashCoroutine = StartCoroutine(DamageFlashCoroutine());
        }

        private IEnumerator DamageFlashCoroutine()
        {
            // Show flash
            damageFlashOverlay.color = damageFlashColor;

            // Fade out
            float elapsed = 0f;
            Color startColor = damageFlashColor;
            Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

            while (elapsed < damageFlashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / damageFlashDuration;
                damageFlashOverlay.color = Color.Lerp(startColor, endColor, t);
                yield return null;
            }

            damageFlashOverlay.color = endColor;
            damageFlashCoroutine = null;
        }

        #endregion

        #region Vignette

        private void UpdateVignette(float healthPercent)
        {
            if (!enableVignette || vignetteOverlay == null) return;

            if (healthPercent > vignetteThreshold)
            {
                // No vignette when healthy
                vignetteOverlay.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, 0f);
                StopVignettePulse();
            }
            else if (healthPercent <= criticalThreshold)
            {
                // Critical: start pulsing
                StartVignettePulse();
            }
            else
            {
                // Low health: static vignette based on health
                StopVignettePulse();
                float intensity = 1f - (healthPercent / vignetteThreshold);
                vignetteOverlay.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, vignetteColor.a * intensity);
            }
        }

        private void StartVignettePulse()
        {
            if (vignettePulseCoroutine == null)
            {
                vignettePulseCoroutine = StartCoroutine(VignettePulseCoroutine());
            }
        }

        private void StopVignettePulse()
        {
            if (vignettePulseCoroutine != null)
            {
                StopCoroutine(vignettePulseCoroutine);
                vignettePulseCoroutine = null;
            }
        }

        private IEnumerator VignettePulseCoroutine()
        {
            float pulseSpeed = 1.5f; // Heartbeat-like pulse
            float minAlpha = vignetteColor.a * 0.5f;
            float maxAlpha = vignetteColor.a;

            while (true)
            {
                // Pulse in
                float t = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
                vignetteOverlay.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, alpha);
                yield return null;
            }
        }

        #endregion

        /// <summary>
        /// Show or hide the entire HUD.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (containerRect != null)
            {
                containerRect.gameObject.SetActive(visible);
            }
        }
    }
}
