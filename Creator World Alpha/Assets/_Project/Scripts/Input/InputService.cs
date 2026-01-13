using UnityEngine;
using UnityEngine.InputSystem;
using CreatorWorld.Core;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Input
{
    /// <summary>
    /// Unified input service. Single source of truth for all player input.
    /// Registers itself with ServiceLocator on Awake.
    ///
    /// IMPORTANT: Uses DefaultExecutionOrder(-100) to ensure this runs BEFORE
    /// all other scripts, so input values are set before anyone reads them.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class InputService : MonoBehaviour, IInputService
    {
        [Header("Settings")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private bool invertY = false;

        // Cached input state
        private Vector2 moveInput;
        private Vector2 lookInput;
        private bool jumpPressed;
        private bool jumpHeld;
        private bool sprintHeld;
        private bool crouchPressed;
        private bool fireHeld;
        private bool firePressed;
        private bool aimHeld;
        private bool reloadPressed;
        private bool weaponSwitch1Pressed;
        private bool weaponSwitch2Pressed;
        private bool weaponCyclePressed;
        private bool holsterPressed;
        private bool fireModePressed;
        private bool slidePressed;
        private bool interactPressed;

        // Interface implementation
        public Vector2 MoveInput => moveInput;
        public Vector2 LookInput => lookInput;
        public bool JumpPressed => jumpPressed;
        public bool JumpHeld => jumpHeld;
        public bool SprintHeld => sprintHeld;
        public bool CrouchPressed => crouchPressed;
        public bool FireHeld => fireHeld;
        public bool FirePressed => firePressed;
        public bool AimHeld => aimHeld;
        public bool ReloadPressed => reloadPressed;
        public bool WeaponSwitch1Pressed => weaponSwitch1Pressed;
        public bool WeaponSwitch2Pressed => weaponSwitch2Pressed;
        public bool WeaponCyclePressed => weaponCyclePressed;
        public bool HolsterPressed => holsterPressed;
        public bool FireModePressed => fireModePressed;
        public bool SlidePressed => slidePressed;
        public bool InteractPressed => interactPressed;

        private void Awake()
        {
            ServiceLocator.Register<IInputService>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<IInputService>();
        }

        private void Update()
        {
            ReadInput();
        }

        private void LateUpdate()
        {
            // Clear single-frame inputs after all systems have read them
            ClearFrameInputs();
        }

        private void ReadInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard == null) return;

            // Movement (WASD)
            float h = 0f, v = 0f;
            if (keyboard.aKey.isPressed) h -= 1f;
            if (keyboard.dKey.isPressed) h += 1f;
            if (keyboard.wKey.isPressed) v += 1f;
            if (keyboard.sKey.isPressed) v -= 1f;
            moveInput = new Vector2(h, v).normalized;

            // Look (Mouse)
            if (mouse != null)
            {
                float yMod = invertY ? -1f : 1f;
                lookInput = mouse.delta.ReadValue() * mouseSensitivity * yMod;
            }

            // Jump (Space)
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                jumpPressed = true;
            }
            jumpHeld = keyboard.spaceKey.isPressed;

            // Sprint (Shift)
            sprintHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

            // Crouch (C or Ctrl) - toggle
            if (keyboard.cKey.wasPressedThisFrame || keyboard.leftCtrlKey.wasPressedThisFrame)
            {
                crouchPressed = true;
            }

            // Fire (Left Mouse)
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    firePressed = true;
                }
                fireHeld = mouse.leftButton.isPressed;

                // Aim (Right Mouse)
                aimHeld = mouse.rightButton.isPressed;

                // Weapon cycle (Scroll wheel)
                if (Mathf.Abs(mouse.scroll.y.ReadValue()) > 0.1f)
                {
                    weaponCyclePressed = true;
                }
            }

            // Reload (R)
            if (keyboard.rKey.wasPressedThisFrame)
            {
                reloadPressed = true;
            }

            // Weapon slots (1, 2)
            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                weaponSwitch1Pressed = true;
            }
            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                weaponSwitch2Pressed = true;
            }

            // Weapon cycle (Tab)
            if (keyboard.tabKey.wasPressedThisFrame)
            {
                weaponCyclePressed = true;
            }

            // Holster (H or 3)
            if (keyboard.hKey.wasPressedThisFrame || keyboard.digit3Key.wasPressedThisFrame)
            {
                holsterPressed = true;
            }

            // Fire mode toggle (B)
            if (keyboard.bKey.wasPressedThisFrame)
            {
                fireModePressed = true;
            }

            // Slide (X)
            if (keyboard.xKey.wasPressedThisFrame)
            {
                slidePressed = true;
            }

            // Interact (E)
            if (keyboard.eKey.wasPressedThisFrame)
            {
                interactPressed = true;
            }
        }

        private void ClearFrameInputs()
        {
            // Clear all single-frame (pressed) inputs
            jumpPressed = false;
            crouchPressed = false;
            firePressed = false;
            reloadPressed = false;
            weaponSwitch1Pressed = false;
            weaponSwitch2Pressed = false;
            weaponCyclePressed = false;
            holsterPressed = false;
            fireModePressed = false;
            slidePressed = false;
            interactPressed = false;
        }

        /// <summary>
        /// Set mouse sensitivity at runtime.
        /// </summary>
        public void SetMouseSensitivity(float sensitivity)
        {
            mouseSensitivity = Mathf.Clamp(sensitivity, 0.1f, 10f);
        }

        /// <summary>
        /// Set Y-axis inversion at runtime.
        /// </summary>
        public void SetInvertY(bool invert)
        {
            invertY = invert;
        }
    }
}
