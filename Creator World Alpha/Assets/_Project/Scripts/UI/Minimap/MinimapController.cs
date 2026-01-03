using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using CreatorWorld.Player;

namespace CreatorWorld.UI.Minimap
{
    /// <summary>
    /// Main minimap controller. Handles UI display and input.
    /// </summary>
    public class MinimapController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private MinimapSettings settings;

        [Header("UI References (Optional - will create if null)")]
        [SerializeField] private RawImage minimapDisplay;
        [SerializeField] private RectTransform playerIcon;

        // Runtime
        private MinimapCamera minimapCamera;
        private Transform playerTransform;
        private RectTransform container;
        private float currentZoom;
        private bool isFullscreen;
        private Vector2 normalSize;
        private Vector2 normalPosition;

        // Resize state
        private bool isResizing;
        private Vector2 resizeStartMousePos;
        private Vector2 resizeStartSize;
        private RectTransform resizeHandle;
        private const float MIN_SIZE = 150f;
        private const float MAX_SIZE = 600f;

        // Cursor state
        private CursorLockMode previousLockState;
        private bool wasOverUI;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            // Find player
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                playerTransform = player.transform;
            }

            // Create default settings if needed
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<MinimapSettings>();
            }

            currentZoom = settings.defaultZoom;

            // Create minimap camera
            CreateMinimapCamera();

            // Setup UI
            SetupUI();
        }

        private void CreateMinimapCamera()
        {
            var cameraGO = new GameObject("MinimapCamera");
            cameraGO.AddComponent<Camera>();
            minimapCamera = cameraGO.AddComponent<MinimapCamera>();
            minimapCamera.Initialize(settings, playerTransform);
        }

        private void SetupUI()
        {
            // Ensure we have a Canvas with proper scaling
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                var canvasGO = new GameObject("MinimapCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                // Configure CanvasScaler for resolution independence
                var scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                canvasGO.AddComponent<GraphicRaycaster>();
                transform.SetParent(canvasGO.transform, false);
            }

            // Ensure this GameObject's RectTransform stretches to fill canvas
            var myRect = GetComponent<RectTransform>();
            if (myRect == null)
            {
                myRect = gameObject.AddComponent<RectTransform>();
            }
            myRect.anchorMin = Vector2.zero;
            myRect.anchorMax = Vector2.one;
            myRect.offsetMin = Vector2.zero;
            myRect.offsetMax = Vector2.zero;

            // Create container
            var containerGO = new GameObject("MinimapContainer");
            containerGO.transform.SetParent(transform, false);
            container = containerGO.AddComponent<RectTransform>();

            // Anchor to bottom-left (resize-safe)
            AnchorBottomLeft(container,
                new Vector2(settings.minimapSize, settings.minimapSize),
                new Vector2(settings.padding, settings.padding));

            // Store for fullscreen toggle
            normalSize = container.sizeDelta;
            normalPosition = container.anchoredPosition;

            // Create border background
            var borderGO = new GameObject("Border");
            borderGO.transform.SetParent(container, false);
            var borderRect = borderGO.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = Vector2.zero;
            borderRect.anchoredPosition = Vector2.zero;
            var borderImage = borderGO.AddComponent<Image>();
            borderImage.color = settings.borderColor;

            // Create minimap display
            var displayGO = new GameObject("MinimapDisplay");
            displayGO.transform.SetParent(container, false);
            var displayRect = displayGO.AddComponent<RectTransform>();
            displayRect.anchorMin = Vector2.zero;
            displayRect.anchorMax = Vector2.one;
            displayRect.sizeDelta = new Vector2(-settings.borderWidth * 2, -settings.borderWidth * 2);
            displayRect.anchoredPosition = Vector2.zero;
            minimapDisplay = displayGO.AddComponent<RawImage>();
            minimapDisplay.texture = minimapCamera.RenderTexture;

            // Create player icon
            var iconGO = new GameObject("PlayerIcon");
            iconGO.transform.SetParent(container, false);
            playerIcon = iconGO.AddComponent<RectTransform>();
            playerIcon.anchorMin = new Vector2(0.5f, 0.5f);
            playerIcon.anchorMax = new Vector2(0.5f, 0.5f);
            playerIcon.sizeDelta = new Vector2(settings.playerIconSize, settings.playerIconSize);
            playerIcon.anchoredPosition = Vector2.zero;
            var iconImage = iconGO.AddComponent<Image>();
            iconImage.color = settings.playerIconColor;
            iconImage.sprite = CreateTriangleSprite();

            // Create resize handle (top-right corner)
            CreateResizeHandle();
        }

        private void CreateResizeHandle()
        {
            var handleGO = new GameObject("ResizeHandle");
            handleGO.transform.SetParent(container, false);
            resizeHandle = handleGO.AddComponent<RectTransform>();

            // Position at top-right corner - larger hit area
            resizeHandle.anchorMin = new Vector2(1, 1);
            resizeHandle.anchorMax = new Vector2(1, 1);
            resizeHandle.pivot = new Vector2(1, 1);
            resizeHandle.sizeDelta = new Vector2(32, 32);
            resizeHandle.anchoredPosition = Vector2.zero;

            // Visual indicator - more visible
            var handleImage = handleGO.AddComponent<Image>();
            handleImage.color = new Color(1f, 1f, 1f, 0.6f);
            handleImage.sprite = CreateResizeSprite();
            handleImage.raycastTarget = true;
        }

        private Sprite CreateResizeSprite()
        {
            int size = 32;
            var texture = new Texture2D(size, size);
            var colors = new Color[size * size];

            // Fill with semi-transparent background
            for (int i = 0; i < colors.Length; i++)
                colors[i] = new Color(0, 0, 0, 0.3f);

            // Draw diagonal lines (resize indicator) - thicker lines
            for (int i = 0; i < size; i++)
            {
                for (int t = -1; t <= 1; t++) // Thicker lines
                {
                    int idx = i + t;
                    if (idx >= 0 && idx < size)
                    {
                        // Main diagonal
                        int x1 = size - 1 - idx;
                        if (x1 >= 0 && x1 < size)
                            colors[i * size + x1] = Color.white;

                        // Secondary diagonals
                        int x2 = size - 1 - idx - 8;
                        if (x2 >= 0 && x2 < size)
                            colors[i * size + x2] = Color.white;

                        int x3 = size - 1 - idx - 16;
                        if (x3 >= 0 && x3 < size)
                            colors[i * size + x3] = Color.white;
                    }
                }
            }

            texture.SetPixels(colors);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private Sprite CreateTriangleSprite()
        {
            int size = 32;
            var texture = new Texture2D(size, size);
            var colors = new Color[size * size];

            // Fill transparent
            for (int i = 0; i < colors.Length; i++)
                colors[i] = Color.clear;

            // Draw triangle pointing up
            int centerX = size / 2;
            for (int y = 2; y < size - 2; y++)
            {
                float t = (float)y / (size - 4);
                int halfWidth = Mathf.RoundToInt((1f - t) * (size / 3f));
                for (int x = centerX - halfWidth; x <= centerX + halfWidth; x++)
                {
                    if (x >= 0 && x < size)
                        colors[y * size + x] = Color.white;
                }
            }

            texture.SetPixels(colors);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private void Update()
        {
            HandleInput();
            HandleResize();
            UpdatePlayerIcon();
        }

        private void HandleResize()
        {
            if (Mouse.current == null || isFullscreen) return;

            bool mouseDown = Mouse.current.leftButton.wasPressedThisFrame;
            bool mouseHeld = Mouse.current.leftButton.isPressed;
            bool mouseUp = Mouse.current.leftButton.wasReleasedThisFrame;
            Vector2 mousePos = Mouse.current.position.ReadValue();

            bool isOverHandle = IsMouseOverResizeHandle();

            // Unlock cursor when over resize handle or resizing
            if (isOverHandle || isResizing)
            {
                if (!wasOverUI)
                {
                    previousLockState = Cursor.lockState;
                    wasOverUI = true;
                }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (wasOverUI && !isResizing)
            {
                Cursor.lockState = previousLockState;
                wasOverUI = false;
            }

            // Start resize
            if (mouseDown && isOverHandle)
            {
                isResizing = true;
                resizeStartMousePos = mousePos;
                resizeStartSize = container.sizeDelta;
                Debug.Log("[Minimap] Resize started");
            }

            // During resize
            if (isResizing && mouseHeld)
            {
                Vector2 delta = mousePos - resizeStartMousePos;
                // Resize from top-right: increase width with +X, increase height with +Y
                float newSize = resizeStartSize.x + delta.x + delta.y;
                newSize = Mathf.Clamp(newSize, MIN_SIZE, MAX_SIZE);
                container.sizeDelta = new Vector2(newSize, newSize);
            }

            // End resize
            if (mouseUp && isResizing)
            {
                isResizing = false;
                normalSize = container.sizeDelta;
                Debug.Log($"[Minimap] Resize ended: {normalSize}");
            }
        }

        private bool IsMouseOverResizeHandle()
        {
            if (resizeHandle == null || Mouse.current == null) return false;

            Vector2 mousePos = Mouse.current.position.ReadValue();

            // Get canvas for proper coordinate conversion
            var canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas?.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas?.worldCamera;

            return RectTransformUtility.RectangleContainsScreenPoint(resizeHandle, mousePos, cam);
        }

        private void HandleInput()
        {
            if (Keyboard.current == null) return;

            // Toggle fullscreen
            if (Keyboard.current[settings.toggleFullscreenKey].wasPressedThisFrame)
            {
                ToggleFullscreen();
            }

            // Zoom
            if (Keyboard.current[settings.zoomInKey].wasPressedThisFrame)
            {
                ZoomIn();
            }
            if (Keyboard.current[settings.zoomOutKey].wasPressedThisFrame)
            {
                ZoomOut();
            }

            // Mouse scroll zoom when hovering
            if (Mouse.current != null && IsMouseOver())
            {
                float scroll = Mouse.current.scroll.y.ReadValue();
                if (scroll > 0) ZoomIn();
                else if (scroll < 0) ZoomOut();
            }
        }

        private bool IsMouseOver()
        {
            if (container == null || Mouse.current == null) return false;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                container, mousePos, null, out localPoint);

            return container.rect.Contains(localPoint);
        }

        private void UpdatePlayerIcon()
        {
            if (playerIcon == null || playerTransform == null) return;

            // Rotate icon to match player facing direction
            float yRotation = playerTransform.eulerAngles.y;
            playerIcon.localRotation = Quaternion.Euler(0, 0, -yRotation);
        }

        public void ZoomIn()
        {
            currentZoom = Mathf.Max(settings.minZoom, currentZoom - settings.zoomStep);
            minimapCamera?.SetZoom(currentZoom);
        }

        public void ZoomOut()
        {
            currentZoom = Mathf.Min(settings.maxZoom, currentZoom + settings.zoomStep);
            minimapCamera?.SetZoom(currentZoom);
        }

        public void ToggleFullscreen()
        {
            isFullscreen = !isFullscreen;

            if (container == null) return;

            if (isFullscreen)
            {
                // Center and enlarge
                float size = Mathf.Min(Screen.width, Screen.height) * 0.7f;
                container.anchorMin = new Vector2(0.5f, 0.5f);
                container.anchorMax = new Vector2(0.5f, 0.5f);
                container.pivot = new Vector2(0.5f, 0.5f);
                container.sizeDelta = new Vector2(size, size);
                container.anchoredPosition = Vector2.zero;
            }
            else
            {
                // Return to bottom-left corner
                container.anchorMin = new Vector2(0, 0);
                container.anchorMax = new Vector2(0, 0);
                container.pivot = new Vector2(0, 0);
                container.sizeDelta = normalSize;
                container.anchoredPosition = normalPosition;
            }
        }

        private void OnDestroy()
        {
            if (minimapCamera != null)
            {
                Destroy(minimapCamera.gameObject);
            }
        }

        /// <summary>
        /// Anchors a RectTransform to bottom-left corner (resize-safe).
        /// </summary>
        private void AnchorBottomLeft(RectTransform rt, Vector2 size, Vector2 margin)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.sizeDelta = size;
            rt.anchoredPosition = margin;
        }
    }
}
