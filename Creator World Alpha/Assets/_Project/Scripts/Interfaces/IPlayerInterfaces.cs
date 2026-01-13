using UnityEngine;

namespace CreatorWorld.Interfaces
{
    /// <summary>
    /// Interface for any entity that can take damage.
    /// </summary>
    public interface IDamageable
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsDead { get; }
        void TakeDamage(float amount, DamageType type = DamageType.Generic);
        void Heal(float amount);
    }

    /// <summary>
    /// Interface for moveable entities.
    /// </summary>
    public interface IMoveable
    {
        Vector3 Velocity { get; }
        bool IsGrounded { get; }
        bool IsMoving { get; }
        float CurrentSpeed { get; }
        void SetMovementEnabled(bool enabled);
    }

    /// <summary>
    /// Interface for weapon implementations.
    /// </summary>
    public interface IWeapon
    {
        string WeaponName { get; }
        WeaponType Type { get; }
        bool CanFire { get; }
        bool IsReloading { get; }
        int CurrentAmmo { get; }
        int MagazineSize { get; }
        int ReserveAmmo { get; }
        void TryFire();
        void StartReload();
        void CancelReload();
    }

    /// <summary>
    /// Interface for input providers.
    /// </summary>
    public interface IInputService
    {
        Vector2 MoveInput { get; }
        Vector2 LookInput { get; }
        bool JumpPressed { get; }
        bool JumpHeld { get; }
        bool SprintHeld { get; }
        bool CrouchPressed { get; }
        bool FireHeld { get; }
        bool FirePressed { get; }
        bool AimHeld { get; }
        bool ReloadPressed { get; }
        bool WeaponSwitch1Pressed { get; }
        bool WeaponSwitch2Pressed { get; }
        bool WeaponCyclePressed { get; }
        bool HolsterPressed { get; }
        bool FireModePressed { get; }
        bool SlidePressed { get; }
        bool InteractPressed { get; }
    }

    /// <summary>
    /// Interface for camera services.
    /// </summary>
    public interface ICameraService
    {
        Transform CameraTransform { get; }
        Vector3 Forward { get; }
        Vector3 Right { get; }
        bool IsAiming { get; }
        float Yaw { get; }
    }

    /// <summary>
    /// Interface for game state management.
    /// </summary>
    public interface IGameStateService
    {
        GameState CurrentState { get; }
        bool IsPlaying { get; }
        void SetGameState(GameState state);
        event System.Action<GameState> OnStateChanged;
    }

    /// <summary>
    /// Interface for audio services.
    /// </summary>
    public interface IAudioService
    {
        void PlayFootstep(FootstepType type, Vector3 position);
        void PlayWeaponSound(WeaponSoundType type, Vector3 position);
        void PlayImpact(SurfaceType surface, Vector3 position);
        void PlayUISound(UISoundType type);
        void SetMasterVolume(float volume);
        void SetSFXVolume(float volume);
        void SetMusicVolume(float volume);
    }

    // Enums moved here for shared access
    public enum DamageType
    {
        Generic,
        Bullet,
        Melee,
        Fall,
        Fire,
        Explosion,
        Starvation,
        Dehydration,
        Bleeding,
        Poison
    }

    public enum WeaponType
    {
        None = 0,
        Rifle = 1,
        Pistol = 2,
        Melee = 3
    }

    public enum GameState
    {
        MainMenu,
        Loading,
        Playing,
        Paused,
        Dead,
        Inventory,
        Building
    }

    public enum FootstepType
    {
        Walk,
        Run,
        Sprint,
        Crouch,
        Land
    }

    public enum WeaponSoundType
    {
        Fire,
        Reload,
        ReloadComplete,
        Empty,
        Equip,
        Holster
    }

    public enum SurfaceType
    {
        Default,
        Grass,
        Dirt,
        Stone,
        Wood,
        Metal,
        Water,
        Sand
    }

    public enum UISoundType
    {
        Click,
        Hover,
        Open,
        Close,
        Error,
        Success
    }

    /// <summary>
    /// Type of hitbox for damage multipliers.
    /// AAA Pattern: Separate colliders per body part for precise hit detection.
    /// </summary>
    public enum HitboxType
    {
        Body,       // 1.0x damage (default)
        Head,       // 2.0-3.0x damage (headshot)
        Limb        // 0.75x damage (arms/legs)
    }

    /// <summary>
    /// Type of hit feedback to display.
    /// Used for hit markers and audio selection.
    /// </summary>
    public enum HitFeedbackType
    {
        Normal,     // White hit marker
        Headshot,   // Yellow hit marker
        Kill        // Red kill marker
    }
}
