using UnityEngine;
using UnityEngine.InputSystem;
using CreatorWorld.Player;
using CreatorWorld.Player.Movement;
using CreatorWorld.Combat;
using CreatorWorld.World;

namespace CreatorWorld.Debugging
{
    /// <summary>
    /// Developer panel with toggleable debug options.
    /// Press ` (backtick/tilde) to open.
    /// </summary>
    public class DevPanel : MonoBehaviour
    {
        [Header("Panel Settings")]
        [SerializeField] private Key toggleKey = Key.F3;
        [SerializeField] private bool startOpen = true; // Start open to verify it works

        [Header("Debug Toggles")]
        [SerializeField] private bool showAnimationDebug = true;
        [SerializeField] private bool showFPS = true;
        [SerializeField] private bool showPlayerStats = false;
        [SerializeField] private bool showInputDebug = false;

        // State
        private bool isPanelOpen;
        private AnimationDebugger animDebugger;

        // FPS tracking
        private float deltaTime;
        private float fps;
        private float fpsUpdateInterval = 0.5f;
        private float fpsTimer;

        // Cached references
        private PlayerController playerController;
        private MovementHandler movementHandler;
        private WeaponInventory weaponInventory;

        // Panel dimensions
        private Rect panelRect = new Rect(10, 10, 250, 300);
        private bool isDragging;
        private Vector2 dragOffset;

        private void Start()
        {
            isPanelOpen = startOpen;
            animDebugger = GetComponent<AnimationDebugger>();
            playerController = GetComponent<PlayerController>();
            movementHandler = GetComponent<MovementHandler>();
            weaponInventory = GetComponent<WeaponInventory>();

            // Disable AnimationDebugger's own GUI - we'll control it
            if (animDebugger != null)
            {
                // We'll read its data but draw our own UI
            }
        }

        private void Update()
        {
            // Toggle panel
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                isPanelOpen = !isPanelOpen;
            }

            // Update FPS
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= fpsUpdateInterval)
            {
                fps = 1.0f / deltaTime;
                fpsTimer = 0;
            }
        }

        private void OnGUI()
        {
            // Always show FPS in corner if enabled (even when panel closed)
            if (showFPS && !isPanelOpen)
            {
                DrawFPSCorner();
            }

            // Show animation debug overlay if enabled (even when panel closed)
            if (showAnimationDebug && !isPanelOpen && animDebugger != null)
            {
                DrawAnimationOverlay();
            }

            if (!isPanelOpen) return;

            // Draw main panel
            GUI.skin.box.fontSize = 12;
            panelRect = GUI.Window(0, panelRect, DrawPanel, "Dev Panel (`)");
        }

        private void DrawPanel(int windowID)
        {
            float y = 25;
            float lineHeight = 22;
            float toggleWidth = 20;

            // FPS display at top
            GUI.Label(new Rect(10, y, 230, 20), $"FPS: {fps:F1} ({deltaTime * 1000:F1}ms)");
            y += lineHeight;

            // Separator
            GUI.Box(new Rect(10, y, 230, 2), "");
            y += 10;

            // Toggle: Animation Debug
            GUI.Label(new Rect(10, y, 180, 20), "Animation Debug");
            bool newAnimDebug = GUI.Toggle(new Rect(200, y, toggleWidth, 20), showAnimationDebug, "");
            if (newAnimDebug != showAnimationDebug)
            {
                showAnimationDebug = newAnimDebug;
                if (animDebugger != null)
                {
                    // Update the actual component
                    var field = typeof(AnimationDebugger).GetField("showOnScreen",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(animDebugger, false); // We handle drawing
                }
            }
            y += lineHeight;

            // Toggle: FPS Counter
            GUI.Label(new Rect(10, y, 180, 20), "FPS Counter");
            showFPS = GUI.Toggle(new Rect(200, y, toggleWidth, 20), showFPS, "");
            y += lineHeight;

            // Toggle: Player Stats
            GUI.Label(new Rect(10, y, 180, 20), "Player Stats");
            showPlayerStats = GUI.Toggle(new Rect(200, y, toggleWidth, 20), showPlayerStats, "");
            y += lineHeight;

            // Toggle: Input Debug
            GUI.Label(new Rect(10, y, 180, 20), "Input Debug");
            showInputDebug = GUI.Toggle(new Rect(200, y, toggleWidth, 20), showInputDebug, "");
            y += lineHeight;

            // Separator
            y += 5;
            GUI.Box(new Rect(10, y, 230, 2), "");
            y += 10;

            // Player Stats section
            if (showPlayerStats)
            {
                GUI.Label(new Rect(10, y, 230, 20), "--- Player Stats ---");
                y += lineHeight;

                if (playerController != null)
                {
                    GUI.Label(new Rect(10, y, 230, 20), $"Speed: {playerController.NormalizedSpeed:F2}");
                    y += lineHeight;
                    GUI.Label(new Rect(10, y, 230, 20), $"Sprinting: {playerController.IsSprinting}");
                    y += lineHeight;
                    GUI.Label(new Rect(10, y, 230, 20), $"Grounded: {playerController.IsGrounded}");
                    y += lineHeight;
                }

                if (movementHandler != null)
                {
                    GUI.Label(new Rect(10, y, 230, 20), $"Velocity: {movementHandler.CurrentSpeed:F1} m/s");
                    y += lineHeight;
                }

                if (weaponInventory != null)
                {
                    string weaponName = weaponInventory.CurrentWeapon != null
                        ? weaponInventory.CurrentWeapon.WeaponName
                        : "None";
                    GUI.Label(new Rect(10, y, 230, 20), $"Weapon: {weaponName}");
                    y += lineHeight;
                }
            }

            // Input Debug section
            if (showInputDebug && Keyboard.current != null)
            {
                GUI.Label(new Rect(10, y, 230, 20), "--- Input ---");
                y += lineHeight;

                bool w = Keyboard.current.wKey.isPressed;
                bool a = Keyboard.current.aKey.isPressed;
                bool s = Keyboard.current.sKey.isPressed;
                bool d = Keyboard.current.dKey.isPressed;
                GUI.Label(new Rect(10, y, 230, 20), $"WASD: [{(w?"W":" ")}{(a?"A":" ")}{(s?"S":" ")}{(d?"D":" ")}]");
                y += lineHeight;

                bool shift = Keyboard.current.leftShiftKey.isPressed;
                bool ctrl = Keyboard.current.leftCtrlKey.isPressed;
                bool space = Keyboard.current.spaceKey.isPressed;
                GUI.Label(new Rect(10, y, 230, 20), $"Shift:{shift} Ctrl:{ctrl} Space:{space}");
                y += lineHeight;
            }

            // Resize panel based on content
            panelRect.height = y + 10;

            // Make window draggable
            GUI.DragWindow(new Rect(0, 0, 10000, 25));
        }

        private void DrawFPSCorner()
        {
            Color fpsColor = fps >= 60 ? Color.green : (fps >= 30 ? Color.yellow : Color.red);

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 14;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = fpsColor;

            // Draw shadow
            GUI.color = Color.black;
            GUI.Label(new Rect(Screen.width - 79, 11, 80, 25), $"FPS: {fps:F0}", style);
            GUI.color = Color.white;

            style.normal.textColor = fpsColor;
            GUI.Label(new Rect(Screen.width - 80, 10, 80, 25), $"FPS: {fps:F0}", style);
        }

        private void DrawAnimationOverlay()
        {
            if (animDebugger == null) return;

            var animator = GetComponent<Animator>();
            if (animator == null) return;

            float speed = animator.GetFloat("Speed");
            int weaponType = animator.GetInteger("WeaponType");
            bool grounded = animator.GetBool("IsGrounded");
            bool crouching = animator.GetBool("IsCrouching");

            string weaponName = weaponType switch
            {
                0 => "Unarmed",
                1 => "Rifle",
                2 => "Pistol",
                _ => "Unknown"
            };

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            string text = $"Anim Speed: {speed:F2}\n" +
                         $"Weapon: {weaponName}\n" +
                         $"Grounded: {grounded}\n" +
                         $"Crouching: {crouching}\n" +
                         $"State: {stateInfo.shortNameHash}";

            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 12;
            style.alignment = TextAnchor.UpperLeft;
            style.normal.textColor = Color.white;
            style.padding = new RectOffset(8, 8, 8, 8);

            GUI.Box(new Rect(10, 40, 180, 110), text, style);
        }
    }
}
