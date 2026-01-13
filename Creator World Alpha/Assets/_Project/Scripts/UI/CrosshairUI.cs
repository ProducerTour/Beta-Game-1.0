using UnityEngine;
using UnityEngine.UI;

namespace CreatorWorld.UI
{
    /// <summary>
    /// Simple crosshair display. Creates a white dot in the center of the screen.
    /// Attach to a Canvas or let it create its own.
    /// </summary>
    public class CrosshairUI : MonoBehaviour
    {
        [Header("Appearance")]
        [SerializeField] private float dotSize = 8f;
        [SerializeField] private Color dotColor = Color.white;
        [SerializeField] private bool addOutline = true;

        private Image dotImage;
        private Canvas canvas;

        private void Start()
        {
            CreateCrosshair();
        }

        private void CreateCrosshair()
        {
            // Find or create canvas
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                // Create a screen-space overlay canvas
                GameObject canvasGO = new GameObject("CrosshairCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100; // Render on top

                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();

                // Move this component to the canvas
                transform.SetParent(canvasGO.transform, false);
            }

            // Ensure this object has a RectTransform (required for UI)
            if (GetComponent<RectTransform>() == null)
            {
                gameObject.AddComponent<RectTransform>();
            }

            // Center this container
            RectTransform myRect = GetComponent<RectTransform>();
            myRect.anchorMin = new Vector2(0.5f, 0.5f);
            myRect.anchorMax = new Vector2(0.5f, 0.5f);
            myRect.pivot = new Vector2(0.5f, 0.5f);
            myRect.anchoredPosition = Vector2.zero;
            myRect.sizeDelta = new Vector2(100, 100);

            // Create outline first (renders behind)
            if (addOutline)
            {
                GameObject outlineGO = new GameObject("CrosshairOutline");
                outlineGO.transform.SetParent(transform, false);

                Image outlineImage = outlineGO.AddComponent<Image>();
                outlineImage.color = Color.black;

                RectTransform outlineRect = outlineGO.GetComponent<RectTransform>();
                outlineRect.anchorMin = new Vector2(0.5f, 0.5f);
                outlineRect.anchorMax = new Vector2(0.5f, 0.5f);
                outlineRect.pivot = new Vector2(0.5f, 0.5f);
                outlineRect.anchoredPosition = Vector2.zero;
                outlineRect.sizeDelta = new Vector2(dotSize + 2f, dotSize + 2f);
            }

            // Create the dot
            GameObject dotGO = new GameObject("CrosshairDot");
            dotGO.transform.SetParent(transform, false);

            dotImage = dotGO.AddComponent<Image>();
            dotImage.color = dotColor;

            // Center it
            RectTransform rect = dotGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(dotSize, dotSize);

            Debug.Log("[CrosshairUI] Crosshair created successfully");
        }

        /// <summary>
        /// Show or hide the crosshair.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (dotImage != null)
            {
                dotImage.enabled = visible;
            }
        }

        /// <summary>
        /// Change crosshair color at runtime.
        /// </summary>
        public void SetColor(Color color)
        {
            dotColor = color;
            if (dotImage != null)
            {
                dotImage.color = color;
            }
        }

        /// <summary>
        /// Change crosshair size at runtime.
        /// </summary>
        public void SetSize(float size)
        {
            dotSize = size;
            if (dotImage != null)
            {
                dotImage.rectTransform.sizeDelta = new Vector2(size, size);
            }
        }
    }
}
