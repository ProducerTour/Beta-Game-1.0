using UnityEngine;
using CreatorWorld.Player;
using CreatorWorld.World;

namespace CreatorWorld.Dev
{
    /// <summary>
    /// Developer tools manager - handles noclip mode, dev menu, and debug features.
    /// Press P to toggle dev menu.
    /// </summary>
    public class DevManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerCamera playerCamera;
        [SerializeField] private ChunkManager chunkManager;

        [Header("Noclip Settings")]
        [SerializeField] private float noclipSpeed = 20f;
        [SerializeField] private float noclipSprintMultiplier = 3f;
        [SerializeField] private float noclipSmoothTime = 0.1f;

        [Header("Dev Menu")]
        [Tooltip("Press P to open dev menu, N for noclip")]

        // State
        private bool isDevMenuOpen;
        private bool isNoclipEnabled;
        private Vector3 noclipVelocity;
        private Vector3 currentVelocity;
        private CharacterController characterController;
        private Camera mainCamera;

        // Cached references
        private Transform playerTransform;
        private Transform cameraTransform;

        // Noclip saved state
        private Vector3 savedPlayerPosition;
        private bool wasCharacterControllerEnabled;

        private void Start()
        {
            // Auto-find references if not assigned
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();

            if (playerCamera == null)
                playerCamera = FindFirstObjectByType<PlayerCamera>();

            if (chunkManager == null)
                chunkManager = FindFirstObjectByType<ChunkManager>();

            if (playerController != null)
            {
                playerTransform = playerController.transform;
                characterController = playerController.GetComponent<CharacterController>();
            }

            mainCamera = Camera.main;
            if (mainCamera != null)
                cameraTransform = mainCamera.transform;

            Debug.Log("[DevManager] Initialized. Press P for dev menu, N for noclip.");
        }

        private void Update()
        {
            HandleInput();

            if (isNoclipEnabled)
            {
                UpdateNoclip();
            }
        }

        private void HandleInput()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[UnityEngine.InputSystem.Key.P].wasPressedThisFrame)
            {
                ToggleDevMenu();
            }

            if (keyboard[UnityEngine.InputSystem.Key.N].wasPressedThisFrame)
            {
                ToggleNoclip();
            }
        }

        private void ToggleDevMenu()
        {
            isDevMenuOpen = !isDevMenuOpen;
            Debug.Log($"[DevManager] Dev Menu: {(isDevMenuOpen ? "OPENED" : "CLOSED")}");

            if (isDevMenuOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Time.timeScale = 0f; // Pause game when menu open
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                Time.timeScale = 1f;
            }
        }

        public void ToggleNoclip()
        {
            isNoclipEnabled = !isNoclipEnabled;

            if (isNoclipEnabled)
            {
                EnableNoclip();
            }
            else
            {
                DisableNoclip();
            }

            Debug.Log($"[DevManager] Noclip: {(isNoclipEnabled ? "ENABLED" : "DISABLED")}");
        }

        private void EnableNoclip()
        {
            if (playerController == null || cameraTransform == null) return;

            // Save player state
            savedPlayerPosition = playerTransform.position;
            wasCharacterControllerEnabled = characterController != null && characterController.enabled;

            // Disable player movement
            playerController.SetMovementEnabled(false);

            // Disable character controller to allow free movement
            if (characterController != null)
                characterController.enabled = false;

            // Reset velocity
            noclipVelocity = Vector3.zero;
            currentVelocity = Vector3.zero;
        }

        private void DisableNoclip()
        {
            if (playerController == null) return;

            // Re-enable character controller
            if (characterController != null)
                characterController.enabled = wasCharacterControllerEnabled;

            // Re-enable player movement
            playerController.SetMovementEnabled(true);
        }

        private void UpdateNoclip()
        {
            if (cameraTransform == null || playerTransform == null) return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            // Movement input (WASD + QE for up/down)
            Vector3 moveInput = Vector3.zero;

            if (keyboard[UnityEngine.InputSystem.Key.W].isPressed) moveInput.z += 1f;
            if (keyboard[UnityEngine.InputSystem.Key.S].isPressed) moveInput.z -= 1f;
            if (keyboard[UnityEngine.InputSystem.Key.A].isPressed) moveInput.x -= 1f;
            if (keyboard[UnityEngine.InputSystem.Key.D].isPressed) moveInput.x += 1f;
            if (keyboard[UnityEngine.InputSystem.Key.E].isPressed || keyboard[UnityEngine.InputSystem.Key.Space].isPressed) moveInput.y += 1f;
            if (keyboard[UnityEngine.InputSystem.Key.Q].isPressed || keyboard[UnityEngine.InputSystem.Key.LeftCtrl].isPressed) moveInput.y -= 1f;

            // Sprint modifier
            float speedMultiplier = keyboard[UnityEngine.InputSystem.Key.LeftShift].isPressed ? noclipSprintMultiplier : 1f;

            // Calculate movement direction relative to camera
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;

            // Remove Y component for horizontal movement (keeps camera pitch from affecting horizontal speed)
            Vector3 horizontalForward = new Vector3(forward.x, 0, forward.z).normalized;
            Vector3 horizontalRight = new Vector3(right.x, 0, right.z).normalized;

            // If looking straight up/down, use camera's actual forward for movement
            if (horizontalForward.magnitude < 0.1f)
                horizontalForward = forward;

            Vector3 moveDirection = (horizontalForward * moveInput.z + horizontalRight * moveInput.x + Vector3.up * moveInput.y).normalized;

            // Target velocity
            noclipVelocity = moveDirection * noclipSpeed * speedMultiplier;

            // Smooth velocity
            currentVelocity = Vector3.SmoothDamp(currentVelocity, noclipVelocity, ref currentVelocity, noclipSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

            // Apply movement to player (moves with camera)
            playerTransform.position += currentVelocity * Time.unscaledDeltaTime;
        }

        private void OnGUI()
        {
            // Always show noclip indicator when enabled
            if (isNoclipEnabled && !isDevMenuOpen)
            {
                DrawNoclipHUD();
            }

            if (!isDevMenuOpen) return;

            // Dev menu background
            float menuWidth = 350f;
            float menuHeight = 500f;
            float menuX = (Screen.width - menuWidth) / 2f;
            float menuY = (Screen.height - menuHeight) / 2f;

            GUI.Box(new Rect(menuX, menuY, menuWidth, menuHeight), "");

            GUILayout.BeginArea(new Rect(menuX + 10, menuY + 10, menuWidth - 20, menuHeight - 20));

            // Title
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("DEV MENU", titleStyle);
            GUILayout.Space(10);

            // Current state info
            GUILayout.Label($"Press P to close | N to toggle noclip");
            GUILayout.Space(5);

            // Noclip section
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Movement", EditorLabelStyle());

            string noclipStatus = isNoclipEnabled ? "<color=lime>ENABLED</color>" : "<color=red>DISABLED</color>";
            if (GUILayout.Button($"Noclip: {(isNoclipEnabled ? "ON" : "OFF")} [N]", GUILayout.Height(30)))
            {
                ToggleNoclip();
            }

            if (isNoclipEnabled)
            {
                GUILayout.Label("WASD - Move | Q/E - Down/Up");
                GUILayout.Label("Shift - Sprint | Space - Up");
            }

            GUILayout.Label($"Speed: {noclipSpeed}");
            noclipSpeed = GUILayout.HorizontalSlider(noclipSpeed, 5f, 100f);
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Position info
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Position", EditorLabelStyle());
            if (playerTransform != null)
            {
                Vector3 pos = playerTransform.position;
                GUILayout.Label($"X: {pos.x:F1}  Y: {pos.y:F1}  Z: {pos.z:F1}");
            }

            if (GUILayout.Button("Teleport to Origin", GUILayout.Height(25)))
            {
                TeleportTo(new Vector3(0, 50, 0));
            }

            if (GUILayout.Button("Teleport to Mountain", GUILayout.Height(25)))
            {
                TeleportTo(new Vector3(500, 150, 500));
            }
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Terrain info
            if (chunkManager != null)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("Terrain", EditorLabelStyle());
                GUILayout.Label($"Active Chunks: {chunkManager.ActiveChunkCount}");

                if (playerTransform != null)
                {
                    float height = TerrainGenerator.GetHeightAt(playerTransform.position.x, playerTransform.position.z, 12345);
                    GUILayout.Label($"Ground Height: {height:F1}m");

                    var biome = TerrainGenerator.GetBiomeAt(playerTransform.position.x, playerTransform.position.z, 12345);
                    GUILayout.Label($"Biome: {biome}");

                    // Show biome weights
                    var weights = TerrainGenerator.GetBiomeWeights(playerTransform.position.x, playerTransform.position.z, 12345);
                    GUILayout.Label($"Weights: S:{weights.r:F2} G:{weights.g:F2} R:{weights.b:F2} Sn:{weights.a:F2}");
                }
                GUILayout.EndVertical();
            }

            GUILayout.FlexibleSpace();

            // Close button
            if (GUILayout.Button("Close [P]", GUILayout.Height(35)))
            {
                ToggleDevMenu();
            }

            GUILayout.EndArea();
        }

        private GUIStyle EditorLabelStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };
        }

        private void DrawNoclipHUD()
        {
            // Noclip indicator in top-left
            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f, 0.8f);
            GUI.Box(new Rect(10, 10, 180, 60), "", style);

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };

            GUI.Label(new Rect(10, 12, 180, 20), "NOCLIP ENABLED", labelStyle);
            GUI.Label(new Rect(10, 30, 180, 18), "WASD + Q/E to fly", labelStyle);
            GUI.Label(new Rect(10, 46, 180, 18), "N to disable | P for menu", labelStyle);

            GUI.backgroundColor = Color.white;

            // Position info in bottom-left
            if (playerTransform != null)
            {
                Vector3 pos = playerTransform.position;
                GUI.Label(new Rect(10, Screen.height - 30, 300, 25),
                    $"Pos: {pos.x:F0}, {pos.y:F0}, {pos.z:F0}");
            }
        }

        public void TeleportTo(Vector3 position)
        {
            if (playerTransform == null) return;

            // Disable character controller for teleport
            bool wasEnabled = false;
            if (characterController != null)
            {
                wasEnabled = characterController.enabled;
                characterController.enabled = false;
            }

            playerTransform.position = position;

            // Re-enable if it was enabled and not in noclip
            if (characterController != null && wasEnabled && !isNoclipEnabled)
            {
                characterController.enabled = true;
            }

            Debug.Log($"[DevManager] Teleported to {position}");
        }

        // Public API
        public bool IsNoclipEnabled => isNoclipEnabled;
        public bool IsDevMenuOpen => isDevMenuOpen;
    }
}
