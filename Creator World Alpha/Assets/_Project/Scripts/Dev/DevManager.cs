using UnityEngine;
using Unity.Netcode;
using CreatorWorld.Player;
using CreatorWorld.World;
using CreatorWorld.Network;

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

        // Scroll and sections
        private Vector2 scrollPosition;
        private bool showMultiplayer = true;
        private bool showMovement = true;
        private bool showTerrain = true;
        private bool showPlayer = false;

        // Multiplayer state
        private string joinCode = "";
        private string networkStatus = "Not connected";
        private bool servicesInitialized = false;

        // Player health reference
        private PlayerHealth playerHealth;

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

            playerHealth = FindFirstObjectByType<PlayerHealth>();
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
            float menuWidth = 380f;
            float menuHeight = 550f;
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
            GUILayout.Label("P to close | N for noclip", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(5);

            // Scrollable content
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(menuHeight - 100));

            // === MULTIPLAYER SECTION ===
            DrawSectionHeader("MULTIPLAYER", ref showMultiplayer);
            if (showMultiplayer)
            {
                DrawMultiplayerSection();
            }
            GUILayout.Space(5);

            // === MOVEMENT SECTION ===
            DrawSectionHeader("MOVEMENT", ref showMovement);
            if (showMovement)
            {
                DrawMovementSection();
            }
            GUILayout.Space(5);

            // === PLAYER SECTION ===
            DrawSectionHeader("PLAYER", ref showPlayer);
            if (showPlayer)
            {
                DrawPlayerSection();
            }
            GUILayout.Space(5);

            // === TERRAIN SECTION ===
            DrawSectionHeader("TERRAIN", ref showTerrain);
            if (showTerrain)
            {
                DrawTerrainSection();
            }

            GUILayout.EndScrollView();

            // Close button (fixed at bottom)
            GUILayout.Space(5);
            if (GUILayout.Button("Close [P]", GUILayout.Height(30)))
            {
                ToggleDevMenu();
            }

            GUILayout.EndArea();
        }

        private void DrawSectionHeader(string title, ref bool expanded)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(expanded ? "[-]" : "[+]", GUILayout.Width(30), GUILayout.Height(22)))
            {
                expanded = !expanded;
            }
            GUILayout.Label(title, GUI.skin.box, GUILayout.Height(22));
            GUILayout.EndHorizontal();
        }

        private void DrawMultiplayerSection()
        {
            GUILayout.BeginVertical(GUI.skin.box);

            if (NetworkManager.Singleton == null)
            {
                GUILayout.Label("NetworkManager not found");
                GUILayout.EndVertical();
                return;
            }

            if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            {
                GUILayout.Label($"Status: {networkStatus}");

                GUI.enabled = !servicesInitialized;
                if (GUILayout.Button(servicesInitialized ? "Services Ready" : "Initialize Services", GUILayout.Height(28)))
                {
                    InitializeNetworkServices();
                }
                GUI.enabled = true;

                if (servicesInitialized)
                {
                    GUILayout.Space(5);
                    if (GUILayout.Button("CREATE SESSION (Host)", GUILayout.Height(28)))
                    {
                        CreateNetworkSession();
                    }

                    GUILayout.Label("Join Code:");
                    joinCode = GUILayout.TextField(joinCode, GUILayout.Height(22));
                    if (GUILayout.Button("JOIN SESSION", GUILayout.Height(28)))
                    {
                        JoinNetworkSession();
                    }
                }

                GUILayout.Space(5);
                GUILayout.Label("--- Local ---");
                if (GUILayout.Button("Host (Local)", GUILayout.Height(25)))
                {
                    NetworkManager.Singleton.StartHost();
                    networkStatus = "Local host";
                }
            }
            else
            {
                string mode = NetworkManager.Singleton.IsHost ? "HOST" :
                              NetworkManager.Singleton.IsServer ? "SERVER" : "CLIENT";
                GUILayout.Label($"Connected: {mode}");
                GUILayout.Label($"Players: {NetworkManager.Singleton.ConnectedClientsIds.Count}");

                var bridge = SessionGameBridge.Instance;
                if (bridge != null && !string.IsNullOrEmpty(bridge.CurrentSessionCode))
                {
                    GUILayout.Label($"Code: {bridge.CurrentSessionCode}");
                }

                if (GUILayout.Button("DISCONNECT", GUILayout.Height(28)))
                {
                    DisconnectNetwork();
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawMovementSection()
        {
            GUILayout.BeginVertical(GUI.skin.box);

            if (GUILayout.Button($"Noclip: {(isNoclipEnabled ? "ON" : "OFF")} [N]", GUILayout.Height(28)))
            {
                ToggleNoclip();
            }

            if (isNoclipEnabled)
            {
                GUILayout.Label("WASD + Q/E | Shift = Sprint");
            }

            GUILayout.Label($"Speed: {noclipSpeed:F0}");
            noclipSpeed = GUILayout.HorizontalSlider(noclipSpeed, 5f, 100f);

            GUILayout.Space(5);
            if (playerTransform != null)
            {
                Vector3 pos = playerTransform.position;
                GUILayout.Label($"Pos: {pos.x:F0}, {pos.y:F0}, {pos.z:F0}");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Origin"))
            {
                TeleportTo(new Vector3(0, 50, 0));
            }
            if (GUILayout.Button("Mountain"))
            {
                TeleportTo(new Vector3(500, 150, 500));
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawPlayerSection()
        {
            GUILayout.BeginVertical(GUI.skin.box);

            if (playerHealth == null)
            {
                playerHealth = FindFirstObjectByType<PlayerHealth>();
            }

            if (playerHealth == null)
            {
                GUILayout.Label("PlayerHealth not found");
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label($"Health: {playerHealth.CurrentHealth:F0}/{playerHealth.MaxHealth:F0}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-10")) playerHealth.TakeDamage(10);
            if (GUILayout.Button("+10")) playerHealth.Heal(10);
            if (GUILayout.Button("Full")) playerHealth.Heal(playerHealth.MaxHealth);
            GUILayout.EndHorizontal();

            GUILayout.Label($"Hunger: {playerHealth.CurrentHunger:F0}/{playerHealth.MaxHunger:F0}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+25")) playerHealth.Eat(25);
            if (GUILayout.Button("Full")) playerHealth.Eat(playerHealth.MaxHunger);
            GUILayout.EndHorizontal();

            GUILayout.Label($"Thirst: {playerHealth.CurrentThirst:F0}/{playerHealth.MaxThirst:F0}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+25")) playerHealth.Drink(25);
            if (GUILayout.Button("Full")) playerHealth.Drink(playerHealth.MaxThirst);
            GUILayout.EndHorizontal();

            GUILayout.Label($"Stamina: {playerHealth.CurrentStamina:F0}/{playerHealth.MaxStamina:F0}");

            GUILayout.EndVertical();
        }

        private void DrawTerrainSection()
        {
            GUILayout.BeginVertical(GUI.skin.box);

            if (chunkManager != null)
            {
                GUILayout.Label($"Active Chunks: {chunkManager.ActiveChunkCount}");
            }

            if (playerTransform != null)
            {
                float height = TerrainGenerator.GetHeightAt(playerTransform.position.x, playerTransform.position.z, 12345);
                GUILayout.Label($"Ground: {height:F1}m");

                var biome = TerrainGenerator.GetBiomeAt(playerTransform.position.x, playerTransform.position.z, 12345);
                GUILayout.Label($"Biome: {biome}");

                var weights = TerrainGenerator.GetBiomeWeights(playerTransform.position.x, playerTransform.position.z, 12345);
                GUILayout.Label($"S:{weights.r:F1} G:{weights.g:F1} R:{weights.b:F1} Sn:{weights.a:F1}");
            }

            GUILayout.EndVertical();
        }

        #region Network Methods

        private async void InitializeNetworkServices()
        {
            var bridge = SessionGameBridge.Instance;
            if (bridge == null)
            {
                networkStatus = "SessionGameBridge not found";
                return;
            }

            networkStatus = "Initializing...";
            await bridge.InitializeServices();

            if (bridge.IsInitialized)
            {
                servicesInitialized = true;
                networkStatus = "Ready";
            }
            else
            {
                networkStatus = "Init failed";
            }
        }

        private async void CreateNetworkSession()
        {
            var bridge = SessionGameBridge.Instance;
            if (bridge == null) return;

            networkStatus = "Creating...";
            var code = await bridge.CreateSession();

            networkStatus = !string.IsNullOrEmpty(code) ? $"Code: {code}" : "Create failed";
        }

        private async void JoinNetworkSession()
        {
            if (string.IsNullOrEmpty(joinCode))
            {
                networkStatus = "Enter code first";
                return;
            }

            var bridge = SessionGameBridge.Instance;
            if (bridge == null) return;

            networkStatus = $"Joining {joinCode}...";
            var success = await bridge.JoinSession(joinCode.Trim().ToUpper());
            networkStatus = success ? "Joined!" : "Join failed";
        }

        private async void DisconnectNetwork()
        {
            var bridge = SessionGameBridge.Instance;
            if (bridge != null && bridge.IsInSession)
            {
                await bridge.LeaveSession();
            }
            else
            {
                NetworkManager.Singleton?.Shutdown();
            }
            networkStatus = "Disconnected";
        }

        #endregion

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
